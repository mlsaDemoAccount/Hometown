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
        var roadNodes = RoadManager.Instance.RoadNodes;
        if (roadNodes.Count < 2) return;

        var cellList = new List<Vector2Int>(roadNodes.Keys);
        Vector2Int start = cellList[Random.Range(0, cellList.Count)];
        Vector2Int goal = cellList[Random.Range(0, cellList.Count)];
        if (start == goal) return;

        var path = RoadManager.Instance.FindPath(start, goal); // now List<RoadNode>, not List<Vector2Int>
        if (path == null || path.Count < 2) return;

        var prefab = vehiclePrefabs[Random.Range(0, vehiclePrefabs.Length)];
        var go = Instantiate(prefab, path[0].worldPosition, Quaternion.identity);
        var agent = go.GetComponent<TrafficAgent>();
        if (agent == null) agent = go.AddComponent<TrafficAgent>();
        agent.SetPath(path);
        activeVehicles.Add(agent);
    }
}

/// Moves a single vehicle along a list of road graph nodes.
/// NOTE: this is still the simple placeholder mover from before - lanes, collision avoidance,
/// stopping at intersections, traffic lights etc. are the "real" traffic system build to come.
public class TrafficAgent : MonoBehaviour
{
    public float moveSpeed = 4f;
    public float rotationSpeed = 8f;

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
            Destroy(gameObject); // reached destination -> despawn (simple approach)
            return;
        }

        Vector3 target = path[currentIndex].worldPosition;
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