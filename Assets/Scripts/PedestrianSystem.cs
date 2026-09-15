using System.Collections.Generic;
using UnityEngine;

/// Spawns pedestrians at Population buildings and routes them to Jobs buildings, walking along a
/// DEDICATED footpath waypoint network (RoadManager.FootpathNodes) - separate from the road
/// graph vehicles use. Waypoints are placed by hand (RoadWaypointMarker with isFootpath = true),
/// exactly where the sidewalk actually is.
public class PedestrianSpawner : MonoBehaviour
{
    public static PedestrianSpawner Instance { get; private set; }

    [Header("Pedestrians")]
    public GameObject[] pedestrianPrefabs;
    public int maxActivePedestrians = 50;
    public float spawnInterval = 1.5f;
    [Tooltip("Optional - if assigned, every spawned pedestrian is parented under this Transform for scene organization only. Uses worldPositionStays=true, so this has zero effect on position/rotation/behavior - purely cosmetic Hierarchy grouping. Leave unassigned to spawn at scene root as before.")]
    public Transform pedestrianParent;

    [Header("Speed")]
    [Tooltip("Default speed range if the spawned prefab doesn't have a PedestrianSpeedProfile component.")]
    public float minSpeed = 1.2f;
    public float maxSpeed = 2f;

    [Header("Walk Spread")]
    [Tooltip("Small random sideways offset per pedestrian (each side, randomly) so multiple people on the same route don't walk in a single perfectly-overlapping file.")]
    public float maxWalkSpread = 0.25f;

    [Header("Debug (test without Population/Jobs buildings)")]
    [Tooltip("When on, spawns between two random footpath waypoints (or the two points below, if assigned) instead of requiring actual Population/Jobs buildings.")]
    public bool debugMode = false;
    [Tooltip("Optional - if assigned, debug spawns always route between these two instead of random points.")]
    public Transform debugStartPoint;
    public Transform debugEndPoint;

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
        if (debugMode)
        {
            TrySpawnDebug();
            return;
        }

        var homes = CityStatsManager.Instance.GetBuildingsByCategory(BuildingCategory.Population);
        var jobs = CityStatsManager.Instance.GetBuildingsByCategory(BuildingCategory.Jobs);
        if (homes.Count == 0 || jobs.Count == 0)
        {
            Debug.LogWarning($"[PedestrianSpawner] No spawn possible: {homes.Count} Population building(s), {jobs.Count} Jobs building(s) placed.");
            return;
        }
        if (RoadManager.Instance.FootpathNodes.Count < 2)
        {
            Debug.LogWarning($"[PedestrianSpawner] No spawn possible: only {RoadManager.Instance.FootpathNodes.Count} footpath waypoint(s) placed.");
            return;
        }
        if (pedestrianPrefabs == null || pedestrianPrefabs.Length == 0)
        {
            Debug.LogWarning("[PedestrianSpawner] No spawn possible: pedestrianPrefabs array is empty.");
            return;
        }

        var home = homes[Random.Range(0, homes.Count)];
        var job = jobs[Random.Range(0, jobs.Count)];

