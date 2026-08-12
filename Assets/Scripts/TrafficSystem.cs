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

    private List<List<Vector2Int>> connectedComponents; // cached once - road graph doesn't change at runtime

    private void TrySpawnVehicle()
    {
        var roadNodes = RoadManager.Instance.RoadNodes;
        if (roadNodes.Count < 2) return;

        EnsureComponentsBuilt(roadNodes);

        // Only consider components with 2+ nodes - a lone isolated node can't have a route.
        var validComponents = connectedComponents.FindAll(c => c.Count >= 2);
        if (validComponents.Count == 0) return;
        var component = validComponents[Random.Range(0, validComponents.Count)];

        Vector2Int start = PickSpawnCellInComponent(roadNodes, component);
        Vector2Int goal = PickDifferentSpawnCellInComponent(roadNodes, component, start);
        if (goal == start) return; // degenerate case - shouldn't normally happen given the component size check above

        var path = RoadManager.Instance.FindPath(start, goal);
        if (path == null || path.Count < 2)
        {
            // Shouldn't happen now that start/goal are guaranteed same-component - if you see
            // this, the component grouping itself has a bug, please report it.
            Debug.LogWarning($"[TrafficSpawner] Unexpected: no path within same component between {start} and {goal}.");
            return;
        }

        var prefab = vehiclePrefabs[Random.Range(0, vehiclePrefabs.Length)];
        var go = Instantiate(prefab, path[0].worldPosition, Quaternion.identity);
        var agent = go.GetComponent<TrafficAgent>();
        if (agent == null) agent = go.AddComponent<TrafficAgent>();
        agent.laneOffset = laneOffset;
        agent.driveOnRight = driveOnRight;
        agent.SetPath(path);
        activeVehicles.Add(agent);
    }

    /// Groups the road graph into connected components via BFS, once. Cached for the rest of
    /// the session since RoadNodes doesn't change at runtime (roads are static, pre-built).
    private void EnsureComponentsBuilt(Dictionary<Vector2Int, RoadNode> roadNodes)
    {
        if (connectedComponents != null) return;

        connectedComponents = new List<List<Vector2Int>>();
        var visited = new HashSet<Vector2Int>();

        foreach (var startCell in roadNodes.Keys)
        {
            if (visited.Contains(startCell)) continue;

            var component = new List<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(startCell);
            visited.Add(startCell);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                component.Add(current);
                foreach (var edge in roadNodes[current].edges)
                {
                    if (!visited.Contains(edge.to.cell))
                    {
                        visited.Add(edge.to.cell);
                        queue.Enqueue(edge.to.cell);
                    }
                }
            }

            connectedComponents.Add(component);
        }

        connectedComponents.Sort((a, b) => b.Count - a.Count); // largest first, mostly cosmetic
        Debug.Log($"[TrafficSpawner] Road network has {connectedComponents.Count} disconnected piece(s) - largest has {connectedComponents[0].Count} nodes.");
    }

    private Vector2Int PickSpawnCellInComponent(Dictionary<Vector2Int, RoadNode> roadNodes, List<Vector2Int> component)
    {
        var deadEnds = component.FindAll(c => roadNodes[c].edges.Count == 1);
        if (deadEnds.Count > 0) return deadEnds[Random.Range(0, deadEnds.Count)];
        return component[Random.Range(0, component.Count)];
    }

    private Vector2Int PickDifferentSpawnCellInComponent(Dictionary<Vector2Int, RoadNode> roadNodes, List<Vector2Int> component, Vector2Int exclude)
    {
        var deadEnds = component.FindAll(c => roadNodes[c].edges.Count == 1 && c != exclude);
        if (deadEnds.Count > 0) return deadEnds[Random.Range(0, deadEnds.Count)];

        var others = component.FindAll(c => c != exclude);
        if (others.Count == 0) return exclude;
        return others[Random.Range(0, others.Count)];
    }
}

/// Moves a single vehicle along a road-graph path, offset sideways onto its correct lane so
/// opposing traffic doesn't drive head-on down the centerline.
/// NOTE: still no collision avoidance, intersection stopping, or traffic lights yet - those
/// come next. This is just movement + lanes.
public class TrafficAgent : MonoBehaviour
{
    public float moveSpeed = 4f;
    public float rotationSpeed = 8f;
    [HideInInspector] public float laneOffset = 0.4f;
    [HideInInspector] public bool driveOnRight = true;

    private List<Vector3> waypoints; // lane-offset positions, computed once from the raw node path
    private int currentIndex = 0;

    public void SetPath(List<RoadNode> nodePath)
    {
        waypoints = BuildLaneOffsetWaypoints(nodePath);
        currentIndex = 0;
    }

    /// Converts raw centerline node positions into lane-offset waypoints. For each segment
    /// (node i -> node i+1), the direction of travel determines a perpendicular "right" vector;
    /// the waypoint gets shifted sideways along that vector by laneOffset. A node shared by two
    /// segments (a corner) uses the outgoing segment's direction, except the final node which
    /// uses the incoming segment's - this gives a clean offset path with a small "kink" at
    /// corners rather than a smooth curve, which is fine for now and easy to spline-smooth later.
    private List<Vector3> BuildLaneOffsetWaypoints(List<RoadNode> nodePath)
    {
        var result = new List<Vector3>(nodePath.Count);
        float sideSign = driveOnRight ? 1f : -1f;

        for (int i = 0; i < nodePath.Count; i++)
        {
            Vector3 dir;

            if (i == 0)
            {
                dir = nodePath[1].worldPosition - nodePath[0].worldPosition;
            }
            else if (i == nodePath.Count - 1)
            {
                dir = nodePath[i].worldPosition - nodePath[i - 1].worldPosition;
            }
            else
            {
                // Blend incoming and outgoing direction so the offset turns smoothly through a
                // corner/intersection instead of snapping from one side-vector to a very
                // different one - this is what was causing the "jogs left then suddenly right"
                // glitch at intersections.
                Vector3 incoming = (nodePath[i].worldPosition - nodePath[i - 1].worldPosition).normalized;
                Vector3 outgoing = (nodePath[i + 1].worldPosition - nodePath[i].worldPosition).normalized;
                dir = incoming + outgoing;
                if (dir.sqrMagnitude < 0.0001f) dir = outgoing; // near-180 turn (rare on a grid) - fall back
            }

            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) { result.Add(nodePath[i].worldPosition); continue; }
            dir.Normalize();

            // Cross(up, dir) points to the right of the direction of travel in Unity's
            // left-handed, Y-up coordinate system.
            Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
            result.Add(nodePath[i].worldPosition + right * (laneOffset * sideSign));
        }

        return result;
    }

    void Update()
    {
        if (waypoints == null || currentIndex >= waypoints.Count)
        {
            Destroy(gameObject); // reached destination -> despawn (simple approach for now)
            return;
        }

        Vector3 target = waypoints[currentIndex];
        Vector3 toTarget = target - transform.position;
        toTarget.y = 0;

        if (toTarget.magnitude < 0.1f)
        {
            currentIndex++;
            return;
        }

        transform.position += toTarget.normalized * moveSpeed * Time.deltaTime;

        if (toTarget.sqrMagnitude > 0.001f)
        {
            Quaternion look = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, rotationSpeed * Time.deltaTime);
        }

        // NEXT UP: raycast-ahead collision/jam avoidance, and checking the upcoming node's
        // IsIntersection / controlType before proceeding through it (stop signs, then lights).
    }
}