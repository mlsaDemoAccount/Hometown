using UnityEngine;
using System.Linq;

/// Central registry of every BuildingData asset in the game, looked up by its buildingId
/// string. Used by CitySaveManager to resolve a saved building back to its ScriptableObject
/// asset (JSON can't store a direct reference to a ScriptableObject, only its id).
///
/// Assign the SAME list of BuildingData assets here as you did on ShopUIController's
/// allBuildings - they serve different purposes (this is a lookup table for save/load, that's
/// what populates the shop UI), but should contain the same assets.
public class BuildingDatabase : MonoBehaviour
{
    public static BuildingDatabase Instance { get; private set; }

    public BuildingData[] allBuildings;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public BuildingData GetById(string buildingId)
    {
        return allBuildings.FirstOrDefault(b => b.buildingId == buildingId);
    }
}
