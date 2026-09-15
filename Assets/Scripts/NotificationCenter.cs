using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// Persistent list of currently-active city issues AND ongoing suggestions - city events
/// (traffic jams, etc, each with a live countdown), persistent stat warnings ("Population is
/// critical"), and the healthy-city growth-chain suggestion ("place something from Lifestyle
/// next") all live in this same list. CityEventManager and CityNotificationManager both call
/// into this to add/remove their own entries.
///
/// Also owns the list SCREEN itself (panelRoot) - shown/hidden via a HUD toggle button
/// (NotificationCenterToggleButton) or automatically closed when an entry's Focus button pans
/// the camera to it, so the player can actually see the result.
public class NotificationCenter : MonoBehaviour
{
    [Tooltip("The whole list screen - can be this.gameObject, or a separate parent if this component lives outside the visual hierarchy.")]
    public GameObject panelRoot;
    public Transform listParent; // Content of a ScrollView
    public GameObject entryPrefab; // prefab with NotificationCenterEntry component
    [Tooltip("Manual close/X button on the screen itself - closes the list without needing to tap an entry's Focus button.")]
    public Button closeButton;

    private Dictionary<CityEventInstance, NotificationCenterEntry> eventEntries = new Dictionary<CityEventInstance, NotificationCenterEntry>();
    private List<NotificationCenterEntry> genericEntries = new List<NotificationCenterEntry>();

    /// Total pending items across both event and generic (stat/growth-chain) entries - used by
    /// NotificationCenterToggleButton's badge count.
    public int TotalEntryCount => eventEntries.Count + genericEntries.Count;

    void Start()
    {
        if (panelRoot == null) panelRoot = gameObject;

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }
        // IMPORTANT: do NOT call Hide() here - same Awake/Start-timing reasoning as every other
        // panel in this project. Keep panelRoot unchecked (inactive) in the Hierarchy by default.
    }

    public void Show()
    {
        panelRoot.SetActive(true);
    }

    public void Hide()
    {
        panelRoot.SetActive(false);
    }

    public void Toggle()
    {
        panelRoot.SetActive(!panelRoot.activeSelf);
    }

    public void AddEntry(CityEventInstance eventInstance)
    {
        if (listParent == null || entryPrefab == null)
        {
            Debug.LogWarning("[NotificationCenter] listParent or entryPrefab not assigned - can't add entry.");
            return;
        }

        var go = Instantiate(entryPrefab, listParent);
        go.SetActive(true); // defensive - see the matching note in NotificationCenterEntry
        var entry = go.GetComponent<NotificationCenterEntry>();
        if (entry == null)
        {
            Debug.LogWarning("[NotificationCenter] entryPrefab has no NotificationCenterEntry component - can't set it up.");
            Destroy(go);
            return;
        }
        entry.Owner = this;
        entry.SetupForEvent(eventInstance);
        eventEntries[eventInstance] = entry;

        ForceLayoutRebuild();
    }

    public void RemoveEntry(CityEventInstance eventInstance)
    {
        if (eventEntries.TryGetValue(eventInstance, out var entry))
        {
            if (entry != null) Destroy(entry.gameObject);
            eventEntries.Remove(eventInstance);
        }
    }

    /// Used for persistent stat warnings AND the healthy-city growth-chain suggestion - both
    /// are "generic" entries with no single location or countdown, but ARE tappable to open
    /// the shop to relatedCategory. Returns the created entry so the caller can hold onto it
    /// and pass it to RemoveGenericEntry() later.
    public NotificationCenterEntry AddGenericEntry(string message, BuildingCategory relatedCategory)
    {
        if (listParent == null || entryPrefab == null)
        {
            Debug.LogWarning("[NotificationCenter] listParent or entryPrefab not assigned - can't add generic entry.");
            return null;
        }

        var go = Instantiate(entryPrefab, listParent);
        go.SetActive(true); // defensive - see the matching note in NotificationCenterEntry
        var entry = go.GetComponent<NotificationCenterEntry>();
        if (entry == null)
        {
            Debug.LogWarning("[NotificationCenter] entryPrefab has no NotificationCenterEntry component - can't set it up.");
            Destroy(go);
            return null;
        }
        entry.Owner = this;
        entry.SetupForStatNotification(message, relatedCategory);
        genericEntries.Add(entry);
        Debug.Log($"[NotificationCenter] Added generic entry: '{message}' (total generic entries now: {genericEntries.Count})");

        ForceLayoutRebuild();

        return entry;
    }

    public void RemoveGenericEntry(NotificationCenterEntry entry)
    {
        if (entry == null) return;
        genericEntries.Remove(entry);
        Destroy(entry.gameObject);
    }

    /// Content Size Fitter/Layout Group doesn't always recalculate immediately for a
    /// runtime-instantiated child - forcing it here is what makes a newly-added entry actually
    /// render this frame instead of possibly sitting invisible inside a not-yet-resized
    /// Content rect (same fix ShopUIController needed for its item list).
    private void ForceLayoutRebuild()
    {
        if (listParent is RectTransform listRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(listRect);
    }
}