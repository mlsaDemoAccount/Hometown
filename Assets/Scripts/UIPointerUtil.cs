using UnityEngine;
using UnityEngine.EventSystems;

/// Shared check used by BuildingPlacementController and BuildingSelectionController so that
/// clicks/taps on UI buttons (Move, Demolish, Upgrade, Shop items, etc.) never also get read
/// as a world-space tap on the same frame. Without this, pressing a UI button can trigger
/// ghost placement/selection at whatever's on-screen behind that button.
public static class UIPointerUtil
{
    public static bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;

#if UNITY_EDITOR
        return EventSystem.current.IsPointerOverGameObject();
#else
        if (Input.touchCount > 0)
            return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
        return false;
#endif
    }
}
