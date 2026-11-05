using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// Stand-in for the real backend, until it exists. Keeps the whole "server" in one JSON file in
/// Application.persistentDataPath. Every ParrelSync clone uses that same folder, so two editors
/// signed in as different players see each other's cities, invites and online state - just like
/// they would with a real server. Answers immediately (the real one will take a moment).
///
/// Mirrors the rules the real backend must follow: 2 participants per city, one city per pair,
/// optimistic snapshot versions, single-use invites that expire, presence = heartbeat < 75 s old.
public class MockCityBackend : ICityBackend, IPeerCityBackend
{
    private const int OnlineSeconds = 75;
    private const int InviteHours = 24;
    private const string InviteAlphabet = "0123456789"; // digits only - easy to type

    [Serializable] private class UserRow { public string userId, firstName, lastSeenUtc; public int playerImageIndex; public int gender = -1; }
    [Serializable] private class SnapshotRow { public int version; public string savedAtUtc, data; }
    [Serializable] private class CityRow
    {
        public string cityId, name, lastPlayedUtc, hostUserId, photonRoom;
        public List<string> participantIds = new List<string>();
        public SnapshotRow snapshot; // null = none yet
    }
    [Serializable] private class InviteRow { public string inviteCode, cityId, expiresUtc; }
    [Serializable] private class Db
    {
        public List<UserRow> users = new List<UserRow>();
        public List<CityRow> cities = new List<CityRow>();
        public List<InviteRow> invites = new List<InviteRow>();
    }

    private static string DbPath => Path.Combine(Application.persistentDataPath, "mock_cities_server.json");

    private Db Load()
    {
        try
        {
            if (File.Exists(DbPath)) return JsonUtility.FromJson<Db>(File.ReadAllText(DbPath)) ?? new Db();
        }
        catch (Exception e) { Debug.LogWarning("[MockCityBackend] Couldn't read the mock server file: " + e.Message); }
        return new Db();
    }

    private void Save(Db db) => File.WriteAllText(DbPath, JsonUtility.ToJson(db));

    // ---------- Who am I ----------

    public string MyUserId
    {
        get
        {
            var u = UserDetails.Instance;
            if (u != null && u.isLoggedIn && !string.IsNullOrEmpty(u.userId)) return u.userId;
            // Not signed in (editor testing): one stable fake player per project folder, so two
            // ParrelSync clones are two different players.
            return "guest_" + (uint)Application.dataPath.GetHashCode();
        }
    }

    private UserRow Me(Db db)
    {
        string id = MyUserId;
        var row = db.users.Find(x => x.userId == id);
        if (row == null)
        {
            row = new UserRow { userId = id };
            db.users.Add(row);
        }
        var u = UserDetails.Instance;
        bool real = u != null && u.isLoggedIn;
        string alias = u != null ? (string.IsNullOrEmpty(u.firstName) ? (real ? u.username : "") : u.firstName) : "";
        row.firstName = !string.IsNullOrEmpty(alias) ? alias : "Player " + (id.Length > 4 ? id.Substring(id.Length - 4) : id);
        row.playerImageIndex = PlayerIdentity.MyAvatarIndex; // the Profile screen's choice (works for guests in the editor too)
        row.gender = PlayerIdentity.MyGender;
        return row;
    }

    // ---------- Helpers ----------

    private static string Now() => DateTime.UtcNow.ToString("o");

    private static bool Online(UserRow u)
    {
        if (u == null || string.IsNullOrEmpty(u.lastSeenUtc)) return false;
        return DateTime.TryParse(u.lastSeenUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var seen)
               && (DateTime.UtcNow - seen).TotalSeconds < OnlineSeconds;
    }

