using System.Collections;
using UnityEngine;

/// GARBAGE-EVENT-SPECIFIC animation behavior. Attach this ADDITIONALLY to a garbage event
/// prefab, alongside the existing generic CityEventInstance component. Does NOT modify
/// CityEventInstance/CityEventData/CityEventManager at all - purely additive, so traffic jams
/// and every other event type are completely unaffected by anything in this file, and this
/// same isolation pattern can be copied for Ambulance/Police/Firefighter with entirely different
/// behavior later without any of them interfering with each other.
///
/// IDLE LOOP (before Fix is tapped): worker1 patrols back and forth between
/// worker1GarbageSpot and worker1TruckSpot, waiting idleWaitSeconds at each. worker2 just plays
/// its own idle animation in place via the Animator already on it - no movement, and this
/// script never touches it until Fix is tapped.
///
/// ON FIX TAPPED: garbageVisual hides immediately, Fix button hides immediately (can't be
/// tapped again), each worker walks to its own gate position and is deactivated the instant it
/// arrives (looks like boarding), then once both that are actually present have finished
/// boarding, the truck drives away via EventVehicleDeparture.SendStraightWithTraffic() - a
/// SHARED utility (see that file) that reuses TrafficAgent's own movement/collision-avoidance
/// via a synthetic straight-line path, so the truck genuinely respects other traffic without
/// this script duplicating any of that logic. Any future event type (police, ambulance,
/// fumigation) calls the exact same shared method for the same behavior.
///
/// EVERY reference below is optional - if a piece is missing on a given prefab (e.g. only one
/// worker exists, or no gate position is set for it), this script skips exactly that piece of
/// behavior instead of erroring, so the same script works across garbage prefabs with
/// different setups without needing every field filled in.
[RequireComponent(typeof(CityEventInstance))]
public class GarbageEventBehavior : MonoBehaviour
{
    [Header("Garbage Visual")]
    [Tooltip("The garbage pile itself - hidden the instant Fix is tapped. Leave unassigned to skip.")]
    public GameObject garbageVisual;

    [Header("Fix Canvas")]
    [Tooltip("The WHOLE Fix canvas (button + any label text) - hidden the instant Fix is tapped. Use this instead of (or as well as) CityEventInstance's own fixButtonRoot if that one only covers the button itself and not sibling text on the same canvas. Leave unassigned to just rely on fixButtonRoot.")]
    public GameObject fixCanvasRoot;

    [Header("Worker 1 - Patrols, then Boards")]
    public Transform worker1;
    [Tooltip("Where worker1 stands while collecting garbage during the idle loop.")]
    public Transform worker1GarbageSpot;
    [Tooltip("Where worker1 stands at the truck's back during the idle loop.")]
    public Transform worker1TruckSpot;
    [Tooltip("Where worker1 walks to on Fix, then gets deactivated (looks like boarding).")]
    public Transform worker1GatePosition;

    [Header("Worker 2 - Idle Only, then Boards")]
    [Tooltip("Optional second worker - untouched by this script until Fix is tapped (its own idle animation plays independently). Leave unassigned entirely if a given prefab only has one worker.")]
    public Transform worker2;
    public Transform worker2GatePosition;

    [Header("Movement")]
    public float idleWaitSeconds = 2f;
    public float workerWalkSpeed = 2f;

    [Header("Truck Drive-Away (simple straight departure)")]
    [Tooltip("Temporary toggle - if false, the truck stays where it is and is removed along with the rest of the event, instead of driving away. Useful for isolating other testing (e.g. placement validation) from the departure movement itself.")]
    public bool enableTruckDeparture = false;
    [Tooltip("The truck itself - drives straight forward in whatever direction it's already facing, then is removed. Leave unassigned to skip - the truck just stays put.")]
    public Transform truck;
    [Tooltip("Seconds after workers finish boarding before the truck starts driving away.")]
    public float truckDepartDelay = 0.5f;
    [Tooltip("How many grid cells the truck drives before disappearing - should match (or be <=) this event's CityEventData.straightCellsRequired, since that's what CityEventManager already guarantees is clear straight road ahead when this event was placed.")]
    public int departGridCells = 3;
    [Tooltip("Speed the truck drives forward during departure.")]
    public float truckDepartSpeed = 6f;
    [Tooltip("Collision-avoidance ray settings for the truck - tune these here since TrafficAgent only gets added to it at runtime, so there's normally no Inspector for these values otherwise. Same reusable settings type as any future event vehicle (police, ambulance, etc.) would use.")]
    public EventVehicleTrafficSettings truckTrafficSettings;

