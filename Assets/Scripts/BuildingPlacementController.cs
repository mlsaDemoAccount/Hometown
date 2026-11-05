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
    public Material ghostInvalidMaterial;
    [Range(0.1f, 1f)]
    [Tooltip("Opacity of the building's own materials when the ghost is on a valid position.")]
    public float ghostValidAlpha = 0.5f;
    [Header("Ground Indicator Sprite (centralized - not per-building)")]
    public Sprite indicatorValidSprite;
    public Sprite indicatorInvalidSprite;
    public PlacementControlPanel controlPanel; // assign in Inspector - the Done/Cancel panel
    [Tooltip("Optional - if assigned, every REAL placed building (not the ghost preview) is parented under this Transform for scene organization only. Uses worldPositionStays=true, so this has zero effect on position/rotation/scale/behavior - purely cosmetic Hierarchy grouping. Leave unassigned to spawn at scene root as before.")]
    public Transform buildingsParent;

    private bool lastGhostValid = false;
    public bool IsCurrentGhostValid() => lastGhostValid;

    // Ghost material state - caches each renderer's real materials (all slots, including
    // multi-material meshes) so they can be restored transparently on a valid position, and
    // avoids recreating transparent material instances every frame when nothing has changed.
    private Dictionary<Renderer, Material[]> ghostOriginalMaterials = new Dictionary<Renderer, Material[]>();
    private List<Material> ghostTransparentInstances = new List<Material>();
    private bool? lastAppliedGhostState = null;
    private GhostBaseIndicator ghostBaseIndicator; // base decal sprite, handled separately from the generic material loop

    private enum Mode { None, Placing, Moving, Demolishing }
    private Mode currentMode = Mode.None;

    /// True while placing, moving, or in demolish mode - selection should ignore taps during this.
    public bool CurrentModeIsBusy => currentMode != Mode.None;

    /// True specifically during Placing or Moving (not Demolishing) - used to show/hide Done/Cancel UI.
    public bool IsPlacingOrMoving => currentMode == Mode.Placing || currentMode == Mode.Moving;

    private BuildingData selectedBuildingData; // from shop
    private GameObject ghostObject;
    private const float MoveIdleSeconds = 30f; // co-op: an untouched move preview gives up its lock after this long
    private float lastMoveActivity;
    private Vector2Int lastGhostCell;
    private int ghostRotationSteps = 0; // 0-3, each step = 90 degrees clockwise

    private BuildingInstance movingInstance; // set when repositioning an existing building
    private int nextInstanceId = 0;
    private Dictionary<int, BuildingInstance> instances = new Dictionary<int, BuildingInstance>();

    /// Returns the visual prefab for a given upgrade level.
    private GameObject GetVisualPrefab(BuildingData data, int levelIndex = 0)
    {
        var level = data.GetLevel(levelIndex);
        return level != null ? level.visualPrefab : null;
    }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        if (mainCamera == null) mainCamera = Camera.main;
    }

    void Update()
    {
        if (currentMode == Mode.None) return;
        if (Coop.EditLocked) { CancelCurrentAction(); return; } // the other player is away: nothing can change
        if (currentMode == Mode.Moving && Coop.Active && Time.unscaledTime - lastMoveActivity > MoveIdleSeconds)
        {
            CancelCurrentAction(); // frees the building for the other player
            CityNetworkSync.Instance.ShowNotice("Move cancelled - no activity.");
            return;
        }
        if (UIPointerUtil.IsPointerOverUI()) return; // don't let a UI click also register as a world tap

        if (!TryGetPointerScreenPosition(out Vector2 screenPos)) return; // no touch/click this frame
        lastMoveActivity = Time.unscaledTime;

        if (currentMode == Mode.Placing || currentMode == Mode.Moving)
            HandlePlacingOrMoving(screenPos);
        else if (currentMode == Mode.Demolishing)
            HandleDemolishTap(screenPos);
    }

    // ---------- Public API called from Shop / UI ----------

    public void BeginPlacement(BuildingData data)
    {
        if (Coop.EditLocked) return;
        CancelCurrentAction();
        selectedBuildingData = data;
        currentMode = Mode.Placing;
        ghostRotationSteps = 0; // fresh placement always starts unrotated
        SpawnGhost(GetVisualPrefab(data), data);
        controlPanel?.Show(); // show immediately - must not depend on anything that could throw below

        if (mainCamera == null) mainCamera = Camera.main; // camera may not have been ready at Awake

        if (mainCamera != null)
        {
            // Start the ghost wherever the camera is currently looking, not world origin (0,0).
            Ray centerRay = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(centerRay, out RaycastHit hit, 500f, groundLayerMask))
            {
                Vector2Int cell = GridManager.Instance.WorldToGrid(hit.point);
                Vector2Int effectiveSize = BuildingInstance.GetRotatedFootprint(data.footprintSize, ghostRotationSteps);
                lastGhostCell = cell;
                lastGhostValid = GridManager.Instance.CanPlaceFootprint(cell, effectiveSize) && PassesRoadCheck(cell, effectiveSize);
                ghostObject.transform.position = GridManager.Instance.FootprintCenterToWorld(cell, effectiveSize);
                SetGhostMaterial(lastGhostValid);
            }
        }
    }

    public void BeginMove(BuildingInstance instance)
    {
        if (Coop.EditLocked) return;
        if (IsLockedByOther(instance.instanceId)) { CityNetworkSync.Instance.ShowNotice("Your friend is moving this building right now."); return; }
        CancelCurrentAction();
        movingInstance = instance;
        lastMoveActivity = Time.unscaledTime;
        selectedBuildingData = instance.data;
        currentMode = Mode.Moving;
        ghostRotationSteps = instance.rotationSteps; // start the ghost matching its current orientation

        Vector2Int origin = instance.gridOrigin;
        Vector2Int existingEffectiveSize = BuildingInstance.GetRotatedFootprint(instance.data.footprintSize, instance.rotationSteps);
        // Free the old footprint immediately so the ghost can preview over it.
        GridManager.Instance.ClearFootprint(origin, existingEffectiveSize);
        SpawnGhost(instance.data.GetLevel(instance.currentLevelIndex).visualPrefab, instance.data);
        ghostObject.transform.rotation = Quaternion.Euler(0f, ghostRotationSteps * 90f, 0f);

        // Start the ghost exactly where the building already is, not at world origin.
        ghostObject.transform.position = GridManager.Instance.FootprintCenterToWorld(origin, existingEffectiveSize);
        lastGhostCell = origin;
        lastGhostValid = GridManager.Instance.CanPlaceFootprint(origin, existingEffectiveSize);
        SetGhostMaterial(lastGhostValid);

        instance.gameObject.SetActive(false);
        controlPanel?.Show();

        // Co-op: tell the host (and so the friend) that this building is in use.
        if (Coop.IsHost) CityNetworkSync.Instance.BroadcastLock(instance.instanceId, true);
        else if (Coop.IsGuest) CityNetworkSync.Instance.RequestLock(instance.instanceId);
    }

    /// Co-op: the other player is moving this building, so it can't be moved, upgraded or demolished now.
    public bool IsLockedByOther(int id) => Coop.Active && CityNetworkSync.Instance.IsLockedByOther(id);

    /// The building this phone is moving right now, or -1.
    public int MovingId => movingInstance != null ? movingInstance.instanceId : -1;

    /// Co-op: our move ended (Done / cancel) - let the other player use the building again.
    private void ReleaseMoveLock(int id)
    {
        if (Coop.IsHost) CityNetworkSync.Instance.BroadcastLock(id, false);
        else if (Coop.IsGuest) CityNetworkSync.Instance.RequestReleaseLock(id);
    }

    public void EnterDemolishMode()
    {
        if (Coop.EditLocked) return;
        CancelCurrentAction();
        currentMode = Mode.Demolishing;
    }

    public void CancelCurrentAction()
    {
        if (movingInstance != null)
        {
            // put it back where it was if cancelled - use its EXISTING rotation's effective
            // size, not the raw footprint, in case it was already rotated before this move began.
            Vector2Int effectiveSize = BuildingInstance.GetRotatedFootprint(movingInstance.data.footprintSize, movingInstance.rotationSteps);
            GridManager.Instance.SetFootprint(movingInstance.gridOrigin, effectiveSize,
                GridManager.CellType.Building, movingInstance.instanceId);
            movingInstance.gameObject.SetActive(true);
            int releasedId = movingInstance.instanceId;
            movingInstance = null;
            ReleaseMoveLock(releasedId);
        }
        if (ghostObject != null) { ClearGhostMaterialState(); Destroy(ghostObject); }
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

        CacheOriginalMaterials(ghostObject);
        lastAppliedGhostState = null; // force the first SetGhostMaterial call to actually apply
        SetGhostMaterial(false);
    }

    /// Records each renderer's real materials (every slot, including multi-material meshes)
    /// before any ghost tinting happens - this is what gets restored (transparently) when the
    /// ghost is on a valid position, instead of a flat placeholder color. Excludes the base
    /// indicator sprite (if present) - that gets dedicated handling via GhostBaseIndicator.
    private void CacheOriginalMaterials(GameObject obj)
    {
        ghostOriginalMaterials.Clear();

        ghostBaseIndicator = obj.GetComponentInChildren<GhostBaseIndicator>();
        Renderer indicatorRenderer = ghostBaseIndicator != null ? ghostBaseIndicator.spriteRenderer : null;

        foreach (var r in obj.GetComponentsInChildren<Renderer>())
        {
            if (r == indicatorRenderer) continue; // handled separately, not via the generic transparency system
            ghostOriginalMaterials[r] = r.materials; // .materials already returns per-instance copies
        }
    }

    /// Destroys any transparent material instances created for the current ghost - call before
    /// discarding the ghost object entirely, since these are plain C# objects that don't get
    /// cleaned up automatically just because the Renderer referencing them was destroyed.
    private void ClearGhostMaterialState()
    {
        foreach (var m in ghostTransparentInstances) if (m != null) Destroy(m);
        ghostTransparentInstances.Clear();
        ghostOriginalMaterials.Clear();
        ghostBaseIndicator = null;
        lastAppliedGhostState = null;
    }

    private void HandlePlacingOrMoving(Vector2 screenPos)
    {
        if (!RaycastGround(screenPos, out Vector3 worldPoint)) return;

        Vector2Int cell = GridManager.Instance.WorldToGrid(worldPoint);
        Vector2Int effectiveSize = BuildingInstance.GetRotatedFootprint(selectedBuildingData.footprintSize, ghostRotationSteps);
        bool canPlace = GridManager.Instance.CanPlaceFootprint(cell, effectiveSize)
            && PassesRoadCheck(cell, effectiveSize);

        if (cell != lastGhostCell)
        {
            lastGhostCell = cell;
            ghostObject.transform.position = GridManager.Instance.FootprintCenterToWorld(cell, effectiveSize);
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
        if (Coop.EditLocked) return;
        RefreshGhostValidity(); // the grid may have changed since the last drag (co-op)
        if (!lastGhostValid) return;
        ConfirmPlacement(lastGhostCell);
    }

    /// Rotates the current ghost 90 degrees clockwise. Call from a Rotate button during
    /// placement or move. Re-validates the position and re-centers the ghost, since a
    /// non-square footprint's effective width/depth swaps on a 90/270 degree rotation.
    public void RotateGhost()
    {
        if (ghostObject == null || selectedBuildingData == null) return;

        ghostRotationSteps = (ghostRotationSteps + 1) % 4;
        ghostObject.transform.rotation = Quaternion.Euler(0f, ghostRotationSteps * 90f, 0f);

        Vector2Int effectiveSize = BuildingInstance.GetRotatedFootprint(selectedBuildingData.footprintSize, ghostRotationSteps);
        bool canPlace = GridManager.Instance.CanPlaceFootprint(lastGhostCell, effectiveSize) && PassesRoadCheck(lastGhostCell, effectiveSize);
        lastGhostValid = canPlace;
        ghostObject.transform.position = GridManager.Instance.FootprintCenterToWorld(lastGhostCell, effectiveSize);
        SetGhostMaterial(canPlace);
    }

    private void ConfirmPlacement(Vector2Int cell)
    {
        if (currentMode == Mode.Placing)
        {
            if (EconomyManager.Instance.currentFunds < selectedBuildingData.placementCost)
            {
                Debug.LogWarning($"[BuildingPlacementController] Can't place '{selectedBuildingData.displayName}' - costs {selectedBuildingData.placementCost}, but only {EconomyManager.Instance.currentFunds} available. Ghost stays active; Done will keep failing until funds are sufficient.");
                return; // not enough funds - stay in placing mode
            }

            if (Coop.IsGuest)
            {
                // Co-op guest: only ASK. The host checks the spot and the funds, builds it, and
                // tells both phones - the building appears here when the host confirms.
                CityNetworkSync.Instance.RequestPlace(selectedBuildingData.buildingId, cell, ghostRotationSteps);
            }
            else
            {
                EconomyManager.Instance.TrySpend(selectedBuildingData.placementCost);
                var placed = CreateBuilding(selectedBuildingData, cell, ghostRotationSteps, -1, true);
                if (Coop.IsHost) CityNetworkSync.Instance.BroadcastPlaced(placed);
            }
        }
        else if (currentMode == Mode.Moving)
        {
            if (Coop.IsGuest)
            {
                // Co-op guest: put the building back and ASK; the host moves it on both phones.
                int id = movingInstance.instanceId;
                CancelMoveInPlace();
                CityNetworkSync.Instance.RequestMove(id, cell, ghostRotationSteps);
                ReleaseMoveLock(id); // sent after the move request, so the host handles them in this order
            }
            else
            {
                var moved = movingInstance;
                movingInstance = null;
                ApplyMove(moved, cell, ghostRotationSteps);
                if (Coop.IsHost)
                {
                    CityNetworkSync.Instance.BroadcastMoved(moved.instanceId, cell, ghostRotationSteps);
                    ReleaseMoveLock(moved.instanceId);
                }
            }
        }

        ClearGhostMaterialState();
        Destroy(ghostObject);
        selectedBuildingData = null;
        currentMode = Mode.None;
        controlPanel?.Hide();
        CitySaveManager.Instance?.SaveCity();
    }

    /// Puts a building at a new spot and re-marks the grid (its old footprint must already be free).
    private void ApplyMove(BuildingInstance instance, Vector2Int cell, int rotationSteps)
    {
        Vector2Int effectiveSize = BuildingInstance.GetRotatedFootprint(instance.data.footprintSize, rotationSteps);
        instance.transform.position = GridManager.Instance.FootprintCenterToWorld(cell, effectiveSize);
        instance.transform.rotation = Quaternion.Euler(0f, rotationSteps * 90f, 0f);
        instance.gridOrigin = cell;
        instance.rotationSteps = rotationSteps;
        instance.gameObject.SetActive(true);
        DisableIndicatorOnRealBuilding(instance.gameObject); // defensive - should already be disabled from original placement
        GridManager.Instance.SetFootprint(cell, effectiveSize, GridManager.CellType.Building, instance.instanceId);
    }

    /// Undoes the "lift" of BeginMove without leaving move mode's other state behind (co-op guest).
    private void CancelMoveInPlace()
    {
        Vector2Int size = BuildingInstance.GetRotatedFootprint(movingInstance.data.footprintSize, movingInstance.rotationSteps);
        GridManager.Instance.SetFootprint(movingInstance.gridOrigin, size, GridManager.CellType.Building, movingInstance.instanceId);
        movingInstance.gameObject.SetActive(true);
        movingInstance = null;
    }

    /// Disables the base indicator sprite on a REAL, placed building - it's part of the prefab
    /// so it comes along automatically when instantiated, but should only ever be visible on
    /// the ghost preview, not on a building that's actually been placed.
    private void DisableIndicatorOnRealBuilding(GameObject obj)
    {
        var indicator = obj.GetComponentInChildren<GhostBaseIndicator>();
        if (indicator != null) indicator.gameObject.SetActive(false);
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
                DemolishInstance(instance);
        }
        // NOTE: roads are pre-built static geometry now (see RoadFootprintScanner) - they can't
        // be demolished by the player, so there's no "else if road" branch here anymore.
    }

    // ---------- Building creation / removal (shared by solo, co-op host and co-op guest) ----------

    /// Builds a new building from a purchase: spawns it, marks the grid, starts the construction
    /// timer. Funds are NOT handled here. awardXp is false on the co-op guest (the host owns XP).
    private BuildingInstance CreateBuilding(BuildingData data, Vector2Int cell, int rotationSteps, int forcedId, bool awardXp)
    {
        Vector2Int effectiveSize = BuildingInstance.GetRotatedFootprint(data.footprintSize, rotationSteps);
        Quaternion rotation = Quaternion.Euler(0f, rotationSteps * 90f, 0f);

        GameObject obj = Instantiate(GetVisualPrefab(data),
            GridManager.Instance.FootprintCenterToWorld(cell, effectiveSize), rotation);
        obj.transform.SetParent(buildingsParent, true); // true = keep world position exactly as placed, purely organizational (null = scene root, same as before if unassigned)
        ApplyFootprintScale(obj, data);
        DisableIndicatorOnRealBuilding(obj); // indicator sprite is only for the ghost preview
        var instance = obj.AddComponent<BuildingInstance>();
        int id = TakeId(forcedId);
        instance.Initialize(data, cell, id);
        instance.rotationSteps = rotationSteps;
        instances[id] = instance;
        GridManager.Instance.SetFootprint(cell, effectiveSize, GridManager.CellType.Building, id);

        // Start construction timer if this prefab has one set up and a nonzero duration -
        // otherwise it's placed instantly, same as before this feature existed.
        var constructionState = obj.GetComponent<BuildingConstructionState>();
        if (constructionState != null && data.constructionDuration > 0f)
        {
            constructionState.BeginConstruction(data.constructionDuration);
            // XP awarded later, when construction actually completes (see BuildingInstance.CompleteInitialConstruction) -
            // not here, so a half-built building doesn't already grant the reward.
        }
        else if (awardXp)
        {
            // No timer - building is instantly complete, award this building's own XP now.
            PlayerProgressManager.Instance?.AddXP(data.placementXP);
        }
        RefreshGhostValidity(); // a building may have just landed under the other player's preview
        return instance;
    }

    /// Re-checks the spot under the current preview, e.g. after the other player built there.
    private void RefreshGhostValidity()
    {
        if (ghostObject == null || selectedBuildingData == null) return;
        Vector2Int size = BuildingInstance.GetRotatedFootprint(selectedBuildingData.footprintSize, ghostRotationSteps);
        lastGhostValid = GridManager.Instance.CanPlaceFootprint(lastGhostCell, size) && PassesRoadCheck(lastGhostCell, size);
        SetGhostMaterial(lastGhostValid);
    }

    private int TakeId(int forcedId)
    {
        if (forcedId < 0) return nextInstanceId++;
        if (forcedId >= nextInstanceId) nextInstanceId = forcedId + 1;
        return forcedId;
    }

    /// Removes a building: frees its cells and destroys it. refund = false on the co-op guest.
    private void RemoveBuilding(BuildingInstance instance, bool refund)
    {
        if (instance == movingInstance) { movingInstance = null; CancelCurrentAction(); } // the other player removed what we were moving
        Vector2Int effectiveSize = BuildingInstance.GetRotatedFootprint(instance.data.footprintSize, instance.rotationSteps);
        GridManager.Instance.ClearFootprint(instance.gridOrigin, effectiveSize);
        if (refund) EconomyManager.Instance.RefundDemolish(instance.data.placementCost);
        instances.Remove(instance.instanceId);
        CityNetworkSync.Instance?.ForgetLock(instance.instanceId);
        if (BuildingSelectionController.Instance != null && BuildingSelectionController.Instance.Selected == instance)
            BuildingSelectionController.Instance.Deselect();
        Destroy(instance.gameObject);
        CitySaveManager.Instance?.SaveCity();
    }

    // ---------- Co-op entry points (called by CityNetworkSync only) ----------

    /// Host: a guest asked to place a building. Checks everything, then builds it.
    /// Returns the new building, or null with the reason in 'error'.
    public BuildingInstance HostPlace(string buildingId, Vector2Int cell, int rotationSteps, out string error)
    {
        error = null;
        var data = BuildingDatabase.Instance != null ? BuildingDatabase.Instance.GetById(buildingId) : null;
        if (data == null) { error = "Unknown building."; return null; }

        Vector2Int size = BuildingInstance.GetRotatedFootprint(data.footprintSize, rotationSteps);
        if (!GridManager.Instance.CanPlaceFootprint(cell, size) || !PassesRoadCheck(data, cell, size))
        {
            error = "That spot isn't available any more.";
            return null;
        }
        if (!EconomyManager.Instance.TrySpend(data.placementCost))
        {
            error = "Not enough funds.";
            return null;
        }
        return CreateBuilding(data, cell, rotationSteps, -1, true);
    }

    /// Guest: the host built this - show it. Skipped if it is already here (see CityNetworkSync).
    public void RemotePlaced(string buildingId, Vector2Int cell, int rotationSteps, int id)
    {
        if (instances.ContainsKey(id)) return;
        var data = BuildingDatabase.Instance != null ? BuildingDatabase.Instance.GetById(buildingId) : null;
        if (data == null) { Debug.LogWarning("[BuildingPlacementController] Host placed unknown building '" + buildingId + "'."); return; }
        CreateBuilding(data, cell, rotationSteps, id, false);
    }

    /// Host: a guest asked to demolish.
    public bool HostDemolish(int id)
    {
        if (!instances.TryGetValue(id, out var instance)) return false;
        RemoveBuilding(instance, true);
        return true;
    }

    /// Guest: the host demolished this - remove it (no refund here; funds come from the host).
    public void RemoteDemolished(int id)
    {
        if (instances.TryGetValue(id, out var instance)) RemoveBuilding(instance, false);
    }

    /// Host: a guest asked to move a building. Checks the new spot, then moves it.
    public bool HostMove(int id, Vector2Int cell, int rotationSteps, out string error)
    {
        error = null;
        if (!instances.TryGetValue(id, out var instance)) { error = "That building is gone."; return false; }

        bool hostIsMovingIt = instance == movingInstance; // its cells are already free in that case
        Vector2Int oldSize = BuildingInstance.GetRotatedFootprint(instance.data.footprintSize, instance.rotationSteps);
        Vector2Int newSize = BuildingInstance.GetRotatedFootprint(instance.data.footprintSize, rotationSteps);
        GridManager.Instance.ClearFootprint(instance.gridOrigin, oldSize); // its own cells don't block it
        if (!GridManager.Instance.CanPlaceFootprint(cell, newSize) || !PassesRoadCheck(instance.data, cell, newSize))
        {
            if (!hostIsMovingIt) GridManager.Instance.SetFootprint(instance.gridOrigin, oldSize, GridManager.CellType.Building, id);
            error = "That spot isn't available any more.";
            return false;
        }
        if (hostIsMovingIt)
        {
            // The friend finished first: their move wins and our unfinished move is dropped.
            CancelCurrentAction();
            GridManager.Instance.ClearFootprint(instance.gridOrigin, oldSize);
            CityNetworkSync.Instance.ShowNotice("Your friend moved that building first.");
        }
        ApplyMove(instance, cell, rotationSteps);
        CitySaveManager.Instance?.SaveCity();
        RefreshGhostValidity();
        return true;
    }

    /// Guest: the host moved this building.
    public void RemoteMoved(int id, Vector2Int cell, int rotationSteps)
    {
        if (!instances.TryGetValue(id, out var instance)) return;
        if (instance == movingInstance)
        {
            // The host's move finished first and wins: drop our unfinished move.
            CancelCurrentAction();
            CityNetworkSync.Instance.ShowNotice("Your friend moved that building first.");
        }
        Vector2Int oldSize = BuildingInstance.GetRotatedFootprint(instance.data.footprintSize, instance.rotationSteps);
        GridManager.Instance.ClearFootprint(instance.gridOrigin, oldSize);
        ApplyMove(instance, cell, rotationSteps);
        RefreshGhostValidity();
    }

    /// Host: a guest asked to upgrade a building. Spends the funds and starts the upgrade.
    public bool HostUpgrade(int id, out string error)
    {
        error = null;
        if (!instances.TryGetValue(id, out var instance) || !instance.CanUpgrade) { error = "That building can't be upgraded now."; return false; }
        if (id == MovingId || IsLockedByOther(id)) { error = "That building is being moved."; return false; }
        if (!instance.TryUpgrade(EconomyManager.Instance)) { error = "Not enough funds."; return false; }
        return true;
    }

    /// Guest: the host started an upgrade (funds were already taken there).
    public void RemoteUpgradeStarted(int id)
    {
        if (instances.TryGetValue(id, out var instance)) instance.StartUpgradeFromHost();
    }

    /// Guest: the host finished this building's construction / upgrade.
    public void RemoteConstructionDone(int id)
    {
        if (instances.TryGetValue(id, out var instance) && instance.ConstructionState != null) instance.ConstructionState.FinishFromHost();
    }

    /// Removes every building - used before loading the host's city onto the guest.
    public void ClearAllBuildings()
    {
        CancelCurrentAction();
        BuildingSelectionController.Instance?.Deselect();
        foreach (var instance in new List<BuildingInstance>(instances.Values))
        {
            if (instance == null) continue;
            Vector2Int size = BuildingInstance.GetRotatedFootprint(instance.data.footprintSize, instance.rotationSteps);
            GridManager.Instance.ClearFootprint(instance.gridOrigin, size);
            Destroy(instance.gameObject);
        }
        instances.Clear();
        nextInstanceId = 0;
    }

    // ---------- Helpers ----------

    /// Look up a placed building by its instance id (e.g. GridManager.GetOccupant(cell)).
    /// Returns null if no building with that id exists (or id is -1 / empty cell).
    public BuildingInstance GetInstanceById(int id)
    {
        return instances.TryGetValue(id, out var instance) ? instance : null;
    }

    /// Every currently placed building - used by CitySaveManager to write the save file.
    public IEnumerable<BuildingInstance> AllPlacedBuildings => instances.Values;

    /// Places a building directly, bypassing the economy/ghost/Done-Cancel flow entirely -
    /// used ONLY by CitySaveManager when restoring a saved city on load. A normal player
    /// placement always goes through BeginPlacement -> ConfirmPlacement instead.
    public void PlaceBuildingDirect(BuildingData data, Vector2Int cell, int rotationSteps, int levelIndex,
        ConstructionPhase savedPhase = ConstructionPhase.Complete, float savedRemaining = 0f, float savedTotal = 0f, int savedPendingLevelIndex = 0, int forcedId = -1)
    {
        Vector2Int effectiveSize = BuildingInstance.GetRotatedFootprint(data.footprintSize, rotationSteps);
        Quaternion rotation = Quaternion.Euler(0f, rotationSteps * 90f, 0f);

        GameObject obj = Instantiate(GetVisualPrefab(data, levelIndex),
            GridManager.Instance.FootprintCenterToWorld(cell, effectiveSize), rotation);
        obj.transform.SetParent(buildingsParent, true); // true = keep world position exactly as placed, purely organizational
        ApplyFootprintScale(obj, data);
        DisableIndicatorOnRealBuilding(obj);

        var instance = obj.AddComponent<BuildingInstance>();
        int id = TakeId(forcedId);
        instance.Initialize(data, cell, id, levelIndex);
        instance.rotationSteps = rotationSteps;
        instances[id] = instance;

        GridManager.Instance.SetFootprint(cell, effectiveSize, GridManager.CellType.Building, id);

        // Restore construction/upgrade progress exactly as saved, instead of starting fresh -
        // a building mid-construction when the game closed should resume, not reset.
        var constructionState = obj.GetComponent<BuildingConstructionState>();
        if (constructionState != null)
            constructionState.RestoreState(savedPhase, savedRemaining, savedTotal, savedPendingLevelIndex);
    }

    /// Demolishes a specific building directly (used by the select-first-then-Demolish-button flow).
    /// No mode switching, no "next tap" needed - call this straight from the Demolish button.
    public void DemolishInstance(BuildingInstance instance)
    {
        if (instance == null || Coop.EditLocked) return;
        if (IsLockedByOther(instance.instanceId)) { CityNetworkSync.Instance.ShowNotice("Your friend is moving this building right now."); return; }
        if (Coop.IsGuest)
        {
            // Co-op guest: ask the host; the building disappears when the host confirms.
            BuildingSelectionController.Instance?.Deselect();
            CityNetworkSync.Instance.RequestDemolish(instance.instanceId);
            return;
        }

        int id = instance.instanceId;
        RemoveBuilding(instance, true);
        if (Coop.IsHost) CityNetworkSync.Instance.BroadcastDemolished(id);
    }

    private void SetGhostMaterial(bool valid)
    {
        if (lastAppliedGhostState == valid) return; // nothing changed - avoid recreating material instances every frame
        lastAppliedGhostState = valid;

        ghostBaseIndicator?.SetValid(valid, indicatorValidSprite, indicatorInvalidSprite);

        // Discard any transparent instances from a previous valid-state application before
        // creating new ones - these are plain objects, not cleaned up automatically.
        foreach (var m in ghostTransparentInstances) if (m != null) Destroy(m);
        ghostTransparentInstances.Clear();

        foreach (var kvp in ghostOriginalMaterials)
        {
            var renderer = kvp.Key;
            var originalMats = kvp.Value;
            if (renderer == null) continue;

            if (!valid)
            {
                // Fill EVERY material slot with the invalid material - a renderer.material
                // (singular) assignment only ever touches slot 0, silently leaving any other
                // slots on a multi-material mesh showing their real material underneath.
                var invalidMats = new Material[originalMats.Length];
                for (int i = 0; i < invalidMats.Length; i++) invalidMats[i] = ghostInvalidMaterial;
                renderer.materials = invalidMats;
            }
            else
            {
                // Valid: restore the building's OWN materials, per slot, at reduced opacity -
                // works for any building since it's driven entirely by whatever this specific
                // ghost's renderers actually have, not a hardcoded material.
                var transparentMats = new Material[originalMats.Length];
                for (int i = 0; i < transparentMats.Length; i++)
                {
                    var t = GhostMaterialUtility.MakeTransparentInstance(originalMats[i], ghostValidAlpha);
                    transparentMats[i] = t;
                    ghostTransparentInstances.Add(t);
                }
                renderer.materials = transparentMats;
            }
        }
    }

    /// Returns true if the building doesn't require footpath adjacency, OR RoadManager isn't in
    /// the scene yet (so you can test placement before the road/footpath system exists), OR the
    /// footpath check passes. Checks footpath, not road - buildings connect to the sidewalk,
    /// not the vehicle lane, since the two run parallel to each other. Takes the EFFECTIVE
    /// (possibly rotated) footprint size, not the raw one, so rotation is checked correctly.
    private bool PassesRoadCheck(Vector2Int cell, Vector2Int effectiveFootprintSize) =>
        PassesRoadCheck(selectedBuildingData, cell, effectiveFootprintSize);

    private bool PassesRoadCheck(BuildingData data, Vector2Int cell, Vector2Int effectiveFootprintSize)
    {
        if (!data.requiresFootpathAdjacency) return true;
        if (RoadManager.Instance == null) return true; // road/footpath not implemented yet - don't block placement
        return RoadManager.Instance.IsAdjacentToFootpath(cell, effectiveFootprintSize);
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