    private static CityDto ToDto(Db db, CityRow c)
    {
        var people = new List<ParticipantDto>();
        foreach (var id in c.participantIds)
        {
            var u = db.users.Find(x => x.userId == id);
            people.Add(new ParticipantDto
            {
                userId = id,
                firstName = u != null ? u.firstName : "Player",
                playerImageIndex = u != null ? u.playerImageIndex : 0,
                gender = u != null ? u.gender : -1,
                online = Online(u),
                lastSeenUtc = u != null ? (u.lastSeenUtc ?? "") : ""
            });
        }
        var host = c.hostUserId != null ? db.users.Find(x => x.userId == c.hostUserId) : null;
        bool hostingNow = !string.IsNullOrEmpty(c.photonRoom) && Online(host);
        return new CityDto
        {
            cityId = c.cityId,
            name = c.name,
            version = c.snapshot != null ? c.snapshot.version : 0,
            lastPlayedUtc = c.lastPlayedUtc ?? "",
            participants = people.ToArray(),
            hostingNow = hostingNow,
            hostUserId = hostingNow ? c.hostUserId : ""
        };
    }

    private CityRow Mine(Db db, string cityId, out string error)
    {
        var c = db.cities.Find(x => x.cityId == cityId);
        error = null;
        if (c == null) { error = "City not found."; return null; }
        if (!c.participantIds.Contains(MyUserId)) { error = "You are not part of this city."; return null; }
        return c;
    }

    // ---------- Cities ----------

    public CityCall<CityDto> CreateCity(string name)
    {
        var db = Load();
        Me(db);
        var c = new CityRow
        {
            cityId = Guid.NewGuid().ToString("N"),
            name = string.IsNullOrWhiteSpace(name) ? "Our city" : name.Trim(),
            participantIds = new List<string> { MyUserId }
        };
        db.cities.Add(c);
        Save(db);
        return CityCall<CityDto>.Success(ToDto(db, c));
    }

    public CityCall<CityListDto> GetMyCities()
    {
        var db = Load();
        Me(db);
        PurgeExpiredWaitingCities(db);
        MergeDuplicatePairs(db, MyUserId);
        Save(db); // keeps my own name / avatar up to date for my partners
        var list = new List<CityDto>();
        foreach (var c in db.cities)
            if (c.participantIds.Contains(MyUserId)) list.Add(ToDto(db, c));
        list.Sort((a, b) => string.CompareOrdinal(b.lastPlayedUtc, a.lastPlayedUtc)); // most recent first
        return CityCall<CityListDto>.Success(new CityListDto { cities = list.ToArray() });
    }

    private static bool SamePair(CityRow a, CityRow b)
    {
        return a.participantIds.Count == 2 && b.participantIds.Count == 2
               && b.participantIds.Contains(a.participantIds[0]) && b.participantIds.Contains(a.participantIds[1]);
    }

    private static int Version(CityRow c) => c.snapshot != null ? c.snapshot.version : 0;

    /// One city per pair of players: if the same two players ended up with two cities, keep the one with
    /// the most progress and fold the other into it.
    private static void MergeDuplicatePairs(Db db, string me)
    {
        for (int i = 0; i < db.cities.Count; i++)
        {
            var a = db.cities[i];
            if (a.participantIds.Count < 2 || !a.participantIds.Contains(me)) continue;
            for (int j = i + 1; j < db.cities.Count; j++)
            {
                var b = db.cities[j];
                if (!SamePair(a, b)) continue;

                bool keepA = Version(a) != Version(b)
                    ? Version(a) > Version(b)
                    : string.CompareOrdinal(a.lastPlayedUtc ?? "", b.lastPlayedUtc ?? "") >= 0;
                var keep = keepA ? a : b;
                var drop = keepA ? b : a;
                if (keep.snapshot == null) keep.snapshot = drop.snapshot;
                if (string.CompareOrdinal(drop.lastPlayedUtc ?? "", keep.lastPlayedUtc ?? "") > 0) keep.lastPlayedUtc = drop.lastPlayedUtc;
                db.invites.RemoveAll(x => x.cityId == drop.cityId);
                db.cities.Remove(drop);
                i = -1; // start over: the list changed
                break;
            }
        }
    }

    /// A city that never got its second player is deleted once its invite has expired (with the invite).
    private static void PurgeExpiredWaitingCities(Db db)
    {
        var expired = db.invites.FindAll(i =>
            DateTime.TryParse(i.expiresUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var exp) && exp < DateTime.UtcNow);
        foreach (var invite in expired)
        {
            db.invites.Remove(invite);
            var city = db.cities.Find(c => c.cityId == invite.cityId);
            if (city != null && city.participantIds.Count < 2 && city.snapshot == null) db.cities.Remove(city);
        }
    }

