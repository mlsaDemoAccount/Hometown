using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// One row in the Notification Center list. For a CITY EVENT entry (traffic jam, etc.), shows
/// a live countdown and a Focus button (tap to pan the camera there - actually resolving it
/// happens via the world-space Fix button once you're looking at it, not from this list). For
/// a STAT-based entry (persistent category warnings, and the healthy-city growth-chain
/// suggestion), the whole row is tappable instead - opens the shop to the relevant category
/// and closes this screen, same effect as the toast's Accept button.
public class NotificationCenterEntry : MonoBehaviour
{
    public TMP_Text messageText;
    public TMP_Text countdownText; // optional - hidden for stat entries
    [Tooltip("Tap to pan the camera to this event's location, same as the toast's Accept button, AND closes the Notification Center screen so the player can actually see the result. Hidden for stat entries (there's no single location to pan to for those).")]
    public Button focusButton;
    [Tooltip("Seconds the camera takes to pan here when focusButton is tapped.")]
    public float cameraFocusDuration = 1.5f;
    [Tooltip("Zoom level (orthoSize/height) the camera animates to - matches CityEventManager's cameraFocusZoom by default, keep them in sync for consistent behavior between the toast and this list.")]
    public float cameraFocusZoom = 10f;
    [Tooltip("Tap anywhere on the row to open the shop to the relevant category, for STAT entries only (population warnings, growth-chain suggestions). Can be a Button covering the whole row's background. Hidden/unused for event entries (they use focusButton instead).")]
    public Button rowButton;

    /// Set automatically by NotificationCenter right after it creates this entry - not
    /// assigned manually in the Inspector.
    public NotificationCenter Owner { get; set; }

    private CityEventInstance linkedEvent; // null for stat-based entries
    private BuildingCategory? linkedCategory; // set only for stat-based entries

    public void SetupForEvent(CityEventInstance eventInstance)
    {
        linkedEvent = eventInstance;
        linkedCategory = null;
        if (messageText != null) messageText.text = eventInstance.Data.notificationMessage;

        if (focusButton != null)
        {
            focusButton.gameObject.SetActive(true);
            focusButton.onClick.RemoveAllListeners();
            focusButton.onClick.AddListener(OnFocusClicked);
        }
        if (rowButton != null) rowButton.gameObject.SetActive(false); // events use focusButton instead
        if (countdownText != null) countdownText.gameObject.SetActive(true);

        // Defensive: force this entry active regardless of the prefab's own authored state -
        // a repeatable list-item prefab should default to ACTIVE (only the parent screen
        // itself should start inactive), but this guards against that being set up backwards.
        gameObject.SetActive(true);
    }

    /// Used for both persistent category warnings AND the healthy-city growth-chain
    /// suggestion - relatedCategory is what tapping the row opens the shop to.
    public void SetupForStatNotification(string message, BuildingCategory relatedCategory)
    {
        linkedEvent = null;
        linkedCategory = relatedCategory;
        if (messageText != null) messageText.text = message;
        if (focusButton != null) focusButton.gameObject.SetActive(false); // no single location to pan to for a stat notification
        if (countdownText != null) countdownText.gameObject.SetActive(false);

        if (rowButton != null)
        {
            rowButton.gameObject.SetActive(true);
            rowButton.onClick.RemoveAllListeners();
            rowButton.onClick.AddListener(OnRowClicked);
        }

        gameObject.SetActive(true); // same defensive fix as SetupForEvent above
    }

    /// Updates an existing entry's text in place (e.g. Warning -> Critical wording, or the
    /// growth-chain suggestion advancing to the next category) - used instead of recreating
    /// the entry, which would also needlessly reset its buttons.
    public void UpdateMessage(string message)
    {
        if (messageText != null) messageText.text = message;
    }

    void Update()
    {
        if (linkedEvent == null || countdownText == null) return;
        int seconds = Mathf.CeilToInt(Mathf.Max(linkedEvent.RemainingLifetime, 0f));
        countdownText.text = $"{seconds}s";
    }

    private void OnFocusClicked()
    {
        if (linkedEvent != null && MobileCameraController.Instance != null)
            MobileCameraController.Instance.FocusOn(linkedEvent.transform.position, cameraFocusDuration, cameraFocusZoom);

        Owner?.Hide(); // close the list screen so the player can actually see the camera move
    }

    private void OnRowClicked()
    {
        if (linkedCategory.HasValue)
        {
            if (UiManager.Instance != null)
            {
                UiManager.Instance.ShowHideGameplayPanel(false);
                UiManager.Instance.ShowHideShopPanel(true);
            }
            ShopUIController.Instance?.SelectCategoryExternally(linkedCategory.Value);
        }

        Owner?.Hide(); // close the list screen so the shop is actually visible underneath
    }
}