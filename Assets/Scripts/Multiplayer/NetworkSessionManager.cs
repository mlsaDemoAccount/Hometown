using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum NetSessionState { Idle, Hosting, Connecting, Connected, Failed }

/// Owns the online multiplayer connection (Photon Fusion, Host Mode, Photon Cloud): host a
/// room, join one by its code, leave, and count the players. One player is the host and is the
/// authority; the other mirrors. Needs internet on both phones.
///
/// This is the ONLY script that implements Fusion's runner callbacks (project rule: nothing
/// outside the networking layer reacts to network events). The UI just polls State /
/// StatusMessage / PlayerCount.
public class NetworkSessionManager : MonoBehaviour, INetworkRunnerCallbacks
{
    public static NetworkSessionManager Instance { get; private set; }

    [Tooltip("Networked prefab spawned by the host (the shared test cube). Test scene only.")]
    public NetworkObject cubePrefab;

    [Tooltip("Networked prefab spawned by the host that keeps the two cities in sync (CityNetworkSync). Gameplay scene.")]
    public NetworkObject citySyncPrefab;

    [Tooltip("Let Fusion manage the scene. On for the test scene; OFF in Gameplay, where both phones are already in the scene and it must not be reloaded.")]
    public bool syncScene = true;

    public NetSessionState State { get; private set; } = NetSessionState.Idle;
    public string StatusMessage { get; private set; } = "";
    public JoinCode Session { get; private set; }
    public int PlayerCount => players.Count;
    public bool IsHost => runner != null && runner.IsServer;
    public bool IsBusy => starting; // creating / joining a room right now

    private NetworkRunner runner;
    private readonly HashSet<PlayerRef> players = new HashSet<PlayerRef>();
    private bool starting;
    private bool leaving;

    /// First byte of every reliable message we send (the Fusion key is the same for all of them).
    public const byte MsgCity = 1, MsgStartGame = 2, MsgGuestReady = 3, MsgJoinRequest = 4, MsgJoinReply = 5, MsgProfile = 6;
    public const string GameplaySceneName = "Gameplay";

    /// The host (or, on the guest, the host's message) asked both phones to go to Gameplay.
    /// The menu UI polls this, shows the lobby countdown, and calls ConsumeStartGame().
    public bool StartGameRequested { get; private set; }

    private bool guestReady, citySpawned, readySent;
    private bool fixedRoom; // hosting a city's room: the name must not change if it is taken

    // ---- The other player is away (dropped, or left): wait for them, then lock the city ----
    /// How long we wait for the other player to come back before telling the player they are not coming.
    public const float AwayGraceSeconds = 60f;

    private JoinCode lastClientCode;
    private bool hasClientCode;
    private bool sessionWasFull;     // both players were in this session at some point
    private bool hostAway;           // host side: the guest dropped and has not come back
    private bool reconnectActive;    // our own connection was lost and we are retrying
    private float awaySince;
    private Coroutine reconnectRoutine;

    /// The city can only be played while both players are connected: true while we wait for the
    /// other player (host) or for our own connection to come back (guest).
    public bool Away => reconnectActive || hostAway;

    /// Seconds left of the grace period (0 = the other player did not come back in time).
    public float AwayLeft => Mathf.Max(0f, AwayGraceSeconds - (Time.unscaledTime - awaySince));
    public bool AwayTimedOut => Away && AwayLeft <= 0f;
    /// True when WE lost our own connection (rather than the other player leaving).
    public bool OwnConnectionLost => reconnectActive;

    void Awake()
    {
        // One session manager for the whole app: it is created in the main menu and carries the
        // connection into Gameplay. A copy that comes with a scene (e.g. Gameplay opened directly
        // in the editor) removes itself and leaves the UI on its object alone.
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        if (transform.parent == null) DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (runner == null || SceneManager.GetActiveScene().name != GameplaySceneName) return;

        StartGameRequested = false; // already in Gameplay - a "start" message from a reconnect is moot
        // In Gameplay: the guest tells the host it is ready, then the host spawns the shared city
        // object (so the guest has a scene to receive it in) and the guest asks for the city.
        if (State == NetSessionState.Connected && !readySent)
        {
            readySent = true;
            SendMessageToHost(MsgGuestReady, null);
        }
        if (State == NetSessionState.Hosting && guestReady && !citySpawned && citySyncPrefab != null)
        {
            citySpawned = true;
            StartCoroutine(SpawnCityWhenReady());
        }
    }

