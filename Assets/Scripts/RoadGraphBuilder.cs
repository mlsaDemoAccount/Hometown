using System.Collections.Generic;
using UnityEngine;

/// Two separate jobs, kept deliberately independent:
///
/// 1. GRID OCCUPANCY MARKING (automatic, road only): scans the static road mesh via raycasts
///    and marks every covered grid cell in GridManager as Road. There is no separate footpath
///    mesh/collider/layer at all - footpath is purely visual, fused into the road geometry, so
///    there's nothing to auto-detect for it. Building placement checks Road adjacency, which
///    now also covers "next to the footpath," since they're the same physical cell.
///
/// 2. CAR/PEDESTRIAN MOVEMENT GRAPH (fully manual): built ONLY from RoadWaypointMarker objects
///    already placed in the scene, connected via their `connections` list. Road waypoints can
///    be bootstrapped with the auto-generate tool below (real, detectable geometry); footpath
///    waypoints are 100% hand-placed - position them yourself on the sides of the road, there's
///    no mesh to generate them from.
///
/// SETUP:
/// 1. Road mesh needs a Collider (MeshCollider is fine, does not need to be convex).
/// 2. Put it on a dedicated layer (e.g. "Road"), assign to roadLayerMask below.
/// 3. Attach this to your Managers object alongside RoadManager and GridManager.
/// 4. Create waypoints: empty GameObject + RoadWaypointMarker component, positioned on the
///    road centerline (vehicles) or off to the side (pedestrians), with `isFootpath` set
///    correctly. Drag neighboring waypoints into each marker's `connections` list to link them.
public class RoadGraphBuilder : MonoBehaviour
{
    [Header("Grid Occupancy Detection (automatic, road only)")]
    public LayerMask roadLayerMask;      // your pre-built road mesh's collider layer
    public float rayStartHeight = 50f;   // start well above the tallest point of your mesh
    public float rayDistance = 200f;     // must comfortably reach from rayStartHeight down through the mesh

    void Start()
    {
        BuildGraphs();
    }

    /// Right-click this component in the Inspector to re-run without pressing Play - handy
    /// after editing the road mesh, or after adding/rewiring waypoint markers.
    [ContextMenu("Rebuild Road/Footpath Graph Now")]
    public void BuildGraphs()
    {
        var grid = GridManager.Instance;
        if (grid == null) { Debug.LogError("[RoadGraphBuilder] GridManager not found - aborted."); return; }

        // Job 1: grid occupancy marking, automatic from the road mesh only.
        int roadCellCount = ScanAndMarkRoadCells(grid, roadLayerMask);
        Debug.Log($"[RoadGraphBuilder] Mesh scan: {roadCellCount} road cell(s) detected. (0 here means the collider/layer setup isn't being hit.)");

        // Job 2: movement graph, entirely from hand-placed markers - nothing auto-generated.
        var roadNodes = BuildGraphFromMarkers(isFootpath: false);
        var footpathNodes = BuildGraphFromMarkers(isFootpath: true);
        RoadManager.Instance.SetGraphs(roadNodes, footpathNodes);

        int intersections = 0;
        foreach (var n in roadNodes) if (n.IsIntersection) intersections++;
        Debug.Log($"[RoadGraphBuilder] Waypoint graph (from RoadWaypointMarker objects in scene): Road {roadNodes.Count} waypoints ({intersections} intersections), Footpath {footpathNodes.Count} waypoints. (0 here just means no markers exist yet - separate from the mesh scan above.)");
    }

    private int ScanAndMarkRoadCells(GridManager grid, LayerMask layerMask)
    {
        int count = 0;
        for (int x = 0; x < grid.width; x++)
        {
            for (int y = 0; y < grid.height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                Vector3 rayOrigin = grid.GridToWorld(cell) + Vector3.up * rayStartHeight;
                if (Physics.Raycast(rayOrigin, Vector3.down, rayDistance, layerMask))
                {
                    grid.SetFootprint(cell, Vector2Int.one, GridManager.CellType.Road, -1, GridManager.OccupantKind.Road);
                    count++;
                }
            }
        }
        return count;
    }

