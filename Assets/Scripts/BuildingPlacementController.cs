using UnityEngine;
using System.Collections.Generic;

/// Handles the full place / move / demolish workflow for buildings.
/// Works via touch (mobile) - tap to place, drag existing building to move,
/// long-press or a "demolish mode" toggle button to remove.
public class BuildingPlacementController : MonoBehaviour
{
    public static BuildingPlacementController Instance { get; private set; }

    public Camera mainCamera;
    public LayerMask groundLayerMask;
    public Material ghostValidMaterial;
    public Material ghostInvalidMaterial;
    public PlacementControlPanel controlPanel; // assign in Inspector - the Done/Cancel panel

    private bool lastGhostValid = false;
    public bool IsCurrentGhostValid() => lastGhostValid;

    private enum Mode { None, Placing, Moving, Demolishing }
    private Mode currentMode = Mode.None;

    /// True while placing, moving, or in demolish mode - selection should ignore taps during this.
    public bool CurrentModeIsBusy => currentMode != Mode.None;

    /// True specifically during Placing or Moving (not Demolishing) - used to show/hide Done/Cancel UI.
    public bool IsPlacingOrMoving => currentMode == Mode.Placing || currentMode == Mode.Moving;

    private BuildingData selectedBuildingData; // from shop
    private GameObject ghostObject;
    private Vector2Int lastGhostCell;

