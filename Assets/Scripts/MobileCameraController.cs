using System.Collections;
using UnityEngine;

/// Touch-first camera rig: one-finger drag to pan, two-finger pinch to zoom.
/// Clamps to the grid's world bounds so the player can't pan into the void.
/// Attach to a parent "CameraRig" GameObject; put the actual Camera as a child
/// angled down (typical city-builder isometric-ish view).
public class MobileCameraController : MonoBehaviour
{
    public static MobileCameraController Instance { get; private set; }

    [Header("References")]
    public Camera cam;

    [Header("Pan")]
    public float panSpeed = 1f;
    public float edgePadding = 5f; // extra world units allowed past grid edge (used when useManualPanLimits is off)

    [Header("Pan Limits (optional manual override)")]
    public bool useManualPanLimits = false; // if false, bounds come from GridManager + edgePadding automatically
    public float minX = 0f;
    public float maxX = 100f;
    public float minZ = 0f;
    public float maxZ = 100f;

    [Header("Zoom")]
    public float minZoom = 5f;   // camera height/orthoSize at closest
    public float maxZoom = 40f;  // camera height/orthoSize at farthest
    public float zoomSpeed = 0.05f;
    private float targetZoom;

    private Vector2 lastPanPosition;
    private bool isPanning = false;
    private bool isFocusing = false; // true while FocusOn's coroutine is actively moving the camera
    private Coroutine focusCoroutine;

    void Awake()
    {
        Instance = this; // one camera rig per scene - no duplicate-guard needed
    }

    void Start()
    {
        if (cam == null) cam = Camera.main;
        targetZoom = cam.orthographic ? cam.orthographicSize : transform.position.y;
    }

    void Update()
    {
        // Camera is being moved programmatically (FocusOn) - don't let player pan/zoom fight it.
        if (isFocusing) return;

        // Don't let camera pan/zoom fight with an active building placement/move/demolish drag.
        if (BuildingPlacementController.Instance != null && BuildingPlacementController.Instance.CurrentModeIsBusy)
        {
            isPanning = false;
            return;
        }

#if UNITY_EDITOR
        HandleEditorMouseFallback();
#else
        if (Input.touchCount == 1 && !UIPointerUtil.IsPointerOverUI())
            HandleDrag(Input.GetTouch(0));
        else if (Input.touchCount == 2)
            HandlePinch(Input.GetTouch(0), Input.GetTouch(1));
        else
            isPanning = false;
#endif

        ClampToBounds();
    }

    /// Smoothly pans (and optionally zooms) the camera to a target world position over
    /// `duration` seconds. Built generically for reuse beyond just this one caller:
    /// - City event notifications (wired up now)
    /// - A future game-start "show the full map, then zoom into the starting area" sequence
    /// - A future "focus camera when a building is selected from the shop" behavior
    /// Neither of the latter two are wired in yet (per current scope), but this method doesn't
    /// need to change to support them later - just call FocusOn with different arguments.
    ///
    /// targetZoomValue is optional - pass null to keep whatever zoom level the player currently
    /// has (used for the notification case: pan only, don't force a specific zoom). Pass a
    /// value to also animate zoom toward it (e.g. the future "start zoomed out, end zoomed in"
    /// sequence).
    ///
    /// Blocks normal player pan/zoom input for the duration, same as an active placement does.
    public void FocusOn(Vector3 worldPosition, float duration, float? targetZoomValue = null)
    {
        if (focusCoroutine != null) StopCoroutine(focusCoroutine);
        focusCoroutine = StartCoroutine(FocusRoutine(worldPosition, duration, targetZoomValue));
    }

