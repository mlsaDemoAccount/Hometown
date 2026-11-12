using System.IO;
using UnityEditor;
using UnityEngine;

/// Tools > Grid > Bake. Scans the open scene and writes GridBlockData:
///  1. filler buildings (every child of the filler root object),
///  2. terrain cells that are too steep or under the water height,
///  3. your hand-painted overrides (kept on every re-bake).
/// Run it again whenever fillers, terrain or base meshes change.
public class GridBakeWindow : EditorWindow
{
    const string AssetPath = "Assets/Data/GridBlockData.asset";

    string fillerRootName = "city new bulldings";
    float maxFillerSize = 40f;          // meshes bigger than this (metres) are ignored - ground, mountains, groups
    float fillerShrink = 0.1f;          // metres cut from every side so a hairline overlap doesn't take a whole extra cell
    bool useTerrain = true;
    float slopeLimit = 35f;             // degrees
    bool useWaterHeight = true;
    float waterHeight = 0f;             // world Y; cells with terrain below this are water
    bool blockOutsideTerrain = false;

    bool paint;
    int brush;                          // 0 block, 1 free, 2 erase hand edit
    int brushSize = 1;
    bool showPreview = true;

    GridBlockData data;
    Mesh previewMesh;
    Material previewMat;
    string report = "";
    Vector2 scroll;

    [MenuItem("Tools/Grid/Bake")]
    static void Open() => GetWindow<GridBakeWindow>("Grid Bake");

    void OnEnable()
    {
        SceneView.duringSceneGui += OnScene;
        data = AssetDatabase.LoadAssetAtPath<GridBlockData>(AssetPath);
        RebuildPreview();
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnScene;
        if (previewMesh != null) DestroyImmediate(previewMesh);
        if (previewMat != null) DestroyImmediate(previewMat);
    }

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        var grid = FindFirstObjectByType<GridManager>();
        if (grid == null) { EditorGUILayout.HelpBox("Open the Gameplay scene (no GridManager found).", MessageType.Warning); EditorGUILayout.EndScrollView(); return; }
        EditorGUILayout.LabelField($"Grid {grid.width} x {grid.height}, cell {grid.cellSize} m, origin {grid.origin}");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Fillers", EditorStyles.boldLabel);
        fillerRootName = EditorGUILayout.TextField("Filler root object", fillerRootName);
        maxFillerSize = EditorGUILayout.Slider("Ignore meshes larger than (m)", maxFillerSize, 5f, 300f);
        fillerShrink = EditorGUILayout.Slider("Edge shrink (m)", fillerShrink, 0f, 0.5f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Terrain (water / mountains)", EditorStyles.boldLabel);
        useTerrain = EditorGUILayout.Toggle("Block by terrain", useTerrain);
        using (new EditorGUI.DisabledScope(!useTerrain))
        {
            slopeLimit = EditorGUILayout.Slider("Max slope (deg)", slopeLimit, 5f, 85f);
            useWaterHeight = EditorGUILayout.Toggle("Block below water height", useWaterHeight);
            using (new EditorGUI.DisabledScope(!useWaterHeight))
                waterHeight = EditorGUILayout.FloatField("Water height (world Y)", waterHeight);
            blockOutsideTerrain = EditorGUILayout.Toggle("Block cells with no terrain", blockOutsideTerrain);
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("Bake", GUILayout.Height(30))) Bake(grid);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Hand edits (kept on re-bake)", EditorStyles.boldLabel);
        showPreview = EditorGUILayout.Toggle("Show blocked cells in Scene view", showPreview);
        paint = EditorGUILayout.Toggle("Paint mode (drag in Scene view)", paint);
        brush = GUILayout.Toolbar(brush, new[] { "Block", "Free", "Erase edit" });
        brushSize = EditorGUILayout.IntSlider("Brush size (cells)", brushSize, 1, 9);
        if (data != null && GUILayout.Button("Clear all hand edits"))
        {
            Undo.RecordObject(data, "Clear grid hand edits");
            data.manual = new byte[data.width * data.height];
            Recompose();
        }

        if (!string.IsNullOrEmpty(report)) EditorGUILayout.HelpBox(report, MessageType.Info);
        EditorGUILayout.EndScrollView();
        SceneView.RepaintAll();
    }

    // ---------- Bake ----------

