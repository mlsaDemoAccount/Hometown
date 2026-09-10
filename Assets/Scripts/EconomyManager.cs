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
        return true;
    }

    public void AddFunds(int amount)
    {
        currentFunds += amount;
    }

    /// Refund a percentage on demolish, e.g. 50%.
    public void RefundDemolish(int originalCost, float refundPercent = 0.5f)
    {
        AddFunds(Mathf.RoundToInt(originalCost * refundPercent));
    }

    /// Sets funds directly - used by CitySaveManager when loading a saved city, not for normal
    /// gameplay spending (use TrySpend/AddFunds for that).
    public void SetFunds(int amount)
    {
        currentFunds = amount;
    }
}