    private IEnumerator FocusRoutine(Vector3 worldPosition, float duration, float? targetZoomValue)
    {
        isFocusing = true;

        Vector3 startPos = transform.position;
        float startZoom = targetZoom;
        float endZoom = targetZoomValue ?? targetZoom; // keep current zoom if not specified

        // The camera is an angled/offset CHILD of this rig - rig.position is NOT the same
        // point the camera is actually looking at on the ground. For a PERSPECTIVE camera
        // (zoom = rig height via ApplyZoom), that offset SCALES WITH HEIGHT - it is NOT
        // constant across zoom levels. Measuring it at the STARTING zoom and using that fixed
        // value throughout the whole animation goes stale the moment zoom actually changes by
        // a meaningful amount (this was the bug: worked when already at target zoom / no zoom
        // change happening, broke when zooming in/out a lot during the pan). Measuring it at
        // the ENDING zoom instead - where the animation will actually finish - fixes this.
        Vector3 groundOffset = MeasureGroundOffsetAtZoom(endZoom, startPos);
        Vector3 endPos = worldPosition - groundOffset;
        endPos.y = startPos.y; // pan XZ only here - zoom's own height control is handled separately below

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));

            transform.position = Vector3.Lerp(startPos, endPos, t);
            targetZoom = Mathf.Lerp(startZoom, endZoom, t);
            ApplyZoom();

            yield return null;
        }

        transform.position = endPos;
        targetZoom = endZoom;
        ApplyZoom();
        ClampToBounds(); // in case the target sits slightly outside the allowed pan bounds

        isFocusing = false;
        focusCoroutine = null;
    }

    /// Temporarily applies the given zoom + a neutral XZ position to measure what the ground
    /// offset WOULD be at that zoom level, then restores everything exactly as it was - a
    /// transparent, side-effect-free probe (the animation hasn't started moving yet when this
    /// runs). Needed because a perspective camera's offset scales with height/zoom, so it must
    /// be measured at the zoom we're actually animating TO, not whatever zoom we're at right now.
    private Vector3 MeasureGroundOffsetAtZoom(float zoomValue, Vector3 xzPosition)
    {
        Vector3 originalPos = transform.position;
        float originalZoom = targetZoom;

        targetZoom = zoomValue;
        transform.position = xzPosition;
        ApplyZoom();

        Vector3 offset = GetGroundCenterOffset();

        transform.position = originalPos;
        targetZoom = originalZoom;
        ApplyZoom();

        return offset;
    }

    /// Casts a ray from the exact center of the viewport down to the ground plane (y=0) and
    /// returns (groundHitPoint - rig.position) - the constant offset between where the rig
    /// SITS and where the camera actually LOOKS, caused by the camera being an angled/offset
    /// child rather than sitting directly above its target. Adjust the plane height if your
    /// ground isn't at world y=0.
    private Vector3 GetGroundCenterOffset()
    {
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

        if (groundPlane.Raycast(ray, out float distance))
        {
            Vector3 groundHitPoint = ray.GetPoint(distance);
            return groundHitPoint - transform.position;
        }

        return Vector3.zero; // camera somehow isn't facing the ground - fall back to no compensation
    }

    private void HandleDrag(Touch touch)
    {
        if (!isPanning)
        {
            // Start a new pan - either a fresh touch (Began), or a finger that's continuing
            // after the other one lifted mid-pinch (its phase won't be Began in that case).
            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) return;
            lastPanPosition = touch.position;
            isPanning = true;
            return; // wait a frame so we don't jump using a delta from before this pan started
        }

        if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
        {
            Vector2 delta = touch.position - lastPanPosition;
            PanByScreenDelta(delta);
            lastPanPosition = touch.position;
        }
        else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
        {
            isPanning = false;
        }
    }

    private void HandlePinch(Touch t0, Touch t1)
    {
        Vector2 prevPos0 = t0.position - t0.deltaPosition;
        Vector2 prevPos1 = t1.position - t1.deltaPosition;

        float prevDist = (prevPos0 - prevPos1).magnitude;
        float currDist = (t0.position - t1.position).magnitude;
        float delta = currDist - prevDist;

        targetZoom -= delta * zoomSpeed;
        targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
        ApplyZoom();
    }

    private void PanByScreenDelta(Vector2 screenDelta)
    {
        // Convert screen-space drag into world-space movement along the camera's flat plane.
        // Lerp keeps a sensible minimum speed even fully zoomed in - the old (targetZoom/maxZoom)
        // formula collapsed toward zero near minZoom, making pan feel broken when zoomed in.
        float zoomFraction = Mathf.InverseLerp(minZoom, maxZoom, targetZoom); // 0 = zoomed in, 1 = zoomed out
        float scale = Mathf.Lerp(0.5f, 1.5f, zoomFraction) * panSpeed;
        Vector3 right = transform.right;
        Vector3 forward = Vector3.Cross(right, Vector3.up); // flat forward ignoring tilt
        Vector3 move = (-right * screenDelta.x - forward * screenDelta.y) * scale * 0.02f;
        transform.position += move;
    }

    private void ApplyZoom()
    {
        if (cam.orthographic)
        {
            cam.orthographicSize = targetZoom;
        }
        else
        {
            Vector3 pos = transform.position;
            pos.y = targetZoom;
            transform.position = pos;
        }
    }

    private void ClampToBounds()
    {
        Vector3 pos = transform.position;

        if (useManualPanLimits)
        {
            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            pos.z = Mathf.Clamp(pos.z, minZ, maxZ);
        }
        else
        {
            if (GridManager.Instance == null) return;
            Bounds b = GridManager.Instance.GetWorldBounds();
            pos.x = Mathf.Clamp(pos.x, b.min.x - edgePadding, b.max.x + edgePadding);
            pos.z = Mathf.Clamp(pos.z, b.min.z - edgePadding, b.max.z + edgePadding);
        }

        transform.position = pos;
    }

    // Lets you test pan/zoom with a mouse + scroll wheel inside the Unity Editor.
    private void HandleEditorMouseFallback()
    {
        if (Input.touchCount > 0) return; // don't double-handle on devices that report both
        if (Input.GetMouseButtonDown(0) && UIPointerUtil.IsPointerOverUI()) return; // don't start a pan from a UI click

        if (Input.GetMouseButtonDown(0)) { lastPanPosition = Input.mousePosition; isPanning = true; }
        if (Input.GetMouseButton(0) && isPanning)
        {
            Vector2 delta = (Vector2)Input.mousePosition - lastPanPosition;
            PanByScreenDelta(delta);
            lastPanPosition = Input.mousePosition;
        }
        if (Input.GetMouseButtonUp(0)) isPanning = false;

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.0001f)
        {
            targetZoom -= scroll * 500f * zoomSpeed;
            targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
            ApplyZoom();
        }
    }
}