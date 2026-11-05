using System;

// Data shapes of the cities backend (see Docs/CityBackendSpec.md). JsonUtility-friendly: public
// fields only, no properties, no nullable types - an empty string / -1 stands for "none".

[Serializable]
public class ParticipantDto
{
    public string userId;
    public string firstName;
    public int playerImageIndex;
    public int gender = -1;    // 0 = Male, 1 = Female, -1 = not chosen
    public bool online;
    public string lastSeenUtc; // ISO 8601, empty if never seen
}

[Serializable]
public class CityDto
{
    public string cityId;
    public string name;
    public int version;             // +1 on every accepted snapshot upload
    public string lastPlayedUtc;    // ISO 8601, empty if never played
    public ParticipantDto[] participants = new ParticipantDto[0]; // 1 or 2
    public bool hostingNow;
    public string hostUserId;

    /// The other player of this city, or null while the city has only one participant.
    public ParticipantDto Partner(string myUserId)
    {
        foreach (var p in participants) if (p.userId != myUserId) return p;
        return null;
    }
}

[Serializable]
public class CityListDto
{
    public CityDto[] cities = new CityDto[0];
}

[Serializable]
public class SnapshotDto
{
    public int version;
    public string savedAtUtc;
    public string data; // the game's own JSON (CitySaveData), opaque to the server
}

[Serializable]
public class SaveSnapshotResult
{
    public bool conflict;      // true: someone saved a newer version first (nothing was stored)
    public int version;        // the new version if stored, otherwise the server's current one
}

[Serializable]
public class InviteDto
{
    public string inviteCode;
    public string url;
    public string expiresUtc;
}

[Serializable]
public class AcceptInviteResult
{
    public CityDto city;
    public string photonRoom; // set only when the host's session is live right now
}

[Serializable]
public class SessionDto
{
    public bool hostingNow;
    public string photonRoom;
    public string hostUserId;
}

/// Sent by a player who joined a friend's room with an invite code, straight after connecting (no
/// backend lookup needed): who they are, and which invite they used.
[Serializable]
public class JoinRequest
{
    public string inviteCode;
    public ParticipantDto me;
}

/// The host's answer: the city to adopt (id, name, both players) and its latest save.
[Serializable]
public class JoinReply
{
    public bool ok;
    public string error;
    public CityDto city;
    public int snapshotVersion;
    public string snapshotData; // empty = brand-new city
}

/// A pending cities-API call. The caller yields on it from its own coroutine
/// (yield return new WaitUntil(() => call.isDone)) and then reads ok / error / result -
/// the same no-callbacks pattern as ApiRequestHandle.
public class CityCall<T>
{
    public bool isDone;
    public bool ok;
    public string error;
    public T result;
    public bool notFound;   // the thing asked for does not exist (an unknown invite code, say)

    public static CityCall<T> NotFound(string message) => new CityCall<T> { isDone = true, ok = false, notFound = true, error = message };
    public static CityCall<T> Success(T value) => new CityCall<T> { isDone = true, ok = true, result = value };
    public static CityCall<T> Fail(string message) => new CityCall<T> { isDone = true, ok = false, error = message };
}