    public CityCall<CityDto> GetCity(string cityId)
    {
        var db = Load();
        var c = Mine(db, cityId, out string error);
        return c == null ? CityCall<CityDto>.Fail(error) : CityCall<CityDto>.Success(ToDto(db, c));
    }

    public CityCall<CityDto> RenameCity(string cityId, string name)
    {
        var db = Load();
        var c = Mine(db, cityId, out string error);
        if (c == null) return CityCall<CityDto>.Fail(error);
        name = (name ?? "").Trim();
        if (name.Length == 0) return CityCall<CityDto>.Fail("Enter a name for the city.");
        c.name = name.Length > 24 ? name.Substring(0, 24) : name;
        Save(db);
        return CityCall<CityDto>.Success(ToDto(db, c));
    }

    // ---------- Snapshots ----------

    public CityCall<SnapshotDto> GetSnapshot(string cityId)
    {
        var db = Load();
        var c = Mine(db, cityId, out string error);
        if (c == null) return CityCall<SnapshotDto>.Fail(error);
        if (c.snapshot == null) return CityCall<SnapshotDto>.Success(null);
        return CityCall<SnapshotDto>.Success(new SnapshotDto
        {
            version = c.snapshot.version, savedAtUtc = c.snapshot.savedAtUtc, data = c.snapshot.data
        });
    }

    public CityCall<SaveSnapshotResult> PutSnapshot(string cityId, int baseVersion, string data)
    {
        var db = Load();
        var c = Mine(db, cityId, out string error);
        if (c == null) return CityCall<SaveSnapshotResult>.Fail(error);
        if (data != null && data.Length > 1024 * 1024) return CityCall<SaveSnapshotResult>.Fail("City is too large.");

        int current = c.snapshot != null ? c.snapshot.version : 0;
        if (baseVersion != current)
            return CityCall<SaveSnapshotResult>.Success(new SaveSnapshotResult { conflict = true, version = current });

        c.snapshot = new SnapshotRow { version = current + 1, savedAtUtc = Now(), data = data };
        c.lastPlayedUtc = Now();
        Save(db);
        return CityCall<SaveSnapshotResult>.Success(new SaveSnapshotResult { conflict = false, version = c.snapshot.version });
    }

    // ---------- Invites ----------

    public CityCall<InviteDto> CreateInvite(string cityId)
    {
        var db = Load();
        var c = Mine(db, cityId, out string error);
        if (c == null) return CityCall<InviteDto>.Fail(error);
        if (c.participantIds.Count >= 2) return CityCall<InviteDto>.Fail("This city already has two players.");

        db.invites.RemoveAll(x => x.cityId == cityId); // a new invite replaces the previous one
        var chars = new char[6];
        var rng = new System.Random();
        for (int i = 0; i < chars.Length; i++) chars[i] = InviteAlphabet[rng.Next(InviteAlphabet.Length)];
        var invite = new InviteRow
        {
            inviteCode = new string(chars), cityId = cityId,
            expiresUtc = DateTime.UtcNow.AddHours(InviteHours).ToString("o")
        };
        db.invites.Add(invite);
        Save(db);
        return CityCall<InviteDto>.Success(new InviteDto
        {
            inviteCode = invite.inviteCode,
            url = "https://hometown.example.com/join/" + invite.inviteCode, // real domain comes with the backend
            expiresUtc = invite.expiresUtc
        });
    }

