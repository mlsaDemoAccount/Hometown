using UnityEngine;

// The 5 stat categories every building contributes to (positively or negatively).
public enum BuildingCategory
{
    Population,
    Jobs,
    Pollution,
    Happiness,
    Garbage
}

[System.Serializable]
public class BuildingUpgradeLevel
{
    public int level = 1;
    public GameObject visualPrefab;     // swapped in when this level is reached
    public int upgradeCost = 100;       // cost to reach the NEXT level from this one
    public float populationOutput;
    public float jobsOutput;
    public float pollutionOutput;
    public float happinessOutput;
    public float garbageOutput;
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
    public bool requiresRoadAdjacency = true; // most buildings need to touch a road

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
