using System.Collections.Generic;
using UnityEngine;

/// Persistent list of currently-active city issues. Right now this only holds CITY EVENT
/// entries (traffic jams, etc, each with a live countdown + Fix button) - CityEventManager
/// calls AddEntry/RemoveEntry automatically as events spawn/resolve/expire.
///
/// NOT YET WIRED: stat-based notifications (from CityNotificationManager - "Population is
/// critical, place from X tab") are meant to ALSO live in this same list per the original
/// spec, persisting with no countdown until the underlying stat actually resolves itself. That
/// integration needs CityNotificationManager to poll stat status and remove its own entry once
/// resolved - deliberately left as a follow-up, not half-built here. AddGenericEntry() below is
/// the ready-made hook for that, once you want it built.
public class NotificationCenter : MonoBehaviour
{
    public Transform listParent; // Content of a ScrollView
    public GameObject entryPrefab; // prefab with NotificationCenterEntry component

    private Dictionary<CityEventInstance, NotificationCenterEntry> eventEntries = new Dictionary<CityEventInstance, NotificationCenterEntry>();

    public void AddEntry(CityEventInstance eventInstance)
    {
        if (listParent == null || entryPrefab == null)
        {
            Debug.LogWarning("[NotificationCenter] listParent or entryPrefab not assigned - can't add entry.");
            return;
        }

        var go = Instantiate(entryPrefab, listParent);
        var entry = go.GetComponent<NotificationCenterEntry>();
        entry.SetupForEvent(eventInstance);
        eventEntries[eventInstance] = entry;
    }

    public void RemoveEntry(CityEventInstance eventInstance)
    {
        if (eventEntries.TryGetValue(eventInstance, out var entry))
        {
            if (entry != null) Destroy(entry.gameObject);
            eventEntries.Remove(eventInstance);
        }
    }

    /// Ready-made hook for the future stat-notification integration described above - not
    /// called anywhere yet. Returns the created entry so the caller can hold onto it and
    /// Destroy(entry.gameObject) once the underlying stat resolves.
    public NotificationCenterEntry AddGenericEntry(string message)
    {
        if (listParent == null || entryPrefab == null) return null;

        var go = Instantiate(entryPrefab, listParent);
        var entry = go.GetComponent<NotificationCenterEntry>();
        entry.SetupForStatNotification(message);
        return entry;
    }
}
