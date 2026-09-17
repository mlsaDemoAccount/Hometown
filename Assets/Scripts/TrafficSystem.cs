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
    [Tooltip("Optional - if assigned, every spawned vehicle is parented under this Transform for scene organization only. Uses worldPositionStays=true, so this has zero effect on position/rotation/behavior - purely cosmetic Hierarchy grouping. Leave unassigned to spawn at scene root as before.")]
    public Transform trafficParent;

    [Header("Lane")]
    [Tooltip("How far a vehicle sits from the road centerline, toward its correct side.")]
    public float laneOffset = 0.4f;
    [Tooltip("On = right-hand traffic (drive on the right, like US/EU). Off = left-hand (UK/Japan/etc).")]
    public bool driveOnRight = true;

    [Header("Speed")]
    [Tooltip("Default speed range if the spawned prefab doesn't have a VehicleSpeedProfile component.")]
    public float minSpeed = 3f;
    public float maxSpeed = 6f;

    [Header("Spawn Safety")]
    [Tooltip("Don't spawn if an existing vehicle is already this close to the spawn point - prevents new cars appearing on top of one already there.")]
    public float spawnClearRadius = 1.5f;

    [Header("Manual Spawn Points (optional)")]
    [Tooltip("If assigned, a fraction of spawns will originate from these specific points instead of random dead-ends - useful for guaranteeing traffic reaches a specific area of a large city that random selection rarely visits.")]
    public List<Transform> manualSpawnPoints = new List<Transform>();
    [Range(0f, 1f)]
    [Tooltip("Fraction of spawn attempts that use a manual point (when any are assigned) instead of normal random selection. 0.5 = about half.")]
    public float manualSpawnChance = 0.5f;

    [Header("Zoom-Based Culling")]
    [Tooltip("When the camera's zoom value (MobileCameraController.CurrentZoom, same units as its Min/Max Zoom) exceeds this - i.e. zoomed further OUT than this - every active vehicle is deactivated entirely (stops both rendering AND movement simulation), cutting draw calls and CPU cost at a distance where cars aren't meaningfully visible to the player anyway. Zoomed back in below this value re-activates them. Set to a large value (e.g. way above maxZoom) to disable this feature entirely.")]
    public float hideVehiclesAboveZoom = 30f;

    private bool vehiclesCurrentlyHidden = false;

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

        UpdateVehicleVisibilityForZoom();
    }

    /// Hides/shows every active vehicle based on current camera zoom - a car whose whole
    /// GameObject is deactivated costs nothing (no draw call, no Update()/movement/raycast
    /// simulation), not just an invisible render. Only toggles when actually crossing the
    /// threshold, not every frame, to avoid redundant SetActive calls while zoom sits still.
    private void UpdateVehicleVisibilityForZoom()
    {
        if (MobileCameraController.Instance == null) return;

        bool shouldHide = MobileCameraController.Instance.CurrentZoom > hideVehiclesAboveZoom;
        if (shouldHide == vehiclesCurrentlyHidden) return;

        vehiclesCurrentlyHidden = shouldHide;
        foreach (var vehicle in activeVehicles)
        {
            if (vehicle != null) vehicle.gameObject.SetActive(!shouldHide);
        }
    }

    private List<List<RoadNode>> connectedComponents; // cached once - road graph doesn't change at runtime

    private void TrySpawnVehicle()
    {
        var roadNodes = RoadManager.Instance.RoadNodes;
        if (roadNodes.Count < 2) return;

        EnsureComponentsBuilt(roadNodes);

        RoadNode start;
        List<RoadNode> component;

        bool useManualPoint = manualSpawnPoints != null && manualSpawnPoints.Count > 0 && Random.value < manualSpawnChance;
        if (useManualPoint)
        {
            var point = manualSpawnPoints[Random.Range(0, manualSpawnPoints.Count)];
            if (point == null) return;

            start = NearestRoadNode(point.position);
            component = start != null ? connectedComponents.Find(c => c.Contains(start)) : null;

            if (start == null || component == null || component.Count < 2)
            {
                Debug.LogWarning($"[TrafficSpawner] Manual spawn point '{point.name}' has no reachable road waypoint nearby - this means there's no waypoint graph coverage there yet (place/connect RoadWaypointMarker objects in that area), not just a spawn-selection issue.");
                return;
            }
        }
        else
        {
            // Only consider components with 2+ nodes - a lone isolated node can't have a route.
            var validComponents = connectedComponents.FindAll(c => c.Count >= 2);
            if (validComponents.Count == 0) return;
            component = validComponents[Random.Range(0, validComponents.Count)];
            start = PickSpawnNodeInComponent(component);
        }

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

        if (!IsSpawnPointClear(path[0].worldPosition))
            return; // another vehicle is already at/near the spawn point - try again next interval

        // Face the direction of the first path segment, not a fixed world direction - otherwise
        // every car spawns facing the same way regardless of the road's actual orientation and
        // visibly snaps to the correct heading over the next few frames.
        Vector3 initialDir = path[1].worldPosition - path[0].worldPosition;
        initialDir.y = 0;
        Quaternion initialRotation = initialDir.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(initialDir.normalized, Vector3.up)
            : Quaternion.identity;

        var prefab = vehiclePrefabs[Random.Range(0, vehiclePrefabs.Length)];
        var go = Instantiate(prefab, path[0].worldPosition, initialRotation);
        if (trafficParent != null) go.transform.SetParent(trafficParent, true); // true = keep world position exactly as spawned, purely organizational
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

        // If already zoomed out past the threshold, keep this new vehicle hidden from the
        // instant it spawns - otherwise it would flash visible for one frame before the next
        // UpdateVehicleVisibilityForZoom() check catches it (which only re-checks on threshold
        // CROSSING, not every frame, so it wouldn't catch this on its own).
        if (vehiclesCurrentlyHidden) go.SetActive(false);
    }

    /// Finds the closest road waypoint (by straight-line distance) to a world position - used
    /// to resolve a manual spawn point Transform to an actual graph node.
    private RoadNode NearestRoadNode(Vector3 worldPos)
    {
        RoadNode nearest = null;
        float bestSqrDist = float.MaxValue;
        foreach (var node in RoadManager.Instance.RoadNodes)
        {
            float d = (node.worldPosition - worldPos).sqrMagnitude;
            if (d < bestSqrDist) { bestSqrDist = d; nearest = node; }
        }
        return nearest;
    }

    private bool IsSpawnPointClear(Vector3 spawnPos)
    {
        float sqrRadius = spawnClearRadius * spawnClearRadius;
        foreach (var v in activeVehicles)
        {
            if (v == null) continue;
            if ((v.transform.position - spawnPos).sqrMagnitude < sqrRadius) return false;
        }
        return true;
    }

    /// Called by a TrafficAgent when it arrives at its destination - picks a new one from the
    /// same connected component instead of despawning, so cars keep driving indefinitely rather
    /// than vanishing every time they arrive somewhere. Returns null if fromNode is a dead end
    /// (map edge) or no suitable route could be found - the agent treats null as "time to
    /// despawn," which now reads as "left the city" rather than "car disappeared mid-street."
    ///
    /// avoidFirstStepTo: the node the vehicle just arrived FROM. Explicitly avoids picking a new
    /// route whose very first step goes straight back there - without this, a car would
    /// sometimes spin 180 degrees in place right after arriving, which looks wrong and can
    /// overlap traffic that was following behind it.
    public List<RoadNode> RequestContinuationPath(RoadNode fromNode, RoadNode avoidFirstStepTo)
    {
        if (fromNode == null) return null;
        if (fromNode.edges.Count == 1) return null; // dead end - let it despawn here, this is the rare "leaving the city" case

        EnsureComponentsBuilt(RoadManager.Instance.RoadNodes);
        var component = connectedComponents.Find(c => c.Contains(fromNode));
        if (component == null || component.Count < 2) return null;

        const int maxAttempts = 5;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            RoadNode newGoal = PickDifferentSpawnNodeInComponent(component, fromNode);
            if (newGoal == fromNode) continue;

            var path = RoadManager.Instance.FindPath(fromNode, newGoal);
            if (path == null || path.Count < 2) continue;

            // Reject if the very next step would backtrack to where we just came from.
            if (avoidFirstStepTo != null && path[1] == avoidFirstStepTo) continue;

            return path;
        }

        // Couldn't find a non-U-turn route after several tries (likely a dead-end-ish spur) -
        // despawn instead of forcing a jarring in-place spin.
        return null;
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