    private List<RoadNode> BuildGraphFromMarkers(bool isFootpath)
    {
        var markers = new List<RoadWaypointMarker>();
        foreach (var m in FindObjectsOfType<RoadWaypointMarker>())
            if (m.isFootpath == isFootpath) markers.Add(m);

        var nodeByMarker = new Dictionary<RoadWaypointMarker, RoadNode>();
        foreach (var m in markers)
            nodeByMarker[m] = new RoadNode(m.cell, m.transform.position);

        foreach (var m in markers)
        {
            var node = nodeByMarker[m];
            foreach (var connectedMarker in m.connections)
            {
                if (connectedMarker == null) continue;
                if (!nodeByMarker.TryGetValue(connectedMarker, out var neighborNode))
                {
                    Debug.LogWarning($"[RoadGraphBuilder] Waypoint '{m.name}' connects to '{connectedMarker.name}', but that marker is missing from the scene or has a mismatched isFootpath flag. Skipped.");
                    continue;
                }

                float dist = Vector3.Distance(node.worldPosition, neighborNode.worldPosition);
                node.edges.Add(new RoadEdge(node, neighborNode, dist));

                // Auto-add the reverse edge - a normal two-way road only needs wiring one
                // direction. (One-way roads later: add a per-connection flag and skip this.)
                if (!neighborNode.edges.Exists(e => e.to == node))
                    neighborNode.edges.Add(new RoadEdge(neighborNode, node, dist));
            }
        }

        return new List<RoadNode>(nodeByMarker.Values);
    }

    // ---------- Visualization ----------

    private void OnDrawGizmos()
    {
        if (RoadManager.Instance == null) return;
        DrawGraphGizmos(RoadManager.Instance.RoadNodes, Color.yellow);
        DrawGraphGizmos(RoadManager.Instance.FootpathNodes, new Color(0.2f, 0.8f, 1f));
    }

    private void DrawGraphGizmos(List<RoadNode> nodes, Color color)
    {
        if (nodes == null) return;
        Gizmos.color = color;
        Vector3 lift = Vector3.up * 0.1f;

        foreach (var node in nodes)
        {
            Gizmos.DrawSphere(node.worldPosition + lift, node.IsIntersection ? 0.25f : 0.12f);
            foreach (var edge in node.edges)
                Gizmos.DrawLine(node.worldPosition + lift, edge.to.worldPosition + lift);
        }
    }

    // ---------- Baseline waypoint generation (optional starting point, not automatic every run) ----------

    /// Creates a starting set of ROAD waypoint markers by scanning the road mesh directly - one
    /// per detected cell, auto-linked to its grid-adjacent neighbors. FOOTPATH waypoints are NOT
    /// generated here - there's no footpath mesh/collider to detect, so place those entirely by
    /// hand, positioned to the side of the road waypoints this generates.
    ///
    /// This is a FAST STARTING POINT for roads, not the final network: delete markers you don't
    /// need (e.g. thin out long straight stretches), and add/move markers where you want
    /// smoother curves or better-placed intersections.
    ///
    /// PLAY MODE CAVEAT: if you run this while playing, copy the generated container out
    /// (Ctrl+C) before stopping Play, or Unity will discard it.
    [ContextMenu("Auto-Generate Baseline Road Waypoints From Mesh")]
    public void AutoGenerateBaselineWaypoints()
    {
        var grid = GridManager.Instance;
        if (grid == null) { Debug.LogError("[RoadGraphBuilder] GridManager not found - aborted."); return; }

        var roadContainer = new GameObject("RoadWaypoints (Auto-Generated)").transform;
        GenerateWaypointsForLayer(grid, roadLayerMask, roadContainer, isFootpath: false);

        Debug.Log("[RoadGraphBuilder] Baseline ROAD waypoints generated. Thin out straight stretches, add waypoints on curves, and move intersection waypoints as needed. Footpath waypoints still need to be placed by hand - no mesh to generate them from. If in Play Mode, copy the container out before stopping.");
    }

    private void GenerateWaypointsForLayer(GridManager grid, LayerMask layerMask, Transform container, bool isFootpath)
    {
        var detectedCells = new HashSet<Vector2Int>();
        for (int x = 0; x < grid.width; x++)
        {
            for (int y = 0; y < grid.height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                Vector3 rayOrigin = grid.GridToWorld(cell) + Vector3.up * rayStartHeight;
                if (Physics.Raycast(rayOrigin, Vector3.down, rayDistance, layerMask))
                    detectedCells.Add(cell);
            }
        }

        var markerByCell = new Dictionary<Vector2Int, RoadWaypointMarker>();
        foreach (var cell in detectedCells)
        {
            var go = new GameObject($"Waypoint_{cell.x}_{cell.y}");
            go.transform.SetParent(container);
            go.transform.position = grid.GridToWorld(cell);
            var marker = go.AddComponent<RoadWaypointMarker>();
            marker.cell = cell;
            marker.isFootpath = isFootpath;
            markerByCell[cell] = marker;
        }

        // Link each marker to its right/up neighbor only (if present) - the reverse edge is
        // added automatically when the graph builds, so this avoids redundant double-linking.
        foreach (var kvp in markerByCell)
        {
            var cell = kvp.Key;
            var marker = kvp.Value;
            if (markerByCell.TryGetValue(cell + Vector2Int.right, out var rightNeighbor))
                marker.connections.Add(rightNeighbor);
            if (markerByCell.TryGetValue(cell + Vector2Int.up, out var upNeighbor))
                marker.connections.Add(upNeighbor);
        }
    }
}