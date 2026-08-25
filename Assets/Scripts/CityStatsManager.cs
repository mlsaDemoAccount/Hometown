using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public enum CityMetricStatus { Healthy, Warning, Critical }

[System.Serializable]
public class CityMetricConfig
{
    public BuildingCategory category;
    public float startingPoints;
    public float warningThreshold;
    public float criticalThreshold;
    [Tooltip("true = going HIGHER is bad (Pollution, Population, CrimeRate, Traffic). false = going LOWER is bad (Jobs, Lifestyle).")]
    public bool highIsBad;
}

/// Tracks each city category as a 0-1000 point metric - starting points plus every placed
/// building's cumulative per-category delta (from BuildingUpgradeLevel's output fields),
/// clamped to the 0-1000 range, with warning/critical thresholds and HIGH-is-bad vs LOW-is-bad
/// direction per category.
///
/// Uses the 6 CURRENTLY IMPLEMENTED categories (Garbage was merged into Pollution earlier) -
/// deliberately NOT the 7-category version shown in early reference docs. Any "Garbage" delta
/// from a building should go into that building's pollutionOutput field.
public class CityStatsManager : MonoBehaviour
{
    public static CityStatsManager Instance { get; private set; }

    [Header("Metric Configuration (0-1000 scale per category)")]
    public List<CityMetricConfig> metricConfigs = new List<CityMetricConfig>
    {
        new CityMetricConfig { category = BuildingCategory.Population, startingPoints = 380, warningThreshold = 800, criticalThreshold = 950, highIsBad = true },
        new CityMetricConfig { category = BuildingCategory.Pollution,  startingPoints = 330, warningThreshold = 500, criticalThreshold = 1000, highIsBad = true },
        new CityMetricConfig { category = BuildingCategory.CrimeRate,  startingPoints = 280, warningThreshold = 400, criticalThreshold = 700, highIsBad = true },
        new CityMetricConfig { category = BuildingCategory.Traffic,    startingPoints = 350, warningThreshold = 600, criticalThreshold = 850, highIsBad = true },
        new CityMetricConfig { category = BuildingCategory.Jobs,       startingPoints = 720, warningThreshold = 600, criticalThreshold = 300, highIsBad = false },
        new CityMetricConfig { category = BuildingCategory.Lifestyle,  startingPoints = 650, warningThreshold = 500, criticalThreshold = 250, highIsBad = false },
    };

    private Dictionary<BuildingCategory, float> currentPoints = new Dictionary<BuildingCategory, float>();
    private List<BuildingInstance> allBuildings = new List<BuildingInstance>();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        // Initialize immediately, before any building is placed - otherwise currentPoints stays
        // empty (and GetPoints() returns 0 for everything) until the first RegisterBuilding call.
        RecalculateAll();
    }

    public void RegisterBuilding(BuildingInstance b)
    {
        allBuildings.Add(b);
        RecalculateAll();
    }

    public void UnregisterBuilding(BuildingInstance b)
    {
        allBuildings.Remove(b);
        RecalculateAll();
    }

    public void RecalculateAll()
    {
        // Every category starts at its configured starting points.
        currentPoints.Clear();
        foreach (var config in metricConfigs)
            currentPoints[config.category] = config.startingPoints;

        // Add every placed building's per-category delta, from its CURRENT upgrade level -
        // this is the "cascade rules" table: placing/upgrading a building shifts multiple
        // categories at once (e.g. a house adds Population, Pollution, and Traffic together).
        foreach (var b in allBuildings)
        {
            var (pop, jobs, pollution, crimeRate, traffic, lifestyle) = b.GetStats();
            AddDelta(BuildingCategory.Population, pop);
            AddDelta(BuildingCategory.Jobs, jobs);
            AddDelta(BuildingCategory.Pollution, pollution);
            AddDelta(BuildingCategory.CrimeRate, crimeRate);
            AddDelta(BuildingCategory.Traffic, traffic);
            AddDelta(BuildingCategory.Lifestyle, lifestyle);
        }

        // Clamp every category to the 0-1000 scale.
        var categories = new List<BuildingCategory>(currentPoints.Keys);
        foreach (var cat in categories)
            currentPoints[cat] = Mathf.Clamp(currentPoints[cat], 0f, 1000f);

        CityStatsPanel.Instance?.RefreshDisplay(); // direct call, no Action/event
    }

    private void AddDelta(BuildingCategory category, float delta)
    {
        if (currentPoints.ContainsKey(category)) currentPoints[category] += delta;
    }

    public float GetPoints(BuildingCategory category) =>
        currentPoints.TryGetValue(category, out var v) ? v : 0f;

    public float GetNormalized01(BuildingCategory category) => GetPoints(category) / 1000f;

    private CityMetricConfig GetConfig(BuildingCategory category) =>
        metricConfigs.Find(c => c.category == category);

    public CityMetricStatus GetStatus(BuildingCategory category)
    {
        var config = GetConfig(category);
        if (config == null) return CityMetricStatus.Healthy;
        float points = GetPoints(category);

        if (config.highIsBad)
        {
            if (points >= config.criticalThreshold) return CityMetricStatus.Critical;
            if (points >= config.warningThreshold) return CityMetricStatus.Warning;
        }
        else
        {
            if (points <= config.criticalThreshold) return CityMetricStatus.Critical;
            if (points <= config.warningThreshold) return CityMetricStatus.Warning;
        }
        return CityMetricStatus.Healthy;
    }

    public List<BuildingInstance> GetBuildingsByCategory(BuildingCategory category) =>
        allBuildings.Where(b => b.data.primaryCategory == category).ToList();
}