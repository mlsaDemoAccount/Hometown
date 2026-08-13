using UnityEngine;

/// Optional - attach to a specific vehicle prefab to give it its own speed range instead of
/// using TrafficSpawner's global min/max (e.g. a truck slower than a sports car).
/// TrafficSpawner automatically checks for this component on whatever prefab it spawns and
/// uses it instead of the global range if present.
public class VehicleSpeedProfile : MonoBehaviour
{
    public float minSpeed = 3f;
    public float maxSpeed = 6f;
}
