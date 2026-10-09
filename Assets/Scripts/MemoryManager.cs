using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class MemoryListHandle
{
    public bool isDone;
    public bool success;
    public string message;
    public List<MemoryEntry> items = new List<MemoryEntry>();
}

public class MemoryAddHandle
{
    public bool isDone;
    public bool success;
    public string message;
    public MemoryEntry entry;
}

public class MemoryTextureHandle
{
    public bool isDone;
    public Texture2D texture; // null on failure
}

/// Memory SERVICE: loads/saves a player's memories per landmark and shares the "current
/// landmark / current memory" between the three memory screens. Owns no UI. Same no-callback
/// handle pattern as AuthManager (wait on handle.isDone from your own coroutine).
///
/// Created on first use (no scene setup needed). Lists are cached per player + landmark for the
/// session, so reopening a landmark is instant; Refresh by passing force = true.
public class MemoryManager : MonoBehaviour
{
    private static MemoryManager instance;

    public static MemoryManager Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("MemoryManager"); // root object, so DontDestroyOnLoad works
                instance = go.AddComponent<MemoryManager>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    /// The landmark whose memories are being browsed, and the memory shown in the diary.
    public LandmarkMarker CurrentLandmark { get; set; }
    public int CurrentIndex { get; set; }

    private readonly Dictionary<string, List<MemoryEntry>> cache = new Dictionary<string, List<MemoryEntry>>();
    private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
    }

    void OnDestroy()
    {
        ClearTextures();
    }

    // The cache is per player: a different account / guest on the same device never sees it.
    private static string CacheKey(string landmarkId) => UserDetails.SaveScope + "|" + landmarkId;

    /// Cached list for a landmark (empty if not loaded yet).
    public List<MemoryEntry> GetCached(string landmarkId)
    {
        return cache.TryGetValue(CacheKey(landmarkId), out var list) ? list : new List<MemoryEntry>();
    }

    public MemoryListHandle LoadMemories(string landmarkId, bool force = false)
    {
        var handle = new MemoryListHandle();
        StartCoroutine(LoadRoutine(landmarkId, force, handle));
        return handle;
    }

    public MemoryAddHandle AddMemory(string landmarkId, string title, string description, byte[] jpgBytes)
    {
        var handle = new MemoryAddHandle();
        StartCoroutine(AddRoutine(landmarkId, title, description, jpgBytes, handle));
        return handle;
    }

    /// Downloads (or returns the cached) photo for a memory. Never throws: texture is null on failure.
    public MemoryTextureHandle LoadTexture(MemoryEntry entry)
    {
        var handle = new MemoryTextureHandle();
        StartCoroutine(TextureRoutine(entry, handle));
        return handle;
    }

    /// Frees downloaded photos (call when leaving the memory screens for good, e.g. on logout).
    public void ClearTextures()
    {
        foreach (var t in textures.Values) if (t != null) Destroy(t);
        textures.Clear();
    }

    public void ClearAll()
    {
        cache.Clear();
        ClearTextures();
        CurrentLandmark = null;
        CurrentIndex = 0;
    }

    // ---------- Routines ----------

    private IEnumerator LoadRoutine(string landmarkId, bool force, MemoryListHandle handle)
    {
        string key = CacheKey(landmarkId);
        if (!force && cache.TryGetValue(key, out var cached))
        {
            handle.items = cached;
            handle.success = true;
            handle.isDone = true;
            yield break;
        }

        var api = APIManager.Instance.Get("/api/memories/" + UnityWebRequest.EscapeURL(landmarkId));
        while (!api.isDone) yield return null;

        if (!api.response.success)
        {
            handle.message = AuthManager.ErrorMessage(api.response);
            handle.isDone = true;
            yield break;
        }

        MemoryEntryList list = null;
        try { list = JsonUtility.FromJson<MemoryEntryList>("{\"items\":" + api.response.rawJson + "}"); }
        catch (System.Exception) { }

        if (list == null)
        {
            handle.message = "Unexpected response from the server.";
            handle.isDone = true;
            yield break;
        }

        // Newest first.
        list.items.Sort((a, b) => string.CompareOrdinal(b.createdUtc, a.createdUtc));
        cache[key] = list.items;
        handle.items = list.items;
        handle.success = true;
        handle.isDone = true;
    }

    private IEnumerator AddRoutine(string landmarkId, string title, string description, byte[] jpg, MemoryAddHandle handle)
    {
        var sections = new List<IMultipartFormSection>
        {
            new MultipartFormDataSection("LandmarkId", landmarkId),
            new MultipartFormDataSection("Title", title ?? ""),
            new MultipartFormDataSection("Description", description ?? ""),
            new MultipartFormFileSection("ImageFile", jpg, "memory.jpg", "image/jpeg")
        };

        var api = APIManager.Instance.PostMultipart("/api/memories", sections);
        while (!api.isDone) yield return null;

        if (!api.response.success)
        {
            handle.message = AuthManager.ErrorMessage(api.response);
            handle.isDone = true;
            yield break;
        }

        MemoryEntry entry = null;
        try { entry = JsonUtility.FromJson<MemoryEntry>(api.response.rawJson); }
        catch (System.Exception) { }

        if (entry != null && !string.IsNullOrEmpty(entry.id))
        {
            string key = CacheKey(landmarkId);
            if (!cache.TryGetValue(key, out var list)) { list = new List<MemoryEntry>(); cache[key] = list; }
            list.Insert(0, entry);
            handle.entry = entry;
        }
        else
        {
            cache.Remove(CacheKey(landmarkId)); // saved, but unreadable reply - force a reload next time
        }

        handle.success = true;
        handle.isDone = true;
    }

    private IEnumerator TextureRoutine(MemoryEntry entry, MemoryTextureHandle handle)
    {
        string url = entry != null ? APIManager.Instance.ResolveUrl(entry.imageUrl) : "";
        if (string.IsNullOrEmpty(url)) { handle.isDone = true; yield break; }

        if (textures.TryGetValue(url, out var cached) && cached != null)
        {
            handle.texture = cached;
            handle.isDone = true;
            yield break;
        }

        using (var request = UnityWebRequestTexture.GetTexture(url))
        {
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                var tex = DownloadHandlerTexture.GetContent(request);
                textures[url] = tex;
                handle.texture = tex;
            }
            else
            {
                Debug.LogWarning($"[MemoryManager] Couldn't load photo {url}: {request.error}");
            }
        }
        handle.isDone = true;
    }
}
