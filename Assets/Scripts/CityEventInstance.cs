using UnityEngine;
using UnityEngine.UI;

/// Runtime component on a spawned city event prefab (traffic jam, garbage pile, etc.) -
/// handles the world-space Fix button, the resolve sequence (particle plays, event clears,
/// funds awarded), and the auto-expiry timer (silently removed with no reward/penalty if the
/// player never resolves it in time).
public class CityEventInstance : MonoBehaviour
{
    [Header("World-Space UI (optional)")]
    [Tooltip("The problem's visible mesh/model - hidden the instant Fix is tapped, so it reads as 'already fixed, particle still finishing' rather than 'still broken with sparkles on it'. Leave unassigned to just let the whole object stay as-is until it's destroyed.")]
    public GameObject mainVisual;
    [Tooltip("Canvas/button shown above the event in the world - tap to resolve directly without going through the Notification Center.")]
    public GameObject fixButtonRoot;
    public Button fixButton;

    public CityEventData Data { get; private set; }
    public float RemainingLifetime { get; private set; }

    private bool isResolving = false;
    private bool isExpired = false;

    public void Initialize(CityEventData data)
    {
        Data = data;
        RemainingLifetime = data.autoExpireDuration;

        if (fixButton != null)
        {
            fixButton.onClick.RemoveAllListeners();
            fixButton.onClick.AddListener(ResolveByPlayer);
        }
    }

    void Update()
    {
        if (isResolving || isExpired) return;

        RemainingLifetime -= Time.deltaTime;
        if (RemainingLifetime <= 0f)
            AutoExpire();
    }

    /// Called by the world-space Fix button OR a Notification Center entry's Fix button - both
    /// paths lead here, so resolving from either place behaves identically.
    public void ResolveByPlayer()
    {
        if (isResolving || isExpired) return;
        isResolving = true;

        if (mainVisual != null) mainVisual.SetActive(false);
        if (fixButtonRoot != null) fixButtonRoot.SetActive(false);

        if (Data.resolveParticlePrefab != null)
            Instantiate(Data.resolveParticlePrefab, transform.position, Quaternion.identity);

        // The city's funds belong to the host: a guest only reports which event was fixed.
        if (Coop.IsGuest) CityNetworkSync.Instance.RequestEventReward(Data.displayName);
        else EconomyManager.Instance?.AddFunds(Random.Range(Data.minFundsReward, Data.maxFundsReward + 1));

        CityEventManager.Instance?.RemoveEvent(this);
        Destroy(gameObject, Data.resolveDuration);
    }

    private void AutoExpire()
    {
        isExpired = true;
        CityEventManager.Instance?.RemoveEvent(this);
        Destroy(gameObject);
    }
}
