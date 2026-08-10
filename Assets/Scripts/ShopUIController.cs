
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
 
/// Simple shop panel: filter buildings by category, tap an item to start placement.
/// Hook buttons up in the Unity Inspector or generate them at runtime from `allBuildings`.
public class ShopUIController : MonoBehaviour
{
    [Header("Data")]
    public BuildingData[] allBuildings;
 
    [Header("UI References")]
    public Transform itemListParent;      // Content of a ScrollView
    public GameObject shopItemButtonPrefab; // prefab with ShopItemButton component
    public GameObject shopPanel;          // whole panel, toggled open/closed
    public Text fundsLabel;
 
    private BuildingCategory? activeFilter = null;
 
    void Start()
    {
        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.OnFundsChanged += UpdateFundsLabel;
            UpdateFundsLabel(EconomyManager.Instance.currentFunds);
        }
        PopulateList();
    }
 
    public void FilterByCategory(int categoryIndex)
    {
        activeFilter = (BuildingCategory)categoryIndex;
        PopulateList();
    }
 
    public void ClearFilter()
    {
        activeFilter = null;
        PopulateList();
    }
 
    private void PopulateList()
    {
        foreach (Transform child in itemListParent) Destroy(child.gameObject);
 
        var toShow = activeFilter == null
            ? allBuildings
            : allBuildings.Where(b => b.primaryCategory == activeFilter.Value).ToArray();
 
        foreach (var building in toShow)
        {
            var go = Instantiate(shopItemButtonPrefab, itemListParent);
            var item = go.GetComponent<ShopItemButton>();
            item.Setup(building, OnBuildingSelected);
        }
    }
 
    private void OnBuildingSelected(BuildingData data)
    {
        if (shopPanel != null) shopPanel.SetActive(false); // close shop before placement starts
        BuildingPlacementController.Instance.BeginPlacement(data);
    }
 
    private void UpdateFundsLabel(int funds)
    {
        if (fundsLabel != null) fundsLabel.text = $"${funds}";
    }
}