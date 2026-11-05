using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// Floats a small "Ana is moving this" label above every building the other player is moving right
/// now. Creates itself, needs no scene setup, and only polls the lock list kept by CityNetworkSync.
public class FriendMoveMarkers : MonoBehaviour
{
    private readonly Dictionary<int, Transform> markers = new Dictionary<int, Transform>();
    private readonly List<int> stale = new List<int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        var go = new GameObject("FriendMoveMarkers");
        DontDestroyOnLoad(go);
        go.AddComponent<FriendMoveMarkers>();
    }

    void LateUpdate()
    {
        var sync = CityNetworkSync.Instance;
        var placement = BuildingPlacementController.Instance;
        var cam = Camera.main;

        // drop markers whose building is no longer being moved (or is gone)
        stale.Clear();
        foreach (var pair in markers)
        {
            bool locked = sync != null && sync.IsLockedByOther(pair.Key);
            if (pair.Value == null || !locked || placement == null || placement.GetInstanceById(pair.Key) == null) stale.Add(pair.Key);
        }
        foreach (int id in stale)
        {
            if (markers[id] != null) Destroy(markers[id].gameObject);
            markers.Remove(id);
        }

        if (sync == null || placement == null || cam == null) return;
        string who = CityLauncher.Instance != null && CityLauncher.Instance.Active && !string.IsNullOrEmpty(CityLauncher.Instance.WaitingFor)
            ? CityLauncher.Instance.WaitingFor : "Your friend";

        foreach (int id in sync.OtherLocks)
        {
            var building = placement.GetInstanceById(id);
            if (building == null) continue;
            if (!markers.TryGetValue(id, out var marker) || marker == null)
            {
                marker = NewMarker(transform);
                markers[id] = marker;
            }
            marker.GetComponent<TMP_Text>().text = who + " is moving this";

            var renderers = building.GetComponentsInChildren<Renderer>();
            var bounds = new Bounds(building.transform.position, Vector3.zero);
            foreach (var r in renderers)
            {
                bounds.Encapsulate(r.bounds);
            }
            marker.position = new Vector3(bounds.center.x, bounds.max.y + 1.2f + Mathf.Sin(Time.unscaledTime * 3f) * 0.15f, bounds.center.z);
            marker.rotation = cam.transform.rotation; // always readable
        }
    }

    private static Transform NewMarker(Transform parent)
    {
        var go = new GameObject("Friend Move Marker");
        go.transform.SetParent(parent, true);
        var text = go.AddComponent<TextMeshPro>();
        text.fontSize = 6f;
        text.alignment = TextAlignmentOptions.Center;
        text.fontStyle = FontStyles.Bold;
        text.color = new Color(1f, 0.65f, 0.15f);
        text.outlineWidth = 0.25f;
        text.outlineColor = new Color32(20, 12, 5, 255);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.rectTransform.sizeDelta = new Vector2(12f, 2f);
        return go.transform;
    }
}

