using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// Tools > Performance > Audit Open Scene: counts what the open scene really asks the phone to draw
/// (renderers, materials, triangles, textures, lights, static flags) and lists the worst offenders.
/// The report is printed to the Console and saved to Docs/PerfAudit.txt.
/// Tools > Performance > Mark Selected As Static: sets batching/occlusion static on scenery.
public static class ScenePerfAudit
{
    const StaticEditorFlags SceneryFlags =
        StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic;

    [MenuItem("Tools/Performance/Audit Open Scene")]
    static void Audit()
    {
        var sb = new StringBuilder();
        var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
        var meshRenderers = renderers.Where(r => r is MeshRenderer || r is SkinnedMeshRenderer).ToList();

        long tris = 0;
        var meshUse = new Dictionary<Mesh, int>();
        var matUse = new Dictionary<Material, int>();
        int notStatic = 0, notBatchStatic = 0, notOccludee = 0, shadowCasters = 0, inLod = 0, transparent = 0, noInstancing = 0;

        foreach (var r in meshRenderers)
        {
            Mesh m = r is SkinnedMeshRenderer smr ? smr.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
            if (m != null)
            {
                meshUse[m] = meshUse.TryGetValue(m, out int c) ? c + 1 : 1;
                tris += TriCount(m);
            }
            foreach (var mat in r.sharedMaterials)
            {
                if (mat == null) continue;
                matUse[mat] = matUse.TryGetValue(mat, out int c) ? c + 1 : 1;
            }
            var flags = GameObjectUtility.GetStaticEditorFlags(r.gameObject);
            if (flags == 0) notStatic++;
            if ((flags & StaticEditorFlags.BatchingStatic) == 0) notBatchStatic++;
            if ((flags & StaticEditorFlags.OccludeeStatic) == 0) notOccludee++;
            if (r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off) shadowCasters++;
            if (r.GetComponentInParent<LODGroup>() != null) inLod++;
        }
        foreach (var mat in matUse.Keys)
        {
            if (mat.renderQueue >= 2500) transparent++;
            if (!mat.enableInstancing) noInstancing++;
        }

        sb.AppendLine($"Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
        sb.AppendLine($"Active mesh renderers: {meshRenderers.Count}   (in LOD groups: {inLod})");
        sb.AppendLine($"Triangles if everything drew (all LOD levels counted): {tris:N0}");
        sb.AppendLine($"Unique meshes: {meshUse.Count}   Unique materials: {matUse.Count}  (transparent/alpha queue: {transparent}, without GPU instancing: {noInstancing})");
        sb.AppendLine($"Renderers NOT static at all: {notStatic}   not batching-static: {notBatchStatic}   not occludee-static: {notOccludee}");
        sb.AppendLine("  -> objects that are not Occludee Static are never hidden by baked occlusion culling,");
        sb.AppendLine("     and objects that are not Batching Static cost one draw call each.");
        sb.AppendLine($"Shadow-casting renderers: {shadowCasters}");

        var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude);
        sb.AppendLine($"Lights: {lights.Length} (realtime shadows on: {lights.Count(l => l.shadows != LightShadows.None && l.enabled)})");
        sb.AppendLine($"Animators: {Object.FindObjectsByType<Animator>(FindObjectsInactive.Exclude).Length}   Particle systems: {Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude).Length}   Canvases: {Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude).Length}");

        sb.AppendLine("\n== Heaviest meshes (triangles x copies) ==");
        foreach (var kv in meshUse.OrderByDescending(k => (long)TriCount(k.Key) * k.Value).Take(15))
            sb.AppendLine($"  {kv.Key.name}: {TriCount(kv.Key):N0} tris x {kv.Value}  = {(long)TriCount(kv.Key) * kv.Value:N0}   [{AssetDatabase.GetAssetPath(kv.Key)}]{(kv.Key.isReadable ? "  READ/WRITE ON" : "")}");

        sb.AppendLine("\n== Most-used materials ==");
        foreach (var kv in matUse.OrderByDescending(k => k.Value).Take(12))
            sb.AppendLine($"  {kv.Key.name} ({kv.Key.shader.name}): {kv.Value} renderers{(kv.Key.renderQueue >= 2500 ? "  TRANSPARENT" : "")}");

        // Textures
        var texUse = new Dictionary<Texture, int>();
        foreach (var mat in matUse.Keys)
            foreach (var id in mat.GetTexturePropertyNameIDs())
            {
                var t = mat.GetTexture(id);
                if (t != null) texUse[t] = texUse.TryGetValue(t, out int c) ? c + 1 : 1;
            }
        long texBytes = 0;
        var texRows = new List<(long bytes, string text)>();
        foreach (var t in texUse.Keys)
        {
            string path = AssetDatabase.GetAssetPath(t);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            string android = "n/a";
            long bytes = (long)t.width * t.height * 4 / (imp != null && imp.mipmapEnabled ? 3 : 4) * 4 / 4;
            if (imp != null)
            {
                var ps = imp.GetPlatformTextureSettings("Android");
                android = ps.overridden ? $"override {ps.format} max {ps.maxTextureSize}" : $"default (max {imp.maxTextureSize}, compression {imp.textureCompression})";
                // rough on-phone size: ASTC-class compression ~ 1 byte/pixel, uncompressed 4
                bool compressed = ps.overridden ? ps.format.ToString().Contains("ASTC") || ps.format.ToString().Contains("ETC") : imp.textureCompression != TextureImporterCompression.Uncompressed;
                int maxS = ps.overridden ? ps.maxTextureSize : imp.maxTextureSize;
                int side = Mathf.Min(Mathf.Max(t.width, t.height), maxS);
                bytes = (long)side * side * (compressed ? 1 : 4) * (imp.mipmapEnabled ? 4 : 3) / 3;
            }
            texBytes += bytes;
            texRows.Add((bytes, $"  {t.name}: {t.width}x{t.height}, ~{bytes / 1048576f:0.0} MB on phone, Android: {android}  [{path}]"));
        }
        sb.AppendLine($"\n== Textures used by scene materials: {texUse.Count}, roughly {texBytes / 1048576f:0.0} MB on the phone ==");
        foreach (var row in texRows.OrderByDescending(x => x.bytes).Take(15)) sb.AppendLine(row.text);

        string report = sb.ToString();
        Debug.Log(report);
        Directory.CreateDirectory("Docs");
        File.WriteAllText("Docs/PerfAudit.txt", report);
        EditorUtility.DisplayDialog("Scene audit done", "Saved to Docs/PerfAudit.txt and printed in the Console.\n\n" +
            $"{meshRenderers.Count} renderers, {matUse.Count} materials, {tris:N0} triangles.\n" +
            $"{notBatchStatic} renderers are not batching-static, {notOccludee} are not occludee-static.", "OK");
    }

