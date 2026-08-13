using System.Collections.Generic;
using UnityEngine;

/// Spawns vehicles that pick random road-to-road routes and drive themselves along the road
/// graph's waypoints (RoadNodes), offset onto their correct lane side.
/// Attach TrafficSpawner to an empty GameObject in the scene.
public class TrafficSpawner : MonoBehaviour
{
    public static TrafficSpawner Instance { get; private set; }

    [Header("Vehicles")]
    public GameObject[] vehiclePrefabs;
    public int maxActiveVehicles = 30;
    public float spawnInterval = 2f;

    [Header("Lane")]
    [Tooltip("How far a vehicle sits from the road centerline, toward its correct side.")]
    public float laneOffset = 0.4f;
    [Tooltip("On = right-hand traffic (drive on the right, like US/EU). Off = left-hand (UK/Japan/etc).")]
    public bool driveOnRight = true;

    [Header("Speed")]
    [Tooltip("Default speed range if the spawned prefab doesn't have a VehicleSpeedProfile component.")]
    public float minSpeed = 3f;
    public float maxSpeed = 6f;

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

    private List<List<RoadNode>> connectedComponents; // cached once - road graph doesn't change at runtime

    private void TrySpawnVehicle()
    {
        var roadNodes = RoadManager.Instance.RoadNodes;
        if (roadNodes.Count < 2) return;

        EnsureComponentsBuilt(roadNodes);

        // Only consider components with 2+ nodes - a lone isolated node can't have a route.
        var validComponents = connectedComponents.FindAll(c => c.Count >= 2);
        if (validComponents.Count == 0) return;
        var component = validComponents[Random.Range(0, validComponents.Count)];

        RoadNode start = PickSpawnNodeInComponent(component);
        RoadNode goal = PickDifferentSpawnNodeInComponent(component, start);
        if (goal == start) return; // degenerate case - shouldn't normally happen given the component size check above

        var path = RoadManager.Instance.FindPath(start, goal);
        if (path == null || path.Count < 2)
        {
            // Shouldn't happen now that start/goal are guaranteed same-component - if you see
            // this, the component grouping itself has a bug, please report it.
            Debug.LogWarning("[TrafficSpawner] Unexpected: no path within same component.");
            return;
        }

        var prefab = vehiclePrefabs[Random.Range(0, vehiclePrefabs.Length)];
        var go = Instantiate(prefab, path[0].worldPosition, Quaternion.identity);
        var agent = go.GetComponent<TrafficAgent>();
        if (agent == null) agent = go.AddComponent<TrafficAgent>();
        agent.laneOffset = laneOffset;
        agent.driveOnRight = driveOnRight;

        // Apply vertical offset to the actual spawn position too - Instantiate's position
        // argument above ignores anything set on the prefab, so this has to be corrected
        // explicitly here rather than relying on the prefab's own transform.
        go.transform.position = path[0].worldPosition + Vector3.up * agent.verticalOffset;

        var speedProfile = go.GetComponent<VehicleSpeedProfile>();
        agent.moveSpeed = speedProfile != null
            ? Random.Range(speedProfile.minSpeed, speedProfile.maxSpeed)
            : Random.Range(minSpeed, maxSpeed);

        agent.SetPath(path);
        activeVehicles.Add(agent);
    }

    /// Groups the road graph into connected components via BFS, once. Cached for the rest of
    /// the session since RoadNodes doesn't change at runtime (waypoints are placed by hand,
    /// not modified during Play).
    private void EnsureComponentsBuilt(List<RoadNode> roadNodes)
    {
        if (connectedComponents != null) return;

        connectedComponents = new List<List<RoadNode>>();
        var visited = new HashSet<RoadNode>();

        foreach (var startNode in roadNodes)
        {
            if (visited.Contains(startNode)) continue;

            var component = new List<RoadNode>();
            var queue = new Queue<RoadNode>();
            queue.Enqueue(startNode);
            visited.Add(startNode);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                component.Add(current);
                foreach (var edge in current.edges)
                {
                    if (!visited.Contains(edge.to))
                    {
                        visited.Add(edge.to);
                        queue.Enqueue(edge.to);
                    }
                }
            }

            connectedComponents.Add(component);
        }

        connectedComponents.Sort((a, b) => b.Count - a.Count); // largest first, mostly cosmetic
        Debug.Log($"[TrafficSpawner] Road waypoint graph has {connectedComponents.Count} disconnected piece(s) - largest has {connectedComponents[0].Count} nodes.");
    }

    private RoadNode PickSpawnNodeInComponent(List<RoadNode> component)
    {
        var deadEnds = component.FindAll(n => n.edges.Count == 1);
        if (deadEnds.Count > 0) return deadEnds[Random.Range(0, deadEnds.Count)];
        return component[Random.Range(0, component.Count)];
    }

    private RoadNode PickDifferentSpawnNodeInComponent(List<RoadNode> component, RoadNode exclude)
    {
        var deadEnds = component.FindAll(n => n.edges.Count == 1 && n != exclude);
        if (deadEnds.Count > 0) return deadEnds[Random.Range(0, deadEnds.Count)];

        var others = component.FindAll(n => n != exclude);
        if (others.Count == 0) return exclude;
        return others[Random.Range(0, others.Count)];
    }
}