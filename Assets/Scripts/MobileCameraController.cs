using UnityEngine;

/// Touch-first camera rig: one-finger drag to pan, two-finger pinch to zoom.
/// Clamps to the grid's world bounds so the player can't pan into the void.
/// Attach to a parent "CameraRig" GameObject; put the actual Camera as a child
/// angled down (typical city-builder isometric-ish view).
public class MobileCameraController : MonoBehaviour
{
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

    void Start()
    {
        if (cam == null) cam = Camera.main;
        targetZoom = cam.orthographic ? cam.orthographicSize : transform.position.y;
    }

    void Update()
    {
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