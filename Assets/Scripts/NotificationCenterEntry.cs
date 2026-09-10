using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// One row in the Notification Center list. For a CITY EVENT entry (traffic jam, etc.), shows
/// a live countdown and a Fix button. For a STAT-based entry (from CityNotificationManager's
/// suggestions - not yet wired into this list, see NotificationCenter), shows neither - it
/// just sits there until whatever created it removes it externally.
public class NotificationCenterEntry : MonoBehaviour
{
    public TMP_Text messageText;
    public TMP_Text countdownText; // optional - hidden for stat entries
    public Button fixButton;       // optional - hidden for stat entries
    [Tooltip("Optional - tap to pan the camera to this event's location, same as the toast's Accept button. Hidden for stat entries (there's no single location to pan to for those).")]
    public Button focusButton;
    [Tooltip("Seconds the camera takes to pan here when focusButton is tapped.")]
    public float cameraFocusDuration = 1.5f;
    [Tooltip("Zoom level (orthoSize/height) the camera animates to - matches CityEventManager's cameraFocusZoom by default, keep them in sync for consistent behavior between the toast and this list.")]
    public float cameraFocusZoom = 10f;

    private CityEventInstance linkedEvent; // null for stat-based entries

    public void SetupForEvent(CityEventInstance eventInstance)
    {
        linkedEvent = eventInstance;
        if (messageText != null) messageText.text = eventInstance.Data.notificationMessage;

        if (fixButton != null)
        {
            fixButton.gameObject.SetActive(true);
            fixButton.onClick.RemoveAllListeners();
            fixButton.onClick.AddListener(OnFixClicked);
        }
        if (focusButton != null)
        {
            focusButton.gameObject.SetActive(true);
            focusButton.onClick.RemoveAllListeners();
            focusButton.onClick.AddListener(OnFocusClicked);
        }
        if (countdownText != null) countdownText.gameObject.SetActive(true);
    }

    /// For future use once stat-based notifications are wired into this same list - not called
    /// anywhere yet (see NotificationCenter's note on this).
    public void SetupForStatNotification(string message)
    {
        linkedEvent = null;
        if (messageText != null) messageText.text = message;
        if (fixButton != null) fixButton.gameObject.SetActive(false);
        if (focusButton != null) focusButton.gameObject.SetActive(false); // no single location to pan to for a stat notification
        if (countdownText != null) countdownText.gameObject.SetActive(false);
    }

    void Update()
    {
        if (linkedEvent == null || countdownText == null) return;
        int seconds = Mathf.CeilToInt(Mathf.Max(linkedEvent.RemainingLifetime, 0f));
        countdownText.text = $"{seconds}s";
    }

    private void OnFixClicked()
    {
        linkedEvent?.ResolveByPlayer();
    }

    private void OnFocusClicked()
    {
        if (linkedEvent != null && MobileCameraController.Instance != null)
            MobileCameraController.Instance.FocusOn(linkedEvent.transform.position, cameraFocusDuration, cameraFocusZoom);
    }
}