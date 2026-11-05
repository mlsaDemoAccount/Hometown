using UnityEngine;
using UnityEngine.EventSystems;

/// Lets either player drag the shared cube with mouse / finger. It only sends move REQUESTS to
/// the host (SyncedCube.RPC_RequestMove) - the cube itself moves when the host confirms.
public class CubeDragController : MonoBehaviour
{
    [SerializeField] private float sendInterval = 0.04f; // ~25 requests per second while dragging

    [SerializeField] private float grabRadius = 1.2f; // metres around the cube that still count as a grab

    private Camera cam;
    private bool dragging;
    private Vector3 grabOffset;
    private float nextSend;

    void Update()
    {
        var cube = SyncedCube.Instance;
        if (cube == null) { dragging = false; return; }
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        if (Input.GetMouseButtonDown(0) && !IsOverUi())
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            // Grab the cube if the click hits it - even through the ground collider - or lands
            // close to it on the ground (a finger / mouse is not pixel-exact).
            bool hitCube = false;
            foreach (var h in Physics.RaycastAll(ray))
                if (h.collider.GetComponentInParent<SyncedCube>() == cube) { hitCube = true; break; }

            if (TryGroundPoint(ray, cube.transform.position.y, out var point))
            {
                var flat = cube.transform.position - point;
                flat.y = 0f;
                if (hitCube || flat.magnitude <= grabRadius)
                {
                    dragging = true;
                    grabOffset = cube.transform.position - point;
                }
            }
        }
        else if (Input.GetMouseButtonUp(0))
        {
            dragging = false;
        }

        if (dragging && Input.GetMouseButton(0) && Time.unscaledTime >= nextSend)
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (TryGroundPoint(ray, cube.transform.position.y, out var point))
            {
                nextSend = Time.unscaledTime + sendInterval;
                cube.RequestMove(point + grabOffset);
            }
        }
    }

    private static bool TryGroundPoint(Ray ray, float height, out Vector3 point)
    {
        var plane = new Plane(Vector3.up, new Vector3(0f, height, 0f));
        if (plane.Raycast(ray, out float distance)) { point = ray.GetPoint(distance); return true; }
        point = default;
        return false;
    }

    // Only real controls (buttons, input fields) block the drag. A plain background image - e.g.
    // the translucent panels - must not, or a mouse in the editor can never grab the cube.
    private static readonly System.Collections.Generic.List<RaycastResult> uiHits = new System.Collections.Generic.List<RaycastResult>();

    private static bool IsOverUi()
    {
        if (EventSystem.current == null) return false;
        var pointer = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
        uiHits.Clear();
        EventSystem.current.RaycastAll(pointer, uiHits);
        foreach (var hit in uiHits)
            if (hit.gameObject.GetComponentInParent<UnityEngine.UI.Selectable>() != null) return true;
        return false;
    }
}
