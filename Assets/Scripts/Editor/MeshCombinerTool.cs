#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// TESTING/UTILITY TOOL - combines the mesh of every selected GameObject (with a MeshFilter)
/// into one new combined mesh object. Fully generic - works on ANY selection (roads, mountains,
/// props, whatever), not tied to any specific kind of object.
///
/// Correctly preserves MULTIPLE materials as separate submeshes (e.g. road + footpath) instead
/// of flattening everything into one - see the two-pass explanation below.
///
/// Each run saves its OWN uniquely-named asset (via AssetDatabase.GenerateUniqueAssetPath, based
/// on your current selection's name) - it will NEVER overwrite a previous combine's saved mesh,
/// even if you combine something else afterward. This was the bug in the earlier version: it
/// hardcoded one fixed filename, so a second combine (e.g. mountains) silently overwrote the
/// first one's asset (e.g. roads), breaking anything still referencing it.
///
/// NOT meant to ship as-is: a single combined mesh spanning a large area gets ONE bounding box,
/// so Unity can never frustum/occlusion-cull it - it renders in full every frame regardless of
/// camera position. For a real fix, combine in spatial CHUNKS (per region) instead of
/// everything at once, to keep bounding boxes reasonably tight.
public static class MeshCombinerTool
{
    private struct SubmeshEntry
    {
        public Mesh mesh;
        public int submeshIndex;
        public Material material;
        public Matrix4x4 transform;
    }

    [MenuItem("Tools/Mesh Combiner/Combine Selected Meshes (Testing)")]
    private static void CombineSelected()
    {
        GameObject[] selected = Selection.gameObjects;
        if (selected.Length == 0)
        {
            Debug.LogWarning("[MeshCombinerTool] Nothing selected - select the GameObjects you want to combine first.");
            return;
        }

        var meshFilters = selected
            .SelectMany(go => go.GetComponentsInChildren<MeshFilter>())
            .Where(mf => mf.sharedMesh != null)
            .ToArray();

        if (meshFilters.Length == 0)
        {
            Debug.LogWarning("[MeshCombinerTool] No MeshFilters with a mesh found in the selection.");
            return;
        }

        // Pass 0: flatten every (mesh, submesh) pair into an entry tagged with its actual
        // material - a mesh with 2 submeshes (e.g. road + footpath) produces 2 separate entries.
        var entries = new List<SubmeshEntry>();
        foreach (var mf in meshFilters)
        {
            var renderer = mf.GetComponent<MeshRenderer>();
            if (renderer == null) continue;

            Material[] mats = renderer.sharedMaterials;
            Mesh mesh = mf.sharedMesh;

            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                // If there are fewer material slots than submeshes, Unity repeats the last
                // material for the extras - match that same fallback behavior here.
                Material mat = sub < mats.Length ? mats[sub] : (mats.Length > 0 ? mats[mats.Length - 1] : null);
                if (mat == null) continue;

                entries.Add(new SubmeshEntry
                {
                    mesh = mesh,
                    submeshIndex = sub,
                    material = mat,
                    transform = mf.transform.localToWorldMatrix
                });
            }
        }

        if (entries.Count == 0)
        {
            Debug.LogWarning("[MeshCombinerTool] No valid (mesh, material) submesh pairs found in the selection.");
            return;
        }

        // Pass 1: combine each material's pieces into ONE mesh (merged into a single submesh).
        var groupedByMaterial = entries.GroupBy(e => e.material).ToList();
        var perMaterialMeshes = new List<Mesh>();
        var finalMaterials = new List<Material>();

        foreach (var group in groupedByMaterial)
        {
            var combineList = group.Select(e => new CombineInstance
            {
                mesh = e.mesh,
                subMeshIndex = e.submeshIndex,
                transform = e.transform
            }).ToArray();

            Mesh materialMesh = new Mesh();
            materialMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // safe default at this combine scale
            materialMesh.CombineMeshes(combineList, true, true); // true = merge this ONE material's pieces together

            perMaterialMeshes.Add(materialMesh);
            finalMaterials.Add(group.Key);
        }

        // Pass 2: combine the per-material meshes together WITHOUT merging submeshes, so each
        // material keeps its own separate submesh in the final result.
        var finalCombineList = perMaterialMeshes.Select(m => new CombineInstance
        {
            mesh = m,
            subMeshIndex = 0,
            transform = Matrix4x4.identity // already baked into world space from pass 1
        }).ToArray();

        Mesh finalMesh = new Mesh();
        finalMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        finalMesh.CombineMeshes(finalCombineList, false, false); // false = KEEP submeshes separate, one per material

        // Name everything after the current selection instead of a fixed generic name, so
        // different combine operations are distinguishable at a glance in the Hierarchy/Project.
        string baseName = $"Combined_{selected[0].name}";

        GameObject combinedObject = new GameObject(baseName);
        var newFilter = combinedObject.AddComponent<MeshFilter>();
        var newRenderer = combinedObject.AddComponent<MeshRenderer>();
        newFilter.sharedMesh = finalMesh;
        newRenderer.sharedMaterials = finalMaterials.ToArray(); // order matches finalMesh's submesh order

        // GenerateUniqueAssetPath appends " 1", " 2", etc. if the base name is already taken -
        // this is the actual fix: every combine gets its OWN file, never overwriting a previous
        // one's saved mesh (which was the bug - mountains overwriting roads' asset).
        string path = AssetDatabase.GenerateUniqueAssetPath($"Assets/{baseName}.asset");
        AssetDatabase.CreateAsset(finalMesh, path);
        AssetDatabase.SaveAssets();

        Debug.Log($"[MeshCombinerTool] Combined {entries.Count} submesh pieces across {finalMaterials.Count} material(s) " +
                   $"into '{combinedObject.name}' ({finalMesh.vertexCount} vertices, {finalMesh.subMeshCount} submeshes). " +
                   $"Mesh saved to {path}. Disable the original objects to compare draw calls (Stats overlay or Frame Debugger).");

        Selection.activeGameObject = combinedObject;
    }
}
#endif
