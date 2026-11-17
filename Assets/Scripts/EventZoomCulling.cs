using System.Collections.Generic;
using UnityEngine;

/// Zoom-based hide/show for a spawned city event (ambulance, police, garbage, fumigation...), same idea as
/// the traffic and pedestrian zoom culling: zoomed OUT past CityEventManager.hideEventsAboveZoom the event
/// stops rendering and animating, zoomed back IN it comes back.
///
/// The event object itself stays ACTIVE on purpose - its lifetime timer (CityEventInstance), the
/// resolve sequence and any coroutines keep running, so an event never "freezes" while the player is
/// zoomed out. Only what costs draw calls / CPU is switched off: Renderers, Canvases (world-space Fix
/// button), Colliders and Animators. Only the components THIS script switched off are switched back on,
/// so anything the event logic hid itself (e.g. the garbage after Fix) stays hidden.
/// Added automatically by CityEventManager when an event spawns - nothing to set up per prefab.
public class EventZoomCulling : MonoBehaviour
{
    private readonly List<Behaviour> switchedBehaviours = new List<Behaviour>();
    private readonly List<Renderer> switchedRenderers = new List<Renderer>();
    private readonly List<Collider> switchedColliders = new List<Collider>();
    private bool hidden;

    void Update()
    {
        var manager = CityEventManager.Instance;
        if (manager == null || !manager.hideEventsWhenZoomedOut || MobileCameraController.Instance == null)
        {
            if (hidden) Show();
            return;
        }

        bool shouldHide = MobileCameraController.Instance.CurrentZoom > manager.hideEventsAboveZoom;
        if (shouldHide == hidden) return;
        if (shouldHide) Hide(); else Show();
    }

    private void Hide()
    {
        hidden = true;
        foreach (var r in GetComponentsInChildren<Renderer>(false))
            if (r.enabled) { r.enabled = false; switchedRenderers.Add(r); }
        foreach (var c in GetComponentsInChildren<Canvas>(false))
            if (c.enabled) { c.enabled = false; switchedBehaviours.Add(c); }
        foreach (var a in GetComponentsInChildren<Animator>(false))
            if (a.enabled) { a.enabled = false; switchedBehaviours.Add(a); }
        foreach (var c in GetComponentsInChildren<Collider>(false))
            if (c.enabled) { c.enabled = false; switchedColliders.Add(c); }
    }

    private void Show()
    {
        hidden = false;
        foreach (var r in switchedRenderers) if (r != null) r.enabled = true;
        foreach (var b in switchedBehaviours) if (b != null) b.enabled = true;
        foreach (var c in switchedColliders) if (c != null) c.enabled = true;
        switchedRenderers.Clear(); switchedBehaviours.Clear(); switchedColliders.Clear();
    }

    void OnDisable() { if (hidden) Show(); }
}
