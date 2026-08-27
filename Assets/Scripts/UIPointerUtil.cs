using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// Checks whether the current pointer/touch is over UI, so world-space controllers (placement,
/// selection, camera) can ignore it. Uses TWO independent checks:
///
/// 1. EventSystem.IsPointerOverGameObject() - the standard Unity check, but its accuracy
///    depends on EventSystem having already run its own raycast update this frame BEFORE
///    whatever script calls this. Script Execution Order can encourage that, but on some
///    devices/frame timings it's still not guaranteed.
///
/// 2. A registry of explicit UI RectTransforms (see UIBlockerRegistrar) checked directly via
///    RectTransformUtility.RectangleContainsScreenPoint - this does NOT depend on EventSystem
///    having run yet at all, so it closes the gap completely regardless of script/frame order.
///    Add a UIBlockerRegistrar component to any panel/button that should robustly block
///    world-space taps (Done/Cancel/Rotate, Move/Demolish/Upgrade, Shop panel, notifications).
///
/// Either check returning true is enough to count as "over UI".
public static class UIPointerUtil
{
    private static List<RectTransform> registeredBlockers = new List<RectTransform>();

    public static void RegisterBlocker(RectTransform rect)
    {
        if (rect != null && !registeredBlockers.Contains(rect))
            registeredBlockers.Add(rect);
    }

    public static void UnregisterBlocker(RectTransform rect)
    {
        registeredBlockers.Remove(rect);
    }

    public static bool IsPointerOverUI()
    {
        if (IsOverRegisteredBlocker()) return true;

        if (EventSystem.current == null) return false;

#if UNITY_EDITOR
        return EventSystem.current.IsPointerOverGameObject();
#else
        if (Input.touchCount > 0)
            return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
        return false;
#endif
    }

    private static bool IsOverRegisteredBlocker()
    {
        if (registeredBlockers.Count == 0) return false;
        if (!TryGetScreenPos(out Vector2 screenPos)) return false;

        for (int i = registeredBlockers.Count - 1; i >= 0; i--)
        {
            var rect = registeredBlockers[i];
            if (rect == null) { registeredBlockers.RemoveAt(i); continue; } // clean up stale/destroyed entries
            if (!rect.gameObject.activeInHierarchy) continue; // inactive panel shouldn't block anything

            Camera cam = GetCanvasCameraFor(rect);
            if (RectTransformUtility.RectangleContainsScreenPoint(rect, screenPos, cam))
                return true;
        }

        return false;
    }

    private static bool TryGetScreenPos(out Vector2 pos)
    {
#if UNITY_EDITOR
        pos = Input.mousePosition;
        return true;
#else
        if (Input.touchCount > 0) { pos = Input.GetTouch(0).position; return true; }
        pos = default;
        return false;
#endif
    }

    private static Camera GetCanvasCameraFor(RectTransform rect)
    {
        var canvas = rect.GetComponentInParent<Canvas>();
        if (canvas == null) return null;
        return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
    }
}