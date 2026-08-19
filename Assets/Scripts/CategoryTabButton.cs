using UnityEngine;
using UnityEngine.UI;

/// Attach to the category tab button prefab - a Button with a Text label, and optionally an
/// Image whose color changes for the selected/unselected state.
public class CategoryTabButton : MonoBehaviour
{
    public Button button;
    public Text label;
    public Image background; // optional - leave unassigned if you'd rather style selection some other way

    public Color selectedColor = new Color(0.25f, 0.55f, 1f);
    public Color unselectedColor = new Color(0.85f, 0.85f, 0.85f);

    private BuildingCategory? category;

    public void Setup(string labelText, BuildingCategory? forCategory)
    {
        category = forCategory;
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
    }
}
