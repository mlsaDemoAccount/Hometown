using System.Collections;
using UnityEngine;

/// Plays a one-time camera pan/zoom intro when the Gameplay scene starts - camera begins at an
/// explicit initial position/zoom (e.g. a wide establishing view of the whole city) and
/// smoothly moves to a target position/zoom (the actual gameplay starting view), reusing the
/// SAME FocusOn() capability already used for event-notification camera pans - no separate
/// movement logic duplicated here.
public class CameraIntroSequence : MonoBehaviour
{
    [Header("Start")]
    [Tooltip("Where the camera starts, before the intro pan begins - e.g. a wide establishing position over the whole map. Only X/Z matter here; height comes from Initial Zoom below.")]
    public Transform initialPosition;
    [Tooltip("Zoom level (orthoSize/height, same units as MobileCameraController's Min/Max Zoom) the camera starts at.")]
    public float initialZoom = 40f;

    [Header("Target (where gameplay actually starts)")]
    [Tooltip("Where the camera ends up once the intro finishes - the actual gameplay starting view.")]
    public Transform targetPosition;
    [Tooltip("Zoom level the camera ends the intro at.")]
    public float targetZoom = 20f;

    [Tooltip("Seconds the intro pan/zoom takes.")]
    public float introDuration = 2f;

    void Start()
    {
        StartCoroutine(PlayIntroNextFrame());
    }

    private IEnumerator PlayIntroNextFrame()
    {
        // Wait one frame so MobileCameraController's OWN Start() (which sets its starting zoom
        // from the camera's actual current state) has definitely already run, regardless of
        // Unity's unguaranteed Start() ordering between different components - otherwise it
        // could run AFTER this and immediately overwrite what's being forced here.
        yield return null;

        if (MobileCameraController.Instance == null) yield break;

        if (initialPosition != null)
            MobileCameraController.Instance.SetPositionImmediate(initialPosition.position);
        MobileCameraController.Instance.SetZoomImmediate(initialZoom);

        Vector3 endPos = targetPosition != null ? targetPosition.position : MobileCameraController.Instance.transform.position;
        MobileCameraController.Instance.FocusOn(endPos, introDuration, targetZoom);
    }
}