using UnityEngine;

/// Optional - attach to a specific pedestrian prefab to give it its own speed range instead of
/// using PedestrianSpawner's global min/max (e.g. a jogger faster than someone strolling).
/// PedestrianSpawner automatically checks for this component on whatever prefab it spawns and
/// uses it instead of the global range if present.
public class PedestrianSpeedProfile : MonoBehaviour
{
    public float minSpeed = 1.2f;
    public float maxSpeed = 2f;
}
