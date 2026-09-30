using UnityEngine;
using UnityEngine.UI;

/// Attach to the shop item button prefab - shows one building's icon/name/cost, and starts
/// placement when tapped. Disables itself if the player can't currently afford it, and stays
/// live-updated while visible (e.g. if funds change while the shop is open) rather than only
/// checking once when the list was populated.
public class ShopItemButton : MonoBehaviour
{
    public Image icon;
    public Text nameLabel;
    public Text costLabel;
    public Button button;

    [Tooltip("Cost label color when the item can't currently be afforded. Leave default white/whatever it already is for the affordable case.")]
    public Color unaffordableCostColor = Color.red;

    private BuildingData data;
    public BuildingData Data => data; // read-only - lets GameplayTutorialManager find a specific building's button
    private Color affordableCostColor;

    public void Setup(BuildingData buildingData)
    {
        data = buildingData;
        if (icon != null) icon.sprite = data.icon;
        if (nameLabel != null) nameLabel.text = data.displayName;
        if (costLabel != null)
        {
            costLabel.text = $"{data.placementCost} WP";
            affordableCostColor = costLabel.color; // remember whatever color was authored, before we start overriding it
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnClicked);

        UpdateAffordability();
    }

    void Update()
    {
        // Polling instead of subscribing to a funds-changed event - keeps affordability
        // current if funds change while this button is visible, without any event/delegate.
        UpdateAffordability();
    }

    private void UpdateAffordability()
    {
        bool canAfford = EconomyManager.Instance == null || EconomyManager.Instance.currentFunds >= data.placementCost;
        button.interactable = canAfford;
        if (costLabel != null) costLabel.color = canAfford ? affordableCostColor : unaffordableCostColor;
    }

    private void OnClicked()
    {
        ShopUIController.Instance.SelectBuilding(data);
    }
}