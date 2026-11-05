using System.Collections;
using UnityEngine;

/// Gets two players into the same city: creates a city and its invite, accepts an invite, or opens
/// an existing city - then connects them (Photon room named after the city) and later saves the
/// city's snapshots to the backend. Lives for the whole app, so the chosen city survives the
/// MainMenu -> Gameplay scene change. The UI only polls Busy / Failed / Status / Invite.
///
/// Either player can be the host: if the partner already has a live room for this city we join it,
/// otherwise we host (and tell the backend; if both try at once, the backend's first-come answer
/// decides and the other one joins).
public class CityLauncher : MonoBehaviour
{
    private const float UploadSeconds = 5f;

    private static CityLauncher instance;

    public static CityLauncher Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("CityLauncher");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<CityLauncher>();
            }
            return instance;
        }
    }

    /// A city has been chosen for this session (Gameplay then loads that city instead of a local file).
    public static bool IsCityMode => instance != null && instance.Active;

    public bool Active { get; private set; }
    public string CityId { get; private set; }

    /// Host: the city's latest saved snapshot (JSON), downloaded before Gameplay starts. Null = a brand-new city.
    public string StartSnapshotJson { get; private set; }
    public int SnapshotVersion { get; private set; }

    public bool Busy { get; private set; }
    public bool Failed { get; private set; }
    public string Status { get; private set; } = "";

    /// Set while we wait for a new friend: the code / link to show (QR + digits).
    public InviteDto Invite { get; private set; }

    /// Name of the partner we are waiting for (existing city).
    public string WaitingFor { get; private set; } = "";

    /// The friend of the city being played (name, avatar, gender) - kept fresh while the city is active.
    public ParticipantDto Partner { get; private set; }
    private CityCall<CityDto> partnerCall;
    private float nextPartnerCheck;

    /// Ask the backend for the friend's profile again right away (e.g. when the lobby opens).
    public void RefreshPartner() => nextPartnerCheck = 0f;

    private Coroutine routine;
    private bool stepOk;
    private string publishedCity;
    private bool dirty;
    private string pendingJson;
    private float nextUpload;
    private CityCall<SaveSnapshotResult> uploading;
    private CityCall<SessionDto> republish;

    private static string RoomFor(string cityId) => "hc" + cityId;

    /// The room a host opens while waiting for a new friend: named after the invite code, so the
    /// friend can connect with just the code - no backend lookup, works between any two phones.
    private static string InviteRoom(string inviteCode) => "hi" + inviteCode;

    private string hostRoom;          // the room we host right now (to announce it again after a reconnect)
    private bool handlingJoin;
    private float playWaitUntil;      // rejoining a city: give up waiting for the friend at this time (0 = not waiting)
    private const float RejoinSeconds = 60f;
    private const string RejoinFailed = "Unable to rejoin - try again.";

    private static string RegionKey(string cityId) => "cityRegion_" + cityId;
    private static void RememberRegion(string cityId, NetworkSessionManager net)
    {
        if (net != null && net.Session.region != null && net.Session.region.Length > 0)
            PlayerPrefs.SetString(RegionKey(cityId), net.Session.region);
    }
    private float guestSeenAt = -1f;
    private const float IntroSeconds = 15f;

    /// Host: the invite is out and nobody has been checked in yet - the lobby must not start the game.
    public bool AwaitingGuest { get; private set; }

    // ---------- Entry points (called by the menu UI) ----------

    /// Start a city with a new friend (existingCityId == null) or re-open the invite of a city that
    /// still waits for its second player. Shows an invite and waits in the city's room.
    public void BeginNewCity(string existingCityId = null)
    {
        StartFlow(NewCityRoutine(existingCityId));
    }

    /// Join a friend's city with the invite code they showed (typed, or read from their QR).
    public void BeginJoinInvite(string inviteCode)
    {
        StartFlow(JoinInviteRoutine(inviteCode));
    }

    /// Open an existing city (partner already known): join their room if they are in it, else host.
    public void BeginPlay(string cityId)
    {
        StartFlow(PlayRoutine(cityId));
    }

    /// Back out: drop the connection and forget the choice.
    public void Cancel()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        NetworkSessionManager.Instance?.Shutdown();
        Busy = Failed = Active = AwaitingGuest = false;
        handlingJoin = false;
        guestSeenAt = -1f;
        playWaitUntil = 0f;
        Status = "";
        Invite = null;
        WaitingFor = "";
        Partner = null;
        partnerCall = null;
    }

    private void StartFlow(IEnumerator flow)
    {
        Cancel();
        Busy = true;
        Status = "Please wait...";
        StartSnapshotJson = null;
        SnapshotVersion = 0;
        routine = StartCoroutine(flow);
    }

    // ---------- Flows ----------

    private IEnumerator NewCityRoutine(string existingCityId)
    {
        string cityId = existingCityId;
        if (cityId == null)
        {
            Status = "Creating your city...";
            var create = CityApi.Backend.CreateCity("Our city");
            yield return Wait(create);
            if (!create.ok) { Fail(create.error); yield break; }
            cityId = create.result.cityId;
        }

        Status = "Creating the invite...";
        var invite = CityApi.Backend.CreateInvite(cityId);
        yield return Wait(invite);
        if (!invite.ok) { Fail(invite.error); yield break; }
        Invite = invite.result;

        Active = true;
        CityId = cityId;
        WaitingFor = "your friend";
        AwaitingGuest = true;
        yield return HostCity(cityId, InviteRoom(invite.result.inviteCode));
    }

    private IEnumerator JoinInviteRoutine(string inviteCode)
    {
        inviteCode = (inviteCode ?? "").Trim();
        Status = "Checking the invite...";
        var accept = CityApi.Backend.AcceptInvite(inviteCode);
        yield return Wait(accept);

        var peerBackend = CityApi.Backend as IPeerCityBackend;
        string room;
        bool viaPhoton = false;
        if (accept.ok)
        {
            if (string.IsNullOrEmpty(accept.result.photonRoom))
            {
                Fail("You're in! But your friend isn't waiting in the game right now. Ask them to open the invite screen, then pick this city from your list.");
                yield break;
            }
            Active = true;
            CityId = accept.result.city.cityId;
            var friend = accept.result.city.Partner(CityApi.Backend.MyUserId);
            WaitingFor = friend != null ? friend.firstName : "your friend";
            Partner = friend;
            room = accept.result.photonRoom;
        }
        else if (accept.notFound && peerBackend != null)
        {
            // This device doesn't know the invite (the invite lives on the friend's phone). The code IS
            // the friend's Photon room, so connect straight to it and let the friend hand the city over.
            viaPhoton = true;
            WaitingFor = "your friend";
            room = InviteRoom(inviteCode);
        }
        else { Fail(accept.error); yield break; }

        yield return JoinSession(room);
        if (!stepOk)
        {
            if (viaPhoton && Status.StartsWith("Room not found"))
                Status = "That invite code isn't valid, or your friend isn't waiting any more.";
            yield break;
        }

        // Introduce ourselves and wait for the host to confirm and send the city.
        var net = NetworkSessionManager.Instance;
        var me = peerBackend != null ? peerBackend.MyProfile() : new ParticipantDto { userId = CityApi.Backend.MyUserId, firstName = "Player" };
        net.SendJoinRequest(JsonUtility.ToJson(new JoinRequest { inviteCode = inviteCode, me = me }));
        Status = "Waiting for " + WaitingFor + "...";

        string replyJson = null;
        float giveUp = Time.unscaledTime + IntroSeconds;
        while (replyJson == null && Time.unscaledTime < giveUp && net.State == NetSessionState.Connected)
        {
            replyJson = net.TakeJoinReply();
            if (replyJson == null) yield return null;
        }
        var reply = replyJson != null ? JsonUtility.FromJson<JoinReply>(replyJson) : null;
        if (reply == null || !reply.ok || reply.city == null)
        {
            net.Shutdown();
            Fail(reply != null && !string.IsNullOrEmpty(reply.error) ? reply.error : "Your friend didn't answer. Ask them to try again.");
            yield break;
        }

        if (peerBackend != null)
        {
            var adopted = peerBackend.AdoptCity(reply);
            yield return Wait(adopted);
            if (!adopted.ok) { net.Shutdown(); Fail(adopted.error); yield break; }
        }
        Active = true;
        CityId = reply.city.cityId;
        var host = reply.city.Partner(CityApi.Backend.MyUserId);
        if (host != null) { Partner = host; WaitingFor = host.firstName; }
        Busy = false;
    }

    private IEnumerator PlayRoutine(string cityId)
    {
        Status = "Checking your city...";
        var city = CityApi.Backend.GetCity(cityId);
        yield return Wait(city);
        if (!city.ok) { Fail(city.error); yield break; }
        var partner = city.result.Partner(CityApi.Backend.MyUserId);
        WaitingFor = partner != null ? partner.firstName : "your friend";
        Partner = partner;

        Active = true;
        CityId = cityId;

        // Is the partner already hosting this city? Then join them.
        var session = CityApi.Backend.GetSession(cityId);
        yield return Wait(session);
        if (session.ok && session.result.hostingNow && session.result.hostUserId != CityApi.Backend.MyUserId)
        {
            yield return JoinSession(session.result.photonRoom);
            if (stepOk) { RememberRegion(cityId, NetworkSessionManager.Instance); Busy = false; yield break; }
            NetworkSessionManager.Instance?.Shutdown(); // their room is gone (stale) - host it ourselves
        }

        // The backend can't see the friend's phone (they may be on another device), but Photon can:
        // their room is named after the city. Look for it first - only host if it isn't there.
        string room = RoomFor(cityId);
        string remembered = PlayerPrefs.GetString(RegionKey(cityId), "");
        if (remembered.Length > 0) yield return TryJoinRoom(room, remembered);
        if (!stepOk) yield return TryJoinRoom(room, ""); // "" = search every region
        if (stepOk)
        {
            RememberRegion(cityId, NetworkSessionManager.Instance);
            Busy = false;
            yield break;
        }
        yield return HostCity(cityId, room, true);
    }

    /// Try to connect to a room. Sets stepOk; unlike JoinSession it does not treat "not found" as an error.
    private IEnumerator TryJoinRoom(string room, string region)
    {
        stepOk = false;
        var net = NetworkSessionManager.Instance;
        if (net == null) yield break;
        Status = "Looking for " + WaitingFor + "...";
        if (!net.StartClient(new JoinCode { room = room, region = region })) yield break;
        while (net.IsBusy) yield return null;
        stepOk = net.State == NetSessionState.Connected;
    }

    /// Host this city's room, announce it, and load the city's latest snapshot.
    private IEnumerator HostCity(string cityId, string room, bool rejoining = false)
    {
        var net = NetworkSessionManager.Instance;
        if (net == null) { Fail("Multiplayer isn't available in this scene."); yield break; }

        Status = "Opening the room...";
        hostRoom = room;
        if (!net.StartHost(room)) { Fail(net.StatusMessage); yield break; }
        while (net.IsBusy) yield return null;
        if (net.State != NetSessionState.Hosting)
        {
            if (rejoining && net.StatusMessage.StartsWith("Room code already in use"))
            {
                // the friend opened the room a moment ago - join them instead
                yield return TryJoinRoom(room, "");
                if (stepOk) { RememberRegion(cityId, net); Busy = false; yield break; }
                Fail(RejoinFailed);
                yield break;
            }
            Fail(net.StatusMessage);
            yield break;
        }

        var publish = CityApi.Backend.PublishSession(cityId, net.Session.room + "|" + net.Session.region);
        yield return Wait(publish);
        if (!publish.ok) { net.Shutdown(); Fail(publish.error); yield break; }

        if (publish.result.hostUserId != CityApi.Backend.MyUserId)
        {
            // Both of us tried to host at the same moment and the partner was first: join them.
            net.Shutdown();
            yield return JoinSession(publish.result.photonRoom);
            if (stepOk) Busy = false;
            yield break;
        }
        publishedCity = cityId;
        RememberRegion(cityId, net);

        Status = "Loading the city...";
        var snapshot = CityApi.Backend.GetSnapshot(cityId);
        yield return Wait(snapshot);
        if (!snapshot.ok) { net.Shutdown(); Fail(snapshot.error); yield break; }
        if (snapshot.result != null)
        {
            StartSnapshotJson = snapshot.result.data;
            SnapshotVersion = snapshot.result.version;
        }

        Busy = false;
        Status = "Waiting for " + WaitingFor + "...";
        if (rejoining) playWaitUntil = Time.unscaledTime + RejoinSeconds;
    }

    /// Connect to a partner's room ("room|region"). Sets stepOk; on failure also calls Fail().
    private IEnumerator JoinSession(string photonRoom)
    {
        stepOk = false;
        var net = NetworkSessionManager.Instance;
        if (net == null) { Fail("Multiplayer isn't available in this scene."); yield break; }

        string[] parts = (photonRoom ?? "").Split('|');
        var code = new JoinCode { room = parts[0], region = parts.Length > 1 ? parts[1] : "" };

        Status = "Joining " + WaitingFor + "...";
        if (!net.StartClient(code)) { Fail(net.StatusMessage); yield break; }
        while (net.IsBusy) yield return null;
        if (net.State != NetSessionState.Connected) { Fail(net.StatusMessage); yield break; }
        stepOk = true;
    }

    private static IEnumerator Wait<T>(CityCall<T> call)
    {
        while (!call.isDone) yield return null;
    }

    private void Fail(string message)
    {
        Busy = false;
        Failed = true;
        Active = false;
        AwaitingGuest = false;
        Status = message;
        Debug.LogWarning("[CityLauncher] " + message);
    }

    // ---------- Saving the city (host only) ----------

    /// Called by CitySaveManager after each local save with the city as JSON. Uploaded to the
    /// backend at most every few seconds, and once more when the session ends.
    public static void NotifyChanged(string json)
    {
        if (instance == null || !instance.Active) return;
        instance.pendingJson = json;
        instance.dirty = true;
    }

    void Update()
    {
        UpdatePartner();
        var net = NetworkSessionManager.Instance;
        bool hosting = net != null && net.State == NetSessionState.Hosting;
        if (net != null) ExchangeProfiles(net);

        if (AwaitingGuest && hosting) HostWelcomeGuest(net);

        // Reopened a city and the friend never showed up: say so instead of waiting forever.
        if (playWaitUntil > 0f)
        {
            if (!hosting || net.PlayerCount >= 2) playWaitUntil = 0f;
            else if (Time.unscaledTime > playWaitUntil)
            {
                playWaitUntil = 0f;
                net.Shutdown();
                Fail(RejoinFailed);
            }
        }

        // The room we announced is gone: save one last time, then tell the backend.
        if (publishedCity != null && !hosting)
        {
            if (dirty) UploadNow();
            CityApi.Backend.EndSession(publishedCity);
            publishedCity = null;
        }

        if (uploading != null && uploading.isDone)
        {
            if (uploading.ok)
            {
                SnapshotVersion = uploading.result.version;
                if (uploading.result.conflict) dirty = true; // someone saved first - try again on top of theirs
            }
            else Debug.LogWarning("[CityLauncher] Saving the city failed: " + uploading.error);
            uploading = null;
        }

        if (dirty && uploading == null && hosting && Time.unscaledTime >= nextUpload) UploadNow();

        // We lost our room and got it back (see NetworkSessionManager's reconnect): announce it again.
        if (hosting && Active && !Busy && publishedCity == null && republish == null && net.Session.room == hostRoom)
            republish = CityApi.Backend.PublishSession(CityId, net.Session.room + "|" + net.Session.region);
        if (republish != null && republish.isDone)
        {
            if (republish.ok && republish.result.hostUserId == CityApi.Backend.MyUserId) publishedCity = CityId;
            republish = null;
        }
    }

    // ---------- Keeping the friend's name / avatar current ----------

    private float nextProfileSend;
    private const float ProfileSeconds = 20f;

    /// While two phones are connected they swap their profiles now and then, so a name or avatar the
    /// friend changed shows up here without any backend.
    private void ExchangeProfiles(NetworkSessionManager net)
    {
        var peerBackend = CityApi.Backend as IPeerCityBackend;

        string json = net.TakeProfile();
        if (json != null && Active && peerBackend != null && !string.IsNullOrEmpty(CityId))
        {
            var p = JsonUtility.FromJson<ParticipantDto>(json);
            if (p != null && peerBackend.UpdatePeer(CityId, p))
            {
                Partner = p;
                WaitingFor = p.firstName;
            }
        }

        bool linked = net.State == NetSessionState.Connected || (net.State == NetSessionState.Hosting && net.PlayerCount >= 2);
        if (!linked) { nextProfileSend = 0f; return; }
        if (peerBackend == null || Time.unscaledTime < nextProfileSend) return;
        nextProfileSend = Time.unscaledTime + ProfileSeconds;
        net.SendProfile(JsonUtility.ToJson(peerBackend.MyProfile()));
    }

    // ---------- Host: a friend joined with the invite code ----------

    private void HostWelcomeGuest(NetworkSessionManager net)
    {
        if (!handlingJoin && net.TakeJoinRequest(out string json, out var from))
        {
            handlingJoin = true;
            StartCoroutine(HandleJoinRequest(json, from));
        }

        // Somebody is in the room but never introduced themselves: send them away after a while.
        if (net.PlayerCount < 2 || handlingJoin) { guestSeenAt = -1f; return; }
        if (guestSeenAt < 0f) guestSeenAt = Time.unscaledTime;
        else if (Time.unscaledTime - guestSeenAt > IntroSeconds)
        {
            net.RejectOthers();
            guestSeenAt = -1f;
        }
    }

    private IEnumerator HandleJoinRequest(string json, Fusion.PlayerRef from)
    {
        var net = NetworkSessionManager.Instance;
        var request = JsonUtility.FromJson<JoinRequest>(json);
        var reply = new JoinReply();

        if (request == null || request.me == null || string.IsNullOrEmpty(request.me.userId))
            reply.error = "Couldn't read the join request.";
        else if (Invite != null && request.inviteCode != Invite.inviteCode)
            reply.error = "That invite code isn't valid.";
        else
        {
            CityCall<CityDto> city;
            if (CityApi.Backend is IPeerCityBackend peerBackend) city = peerBackend.AcceptPeer(CityId, request.me);
            else city = CityApi.Backend.GetCity(CityId); // a real backend already added them when they accepted the invite
            yield return Wait(city);

            if (!city.ok) reply.error = city.error;
            else if (System.Array.Find(city.result.participants, p => p.userId == request.me.userId) == null)
                reply.error = "You are not part of this city.";
            else
            {
                if (city.result.cityId != CityId)
                {
                    // we already had a city together: play that one, the new empty one is gone
                    CityApi.Backend.EndSession(CityId);
                    CityId = city.result.cityId;
                    publishedCity = null; // announced again in Update
                    StartSnapshotJson = null;
                    SnapshotVersion = 0;
                }
                var snapshot = CityApi.Backend.GetSnapshot(CityId);
                yield return Wait(snapshot);
                if (snapshot.ok && snapshot.result != null)
                {
                    StartSnapshotJson = snapshot.result.data;
                    SnapshotVersion = snapshot.result.version;
                    reply.snapshotData = snapshot.result.data;
                    reply.snapshotVersion = snapshot.result.version;
                    reply.funds = snapshot.result.funds; reply.xp = snapshot.result.xp; reply.level = snapshot.result.level;
                }
                reply.ok = true;
                reply.city = city.result;
                var guest = city.result.Partner(CityApi.Backend.MyUserId);
                if (guest != null) { Partner = guest; WaitingFor = guest.firstName; }
            }
        }

        if (net != null) net.SendJoinReply(from, JsonUtility.ToJson(reply));
        if (reply.ok) AwaitingGuest = false;
        else
        {
            Debug.LogWarning("[CityLauncher] Join refused: " + reply.error);
            yield return new WaitForSecondsRealtime(0.5f); // let the answer reach them first
            if (net != null) net.RejectPlayer(from);
        }
        handlingJoin = false;
    }

    // The friend's profile: asked again every few seconds until known, then now and then.
    private void UpdatePartner()
    {
        if (!Active || string.IsNullOrEmpty(CityId)) return;
        if (partnerCall != null)
        {
            if (!partnerCall.isDone) return;
            if (partnerCall.ok)
            {
                var found = partnerCall.result.Partner(CityApi.Backend.MyUserId);
                if (found != null) { Partner = found; WaitingFor = found.firstName; }
            }
            partnerCall = null;
            return;
        }
        if (Time.unscaledTime < nextPartnerCheck) return;
        nextPartnerCheck = Time.unscaledTime + (Partner == null ? 3f : 30f);
        partnerCall = CityApi.Backend.GetCity(CityId);
    }

    private void UploadNow()
    {
        if (!Active || string.IsNullOrEmpty(pendingJson) || uploading != null) return;
        // Funds / XP / level belong to the city: sent next to the snapshot so the server can list them.
        var save = JsonUtility.FromJson<CitySaveData>(pendingJson);
        int level = PlayerProgressManager.Instance != null ? PlayerProgressManager.Instance.currentLevel : 1;
        uploading = CityApi.Backend.PutSnapshot(CityId, SnapshotVersion, save != null ? save.funds : 0, save != null ? save.xp : 0, level, pendingJson);
        dirty = false;
        nextUpload = Time.unscaledTime + UploadSeconds;
    }

    void OnApplicationPause(bool paused)
    {
        if (paused && dirty && publishedCity != null) UploadNow();
    }

    void OnApplicationQuit()
    {
        if (dirty && publishedCity != null) UploadNow();
        if (publishedCity != null) CityApi.Backend.EndSession(publishedCity);
    }
}
