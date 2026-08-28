using System.Collections.Generic;
using System.IO;
using UnityEngine;

[System.Serializable]
public class BuildingSaveData
{
    public string buildingId;
    public int gridX;
    public int gridY;
    public int rotationSteps;
    public int currentLevelIndex;
}

[System.Serializable]
public class CitySaveData
{
    public int funds;
    public List<BuildingSaveData> buildings = new List<BuildingSaveData>();
}

/// Saves/loads the placed city (buildings, funds) to a local JSON file. Uses
/// Application.persistentDataPath, which Unity resolves to the correct writable location
/// automatically on Android, iOS, Windows, and Mac - no platform-specific code needed.
///
/// This is LOCAL-ONLY for now. When REST API sync is added later, the same CitySaveData
/// structure can be sent/received over HTTP instead of read/written to disk - SaveCity()/
/// LoadCity() are the only two methods that would need their I/O swapped out; nothing else in
/// the game needs to change.
public class CitySaveManager : MonoBehaviour
{
    public static CitySaveManager Instance { get; private set; }

    private string SavePath => Path.Combine(Application.persistentDataPath, "city_save.json");

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        LoadCity();
    }

    [ContextMenu("Save City Now")]
    public void SaveCity()
    {
        var data = new CitySaveData();
        data.funds = EconomyManager.Instance != null ? EconomyManager.Instance.currentFunds : 0;

        if (BuildingPlacementController.Instance != null)
        {
            foreach (var instance in BuildingPlacementController.Instance.AllPlacedBuildings)
            {
                data.buildings.Add(new BuildingSaveData
                {
                    buildingId = instance.data.buildingId,
                    gridX = instance.gridOrigin.x,
                    gridY = instance.gridOrigin.y,
                    rotationSteps = instance.rotationSteps,
                    currentLevelIndex = instance.currentLevelIndex
                });
            }
        }

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(SavePath, json);
        Debug.Log($"[CitySaveManager] Saved {data.buildings.Count} building(s) to {SavePath}");
    }

    [ContextMenu("Load City Now")]
    public void LoadCity()
    {
        if (!File.Exists(SavePath))
        {
            Debug.Log("[CitySaveManager] No save file found - starting fresh (first-time player).");
            return;
        }

        string json = File.ReadAllText(SavePath);
        CitySaveData data = JsonUtility.FromJson<CitySaveData>(json);
        if (data == null) return;

        if (EconomyManager.Instance != null)
            EconomyManager.Instance.SetFunds(data.funds);

        int loaded = 0;
        foreach (var saved in data.buildings)
        {
            BuildingData buildingData = BuildingDatabase.Instance != null
                ? BuildingDatabase.Instance.GetById(saved.buildingId)
                : null;

            if (buildingData == null)
            {
                Debug.LogWarning($"[CitySaveManager] Couldn't find BuildingData for id '{saved.buildingId}' - skipping. Was it removed/renamed since this save was made?");
                continue;
            }

            Vector2Int cell = new Vector2Int(saved.gridX, saved.gridY);
            BuildingPlacementController.Instance.PlaceBuildingDirect(buildingData, cell, saved.rotationSteps, saved.currentLevelIndex);
            loaded++;
        }

        Debug.Log($"[CitySaveManager] Loaded {loaded}/{data.buildings.Count} building(s) from {SavePath}");
    }

    /// Wipes the save file entirely - use this to test a fresh/first-time city without
    /// reinstalling or clearing the whole device's app data.
    [ContextMenu("Clear Save (Testing)")]
    public void ClearSave()
    {
        if (File.Exists(SavePath))
        {
            File.Delete(SavePath);
            Debug.Log("[CitySaveManager] Save file deleted.");
        }
        else
        {
            Debug.Log("[CitySaveManager] No save file to delete.");
        }
    }
}
