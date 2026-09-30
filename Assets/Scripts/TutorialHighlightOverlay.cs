using UnityEngine;
using UnityEngine.UI;

/// Dims the whole screen EXCEPT a rectangular "hole" around a target UI element, and blocks
/// taps everywhere outside the hole. Built from four dim Images (top/bottom/left/right of the
/// hole), so the hole itself has no Graphic on top of it - taps there reach the real target
/// button normally. No masks, shaders, or re-parenting of the target needed.
///
/// The hole is recomputed every LateUpdate, so it follows the target if it moves or scrolls.
public class TutorialHighlightOverlay : MonoBehaviour
{
    [Tooltip("Full-screen RectTransform (stretch/stretch, zero offsets) that holds the four dim images.")]
    public RectTransform overlayRoot;
    public RectTransform dimTop;
    public RectTransform dimBottom;
    public RectTransform dimLeft;
    public RectTransform dimRight;
    [Tooltip("Extra space around the target, in pixels.")]
    public float padding = 12f;

    private RectTransform target;
    private readonly Vector3[] corners = new Vector3[4];

    public RectTransform Target => target;

    public void SetTarget(RectTransform newTarget)
    {
        target = newTarget;
        overlayRoot.gameObject.SetActive(target != null);
        if (target != null) Refresh();
    }

    public void Hide()
    {
        target = null;
        overlayRoot.gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        if (target == null) return;
        if (!target.gameObject.activeInHierarchy) return; // target hidden/destroyed - the manager will retarget
        Refresh();
    }

    private void Refresh()
    {
        Rect bounds = overlayRoot.rect;

        // Target corners -> overlayRoot local space -> relative to overlayRoot's bottom-left.
        target.GetWorldCorners(corners);
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        for (int i = 0; i < 4; i++)
        {
            Vector3 local = overlayRoot.InverseTransformPoint(corners[i]);
            Vector2 p = new Vector2(local.x - bounds.xMin, local.y - bounds.yMin);
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }

        min -= Vector2.one * padding;
        max += Vector2.one * padding;
        min = Vector2.Max(min, Vector2.zero);
        max = Vector2.Min(max, new Vector2(bounds.width, bounds.height));

        float w = bounds.width;
        float h = bounds.height;

        Place(dimBottom, 0f, 0f, w, min.y);
        Place(dimTop, 0f, max.y, w, h - max.y);
        Place(dimLeft, 0f, min.y, min.x, max.y - min.y);
        Place(dimRight, max.x, min.y, w - max.x, max.y - min.y);
    }

    /// Positions a dim rect using bottom-left anchors/pivot, in overlayRoot-relative pixels.
    private static void Place(RectTransform rt, float x, float y, float width, float height)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(Mathf.Max(0f, width), Mathf.Max(0f, height));
    }
}
