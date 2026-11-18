#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Tools > Performance > Clear Batching Static (Combined + Disabled)
/// Static batching builds extra "Combined Mesh (root: scene)" copies at build time. Objects that are
/// already hand-combined (MeshCombinerTool output) or switched off gain nothing from it, so this
/// removes ONLY the Batching Static flag from those (other static flags are untouched).
/// Asks first and shows the counts; Undo (Cmd/Ctrl+Z) reverts it.
public static class ClearBatchingStaticTool
{
    [MenuItem("Tools/Performance/Clear Batching Static (Combined + Disabled)")]
    private static void Run()
    {
        var combined = new List<GameObject>();
        var disabled = new List<GameObject>();
        int totalStatic = 0;

        var scene = EditorSceneManager.GetActiveScene();
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var go = t.gameObject;
                var flags = GameObjectUtility.GetStaticEditorFlags(go);
                if ((flags & StaticEditorFlags.BatchingStatic) == 0) continue;
                totalStatic++;

                if (!go.activeInHierarchy) { disabled.Add(go); continue; }
                if (IsCombined(go)) combined.Add(go);
            }
        }

        int count = combined.Count + disabled.Count;
        string msg = "Scene '" + scene.name + "'\n\n" +
                     "Objects with Batching Static: " + totalStatic + "\n" +
                     " - already-combined meshes: " + combined.Count + "\n" +
                     " - disabled objects: " + disabled.Count + "\n\n" +
                     (count == 0 ? "Nothing to clear." : "Clear Batching Static on these " + count + " object(s)?\n(Undo with Cmd/Ctrl+Z)");
        if (count == 0) { EditorUtility.DisplayDialog("Clear Batching Static", msg, "OK"); return; }
        if (!EditorUtility.DisplayDialog("Clear Batching Static", msg, "Clear", "Cancel")) return;

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        foreach (var go in combined) Clear(go);
        foreach (var go in disabled) Clear(go);
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[Clear Batching Static] Cleared " + combined.Count + " combined + " + disabled.Count + " disabled object(s). " +
                  (totalStatic - count) + " object(s) still use static batching. Save the scene and rebuild to see the size change.");
    }

    private static void Clear(GameObject go)
    {
        Undo.RegisterCompleteObjectUndo(go, "Clear Batching Static");
        var flags = GameObjectUtility.GetStaticEditorFlags(go);
        GameObjectUtility.SetStaticEditorFlags(go, flags & ~StaticEditorFlags.BatchingStatic);
    }

    /// Output of MeshCombinerTool: object or mesh named "Combined_*" (mesh saved as Assets/Combined_*.asset).
    private static bool IsCombined(GameObject go)
    {
        if (go.name.StartsWith("Combined_")) return true;
        var mf = go.GetComponent<MeshFilter>();
        return mf != null && mf.sharedMesh != null && mf.sharedMesh.name.StartsWith("Combined_");
    }
}
#endif
