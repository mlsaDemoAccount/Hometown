using System.Collections.Generic;
using UnityEngine;

/// Runs once at scene start: detects road/footpath cells from your pre-built static meshes
/// (downward raycasts - work fine against non-convex MeshColliders, unlike overlap/box checks),
/// then links each detected cell to its 4-directional neighbors to build a proper NODE GRAPH
/// (RoadManager.RoadNodes / FootpathNodes) - not just a flat "is this cell road" set.
///
/// This graph is what lets traffic lights, stop signs, one-way streets, and real queuing/jams
/// slot in later without another rewrite: intersections fall out automatically as any node with
/// 3+ edges, and each RoadNode already has a reserved `controlType` field for future signals.
///
/// SETUP:
/// 1. Road mesh needs a Collider (MeshCollider is fine - does NOT need to be convex, since we
///    raycast rather than use overlap/box checks).
/// 2. Put it on a dedicated layer (e.g. "Road"), assign to roadLayerMask below.
/// 3. Same for footpath mesh on its own layer (e.g. "Footpath"), assign to footpathLayerMask.
/// 4. Attach this to your Managers object alongside RoadManager and GridManager.
public class RoadGraphBuilder : MonoBehaviour
{
    [Header("Detection Layers")]
    public LayerMask roadLayerMask;      // your pre-built road mesh's collider layer
    public LayerMask footpathLayerMask;  // your pre-built footpath mesh's collider layer

    [Header("Raycast Settings")]
    public float rayStartHeight = 50f;   // start well above the tallest point of your meshes
    public float rayDistance = 200f;     // must comfortably reach from rayStartHeight down through the mesh

    private static readonly Vector2Int[] Dirs4 = {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    void Start()
    {
        BuildGraphs();
    }

    /// Right-click this component in the Inspector to re-run without pressing Play - handy
    /// after editing the road mesh in the Editor.
    [ContextMenu("Rebuild Road/Footpath Graph Now")]
    public void BuildGraphs()
    {
        var grid = GridManager.Instance;
        if (grid == null) { Debug.LogError("[RoadGraphBuilder] GridManager not found - aborted."); return; }

        var roadNodes = DetectAndBuildGraph(grid, roadLayerMask, GridManager.CellType.Road, GridManager.OccupantKind.Road);
        var footpathNodes = DetectAndBuildGraph(grid, footpathLayerMask, GridManager.CellType.Footpath, GridManager.OccupantKind.Footpath);

        RoadManager.Instance.SetGraphs(roadNodes, footpathNodes);

        int intersections = 0;
        foreach (var n in roadNodes.Values) if (n.IsIntersection) intersections++;
        Debug.Log($"[RoadGraphBuilder] Road: {roadNodes.Count} nodes ({intersections} intersections). Footpath: {footpathNodes.Count} nodes.");
    }

    private Dictionary<Vector2Int, RoadNode> DetectAndBuildGraph(GridManager grid, LayerMask layerMask, GridManager.CellType markAsType, GridManager.OccupantKind markAsKind)
    {
        // Pass 1: detect every cell this mesh covers - the FULL width, not just a centerline.
        var rawCells = new HashSet<Vector2Int>();
        for (int x = 0; x < grid.width; x++)
        {
            for (int y = 0; y < grid.height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                Vector3 rayOrigin = grid.GridToWorld(cell) + Vector3.up * rayStartHeight;

                if (Physics.Raycast(rayOrigin, Vector3.down, rayDistance, layerMask))
                {
                    rawCells.Add(cell);
                    // Mark the FULL raw footprint in GridManager - both road and footpath block
                    // building placement, and building road-adjacency checks need to know about
                    // the real, full-width edge, not just a thin centerline.
                    grid.SetFootprint(cell, Vector2Int.one, markAsType, -1, markAsKind);
                }
            }
        }

        // Pass 2: reduce the (possibly many-cells-wide) raw footprint to a 1-cell-wide skeleton.
        // Without this, a road wider than 1 cell would make almost every interior cell look like
        // a 4-way intersection (road neighbors on all sides) - see GridSkeletonizer for why.
        var skeletonCells = GridSkeletonizer.Thin(rawCells);

        // Pass 3: build the node graph from the SKELETON only - this is what traffic/pedestrians
        // path along, and what intersection detection (3+ edges) is based on.
        var nodes = new Dictionary<Vector2Int, RoadNode>();
        foreach (var cell in skeletonCells)
            nodes[cell] = new RoadNode(cell, grid.GridToWorld(cell));

        foreach (var kvp in nodes)
        {
            var node = kvp.Value;
            foreach (var dir in Dirs4)
            {
                if (nodes.TryGetValue(node.cell + dir, out var neighbor))
                {
                    float dist = Vector3.Distance(node.worldPosition, neighbor.worldPosition);
                    node.edges.Add(new RoadEdge(node, neighbor, dist));
                }
            }
        }

        return nodes;
    }
}