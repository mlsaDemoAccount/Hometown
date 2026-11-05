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

        Refresh();
    }

    // The other player may pick the building up while this panel is open.
    void Update()
    {
        if (current != null) Refresh();
    }

    private void Refresh()
    {
        bool busy = Coop.EditLocked || BuildingPlacementController.Instance.IsLockedByOther(current.instanceId);
        moveButton.interactable = !busy;
        demolishButton.interactable = !busy;

        bool canUpgrade = current.CanUpgrade && !busy;
        upgradeButton.interactable = canUpgrade;
        if (upgradeCostLabel != null)
            upgradeCostLabel.text = current.CanUpgrade ? $"Upgrade - {current.NextUpgradeCost} WP" : "Max Level";
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
        if (current == null || Coop.EditLocked) return;
        if (Coop.IsGuest)
        {
            // Co-op guest: only ASK. The host takes the funds and starts the upgrade on both phones.
            if (!current.CanUpgrade || EconomyManager.Instance.currentFunds < current.NextUpgradeCost) return;
            CityNetworkSync.Instance.RequestUpgrade(current.instanceId);
            BuildingSelectionController.Instance.Deselect();
            Hide();
            return;
        }
        bool success = current.TryUpgrade(EconomyManager.Instance);
        if (success)
        {
            // Clear selection and close the panel while the upgrade timer runs - the player
            // will re-select the building afterward if they want to see its new state, same
            // as Move/Demolish already do rather than leaving the panel open and stale.
            BuildingSelectionController.Instance.Deselect();
            Hide();
        }
        // if success == false, not enough funds - could flash the cost label red here, etc.
    }
}