    void Bake(GridManager grid)
    {
        int w = grid.width, h = grid.height;
        var baseCells = new byte[w * h];
        int fillers = 0, fillerObjects = 0, terrainCells = 0, noTerrain = 0;

        // 1. Fillers
        var root = FindRoot(fillerRootName);
        if (root == null) report = $"Filler root '{fillerRootName}' not found - fillers skipped.\n";
        else
        {
            report = "";
            // Every mesh is measured on its own (a child can be a whole group of buildings, and one
            // box around a group would block everything between them). Anything larger than the
            // size limit is skipped and listed, so a ground plane / mountain can't paint the map.
            var skipped = new System.Text.StringBuilder();
            int skippedCount = 0;
            foreach (var r in root.GetComponentsInChildren<Renderer>(false))
            {
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                Bounds b = r.bounds;
                if (b.size.x > maxFillerSize || b.size.z > maxFillerSize)
                {
                    skippedCount++;
                    if (skippedCount <= 8) skipped.AppendLine($"  - {r.name} ({b.size.x:0}x{b.size.z:0} m)");
                    continue;
                }
                fillerObjects++;

                Vector2Int min = grid.WorldToGrid(new Vector3(b.min.x + fillerShrink, 0, b.min.z + fillerShrink));
                Vector2Int max = grid.WorldToGrid(new Vector3(b.max.x - fillerShrink, 0, b.max.z - fillerShrink));
                for (int x = min.x; x <= max.x; x++)
                    for (int y = min.y; y <= max.y; y++)
                    {
                        if (x < 0 || y < 0 || x >= w || y >= h) continue;
                        if (baseCells[y * w + x] == GridBlockData.Free) fillers++;
                        baseCells[y * w + x] = GridBlockData.Filler;
                    }
            }
            if (skippedCount > 0)
                report += $"Skipped {skippedCount} meshes larger than {maxFillerSize:0} m (raise the limit if they are real buildings):\n{skipped}";
        }

        // 2. Terrain
        if (useTerrain)
        {
            var terrains = Terrain.activeTerrains;
            if (terrains.Length == 0) report += "No active Terrain found - terrain step skipped.\n";
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    int i = y * w + x;
                    Vector3 p = grid.GridToWorld(new Vector2Int(x, y));
                    Terrain t = TerrainAt(terrains, p);
                    if (t == null)
                    {
                        if (terrains.Length > 0) noTerrain++;
                        if (blockOutsideTerrain && baseCells[i] == GridBlockData.Free) { baseCells[i] = GridBlockData.Terrain; terrainCells++; }
                        continue;
                    }
                    if (baseCells[i] != GridBlockData.Free) continue;

                    var td = t.terrainData;
                    Vector3 local = p - t.transform.position;
                    float nx = local.x / td.size.x, nz = local.z / td.size.z;
                    float worldY = t.transform.position.y + td.GetInterpolatedHeight(nx, nz);
                    float steep = td.GetSteepness(nx, nz);
                    bool water = useWaterHeight && worldY < waterHeight;
                    if (water || steep > slopeLimit) { baseCells[i] = GridBlockData.Terrain; terrainCells++; }
                }
        }

        // 3. Save + hand edits
        EnsureAsset();
        Undo.RecordObject(data, "Bake grid");
        if (data.width != w || data.height != h || data.manual == null || data.manual.Length != w * h)
            data.manual = new byte[w * h];
        data.width = w; data.height = h;
        data.baseCells = baseCells;
        Recompose();

        var so = new SerializedObject(grid);
        so.FindProperty("blockData").objectReferenceValue = data;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(grid);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(grid.gameObject.scene);

