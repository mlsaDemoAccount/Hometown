using UnityEngine;

// The 6 fixed stat categories. Signs and Decoration items get folded into these (Signs ->
// Traffic, Decoration -> Lifestyle) rather than having their own categories.
public enum BuildingCategory
{
    Population,
    Jobs,
    Pollution,
    CrimeRate,
    Traffic,
    Lifestyle
}

[System.Serializable]
public class BuildingUpgradeLevel
{
    public int level = 1;
    public GameObject visualPrefab;     // swapped in when this level is reached
    public int upgradeCost = 100;       // cost to reach the NEXT level from this one
    [Tooltip("Seconds this level's upgrade takes to complete once started (upgrading FROM this level TO the next one). 0 = instant.")]
    public float upgradeDuration = 60f;
    [Tooltip("XP awarded when this level's upgrade completes (upgrading FROM this level TO the next one).")]
    public int upgradeXP = 15;
    public float populationOutput;
    public float jobsOutput;
    public float pollutionOutput;
    public float crimeRateOutput;
    public float trafficOutput;
    public float lifestyleOutput;
}

[CreateAssetMenu(fileName = "NewBuilding", menuName = "CityBuilder/Building Data")]
public class BuildingData : ScriptableObject
{
    [Header("Identity")]
    public string buildingId;
    public string displayName;
    public Sprite icon;
    public BuildingCategory primaryCategory;

    [Header("Placement")]
    public Vector2Int footprintSize = new Vector2Int(1, 1); // e.g. 2x2 for bigger buildings
    public int placementCost = 100;
    [Tooltip("Real buildings usually need this ON. Standalone decoration/signs can turn it OFF.")]
    public bool requiresFootpathAdjacency = true;

    [Header("Construction")]
    [Tooltip("Seconds a freshly placed building takes to finish construction. 0 = instant (no construction sequence) - safe default for buildings that don't have construction-stage visuals set up yet.")]
    public float constructionDuration = 0f;
    [Tooltip("XP awarded when this building finishes construction (placement).")]
    public int placementXP = 10;

    [Header("Placeholder Art")]
    // If true, stretches the visual prefab's X/Z scale to exactly fill footprintSize * cellSize -
    // useful while you're using default Unity primitives (Cube, etc.) as placeholder buildings.
    // Turn this OFF once you swap in a real model that's already correctly sized/modeled.
    public bool autoScaleToFootprint = true;

    [Header("Upgrades (index 0 = level 1)")]
    public BuildingUpgradeLevel[] levels;

    public BuildingUpgradeLevel GetLevel(int levelIndex)
    {
        if (levels == null || levels.Length == 0) return null;
        levelIndex = Mathf.Clamp(levelIndex, 0, levels.Length - 1);
        return levels[levelIndex];
    }

    public int MaxLevelIndex => (levels != null && levels.Length > 0) ? levels.Length - 1 : 0;
}