    private IEnumerator SpawnCityWhenReady()
    {
        // let the host's own saved city finish loading first - that is what the guest receives
        while (CitySaveManager.Instance == null || BuildingPlacementController.Instance == null) yield return null;
        yield return null;
        yield return null;
        if (runner != null && runner.IsServer) runner.Spawn(citySyncPrefab, Vector3.zero, Quaternion.identity);
    }

    /// Host: tell the other player to load Gameplay now (and do the same here via StartGameRequested).
    public void HostStartGame()
    {
        if (runner == null || !runner.IsServer) return;
        foreach (var p in players)
            if (p != runner.LocalPlayer) runner.SendReliableDataToPlayer(p, ReliableKey.FromInts(1, 0, 0, 0), new byte[] { MsgStartGame });
        StartGameRequested = true;
    }

    public void ConsumeStartGame() => StartGameRequested = false;

    /// Reliable message to the host with a type byte and optional payload.
    private void SendMessageToHost(byte type, byte[] payload)
    {
        var bytes = new byte[1 + (payload?.Length ?? 0)];
        bytes[0] = type;
        if (payload != null) System.Buffer.BlockCopy(payload, 0, bytes, 1, payload.Length);
        runner.SendReliableDataToServer(ReliableKey.FromInts(1, 0, 0, 0), bytes);
    }

    /// Host -> one player: the city, as JSON.
    public void SendCityTo(PlayerRef player, string json)
    {
        var body = System.Text.Encoding.UTF8.GetBytes(json);
        var bytes = new byte[1 + body.Length];
        bytes[0] = MsgCity;
        System.Buffer.BlockCopy(body, 0, bytes, 1, body.Length);
        runner.SendReliableDataToPlayer(player, ReliableKey.FromInts(1, 0, 0, 0), bytes);
    }

    // ---- Invite hand-over: the joining phone introduces itself, the host answers with the city ----
    private string joinRequestJson, joinReplyJson;
    private PlayerRef joinRequestFrom;
    private readonly HashSet<PlayerRef> rejected = new HashSet<PlayerRef>();

    /// Guest -> host: who I am and which invite I used (JSON).
    public void SendJoinRequest(string json)
    {
        if (runner == null || runner.IsServer) return;
        SendMessageToHost(MsgJoinRequest, System.Text.Encoding.UTF8.GetBytes(json));
    }

    /// Host -> that guest: the city (or the reason they can't join), as JSON.
    public void SendJoinReply(PlayerRef player, string json)
    {
        if (runner == null || !runner.IsServer) return;
        var body = System.Text.Encoding.UTF8.GetBytes(json);
        var bytes = new byte[1 + body.Length];
        bytes[0] = MsgJoinReply;
        System.Buffer.BlockCopy(body, 0, bytes, 1, body.Length);
        runner.SendReliableDataToPlayer(player, ReliableKey.FromInts(1, 0, 0, 0), bytes);
    }

    // ---- Profile exchange: each phone tells the other its current name / avatar / gender ----
    private string profileInJson;

    /// Send my profile (JSON) to the other player. Host: to everybody else; guest: to the host.
    public void SendProfile(string json)
    {
        if (runner == null) return;
        var body = System.Text.Encoding.UTF8.GetBytes(json);
        if (runner.IsServer)
        {
            var bytes = new byte[1 + body.Length];
            bytes[0] = MsgProfile;
            System.Buffer.BlockCopy(body, 0, bytes, 1, body.Length);
            foreach (var p in players)
                if (p != runner.LocalPlayer) runner.SendReliableDataToPlayer(p, ReliableKey.FromInts(1, 0, 0, 0), bytes);
        }
        else SendMessageToHost(MsgProfile, body);
    }

    /// The other player's latest profile (once), or null.
    public string TakeProfile()
    {
        var json = profileInJson;
        profileInJson = null;
        return json;
    }

    /// Host: a join request that arrived (once), or false.
    public bool TakeJoinRequest(out string json, out PlayerRef from)
    {
        json = joinRequestJson;
        from = joinRequestFrom;
        joinRequestJson = null;
        return json != null;
    }

    /// Guest: the host's answer (once), or null while it hasn't arrived.
    public string TakeJoinReply()
    {
        var json = joinReplyJson;
        joinReplyJson = null;
        return json;
    }

    /// Host: send a player away. Their leaving is not treated as "the friend dropped".
    public void RejectPlayer(PlayerRef player)
    {
        if (runner == null || !runner.IsServer || player == runner.LocalPlayer) return;
        rejected.Add(player);
        runner.Disconnect(player);
    }

