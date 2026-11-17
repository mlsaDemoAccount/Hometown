#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Copies the hand-tuned "Host" and "Join" panels from the open MainMenu scene into the Gameplay scene
/// (exact copy: sprites, positions, scales, fonts) and re-links the Gameplay MultiplayerTestUI to the copies.
public static class ConnectionUiCopier
{
    private const string TargetScenePath = "Assets/Scenes/Gameplay.unity";

    [MenuItem("Hometown/UI/Copy Host & Join from MainMenu to Gameplay")]
    public static void Copy()
    {
        // Source = the MultiplayerTestUI in the open MainMenu scene.
        MultiplayerTestUI src = null;
        for (int i = 0; i < SceneManager.sceneCount && src == null; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (!s.isLoaded || s.path == TargetScenePath) continue;
            foreach (var r in s.GetRootGameObjects())
            {
                src = r.GetComponentInChildren<MultiplayerTestUI>(true);
                if (src != null && src.transform.Find("Multiplayer Canvas/Host") != null) break;
                src = null;
            }
        }
        if (src == null) { Debug.LogError("[Connection UI] Open the MainMenu scene (with the styled Host/Join) first."); return; }

        var srcCanvas = src.transform.Find("Multiplayer Canvas");
        var srcHost = srcCanvas.Find("Host");
        var srcJoin = srcCanvas.Find("Join");

        bool wasOpen = SceneManager.GetSceneByPath(TargetScenePath).isLoaded;
        var scene = wasOpen ? SceneManager.GetSceneByPath(TargetScenePath)
                            : EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Additive);

        MultiplayerTestUI dst = null;
        foreach (var r in scene.GetRootGameObjects())
        {
            dst = r.GetComponentInChildren<MultiplayerTestUI>(true);
            if (dst != null) break;
        }
        if (dst == null || dst.transform.Find("Multiplayer Canvas") == null)
        {
            Debug.LogError("[Connection UI] Gameplay has no MultiplayerTestUI with a 'Multiplayer Canvas'. Run Hometown > UI > Build Co-op UI there first.");
            if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
            return;
        }

        var dstCanvas = dst.transform.Find("Multiplayer Canvas");
        int hostIndex = RemoveOld(dstCanvas, "Host", srcHost.GetSiblingIndex());
        int joinIndex = RemoveOld(dstCanvas, "Join", srcJoin.GetSiblingIndex());

        var newHost = Clone(srcHost, dstCanvas, scene, hostIndex);
        var newJoin = Clone(srcJoin, dstCanvas, scene, joinIndex);

        // Re-link every reference that pointed inside Host/Join, matching by path under the canvas.
        var srcSo = new SerializedObject(src);
        var dstSo = new SerializedObject(dst);
        int linked = 0;
        var it = srcSo.GetIterator();
        while (it.NextVisible(true))
        {
            if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue == null) continue;
            var obj = it.objectReferenceValue;
            Transform t = obj is Component c ? c.transform : obj is GameObject g ? g.transform : null;
            if (t == null || !(t == srcHost || t == srcJoin || t.IsChildOf(srcHost) || t.IsChildOf(srcJoin))) continue;

            string rel = RelPath(t, srcCanvas);
            var found = dstCanvas.Find(rel);
            if (found == null) { Debug.LogWarning("[Connection UI] Could not find '" + rel + "' in Gameplay copy."); continue; }

            var dp = dstSo.FindProperty(it.propertyPath);
            if (dp == null) continue;
            Object value = found.gameObject;
            if (obj is Component comp) value = found.GetComponent(comp.GetType());
            if (value == null) continue;
            dp.objectReferenceValue = value;
            linked++;
        }
        dstSo.ApplyModifiedProperties();

        // Keep the little differences between menu mode (6-digit invite) and test mode (4-digit room code).
        if (src.menuMode != dst.menuMode)
        {
            foreach (var lbl in newHost.GetComponentsInChildren<TMP_Text>(true))
                if (lbl.transform.parent.name == "Cancel Button") lbl.text = dst.menuMode ? "Cancel" : "Stop hosting";
            foreach (var inp in newJoin.GetComponentsInChildren<TMP_InputField>(true))
            {
                inp.characterLimit = dst.menuMode ? 6 : 4;
                if (inp.placeholder is TMP_Text ph) ph.text = dst.menuMode ? "Type the 6-digit invite code" : "Type the 4-digit room code";
            }
        }

        newHost.SetActive(false); newJoin.SetActive(false);
        EditorUtility.SetDirty(dst);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
        Debug.Log("[Connection UI] Copied Host & Join to Gameplay and re-linked " + linked + " reference(s).");
    }

    private static GameObject Clone(Transform source, Transform parent, Scene scene, int index)
    {
        var copy = Object.Instantiate(source.gameObject);
        copy.name = source.name;
        SceneManager.MoveGameObjectToScene(copy, scene);
        copy.transform.SetParent(parent, false);
        copy.transform.SetSiblingIndex(Mathf.Min(index, parent.childCount - 1));
        return copy;
    }

    private static int RemoveOld(Transform canvas, string name, int fallback)
    {
        var old = canvas.Find(name);
        if (old == null) return fallback;
        int i = old.GetSiblingIndex();
        Object.DestroyImmediate(old.gameObject);
        return i;
    }

    private static string RelPath(Transform t, Transform root)
    {
        string p = t.name;
        while (t.parent != null && t.parent != root) { t = t.parent; p = t.name + "/" + p; }
        return p;
    }
}
#endif
