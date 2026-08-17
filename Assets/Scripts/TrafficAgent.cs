using System.Collections.Generic;
using UnityEngine;

/// Moves a single vehicle along a road-graph path, offset sideways onto its correct lane so
/// opposing traffic doesn't drive head-on down the centerline. Slows/stops for vehicles ahead
/// via a forward raycast - queuing/jams emerge naturally from every car doing this independently.
/// NOTE: still no intersection stopping (stop signs/lights) yet - that's next. This handles
/// same-direction following distance, not right-of-way at crossings.
public class TrafficAgent : MonoBehaviour
{
    public float moveSpeed = 4f;
    public float rotationSpeed = 8f;
    [Tooltip("Lifts the vehicle above the waypoint's height - tune this per prefab if the model's pivot isn't at its base/wheels (most common cause of a car appearing to sink into the road).")]
    public float verticalOffset = 0f;
    [HideInInspector] public float laneOffset = 0.4f;
    [HideInInspector] public bool driveOnRight = true;

    [Header("Collision Avoidance")]
    [Tooltip("Layer(s) other vehicles are on. Assign a dedicated 'Vehicle' layer to all vehicle prefabs and select it here.")]
    public LayerMask vehicleLayerMask;
    [Tooltip("How far ahead to check for another vehicle.")]
    public float lookAheadDistance = 3f;
    [Tooltip("Distance at which the vehicle stops completely rather than just slowing.")]
    public float minFollowDistance = 1.2f;
    [Tooltip("How far in front of the vehicle's center the check ray starts - roughly your model's front bumper, so it doesn't detect its own collider.")]
    public float rayOriginForwardOffset = 0.6f;
    [Tooltip("Vertical height of the check ray, relative to the vehicle's pivot - tune this so the ray actually crosses through other vehicles' colliders instead of passing over/under them.")]
    public float rayHeightOffset = 0.3f;
    [Tooltip("Draws the forward check ray in the Scene view while playing - green = clear, yellow = slowing down, red = stopped.")]
    public bool showDebugRay = true;

    private List<Vector3> waypoints; // lane-offset positions, computed once from the raw node path
    private List<RoadNode> currentNodePath; // kept around so we know where we ended up on arrival
    private int currentIndex = 0;

    public void SetPath(List<RoadNode> nodePath)
    {
        currentNodePath = nodePath;
        waypoints = BuildLaneOffsetWaypoints(nodePath);
        currentIndex = 0;
    }

    /// Converts raw centerline node positions into lane-offset waypoints via the shared utility.
    private List<Vector3> BuildLaneOffsetWaypoints(List<RoadNode> nodePath) =>
        PathOffsetUtility.BuildOffsetWaypoints(nodePath, laneOffset, driveOnRight);

    void Update()
    {
        if (waypoints == null || currentIndex >= waypoints.Count)
        {
            TryContinueToNewDestination();
            return;
        }

        Vector3 target = waypoints[currentIndex] + Vector3.up * verticalOffset;
        Vector3 toTarget = target - transform.position;
        toTarget.y = 0;

        Vector3 moveDir = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : transform.forward;

        float speedMultiplier = GetSpeedMultiplierFromVehicleAhead(moveDir);
        float distanceThisFrame = moveSpeed * speedMultiplier * Time.deltaTime;

        // Always allow rotation to settle even while stopped/slowed, so a queued car still
        // faces the right way rather than freezing mid-turn.
        if (moveDir.sqrMagnitude > 0.0001f)
        {
            Quaternion look = Quaternion.LookRotation(moveDir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, rotationSpeed * Time.deltaTime);
        }

        if (speedMultiplier <= 0.001f) return; // fully stopped this frame - blocked by a vehicle ahead

        if (toTarget.magnitude <= distanceThisFrame)
        {
            // Would reach (or overshoot) the waypoint this frame - snap exactly onto it instead
            // of stepping past it. This is what was letting fast-moving cars blow through turns
            // and cut across off the road at speed.
            transform.position = target;
            currentIndex++;
            return;
        }

        transform.position += moveDir * distanceThisFrame;

        // NEXT UP: checking the upcoming node's IsIntersection / controlType before proceeding
        // through it (stop signs, then lights) - this handles following distance, not
        // right-of-way at crossings, so perpendicular traffic can still meet at an intersection.
    }

    /// Called on arrival instead of despawning immediately. Asks TrafficSpawner for a new
    /// destination from here; only actually despawns if that returns null (a dead end, or no
    /// route found), which is now the rare case rather than happening on every arrival.
    private void TryContinueToNewDestination()
    {
        RoadNode endNode = (currentNodePath != null && currentNodePath.Count > 0)
            ? currentNodePath[currentNodePath.Count - 1]
            : null;

        // The node just before arrival - used to reject a continuation route that would
        // immediately double back the way we came (see RequestContinuationPath).
        RoadNode arrivedFromNode = (currentNodePath != null && currentNodePath.Count >= 2)
            ? currentNodePath[currentNodePath.Count - 2]
            : null;

        var newPath = (endNode != null && TrafficSpawner.Instance != null)
            ? TrafficSpawner.Instance.RequestContinuationPath(endNode, arrivedFromNode)
            : null;

        if (newPath == null || newPath.Count < 2)
        {
            Destroy(gameObject); // dead end reached, or no further route - "left the city"
            return;
        }

        SetPath(newPath);
    }

    /// Returns 0-1: how much of moveSpeed to actually use this frame, based on whether another
    /// vehicle is close ahead. 1 = full speed (nothing ahead), 0 = fully stopped (too close).
    /// Between minFollowDistance and lookAheadDistance, speed scales smoothly - this is what
    /// makes a queue form gradually behind a stopped car instead of every car in line braking
    /// instantly to zero at the same moment.
    private float GetSpeedMultiplierFromVehicleAhead(Vector3 moveDir)
    {
        Vector3 rayOrigin = transform.position + moveDir * rayOriginForwardOffset + Vector3.up * rayHeightOffset;

        float multiplier = 1f;
        float debugRayLength = lookAheadDistance;
        Color debugColor = Color.green;

        if (Physics.Raycast(rayOrigin, moveDir, out RaycastHit hit, lookAheadDistance, vehicleLayerMask))
        {
            debugRayLength = hit.distance; // draw only up to the hit point, not the full lookAheadDistance

            if (hit.collider.gameObject != gameObject) // safety check, shouldn't hit self given the forward offset
            {
                if (hit.distance <= minFollowDistance)
                {
                    multiplier = 0f;
                    debugColor = Color.red;
                }
                else
                {
                    multiplier = Mathf.Clamp01(Mathf.InverseLerp(minFollowDistance, lookAheadDistance, hit.distance));
                    debugColor = Color.yellow;
                }
            }
        }

        if (showDebugRay)
            Debug.DrawRay(rayOrigin, moveDir * debugRayLength, debugColor);

        return multiplier;
    }
}