    /// Host: send away everybody who is not me (someone joined the room but never introduced themselves).
    public void RejectOthers()
    {
        if (runner == null || !runner.IsServer) return;
        foreach (var p in new List<PlayerRef>(players))
            if (p != runner.LocalPlayer) RejectPlayer(p);
    }

    private void ResetSessionFlags()
    {
        joinRequestJson = joinReplyJson = profileInJson = null;
        guestReady = citySpawned = readySent = false;
        sessionWasFull = hostAway = false;
        StartGameRequested = false;
    }

    // ---------- Host / Join / Leave ----------

    public bool StartHost()
    {
        if (!CanStart()) return false;
        fixedRoom = false;
        Session = new JoinCode { room = JoinCode.GenerateRoom(), region = "" };
        StartCoroutine(StartRoutine(GameMode.Host, Session));
        return true;
    }

    /// Host a city's own room (its name comes from the city, not from a random 4-digit number).
    public bool StartHost(string room, string region = "")
    {
        if (!CanStart()) return false;
        fixedRoom = true;
        Session = new JoinCode { room = room, region = region ?? "" };
        StartCoroutine(StartRoutine(GameMode.Host, Session));
        return true;
    }

    public bool StartClient(JoinCode session)
    {
        if (!CanStart()) return false;
        Session = session;
        lastClientCode = session;
        hasClientCode = true;
        StartCoroutine(StartRoutine(GameMode.Client, session));
        return true;
    }

    /// The player leaves for good: no reconnecting.
    public void Shutdown()
    {
        if (reconnectRoutine != null) { StopCoroutine(reconnectRoutine); reconnectRoutine = null; }
        reconnectActive = false;
        hasClientCode = false;
        ShutdownRunner();
    }

    private void ShutdownRunner()
    {
        leaving = true;
        if (runner != null)
        {
            runner.Shutdown(); // destroys the runner object too
            runner = null;
        }
        players.Clear();
        ResetSessionFlags();
        State = NetSessionState.Idle;
        StatusMessage = "";
    }

    // ---------- Starting ----------

