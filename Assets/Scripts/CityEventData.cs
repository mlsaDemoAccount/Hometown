using UnityEngine;

public enum CityEventType { TrafficJam, Garbage, Ambulance, Police, Fumigation }

[CreateAssetMenu(fileName = "NewCityEvent", menuName = "CityBuilder/City Event Data")]
public class CityEventData : ScriptableObject
{
    [Header("Identity")]
    public CityEventType eventType;
    public string displayName;
    [TextArea] public string notificationMessage;

    [Header("Visuals")]
    [Tooltip("Random variant picked each time this event spawns - e.g. different traffic jam scenes (two cars stopped, an accident, etc). Add as many as you like for visual variety within this one type.")]
    public GameObject[] visualPrefabVariants;
    [Tooltip("Particle effect played at the event's position when the player taps Fix.")]
    public GameObject resolveParticlePrefab;
    [Tooltip("Correction applied on top of the computed road-direction rotation. The road-alignment math assumes the prefab's modeled 'forward' faces local +Z - if it was actually modeled facing a different direction, every spawn will be consistently off by a fixed amount (usually 90 or 180). Try 0, 90, 180, 270 here until it looks right - once correct for one road, it's correct for all of them.")]
    public float rotationOffsetDegrees = 0f;

    [Header("Resolution")]
    [Tooltip("Seconds the resolve sequence takes once Fix is tapped - the particle plays during this window before the event object is fully removed.")]
    public float resolveDuration = 3f;
    [Tooltip("Funds reward is a random value in this range each time - set min = max for a fixed amount per event type.")]
    public int minFundsReward = 20;
    public int maxFundsReward = 50;

    [Header("Lifetime")]
    [Tooltip("Seconds before this event auto-expires if the player never resolves it - removed silently with no reward and no penalty.")]
    public float autoExpireDuration = 600f; // 10 minutes
}