    public CityCall<AcceptInviteResult> AcceptInvite(string inviteCode)
    {
        var db = Load();
        Me(db);
        inviteCode = (inviteCode ?? "").Trim().ToUpperInvariant();
        var invite = db.invites.Find(x => x.inviteCode == inviteCode);
        if (invite == null) return CityCall<AcceptInviteResult>.NotFound("That invite code isn't valid.");
        if (DateTime.TryParse(invite.expiresUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var exp) && exp < DateTime.UtcNow)
        {
            db.invites.Remove(invite);
            Save(db);
            return CityCall<AcceptInviteResult>.Fail("That invite has expired.");
        }
        var city = db.cities.Find(x => x.cityId == invite.cityId);
        if (city == null) return CityCall<AcceptInviteResult>.Fail("That city no longer exists.");

        string me = MyUserId;
        if (!city.participantIds.Contains(me))
        {
            if (city.participantIds.Count >= 2) return CityCall<AcceptInviteResult>.Fail("This city already has two players.");

            // One city per pair (for now): if these two already share a city, open that one instead.
            string inviter = city.participantIds[0];
            var existing = db.cities.Find(x => x != city && x.participantIds.Contains(me) && x.participantIds.Contains(inviter));
            if (existing != null)
            {
                if (city.snapshot == null && city.participantIds.Count == 1) db.cities.Remove(city); // the empty one is not needed
                db.invites.Remove(invite);
                Save(db);
                return CityCall<AcceptInviteResult>.Success(new AcceptInviteResult { city = ToDto(db, existing), photonRoom = LiveRoom(db, existing) });
            }
            city.participantIds.Add(me);
        }
        db.invites.Remove(invite); // single use
        Save(db);
        return CityCall<AcceptInviteResult>.Success(new AcceptInviteResult { city = ToDto(db, city), photonRoom = LiveRoom(db, city) });
    }

    private static string LiveRoom(Db db, CityRow c)
    {
        var dto = ToDto(db, c);
        return dto.hostingNow ? c.photonRoom : "";
    }

    // ---------- Live session ----------

    public CityCall<SessionDto> GetSession(string cityId)
    {
        var db = Load();
        var c = Mine(db, cityId, out string error);
        if (c == null) return CityCall<SessionDto>.Fail(error);
        var dto = ToDto(db, c);
        return CityCall<SessionDto>.Success(new SessionDto
        {
            hostingNow = dto.hostingNow, photonRoom = dto.hostingNow ? c.photonRoom : "", hostUserId = dto.hostUserId
        });
    }

    public CityCall<SessionDto> PublishSession(string cityId, string photonRoom)
    {
        var db = Load();
        var c = Mine(db, cityId, out string error);
        if (c == null) return CityCall<SessionDto>.Fail(error);

        var live = ToDto(db, c);
        if (live.hostingNow && live.hostUserId != MyUserId) // the partner got there first
            return CityCall<SessionDto>.Success(new SessionDto { hostingNow = true, photonRoom = c.photonRoom, hostUserId = live.hostUserId });

        Me(db).lastSeenUtc = Now(); // publishing counts as being online
        c.photonRoom = photonRoom;
        c.hostUserId = MyUserId;
        Save(db);
        return CityCall<SessionDto>.Success(new SessionDto { hostingNow = true, photonRoom = photonRoom, hostUserId = MyUserId });
    }

    public CityCall<bool> EndSession(string cityId)
    {
        var db = Load();
        var c = Mine(db, cityId, out string error);
        if (c == null) return CityCall<bool>.Fail(error);
        if (c.hostUserId == MyUserId) { c.photonRoom = ""; c.hostUserId = ""; Save(db); }
        return CityCall<bool>.Success(true);
    }

    // ---------- Handing a city to a friend (no shared server) ----------

    public ParticipantDto MyProfile()
    {
        var db = Load();
        var u = Me(db);
        Save(db);
        return new ParticipantDto
        {
            userId = u.userId, firstName = u.firstName, playerImageIndex = u.playerImageIndex,
            gender = u.gender, online = true, lastSeenUtc = Now()
        };
    }

    private static void RememberUser(Db db, ParticipantDto p)
    {
        var row = db.users.Find(x => x.userId == p.userId);
        if (row == null) { row = new UserRow { userId = p.userId }; db.users.Add(row); }
        row.firstName = string.IsNullOrEmpty(p.firstName) ? "Player" : p.firstName;
        row.playerImageIndex = p.playerImageIndex;
        row.gender = p.gender;
        row.lastSeenUtc = Now(); // they are connected to us right now
    }