    private CityEventInstance eventInstance;
    private bool resolving = false;
    private bool worker1Boarded = false;
    private bool worker2Boarded = false;
    private Coroutine worker1PatrolCoroutine;

    void Awake()
    {
        eventInstance = GetComponent<CityEventInstance>();
    }

    void Start()
    {
        // Take over the Fix button entirely - CityEventInstance's own default click handler
        // would otherwise ALSO fire on the same tap, hiding things and destroying this object
        // on its own generic timer while this script's longer custom sequence is still
        // running. Removing its listener and adding ours instead avoids that conflict.
        if (eventInstance.fixButton != null)
        {
            eventInstance.fixButton.onClick.RemoveAllListeners();
            eventInstance.fixButton.onClick.AddListener(OnFixTapped);
        }

        if (worker1 != null && worker1GarbageSpot != null && worker1TruckSpot != null)
            worker1PatrolCoroutine = StartCoroutine(Worker1PatrolLoop());
    }

    private IEnumerator Worker1PatrolLoop()
    {
        while (!resolving)
        {
            yield return MoveTo(worker1, worker1GarbageSpot.position, workerWalkSpeed);
            if (resolving) yield break;
            yield return new WaitForSeconds(idleWaitSeconds);
            if (resolving) yield break;

            yield return MoveTo(worker1, worker1TruckSpot.position, workerWalkSpeed);
            if (resolving) yield break;
            yield return new WaitForSeconds(idleWaitSeconds);
        }
    }

    private void OnFixTapped()
    {
        if (resolving) return;
        resolving = true;

        // Explicitly stop the patrol loop RIGHT NOW, regardless of where it currently is -
        // its own internal "if (resolving) yield break" checks only run BETWEEN operations
        // (after a MoveTo or WaitForSeconds finishes), not continuously. If Fix is tapped while
        // that loop is mid-MoveTo, it wouldn't notice resolving=true until it finished reaching
        // its OWN current destination - meanwhile BoardWorker1() (started moments from now)
        // would ALSO be moving worker1 toward a DIFFERENT destination at the same time, both
        // coroutines fighting over the same transform every frame. Neither MoveTo's distance
        // check could ever settle in that state, so BoardWorker1() would never complete,
        // worker1Boarded would never become true, and the whole sequence would hang forever -
        // this was the exact intermittent freeze (only happened if the tap landed mid-patrol-
        // movement, which is why it worked most of the time). StopCoroutine halts it
        // immediately no matter where it currently is, closing that race entirely.
        if (worker1PatrolCoroutine != null)
        {
            StopCoroutine(worker1PatrolCoroutine);
            worker1PatrolCoroutine = null;
        }

        if (garbageVisual != null) garbageVisual.SetActive(false);

        // Hide immediately so the player can't tap it again mid-sequence - fixCanvasRoot
        // covers the WHOLE canvas (button + text), fixButtonRoot is CityEventInstance's own
        // button-only reference kept as a fallback in case fixCanvasRoot isn't assigned.
        if (fixCanvasRoot != null) fixCanvasRoot.SetActive(false);
        else if (eventInstance.fixButtonRoot != null) eventInstance.fixButtonRoot.SetActive(false);

        StartCoroutine(ResolveSequence());
    }

