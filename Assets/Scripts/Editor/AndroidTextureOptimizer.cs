using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// Tools > Performance > Optimize Android Textures (open scene).
/// Finds every texture used by materials of the open scene and sets its ANDROID import settings:
/// max size capped (default 1024), ASTC compression, crunch off. Android and iPhone overrides change;
/// the source images and other platforms are untouched. All changes show up in git and can be reverted.
public class AndroidTextureOptimizer : EditorWindow
{
    int maxSize = 1024;
    int astcBlock = 6;          // 4 = best quality, 6 = good balance, 8 = smallest
    bool onlyShrinkBigOnes = true;
    string report = "";

    [MenuItem("Tools/Performance/Optimize Android Textures")]
    static void Open() => GetWindow<AndroidTextureOptimizer>("Android Textures");

    void OnGUI()
    {
        EditorGUILayout.HelpBox("Applies to textures used by materials in the OPEN scene. Android and iOS settings change (both use ASTC).", MessageType.Info);
        maxSize = EditorGUILayout.IntPopup("Max texture size", maxSize, new[] { "512", "1024", "2048" }, new[] { 512, 1024, 2048 });
        astcBlock = EditorGUILayout.IntPopup("ASTC block size", astcBlock, new[] { "4x4 (best)", "6x6 (balanced)", "8x8 (smallest)" }, new[] { 4, 6, 8 });
        onlyShrinkBigOnes = EditorGUILayout.Toggle("Never enlarge small textures", onlyShrinkBigOnes);
        if (GUILayout.Button("Dry run (list only)", GUILayout.Height(26))) Run(false);
        if (GUILayout.Button("Apply", GUILayout.Height(30))) Run(true);
        if (!string.IsNullOrEmpty(report)) EditorGUILayout.HelpBox(report, MessageType.None);
    }

    void Run(bool apply)
    {
        var textures = new HashSet<Texture>();
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
            foreach (var mat in r.sharedMaterials)
            {
                if (mat == null) continue;
                foreach (var id in mat.GetTexturePropertyNameIDs())
                {
                    var t = mat.GetTexture(id);
                    if (t != null) textures.Add(t);
                }
            }

        var format = astcBlock == 4 ? TextureImporterFormat.ASTC_4x4 : astcBlock == 6 ? TextureImporterFormat.ASTC_6x6 : TextureImporterFormat.ASTC_8x8;
        int changed = 0, already = 0, skipped = 0;
        long before = 0, after = 0;
        var paths = new List<string>();

        foreach (var t in textures)
        {
            string path = AssetDatabase.GetAssetPath(t);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null || imp.textureType == TextureImporterType.Sprite || imp.textureShape != TextureImporterShape.Texture2D) { skipped++; continue; }

            var cur = imp.GetPlatformTextureSettings("Android");
            int curMax = cur.overridden ? cur.maxTextureSize : imp.maxTextureSize;
            int src = Mathf.Max(t.width, t.height);
            int newMax = onlyShrinkBigOnes ? Mathf.Min(maxSize, Mathf.Max(32, Mathf.NextPowerOfTwo(src))) : maxSize;
            before += Estimate(src, curMax, 1f);
            after += Estimate(src, newMax, 1f);

            // Android and iPhone get the same settings.
            bool needsChange = false;
            foreach (var platform in new[] { "Android", "iPhone" })
            {
                var ps = imp.GetPlatformTextureSettings(platform);
                if (!(ps.overridden && ps.maxTextureSize == newMax && ps.format == format && !ps.crunchedCompression)) needsChange = true;
            }
            if (!needsChange) { already++; continue; }

            changed++;
            paths.Add($"{t.name}: {src}px -> {newMax}px");
            if (!apply) continue;
            foreach (var platform in new[] { "Android", "iPhone" })
            {
                var ps = imp.GetPlatformTextureSettings(platform);
                ps.overridden = true;
                ps.maxTextureSize = newMax;
                ps.format = format;
                ps.compressionQuality = 50;
                ps.crunchedCompression = false;
                imp.SetPlatformTextureSettings(ps);
            }
            imp.SaveAndReimport();
        }

        report = $"{(apply ? "APPLIED" : "DRY RUN")}: {textures.Count} textures in scene, {changed} {(apply ? "changed" : "would change")}, {already} already fine, {skipped} skipped (sprites, cubemaps...).\n" +
                 $"Rough size on phone: {before / 1048576f:0} MB -> {after / 1048576f:0} MB (ASTC ~1 byte/pixel incl. mipmaps).\n" +
                 string.Join("\n", paths.Take(12)) + (paths.Count > 12 ? $"\n... and {paths.Count - 12} more" : "");
        Debug.Log(report);
        Repaint();
    }

    static long Estimate(int sourceSize, int max, float unused)
    {
        long side = Mathf.Min(sourceSize, max);
        return side * side * 4 / 3;
    }
}
