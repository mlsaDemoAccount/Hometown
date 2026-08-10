using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// Spawns pedestrians at Population buildings and routes them to Jobs buildings, reusing
/// the same road-network pathfinding as traffic (people walk alongside roads for simplicity;
/// swap in a separate sidewalk grid later if you want them off the road).
public class PedestrianSpawner : MonoBehaviour
{
    public static PedestrianSpawner Instance { get; private set; }

    public GameObject[] pedestrianPrefabs;
    public int maxActivePedestrians = 50;
    public float spawnInterval = 1.5f;

    private float timer;
    private List<PedestrianAgent> active = new List<PedestrianAgent>();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= spawnInterval && active.Count < maxActivePedestrians)
        {
            timer = 0f;
            TrySpawn();
        }
        active.RemoveAll(p => p == null);
    }

    private void TrySpawn()
    {
        var homes = CityStatsManager.Instance.GetBuildingsByCategory(BuildingCategory.Population);
        var jobs = CityStatsManager.Instance.GetBuildingsByCategory(BuildingCategory.Jobs);
        if (homes.Count == 0 || jobs.Count == 0) return;

        var home = homes[Random.Range(0, homes.Count)];
        var job = jobs[Random.Range(0, jobs.Count)];

        Vector2Int start = NearestRoadCell(home.gridOrigin);
        Vector2Int goal = NearestRoadCell(job.gridOrigin);
        if (start == goal) return;

        var path = RoadManager.Instance.FindPath(start, goal);
        if (path == null || path.Count < 2) return;

        var prefab = pedestrianPrefabs[Random.Range(0, pedestrianPrefabs.Length)];
        var go = Instantiate(prefab, GridManager.Instance.GridToWorld(start), Quaternion.identity);
        var agent = go.GetComponent<PedestrianAgent>() ?? go.AddComponent<PedestrianAgent>();
        agent.SetPath(path);
        active.Add(agent);
    }

    private Vector2Int NearestRoadCell(Vector2Int origin)
    {
        // Simple ring search outward from the building for the closest road tile.
        for (int r = 1; r < 10; r++)
        {
            for (int x = -r; x <= r; x++)
                for (int y = -r; y <= r; y++)
                {
                    var c = origin + new Vector2Int(x, y);
                    if (RoadManager.Instance.IsRoad(c)) return c;
                }
        }
        return origin;
    }
}

public class PedestrianAgent : MonoBehaviour
{
    public float moveSpeed = 1.5f;
    private List<Vector2Int> path;
    private int currentIndex = 0;

    public void SetPath(List<Vector2Int> newPath)
    {
        path = newPath;
        currentIndex = 0;
    }

    void Update()
    {
        if (path == null || currentIndex >= path.Count)
        {
            Destroy(gameObject); // arrived at work -> despawn (simple loop; re-spawn handles the cycle)
            return;
        }

        Vector3 target = GridManager.Instance.GridToWorld(path[currentIndex]);
        // offset slightly off road center so people don't overlap vehicles exactly - optional polish
        Vector3 dir = target - transform.position;
        dir.y = 0;

        if (dir.magnitude < 0.05f) { currentIndex++; return; }

        transform.position += dir.normalized * moveSpeed * Time.deltaTime;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }
}