    private IEnumerator ResolveSequence()
    {
        Debug.Log($"[GarbageEventBehavior] ResolveSequence started on '{gameObject.name}'.");

        bool needWorker1 = worker1 != null && worker1GatePosition != null;
        bool needWorker2 = worker2 != null && worker2GatePosition != null;

        worker1Boarded = !needWorker1; // already "done" if this prefab has no such worker/position
        worker2Boarded = !needWorker2;

        if (needWorker1) StartCoroutine(BoardWorker1());
        if (needWorker2) StartCoroutine(BoardWorker2());

        while (!worker1Boarded || !worker2Boarded)
            yield return null;

        Debug.Log("[GarbageEventBehavior] Both workers boarded - waiting truckDepartDelay.");

        yield return new WaitForSeconds(truckDepartDelay);

        Debug.Log("[GarbageEventBehavior] Delay finished - sending truck away via shared EventVehicleDeparture utility.");

        // Send the truck away COMPLETELY (reparent + drive off, respecting traffic) BEFORE
        // calling ResolveByPlayer() - not after. GarbageEventBehavior lives on the same
        // GameObject as CityEventInstance, so if ResolveByPlayer() deactivates or schedules
        // destruction of the event root, EVERY coroutine running on this component - including
        // this whole sequence - halts immediately, wherever it happens to be. Finishing the
        // truck's departure first makes it fully independent before that risk exists at all.
        //
        // Uses the SHARED EventVehicleDeparture utility - reuses TrafficAgent's real collision-
        // avoidance instead of a separate implementation, and any future event type (police,
        // ambulance, fumigation) calls this exact same method for the same behavior.
        if (enableTruckDeparture && truck != null)
        {
            // Duration computed from grid cells, not a fixed time - matches EXACTLY the
            // straight-road guarantee CityEventManager already validated when placing this
            // event (data.straightCellsRequired). Using GridManager's real cellSize means this
            // stays correct automatically if the grid's cell size or the truck's speed ever
            // change, without needing to manually re-tune a separate duration value.
            float cellSize = GridManager.Instance != null ? GridManager.Instance.cellSize : 1f;
            float departDistance = departGridCells * cellSize;
            float computedDuration = truckDepartSpeed > 0f ? departDistance / truckDepartSpeed : 3f;

            yield return EventVehicleDeparture.SendStraightWithTraffic(truck, computedDuration, truckDepartSpeed, truckTrafficSettings);
        }
        else if (!enableTruckDeparture)
        {
            Debug.Log("[GarbageEventBehavior] Truck departure disabled (enableTruckDeparture = false) - truck stays put and will be removed with the rest of the event.");
        }
        else
        {
            Debug.LogWarning("[GarbageEventBehavior] truck field is not assigned (null) - skipping departure entirely. Check the Truck field in the Inspector on this event's GarbageEventBehavior component.");
        }

        Debug.Log("[GarbageEventBehavior] Truck departure finished - calling ResolveByPlayer().");

        eventInstance.ResolveByPlayer();

        Debug.Log("[GarbageEventBehavior] ResolveSequence complete.");
    }

    private IEnumerator BoardWorker1()
    {
        yield return MoveTo(worker1, worker1GatePosition.position, workerWalkSpeed);
        worker1.gameObject.SetActive(false);
        worker1Boarded = true;
        Debug.Log("[GarbageEventBehavior] worker1 boarded.");
    }

    private IEnumerator BoardWorker2()
    {
        yield return MoveTo(worker2, worker2GatePosition.position, workerWalkSpeed);
        worker2.gameObject.SetActive(false);
        worker2Boarded = true;
        Debug.Log("[GarbageEventBehavior] worker2 boarded.");
    }

    private IEnumerator MoveTo(Transform mover, Vector3 destination, float speed)
    {
        while (Vector3.Distance(mover.position, destination) > 0.05f)
        {
            mover.position = Vector3.MoveTowards(mover.position, destination, speed * Time.deltaTime);

            Vector3 direction = destination - mover.position;
            if (direction.sqrMagnitude > 0.001f)
                mover.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

            yield return null;
        }
    }
}