#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// Tools > Mesh Combiner > Slim Selected Mesh Asset.
/// A combined mesh made by MeshCombinerTool carries 60 bytes per vertex (position, normal, tangent,
/// vertex colour, UV0, UV1). Most materials only use position, normal and UV0. This rewrites the
/// selected mesh asset IN PLACE (same file, same GUID, scene references stay valid) keeping only the
/// channels you choose, then turns on build-time mesh compression.
/// Select the combined object in the Hierarchy or the .asset in the Project window, then run it.
public static class MeshSlimTool
{
    const string KeepTangentsKey = "MeshSlim.KeepTangents";

    [MenuItem("Tools/Mesh Combiner/Slim Selected Mesh Asset")]
    static void Slim()
    {
        Mesh mesh = Selection.activeObject as Mesh;
        if (mesh == null && Selection.activeGameObject != null)
        {
            var mf = Selection.activeGameObject.GetComponent<MeshFilter>();
            if (mf != null) mesh = mf.sharedMesh;
        }
        if (mesh == null || !AssetDatabase.Contains(mesh))
        {
            EditorUtility.DisplayDialog("Slim mesh", "Select a saved mesh asset (or an object using one) first.", "OK");
            return;
        }

        bool keepTangents = EditorUtility.DisplayDialog("Keep tangents?",
            "Tangents are only needed for materials with a NORMAL MAP. Dropping them saves 16 bytes per vertex but normal-mapped materials will light incorrectly.\n\nUnless you know the combined buildings use normal maps, choose 'Drop'.",
            "Keep tangents", "Drop (smaller)");

        string path = AssetDatabase.GetAssetPath(mesh);
        long before = new System.IO.FileInfo(path).Length;

        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var tangents = new List<Vector4>();
        var uv0 = new List<Vector2>();
        mesh.GetVertices(verts);
        mesh.GetNormals(normals);
        mesh.GetUVs(0, uv0);
        if (keepTangents) mesh.GetTangents(tangents);

        var slim = new Mesh { name = mesh.name, indexFormat = mesh.indexFormat };
        slim.SetVertices(verts);
        if (normals.Count == verts.Count) slim.SetNormals(normals);
        if (uv0.Count == verts.Count) slim.SetUVs(0, uv0);
        if (keepTangents && tangents.Count == verts.Count) slim.SetTangents(tangents);

        slim.subMeshCount = mesh.subMeshCount;
        for (int i = 0; i < mesh.subMeshCount; i++)
            slim.SetIndices(mesh.GetIndices(i), MeshTopology.Triangles, i, false);
        slim.bounds = mesh.bounds;

        EditorUtility.CopySerialized(slim, mesh);          // overwrite in place, keeps the asset's GUID
        MeshUtility.SetMeshCompression(mesh, ModelImporterMeshCompression.Medium);
        EditorUtility.SetDirty(mesh);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        long after = new System.IO.FileInfo(path).Length;
        Debug.Log($"[MeshSlimTool] {mesh.name}: file {before / 1048576f:0} MB -> {after / 1048576f:0} MB, {verts.Count:N0} vertices, tangents {(keepTangents ? "kept" : "dropped")}. Build size also shrinks further from mesh compression.");
    }
}
#endif
