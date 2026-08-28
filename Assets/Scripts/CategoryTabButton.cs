using UnityEngine;
using UnityEngine.UI;

/// Attach to the category tab button prefab - a Button with a Text label, an optional
/// background Image whose color changes for selected/unselected state, and an optional icon
/// Image whose SPRITE (not just color) swaps between a dedicated selected and unselected image.
public class CategoryTabButton : MonoBehaviour
{
    public Button button;
    public Text label;
    public Image background; // optional - leave unassigned if you'd rather style selection some other way
    public Image icon;       // optional - leave unassigned if this tab has no icon

    public Color selectedColor = new Color(0.25f, 0.55f, 1f);
    public Color unselectedColor = new Color(0.85f, 0.85f, 0.85f);
    public Color selectedTextColor = Color.white;
    public Color unselectedTextColor = new Color(0.2f, 0.2f, 0.2f);

    private BuildingCategory? category;
    public BuildingCategory? Category => category;

    private Sprite selectedIconSprite;
    private Sprite unselectedIconSprite;

    public void Setup(string labelText, BuildingCategory? forCategory, Sprite selectedIcon, Sprite unselectedIcon)
    {
        category = forCategory;
        selectedIconSprite = selectedIcon;
        unselectedIconSprite = unselectedIcon;

        if (label != null) label.text = labelText;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnClicked);
        SetSelected(false);
    }

    private void OnClicked()
    {
        ShopUIController.Instance.SelectCategory(category, this);
    }

    public void SetSelected(bool selected)
    {
        if (background != null) background.color = selected ? selectedColor : unselectedColor;
        if (label != null) label.color = selected ? selectedTextColor : unselectedTextColor;

        if (icon != null)
        {
            var sprite = selected ? selectedIconSprite : unselectedIconSprite;
            if (sprite != null) icon.sprite = sprite;
        }
    }
}