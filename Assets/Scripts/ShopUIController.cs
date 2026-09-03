using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

/// Shop panel with category tabs (auto-generated from the BuildingCategory enum, plus an "All"
/// tab) and a filtered item list below. Tap a tab to filter, tap an item to start placement.
public class ShopUIController : MonoBehaviour
{
    public static ShopUIController Instance { get; private set; }

    // NOTE: building list now comes from BuildingDatabase.Instance.allBuildings - no separate
    // list here anymore, so there's only one place to add a new building asset.

    [Header("Category Tabs")]
    public Transform tabParent;           // horizontal layout group, parent for tab buttons
    public GameObject tabButtonPrefab;    // prefab with CategoryTabButton component
    public bool includeAllTab = true;     // shows an "All" tab alongside the category ones

    [System.Serializable]
    public class CategoryIconSet
    {
        public BuildingCategory category;
        public Sprite selectedIcon;
        public Sprite unselectedIcon;
    }

    [Header("Category Tab Icons")]
    public Sprite allTabSelectedIcon;
    public Sprite allTabUnselectedIcon;
    public List<CategoryIconSet> categoryIcons = new List<CategoryIconSet>();

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

        CategoryTabButton defaultTab = null;
        BuildingCategory? defaultFilter = null;

        if (includeAllTab)
        {
            defaultTab = CreateTab("All", null);
            defaultFilter = null;
        }

        foreach (BuildingCategory category in System.Enum.GetValues(typeof(BuildingCategory)))
        {
            var tab = CreateTab(category.ToString(), category);
            if (defaultTab == null) { defaultTab = tab; defaultFilter = category; } // no All tab - first category becomes the default instead
        }

        if (defaultTab != null)
        {
            activeFilter = defaultFilter;
            SetActiveTab(defaultTab);
        }

        if (tabParent is RectTransform tabParentRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(tabParentRect);
    }

    private CategoryTabButton CreateTab(string label, BuildingCategory? category)
    {
        var go = Instantiate(tabButtonPrefab, tabParent);
        var tab = go.GetComponent<CategoryTabButton>();

        Sprite selectedIcon, unselectedIcon;
        if (category == null)
        {
            selectedIcon = allTabSelectedIcon;
            unselectedIcon = allTabUnselectedIcon;
        }
        else
        {
            var iconSet = categoryIcons.Find(c => c.category == category.Value);
            selectedIcon = iconSet != null ? iconSet.selectedIcon : null;
            unselectedIcon = iconSet != null ? iconSet.unselectedIcon : null;
        }

        tab.Setup(label, category, selectedIcon, unselectedIcon);
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

    /// Called by something OTHER than a tab click (e.g. CityNotificationManager's Accept
    /// button) to switch to a specific category, same effect as the player tapping that tab.
    public void SelectCategoryExternally(BuildingCategory category)
    {
        var tab = tabButtons.Find(t => t.Category == category);
        if (tab != null)
            SelectCategory(category, tab);
        else
        {
            // Tabs not generated yet for some reason - still apply the filter so the list is correct.
            activeFilter = category;
            PopulateList();
        }
    }

    private void SetActiveTab(CategoryTabButton activeTab)
    {
        foreach (var tab in tabButtons) tab.SetSelected(tab == activeTab);
    }

    private void PopulateList()
    {
        foreach (Transform child in itemListParent) Destroy(child.gameObject);

        if (BuildingDatabase.Instance == null)
        {
            Debug.LogWarning("[ShopUIController] BuildingDatabase not found in scene - shop can't populate.");
            return;
        }

        var allBuildings = BuildingDatabase.Instance.allBuildings;
        var toShow = activeFilter == null
            ? allBuildings
            : allBuildings.Where(b => b.primaryCategory == activeFilter.Value).ToArray();

        foreach (var building in toShow)
        {
            var go = Instantiate(shopItemButtonPrefab, itemListParent);
            var item = go.GetComponent<ShopItemButton>();
            item.Setup(building);
        }

        // Content Size Fitter doesn't recalculate automatically for runtime-instantiated
        // children until Unity's next deferred layout pass - forcing it immediately here is
        // what makes newly-spawned items actually show up this frame instead of sitting
        // invisible inside a zero-sized Content rect (clipped by the ScrollView's Viewport mask).
        if (itemListParent is RectTransform itemListRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(itemListRect);
    }

    /// Called directly by a ShopItemButton when tapped.
    public void SelectBuilding(BuildingData data)
    {
        if (UiManager.Instance != null)
        {
            // Route through UiManager so the Gameplay panel (hidden when Shop opened) gets
            // restored - directly toggling shopPanel here left UiManager's state out of sync,
            // so Gameplay never came back after Cancel/Done.
            UiManager.Instance.ShowHideShopPanel(false);
            UiManager.Instance.ShowHideGameplayPanel(true);
        }
        else if (shopPanel != null)
        {
            shopPanel.SetActive(false); // fallback if UiManager isn't in the scene
        }

        BuildingPlacementController.Instance.BeginPlacement(data);
    }

    /// Called by a back/cross button on the shop panel to close it without selecting anything.
    public void CloseShop()
    {
        if (UiManager.Instance != null)
        {
            UiManager.Instance.ShowHideShopPanel(false);
            UiManager.Instance.ShowHideGameplayPanel(true);
        }
        else if (shopPanel != null)
        {
            shopPanel.SetActive(false); // fallback if UiManager isn't in the scene
        }
    }

    private void UpdateFundsLabel(int funds)
    {
        if (fundsLabel != null) fundsLabel.text = $"{funds} WP";
    }
}