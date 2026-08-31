using UnityEngine;

/// Attach to the World Space Canvas showing a building's construction timer, so it always
/// faces the camera regardless of the building's own rotation or the camera's viewing angle.
/// Without this, a rotated building would rotate its timer text/radial image right along with
/// it, making it unreadable from most angles.
public class WorldSpaceUIBillboard : MonoBehaviour
{
    private Camera cam;

    void Start()
    {
        cam = Camera.main;
    }

    void LateUpdate()
    {
        if (cam == null) cam = Camera.main; // camera may not have been ready at Start
        if (cam == null) return;

        transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, Vector3.up);
    }
}
