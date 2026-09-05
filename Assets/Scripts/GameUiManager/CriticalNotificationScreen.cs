using System.Collections;
using UnityEngine;

public class CriticalNotificationScreen : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform panel;
    public RectTransform titleText;
    public RectTransform descriptionText;
    public RectTransform icon;
    public RectTransform timerText;
    public RectTransform timerSlider;
    public RectTransform closeButton;
    public GameObject[] buttons;

    [Header("Panel Slide Animation")]
    [SerializeField] private float slideDistance = 420f;
    [SerializeField] private float slideDuration = 0.65f;

    [Header("Button Pop Animation")]
    [SerializeField] private float buttonPopDuration = 0.45f;
    [SerializeField] private float buttonPopDelay = 0.12f;

    private Coroutine entranceRoutine;
    private Vector2 panelOriginalPosition;
    private Vector3[] buttonOriginalScales;

    private void Awake()
    {
        if (panel == null) panel = GetComponent<RectTransform>();
        buttons = new[]
        {
            icon != null ? icon.gameObject : null,
            titleText != null ? titleText.gameObject : null,
            descriptionText != null ? descriptionText.gameObject : null,
            timerSlider != null ? timerSlider.gameObject : null,
            timerText != null ? timerText.gameObject : null,
            closeButton != null ? closeButton.gameObject : null
        };
        if (panel != null) panelOriginalPosition = panel.anchoredPosition;
        buttonOriginalScales = new Vector3[buttons == null ? 0 : buttons.Length];
        for (int i = 0; i < buttonOriginalScales.Length; i++)
            if (buttons[i] != null) buttonOriginalScales[i] = buttons[i].transform.localScale;
    }

    private void OnEnable()
    {
        PlayEntrance();
    }

    // =========================================================
    // PANEL SLIDE ANIMATION
    // =========================================================

    public void PlayEntrance()
    {
        if (!isActiveAndEnabled) return;
        ResetAnimation();
        entranceRoutine = StartCoroutine(PlayAfterLayout());
    }

    private IEnumerator PlayAfterLayout()
    {
        // Allow Start and layout groups to finish before animating the final layout.
        yield return null;
        Canvas.ForceUpdateCanvases();
        if (panel != null) panelOriginalPosition = panel.anchoredPosition;
        AnimatePanel();
        AnimateButtons();
        entranceRoutine = null;
    }

    private void AnimatePanel()
    {
        if (panel == null) return;
        panel.anchoredPosition = panelOriginalPosition + Vector2.up * slideDistance;
        LeanTween.move(panel, panelOriginalPosition, slideDuration)
            .setEase(LeanTweenType.easeOutCubic)
            .setIgnoreTimeScale(true);
    }

    // =========================================================
    // BUTTONS POP ONE BY ONE
    // =========================================================

    private void AnimateButtons()
    {
        for (int i = 0; i < buttonOriginalScales.Length; i++)
        {
            if (buttons[i] == null) continue;
            buttons[i].transform.localScale = Vector3.zero;
            LeanTween.scale(buttons[i], buttonOriginalScales[i], buttonPopDuration)
                .setDelay(slideDuration * 0.5f + i * buttonPopDelay)
                .setEase(LeanTweenType.easeOutBack)
                .setIgnoreTimeScale(true);
        }
    }

    private void OnDisable()
    {
        ResetAnimation();
    }

    private void ResetAnimation()
    {
        if (entranceRoutine != null)
        {
            StopCoroutine(entranceRoutine);
            entranceRoutine = null;
        }
        // Restore authored values so reopening mid-animation never causes drift.
        if (panel != null)
        {
            LeanTween.cancel(panel.gameObject);
            panel.anchoredPosition = panelOriginalPosition;
        }
        for (int i = 0; i < buttonOriginalScales.Length; i++)
        {
            if (buttons[i] == null) continue;
            LeanTween.cancel(buttons[i]);
            buttons[i].transform.localScale = buttonOriginalScales[i];
        }
    }
}
