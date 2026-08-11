using System.Collections.Generic;
using UnityEngine;

/// Holds the road and footpath NODE GRAPHS (not flat cell sets), built once at scene start by
/// RoadGraphBuilder from your pre-authored static meshes. Vehicles/pedestrians move node-to-node
/// along edges - this is what makes traffic lights, stop signs, one-way streets, and realistic
/// queuing/jams possible later without another architecture rewrite.
public class RoadManager : MonoBehaviour
{
    public static RoadManager Instance { get; private set; }

    public Dictionary<Vector2Int, RoadNode> RoadNodes { get; private set; } = new Dictionary<Vector2Int, RoadNode>();
    public Dictionary<Vector2Int, RoadNode> FootpathNodes { get; private set; } = new Dictionary<Vector2Int, RoadNode>();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// Called by RoadGraphBuilder once it finishes scanning + linking the static meshes.
    public void SetGraphs(Dictionary<Vector2Int, RoadNode> roadNodes, Dictionary<Vector2Int, RoadNode> footpathNodes)
    {
        RoadNodes = roadNodes;
        FootpathNodes = footpathNodes;
    }

    public bool IsRoad(Vector2Int cell) => RoadNodes.ContainsKey(cell);
    public bool IsFootpath(Vector2Int cell) => FootpathNodes.ContainsKey(cell);
    public RoadNode GetRoadNode(Vector2Int cell) => RoadNodes.TryGetValue(cell, out var n) ? n : null;
    public RoadNode GetFootpathNode(Vector2Int cell) => FootpathNodes.TryGetValue(cell, out var n) ? n : null;

    /// Used by placement controller to enforce "buildings must touch a road". Checks the FULL
    /// road footprint in GridManager, not RoadNodes - RoadNodes is now a thinned centerline
    /// skeleton (see RoadGraphBuilder/GridSkeletonizer), so a building next to a wide road's
    /// edge should still count as adjacent even if it's several cells from that centerline.
    public bool IsAdjacentToRoad(Vector2Int origin, Vector2Int footprint) =>
        IsAdjacentToCellType(origin, footprint, GridManager.CellType.Road);

    /// Same idea, but for footpath - use this instead of IsAdjacentToRoad for buildings that
    /// should connect to the sidewalk rather than the vehicle lane (the more realistic default
    /// for most buildings, since roads and footpaths run parallel to each other).
    public bool IsAdjacentToFootpath(Vector2Int origin, Vector2Int footprint) =>
        IsAdjacentToCellType(origin, footprint, GridManager.CellType.Footpath);

    private bool IsAdjacentToCellType(Vector2Int origin, Vector2Int footprint, GridManager.CellType type)
    {
        for (int x = -1; x <= footprint.x; x++)
        {
            for (int y = -1; y <= footprint.y; y++)
            {
                bool edgeCell = x == -1 || y == -1 || x == footprint.x || y == footprint.y;
                if (!edgeCell) continue;
                var c = origin + new Vector2Int(x, y);
                if (GridManager.Instance.GetType(c) == type) return true;
            }
        }
        return false;
    }

    // ---------- Pathfinding (BFS over the node graph, unweighted) ----------
    // Swap for Dijkstra/A* later if edge weights start mattering (e.g. congestion cost) -
    // the graph structure already supports weighted edges via RoadEdge.length, this just
    // doesn't use that weight yet since a plain grid has uniform edge lengths anyway.

    public List<RoadNode> FindPath(Vector2Int startCell, Vector2Int goalCell) =>
        FindPathOn(RoadNodes, startCell, goalCell);

    public List<RoadNode> FindFootpathPath(Vector2Int startCell, Vector2Int goalCell) =>
        FindPathOn(FootpathNodes, startCell, goalCell);

    private List<RoadNode> FindPathOn(Dictionary<Vector2Int, RoadNode> graph, Vector2Int startCell, Vector2Int goalCell)
    {
        if (!graph.TryGetValue(startCell, out var start) || !graph.TryGetValue(goalCell, out var goal)) return null;

        var visited = new HashSet<RoadNode> { start };
        var cameFrom = new Dictionary<RoadNode, RoadNode>();
        var queue = new Queue<RoadNode>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == goal) return ReconstructPath(cameFrom, start, goal);

            foreach (var edge in current.edges)
            {
                var next = edge.to;
                if (!visited.Contains(next))
                {
                    visited.Add(next);
                    cameFrom[next] = current;
                    queue.Enqueue(next);
                }
            }
        }
        return null; // no path
    }

    private List<RoadNode> ReconstructPath(Dictionary<RoadNode, RoadNode> cameFrom, RoadNode start, RoadNode goal)
    {
        var path = new List<RoadNode> { goal };
        var current = goal;
        while (current != start)
        {
            current = cameFrom[current];
            path.Add(current);
        }
        path.Reverse();
        return path;
    }
}