    public CityCall<CityDto> AcceptPeer(string cityId, ParticipantDto peer)
    {
        var db = Load();
        Me(db);
        var city = Mine(db, cityId, out string error);
        if (city == null) return CityCall<CityDto>.Fail(error);
        if (peer == null || string.IsNullOrEmpty(peer.userId)) return CityCall<CityDto>.Fail("Couldn't read the other player.");
        if (peer.userId == MyUserId) return CityCall<CityDto>.Fail("That is your own invite - ask your friend to use it.");

        RememberUser(db, peer);
        if (!city.participantIds.Contains(peer.userId))
        {
            if (city.participantIds.Count >= 2) return CityCall<CityDto>.Fail("This city already has two players.");

            // One city per pair: if we already share a city, open that one and drop the empty new one.
            var existing = db.cities.Find(x => x != city && x.participantIds.Contains(MyUserId) && x.participantIds.Contains(peer.userId));
            if (existing != null)
            {
                db.invites.RemoveAll(x => x.cityId == cityId);
                if (city.snapshot == null && city.participantIds.Count == 1) db.cities.Remove(city);
                Save(db);
                return CityCall<CityDto>.Success(ToDto(db, existing));
            }
            city.participantIds.Add(peer.userId);
        }
        db.invites.RemoveAll(x => x.cityId == cityId); // single use
        Save(db);
        return CityCall<CityDto>.Success(ToDto(db, city));
    }

    public bool UpdatePeer(string cityId, ParticipantDto peer)
    {
        if (peer == null || string.IsNullOrEmpty(peer.userId) || peer.userId == MyUserId) return false;
        var db = Load();
        var city = db.cities.Find(x => x.cityId == cityId);
        if (city == null || !city.participantIds.Contains(peer.userId)) return false;
        RememberUser(db, peer);
        Save(db);
        return true;
    }

    public CityCall<CityDto> AdoptCity(JoinReply reply)
    {
        if (reply == null || reply.city == null || string.IsNullOrEmpty(reply.city.cityId))
            return CityCall<CityDto>.Fail("Couldn't read the city your friend sent.");
        var db = Load();
        Me(db);

        var ids = new List<string>();
        foreach (var p in reply.city.participants)
        {
            ids.Add(p.userId);
            if (p.userId != MyUserId) RememberUser(db, p); // my own row is kept as it is
        }
        if (!ids.Contains(MyUserId)) ids.Add(MyUserId);

        // Any older city of mine with this same friend is replaced by the one the host just handed over
        // (the host's city id is the one both phones must use), keeping the better save.
        SnapshotRow older = null;
        foreach (var other in db.cities.FindAll(x => x.cityId != reply.city.cityId && x.participantIds.Count == 2 && ids.Count == 2
                                                       && ids.Contains(x.participantIds[0]) && ids.Contains(x.participantIds[1])))
        {
            if (other.snapshot != null && (older == null || other.snapshot.version > older.version)) older = other.snapshot;
            db.invites.RemoveAll(x => x.cityId == other.cityId);
            db.cities.Remove(other);
        }

        var c = db.cities.Find(x => x.cityId == reply.city.cityId);
        if (c == null) { c = new CityRow { cityId = reply.city.cityId }; db.cities.Add(c); }
        c.name = string.IsNullOrEmpty(reply.city.name) ? "Our city" : reply.city.name;
        c.participantIds = ids;
        c.lastPlayedUtc = Now();
        if (!string.IsNullOrEmpty(reply.snapshotData) && (c.snapshot == null || reply.snapshotVersion > c.snapshot.version))
            c.snapshot = new SnapshotRow { version = reply.snapshotVersion, savedAtUtc = Now(), data = reply.snapshotData };
        if (c.snapshot == null) c.snapshot = older;
        Save(db);
        return CityCall<CityDto>.Success(ToDto(db, c));
    }

    // ---------- Presence ----------

    public CityCall<bool> Heartbeat()
    {
        var db = Load();
        Me(db).lastSeenUtc = Now();
        Save(db);
        return CityCall<bool>.Success(true);
    }
}
