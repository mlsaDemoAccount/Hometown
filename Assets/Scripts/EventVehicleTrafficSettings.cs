using UnityEngine;

/// Tunable collision-avoidance settings for an event vehicle (garbage truck, police car,
/// ambulance, etc.) driven via EventVehicleDeparture. Since TrafficAgent only gets added to
/// these vehicles at RUNTIME (they don't have it pre-attached in the Editor like regular
/// traffic prefabs do), there's normally no Inspector to tune its ray values for - this struct
/// is that Inspector surface instead: expose ONE field of this type on any event behavior
/// script (e.g. GarbageEventBehavior.truckTrafficSettings), tune it directly there, and pass it
/// into EventVehicleDeparture.SendStraightWithTraffic(). Same reusable struct for every future
/// event type - no per-event-type duplication of these fields.
[System.Serializable]
public class EventVehicleTrafficSettings
{
    [Tooltip("Layer(s) other vehicles are on - assign the same 'Vehicle' layer your regular traffic cars use, so this event vehicle actually detects them.")]
    public LayerMask vehicleLayerMask;
    [Tooltip("Same as TrafficAgent's own Vertical Offset - raises/lowers the vehicle's visual height above the movement path, e.g. to match a model whose pivot isn't at ground level.")]
    public float verticalOffset = 0f;
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
}