        RoadNode start = NearestFootpathNode(GridManager.Instance.GridToWorld(home.gridOrigin));
        RoadNode goal = NearestFootpathNode(GridManager.Instance.GridToWorld(job.gridOrigin));
        SpawnAlongPath(start, goal);
    }

    private void TrySpawnDebug()
    {
        if (RoadManager.Instance.FootpathNodes.Count < 2)
        {
            Debug.LogWarning($"[PedestrianSpawner] Debug mode: only {RoadManager.Instance.FootpathNodes.Count} footpath waypoint(s) placed.");
            return;
        }
        if (pedestrianPrefabs == null || pedestrianPrefabs.Length == 0)
        {
            Debug.LogWarning("[PedestrianSpawner] Debug mode: pedestrianPrefabs array is empty.");
            return;
        }

        RoadNode start, goal;
        if (debugStartPoint != null && debugEndPoint != null)
        {
            start = NearestFootpathNode(debugStartPoint.position);
            goal = NearestFootpathNode(debugEndPoint.position);
        }
        else
        {
            var nodes = RoadManager.Instance.FootpathNodes;
            start = nodes[Random.Range(0, nodes.Count)];
            goal = nodes[Random.Range(0, nodes.Count)];
        }

        SpawnAlongPath(start, goal);
    }

    private void SpawnAlongPath(RoadNode start, RoadNode goal)
    {
        if (start == null || goal == null || start == goal)
        {
            Debug.LogWarning("[PedestrianSpawner] No spawn possible: couldn't find distinct start/goal footpath waypoints.");
            return;
        }

        var path = RoadManager.Instance.FindFootpathPath(start, goal);
        if (path == null || path.Count < 2)
        {
            Debug.LogWarning("[PedestrianSpawner] No path found between these footpath waypoints - likely disconnected footpath pieces (check your marker `connections`).");
            return;
        }

        var prefab = pedestrianPrefabs[Random.Range(0, pedestrianPrefabs.Length)];
        var go = Instantiate(prefab, path[0].worldPosition, Quaternion.identity);
        if (pedestrianParent != null) go.transform.SetParent(pedestrianParent, true); // true = keep world position exactly as spawned, purely organizational
        var agent = go.GetComponent<PedestrianAgent>() ?? go.AddComponent<PedestrianAgent>();

        var speedProfile = go.GetComponent<PedestrianSpeedProfile>();
        agent.moveSpeed = speedProfile != null
            ? Random.Range(speedProfile.minSpeed, speedProfile.maxSpeed)
            : Random.Range(minSpeed, maxSpeed);

        // Small random per-pedestrian sideways offset (either side) so multiple people on the
        // same route spread out naturally instead of walking in a single overlapping line.
        float walkOffset = Random.Range(0f, maxWalkSpread);
        bool offsetToRight = Random.value > 0.5f;
        agent.SetPath(path, walkOffset, offsetToRight);

        active.Add(agent);
    }

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

/// Moves a single pedestrian along a footpath-graph path, with a small per-pedestrian sideways
/// offset (spreads people out) and a vertical offset (corrects sinking into the ground, same
/// idea as TrafficAgent's verticalOffset - tune per prefab if the model's pivot isn't at its feet).
public class PedestrianAgent : MonoBehaviour
{
    public float moveSpeed = 1.5f;
    public float rotationSpeed = 6f;
    [Tooltip("Lifts the pedestrian above the waypoint's height - tune this per prefab if the model's pivot isn't at its base/feet.")]
    public float verticalOffset = 0f;

    private List<Vector3> waypoints;
    private int currentIndex = 0;

    public void SetPath(List<RoadNode> nodePath, float walkOffset, bool offsetToRight)
    {
        waypoints = PathOffsetUtility.BuildOffsetWaypoints(nodePath, walkOffset, offsetToRight);
        for (int i = 0; i < waypoints.Count; i++)
            waypoints[i] += Vector3.up * verticalOffset;

        currentIndex = 0;

        // Snap immediately to the (offset + lifted) starting position - otherwise the pedestrian
        // would spawn at the raw node position and visibly slide sideways/up to the real start.
        if (waypoints.Count > 0) transform.position = waypoints[0];
    }

    void Update()
    {
        if (waypoints == null || currentIndex >= waypoints.Count)
        {
            Destroy(gameObject); // arrived at work -> despawn (simple loop; re-spawn handles the cycle)
            return;
        }

        Vector3 target = waypoints[currentIndex];
        Vector3 toTarget = target - transform.position;
        toTarget.y = 0;

        float distanceThisFrame = moveSpeed * Time.deltaTime;

        if (toTarget.magnitude <= distanceThisFrame)
        {
            transform.position = target;
            currentIndex++;
            return;
        }

        transform.position += toTarget.normalized * distanceThisFrame;

        if (toTarget.sqrMagnitude > 0.001f)
        {
            Quaternion look = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, rotationSpeed * Time.deltaTime);
        }
    }
}