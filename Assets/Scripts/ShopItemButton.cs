using UnityEngine;
using UnityEngine.UI;

/// Attach to the shop item button prefab.
public class ShopItemButton : MonoBehaviour
{
    public Image icon;
    public Text nameLabel;
    public Text costLabel;
    public Button button;

    private BuildingData data;

    public void Setup(BuildingData buildingData)
    {
        data = buildingData;
        if (icon != null) icon.sprite = data.icon;
        if (nameLabel != null) nameLabel.text = data.displayName;
        if (costLabel != null) costLabel.text = $"${data.placementCost}";
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnClicked);
    }

    private void OnClicked()
    {
        ShopUIController.Instance.SelectBuilding(data);
    }
}