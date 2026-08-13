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
        if (RoadManager.Instance.FootpathNodes.Count < 2) return;

        var home = homes[Random.Range(0, homes.Count)];
        var job = jobs[Random.Range(0, jobs.Count)];

        RoadNode start = NearestFootpathNode(GridManager.Instance.GridToWorld(home.gridOrigin));
        RoadNode goal = NearestFootpathNode(GridManager.Instance.GridToWorld(job.gridOrigin));
        if (start == null || goal == null || start == goal) return;

        var path = RoadManager.Instance.FindFootpathPath(start, goal);
        if (path == null || path.Count < 2) return;

        var prefab = pedestrianPrefabs[Random.Range(0, pedestrianPrefabs.Length)];
        var go = Instantiate(prefab, path[0].worldPosition, Quaternion.identity);
        var agent = go.GetComponent<PedestrianAgent>() ?? go.AddComponent<PedestrianAgent>();
        agent.SetPath(path);
        active.Add(agent);
    }

    /// Finds the closest FOOTPATH waypoint (by straight-line distance) to a world position -
    /// used to find where a building "enters" the footpath network.
    private RoadNode NearestFootpathNode(Vector3 worldPos)
    {
        RoadNode nearest = null;
        float bestSqrDist = float.MaxValue;
        foreach (var node in RoadManager.Instance.FootpathNodes)
        {
            float d = (node.worldPosition - worldPos).sqrMagnitude;
            if (d < bestSqrDist) { bestSqrDist = d; nearest = node; }
        }
        return nearest;
    }
}

/// NOTE: still the simple placeholder mover from before - proper footpath-only movement,
/// crowd avoidance, etc. are part of the "real" pedestrian system build to come after traffic.
public class PedestrianAgent : MonoBehaviour
{
    public float moveSpeed = 1.5f;
    private List<RoadNode> path;
    private int currentIndex = 0;

    public void SetPath(List<RoadNode> newPath)
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

        Vector3 target = path[currentIndex].worldPosition;
        // offset slightly off road center so people don't overlap vehicles exactly - optional polish
        Vector3 dir = target - transform.position;
        dir.y = 0;

        if (dir.magnitude < 0.05f) { currentIndex++; return; }

        transform.position += dir.normalized * moveSpeed * Time.deltaTime;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }
}