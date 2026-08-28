using UnityEngine;

/// Attached to every placed building GameObject at runtime.
public class BuildingInstance : MonoBehaviour
{
    public int instanceId;
    public BuildingData data;
    public Vector2Int gridOrigin;      // bottom-left cell of footprint
    public int currentLevelIndex = 0;
    public int rotationSteps = 0;      // 0-3, each step = 90 degrees clockwise

    private GameObject currentVisual;

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
        // NOTE: visual is instantiated externally (by BuildingPlacementController) and this
        // component is added to that same object - do NOT spawn another visual here or you'll
        // get a duplicate nested under this one. Upgrade visual-swapping is disabled for now;
        // TryUpgrade below only updates stats/level, not the model. Revisit when needed.
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
        // Visual swapping on upgrade is disabled for now (see note in Initialize).
        // ApplyVisualForLevel();
        CityStatsManager.Instance?.RecalculateAll(); // level change affects totals
        CitySaveManager.Instance?.SaveCity();
        return true;
    }

    private void ApplyVisualForLevel()
    {
        var level = data.GetLevel(currentLevelIndex);
        if (currentVisual != null) Destroy(currentVisual);
        if (level != null && level.visualPrefab != null)
        {
            currentVisual = Instantiate(level.visualPrefab, transform);
            currentVisual.transform.localPosition = Vector3.zero;
        }
    }

    /// Current stat contribution based on upgrade level.
    public (float pop, float jobs, float pollution, float crimeRate, float traffic, float lifestyle) GetStats()
    {
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