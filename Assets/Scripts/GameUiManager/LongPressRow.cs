using UnityEngine;
using UnityEngine.EventSystems;

/// Put on any UI element: after it has been held down for a moment, ConsumeLongPress() returns
/// true once. Polled by the owning screen (no events/callbacks, like the rest of the project).
public class LongPressRow : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public float holdSeconds = 0.6f;

    private float downAt = -1f;
    private Vector2 downPos;
    private bool fired;

    public void OnPointerDown(PointerEventData e) { downAt = Time.unscaledTime; downPos = e.position; fired = false; }
    public void OnPointerUp(PointerEventData e) => downAt = -1f;
    public void OnPointerExit(PointerEventData e) => downAt = -1f;

    public bool ConsumeLongPress()
    {
        if (downAt < 0f || fired || Time.unscaledTime - downAt < holdSeconds) return false;
        if (((Vector2)Input.mousePosition - downPos).sqrMagnitude > 400f) { downAt = -1f; return false; } // the finger moved: it is a scroll, not a hold
        fired = true;
        return true;
    }
}
