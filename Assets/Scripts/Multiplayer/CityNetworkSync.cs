using System;
using System.Collections.Generic;
using System.Text;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

/// What the rest of the game asks before doing something that co-op changes. Plain static
/// checks, so game scripts never touch Fusion directly.
public static class Coop
{
    /// A co-op session is running and this phone is part of it.
    public static bool Active => CityNetworkSync.Instance != null;

    /// This phone owns the city (it checks and applies every change).
    public static bool IsHost => Active && CityNetworkSync.Instance.IsHostSide;

    /// This phone shows the host's city and sends its changes as requests.
    public static bool IsGuest => Active && !CityNetworkSync.Instance.IsHostSide;

    /// The other player is away (or we lost our connection): the city can't be changed until both are back.
    public static bool EditLocked => NetworkSessionManager.Instance != null && NetworkSessionManager.Instance.Away;
}

/// The one networked object that keeps the two phones' cities identical (the real-game version of
/// the test cube). Follows request -> validate -> apply:
///   1. either player acts. The HOST applies its own actions at once; the GUEST only sends a request;
///   2. the host checks the request (spot still free, enough funds ...) and applies it;
///   3. the host tells the guest what happened, and the guest shows the same result.
/// The host owns funds and XP; they are networked values the guest only displays.
/// A joining guest first receives the host's whole city (reliable data, too big for an RPC).
public class CityNetworkSync : NetworkBehaviour
{
    public static CityNetworkSync Instance { get; private set; }

    [Networked] public int Funds { get; set; }
    [Networked] public int Xp { get; set; }

    public bool IsHostSide => Object.HasStateAuthority;

    /// Guest: the host's city has been loaded. (The host is always ready.)
    public bool CityReady { get; private set; }

    /// A short message for the player ("Not enough funds."), shown by the co-op UI for a few seconds.
    public string Notice { get; private set; }
    public float NoticeUntil { get; private set; }

    private readonly List<Action> waitingForCity = new List<Action>();

    private string pendingCityJson; // the host's city, waiting for Gameplay to be ready to show it

    // Buildings the OTHER player is moving right now (nobody else may move, upgrade or demolish them).
    // The host decides who gets a lock; the guest hears about the host's locks via RPC_LockChanged.
    private readonly HashSet<int> otherLocks = new HashSet<int>();

    public bool IsLockedByOther(int id) => otherLocks.Contains(id);
    public void ForgetLock(int id) => otherLocks.Remove(id);
    public HashSet<int> OtherLocks => otherLocks; // read-only use: which buildings the other player is moving

