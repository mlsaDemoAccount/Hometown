#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Tools > Performance > Replace Buildings
/// Finds every building in the open scene by name (or by prefab) and puts a different prefab at the same
/// position and rotation, under the same parent. Undo works (Ctrl/Cmd+Z undoes the whole replace).
public class BuildingReplacer : EditorWindow
{
    private string buildingName = "";
    private GameObject sourcePrefab;
    private GameObject replacement;
    private bool exactName = true;
    private bool keepOriginalScale;
    private bool alignByBounds;
    private Vector3 positionOffset;
    private Vector3 rotationOffset;
    private enum OriginalAction { Delete, Disable }
    private OriginalAction original = OriginalAction.Delete;
    private bool onlyActive = true;

    private List<GameObject> matches = new List<GameObject>();
    private Vector2 scroll;

    [MenuItem("Tools/Performance/Replace Buildings")]
    private static void Open() => GetWindow<BuildingReplacer>("Replace Buildings").minSize = new Vector2(380, 460);

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("1) Type the name of the building in the scene (or drop its prefab).\n2) Drop the prefab that should take its place.\n3) Find, check the list, then Replace.", MessageType.None);

        EditorGUILayout.LabelField("Building to replace", EditorStyles.boldLabel);
        buildingName = EditorGUILayout.TextField("Name", buildingName);
        exactName = EditorGUILayout.ToggleLeft("Exact name (ignores '(1)', '(Clone)' endings). Off = name contains text", exactName);
        sourcePrefab = (GameObject)EditorGUILayout.ObjectField("...or its prefab", sourcePrefab, typeof(GameObject), false);
        onlyActive = EditorGUILayout.ToggleLeft("Only objects active in the hierarchy", onlyActive);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("New building", EditorStyles.boldLabel);
        replacement = (GameObject)EditorGUILayout.ObjectField("Replacement prefab", replacement, typeof(GameObject), false);
        keepOriginalScale = EditorGUILayout.ToggleLeft("Copy the old building's scale (off = use the new prefab's own scale)", keepOriginalScale);
        alignByBounds = EditorGUILayout.ToggleLeft("Align by size: same centre and ground level as the old building (use when pivots differ)", alignByBounds);
        positionOffset = EditorGUILayout.Vector3Field("Extra position offset", positionOffset);
        rotationOffset = EditorGUILayout.Vector3Field("Extra rotation (degrees)", rotationOffset);
        original = (OriginalAction)EditorGUILayout.EnumPopup("Old building", original);

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Find in scene", GUILayout.Height(26))) Find();
            using (new EditorGUI.DisabledScope(replacement == null || matches.Count == 0))
                if (GUILayout.Button("Replace " + matches.Count + " building(s)", GUILayout.Height(26))) Replace();
        }

        EditorGUILayout.LabelField("Found: " + matches.Count, EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (var go in matches)
        {
            if (go == null) continue;
            if (GUILayout.Button(go.name + "   (" + go.transform.position.ToString("F1") + ")", EditorStyles.miniButtonLeft))
            {
                Selection.activeGameObject = go;
                SceneView.lastActiveSceneView?.FrameSelected();
            }
        }
        EditorGUILayout.EndScrollView();
        if (matches.Count > 0 && GUILayout.Button("Select all found")) Selection.objects = matches.Cast<Object>().ToArray();
    }

    private static string Clean(string n)
    {
        n = Regex.Replace(n, @"\s*\(Clone\)$", "");
        n = Regex.Replace(n, @"\s*\(\d+\)$", "");
        n = Regex.Replace(n, @"\s+\d+$", "");   // "Name 1" copies made by Duplicate
        return n.Trim();
    }

    private void Find()
    {
        matches.Clear();
        string want = (buildingName ?? "").Trim();
        if (sourcePrefab == null && want.Length == 0) { ShowNotification(new GUIContent("Type a name or drop a prefab")); return; }

        var all = new List<GameObject>();
        var scene = EditorSceneManager.GetActiveScene();
        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(!onlyActive))
                all.Add(t.gameObject);

        var hits = new HashSet<GameObject>();
        foreach (var go in all)
        {
            if (go.hideFlags != HideFlags.None) continue;
            if (sourcePrefab != null)
            {
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(go)) continue;
                var src = PrefabUtility.GetCorrespondingObjectFromSource(go);
                if (src == null || PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go) != AssetDatabase.GetAssetPath(sourcePrefab)) continue;
                hits.Add(go);
            }
            else
            {
                bool ok = exactName
                    ? string.Equals(Clean(go.name), Clean(want), System.StringComparison.OrdinalIgnoreCase)
                    : go.name.IndexOf(want, System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (ok) hits.Add(go);
            }
        }
        // Keep only the outermost match (do not replace a child of something that is already replaced).
        matches = hits.Where(g => !HasMatchingAncestor(g, hits)).OrderBy(g => g.name).ToList();
        if (matches.Count == 0) ShowNotification(new GUIContent("Nothing found"));
    }

    private static bool HasMatchingAncestor(GameObject go, HashSet<GameObject> hits)
    {
        for (var p = go.transform.parent; p != null; p = p.parent)
            if (hits.Contains(p.gameObject)) return true;
        return false;
    }

    private void Replace()
    {
        if (replacement == null) return;
        matches.RemoveAll(m => m == null);
        if (matches.Count == 0) return;
        if (!EditorUtility.DisplayDialog("Replace buildings",
            "Replace " + matches.Count + " building(s) with '" + replacement.name + "'?\n(Undo with Cmd/Ctrl+Z)", "Replace", "Cancel")) return;

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        var created = new List<Object>();
        bool isPrefab = PrefabUtility.GetPrefabAssetType(replacement) != PrefabAssetType.NotAPrefab;

        foreach (var old in matches)
        {
            var parent = old.transform.parent;
            GameObject neo = isPrefab
                ? (GameObject)PrefabUtility.InstantiatePrefab(replacement, old.scene)
                : Instantiate(replacement);
            Undo.RegisterCreatedObjectUndo(neo, "Replace building");
            if (neo.scene != old.scene) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(neo, old.scene);

            neo.transform.SetParent(parent, true);
            neo.transform.SetPositionAndRotation(old.transform.position, old.transform.rotation * Quaternion.Euler(rotationOffset));
            if (keepOriginalScale) neo.transform.localScale = old.transform.localScale;
            neo.name = replacement.name;
            neo.transform.SetSiblingIndex(old.transform.GetSiblingIndex() + 1);
            neo.layer = old.layer;
            neo.isStatic = old.isStatic;

            if (alignByBounds && TryBounds(old, out var ob) && TryBounds(neo, out var nb))
            {
                Vector3 delta = new Vector3(ob.center.x - nb.center.x, ob.min.y - nb.min.y, ob.center.z - nb.center.z);
                neo.transform.position += delta;
            }
            neo.transform.position += positionOffset;

            if (original == OriginalAction.Delete) Undo.DestroyObjectImmediate(old);
            else { Undo.RecordObject(old, "Disable old building"); old.SetActive(false); }
            created.Add(neo);
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkAllScenesDirty();
        Selection.objects = created.ToArray();
        Debug.Log("[Replace Buildings] Replaced " + created.Count + " building(s) with '" + replacement.name + "'.");
        matches.Clear();
    }

    private static bool TryBounds(GameObject go, out Bounds bounds)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        bounds = default;
        if (rs.Length == 0) return false;
        bounds = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) bounds.Encapsulate(rs[i].bounds);
        return true;
    }
}
#endif
