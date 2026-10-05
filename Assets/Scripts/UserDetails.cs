using UnityEngine;

/// Holds the player's session/profile data - persists across scenes (MainMenu -> Gameplay)
/// via DontDestroyOnLoad, so it's available wherever needed once login exists. Right now these
/// fields are mostly empty placeholders since auth isn't built yet - this is the container that
/// future login/signup/profile API responses will populate via SetSession().
public class UserDetails : MonoBehaviour
{
    public static UserDetails Instance { get; private set; }

    [Header("Session (populated once auth is integrated)")]
    public string userId = "";
    public string username = "";
    public string authToken = "";
    public bool isLoggedIn = false;

    [Header("Profile (filled from the API after login)")]
    public string email = "";
    public string firstName = "";
    public string lastName = "";
    public string role = "";
    public int playerImageIndex = 0;
    public int playerXp = 0;
    public double funds = 0;

    // Extend this with whatever profile fields you actually need later - display name,
    // avatar/level, currency, settings, etc. - all in one known place other scripts can read.

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// Called by the login/signup flow once auth is built.
    public void SetSession(string id, string name, string token)
    {
        userId = id;
        username = name;
        authToken = token;
        isLoggedIn = true;
    }

    /// Called by AuthManager with the user returned from login/profile.
    public void SetFromUser(UserDto user, string token)
    {
        SetSession(user.id, user.userName, token);
        email = user.email;
        firstName = user.firstName;
        lastName = user.lastName;
        role = user.role;
        playerImageIndex = user.playerImageIndex;
        playerXp = user.playerXp;
        funds = user.funds;
    }

    /// Called on logout.
    public void ClearSession()
    {
        userId = "";
        username = "";
        authToken = "";
        isLoggedIn = false;
        email = firstName = lastName = role = "";
        playerImageIndex = 0;
        playerXp = 0;
        funds = 0;
    }
}
