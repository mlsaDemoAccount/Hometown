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
        RoadNode node = PickNonIntersectionNode();
        if (node == null)
        {
            Debug.LogWarning("[CityEventManager] Couldn't find a non-intersection road node to spawn at.");
            return;
        }

        SpawnEventAt(data, node.worldPosition, GetRoadAlignedRotation(node, data.rotationOffsetDegrees));
    }

    /// Avoids intersections specifically - a node with 3+ edges can have edges[0] pointing in
    /// ANY connected direction (including a perpendicular branch), which was making rotation
    /// look wrong/unpredictable there. A plain 2-edge (or fewer) node is a straight mid-road
    /// point where both edges run along the same line, giving a reliable road-aligned direction.
    private RoadNode PickNonIntersectionNode()
    {
        var candidates = RoadManager.Instance.RoadNodes.FindAll(n => !n.IsIntersection);
        if (candidates.Count == 0)
        {
            Debug.LogWarning("[CityEventManager] No non-intersection road nodes found - falling back to any road node (rotation may be less reliable here).");
            candidates = RoadManager.Instance.RoadNodes;
        }
        return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : null;
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

        if (toastPanel != null)
            StartCoroutine(ShowToastRoutine(data.notificationMessage, position));

        notificationCenter?.AddEntry(instance);
    }

    private IEnumerator ShowToastRoutine(string message, Vector3 eventPosition)
    {
        var handle = toastPanel.Show(message);
        yield return new WaitUntil(() => handle.isDone);

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