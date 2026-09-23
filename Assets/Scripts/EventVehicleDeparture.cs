using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Shared utility for sending an event vehicle (garbage truck, police car, ambulance, etc.)
/// driving straight away from wherever it's parked, WHILE respecting other traffic - reuses
/// TrafficAgent's own movement/rotation/collision-avoidance pipeline directly (via a single
/// synthetic 2-point path: current position -> a point far enough ahead to cover the whole
/// departure), instead of duplicating that logic separately. Any future tuning/improvements
/// made to TrafficAgent automatically apply here too, with zero code duplication - and any
/// future event type (police, ambulance, fumigation) can call this exact same method.
///
/// DELIBERATELY SIMPLE: no waypoint-following/road-turn-steering, no repeated path refresh, no
/// lane offset - the vehicle just drives straight in whatever direction it's already facing,
/// dead-center on the road, for the whole duration. A more elaborate segmented-steering version
/// was tried and reverted after repeated issues (jerking, early destruction, leaving the road
/// after sharp turns) - this single-path version is intentionally simple and reliable instead.
///
/// Also respects the same zoom-based hide/show TrafficSpawner already applies to regular
/// traffic (reusing its hideVehiclesAboveZoom threshold directly) - so this vehicle stops
/// rendering/simulating at the same zoom level every other car already does, instead of being
/// invisible to that whole system.
public static class EventVehicleDeparture
{
    /// Reparents the vehicle onto TrafficSpawner's own container (if available), then drives it
    /// straight for `duration` seconds - respecting other traffic via TrafficAgent's real
    /// collision-avoidance throughout - then destroys it once that time is up.
    ///
    /// trafficSettings is optional - pass null to just use TrafficAgent's own built-in
    /// defaults. Pass an EventVehicleTrafficSettings (exposed as an Inspector field on your
    /// event behavior script) to tune the collision-avoidance ray for this vehicle, since
    /// TrafficAgent only gets added to it at runtime and otherwise has no Inspector to tune.
    public static IEnumerator SendStraightWithTraffic(Transform vehicle, float duration, float speed, EventVehicleTrafficSettings trafficSettings = null)
    {
        if (vehicle == null) yield break;

        Transform parent = TrafficSpawner.Instance != null ? TrafficSpawner.Instance.trafficParent : null;
        vehicle.SetParent(parent, true); // true = keep current world position/rotation exactly as-is, no visual jump

        var agent = vehicle.GetComponent<TrafficAgent>();
        if (agent == null) agent = vehicle.gameObject.AddComponent<TrafficAgent>();

        agent.laneOffset = 0f; // dead-center on the road - no lane offset
        if (TrafficSpawner.Instance != null)
            agent.driveOnRight = TrafficSpawner.Instance.driveOnRight;
        if (speed > 0f) agent.moveSpeed = speed;

        if (trafficSettings != null)
        {
            agent.vehicleLayerMask = trafficSettings.vehicleLayerMask;
            agent.verticalOffset = trafficSettings.verticalOffset;
            agent.lookAheadDistance = trafficSettings.lookAheadDistance;
            agent.minFollowDistance = trafficSettings.minFollowDistance;
            agent.rayOriginForwardOffset = trafficSettings.rayOriginForwardOffset;
            agent.rayHeightOffset = trafficSettings.rayHeightOffset;
            agent.showDebugRay = trafficSettings.showDebugRay;
        }

        float effectiveSpeed = speed > 0f ? speed : agent.moveSpeed;

        // Single straight-line path, sized generously beyond what the vehicle could possibly
        // travel in `duration` seconds even at full uninterrupted speed. This guarantees
        // TrafficAgent never reaches the end of its own path during the departure window, so
        // its internal "reached the end, find a new destination" logic (which would destroy the
        // vehicle when no valid continuation exists) never triggers - no repeated SetPath calls
        // needed at all.
        float safeDistance = effectiveSpeed * duration * 1.5f;

        Vector3 startPos = vehicle.position;
        Vector3 endPos = startPos + vehicle.forward * safeDistance;
        endPos.y = startPos.y;

        var startNode = new RoadNode(Vector2Int.zero, startPos);
        var endNode = new RoadNode(Vector2Int.zero, endPos);
        agent.SetPath(new List<RoadNode> { startNode, endNode });

        Debug.Log($"[EventVehicleDeparture] Sending '{vehicle.name}' straight for {duration}s (path distance {safeDistance:F1}).");

        float elapsed = 0f;
        bool currentlyHidden = false;

        while (vehicle != null && elapsed < duration)
        {
            elapsed += Time.deltaTime;

            // Same zoom-based show/hide regular traffic already gets (see TrafficSpawner) -
            // reuses ITS threshold directly rather than a separate configurable value, so this
            // truck always matches whatever distance regular cars already disappear at. Without
            // this, the truck would keep simulating/rendering even when zoomed out past the
            // point every other vehicle in the city has already been hidden for performance.
            if (MobileCameraController.Instance != null && TrafficSpawner.Instance != null)
            {
                bool shouldHide = MobileCameraController.Instance.CurrentZoom > TrafficSpawner.Instance.hideVehiclesAboveZoom;
                if (shouldHide != currentlyHidden)
                {
                    currentlyHidden = shouldHide;
                    vehicle.gameObject.SetActive(!shouldHide);
                }
            }

            yield return null;
        }

        if (vehicle != null)
        {
            Debug.Log($"[EventVehicleDeparture] '{vehicle.name}' finished after {elapsed:F1}s - destroying it.");
            Object.Destroy(vehicle.gameObject);
        }
        else
        {
            Debug.LogWarning("[EventVehicleDeparture] Vehicle was already destroyed before this coroutine finished.");
        }
    }
}