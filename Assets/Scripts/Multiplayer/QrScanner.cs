using System.Collections;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

/// Scans QR codes with the phone camera (WebCamTexture, so no extra camera plugin).
/// Begin() shows the live camera on a RawImage; poll Result / Error from your own Update.
public class QrScanner : MonoBehaviour
{
    public string Result { get; private set; }   // text of the QR code once found
    public string Error { get; private set; }    // set if the camera can't be used
    public bool IsRunning { get; private set; }

    private WebCamTexture cam;
    private RawImage preview;
    private Color32[] frame;
    private Coroutine routine;
    private Vector2 boxSize; // the frame the preview must fit inside (from the first Begin)
    private const float ScanInterval = 0.25f;

    public void Begin(RawImage previewImage)
    {
        End();
        preview = previewImage;
        if (boxSize == Vector2.zero) boxSize = preview.rectTransform.sizeDelta;
        Result = null;
        Error = null;
        IsRunning = true;
        routine = StartCoroutine(Run());
    }

    public void End()
    {
        IsRunning = false;
        Result = null; // callers read Result before calling End()
        if (routine != null) { StopCoroutine(routine); routine = null; }
        if (cam != null)
        {
            cam.Stop();
            Destroy(cam);
            cam = null;
        }
        if (preview != null) preview.texture = null;
    }

    void OnDisable() => End();

    private IEnumerator Run()
    {
        yield return RequestCameraPermission();
        if (Error != null) { IsRunning = false; yield break; }

        WebCamDevice[] devices = WebCamTexture.devices;
        if (devices.Length == 0)
        {
            Error = "No camera found. Type the code instead.";
            IsRunning = false;
            yield break;
        }

        // Prefer the back camera.
        string deviceName = devices[0].name;
        foreach (var d in devices) if (!d.isFrontFacing) { deviceName = d.name; break; }

        cam = new WebCamTexture(deviceName, 640, 480, 30);
        if (preview != null) preview.texture = cam;
        cam.Play();

        float timeout = 8f;
        while (cam != null && cam.width < 100 && timeout > 0f) { timeout -= Time.unscaledDeltaTime; yield return null; }
        if (cam == null || cam.width < 100)
        {
            Error = "Couldn't start the camera. Type the code instead.";
            IsRunning = false;
            yield break;
        }

        float next = 0f;
        while (Result == null)
        {
            FitPreview();
            if (Time.unscaledTime >= next && cam.didUpdateThisFrame)
            {
                next = Time.unscaledTime + ScanInterval;
                if (frame == null || frame.Length != cam.width * cam.height) frame = new Color32[cam.width * cam.height];
                cam.GetPixels32(frame);
                string text = QrCodeUtil.Decode(frame, cam.width, cam.height);
                if (!string.IsNullOrEmpty(text)) Result = text;
            }
            yield return null;
        }
        IsRunning = false;
    }

    // Phone cameras report a rotation / mirror flag: apply them so the preview looks upright, and
    // size it to the camera's real aspect ratio (a 4:3 picture is not stretched into a square).
    private void FitPreview()
    {
        if (preview == null || cam == null || cam.width < 100) return;

        int angle = ((cam.videoRotationAngle % 360) + 360) % 360;
        bool turned = angle == 90 || angle == 270;
        float aspect = turned ? (float)cam.height / cam.width : (float)cam.width / cam.height; // as displayed

        Vector2 shown = aspect >= 1f ? new Vector2(boxSize.x, boxSize.x / aspect)
                                     : new Vector2(boxSize.y * aspect, boxSize.y);
        shown = Vector2.Min(shown, boxSize);
        preview.rectTransform.sizeDelta = turned ? new Vector2(shown.y, shown.x) : shown;
        preview.rectTransform.localEulerAngles = new Vector3(0, 0, -angle);
        preview.uvRect = cam.videoVerticallyMirrored ? new Rect(0, 1, 1, -1) : new Rect(0, 0, 1, 1);
    }

    private IEnumerator RequestCameraPermission()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            Permission.RequestUserPermission(Permission.Camera);
            float wait = 20f;
            while (!Permission.HasUserAuthorizedPermission(Permission.Camera) && wait > 0f)
            {
                wait -= Time.unscaledDeltaTime;
                yield return null;
            }
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
                Error = "Camera permission denied. Allow Camera in Settings, or type the code instead.";
        }
#else
        yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            Error = "Camera permission denied. Allow Camera in Settings, or type the code instead.";
#endif
    }
}
