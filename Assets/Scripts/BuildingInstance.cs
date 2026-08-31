using UnityEngine;

/// Attached to every placed building GameObject at runtime.
public class BuildingInstance : MonoBehaviour
{
    public int instanceId;
    public BuildingData data;
    public Vector2Int gridOrigin;      // bottom-left cell of footprint
    public int currentLevelIndex = 0;
    public int rotationSteps = 0;      // 0-3, each step = 90 degrees clockwise

    private BuildingConstructionState constructionState; // optional - null if this prefab has none
    public BuildingConstructionState ConstructionState => constructionState;

    /// Swaps footprint width/height when rotated 90 or 270 degrees - a 3x4 footprint occupies
    /// a 4x3 area in grid space once rotated a quarter turn. Static so BuildingPlacementController
    /// can use the same logic for the ghost, before an actual BuildingInstance even exists.
    public static Vector2Int GetRotatedFootprint(Vector2Int size, int rotationSteps)
    {
        bool swapped = (rotationSteps % 2) != 0; // 1 or 3 = 90/270 degrees
        return swapped ? new Vector2Int(size.y, size.x) : size;
    }

    public void Initialize(BuildingData buildingData, Vector2Int origin, int id, int startingLevelIndex = 0)
    {
        data = buildingData;
        gridOrigin = origin;
        instanceId = id;
        currentLevelIndex = startingLevelIndex;
        constructionState = GetComponent<BuildingConstructionState>();
        // NOTE: visual is instantiated externally (by BuildingPlacementController) and this
        // component is added to that same object - do NOT spawn another visual here or you'll
        // get a duplicate nested under this one.
        CityStatsManager.Instance?.RegisterBuilding(this);
    }

    /// True while a fresh construction or upgrade timer is running - stats are zero and the
    /// player generally shouldn't be able to start another action until this finishes.
    public bool IsBusyConstructing =>
        constructionState != null && constructionState.phase != ConstructionPhase.Complete;

    public bool CanUpgrade => !IsBusyConstructing && currentLevelIndex < data.MaxLevelIndex;

    public int NextUpgradeCost =>
        CanUpgrade ? data.GetLevel(currentLevelIndex).upgradeCost : -1;

    public bool TryUpgrade(EconomyManager economy)
    {
        if (!CanUpgrade) return false;
        int cost = NextUpgradeCost;
        if (!economy.TrySpend(cost)) return false;

        int newLevelIndex = currentLevelIndex + 1;
        float duration = data.GetLevel(currentLevelIndex).upgradeDuration;

        if (constructionState != null && duration > 0f)
        {
            // Timed upgrade - level doesn't actually change until BuildingConstructionState
            // calls CompleteUpgrade() when the timer finishes.
            constructionState.BeginUpgrade(duration, newLevelIndex);
        }
        else
        {
            // No construction component on this prefab, or 0-duration upgrade - apply instantly.
            currentLevelIndex = newLevelIndex;
            CityStatsManager.Instance?.RecalculateAll();
            CitySaveManager.Instance?.SaveCity();
        }
        return true;
    }

    /// Called by BuildingConstructionState when a fresh construction timer finishes.
    public void CompleteInitialConstruction()
    {
        CityStatsManager.Instance?.RecalculateAll(); // this building's stats now count
        CitySaveManager.Instance?.SaveCity();
    }

    /// Called by BuildingConstructionState when an upgrade timer finishes.
    public void CompleteUpgrade(int newLevelIndex)
    {
        currentLevelIndex = newLevelIndex;
        CityStatsManager.Instance?.RecalculateAll();
        CitySaveManager.Instance?.SaveCity();
    }

    /// Current stat contribution based on upgrade level. Returns all zeros while under
    /// construction or upgrading - a half-built building shouldn't already count.
    public (float pop, float jobs, float pollution, float crimeRate, float traffic, float lifestyle) GetStats()
    {
        if (IsBusyConstructing) return (0, 0, 0, 0, 0, 0);

        var lvl = data.GetLevel(currentLevelIndex);
        if (lvl == null) return (0, 0, 0, 0, 0, 0);
        return (lvl.populationOutput, lvl.jobsOutput, lvl.pollutionOutput,
                lvl.crimeRateOutput, lvl.trafficOutput, lvl.lifestyleOutput);
    }

    void OnDestroy()
    {
        CityStatsManager.Instance?.UnregisterBuilding(this);
    }
}