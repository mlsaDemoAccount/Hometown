using UnityEngine;

/// Attached to every placed building GameObject at runtime.
public class BuildingInstance : MonoBehaviour
{
    public int instanceId;
    public BuildingData data;
    public Vector2Int gridOrigin;      // bottom-left cell of footprint
    public int currentLevelIndex = 0;

    private GameObject currentVisual;

    public void Initialize(BuildingData buildingData, Vector2Int origin, int id)
    {
        data = buildingData;
        gridOrigin = origin;
        instanceId = id;
        currentLevelIndex = 0;
        ApplyVisualForLevel();
        CityStatsManager.Instance?.RegisterBuilding(this);
    }

    public bool CanUpgrade => currentLevelIndex < data.MaxLevelIndex;

    public int NextUpgradeCost =>
        CanUpgrade ? data.GetLevel(currentLevelIndex).upgradeCost : -1;

    public bool TryUpgrade(EconomyManager economy)
    {
        if (!CanUpgrade) return false;
        int cost = NextUpgradeCost;
        if (!economy.TrySpend(cost)) return false;

        currentLevelIndex++;
        ApplyVisualForLevel();
        CityStatsManager.Instance?.RecalculateAll(); // level change affects totals
        return true;
    }

    private void ApplyVisualForLevel()
    {
        var level = data.GetLevel(currentLevelIndex);
        // if (currentVisual != null) Destroy(currentVisual);
        // if (level != null && level.visualPrefab != null)
        // {
        //     currentVisual = Instantiate(level.visualPrefab, transform);
        //     currentVisual.transform.localPosition = Vector3.zero;
        // }
    }

    /// Current stat contribution based on upgrade level.
    public (float pop, float jobs, float pollution, float happiness, float garbage) GetStats()
    {
        var lvl = data.GetLevel(currentLevelIndex);
        if (lvl == null) return (0, 0, 0, 0, 0);
        return (lvl.populationOutput, lvl.jobsOutput, lvl.pollutionOutput, lvl.happinessOutput, lvl.garbageOutput);
    }

    void OnDestroy()
    {
        CityStatsManager.Instance?.UnregisterBuilding(this);
    }
}
