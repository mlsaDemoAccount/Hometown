using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// Aggregates Population / Jobs / Pollution / CrimeRate / Traffic / Lifestyle across every placed building.
public class CityStatsManager : MonoBehaviour
{
    public static CityStatsManager Instance { get; private set; }

    public float TotalPopulation { get; private set; }
    public float TotalJobs { get; private set; }
    public float TotalPollution { get; private set; }
    public float TotalCrimeRate { get; private set; }
    public float TotalTraffic { get; private set; }
    public float TotalLifestyle { get; private set; }

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
        TotalPopulation = TotalJobs = TotalPollution = TotalCrimeRate = TotalTraffic = TotalLifestyle = 0f;

        foreach (var b in allBuildings)
        {
            var (pop, jobs, pollution, crimeRate, traffic, lifestyle) = b.GetStats();
            TotalPopulation += pop;
            TotalJobs += jobs;
            TotalPollution += pollution;
            TotalCrimeRate += crimeRate;
            TotalTraffic += traffic;
            TotalLifestyle += lifestyle;
        }

        OnStatsChanged?.Invoke();
    }

    public List<BuildingInstance> GetBuildingsByCategory(BuildingCategory category) =>
        allBuildings.Where(b => b.data.primaryCategory == category).ToList();
}