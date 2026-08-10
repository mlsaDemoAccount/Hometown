using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// Aggregates Population / Jobs / Pollution / Happiness / Garbage across every placed building.
public class CityStatsManager : MonoBehaviour
{
    public static CityStatsManager Instance { get; private set; }

    public float TotalPopulation { get; private set; }
    public float TotalJobs { get; private set; }
    public float TotalPollution { get; private set; }
    public float TotalHappiness { get; private set; }
    public float TotalGarbage { get; private set; }

    private List<BuildingInstance> allBuildings = new List<BuildingInstance>();

    public System.Action OnStatsChanged;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
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
        TotalPopulation = TotalJobs = TotalPollution = TotalHappiness = TotalGarbage = 0f;

        foreach (var b in allBuildings)
        {
            var (pop, jobs, pollution, happiness, garbage) = b.GetStats();
            TotalPopulation += pop;
            TotalJobs += jobs;
            TotalPollution += pollution;
            TotalHappiness += happiness;
            TotalGarbage += garbage;
        }

        OnStatsChanged?.Invoke();
    }

    public List<BuildingInstance> GetBuildingsByCategory(BuildingCategory category) =>
        allBuildings.Where(b => b.data.primaryCategory == category).ToList();
}
