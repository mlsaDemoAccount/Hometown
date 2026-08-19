using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

/// Shop panel with category tabs (auto-generated from the BuildingCategory enum, plus an "All"
/// tab) and a filtered item list below. Tap a tab to filter, tap an item to start placement.
public class ShopUIController : MonoBehaviour
{
    public static ShopUIController Instance { get; private set; }

    [Header("Data")]
    public BuildingData[] allBuildings;

    [Header("Category Tabs")]
    public Transform tabParent;           // horizontal layout group, parent for tab buttons
    public GameObject tabButtonPrefab;    // prefab with CategoryTabButton component
    public bool includeAllTab = true;     // shows an "All" tab alongside the category ones

    [Header("Item List")]
    public Transform itemListParent;      // Content of a ScrollView
    public GameObject shopItemButtonPrefab; // prefab with ShopItemButton component
    public GameObject shopPanel;          // whole panel, toggled open/closed
    public Text fundsLabel;

    private BuildingCategory? activeFilter = null;
    private List<CategoryTabButton> tabButtons = new List<CategoryTabButton>();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.OnFundsChanged += UpdateFundsLabel;
            UpdateFundsLabel(EconomyManager.Instance.currentFunds);
        }

        GenerateTabs();
        PopulateList();
    }

    private void GenerateTabs()
    {
        if (tabParent == null || tabButtonPrefab == null) return; // tabs optional - skip if not set up

        foreach (Transform child in tabParent) Destroy(child.gameObject);
        tabButtons.Clear();

        if (includeAllTab)
        {
            var allTab = CreateTab("All", null);
            SetActiveTab(allTab); // starts selected, matches activeFilter starting as null
        }

        foreach (BuildingCategory category in System.Enum.GetValues(typeof(BuildingCategory)))
            CreateTab(category.ToString(), category);
    }

    private CategoryTabButton CreateTab(string label, BuildingCategory? category)
    {
        var go = Instantiate(tabButtonPrefab, tabParent);
        var tab = go.GetComponent<CategoryTabButton>();
        tab.Setup(label, category);
        tabButtons.Add(tab);
        return tab;
    }

    /// Called directly by a CategoryTabButton when tapped - no delegate/event needed, it just
    /// calls straight into this singleton.
    public void SelectCategory(BuildingCategory? category, CategoryTabButton clickedTab)
    {
        activeFilter = category;
        SetActiveTab(clickedTab);
        PopulateList();
    }

    private void SetActiveTab(CategoryTabButton activeTab)
    {
        foreach (var tab in tabButtons) tab.SetSelected(tab == activeTab);
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
            item.Setup(building);
        }
    }

    /// Called directly by a ShopItemButton when tapped.
    public void SelectBuilding(BuildingData data)
    {
        if (shopPanel != null) shopPanel.SetActive(false); // close shop before placement starts
        BuildingPlacementController.Instance.BeginPlacement(data);
    }

    private void UpdateFundsLabel(int funds)
    {
        if (fundsLabel != null) fundsLabel.text = $"${funds}";
    }
}