using UnityEngine;
using System;

public class EconomyManager : MonoBehaviour
{
    public static EconomyManager Instance { get; private set; }

    public int currentFunds = 5000;
    public event Action<int> OnFundsChanged;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public bool TrySpend(int amount)
    {
        if (amount < 0 || currentFunds < amount) return false;
        currentFunds -= amount;
        OnFundsChanged?.Invoke(currentFunds);
        return true;
    }

    public void AddFunds(int amount)
    {
        currentFunds += amount;
        OnFundsChanged?.Invoke(currentFunds);
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
        OnFundsChanged?.Invoke(currentFunds);
    }
}