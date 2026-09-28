using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Spawns random city events (traffic jams, garbage, ambulance calls, police incidents,
/// fumigation needs) at random ROAD positions (uniform pick - not weighted toward population),
/// on a random timer, up to a concurrent cap. Player resolves an event for a funds reward, or
/// it silently auto-expires with no reward/penalty either way.
///
/// Data-driven: add a new CityEventData asset to eventTypes to add a new event type, no code
/// changes needed - this is the shared foundation all 5 planned event types build on.
public class CityEventManager : MonoBehaviour
{
    public static CityEventManager Instance { get; private set; }

    [Header("Event Types")]
    [Tooltip("Every kind of event that can spawn. Add a new CityEventData asset here to add a new event type.")]
    public List<CityEventData> eventTypes = new List<CityEventData>();

    [Header("Spawn Timing")]
    public float minSpawnGap = 30f;
    public float maxSpawnGap = 120f;
    [Tooltip("While this many events are active at once, the spawn timer effectively pauses until one is resolved or expires.")]
    public int maxConcurrentEvents = 4;

    [Header("Toast Notification (reuses CityNotificationPanel)")]
    [Tooltip("Direct reference, not a singleton lookup - same reasoning as everywhere else in this project (this panel likely starts inactive in the Hierarchy).")]
    public CityNotificationPanel toastPanel;
    [Tooltip("Seconds the camera takes to pan to the event's location when Accept is tapped on the toast.")]
    public float cameraFocusDuration = 1.5f;
    [Tooltip("Zoom level (orthoSize/height, matching MobileCameraController's minZoom/maxZoom range) the camera animates to when focusing on an event - so it actually gets a close look, not just a pan at whatever zoom the player happened to be at.")]
    public float cameraFocusZoom = 10f;

    [Header("Notification Center")]
    public NotificationCenter notificationCenter;

    private List<CityEventInstance> activeEvents = new List<CityEventInstance>();
    private float spawnTimer;
    private float nextSpawnGap;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        PickNextSpawnGap();
    }

    void Update()
    {
        if (activeEvents.Count >= maxConcurrentEvents) return; // cap reached - timer pauses

        spawnTimer += Time.deltaTime;
        if (spawnTimer >= nextSpawnGap)
        {
            spawnTimer = 0f;
            PickNextSpawnGap();
            TrySpawnEvent();
        }
    }

    private void PickNextSpawnGap()
    {
        nextSpawnGap = Random.Range(minSpawnGap, maxSpawnGap);
    }

    private void TrySpawnEvent()
    {
        if (eventTypes.Count == 0)
        {
            Debug.LogWarning("[CityEventManager] No event types configured in eventTypes - nothing to spawn.");
            return;
        }
        if (RoadManager.Instance == null || RoadManager.Instance.RoadNodes == null || RoadManager.Instance.RoadNodes.Count == 0)
        {
            Debug.LogWarning("[CityEventManager] No road nodes available - can't place an event.");
            return;
        }

        CityEventData data = eventTypes[Random.Range(0, eventTypes.Count)];
        RoadNode node = PickNonIntersectionNode(data);
        if (node == null)
        {
            string requirement = data.requiresStraightRoadAhead ? $" (needs {data.straightCellsRequired} straight road cells ahead)" : "";
            Debug.LogWarning($"[CityEventManager] Couldn't find a suitable road node to spawn '{data.displayName}' at{requirement}.");
            return;
        }

        SpawnEventAt(data, node.worldPosition, GetRoadAlignedRotation(node, data.rotationOffsetDegrees));
    }

    /// Avoids intersections specifically - a node with 3+ edges can have edges[0] pointing in
    /// ANY connected direction (including a perpendicular branch), which was making rotation
    /// look wrong/unpredictable there. A plain 2-edge (or fewer) node is a straight mid-road
    /// point where both edges run along the same line, giving a reliable road-aligned direction.
    ///
    /// If data.requiresStraightRoadAhead is set, candidates are further filtered to only nodes
    /// with a genuinely clear, straight run of road ahead (see HasStraightRoadAhead) - this is
    /// what lets a vehicle-departure event (garbage truck, etc.) safely drive forward without
    /// leaving the road or hitting a turn, and naturally excludes turning/intersection areas
    /// from ever being chosen for such an event.
    private RoadNode PickNonIntersectionNode(CityEventData data)
    {
        var candidates = RoadManager.Instance.RoadNodes.FindAll(n => !n.IsIntersection);
        if (candidates.Count == 0)
        {
            Debug.LogWarning("[CityEventManager] No non-intersection road nodes found - falling back to any road node (rotation may be less reliable here).");
            candidates = RoadManager.Instance.RoadNodes;
        }

        if (data.requiresStraightRoadAhead)
        {
            var straightCandidates = candidates.FindAll(n => HasStraightRoadAhead(n, data.straightCellsRequired));
            if (straightCandidates.Count == 0)
            {
                Debug.LogWarning($"[CityEventManager] No road node found with {data.straightCellsRequired} straight cells ahead for '{data.displayName}' - skipping this spawn attempt (will retry next timer).");
                return null;
            }
            candidates = straightCandidates;
        }

        return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : null;
    }

    /// Checks whether the road GENUINELY continues straight for at least `cellsRequired` grid
    /// cells worth of distance, by walking the WAYPOINT GRAPH itself - not by checking raw grid
    /// cell type. Raw cell-type checking has a real flaw: at a T-intersection or turn, a
    /// DIFFERENT crossing road segment can occupy the same cardinal-direction grid cells,
    /// making that check pass even though the road actually being followed turns there - this
    /// was the actual cause of events occasionally appearing near turns despite "passing" the
    /// old check. Walking the graph and requiring no intersection along the way, with each hop
    /// genuinely continuing in the same direction (not just any connected edge), avoids that
    /// false positive entirely.
    private bool HasStraightRoadAhead(RoadNode startNode, int cellsRequired)
    {
        if (GridManager.Instance == null || startNode.edges == null || startNode.edges.Count == 0) return false;

        float requiredWorldDistance = cellsRequired * GridManager.Instance.cellSize;

        Vector3 originalDir = startNode.edges[0].to.worldPosition - startNode.worldPosition;
        originalDir.y = 0f;
        if (originalDir.sqrMagnitude < 0.0001f) return false;
        originalDir.Normalize();

        RoadNode current = startNode;
        float coveredDistance = 0f;
        int safetyCounter = 0; // guards against any theoretical infinite loop on a malformed graph

        while (coveredDistance < requiredWorldDistance && safetyCounter < 50)
        {
            safetyCounter++;

            if (current.edges == null || current.edges.Count == 0) return false; // dead end before covering enough distance

            // Find whichever edge from THIS node continues most closely in the ORIGINAL
            // direction - not just edges[0] again, since after the first hop there's no
            // guarantee "keep going the same way" is still index 0 in the new node's own list.
            RoadEdge bestEdge = null;
            float bestDot = -2f;
            foreach (var edge in current.edges)
            {
                Vector3 edgeDir = edge.to.worldPosition - current.worldPosition;
                edgeDir.y = 0f;
                if (edgeDir.sqrMagnitude < 0.0001f) continue;

                float dot = Vector3.Dot(edgeDir.normalized, originalDir);
                if (dot > bestDot) { bestDot = dot; bestEdge = edge; }
            }

            if (bestEdge == null || bestDot < 0.9f) return false; // nothing continues straight enough here - a turn/branch exists

            RoadNode next = bestEdge.to;
            if (next.IsIntersection) return false; // an intersection interrupts the straight run before covering enough distance

            coveredDistance += Vector3.Distance(current.worldPosition, next.worldPosition);
            current = next;
        }

        return coveredDistance >= requiredWorldDistance;
    }

    /// Orients the spawn to match the actual direction the road runs at this node, using its
    /// real graph edges - NOT a fixed rotation. Roads turn/branch across the city, so a fixed
    /// rotation only looks right on segments matching whatever direction the prefab happened to
    /// be authored facing, and looks wrong everywhere else (this was the original bug).
    ///
    /// offsetDegrees corrects for the prefab's actual modeled forward direction not matching
    /// Unity's assumed +Z - without this, the result is consistently off by a fixed amount on
    /// EVERY road (not just some), since the underlying road-direction math itself is correct,
    /// only the prefab's own authored orientation assumption was wrong.
    private Quaternion GetRoadAlignedRotation(RoadNode node, float offsetDegrees)
    {
        if (node.edges == null || node.edges.Count == 0)
            return Quaternion.identity; // isolated node - shouldn't normally happen on a real road, fallback safely

        Vector3 direction = node.edges[0].to.worldPosition - node.worldPosition;
        direction.y = 0f; // stay level - don't tilt based on any height difference between nodes

        if (direction.sqrMagnitude < 0.0001f)
            return Quaternion.identity; // degenerate/zero-length edge, fallback safely

        Quaternion roadRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        return roadRotation * Quaternion.Euler(0f, offsetDegrees, 0f);
    }

    private void SpawnEventAt(CityEventData data, Vector3 position, Quaternion rotation)
    {
        if (data.visualPrefabVariants == null || data.visualPrefabVariants.Length == 0)
        {
            Debug.LogWarning($"[CityEventManager] '{data.displayName}' has no visualPrefabVariants assigned - skipping this spawn.");
            return;
        }

        GameObject prefab = data.visualPrefabVariants[Random.Range(0, data.visualPrefabVariants.Length)];
        GameObject obj = Instantiate(prefab, position, rotation);

        var instance = obj.GetComponent<CityEventInstance>();
        if (instance == null) instance = obj.AddComponent<CityEventInstance>();
        instance.Initialize(data);

        activeEvents.Add(instance);

        if (toastPanel == null)
            Debug.LogWarning($"[CityEventManager] toastPanel is not assigned in the Inspector - '{data.displayName}' spawned but its toast will never show (only the Notification Center list entry will appear).");
        else if (ToastNotificationCoordinator.Instance == null)
            Debug.LogWarning("[CityEventManager] ToastNotificationCoordinator not found in scene - event toasts will never show (only the Notification Center list entries will appear). Add a ToastNotificationCoordinator component to your scene.");
        else
            StartCoroutine(QueueEventToastRoutine(instance, data.notificationMessage, position));

        notificationCenter?.AddEntry(instance);
    }

    /// Waits until the shared toast slot is free, then shows this event's toast. A one-shot
    /// attempt at spawn time doesn't work: the stat-toast system polls the slot every frame, so
    /// it almost always wins and the event's single attempt got dropped. Queuing here means the
    /// toast shows as soon as whatever is currently on screen finishes. If the event is already
    /// resolved/expired while waiting, the toast is skipped - nothing left to announce.
    private IEnumerator QueueEventToastRoutine(CityEventInstance instance, string message, Vector3 eventPosition)
    {
        while (!ToastNotificationCoordinator.Instance.TryBeginPriorityToast())
        {
            if (instance == null) yield break; // event already gone (Unity null check) - don't announce it
            yield return null;
        }

        if (instance == null)
        {
            ToastNotificationCoordinator.Instance.EndToast(); // got the slot but nothing to show - release it immediately
            yield break;
        }

        yield return ShowToastRoutine(message, eventPosition);
    }

    private IEnumerator ShowToastRoutine(string message, Vector3 eventPosition)
    {
        var handle = toastPanel.Show(message);
        yield return new WaitUntil(() => handle.isDone);

        ToastNotificationCoordinator.Instance?.EndToast();

        if (handle.accepted && MobileCameraController.Instance != null)
            MobileCameraController.Instance.FocusOn(eventPosition, cameraFocusDuration, cameraFocusZoom);
    }

    /// Called directly by CityEventInstance when it resolves (Fix tapped) or auto-expires.
    public void RemoveEvent(CityEventInstance instance)
    {
        activeEvents.Remove(instance);
        notificationCenter?.RemoveEntry(instance);
    }
}