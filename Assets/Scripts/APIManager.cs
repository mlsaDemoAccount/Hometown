using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// A pending API call. The CALLER yields on this from their own coroutine
/// (yield return new WaitUntil(() => handle.isDone)) - this is what lets the whole API layer
/// avoid System.Action/delegates entirely: there's no callback parameter anywhere, the
/// "continuation" is just normal sequential code in the caller's own coroutine method body.
public class ApiRequestHandle
{
    public bool isDone = false;
    public APIManager.ApiResponse response;
}

/// Generic REST API layer - handles all HTTP communication for the game. Designed to be reused
/// for every future endpoint (login/signup, forgot password, profiling, shop sync, city data
/// sync, etc.), not just version check. Persists across scenes (DontDestroyOnLoad) since later
/// APIs (shop/city data) will need to be called from the Gameplay scene too, not just MainMenu.
///
/// No callbacks/delegates anywhere - Get()/Post() return an ApiRequestHandle immediately, and
/// the caller waits on handle.isDone from within their own coroutine. See VersionCheckManager
/// for the reference usage pattern.
public class APIManager : MonoBehaviour
{
    public static APIManager Instance { get; private set; }

    [Header("Configuration")]
    [Tooltip("Base URL for all API calls. Every endpoint is appended to this.")]
    public string baseUrl = "http://44.196.88.41:8080";

    [Tooltip("Set automatically once login exists - attached as an Authorization header on every request after that. Leave blank for endpoints that don't need auth (e.g. version check).")]
    public string authToken = "";

    public struct ApiResponse
    {
        public bool success;
        public long statusCode;
        public string rawJson;
        public string error;
    }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// Generic GET - reusable for any future endpoint (version check, profiling, city data, etc.)
    /// Returns immediately with a handle - the actual request runs as a background coroutine.
    public ApiRequestHandle Get(string endpoint)
    {
        var handle = new ApiRequestHandle();
        StartCoroutine(GetRoutine(endpoint, handle));
        return handle;
    }

    /// Generic POST - reusable for login, signup, shop purchases, city data sync, etc.
    public ApiRequestHandle Post(string endpoint, string jsonBody)
    {
        var handle = new ApiRequestHandle();
        StartCoroutine(PostRoutine(endpoint, jsonBody, handle));
        return handle;
    }

    private IEnumerator GetRoutine(string endpoint, ApiRequestHandle handle)
    {
        using (var request = UnityWebRequest.Get(baseUrl + endpoint))
        {
            ApplyHeaders(request);
            yield return request.SendWebRequest();
            handle.response = BuildResponse(request);
            handle.isDone = true;
        }
    }

    private IEnumerator PostRoutine(string endpoint, string jsonBody, ApiRequestHandle handle)
    {
        using (var request = new UnityWebRequest(baseUrl + endpoint, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody ?? "{}");
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            ApplyHeaders(request);
            yield return request.SendWebRequest();
            handle.response = BuildResponse(request);
            handle.isDone = true;
        }
    }

    private void ApplyHeaders(UnityWebRequest request)
    {
        if (!string.IsNullOrEmpty(authToken))
            request.SetRequestHeader("Authorization", $"Bearer {authToken}");
    }

    private ApiResponse BuildResponse(UnityWebRequest request)
    {
        bool success = request.result == UnityWebRequest.Result.Success;
        return new ApiResponse
        {
            success = success,
            statusCode = request.responseCode,
            rawJson = success ? request.downloadHandler.text : null,
            error = success ? null : request.error
        };
    }
}