    private bool CanStart()
    {
        if (starting) return false;

        if (string.IsNullOrEmpty(PhotonAppSettings.Global.AppSettings.AppIdFusion))
        {
            Fail("Photon App ID is missing (Assets/Photon/Fusion/Resources/PhotonAppSettings).");
            return false;
        }
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            Fail("No internet connection. Both players need to be online.");
            return false;
        }
        if (syncScene && SceneManager.GetActiveScene().buildIndex < 0)
        {
            Fail("Add this scene to Build Settings first (File > Build Profiles).");
            return false;
        }
        if (runner != null) ShutdownRunner();
        return true;
    }

    // A typed 4-digit code has no region in it, so the joiner tries its own best region first and
    // then the others until the room is found. A scanned QR carries the region and goes straight there.
    private static readonly string[] FallbackRegions =
        { "eu", "us", "asia", "in", "jp", "usw", "sa", "au", "za", "kr", "cae", "rue" };

    private static List<string> RegionsToTry(JoinCode session)
    {
        var list = new List<string>();
        if (!string.IsNullOrEmpty(session.region)) { list.Add(session.region); return list; }
        list.Add(""); // Photon's best region for this phone
        list.AddRange(FallbackRegions);
        return list;
    }

    private IEnumerator StartRoutine(GameMode mode, JoinCode session)
    {
        starting = true;
        State = mode == GameMode.Host ? NetSessionState.Idle : NetSessionState.Connecting;

        var regions = mode == GameMode.Client ? RegionsToTry(session) : new List<string> { session.region ?? "" };
        int hostRetries = 0;
        bool ok = false;
        string failText = "Couldn't connect.";

        for (int i = 0; i < regions.Count; i++)
        {
            leaving = false;
            players.Clear();
            ResetSessionFlags();
            StatusMessage = mode == GameMode.Host ? "Creating room..."
                : i == 0 ? "Joining room " + session.room + "..."
                : "Looking for room " + session.room + " (" + i + "/" + (regions.Count - 1) + ")...";

            var runnerObject = new GameObject("NetworkRunner");
            DontDestroyOnLoad(runnerObject); // the connection outlives the menu -> Gameplay scene change
            runner = runnerObject.AddComponent<NetworkRunner>();
            runner.AddCallbacks(this);

            var args = new StartGameArgs
            {
                GameMode = mode,
                SessionName = session.room,
                PlayerCount = 2,        // a city is shared by exactly two players
                IsVisible = false       // joinable only by code, never listed publicly
            };
            if (syncScene)
            {
                var sceneInfo = new NetworkSceneInfo();
                sceneInfo.AddSceneRef(SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex));
                args.Scene = sceneInfo;
            }

            if (!string.IsNullOrEmpty(regions[i]))
            {
                var settings = new FusionAppSettings();
                PhotonAppSettings.Global.AppSettings.CopyTo(settings);
                settings.FixedRegion = regions[i];
                args.CustomPhotonAppSettings = settings;
            }

            Task<StartGameResult> task = runner.StartGame(args);
            while (!task.IsCompleted) yield return null;

            if (!task.IsFaulted && !task.IsCanceled && task.Result.Ok) { ok = true; break; }

            bool faulted = task.IsFaulted || task.IsCanceled;
            var reason = faulted ? ShutdownReason.Ok : task.Result.ShutdownReason;
            failText = faulted ? "Couldn't connect. Check your internet connection." : DescribeReason(reason);
            CleanUpRunner();

            if (mode == GameMode.Host && !fixedRoom && reason == ShutdownReason.GameIdAlreadyExists && hostRetries++ < 5)
            {
                session = new JoinCode { room = JoinCode.GenerateRoom(), region = "" }; // that number is taken - pick another
                Session = session;
                i--;
                continue;
            }
            if (mode == GameMode.Client && reason == ShutdownReason.GameNotFound) continue; // maybe in another region
            break;
        }
        starting = false;

        if (!ok)
        {
            Fail(failText);
            yield break;
        }

        if (mode == GameMode.Host)
        {
            // Remember where the room really is, so the QR points the other phone to it.
            Session = new JoinCode { room = session.room, region = JoinCode.CleanRegion(runner.SessionInfo.Region) };
            if (cubePrefab != null) runner.Spawn(cubePrefab, new Vector3(0f, 0.5f, 0f), Quaternion.identity);
            State = NetSessionState.Hosting;
            StatusMessage = "Waiting for the other player...";
        }
        else
        {
            // Remember where the room really is: a later reconnect (or rejoin) goes straight there.
            Session = lastClientCode = new JoinCode { room = session.room, region = JoinCode.CleanRegion(runner.SessionInfo.Region) };
            State = NetSessionState.Connected;
            StatusMessage = "Connected to host.";
        }
    }

    private void CleanUpRunner()
    {
        leaving = true; // our own cleanup - don't treat the shutdown as a surprise
        if (runner != null)
        {
            if (runner.gameObject != null) Destroy(runner.gameObject);
            runner = null;
        }
        players.Clear();
    }

    private void Fail(string message)
    {
        State = NetSessionState.Failed;
        StatusMessage = message;
        Debug.LogWarning("[NetworkSessionManager] " + message);
    }

    private static string DescribeReason(ShutdownReason reason)
    {
        switch (reason)
        {
            case ShutdownReason.GameNotFound: return "Room not found. Check the code, and that the host is still waiting.";
            case ShutdownReason.GameIsFull: return "This room already has two players.";
            case ShutdownReason.GameClosed: return "This room is closed.";
            case ShutdownReason.GameIdAlreadyExists: return "Room code already in use. Try hosting again.";
            case ShutdownReason.MaxCcuReached: return "The game servers are full right now. Try again soon.";
            case ShutdownReason.InvalidRegion: return "Couldn't reach that region. Check the code.";
            case ShutdownReason.ConnectionTimeout:
            case ShutdownReason.OperationTimeout:
            case ShutdownReason.PhotonCloudTimeout: return "Connection timed out. Check your internet connection.";
            default: return "Couldn't connect (" + reason + ").";
        }
    }

    // ---------- Fusion callbacks (the only place they are handled) ----------

    public void OnPlayerJoined(NetworkRunner r, PlayerRef player)
    {
        players.Add(player);
        if (players.Count >= 2) { sessionWasFull = true; hostAway = false; }
        if (r.IsServer && player != r.LocalPlayer)
        {
            StatusMessage = "Player joined!";
            // Somebody coming back to a game that is already running in Gameplay (e.g. from the menu
            // after restarting the app): tell them to load it. One that is already there ignores it.
            if (citySpawned && SceneManager.GetActiveScene().name == GameplaySceneName)
                r.SendReliableDataToPlayer(player, ReliableKey.FromInts(1, 0, 0, 0), new byte[] { MsgStartGame });
        }
    }

    public void OnPlayerLeft(NetworkRunner r, PlayerRef player)
    {
        players.Remove(player);
        if (rejected.Remove(player))
        {
            // a stranger we sent away - the room is still waiting for the real friend
            sessionWasFull = hostAway = false;
            StatusMessage = "Waiting for the other player...";
            return;
        }
        if (leaving || !r.IsServer) return;
        StatusMessage = "The other player left.";
        if (sessionWasFull && !hostAway) { hostAway = true; awaySince = Time.unscaledTime; } // start the wait
    }

    /// Our own connection is gone (not by our choice): keep trying to get back into the same room.
    /// Returns false if this kind of session can't be re-entered (test scene).
    private bool TryBeginReconnect(bool asHost)
    {
        if (SceneManager.GetActiveScene().name != GameplaySceneName) return false; // only a running game is worth waiting for
        if (asHost ? !fixedRoom : !hasClientCode) return false;
        if (reconnectActive) return true;
        reconnectActive = true;
        hostAway = false;
        awaySince = Time.unscaledTime;
        var session = asHost ? Session : lastClientCode;
        CleanUpRunner();
        State = NetSessionState.Connecting;
        StatusMessage = "Connection lost. Reconnecting...";
        reconnectRoutine = StartCoroutine(ReconnectRoutine(asHost, session));
        return true;
    }

    private IEnumerator ReconnectRoutine(bool asHost, JoinCode session)
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(3f);
            if (Application.internetReachability == NetworkReachability.NotReachable) continue;

            bool started = asHost ? StartHost(session.room, session.region) : StartClient(session);
            if (!started) continue;
            while (starting) yield return null;

            if (asHost ? State == NetSessionState.Hosting : State == NetSessionState.Connected) break;
        }
        reconnectActive = false;
        reconnectRoutine = null;
    }

    public void OnShutdown(NetworkRunner r, ShutdownReason reason)
    {
        if (leaving || r != runner) return; // we asked for it
        bool wasLive = State == NetSessionState.Hosting || State == NetSessionState.Connected;
        if (wasLive && TryBeginReconnect(State == NetSessionState.Hosting)) return;
        runner = null;
        players.Clear();
        Fail(wasLive ? "The session ended." : DescribeReason(reason));
    }

    public void OnDisconnectedFromServer(NetworkRunner r, NetDisconnectReason reason)
    {
        Debug.Log("[NetworkSessionManager] Disconnected: " + reason);
        if (!leaving && r == runner && !r.IsServer && State == NetSessionState.Connected) TryBeginReconnect(false);
    }

    public void OnConnectFailed(NetworkRunner r, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        Debug.LogWarning("[NetworkSessionManager] Connect failed: " + reason);
    }

    // Unused for now - the game sends requests through RPCs (see SyncedCube), not per-tick input.
    public void OnInput(NetworkRunner r, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner r, PlayerRef player, NetworkInput input) { }
    public void OnConnectedToServer(NetworkRunner r) { }
    public void OnConnectRequest(NetworkRunner r, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnUserSimulationMessage(NetworkRunner r, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner r, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner r, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner r, HostMigrationToken hostMigrationToken) { }
    // The host's whole city, sent to a joining player (too big for an RPC).
    public void OnReliableDataReceived(NetworkRunner r, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data)
    {
        if (data.Length == 0) return;
        switch (data[0])
        {
            case MsgCity:
                if (CityNetworkSync.Instance != null)
                    CityNetworkSync.Instance.OnCityReceived(System.Text.Encoding.UTF8.GetString(data.Slice(1)));
                break;
            case MsgStartGame:
                StartGameRequested = true; // the menu UI shows the lobby countdown, then loads Gameplay
                break;
            case MsgGuestReady:
                guestReady = true;
                break;
            case MsgJoinRequest:
                joinRequestJson = System.Text.Encoding.UTF8.GetString(data.Slice(1));
                joinRequestFrom = player;
                break;
            case MsgProfile:
                profileInJson = System.Text.Encoding.UTF8.GetString(data.Slice(1));
                break;
            case MsgJoinReply:
                joinReplyJson = System.Text.Encoding.UTF8.GetString(data.Slice(1));
                break;
        }
    }
    public void OnReliableDataProgress(NetworkRunner r, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner r) { }
    public void OnSceneLoadStart(NetworkRunner r) { }
    public void OnObjectEnterAOI(NetworkRunner r, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner r, NetworkObject obj, PlayerRef player) { }
}
