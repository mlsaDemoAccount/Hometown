using System.Collections.Generic;
using UnityEngine;

/// Holds the road and footpath movement graphs (List<RoadNode>, built by RoadGraphBuilder from
/// hand-placed RoadWaypointMarker objects) and the pathfinding over them. Grid occupancy checks
/// (is this cell road/footpath, is a building adjacent to one) are intentionally separate -
/// those still read directly from GridManager, regardless of how the waypoint graph is built.
public class RoadManager : MonoBehaviour
{
    public static RoadManager Instance { get; private set; }

    public List<RoadNode> RoadNodes { get; private set; } = new List<RoadNode>();
    public List<RoadNode> FootpathNodes { get; private set; } = new List<RoadNode>();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// Called by RoadGraphBuilder once it finishes building the graphs from waypoint markers.
    public void SetGraphs(List<RoadNode> roadNodes, List<RoadNode> footpathNodes)
    {
        RoadNodes = roadNodes;
        FootpathNodes = footpathNodes;
    }

    // ---------- Grid occupancy (unrelated to the waypoint graph - reads GridManager directly) ----------

    public bool IsRoad(Vector2Int cell) => GridManager.Instance.GetType(cell) == GridManager.CellType.Road;

    /// There's no separate footpath grid marking anymore (no footpath mesh/collider exists) -
    /// footpath is fused into the same cell as its road, so "is this cell footpath" is really
    /// just "is this cell road."
    public bool IsFootpath(Vector2Int cell) => IsRoad(cell);

    /// Used by placement controller to enforce "buildings must touch a road/footpath". Checks
    /// Road - footpath and road are the same physical cell now, so this single check covers both.
    public bool IsAdjacentToRoad(Vector2Int origin, Vector2Int footprint) =>
        IsAdjacentToCellType(origin, footprint, GridManager.CellType.Road);

    /// Kept as an alias for readability at call sites that specifically mean "near a footpath" -
    /// functionally identical to IsAdjacentToRoad now, since they're the same cell.
    public bool IsAdjacentToFootpath(Vector2Int origin, Vector2Int footprint) =>
        IsAdjacentToRoad(origin, footprint);

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

    // ---------- Pathfinding over the waypoint graph (BFS, unweighted) ----------

    public List<RoadNode> FindPath(RoadNode start, RoadNode goal) => FindPathOn(start, goal);
    public List<RoadNode> FindFootpathPath(RoadNode start, RoadNode goal) => FindPathOn(start, goal);

    private List<RoadNode> FindPathOn(RoadNode start, RoadNode goal)
    {
        if (start == null || goal == null) return null;

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
        return null;
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