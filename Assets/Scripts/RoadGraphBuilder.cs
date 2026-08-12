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

        var roadNodes = DetectAndBuildGraph(grid, roadLayerMask, GridManager.CellType.Road, GridManager.OccupantKind.Road, isFootpathPass: false);
        var footpathNodes = DetectAndBuildGraph(grid, footpathLayerMask, GridManager.CellType.Footpath, GridManager.OccupantKind.Footpath, isFootpathPass: true);

        RoadManager.Instance.SetGraphs(roadNodes, footpathNodes);

        int intersections = 0;
        foreach (var n in roadNodes.Values) if (n.IsIntersection) intersections++;
        Debug.Log($"[RoadGraphBuilder] Road: {roadNodes.Count} nodes ({intersections} intersections). Footpath: {footpathNodes.Count} nodes.");
    }

    private Dictionary<Vector2Int, RoadNode> DetectAndBuildGraph(GridManager grid, LayerMask layerMask, GridManager.CellType markAsType, GridManager.OccupantKind markAsKind, bool isFootpathPass)
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

        // If you've baked + hand-adjusted RoadWaypointMarker objects for this layer, use their
        // CURRENT position instead of the raw auto-detected cell center - lets you nudge
        // individual waypoints for better-looking movement without losing the auto-detected
        // topology (which nodes connect to which is still derived from the skeleton itself).
        var markerOverrides = new Dictionary<Vector2Int, Vector3>();
        foreach (var marker in FindObjectsOfType<RoadWaypointMarker>())
            if (marker.isFootpath == isFootpathPass)
                markerOverrides[marker.cell] = marker.transform.position;

        // Pass 3: build the node graph from the SKELETON only - this is what traffic/pedestrians
        // path along, and what intersection detection (3+ edges) is based on.
        var nodes = new Dictionary<Vector2Int, RoadNode>();
        foreach (var cell in skeletonCells)
        {
            Vector3 pos = markerOverrides.TryGetValue(cell, out var overridden) ? overridden : grid.GridToWorld(cell);
            nodes[cell] = new RoadNode(cell, pos);
        }

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

    // ---------- Visualization ----------

    private void OnDrawGizmos()
    {
        if (RoadManager.Instance == null) return;
        DrawGraphGizmos(RoadManager.Instance.RoadNodes, Color.yellow);
        DrawGraphGizmos(RoadManager.Instance.FootpathNodes, new Color(0.2f, 0.8f, 1f));
    }

    private void DrawGraphGizmos(Dictionary<Vector2Int, RoadNode> nodes, Color color)
    {
        if (nodes == null) return;
        Gizmos.color = color;
        Vector3 lift = Vector3.up * 0.1f; // lift slightly so gizmos aren't clipped into the ground mesh

        foreach (var node in nodes.Values)
        {
            Gizmos.DrawSphere(node.worldPosition + lift, node.IsIntersection ? 0.25f : 0.12f);
            foreach (var edge in node.edges)
                Gizmos.DrawLine(node.worldPosition + lift, edge.to.worldPosition + lift);
        }
    }

    // ---------- Bake to hand-editable GameObjects ----------

    /// Creates a real, draggable GameObject per node (RoadWaypointMarker) so you can nudge
    /// individual waypoints in the Scene view for better-looking movement.
    ///
    /// WORKFLOW (important - read this):
    /// Unity does NOT save changes made while in Play Mode. If you run this while playing,
    /// the baked objects will be DELETED the moment you stop Play unless you rescue them first:
    ///   1. Enter Play Mode, let the graph build (or right-click > Rebuild Road/Footpath Graph Now).
    ///   2. Right-click this component > "Bake Waypoints To GameObjects".
    ///   3. In the Hierarchy, select BOTH "RoadWaypoints (Baked)" and "FootpathWaypoints (Baked)".
    ///   4. Copy them (Ctrl+C / Cmd+C).
    ///   5. Stop Play Mode.
    ///   6. Paste (Ctrl+V / Cmd+V) - Unity supports pasting Play-Mode-created objects back into
    ///      the edited scene. They'll now persist normally.
    ///   7. Drag individual waypoint markers to adjust their position as needed.
    ///   8. Next time you enter Play Mode, RoadGraphBuilder will automatically use their
    ///      positions instead of the raw auto-detected centers.
    [ContextMenu("Bake Waypoints To GameObjects")]
    public void BakeWaypoints()
    {
        if (RoadManager.Instance == null || RoadManager.Instance.RoadNodes.Count == 0)
        {
            Debug.LogWarning("[RoadGraphBuilder] Nothing to bake yet - run the scan first (Play, or right-click > Rebuild Road/Footpath Graph Now).");
            return;
        }

        var roadContainer = new GameObject("RoadWaypoints (Baked)").transform;
        foreach (var node in RoadManager.Instance.RoadNodes.Values)
            CreateMarker(node, roadContainer, isFootpath: false);

        var footpathContainer = new GameObject("FootpathWaypoints (Baked)").transform;
        foreach (var node in RoadManager.Instance.FootpathNodes.Values)
            CreateMarker(node, footpathContainer, isFootpath: true);

        Debug.Log("[RoadGraphBuilder] Baked waypoint GameObjects created. If you're in Play Mode, copy them out before stopping - see the BakeWaypoints() comment for the exact steps, or Unity will discard them.");
    }

    private void CreateMarker(RoadNode node, Transform parent, bool isFootpath)
    {
        var go = new GameObject($"Waypoint_{node.cell.x}_{node.cell.y}");
        go.transform.SetParent(parent);
        go.transform.position = node.worldPosition;
        var marker = go.AddComponent<RoadWaypointMarker>();
        marker.cell = node.cell;
        marker.isFootpath = isFootpath;
    }
}