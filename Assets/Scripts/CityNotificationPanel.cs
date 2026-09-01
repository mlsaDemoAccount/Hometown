using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// The notification bar/popup itself - shows a suggestion message with Accept/Dismiss buttons,
/// plus an auto-dismiss countdown (text + slider). If the player doesn't interact within
/// autoDismissDuration seconds, it dismisses itself automatically - same effect as tapping
/// Dismiss, nothing else changes. CityNotificationManager owns WHAT to suggest and WHEN; this
/// just displays it, counts down, and reports back which action happened.
///
/// NOTE: this script is intentionally used on MULTIPLE separate GameObjects (a critical-styled
/// panel and a normal-styled panel) - it does NOT use a singleton Instance pattern, since that
/// would incorrectly treat the second panel as a duplicate and destroy it. CityNotificationManager
/// holds direct references to both instead.
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
            CityNotificationManager.Instance.OnNotificationDismissed(); // auto-timeout = same as tapping Dismiss
            return;
        }

        UpdateTimerDisplay();
    }

    public void Show(string message)
    {
        if (messageText != null) messageText.text = message;
        panelRoot.SetActive(true);

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
    }

    public void Hide()
    {
        isCountingDown = false; // stop the countdown if closed manually before it would've auto-fired
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

    private void OnAcceptClicked()
    {
        CityNotificationManager.Instance.OnNotificationAccepted();
    }

    private void OnDismissClicked()
    {
        CityNotificationManager.Instance.OnNotificationDismissed();
    }
}