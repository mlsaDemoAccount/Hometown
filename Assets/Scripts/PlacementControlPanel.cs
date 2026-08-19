using UnityEngine;
using UnityEngine.UI;

/// Shown while placing a new building or moving an existing one. Done confirms at the ghost's
/// current cell (disabled if that cell is invalid/red). Cancel backs out - for a move, the
/// building returns to its original spot; for a fresh placement, it's simply abandoned.
public class PlacementControlPanel : MonoBehaviour
{
    public GameObject panelRoot; // can be this.gameObject
    public Button doneButton;
    public Button cancelButton;
    public Button rotateButton; // optional - assign if you have a rotate button in this panel

    void Awake()
    {
        if (panelRoot == null) panelRoot = gameObject;

        doneButton.onClick.AddListener(OnDoneClicked);
        cancelButton.onClick.AddListener(OnCancelClicked);
        if (rotateButton != null) rotateButton.onClick.AddListener(OnRotateClicked);

        // IMPORTANT: do NOT call Hide() here. If panelRoot starts inactive in the scene
        // (as it should - see setup notes), Awake() only runs the moment it's FIRST activated.
        // Calling SetActive(false) from inside that same Awake() would undo the very
        // SetActive(true) that triggered it, silently cancelling the first-ever Show().
        // Just make sure panelRoot is unchecked (inactive) in the Hierarchy by default.
    }

    void Update()
    {
        if (!panelRoot.activeSelf) return;
        doneButton.interactable = BuildingPlacementController.Instance.IsCurrentGhostValid();
    }

    public void Show()
    {
        panelRoot.SetActive(true);
    }

    public void Hide()
    {
        panelRoot.SetActive(false);
    }

    private void OnDoneClicked()
    {
        BuildingPlacementController.Instance.ConfirmCurrentGhostPlacement();
    }

    private void OnCancelClicked()
    {
        BuildingPlacementController.Instance.CancelCurrentAction();
    }

    private void OnRotateClicked()
    {
        BuildingPlacementController.Instance.RotateGhost();
    }
}