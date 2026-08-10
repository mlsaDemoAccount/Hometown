using UnityEngine;

/// Handles tap-to-select on placed buildings when not in Placing/Moving/Demolishing mode.
/// Other UI (an "Inspector" panel with Move / Demolish / Upgrade buttons) should subscribe to
/// OnSelectionChanged and show/hide itself + call BuildingPlacementController methods directly.
public class BuildingSelectionController : MonoBehaviour
{
    public static BuildingSelectionController Instance { get; private set; }

    public Camera mainCamera;
    public BuildingInspectorPanel inspectorPanel; // assign in Inspector - the Move/Demolish/Upgrade panel

    public BuildingInstance Selected { get; private set; }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        if (mainCamera == null) mainCamera = Camera.main;
    }

    void Update()
    {
        // Only allow selecting when the placement controller isn't busy placing/moving/demolishing.
        if (BuildingPlacementController.Instance.CurrentModeIsBusy) return;
        if (UIPointerUtil.IsPointerOverUI()) return; // don't let a UI click also register as a world tap

        if (mainCamera == null) mainCamera = Camera.main; // camera may not have been ready at Awake
        if (mainCamera == null) return;

        if (!TryGetTapPosition(out Vector2 screenPos)) return;

        Ray ray = mainCamera.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 500f))
        {
            var instance = hit.collider.GetComponentInParent<BuildingInstance>();
            if (instance != null)
            {
                Select(instance);
                return;
            }
        }

        // Tapped empty ground / nothing hit -> deselect.
        Deselect();
    }

    public void Select(BuildingInstance instance)
    {
        if (Selected == instance) return; // already selected, nothing to do
        if (Selected != null) ClearVisual(Selected);

        Selected = instance;
        var bob = instance.gameObject.AddComponent<SelectionBobEffect>();
        bob.Begin();

        if (inspectorPanel != null) inspectorPanel.ShowFor(instance);
    }

    public void Deselect()
    {
        if (Selected == null) return;
        ClearVisual(Selected);
        Selected = null;
        if (inspectorPanel != null) inspectorPanel.Hide();
    }

    private void ClearVisual(BuildingInstance instance)
    {
        if (instance == null) return;
        var bob = instance.GetComponent<SelectionBobEffect>();
        if (bob != null) bob.EndAndRestore();
    }

    private bool TryGetTapPosition(out Vector2 pos)
    {
#if UNITY_EDITOR
        if (Input.GetMouseButtonUp(0)) { pos = Input.mousePosition; return true; }
#else
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Ended)
        {
            pos = Input.GetTouch(0).position;
            return true;
        }
#endif
        pos = default;
        return false;
    }
}