    private BuildingInstance movingInstance; // set when repositioning an existing building
    private int nextInstanceId = 0;
    private Dictionary<int, BuildingInstance> instances = new Dictionary<int, BuildingInstance>();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        if (mainCamera == null) mainCamera = Camera.main;
    }

    void Update()
    {
        if (currentMode == Mode.None) return;
        if (UIPointerUtil.IsPointerOverUI()) return; // don't let a UI click also register as a world tap

        if (!TryGetPointerScreenPosition(out Vector2 screenPos)) return; // no touch/click this frame

        if (currentMode == Mode.Placing || currentMode == Mode.Moving)
            HandlePlacingOrMoving(screenPos);
        else if (currentMode == Mode.Demolishing)
            HandleDemolishTap(screenPos);
    }

    // ---------- Public API called from Shop / UI ----------

    public void BeginPlacement(BuildingData data)
    {
        CancelCurrentAction();
        selectedBuildingData = data;
        currentMode = Mode.Placing;
        SpawnGhost(data.levels[0].visualPrefab, data);
        controlPanel?.Show(); // show immediately - must not depend on anything that could throw below

        if (mainCamera == null) mainCamera = Camera.main; // camera may not have been ready at Awake

        if (mainCamera != null)
        {
            // Start the ghost wherever the camera is currently looking, not world origin (0,0).
            Ray centerRay = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(centerRay, out RaycastHit hit, 500f, groundLayerMask))
            {
                Vector2Int cell = GridManager.Instance.WorldToGrid(hit.point);
                lastGhostCell = cell;
                lastGhostValid = GridManager.Instance.CanPlaceFootprint(cell, data.footprintSize) && PassesRoadCheck(cell);
                ghostObject.transform.position = GridManager.Instance.FootprintCenterToWorld(cell, data.footprintSize);
                SetGhostMaterial(lastGhostValid);
            }
        }
    }

    public void BeginMove(BuildingInstance instance)
    {
        CancelCurrentAction();
        movingInstance = instance;
        selectedBuildingData = instance.data;
        currentMode = Mode.Moving;

        Vector2Int origin = instance.gridOrigin;
        // Free the old footprint immediately so the ghost can preview over it.
        GridManager.Instance.ClearFootprint(origin, instance.data.footprintSize);
        SpawnGhost(instance.data.GetLevel(instance.currentLevelIndex).visualPrefab, instance.data);

        // Start the ghost exactly where the building already is, not at world origin.
        ghostObject.transform.position = GridManager.Instance.FootprintCenterToWorld(origin, instance.data.footprintSize);
        lastGhostCell = origin;
        lastGhostValid = GridManager.Instance.CanPlaceFootprint(origin, instance.data.footprintSize);
        SetGhostMaterial(lastGhostValid);

        instance.gameObject.SetActive(false);
        controlPanel?.Show();
    }

    public void EnterDemolishMode()
    {
        CancelCurrentAction();
        currentMode = Mode.Demolishing;
    }

    public void CancelCurrentAction()
    {
        if (movingInstance != null)
        {
            // put it back where it was if cancelled
            GridManager.Instance.SetFootprint(movingInstance.gridOrigin, movingInstance.data.footprintSize,
                GridManager.CellType.Building, movingInstance.instanceId);
            movingInstance.gameObject.SetActive(true);
            movingInstance = null;
        }
        if (ghostObject != null) Destroy(ghostObject);
        selectedBuildingData = null;
        currentMode = Mode.None;
        controlPanel?.Hide();
    }

    // ---------- Placement / move flow ----------

    /// Stretches a placeholder cube (or similar unit-sized primitive) so its X/Z footprint
    /// exactly matches footprintSize * cellSize. Leaves height (Y) untouched. No-op if
    /// data.autoScaleToFootprint is false (i.e. you're using a properly-sized real model).
    private void ApplyFootprintScale(GameObject obj, BuildingData data)
    {
        if (!data.autoScaleToFootprint) return;
        Vector3 scale = obj.transform.localScale;
        scale.x = data.footprintSize.x * GridManager.Instance.cellSize;
        scale.z = data.footprintSize.y * GridManager.Instance.cellSize;
        obj.transform.localScale = scale;
    }

    private void SpawnGhost(GameObject prefab, BuildingData data)
    {
        ghostObject = Instantiate(prefab);
        ghostObject.transform.SetParent(null, true); // force to scene root, ignore any inherited context
        ApplyFootprintScale(ghostObject, data);
        SetGhostMaterial(false);
    }

    private void HandlePlacingOrMoving(Vector2 screenPos)
    {
        if (!RaycastGround(screenPos, out Vector3 worldPoint)) return;

        Vector2Int cell = GridManager.Instance.WorldToGrid(worldPoint);
        bool canPlace = GridManager.Instance.CanPlaceFootprint(cell, selectedBuildingData.footprintSize)
            && PassesRoadCheck(cell);

        if (cell != lastGhostCell)
        {
            lastGhostCell = cell;
            ghostObject.transform.position = GridManager.Instance.FootprintCenterToWorld(cell, selectedBuildingData.footprintSize);
        }
        lastGhostValid = canPlace;
        SetGhostMaterial(canPlace);

        // No tap-to-confirm anymore - the ghost just follows the pointer while dragging and
        // freezes at its last position when released. Confirmation is explicit via the
        // Done/Cancel panel (see ConfirmCurrentGhostPlacement / CancelCurrentAction), so an
        // invalid (red) spot never silently "sticks" without the player choosing what to do.
    }

    /// Called by the Done button. Confirms placement at the ghost's current cell,
    /// but only if that cell is currently valid (button should be disabled otherwise).
    public void ConfirmCurrentGhostPlacement()
    {
        if (!lastGhostValid) return;
        ConfirmPlacement(lastGhostCell);
    }

    private void ConfirmPlacement(Vector2Int cell)
    {
        if (currentMode == Mode.Placing)
        {
            if (!EconomyManager.Instance.TrySpend(selectedBuildingData.placementCost))
                return; // not enough funds - stay in placing mode

            GameObject obj = Instantiate(selectedBuildingData.levels[0].visualPrefab,
                GridManager.Instance.FootprintCenterToWorld(cell, selectedBuildingData.footprintSize), Quaternion.identity);
            obj.transform.SetParent(null, true); // force to scene root
            ApplyFootprintScale(obj, selectedBuildingData);
            var instance = obj.AddComponent<BuildingInstance>();
            int id = nextInstanceId++;
            instance.Initialize(selectedBuildingData, cell, id);
            instances[id] = instance;
            GridManager.Instance.SetFootprint(cell, selectedBuildingData.footprintSize, GridManager.CellType.Building, id);
        }
        else if (currentMode == Mode.Moving)
        {
            movingInstance.transform.position = GridManager.Instance.FootprintCenterToWorld(cell, movingInstance.data.footprintSize);
            movingInstance.gridOrigin = cell;
            movingInstance.gameObject.SetActive(true);
            GridManager.Instance.SetFootprint(cell, movingInstance.data.footprintSize, GridManager.CellType.Building, movingInstance.instanceId);
            movingInstance = null;
        }

        Destroy(ghostObject);
        selectedBuildingData = null;
        currentMode = Mode.None;
        controlPanel?.Hide();
    }

    private void HandleDemolishTap(Vector2 screenPos)
    {
        if (!IsConfirmTap()) return;
        if (!RaycastGround(screenPos, out Vector3 worldPoint)) return;

        Vector2Int cell = GridManager.Instance.WorldToGrid(worldPoint);
        var type = GridManager.Instance.GetType(cell);

        if (type == GridManager.CellType.Building)
        {
            int id = GridManager.Instance.GetOccupant(cell);
            if (instances.TryGetValue(id, out var instance))
            {
                GridManager.Instance.ClearFootprint(instance.gridOrigin, instance.data.footprintSize);
                EconomyManager.Instance.RefundDemolish(instance.data.placementCost);
                instances.Remove(id);
                Destroy(instance.gameObject);
            }
        }
        // NOTE: roads are pre-built static geometry now (see RoadFootprintScanner) - they can't
        // be demolished by the player, so there's no "else if road" branch here anymore.
    }

    // ---------- Helpers ----------

    /// Look up a placed building by its instance id (e.g. GridManager.GetOccupant(cell)).
    /// Returns null if no building with that id exists (or id is -1 / empty cell).
    public BuildingInstance GetInstanceById(int id)
    {
        return instances.TryGetValue(id, out var instance) ? instance : null;
    }

    /// Demolishes a specific building directly (used by the select-first-then-Demolish-button flow).
    /// No mode switching, no "next tap" needed - call this straight from the Demolish button.
    public void DemolishInstance(BuildingInstance instance)
    {
        if (instance == null) return;
        GridManager.Instance.ClearFootprint(instance.gridOrigin, instance.data.footprintSize);
        EconomyManager.Instance.RefundDemolish(instance.data.placementCost);
        instances.Remove(instance.instanceId);
        BuildingSelectionController.Instance?.Deselect();
        Destroy(instance.gameObject);
    }

    private void SetGhostMaterial(bool valid)
    {
        var mat = valid ? ghostValidMaterial : ghostInvalidMaterial;
        foreach (var r in ghostObject.GetComponentsInChildren<Renderer>())
            r.material = mat;
    }

    /// Returns true if the building doesn't require footpath adjacency, OR RoadManager isn't in
    /// the scene yet (so you can test placement before the road/footpath system exists), OR the
    /// footpath check passes. Checks footpath, not road - buildings connect to the sidewalk,
    /// not the vehicle lane, since the two run parallel to each other.
    private bool PassesRoadCheck(Vector2Int cell)
    {
        if (!selectedBuildingData.requiresFootpathAdjacency) return true;
        if (RoadManager.Instance == null) return true; // road/footpath not implemented yet - don't block placement
        return RoadManager.Instance.IsAdjacentToFootpath(cell, selectedBuildingData.footprintSize);
    }

    private bool RaycastGround(Vector2 screenPos, out Vector3 worldPoint)
    {
        Ray ray = mainCamera.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 500f, groundLayerMask))
        {
            worldPoint = hit.point;
            return true;
        }
        worldPoint = Vector3.zero;
        return false;
    }

    /// Returns current pointer position (touch on mobile, mouse in editor) via out param.
    /// Returns false if there's no active touch/click this frame - caller should skip processing.
    private bool TryGetPointerScreenPosition(out Vector2 pos)
    {
#if UNITY_EDITOR
        if (Input.GetMouseButton(0) || Input.GetMouseButtonUp(0)) { pos = Input.mousePosition; return true; }
        pos = default;
        return false;
#else
        if (Input.touchCount > 0) { pos = Input.GetTouch(0).position; return true; }
        pos = default;
        return false;
#endif
    }

    private bool IsConfirmTap()
    {
#if UNITY_EDITOR
        return Input.GetMouseButtonUp(0);
#else
        return Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Ended;
#endif
    }
}