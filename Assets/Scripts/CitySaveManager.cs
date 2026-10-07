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

    // Construction/upgrade timer state - so a building mid-construction when the game closed
    // resumes from where it was on load, instead of resetting. JsonUtility serializes enums
    // fine directly (as their underlying int), no manual conversion needed.
    public ConstructionPhase constructionPhase = ConstructionPhase.Complete;
    public float constructionRemaining;
    public float constructionTotal;
    public int pendingUpgradeLevelIndex;
}

[System.Serializable]
public class CitySaveData
{
    // Embedded IN the data itself, not just implied by the save file's name - matters once
    // this moves to a REST API payload later, which needs the ID as real data, not a filename.
    public string cityId;
    public int funds;
    public int xp;
    public long savedAtUnixSeconds; // real-world timestamp this save was written - used to compute offline progress on load
    public List<BuildingSaveData> buildings = new List<BuildingSaveData>();
}

/// Saves/loads ONE city's data (buildings, funds, XP) to a local JSON file, keyed by cityId -
/// this game supports multiple cities per player, so every save/load is scoped to whichever
/// city is currently active (currentCityId), not a single global save. Uses
/// Application.persistentDataPath, which Unity resolves to the correct writable location
/// automatically on Android, iOS, Windows, and Mac - no platform-specific code needed.
///
/// This is LOCAL-ONLY for now. When REST API sync is added later, the same CitySaveData
/// structure (cityId included) can be sent/received over HTTP instead of read/written to disk -
/// SaveCity()/LoadCity() are the only two methods that would need their I/O swapped out;
/// nothing else in the game needs to change.
public class CitySaveManager : MonoBehaviour
{
    public static CitySaveManager Instance { get; private set; }

    [Tooltip("Which city's save file to use. Later, a city-select screen will set this before entering Gameplay - for now, defaults to a single city so everything works exactly as before.")]
    public string currentCityId = "city_1";

    private string SavePath => Path.Combine(Application.persistentDataPath, $"city_{currentCityId}_save.json");

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        LoadCity();
        StartCoroutine(SyncProfileFromServer());
    }

    /// Server profile is the source of truth for funds and XP when logged in: fetched after the
    /// local load so it wins. Offline / not logged in / pending local changes -> local values stay.
    private System.Collections.IEnumerator SyncProfileFromServer()
    {
        if (AuthManager.Instance == null || !AuthManager.Instance.IsLoggedIn) yield break;

        var handle = AuthManager.Instance.RefreshProfile();
        while (!handle.isDone) yield return null;
        if (!handle.success || UserDetails.Instance == null) yield break;

        if (EconomyManager.Instance != null)
            EconomyManager.Instance.SetFunds(UserDetails.Instance.FundsInt);
        if (PlayerProgressManager.Instance != null)
            PlayerProgressManager.Instance.RestoreXP(UserDetails.Instance.playerXp);
    }

    /// Fires when the app is backgrounded (Android/iOS) or loses focus - this is the reliable
    /// moment to persist in-progress construction/upgrade timers at their ACTUAL current state,
    /// rather than only saving at placement/demolish/completion and leaving mid-countdown
    /// progress stale if the app closes while a timer is still running.
    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus) SaveCity();
    }

    void OnApplicationQuit()
    {
        SaveCity();
    }

    [ContextMenu("Save City Now")]
    public void SaveCity()
    {
        var data = new CitySaveData();
        data.cityId = currentCityId;
        data.funds = EconomyManager.Instance != null ? EconomyManager.Instance.currentFunds : 0;
        data.xp = PlayerProgressManager.Instance != null ? PlayerProgressManager.Instance.currentXP : 0;
        data.savedAtUnixSeconds = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        if (BuildingPlacementController.Instance != null)
        {
            foreach (var instance in BuildingPlacementController.Instance.AllPlacedBuildings)
            {
                var save = new BuildingSaveData
                {
                    buildingId = instance.data.buildingId,
                    gridX = instance.gridOrigin.x,
                    gridY = instance.gridOrigin.y,
                    rotationSteps = instance.rotationSteps,
                    currentLevelIndex = instance.currentLevelIndex
                };

                var cs = instance.ConstructionState;
                if (cs != null)
                {
                    save.constructionPhase = cs.phase;
                    save.constructionRemaining = cs.remainingTime;
                    save.constructionTotal = cs.totalDuration;
                    save.pendingUpgradeLevelIndex = cs.PendingUpgradeLevelIndex;
                }

                data.buildings.Add(save);
            }
        }

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(SavePath, json);
        Debug.Log($"[CitySaveManager] Saved city '{currentCityId}' - {data.buildings.Count} building(s) to {SavePath}");
    }

    [ContextMenu("Load City Now")]
    public void LoadCity()
    {
        if (!File.Exists(SavePath))
        {
            Debug.Log($"[CitySaveManager] No save file found for city '{currentCityId}' - starting fresh.");
            return;
        }

        string json = File.ReadAllText(SavePath);
        CitySaveData data = JsonUtility.FromJson<CitySaveData>(json);
        if (data == null) return;

        if (EconomyManager.Instance != null)
            EconomyManager.Instance.SetFunds(data.funds);

        if (PlayerProgressManager.Instance != null)
            PlayerProgressManager.Instance.RestoreXP(data.xp);

        // How much real-world time passed while the app was closed - subtracted from every
        // in-progress construction/upgrade timer below, so buildings genuinely keep building
        // while offline instead of freezing at whatever they were at on close.
        long nowUnix = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long elapsedSeconds = System.Math.Max(0L, nowUnix - data.savedAtUnixSeconds);
        if (elapsedSeconds > 0) Debug.Log($"[CitySaveManager] {elapsedSeconds}s passed since last save - applying offline construction progress.");

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

            float adjustedRemaining = saved.constructionRemaining;
            if (saved.constructionPhase != ConstructionPhase.Complete)
            {
                // Clamped to 0 (not negative) - if offline time exceeds what was left, the
                // building's own Update() will detect remainingTime <= 0 on the very next frame
                // and complete it automatically, same code path as finishing normally in-session.
                adjustedRemaining = Mathf.Max(0f, saved.constructionRemaining - elapsedSeconds);
            }

            Vector2Int cell = new Vector2Int(saved.gridX, saved.gridY);
            BuildingPlacementController.Instance.PlaceBuildingDirect(
                buildingData, cell, saved.rotationSteps, saved.currentLevelIndex,
                saved.constructionPhase, adjustedRemaining, saved.constructionTotal, saved.pendingUpgradeLevelIndex);
            loaded++;
        }

        Debug.Log($"[CitySaveManager] Loaded city '{currentCityId}' - {loaded}/{data.buildings.Count} building(s) from {SavePath}");
    }

    /// Wipes the CURRENT city's save file - use this to test a fresh/first-time city without
    /// reinstalling or clearing the whole device's app data. Other cities' saves are untouched.
    [ContextMenu("Clear Save (Testing)")]
    public void ClearSave()
    {
        if (File.Exists(SavePath))
        {
            File.Delete(SavePath);
            Debug.Log($"[CitySaveManager] Save file for city '{currentCityId}' deleted.");
        }
        else
        {
            Debug.Log($"[CitySaveManager] No save file to delete for city '{currentCityId}'.");
        }
    }
}