        report += $"Baked: {fillerObjects} filler objects -> {fillers} cells, terrain -> {terrainCells} cells" +
                  (useTerrain && Terrain.activeTerrains.Length > 0 ? $" ({noTerrain} cells have no terrain under them)" : "") +
                  $". Total blocked: {CountBlocked()}. Save the scene.";
    }

    /// Finds the filler root by name ignoring case and stray spaces (the scene object is called
    /// 'city new bulldings ' with a trailing space), including inactive objects.
    static GameObject FindRoot(string name)
    {
        string want = (name ?? "").Trim();
        foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include))
            if (t.gameObject.scene.IsValid() && string.Equals(t.name.Trim(), want, System.StringComparison.OrdinalIgnoreCase))
                return t.gameObject;
        return null;
    }

    static Terrain TerrainAt(Terrain[] terrains, Vector3 p)
    {
        foreach (var t in terrains)
        {
            Vector3 l = p - t.transform.position;
            if (l.x >= 0 && l.z >= 0 && l.x <= t.terrainData.size.x && l.z <= t.terrainData.size.z) return t;
        }
        return null;
    }

    void EnsureAsset()
    {
        if (data != null) return;
        Directory.CreateDirectory("Assets/Data");
        data = AssetDatabase.LoadAssetAtPath<GridBlockData>(AssetPath);
        if (data == null)
        {
            data = CreateInstance<GridBlockData>();
            AssetDatabase.CreateAsset(data, AssetPath);
        }
    }

    /// cells = base layer with the hand edits on top.
    void Recompose()
    {
        int n = data.width * data.height;
        if (data.cells == null || data.cells.Length != n) data.cells = new byte[n];
        for (int i = 0; i < n; i++)
        {
            byte v = i < data.baseCells.Length ? data.baseCells[i] : GridBlockData.Free;
            byte m = i < data.manual.Length ? data.manual[i] : (byte)0;
            if (m == 1) v = GridBlockData.Manual;
            else if (m == 2) v = GridBlockData.Free;
            data.cells[i] = v;
        }
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssetIfDirty(data);
        RebuildPreview();
    }

    int CountBlocked()
    {
        int c = 0;
        foreach (var v in data.cells) if (v != 0) c++;
        return c;
    }

    // ---------- Scene view: preview + paint ----------

    void RebuildPreview()
    {
        if (previewMesh != null) DestroyImmediate(previewMesh);
        previewMesh = null;
        if (data == null || data.cells == null || data.cells.Length != data.width * data.height || data.width == 0) return;

        var grid = FindFirstObjectByType<GridManager>();
        float cs = grid != null ? grid.cellSize : 1f;
        Vector3 o = grid != null ? grid.origin : Vector3.zero;

        var verts = new System.Collections.Generic.List<Vector3>();
        var cols = new System.Collections.Generic.List<Color>();
        var tris = new System.Collections.Generic.List<int>();
        for (int y = 0; y < data.height; y++)
            for (int x = 0; x < data.width; x++)
            {
                byte v = data.cells[y * data.width + x];
                if (v == 0) continue;
                Color c = v == GridBlockData.Filler ? new Color(1f, 0.2f, 0.2f, 0.45f)
                        : v == GridBlockData.Terrain ? new Color(0.2f, 0.5f, 1f, 0.45f)
                        : new Color(1f, 0.8f, 0.1f, 0.45f);
                int s = verts.Count;
                float x0 = o.x + x * cs, z0 = o.z + y * cs, yy = o.y + 0.3f;
                verts.Add(new Vector3(x0, yy, z0)); verts.Add(new Vector3(x0, yy, z0 + cs));
                verts.Add(new Vector3(x0 + cs, yy, z0 + cs)); verts.Add(new Vector3(x0 + cs, yy, z0));
                for (int k = 0; k < 4; k++) cols.Add(c);
                tris.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
            }
        previewMesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, hideFlags = HideFlags.HideAndDontSave };
        previewMesh.SetVertices(verts); previewMesh.SetColors(cols); previewMesh.SetTriangles(tris, 0);
    }

    void OnScene(SceneView view)
    {
        if (showPreview && previewMesh != null && Event.current.type == EventType.Repaint)
        {
            if (previewMat == null)
            {
                previewMat = new Material(Shader.Find("Hidden/Internal-Colored")) { hideFlags = HideFlags.HideAndDontSave };
                previewMat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
                previewMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                previewMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                previewMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            }
            previewMat.SetPass(0);
            Graphics.DrawMeshNow(previewMesh, Matrix4x4.identity);
        }

        if (!paint || data == null) return;
        var e = Event.current;
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0 && !e.alt)
        {
            var grid = FindFirstObjectByType<GridManager>();
            if (grid == null) return;
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            var plane = new Plane(Vector3.up, new Vector3(0, grid.origin.y, 0));
            if (plane.Raycast(ray, out float d))
            {
                Vector2Int c = grid.WorldToGrid(ray.GetPoint(d));
                Undo.RecordObject(data, "Paint grid");
                int r = brushSize / 2;
                for (int dx = -r; dx < brushSize - r; dx++)
                    for (int dy = -r; dy < brushSize - r; dy++)
                    {
                        int x = c.x + dx, y = c.y + dy;
                        if (x < 0 || y < 0 || x >= data.width || y >= data.height) continue;
                        data.manual[y * data.width + x] = brush == 0 ? (byte)1 : brush == 1 ? (byte)2 : (byte)0;
                    }
                Recompose();
            }
            e.Use();
        }
    }
}
