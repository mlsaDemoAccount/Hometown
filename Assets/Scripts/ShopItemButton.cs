using UnityEngine;
using UnityEngine.UI;

public class ShopItemButton : MonoBehaviour
{
    public Image icon;
    public Text nameLabel;
    public Text costLabel;
    public Button button;
 
    public void Setup(BuildingData data, System.Action<BuildingData> onClick)
    {
        if (icon != null) icon.sprite = data.icon;
        if (nameLabel != null) nameLabel.text = data.displayName;
        if (costLabel != null) costLabel.text = $"${data.placementCost}";
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick(data));
    }
}
 