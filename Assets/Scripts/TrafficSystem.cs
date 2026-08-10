using System.Collections.Generic;
using UnityEngine;

/// Spawns vehicles that automatically pick random road-to-road routes and drive themselves.
/// Attach TrafficSpawner to an empty GameObject in the scene.
public class TrafficSpawner : MonoBehaviour
{
    public static TrafficSpawner Instance { get; private set; }

    public GameObject[] vehiclePrefabs;
    public int maxActiveVehicles = 30;
    public float spawnInterval = 2f;

    private List<TrafficAgent> activeVehicles = new List<TrafficAgent>();
    private float timer;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= spawnInterval && activeVehicles.Count < maxActiveVehicles)
        {
            timer = 0f;
            TrySpawnVehicle();
        }
        activeVehicles.RemoveAll(v => v == null);
    }

    private void TrySpawnVehicle()
    {
        var roads = RoadManager.Instance.RoadCells;
        if (roads.Count < 2) return;

        var roadList = new List<Vector2Int>(roads);
        Vector2Int start = roadList[Random.Range(0, roadList.Count)];
        Vector2Int goal = roadList[Random.Range(0, roadList.Count)];
        if (start == goal) return;

        var path = RoadManager.Instance.FindPath(start, goal);
        if (path == null || path.Count < 2) return;

        var prefab = vehiclePrefabs[Random.Range(0, vehiclePrefabs.Length)];
        var go = Instantiate(prefab, GridManager.Instance.GridToWorld(start), Quaternion.identity);
        var agent = go.GetComponent<TrafficAgent>();
        if (agent == null) agent = go.AddComponent<TrafficAgent>();
        agent.SetPath(path);
        activeVehicles.Add(agent);
    }
}

/// Moves a single vehicle along a list of grid-cell waypoints.
public class TrafficAgent : MonoBehaviour
{
    public float moveSpeed = 4f;
    public float rotationSpeed = 8f;

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
            Destroy(gameObject); // reached destination -> despawn (simple approach)
            return;
        }

        Vector3 target = GridManager.Instance.GridToWorld(path[currentIndex]);
        Vector3 dir = (target - transform.position);
        dir.y = 0;

        if (dir.magnitude < 0.05f)
        {
            currentIndex++;
            return;
        }

        transform.position += dir.normalized * moveSpeed * Time.deltaTime;
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion look = Quaternion.LookRotation(dir.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, rotationSpeed * Time.deltaTime);
        }

        // NOTE: for real traffic behavior (lanes, collision avoidance, stopping at intersections)
        // add a simple raycast-ahead check here and pause movement if another TrafficAgent is too close.
    }
}