    static int TriCount(Mesh m)
    {
        long idx = 0;
        for (int i = 0; i < m.subMeshCount; i++) idx += m.GetIndexCount(i);
        return (int)(idx / 3);
    }

    /// Select the scenery root(s) in the Hierarchy first (e.g. "city new bulldings", "citymap").
    /// Objects that carry game scripts or animators are left alone (they can move or be changed at runtime).
    [MenuItem("Tools/Performance/Mark Selected As Static (scenery)")]
    static void MarkStatic()
    {
        var roots = Selection.gameObjects;
        if (roots.Length == 0) { EditorUtility.DisplayDialog("Nothing selected", "Select the scenery root object(s) in the Hierarchy first.", "OK"); return; }

        int changed = 0, skipped = 0;
        foreach (var root in roots)
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var go = t.gameObject;
                bool hasRenderer = go.GetComponent<MeshRenderer>() != null;
                bool hasLogic = go.GetComponents<MonoBehaviour>().Any(m => m != null) || go.GetComponent<Animator>() != null || go.GetComponent<Rigidbody>() != null;
                if (hasLogic) { skipped++; continue; }
                if (!hasRenderer && go.GetComponent<LODGroup>() == null) continue;
                Undo.RecordObject(go, "Mark static");
                GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(go) | SceneryFlags);
                changed++;
            }
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        EditorUtility.DisplayDialog("Marked static", $"{changed} objects set to Batching / Occluder / Occludee static, {skipped} with scripts left alone.\n\n" +
            "Next: save the scene, then re-bake occlusion culling (Window > Rendering > Occlusion Culling > Bake).", "OK");
    }
}
