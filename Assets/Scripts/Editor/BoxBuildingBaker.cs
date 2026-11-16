#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Tools > Performance > Bake Box Building.
/// Turns a detailed building (e.g. apartment.fbx) into a 10-triangle box with the building's four walls and
/// roof photographed onto ONE texture atlas. Same look from the game camera, a tiny fraction of the cost.
///
/// How: the source is placed in a temporary scene, every material is swapped for a flat unlit copy (so no
/// lighting is baked into the picture), an orthographic camera captures +Z, -Z, +X, -X and the top, the five
/// pictures are packed into one atlas, and a box mesh + material + prefab are written next to the source.
/// Nothing in your open scene or the source model is changed.
public class BoxBuildingBaker : EditorWindow
{
    GameObject source;
    float fillFraction = 1.0f;   // 1 = faces sized to roughly fill the atlas; resolution is chosen automatically
    int atlasSize = 2048;
    bool addCollider = true;
    bool makeNormalMap = true;
    float normalStrength = 1f;
    bool makeAoMap = true;        // separate AO texture, adjustable live on the material
    float aoReach = 1f;           // 1 = default size of the shadow around recesses
    string outputFolder = "";
    string status = "";

    const int TempLayer = 31;

    [MenuItem("Tools/Performance/Bake Box Building")]
    static void Open()
    {
        var w = GetWindow<BoxBuildingBaker>("Box Building");
        if (w.source == null)
            w.source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/city map/Building/apartment/apartment.fbx");
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox("Drag the building model (project asset, or an object from the scene) here. It is photographed from 4 sides + top and replaced by a box with that texture. Original is never modified.", MessageType.Info);
        source = (GameObject)EditorGUILayout.ObjectField("Source building", source, typeof(GameObject), true);
        fillFraction = EditorGUILayout.Slider("Atlas fill (resolution)", fillFraction, 0.3f, 1.5f);
        atlasSize = EditorGUILayout.IntPopup("Max atlas size", atlasSize, new[] { "1024", "2048", "4096" }, new[] { 1024, 2048, 4096 });
        makeAoMap = EditorGUILayout.Toggle("Separate AO map (live bar)", makeAoMap);
        aoReach = EditorGUILayout.Slider("AO reach", aoReach, 0.3f, 3f);
        makeNormalMap = EditorGUILayout.Toggle("Generate normal map", makeNormalMap);
        using (new EditorGUI.DisabledScope(!makeNormalMap))
            normalStrength = EditorGUILayout.Slider("Normal strength", normalStrength, 0.2f, 4f);
        addCollider = EditorGUILayout.Toggle("Add box collider", addCollider);
        outputFolder = EditorGUILayout.TextField("Output folder (blank = next to source)", outputFolder);
        using (new EditorGUI.DisabledScope(source == null))
            if (GUILayout.Button("Bake box building", GUILayout.Height(32))) Bake();
        if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.None);
        DrawLiveAo();
    }

    // Live AO bar: edits the baked box material directly, no re-bake needed.
    void DrawLiveAo()
    {
        if (source == null) return;
        string path = $"{OutFolder()}/{source.name}_Box.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null || !mat.HasProperty("_OcclusionStrength") || mat.GetTexture("_OcclusionMap") == null) return;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Live adjustment (no re-bake)", EditorStyles.boldLabel);
        float cur = mat.GetFloat("_OcclusionStrength");
        float v = EditorGUILayout.Slider("AO strength", cur, 0f, 1f);
        if (!Mathf.Approximately(v, cur))
        {
            mat.SetFloat("_OcclusionStrength", v);
            EditorUtility.SetDirty(mat);
            SceneView.RepaintAll();
        }
    }

    string OutFolder()
    {
        string srcPath = AssetDatabase.GetAssetPath(source);
        return !string.IsNullOrEmpty(outputFolder) ? outputFolder
            : (!string.IsNullOrEmpty(srcPath) ? Path.GetDirectoryName(srcPath).Replace('\\', '/') + "/Optimized" : "Assets/Optimized");
    }

    void Bake()
    {
        string srcPath = AssetDatabase.GetAssetPath(source);
        string folder = !string.IsNullOrEmpty(outputFolder) ? outputFolder
            : (!string.IsNullOrEmpty(srcPath) ? Path.GetDirectoryName(srcPath).Replace('\\', '/') + "/Optimized" : "Assets/Optimized");
        EnsureFolder(folder);
        string baseName = source.name + "_Box";

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var tempMats = new List<Material>();
        var tempTextures = new List<Texture2D>();
        var tempNormals = new List<Texture2D>();
        var tempAo = new List<Texture2D>();
        GameObject instance = null, camGo = null;
        RenderTexture rt = null;
        try
        {
            // ---- 1. temp copy with flat unlit materials, LOD0 only, at the origin ----
            instance = Object.Instantiate(source);
            SceneManager.MoveGameObjectToScene(instance, scene);
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;
            instance.SetActive(true);

            foreach (var lod in instance.GetComponentsInChildren<LODGroup>(true))
            {
                var keep = new HashSet<Renderer>();
                var lods = lod.GetLODs();
                if (lods.Length > 0) foreach (var r in lods[0].renderers) if (r != null) keep.Add(r);
                foreach (var r in lod.GetComponentsInChildren<Renderer>(true)) if (!keep.Contains(r)) r.enabled = false;
            }

            var renderers = instance.GetComponentsInChildren<MeshRenderer>(false).Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
            if (renderers.Length == 0) { status = "No mesh renderers found in the source."; return; }

            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var unlitMap = new Dictionary<MeshRenderer, Material[]>();
            Material depthMat = null;
            var depthShader = Shader.Find("Hidden/BoxBakeDepth");
            if (depthShader != null) { depthMat = new Material(depthShader); tempMats.Add(depthMat); }
            foreach (var r in instance.GetComponentsInChildren<Transform>(true)) r.gameObject.layer = TempLayer;
            foreach (var r in renderers)
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    var t = new Material(unlit);
                    Texture tex = FindAlbedo(m, out string texProp);
                    Color col = FindColor(m);
                    t.SetTexture("_BaseMap", tex);
                    t.SetColor("_BaseColor", col);
                    if (tex != null) { t.SetTextureScale("_BaseMap", m.GetTextureScale(texProp)); t.SetTextureOffset("_BaseMap", m.GetTextureOffset(texProp)); }
                    tempMats.Add(t);
                    mats[i] = t;
                }
                r.sharedMaterials = mats;
                unlitMap[r] = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            Vector3 c = b.center, s = b.size;

            // ---- 2. camera ----
            camGo = new GameObject("BakeCam");
            SceneManager.MoveGameObjectToScene(camGo, scene);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.55f, 0.55f, 1f);
            cam.cullingMask = 1 << TempLayer;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            cam.nearClipPlane = 0.01f;

            // Resolution is automatic: pick the pixel density so the 5 faces together cover ~65% of the
            // atlas area (before packing/padding), whatever the model's real-world scale is.
            float totalArea = 2f * s.x * s.y + 2f * s.z * s.y + s.x * s.z;
            float pixelsPerMeter = Mathf.Sqrt(0.65f * atlasSize * atlasSize * fillFraction / Mathf.Max(totalArea, 1e-6f));

            // face definitions: outward normal, camera-up, width, height, depth (distance across the box)
            var faces = new[]
            {
                new Face(Vector3.forward, Vector3.up, s.x, s.y, s.z),
                new Face(Vector3.back,    Vector3.up, s.x, s.y, s.z),
                new Face(Vector3.right,   Vector3.up, s.z, s.y, s.x),
                new Face(Vector3.left,    Vector3.up, s.z, s.y, s.x),
                new Face(Vector3.up,      Vector3.forward, s.x, s.z, s.y),
            };

            foreach (var f in faces)
            {
                int w = Mathf.Clamp(Mathf.RoundToInt(f.width * pixelsPerMeter), 16, 4096);
                int h = Mathf.Clamp(Mathf.RoundToInt(f.height * pixelsPerMeter), 16, 4096);
                if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
                rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
                camGo.transform.position = c + f.normal * (f.depth * 0.5f + 5f);
                camGo.transform.rotation = Quaternion.LookRotation(-f.normal, f.up);
                cam.orthographicSize = f.height * 0.5f;
                cam.aspect = (float)w / h;
                cam.farClipPlane = f.depth + 20f;
                cam.targetTexture = rt;
                var onBlack = Capture(cam, rt, Color.black);
                var onWhite = Capture(cam, rt, Color.white);
                var img = new Texture2D(w, h, TextureFormat.RGBA32, false);
                var pb = onBlack.GetPixels32(); var pw = onWhite.GetPixels32();
                var outPx = new Color32[pb.Length];
                long sr = 0, sg = 0, sb = 0, cnt = 0;
                for (int i = 0; i < pb.Length; i++)
                {
                    // pixel on black = c*a ; pixel on white = c*a + (1-a)  =>  a = 1 - (white - black)
                    float diff = ((pw[i].r - pb[i].r) + (pw[i].g - pb[i].g) + (pw[i].b - pb[i].b)) / (3f * 255f);
                    float al = Mathf.Clamp01(1f - diff);
                    if (al < 0.5f) { outPx[i] = new Color32(0, 0, 0, 0); continue; }
                    float inv = 1f / al;
                    byte rr = (byte)Mathf.Clamp(pb[i].r * inv, 0, 255), gg = (byte)Mathf.Clamp(pb[i].g * inv, 0, 255), bb = (byte)Mathf.Clamp(pb[i].b * inv, 0, 255);
                    outPx[i] = new Color32(rr, gg, bb, 255);
                    sr += rr; sg += gg; sb += bb; cnt++;
                }
                // ---- baked ambient occlusion from a depth capture ----
                if (depthMat != null)
                {
                    foreach (var kv in unlitMap) kv.Key.sharedMaterials = Enumerable.Repeat(depthMat, kv.Value.Length).ToArray();
                    depthMat.SetFloat("_Offset", 5f);
                    depthMat.SetFloat("_Range", f.depth);
                    var rtDepth = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                    cam.targetTexture = rtDepth;
                    var dTex = Capture(cam, rtDepth, Color.white);
                    cam.targetTexture = rt;
                    rtDepth.Release(); Object.DestroyImmediate(rtDepth);
                    foreach (var kv in unlitMap) kv.Key.sharedMaterials = kv.Value;

                    var dp = dTex.GetPixels32();
                    var depth = new float[dp.Length];
                    for (int i = 0; i < dp.Length; i++) depth[i] = (dp[i].r + dp[i].g / 255f) / 255f * f.depth;   // model units from the front plane
                    Object.DestroyImmediate(dTex);

                    // Where is the real wall? Histogram of depths over the opaque pixels; take the SHALLOWEST big peak
                    // (balconies / AC units / roof props are small, window recesses are deeper), so the box wall sits
                    // on the actual facade instead of on the outermost protrusion.
                    const int bins = 512;
                    var hist = new int[bins];
                    for (int i = 0; i < depth.Length; i++)
                        if (outPx[i].a != 0) hist[Mathf.Clamp((int)(depth[i] / f.depth * bins), 0, bins - 1)]++;
                    int peak = 0; foreach (int hc in hist) peak = Mathf.Max(peak, hc);
                    int wallBin = 0;
                    if (f.normal == Vector3.up)
                    {
                        // roof: the biggest flat area is the roof slab itself (towers / parapets are small and higher)
                        int best = 0;
                        for (int bi = 0; bi < bins; bi++) if (hist[bi] > best) { best = hist[bi]; wallBin = bi; }
                    }
                    else
                        for (int bi = 0; bi < bins; bi++) if (hist[bi] >= peak * 0.35f) { wallBin = bi; break; }
                    f.inset = Mathf.Min((wallBin + 0.5f) / bins * f.depth, f.depth * 0.4f);

                    float unit = s.y * aoReach;                      // scale of the effect follows the building height
                    int r1 = Mathf.Max(2, Mathf.RoundToInt(0.02f * unit * pixelsPerMeter));
                    int r2 = Mathf.Max(4, Mathf.RoundToInt(0.08f * unit * pixelsPerMeter));
                    var b1 = BoxBlur(depth, w, h, r1);
                    var b2 = BoxBlur(depth, w, h, r2);
                    float range = 0.04f * unit;
                    var aoPx = new Color32[outPx.Length];
                    for (int i = 0; i < aoPx.Length; i++) aoPx[i] = new Color32(255, 255, 255, 255);
                    if (makeAoMap)
                    for (int i = 0; i < outPx.Length; i++)
                    {
                        if (outPx[i].a == 0) continue;
                        float occ = 0.6f * Mathf.Clamp01((depth[i] - b1[i]) / range) + 0.4f * Mathf.Clamp01((depth[i] - b2[i]) / (range * 2f));
                        if (makeAoMap) { byte av = (byte)((1f - Mathf.Clamp01(occ)) * 255f); aoPx[i] = new Color32(av, av, av, 255); }
                    }


                    if (makeAoMap)
                    {
                        var aoImg = new Texture2D(w, h, TextureFormat.RGBA32, false);
                        aoImg.SetPixels32(aoPx);
                        aoImg.Apply();
                        tempAo.Add(aoImg);
                    }

                    // ---- normal map from the same depth (tangent = camera right, bitangent = camera up) ----
                    if (makeNormalMap)
                    {
                        var hgt = BoxBlur(depth, w, h, 1);
                        var npx = new Color32[outPx.Length];
                        float slopeScale = normalStrength * pixelsPerMeter * 0.5f;
                        float Sample(int xx, int yy, float fallback)
                        {
                            xx = Mathf.Clamp(xx, 0, w - 1); yy = Mathf.Clamp(yy, 0, h - 1);
                            int j = yy * w + xx;
                            return outPx[j].a == 0 ? fallback : hgt[j];
                        }
                        for (int y = 0; y < h; y++)
                            for (int x = 0; x < w; x++)
                            {
                                int i = y * w + x;
                                if (outPx[i].a == 0) { npx[i] = new Color32(128, 128, 255, 255); continue; }
                                float dc = hgt[i];
                                float nx = (Sample(x + 1, y, dc) - Sample(x - 1, y, dc)) * slopeScale;
                                float ny = (Sample(x, y + 1, dc) - Sample(x, y - 1, dc)) * slopeScale;
                                float inv = 1f / Mathf.Sqrt(nx * nx + ny * ny + 1f);
                                npx[i] = new Color32((byte)((nx * inv * 0.5f + 0.5f) * 255f), (byte)((ny * inv * 0.5f + 0.5f) * 255f), (byte)((inv * 0.5f + 0.5f) * 255f), 255);
                            }
                        var nimg = new Texture2D(w, h, TextureFormat.RGBA32, false);
                        nimg.SetPixels32(npx);
                        nimg.Apply();
                        tempNormals.Add(nimg);
                    }
                }

                // Close thin transparent strips at the picture's edges (recessed building corners): extend the nearest
                // opaque pixel outward to the edge, so neighbouring walls meet without see-through slits.
                {
                    int mx = Mathf.Max(3, (int)(w * 0.06f)), my = Mathf.Max(3, (int)(h * 0.04f));
                    for (int y = 0; y < h; y++)
                    {
                        int row = y * w, x0 = -1, x1 = -1;
                        for (int x = 0; x < w; x++) if (outPx[row + x].a != 0) { if (x0 < 0) x0 = x; x1 = x; }
                        if (x0 < 0) continue;
                        if (x0 <= mx) for (int x = 0; x < x0; x++) outPx[row + x] = outPx[row + x0];
                        if (x1 >= w - 1 - mx) for (int x = x1 + 1; x < w; x++) outPx[row + x] = outPx[row + x1];
                    }
                    for (int x = 0; x < w; x++)
                    {
                        int y0 = -1, y1 = -1;
                        for (int y = 0; y < h; y++) if (outPx[y * w + x].a != 0) { if (y0 < 0) y0 = y; y1 = y; }
                        if (y0 < 0) continue;
                        if (y0 <= my) for (int y = 0; y < y0; y++) outPx[y * w + x] = outPx[y0 * w + x];
                        if (y1 >= h - 1 - my) for (int y = y1 + 1; y < h; y++) outPx[y * w + x] = outPx[y1 * w + x];
                    }
                }

                // empty pixels get the average building colour (alpha stays 0) so edges don't get dark/grey fringes
                if (cnt > 0)
                {
                    var avg = new Color32((byte)(sr / cnt), (byte)(sg / cnt), (byte)(sb / cnt), 0);
                    for (int i = 0; i < outPx.Length; i++) if (outPx[i].a == 0) outPx[i] = avg;
                }
                img.SetPixels32(outPx);
                img.Apply();
                Object.DestroyImmediate(onBlack); Object.DestroyImmediate(onWhite);
                tempTextures.Add(img);
                f.right = camGo.transform.right;
                f.upVec = camGo.transform.up;
            }

            // ---- 3. atlas ----
            var atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Rect[] rects = atlas.PackTextures(tempTextures.ToArray(), 8, atlasSize, false);
            string atlasPath = $"{folder}/{baseName}_Atlas.png";
            File.WriteAllBytes(atlasPath, atlas.EncodeToPNG());
            Object.DestroyImmediate(atlas);
            AssetDatabase.ImportAsset(atlasPath, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(atlasPath);
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = true;
            imp.mipmapEnabled = true;
            imp.alphaSource = TextureImporterAlphaSource.FromInput;
            imp.alphaIsTransparency = true;
            imp.mipMapsPreserveCoverage = true;
            imp.alphaTestReferenceValue = 0.5f;
            imp.maxTextureSize = atlasSize;
            foreach (var platform in new[] { "Android", "iPhone" })
            {
                var ps = imp.GetPlatformTextureSettings(platform);
                ps.overridden = true;
                ps.maxTextureSize = atlasSize;
                ps.format = TextureImporterFormat.ASTC_6x6;
                imp.SetPlatformTextureSettings(ps);
            }
            imp.SaveAndReimport();

            string normalPath = null;
            if (makeNormalMap && tempNormals.Count == tempTextures.Count)
            {
                var nAtlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                Rect[] nRects = nAtlas.PackTextures(tempNormals.ToArray(), 8, atlasSize, false);
                for (int i = 0; i < nRects.Length; i++)
                    if (Mathf.Abs(nRects[i].x - rects[i].x) > 0.002f || Mathf.Abs(nRects[i].y - rects[i].y) > 0.002f || Mathf.Abs(nRects[i].width - rects[i].width) > 0.002f)
                        Debug.LogWarning("[BoxBuildingBaker] Normal atlas layout differs from the colour atlas - normals may be misaligned. Bake again or lower 'Atlas fill'.");
                normalPath = $"{folder}/{baseName}_Normal.png";
                File.WriteAllBytes(normalPath, nAtlas.EncodeToPNG());
                Object.DestroyImmediate(nAtlas);
                AssetDatabase.ImportAsset(normalPath, ImportAssetOptions.ForceUpdate);
                var nImp = (TextureImporter)AssetImporter.GetAtPath(normalPath);
                nImp.textureType = TextureImporterType.NormalMap;
                nImp.mipmapEnabled = true;
                nImp.maxTextureSize = atlasSize;
                foreach (var platform in new[] { "Android", "iPhone" })
                {
                    var ps = nImp.GetPlatformTextureSettings(platform);
                    ps.overridden = true;
                    ps.maxTextureSize = atlasSize;
                    ps.format = TextureImporterFormat.ASTC_6x6;
                    nImp.SetPlatformTextureSettings(ps);
                }
                nImp.SaveAndReimport();
            }

            string aoPath = null;
            if (makeAoMap && tempAo.Count == tempTextures.Count)
            {
                var aAtlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                Rect[] aRects = aAtlas.PackTextures(tempAo.ToArray(), 8, atlasSize, false);
                for (int i = 0; i < aRects.Length; i++)
                    if (Mathf.Abs(aRects[i].x - rects[i].x) > 0.002f || Mathf.Abs(aRects[i].y - rects[i].y) > 0.002f || Mathf.Abs(aRects[i].width - rects[i].width) > 0.002f)
                        Debug.LogWarning("[BoxBuildingBaker] AO atlas layout differs from the colour atlas - AO may be misaligned. Bake again.");
                aoPath = $"{folder}/{baseName}_AO.png";
                File.WriteAllBytes(aoPath, aAtlas.EncodeToPNG());
                Object.DestroyImmediate(aAtlas);
                AssetDatabase.ImportAsset(aoPath, ImportAssetOptions.ForceUpdate);
                var aImp = (TextureImporter)AssetImporter.GetAtPath(aoPath);
                aImp.textureType = TextureImporterType.Default;
                aImp.sRGBTexture = false;
                aImp.mipmapEnabled = true;
                aImp.maxTextureSize = atlasSize;
                foreach (var platform in new[] { "Android", "iPhone" })
                {
                    var ps = aImp.GetPlatformTextureSettings(platform);
                    ps.overridden = true;
                    ps.maxTextureSize = atlasSize;
                    ps.format = TextureImporterFormat.ASTC_6x6;
                    aImp.SetPlatformTextureSettings(ps);
                }
                aImp.SaveAndReimport();
            }

            // ---- 4. box mesh (5 quads = 10 triangles, no bottom) ----
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            // The box is the bounding box pulled in to the real wall surface on each side.
            Vector3 coreMin = b.min, coreMax = b.max;
            coreMax.z -= faces[0].inset; coreMin.z += faces[1].inset;
            coreMax.x -= faces[2].inset; coreMin.x += faces[3].inset;
            coreMax.y -= faces[4].inset;
            Vector3 cc = (coreMin + coreMax) * 0.5f, cs = coreMax - coreMin;
            for (int i = 0; i < faces.Length; i++)
            {
                var f = faces[i];
                float halfN = Mathf.Abs(Vector3.Dot(f.normal, cs)) * 0.5f;
                float halfR = Mathf.Abs(Vector3.Dot(f.right, cs)) * 0.5f;
                float halfU = Mathf.Abs(Vector3.Dot(f.upVec, cs)) * 0.5f;
                Vector3 centre = cc + f.normal * halfN;
                var corners = new[]
                {
                    centre - f.right * halfR - f.upVec * halfU, centre - f.right * halfR + f.upVec * halfU,
                    centre + f.right * halfR + f.upVec * halfU, centre + f.right * halfR - f.upVec * halfU
                };
                Rect rc = rects[i];
                int o = verts.Count;
                foreach (var pt in corners)
                {
                    verts.Add(pt); norms.Add(f.normal);
                    // where this corner sits inside the photographed face (the photo covers the full bounding box)
                    float fu = Vector3.Dot(pt - c, f.right) / f.width + 0.5f;
                    float fv = Vector3.Dot(pt - c, f.upVec) / f.height + 0.5f;
                    uvs.Add(new Vector2(rc.xMin + fu * rc.width, rc.yMin + fv * rc.height));
                }
                tris.AddRange(new[] { o, o + 1, o + 2, o, o + 2, o + 3 });
            }
            var mesh = new Mesh { name = baseName };
            mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            string meshPath = $"{folder}/{baseName}.asset";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);

            // ---- 5. material + prefab ----
            string matPath = $"{folder}/{baseName}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, matPath); }
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.1f);
            mat.SetFloat("_Metallic", 0f);
            if (aoPath != null)
            {
                mat.SetTexture("_OcclusionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(aoPath));
                mat.SetFloat("_OcclusionStrength", 1f);
                mat.EnableKeyword("_OCCLUSIONMAP");
            }
            else { mat.SetTexture("_OcclusionMap", null); mat.DisableKeyword("_OCCLUSIONMAP"); }
            if (normalPath != null)
            {
                mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
                mat.SetFloat("_BumpScale", 1f);
                mat.EnableKeyword("_NORMALMAP");
            }
            else { mat.SetTexture("_BumpMap", null); mat.DisableKeyword("_NORMALMAP"); }
            // alpha cutout: transparent corners of the picture are not drawn (no sorting cost like real transparency)
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", 0.5f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.SetOverrideTag("RenderType", "TransparentCutout");
            mat.renderQueue = 2450;
            EditorUtility.SetDirty(mat);

            var root = new GameObject(baseName);
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            root.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (addCollider) { var bc = root.AddComponent<BoxCollider>(); bc.center = b.center; bc.size = b.size; }
            string prefabPath = $"{folder}/{baseName}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            status = $"Wall insets (+Z -Z +X -X top): {faces[0].inset:0.###} {faces[1].inset:0.###} {faces[2].inset:0.###} {faces[3].inset:0.###} {faces[4].inset:0.###}\nDone. {tris.Count / 3} triangles. Size {s.x:0.#} x {s.y:0.#} x {s.z:0.#} (W x H x D).\nPrefab: {prefabPath}\nAtlas: {atlasPath}";
            Debug.Log("[BoxBuildingBaker] " + status);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }
        finally
        {
            foreach (var m in tempMats) Object.DestroyImmediate(m);
            foreach (var t in tempTextures) Object.DestroyImmediate(t);
            foreach (var t in tempNormals) Object.DestroyImmediate(t);
            foreach (var t in tempAo) Object.DestroyImmediate(t);
            if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
            RenderTexture.active = null;
            EditorSceneManager.CloseScene(scene, true);
            Repaint();
        }
    }


    // Different shaders / FBX-converted shader graphs name their colour texture differently
    // (_BaseMap, _MainTex, _BASE_COLOR_MAP, ...). Try the known names, then any "colour/albedo/diffuse" texture,
    // then the first non-empty texture that is not obviously a normal/mask/emission map.
    static Texture FindAlbedo(Material m, out string prop)
    {
        prop = null;
        foreach (var n in new[] { "_BaseMap", "_MainTex", "_BASE_COLOR_MAP", "_BaseColorMap", "_Albedo", "_Diffuse", "_DiffuseMap" })
            if (m.HasProperty(n) && m.GetTexture(n) != null) { prop = n; return m.GetTexture(n); }
        var names = m.GetTexturePropertyNames();
        foreach (var n in names)
        {
            string u = n.ToUpperInvariant();
            if ((u.Contains("BASE") || u.Contains("ALBEDO") || u.Contains("DIFFUSE") || u.Contains("COLOR")) && !u.Contains("EMISSION") && !u.Contains("REFLECT") && m.GetTexture(n) != null)
                { prop = n; return m.GetTexture(n); }
        }
        foreach (var n in names)
        {
            string u = n.ToUpperInvariant();
            if (u.Contains("BUMP") || u.Contains("NORMAL") || u.Contains("MASK") || u.Contains("EMISSION") || u.Contains("LIGHTMAP") || u.Contains("METAL") || u.Contains("ROUGH") || u.Contains("OCCLUSION") || u.Contains("TRANSPARENCY") || u.Contains("REFLECT")) continue;
            if (m.GetTexture(n) != null) { prop = n; return m.GetTexture(n); }
        }
        return null;
    }

    static Color FindColor(Material m)
    {
        foreach (var n in new[] { "_BaseColor", "_Color", "_BASE_COLOR" })
            if (m.HasProperty(n)) return m.GetColor(n);
        return Color.white;
    }

    // Separable box blur using running sums, clamped at the edges.
    static float[] BoxBlur(float[] src, int w, int h, int r)
    {
        var tmp = new float[src.Length];
        var dst = new float[src.Length];
        for (int y = 0; y < h; y++)
        {
            int row = y * w;
            float sum = 0f;
            for (int x = -r; x <= r; x++) sum += src[row + Mathf.Clamp(x, 0, w - 1)];
            for (int x = 0; x < w; x++)
            {
                tmp[row + x] = sum / (2 * r + 1);
                sum += src[row + Mathf.Min(x + r + 1, w - 1)] - src[row + Mathf.Max(x - r, 0)];
            }
        }
        for (int x = 0; x < w; x++)
        {
            float sum = 0f;
            for (int y = -r; y <= r; y++) sum += tmp[Mathf.Clamp(y, 0, h - 1) * w + x];
            for (int y = 0; y < h; y++)
            {
                dst[y * w + x] = sum / (2 * r + 1);
                sum += tmp[Mathf.Min(y + r + 1, h - 1) * w + x] - tmp[Mathf.Max(y - r, 0) * w + x];
            }
        }
        return dst;
    }

    static Texture2D Capture(Camera cam, RenderTexture rt, Color background)
    {
        cam.backgroundColor = background;
        cam.Render();
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var t = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
        t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        t.Apply();
        RenderTexture.active = prev;
        return t;
    }

    class Face
    {
        public Vector3 normal, up, right, upVec;
        public float width, height, depth, inset;
        public Face(Vector3 n, Vector3 u, float w, float h, float d) { normal = n; up = u; width = w; height = h; depth = d; }
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
#endif
