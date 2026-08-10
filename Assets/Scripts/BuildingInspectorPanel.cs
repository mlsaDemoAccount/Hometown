using UnityEngine;
using UnityEngine.UI;

/// The panel that pops up with Move / Demolish / Upgrade buttons when a building is selected.
/// Assign this component to BuildingSelectionController.inspectorPanel in the Inspector.
/// Wire the 3 buttons' OnClick in the Inspector to this script's public methods,
/// OR leave them unassigned and let Start() wire them automatically (see below).
public class BuildingInspectorPanel : MonoBehaviour
{
    [Header("Root")]
    public GameObject panelRoot; // the actual panel GameObject to show/hide (can be this.gameObject)

    [Header("Buttons")]
    public Button moveButton;
    public Button demolishButton;
    public Button upgradeButton;

    [Header("Optional labels")]
    public Text buildingNameLabel;
    public Text upgradeCostLabel;

    private BuildingInstance current;

    void Awake()
    {
        if (panelRoot == null) panelRoot = gameObject;

        moveButton.onClick.AddListener(OnMoveClicked);
        demolishButton.onClick.AddListener(OnDemolishClicked);
        upgradeButton.onClick.AddListener(OnUpgradeClicked);

        // IMPORTANT: do NOT call Hide() here - see note in PlacementControlPanel.Awake().
        // Just make sure panelRoot is unchecked (inactive) in the Hierarchy by default.
    }

    public void ShowFor(BuildingInstance instance)
    {
        current = instance;
        panelRoot.SetActive(true);

        if (buildingNameLabel != null) buildingNameLabel.text = instance.data.displayName;

        bool canUpgrade = instance.CanUpgrade;
        upgradeButton.interactable = canUpgrade;
        if (upgradeCostLabel != null)
            upgradeCostLabel.text = canUpgrade ? $"Upgrade - ${instance.NextUpgradeCost}" : "Max Level";
    }

    public void Hide()
    {
        current = null;
        panelRoot.SetActive(false);
    }

    private void OnMoveClicked()
    {
        if (current == null) return;
        BuildingPlacementController.Instance.BeginMove(current);
        BuildingSelectionController.Instance.Deselect(); // clears bob/highlight before the move happens
        Hide();
    }

    private void OnDemolishClicked()
    {
        if (current == null) return;
        BuildingPlacementController.Instance.DemolishInstance(current);
        Hide(); // DemolishInstance also calls Deselect(), this is just belt-and-suspenders
    }

    private void OnUpgradeClicked()
    {
        if (current == null) return;
        bool success = current.TryUpgrade(EconomyManager.Instance);
        if (success)
        {
            ShowFor(current); // refresh labels (new level, new upgrade cost / maxed out)
        }
        // if success == false, not enough funds - could flash the cost label red here, etc.
    }
}