    public override void Spawned()
    {
        Instance = this;
        // A player who comes back from the main menu receives this object there; it must survive
        // the load of Gameplay.
        if (transform.parent == null) DontDestroyOnLoad(gameObject);
        if (Object.HasStateAuthority)
        {
            CityReady = true;
            PushStats();
        }
        else
        {
            RPC_RequestCity();
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this) Instance = null;
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority) PushStats();
    }

    private void PushStats()
    {
        if (EconomyManager.Instance != null) Funds = EconomyManager.Instance.currentFunds;
        if (PlayerProgressManager.Instance != null) Xp = PlayerProgressManager.Instance.currentXP;
    }

    void Update()
    {
        if (otherLocks.Count > 0 && Coop.EditLocked) otherLocks.Clear(); // the other player is gone: their locks are void

        if (Object == null || !Object.IsValid || Object.HasStateAuthority) return;

        if (pendingCityJson != null && BuildingPlacementController.Instance != null && CitySaveManager.Instance != null)
            ApplyCity(pendingCityJson);
        if (!CityReady) return;

        // Guest: show the host's numbers.
        var economy = EconomyManager.Instance;
        if (economy != null && economy.currentFunds != Funds) economy.SetFunds(Funds);
        var progress = PlayerProgressManager.Instance;
        if (progress != null && progress.currentXP != Xp) progress.RestoreXP(Xp);
    }

    // ---------- Joining: the host sends its whole city ----------

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestCity(RpcInfo info = default)
    {
        string json = JsonUtility.ToJson(CitySaveManager.Instance.BuildSnapshot());
        NetworkSessionManager.Instance.SendCityTo(info.Source, json);
    }

    /// Called by NetworkSessionManager when the host's city arrives on the guest.
    public void OnCityReceived(string json)
    {
        pendingCityJson = json; // applied as soon as the Gameplay scene is there (see Update)
        if (BuildingPlacementController.Instance != null && CitySaveManager.Instance != null) ApplyCity(json);
    }

    private void ApplyCity(string json)
    {
        pendingCityJson = null;
        var data = JsonUtility.FromJson<CitySaveData>(json);
        if (data == null) { Debug.LogWarning("[CityNetworkSync] Couldn't read the host's city."); return; }

        BuildingPlacementController.Instance.ClearAllBuildings();
        CitySaveManager.Instance.blockSaving = true; // the guest's own saved city must stay untouched
        CitySaveManager.Instance.ApplySnapshot(data, false);
        CityReady = true;

        // Changes that reached us while the city was loading.
        foreach (var action in waitingForCity) action();
        waitingForCity.Clear();
    }

    private void WhenCityReady(Action action)
    {
        if (CityReady) action();
        else waitingForCity.Add(action);
    }

    // ---------- Guest -> host: requests ----------

    public void RequestPlace(string buildingId, Vector2Int cell, int rotationSteps)
    {
        if (!CityReady) return;
        RPC_RequestPlace(buildingId, cell.x, cell.y, rotationSteps);
    }

    public void RequestDemolish(int instanceId)
    {
        if (!CityReady) return;
        RPC_RequestDemolish(instanceId);
    }

    public void RequestMove(int instanceId, Vector2Int cell, int rotationSteps)
    {
        if (!CityReady) return;
        RPC_RequestMove(instanceId, cell.x, cell.y, rotationSteps);
    }

    public void RequestUpgrade(int instanceId)
    {
        if (!CityReady) return;
        RPC_RequestUpgrade(instanceId);
    }

    /// Guest: I fixed a city event on my phone - the host pays the reward into the city's funds.
    public void RequestEventReward(string eventName)
    {
        if (!CityReady) return;
        RPC_RequestEventReward(eventName);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestEventReward(string eventName)
    {
        var events = CityEventManager.Instance;
        if (events == null || EconomyManager.Instance == null) return;
        var data = events.eventTypes.Find(e => e != null && e.displayName == eventName);
        if (data == null) return;
        int reward = UnityEngine.Random.Range(data.minFundsReward, data.maxFundsReward + 1);
        EconomyManager.Instance.AddFunds(reward);
        RPC_Notice("Event fixed: +" + reward + " funds");
    }

    // ---------- Move locks ----------

    /// Host: this phone started / finished moving a building.
    public void BroadcastLock(int instanceId, bool locked) => RPC_LockChanged(instanceId, locked);

    /// Guest: ask for the right to move a building (the preview shows at once; the host may say no).
    public void RequestLock(int instanceId) => RPC_RequestLock(instanceId);

    /// Guest: done moving (or gave up).
    public void RequestReleaseLock(int instanceId) => RPC_ReleaseLock(instanceId);

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestLock(int instanceId)
    {
        var placement = BuildingPlacementController.Instance;
        if (Coop.EditLocked || placement.GetInstanceById(instanceId) == null || placement.MovingId == instanceId)
        {
            RPC_LockDenied(instanceId);
            return;
        }
        otherLocks.Add(instanceId);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_ReleaseLock(int instanceId) => otherLocks.Remove(instanceId);

    [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies)]
    private void RPC_LockChanged(int instanceId, bool locked)
    {
        if (!locked) { otherLocks.Remove(instanceId); return; }
        otherLocks.Add(instanceId);
        var placement = BuildingPlacementController.Instance;
        if (placement != null && placement.MovingId == instanceId)
        {
            // We both picked it up at the same moment: the host was first.
            placement.CancelCurrentAction();
            ShowNotice("Your friend is moving this building right now.");
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies)]
    private void RPC_LockDenied(int instanceId)
    {
        var placement = BuildingPlacementController.Instance;
        if (placement == null || placement.MovingId != instanceId) return;
        placement.CancelCurrentAction();
        ShowNotice("Your friend is moving this building right now.");
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestMove(int instanceId, int x, int y, int rotationSteps)
    {
        if (Coop.EditLocked) return;
        if (BuildingPlacementController.Instance.MovingId == instanceId) { RPC_Notice("Your friend is moving this building right now."); return; }
        var cell = new Vector2Int(x, y);
        if (BuildingPlacementController.Instance.HostMove(instanceId, cell, rotationSteps, out string error)) BroadcastMoved(instanceId, cell, rotationSteps);
        else RPC_Notice(error);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestUpgrade(int instanceId)
    {
        if (Coop.EditLocked) return;
        // HostUpgrade -> TryUpgrade broadcasts the start itself
        if (!BuildingPlacementController.Instance.HostUpgrade(instanceId, out string error)) RPC_Notice(error);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestPlace(string buildingId, int x, int y, int rotationSteps)
    {
        var building = BuildingPlacementController.Instance.HostPlace(buildingId, new Vector2Int(x, y), rotationSteps, out string error);
        if (building == null) RPC_Notice(error);
        else BroadcastPlaced(building);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestDemolish(int instanceId)
    {
        if (BuildingPlacementController.Instance.MovingId == instanceId) { RPC_Notice("Your friend is moving this building right now."); return; }
        if (BuildingPlacementController.Instance.HostDemolish(instanceId)) BroadcastDemolished(instanceId);
        else RPC_Notice("That building is already gone.");
    }

    // ---------- Host -> guest: what happened ----------

    public void BroadcastPlaced(BuildingInstance building)
    {
        RPC_Placed(building.data.buildingId, building.gridOrigin.x, building.gridOrigin.y, building.rotationSteps, building.instanceId);
    }

    public void BroadcastDemolished(int instanceId) => RPC_Demolished(instanceId);

    [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies)]
    private void RPC_Placed(string buildingId, int x, int y, int rotationSteps, int instanceId)
    {
        WhenCityReady(() => BuildingPlacementController.Instance.RemotePlaced(buildingId, new Vector2Int(x, y), rotationSteps, instanceId));
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies)]
    private void RPC_Demolished(int instanceId)
    {
        WhenCityReady(() => BuildingPlacementController.Instance.RemoteDemolished(instanceId));
    }

    public void BroadcastMoved(int instanceId, Vector2Int cell, int rotationSteps) => RPC_Moved(instanceId, cell.x, cell.y, rotationSteps);
    public void BroadcastUpgradeStarted(int instanceId) => RPC_UpgradeStarted(instanceId);
    public void BroadcastConstructionDone(int instanceId) => RPC_ConstructionDone(instanceId);

    [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies)]
    private void RPC_Moved(int instanceId, int x, int y, int rotationSteps)
    {
        WhenCityReady(() => BuildingPlacementController.Instance.RemoteMoved(instanceId, new Vector2Int(x, y), rotationSteps));
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies)]
    private void RPC_UpgradeStarted(int instanceId)
    {
        WhenCityReady(() => BuildingPlacementController.Instance.RemoteUpgradeStarted(instanceId));
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies)]
    private void RPC_ConstructionDone(int instanceId)
    {
        WhenCityReady(() => BuildingPlacementController.Instance.RemoteConstructionDone(instanceId));
    }

    /// Shows a message on this phone only.
    public void ShowNotice(string message)
    {
        Notice = message;
        NoticeUntil = Time.unscaledTime + 3f;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies)]
    private void RPC_Notice(string message) => ShowNotice(message);
}
