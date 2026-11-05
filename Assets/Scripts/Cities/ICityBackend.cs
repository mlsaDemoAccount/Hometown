/// Everything the game needs from the cities backend. MockCityBackend implements it on this
/// device; a REST version (RestCityBackend, built from Docs/CityBackendSpec.md) replaces it
/// later without touching any game code - the game only ever talks to CityApi.
public interface ICityBackend
{
    /// The signed-in player (id + first name), used to know "which participant am I".
    string MyUserId { get; }

    CityCall<CityDto> CreateCity(string name);
    CityCall<CityListDto> GetMyCities();
    CityCall<CityDto> GetCity(string cityId);

    /// Either player of the city can rename it; both see the new name.
    CityCall<CityDto> RenameCity(string cityId, string name);

    /// result == null when the city has no snapshot yet (brand-new city).
    CityCall<SnapshotDto> GetSnapshot(string cityId);
    CityCall<SaveSnapshotResult> PutSnapshot(string cityId, int baseVersion, int funds, int xp, int level, string data);

    CityCall<InviteDto> CreateInvite(string cityId);
    CityCall<AcceptInviteResult> AcceptInvite(string inviteCode);

    CityCall<SessionDto> GetSession(string cityId);

    /// Host announces its live room ("room|region"). First one wins: if the other player already
    /// has a live session, nothing is changed and THEIR session is returned (join it instead).
    CityCall<SessionDto> PublishSession(string cityId, string photonRoom);
    CityCall<bool> EndSession(string cityId);

    /// "My app is open right now" - sent every ~30 s.
    CityCall<bool> Heartbeat();
}

/// Optional extra for a backend that lives on this device only (MockCityBackend): the friend's phone
/// has no copy of our "server", so the host hands the city over directly when the friend joins its
/// Photon room. A real backend stores the city for both players and does not need this.
public interface IPeerCityBackend
{
    /// My own profile (id, name, avatar, gender), sent to the host when joining.
    ParticipantDto MyProfile();

    /// Host: the friend joined my invite room - add them to the city. Returns the city they now share
    /// (an older one if the two already had a city together - then the empty new one is dropped).
    CityCall<CityDto> AcceptPeer(string cityId, ParticipantDto peer);

    /// The friend (connected to me right now) sent their current profile: keep it, so a changed name /
    /// avatar shows up here too. Ignored (false) unless they are a participant of that city.
    bool UpdatePeer(string cityId, ParticipantDto peer);

    /// Joiner: store the city the host handed over, so it appears in my list like any other.
    CityCall<CityDto> AdoptCity(JoinReply reply);
}
