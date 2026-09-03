using System.Collections;
using UnityEngine;

[System.Serializable]
public class VersionCheckResponse
{
    public string version;
    public string message;
    public bool forceUpdate;
}

/// Result of a version check - the caller (SplashScreen) waits on isDone from its own coroutine,
/// then reads the rest. No callbacks/delegates - same handle pattern as ApiRequestHandle.
public class VersionCheckHandle
{
    public bool isDone = false;
    public bool updateRequired = false;
    public string message;
    public bool forceUpdate;
    public string storeUrl;
}

/// Pure version-check SERVICE - does NOT control screen flow itself. That's owned by
/// SplashScreen/GameUIManager in this project's architecture. Call CheckVersion(), wait on the
/// returned handle's isDone from your own coroutine, then read updateRequired/message/etc.
public class VersionCheckManager : MonoBehaviour
{
    public static VersionCheckManager Instance { get; private set; }

    [Header("API")]
    [Tooltip("Endpoint appended to APIManager.baseUrl - e.g. '/api/version'.")]
    public string versionEndpoint = "/api/version";

    [Header("Store Links")]
    public string androidStoreUrl = "https://play.google.com/store/apps/details?id=YOUR_PACKAGE_NAME";
    public string iosStoreUrl = "https://apps.apple.com/app/idYOUR_APP_ID";

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public VersionCheckHandle CheckVersion()
    {
        var handle = new VersionCheckHandle();
        StartCoroutine(CheckVersionRoutine(handle));
        return handle;
    }

    private IEnumerator CheckVersionRoutine(VersionCheckHandle handle)
    {
        var apiHandle = APIManager.Instance.Get(versionEndpoint);
        yield return new WaitUntil(() => apiHandle.isDone);

        var response = apiHandle.response;
        if (!response.success)
        {
            Debug.LogWarning($"[VersionCheckManager] Version check failed ({response.error}) - failing open.");
            handle.isDone = true; // updateRequired stays false - caller proceeds normally
            yield break;
        }

        VersionCheckResponse data = null;
        try
        {
            data = JsonUtility.FromJson<VersionCheckResponse>(response.rawJson);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[VersionCheckManager] Couldn't parse version check response: {e.Message} - failing open.");
            handle.isDone = true;
            yield break;
        }

        if (data == null || string.IsNullOrEmpty(data.version))
        {
            Debug.LogWarning("[VersionCheckManager] Version check response missing version - failing open.");
            handle.isDone = true;
            yield break;
        }

        if (IsUpdateRequired(Application.version, data.version))
        {
            handle.updateRequired = true;
            handle.message = data.message;
            handle.forceUpdate = data.forceUpdate;
            handle.storeUrl =
#if UNITY_ANDROID
                androidStoreUrl;
#elif UNITY_IOS
                iosStoreUrl;
#else
                androidStoreUrl; // reasonable default when testing in-editor/desktop
#endif
        }

        handle.isDone = true;
    }

    /// Proper version comparison (correctly treats 1.10 as newer than 1.9), not naive string
    /// comparison which would get that backwards.
    private bool IsUpdateRequired(string currentVersion, string latestVersion)
    {
        if (System.Version.TryParse(currentVersion, out var current) &&
            System.Version.TryParse(latestVersion, out var latest))
        {
            return latest > current;
        }

        Debug.LogWarning($"[VersionCheckManager] Couldn't parse versions as System.Version ('{currentVersion}' / '{latestVersion}') - falling back to plain string comparison.");
        return currentVersion != latestVersion;
    }
}