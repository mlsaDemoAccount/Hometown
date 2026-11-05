using UnityEngine;

/// No event/Action here - anything that needs to react to funds changes (funds display,
/// shop item affordability, etc.) reads currentFunds directly each frame instead of
/// subscribing to a notification. See PlayerProgressManager and ShopItemButton for the
/// polling pattern used instead.
public class EconomyManager : MonoBehaviour
{
    public static EconomyManager Instance { get; private set; }

    public int currentFunds = 5000;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public bool TrySpend(int amount)
    {
        if (amount < 0 || currentFunds < amount) return false;
        currentFunds -= amount;
        SyncFundsToServer();
        return true;
    }

    public void AddFunds(int amount)
    {
        currentFunds += amount;
        SyncFundsToServer();
    }

    // Queues ONLY the funds value for the profile PUT (debounced inside AuthManager).
    private void SyncFundsToServer()
    {
        if (Coop.Active || CityLauncher.IsCityMode) return; // co-op funds belong to the shared city, not the player's own profile
        AuthManager.Instance?.QueueProfileUpdate("funds", currentFunds);
    }

    /// Refund a percentage on demolish, e.g. 50%.
    public void RefundDemolish(int originalCost, float refundPercent = 0.5f)
    {
        AddFunds(Mathf.RoundToInt(originalCost * refundPercent));
    }

    /// Sets funds directly - used when loading a saved city / the server profile, not for normal
    /// gameplay spending (use TrySpend/AddFunds for that). Does NOT push to the server.
    public void SetFunds(int amount)
    {
        currentFunds = amount;
    }
}