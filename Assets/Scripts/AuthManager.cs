using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// Result of an auth call. The caller waits on isDone from its own coroutine
/// (while (!handle.isDone) yield return null;) - same no-callback pattern as ApiRequestHandle.
public class AuthHandle
{
    public bool isDone = false;
    public bool success = false;
    public long statusCode = 0;
    public string message;    // server's reason on failure, or a friendly network error
    public string rawJson;    // raw success body, for endpoints with an undocumented/optional body
}

/// Auth SERVICE (register / verify account / login / forgot + reset password / session restore /
/// logout). Owns no UI - LoginScreen drives it. Built on APIManager; persists across scenes.
public class AuthManager : MonoBehaviour
{
    public static AuthManager Instance { get; private set; }

    private const string TokenPrefKey = "auth_access_token";

    public UserDto CurrentUser { get; private set; }
    public bool IsLoggedIn => CurrentUser != null;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        transform.SetParent(null); // DontDestroyOnLoad only works on root objects; these sit under "Managers"
        DontDestroyOnLoad(gameObject);
    }

    // ---------- Public API ----------

    /// Creates the account. The server then sends an OTP to the email - the player must call
    /// VerifyAccount with it. Does NOT log the player in.
    public AuthHandle Register(string firstName, string email, string password)
    {
        var body = JsonUtility.ToJson(new RegisterRequest { firstName = firstName, email = email, password = password });
        return Run("/api/auth/register", body);
    }

    public AuthHandle VerifyAccount(string email, string otp)
    {
        var body = JsonUtility.ToJson(new VerifyOtpRequest { email = email, otp = otp });
        return Run("/api/auth/verify-account", body);
    }

    /// Logs in; on success stores the token + user (APIManager, UserDetails, PlayerPrefs).
    public AuthHandle Login(string email, string password)
    {
        var handle = new AuthHandle();
        StartCoroutine(LoginRoutine(email, password, handle));
        return handle;
    }

    public AuthHandle ForgotPassword(string email)
    {
        var body = JsonUtility.ToJson(new ForgotPasswordRequest { email = email });
        return Run("/api/auth/forgot-password", body);
    }

    /// Forgot-password step 2+3 in one call: verifies the emailed OTP, then sets the new password.
    public AuthHandle ResetPassword(string email, string otp, string newPassword)
    {
        var handle = new AuthHandle();
        StartCoroutine(ResetPasswordRoutine(email, otp, newPassword, handle));
        return handle;
    }

    /// Change password while logged in.
    public AuthHandle SetPassword(string currentPassword, string newPassword)
    {
        var body = JsonUtility.ToJson(new SetPasswordRequest { currentPassword = currentPassword, newPassword = newPassword });
        return Run("/api/auth/set-password", body);
    }

    /// Auto-login on app start: uses the saved token to fetch the profile. Success = skip the
    /// login screen. A rejected token (401) is cleared; a network failure keeps it for next time.
    public AuthHandle RestoreSession()
    {
        var handle = new AuthHandle();
        StartCoroutine(RestoreSessionRoutine(handle));
        return handle;
    }

    // ---------- Profile (GET / partial PUT) ----------

    // Fields changed locally but not yet confirmed by the server: field name -> JSON literal.
    // Only these are sent, so e.g. spending funds never touches playerXp on the server.
    private readonly Dictionary<string, string> dirtyProfile = new Dictionary<string, string>();
    private bool flushScheduled;
    private bool flushing;
    private const float FlushDelay = 1f; // coalesces bursts of changes into one PUT

    public bool HasPendingProfileChanges => dirtyProfile.Count > 0;

    /// Fetches the latest profile into UserDetails. Skipped (success=false) while local changes
    /// are still waiting to be pushed, so a stale server copy never overwrites them.
    public AuthHandle RefreshProfile()
    {
        var handle = new AuthHandle();
        StartCoroutine(RefreshProfileRoutine(handle));
        return handle;
    }

    /// Queue a single changed value for the server. Valid fields: funds, playerXp,
    /// playerImageIndex (numbers) and firstName, lastName, userName (text).
    public void QueueProfileUpdate(string field, int value) => QueueProfileLiteral(field, value.ToString());

    public void QueueProfileUpdate(string field, string value) =>
        QueueProfileLiteral(field, "\"" + (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"");

    private void QueueProfileLiteral(string field, string literal)
    {
        if (!IsLoggedIn) return; // not signed in (e.g. Gameplay opened directly in the editor)
        dirtyProfile[field] = literal;
        if (!flushScheduled && !flushing) StartCoroutine(DelayedFlush());
    }

    private IEnumerator DelayedFlush()
    {
        flushScheduled = true;
        yield return new WaitForSecondsRealtime(FlushDelay);
        flushScheduled = false;
        yield return FlushProfile();
    }

    private IEnumerator FlushProfile()
    {
        if (flushing || dirtyProfile.Count == 0 || !IsLoggedIn) yield break;
        flushing = true;

        var sent = new Dictionary<string, string>(dirtyProfile);
        var sb = new StringBuilder("{");
        bool first = true;
        foreach (var kv in sent)
        {
            if (!first) sb.Append(',');
            sb.Append('"').Append(kv.Key).Append("\":").Append(kv.Value);
            first = false;
        }
        sb.Append('}');

        var api = APIManager.Instance.Put("/api/auth/profile", sb.ToString());
        while (!api.isDone) yield return null;

        bool ok = api.response.success;
        if (ok)
        {
            // Clear only what was sent AND hasn't changed again while the request was in flight.
            foreach (var kv in sent)
                if (dirtyProfile.TryGetValue(kv.Key, out var current) && current == kv.Value)
                    dirtyProfile.Remove(kv.Key);

            if (dirtyProfile.Count == 0)
            {
                var user = SafeParse<UserDto>(api.response.rawJson);
                if (user != null && !string.IsNullOrEmpty(user.id)) CurrentUser = user;
            }
        }
        else
        {
            Debug.LogWarning($"[AuthManager] Profile update failed ({api.response.statusCode}): {ErrorMessage(api.response)}. Will retry on the next change.");
        }

        flushing = false;
        if (ok && dirtyProfile.Count > 0) StartCoroutine(DelayedFlush());
    }

    private IEnumerator RefreshProfileRoutine(AuthHandle handle)
    {
        if (!IsLoggedIn) { handle.message = "Not logged in."; handle.isDone = true; yield break; }

        var api = APIManager.Instance.Get("/api/auth/profile");
        while (!api.isDone) yield return null;

        Fill(handle, api.response);
        if (handle.success)
        {
            var user = SafeParse<UserDto>(api.response.rawJson);
            if (user == null || string.IsNullOrEmpty(user.id)) handle.success = false;
            else if (dirtyProfile.Count > 0) handle.success = false; // local changes pending - keep them
            else
            {
                CurrentUser = user;
                UserDetails.Instance?.SetFromUser(user, APIManager.Instance.authToken);
            }
        }
        handle.isDone = true;
    }

    // Best effort: push anything still queued when the app is backgrounded or closed.
    void OnApplicationPause(bool paused)
    {
        if (paused && dirtyProfile.Count > 0) StartCoroutine(FlushProfile());
    }

    void OnApplicationQuit()
    {
        if (dirtyProfile.Count > 0) StartCoroutine(FlushProfile());
    }

    public void Logout()
    {
        dirtyProfile.Clear();
        CurrentUser = null;
        PlayerPrefs.DeleteKey(TokenPrefKey);
        PlayerPrefs.Save();
        if (APIManager.Instance != null) APIManager.Instance.authToken = "";
        UserDetails.Instance?.ClearSession();
    }

    // ---------- Routines ----------

    private IEnumerator LoginRoutine(string email, string password, AuthHandle handle)
    {
        var body = JsonUtility.ToJson(new LoginRequest { email = email, password = password });
        var api = APIManager.Instance.Post("/api/auth/login", body);
        while (!api.isDone) yield return null;

        Fill(handle, api.response);
        if (handle.success)
        {
            var auth = SafeParse<AuthResponse>(api.response.rawJson);
            if (auth == null || string.IsNullOrEmpty(auth.accessToken) || auth.user == null)
            {
                handle.success = false;
                handle.message = "Unexpected response from the server.";
            }
            else
            {
                StartSession(auth.user, auth.accessToken);
            }
        }
        handle.isDone = true;
    }

    private IEnumerator ResetPasswordRoutine(string email, string otp, string newPassword, AuthHandle handle)
    {
        var otpBody = JsonUtility.ToJson(new VerifyOtpRequest { email = email, otp = otp });
        var verify = APIManager.Instance.Post("/api/auth/verify-otp", otpBody);
        while (!verify.isDone) yield return null;

        Fill(handle, verify.response);
        if (!handle.success) { handle.isDone = true; yield break; }

        // verify-otp's success body is undocumented. If it returns a token use it, otherwise the
        // OTP itself is what reset-password expects as "token".
        string token = otp;
        var parsed = SafeParse<VerifyOtpResponse>(verify.response.rawJson);
        if (parsed != null)
        {
            if (!string.IsNullOrEmpty(parsed.token)) token = parsed.token;
            else if (!string.IsNullOrEmpty(parsed.resetToken)) token = parsed.resetToken;
        }

        var resetBody = JsonUtility.ToJson(new ResetPasswordRequest { email = email, token = token, newPassword = newPassword });
        var reset = APIManager.Instance.Post("/api/auth/reset-password", resetBody);
        while (!reset.isDone) yield return null;

        Fill(handle, reset.response);
        handle.isDone = true;
    }

    private IEnumerator RestoreSessionRoutine(AuthHandle handle)
    {
        string token = PlayerPrefs.GetString(TokenPrefKey, "");
        if (string.IsNullOrEmpty(token))
        {
            handle.message = "No saved session.";
            handle.isDone = true;
            yield break;
        }

        APIManager.Instance.authToken = token;
        var api = APIManager.Instance.Get("/api/auth/profile");
        while (!api.isDone) yield return null;

        Fill(handle, api.response);
        if (handle.success)
        {
            var user = SafeParse<UserDto>(api.response.rawJson);
            if (user != null && !string.IsNullOrEmpty(user.id)) StartSession(user, token);
            else handle.success = false;
        }
        else
        {
            APIManager.Instance.authToken = "";
            if (api.response.statusCode == 401) // token expired/revoked - forget it
            {
                PlayerPrefs.DeleteKey(TokenPrefKey);
                PlayerPrefs.Save();
            }
        }
        handle.isDone = true;
    }

    // ---------- Helpers ----------

    private AuthHandle Run(string endpoint, string body)
    {
        var handle = new AuthHandle();
        StartCoroutine(PostRoutine(endpoint, body, handle));
        return handle;
    }

    private IEnumerator PostRoutine(string endpoint, string body, AuthHandle handle)
    {
        var api = APIManager.Instance.Post(endpoint, body);
        while (!api.isDone) yield return null;
        Fill(handle, api.response);
        handle.isDone = true;
    }

    private void StartSession(UserDto user, string token)
    {
        CurrentUser = user;
        APIManager.Instance.authToken = token;
        PlayerPrefs.SetString(TokenPrefKey, token);
        PlayerPrefs.Save();
        UserDetails.Instance?.SetFromUser(user, token);
    }

    private static void Fill(AuthHandle handle, APIManager.ApiResponse response)
    {
        handle.success = response.success;
        handle.statusCode = response.statusCode;
        handle.rawJson = response.rawJson;
        handle.message = response.success ? null : ErrorMessage(response);
    }

    private static string ErrorMessage(APIManager.ApiResponse response)
    {
        if (response.statusCode == 0)
            return "Can't reach the server. Check your internet connection.";

        // ASP.NET validation failures: the real reasons live in "errors", the title is generic.
        string validation = ExtractValidationErrors(response.rawJson);
        if (!string.IsNullOrEmpty(validation)) return validation;

        var body = SafeParse<ApiErrorBody>(response.rawJson);
        if (body != null)
        {
            if (!string.IsNullOrEmpty(body.message)) return body.message;
            if (!string.IsNullOrEmpty(body.detail)) return body.detail;
            if (!string.IsNullOrEmpty(body.title)) return body.title;
        }
        return $"Something went wrong ({response.statusCode}). Please try again.";
    }

    /// Collects every message string from {"errors":{"Field":["msg", ...], ...}}, one per line.
    /// JsonUtility can't read dictionaries with dynamic keys, so this scans the JSON by hand:
    /// inside the "errors" object, strings followed by ':' are field names, the rest are messages.
    private static string ExtractValidationErrors(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        int start = json.IndexOf("\"errors\"", System.StringComparison.Ordinal);
        if (start < 0) return null;
        int i = json.IndexOf('{', start);
        if (i < 0) return null;

        var result = new System.Collections.Generic.List<string>();
        int depth = 0;
        for (; i < json.Length; i++)
        {
            char c = json[i];
            if (c == '{') depth++;
            else if (c == '}') { if (--depth == 0) break; }
            else if (c == '"')
            {
                var sb = new System.Text.StringBuilder();
                for (i++; i < json.Length && json[i] != '"'; i++)
                {
                    if (json[i] == '\\' && i + 1 < json.Length)
                    {
                        i++;
                        char e = json[i];
                        if (e == 'n') sb.Append(' ');
                        else if (e == 'u' && i + 4 < json.Length
                                 && int.TryParse(json.Substring(i + 1, 4), System.Globalization.NumberStyles.HexNumber, null, out int code))
                        { sb.Append((char)code); i += 4; }
                        else sb.Append(e);
                    }
                    else sb.Append(json[i]);
                }
                int next = i + 1;
                while (next < json.Length && char.IsWhiteSpace(json[next])) next++;
                bool isKey = next < json.Length && json[next] == ':';
                if (!isKey && sb.Length > 0) result.Add(sb.ToString());
            }
        }
        return result.Count > 0 ? string.Join("\n", result) : null;
    }

    private static T SafeParse<T>(string json) where T : class
    {
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonUtility.FromJson<T>(json); }
        catch (System.Exception) { return null; }
    }
}
