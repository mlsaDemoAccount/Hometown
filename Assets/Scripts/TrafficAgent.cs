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

    /// Returns 0-1: how much of moveSpeed to actually use this frame, based on whether another
    /// vehicle is close ahead. 1 = full speed (nothing ahead), 0 = fully stopped (too close).
    /// Between minFollowDistance and lookAheadDistance, speed scales smoothly - this is what
    /// makes a queue form gradually behind a stopped car instead of every car in line braking
    /// instantly to zero at the same moment.
    private float GetSpeedMultiplierFromVehicleAhead(Vector3 moveDir)
    {
        Vector3 rayOrigin = transform.position + moveDir * rayOriginForwardOffset + Vector3.up * 0.3f;

        if (Physics.Raycast(rayOrigin, moveDir, out RaycastHit hit, lookAheadDistance, vehicleLayerMask))
        {
            if (hit.collider.gameObject == gameObject) return 1f; // safety check, shouldn't happen given the forward offset

            if (hit.distance <= minFollowDistance) return 0f;

            float t = Mathf.InverseLerp(minFollowDistance, lookAheadDistance, hit.distance);
            return Mathf.Clamp01(t);
        }

        return 1f; // nothing ahead within range - full speed
    }
}
