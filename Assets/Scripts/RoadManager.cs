using System.Collections.Generic;
using UnityEngine;

/// Roads are always 1x1 grid cells. Placement/demolish works the same way as buildings
/// but also maintains an adjacency graph used by TrafficSystem/PedestrianSystem for pathfinding.
public class RoadManager : MonoBehaviour
{
    public static RoadManager Instance { get; private set; }

    public GameObject straightRoadPrefab;
    public GameObject cornerRoadPrefab; // optional, for auto-tiling visuals
    public GameObject crossRoadPrefab;
    public GameObject tRoadPrefab;

    private Dictionary<Vector2Int, GameObject> roadVisuals = new Dictionary<Vector2Int, GameObject>();
    public HashSet<Vector2Int> RoadCells { get; private set; } = new HashSet<Vector2Int>();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public bool PlaceRoad(Vector2Int cell)
    {
        if (!GridManager.Instance.IsFree(cell)) return false;
        if (!EconomyManager.Instance.TrySpend(10)) return false; // flat cost per road tile, tweak as needed

        GridManager.Instance.SetFootprint(cell, Vector2Int.one, GridManager.CellType.Road, -1);
        RoadCells.Add(cell);

        GameObject prefab = ChoosePrefab(cell);
        var go = Instantiate(prefab, GridManager.Instance.GridToWorld(cell), Quaternion.identity);
        roadVisuals[cell] = go;

        RefreshNeighborVisuals(cell);
        return true;
    }

    public void DemolishRoadAt(Vector2Int cell)
    {
        if (!RoadCells.Contains(cell)) return;
        RoadCells.Remove(cell);
        GridManager.Instance.ClearFootprint(cell, Vector2Int.one);
        if (roadVisuals.TryGetValue(cell, out var go)) Destroy(go);
        roadVisuals.Remove(cell);
        EconomyManager.Instance.RefundDemolish(10);
        RefreshNeighborVisuals(cell);
    }

    private static readonly Vector2Int[] Dirs4 = {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    public bool IsRoad(Vector2Int cell) => RoadCells.Contains(cell);

    /// Used by placement controller to enforce "buildings must touch a road".
    public bool IsAdjacentToRoad(Vector2Int origin, Vector2Int footprint)
    {
        for (int x = -1; x <= footprint.x; x++)
        {
            for (int y = -1; y <= footprint.y; y++)
            {
                bool edgeCell = x == -1 || y == -1 || x == footprint.x || y == footprint.y;
                if (!edgeCell) continue;
                if (RoadCells.Contains(origin + new Vector2Int(x, y))) return true;
            }
        }
        return false;
    }

    // Picks a visual variant based on which neighbor cells are also roads (auto-tiling).
    private GameObject ChoosePrefab(Vector2Int cell)
    {
        int connections = 0;
        foreach (var d in Dirs4) if (RoadCells.Contains(cell + d)) connections++;

        return connections switch
        {
            >= 4 => crossRoadPrefab,
            3 => tRoadPrefab,
            2 => cornerRoadPrefab, // could distinguish straight vs corner by checking opposite dirs
            _ => straightRoadPrefab
        };
    }

    private void RefreshNeighborVisuals(Vector2Int cell)
    {
        foreach (var d in Dirs4)
        {
            var n = cell + d;
            if (roadVisuals.TryGetValue(n, out var go))
            {
                Destroy(go);
                var prefab = ChoosePrefab(n);
                roadVisuals[n] = Instantiate(prefab, GridManager.Instance.GridToWorld(n), Quaternion.identity);
            }
        }
    }

    // ---------- Pathfinding (BFS - fine for city-builder-scale grids) ----------

    public List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal)
    {
        if (!RoadCells.Contains(start) || !RoadCells.Contains(goal)) return null;

        var visited = new HashSet<Vector2Int> { start };
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == goal) return ReconstructPath(cameFrom, start, goal);

            foreach (var d in Dirs4)
            {
                var next = current + d;
                if (RoadCells.Contains(next) && !visited.Contains(next))
                {
                    visited.Add(next);
                    cameFrom[next] = current;
                    queue.Enqueue(next);
                }
            }
        }
        return null; // no path
    }

    private List<Vector2Int> ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int start, Vector2Int goal)
    {
        var path = new List<Vector2Int> { goal };
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
