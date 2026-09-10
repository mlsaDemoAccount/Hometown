using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// Result of showing a notification - the caller waits on isDone from its own coroutine, then
/// reads accepted. No callbacks/delegates - same handle pattern used throughout this project
/// (ApiRequestHandle, VersionCheckHandle, UpdatePopupChoiceHandle).
public class NotificationChoiceHandle
{
    public bool isDone = false;
    public bool accepted = false; // true = Accept tapped. false = Dismiss tapped OR auto-timeout.
}

/// Generic short-lived notification popup - message + Accept/Dismiss buttons + an auto-dismiss
/// countdown (text + slider). Genuinely reusable: this component does NOT know or care who's
/// using it - both CityNotificationManager (stat-based suggestions) and CityEventManager (city
/// event toasts) call Show() and act on the handle they get back, however THEY interpret
/// accepted/dismissed. No hardcoded reference back to any specific manager.
///
/// Also used on MULTIPLE separate GameObjects at once (e.g. a critical-styled panel and a
/// normal-styled one) - no singleton Instance pattern here, callers hold direct references.
public class CityNotificationPanel : MonoBehaviour
{
    public GameObject panelRoot; // can be this.gameObject
    public TMP_Text messageText;
    public Button acceptButton;
    public Button dismissButton;

    [Header("Auto-Dismiss Timer")]
    [Tooltip("Seconds before this notification automatically dismisses itself if the player doesn't interact. Set to 0 to disable auto-dismiss (stays open until manually closed).")]
    public float autoDismissDuration = 5f;
    [Tooltip("Optional - shows the countdown as text, e.g. '5s'.")]
    public TMP_Text timerText;
    [Tooltip("Optional - Slider whose value drains from 1 to 0 as the countdown progresses.")]
    public Slider timerSlider;

    private float remainingTime;
    private bool isCountingDown = false;
    private NotificationChoiceHandle currentHandle;

    void Start()
    {
        if (panelRoot == null) panelRoot = gameObject;

        acceptButton.onClick.AddListener(OnAcceptClicked);
        dismissButton.onClick.AddListener(OnDismissClicked);

        // IMPORTANT: do NOT call Hide() here - if panelRoot starts inactive in the scene (as it
        // should), Start() only runs the moment it's FIRST activated, and calling SetActive(false)
        // from inside that same Start() would undo the very SetActive(true) that triggered it,
        // silently cancelling the first-ever Show(). Just make sure panelRoot is unchecked
        // (inactive) in the Hierarchy by default.
    }

    void Update()
    {
        if (!isCountingDown) return;

        remainingTime -= Time.deltaTime;
        if (remainingTime <= 0f)
        {
            isCountingDown = false;
            ResolveChoice(false); // auto-timeout counts as NOT accepted, same as Dismiss
            return;
        }

        UpdateTimerDisplay();
    }

    /// Shows the popup with the given message. Returns a handle - wait on its isDone (typically
    /// via yield return new WaitUntil(() => handle.isDone) in your own coroutine) to know when
    /// and how the player responded.
    public NotificationChoiceHandle Show(string message)
    {
        if (messageText != null) messageText.text = message;
        panelRoot.SetActive(true);

        currentHandle = new NotificationChoiceHandle();

        if (autoDismissDuration > 0f)
        {
            remainingTime = autoDismissDuration;
            isCountingDown = true;
            UpdateTimerDisplay();
        }
        else
        {
            isCountingDown = false;
            if (timerText != null) timerText.gameObject.SetActive(false);
            if (timerSlider != null) timerSlider.gameObject.SetActive(false);
        }

        return currentHandle;
    }

    public void Hide()
    {
        isCountingDown = false;
        panelRoot.SetActive(false);
    }

    private void UpdateTimerDisplay()
    {
        if (timerText != null)
        {
            int seconds = Mathf.CeilToInt(Mathf.Max(remainingTime, 0f));
            timerText.text = $"{seconds}s";
        }

        if (timerSlider != null)
            timerSlider.value = remainingTime / autoDismissDuration; // 1 = just shown, 0 = about to auto-dismiss
    }

    private void OnAcceptClicked() => ResolveChoice(true);
    private void OnDismissClicked() => ResolveChoice(false);

    private void ResolveChoice(bool accepted)
    {
        Hide();
        if (currentHandle != null)
        {
            currentHandle.accepted = accepted;
            currentHandle.isDone = true;
        }
    }
}