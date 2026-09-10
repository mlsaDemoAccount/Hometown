using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(CanvasGroup))]
public class AudioSettingsAnimation : MonoBehaviour
{
    [Header("Volume Controls (0 to 1)")]
    [SerializeField] private Slider generalSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private TMP_Text generalValueText;
    [SerializeField] private TMP_Text musicValueText;
    [SerializeField] private TMP_Text sfxValueText;

    [Header("UI References")]
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private RectTransform titleRect;
    [SerializeField] private RectTransform logoRect;
    [SerializeField] private RectTransform backButtonRect;

    // Assign complete row containers:
    // General Volume, Music, SFX.
    [SerializeField] private RectTransform[] audioRows;

    [Header("Screen Entrance")]
    [SerializeField] private float fadeDuration = 0.65f;
    [SerializeField] private float panelDuration = 0.85f;

    // A small scale change looks softer than a large pop.
    [SerializeField] private float panelStartScale = 0.97f;

    [Header("Title and Back Button")]
    [SerializeField] private float elementDuration = 0.7f;
    [SerializeField] private float titleSlideDistance = 12f;
    [SerializeField] private float backSlideDistance = 14f;

    [Header("Audio Rows")]
    [SerializeField] private float rowDuration = 0.8f;
    [SerializeField] private float rowSlideDistance = 22f;
    [SerializeField] private float firstRowDelay = 0.12f;
    [SerializeField] private float rowStagger = 0.1f;

    [Header("Logo Entrance")]
    [SerializeField] private float logoEntranceDuration = 0.85f;
    [SerializeField] private float logoStartScale = 0.92f;

    [Header("Slow Logo Heartbeat")]

    // 1.035 means the logo grows only 3.5%.
    [SerializeField] private float heartbeatScale = 1.035f;

    // Duration of ONE direction: grow or shrink.
    // A complete heartbeat takes twice this value.
    [SerializeField] private float heartbeatHalfDuration = 1.25f;

    [SerializeField] private float heartbeatStartDelay = 0.25f;

    private CanvasGroup screenGroup;
    private CanvasGroup titleGroup;
    private CanvasGroup logoGroup;
    private CanvasGroup backButtonGroup;
    private CanvasGroup[] rowGroups;

    private Vector3 panelOriginalScale;
    private Vector3 logoOriginalScale;

    private Vector2 titleOriginalPosition;
    private Vector2 backOriginalPosition;
    private Vector2[] rowOriginalPositions;

    private float screenOriginalAlpha;
    private float titleOriginalAlpha;
    private float logoOriginalAlpha;
    private float backOriginalAlpha;
    private float[] rowOriginalAlphas;

    private bool originalInteractable;
    private bool originalBlocksRaycasts;
    private bool initialized;

    // Save only the IDs of animations created by THIS script.
    // This avoids cancelling animations belonging to other scripts.
    private readonly List<int> tweenIds = new List<int>();

    private void Awake()
    {
        screenGroup = GetComponent<CanvasGroup>();

        if (panelRect == null)
            panelRect = GetComponent<RectTransform>();

        if (panelRect == null)
        {
            Debug.LogError(
                "AudioSettingsAnimation must be attached to a UI object.",
                this
            );

            enabled = false;
            return;
        }

        if (audioRows == null)
            audioRows = new RectTransform[0];

        // Let Unity finish the current UI layout before saving positions.
        Canvas.ForceUpdateCanvases();

        panelOriginalScale = panelRect.localScale;
        screenOriginalAlpha = screenGroup.alpha;
        originalInteractable = screenGroup.interactable;
        originalBlocksRaycasts = screenGroup.blocksRaycasts;

        if (titleRect != null)
        {
            titleOriginalPosition = titleRect.anchoredPosition;
            titleGroup = GetOrAddCanvasGroup(titleRect);
            titleOriginalAlpha = titleGroup.alpha;
        }

        if (logoRect != null)
        {
            logoOriginalScale = logoRect.localScale;
            logoGroup = GetOrAddCanvasGroup(logoRect);
            logoOriginalAlpha = logoGroup.alpha;
        }

        if (backButtonRect != null)
        {
            backOriginalPosition = backButtonRect.anchoredPosition;
            backButtonGroup = GetOrAddCanvasGroup(backButtonRect);
            backOriginalAlpha = backButtonGroup.alpha;
        }

        rowOriginalPositions = new Vector2[audioRows.Length];
        rowGroups = new CanvasGroup[audioRows.Length];
        rowOriginalAlphas = new float[audioRows.Length];

        for (int i = 0; i < audioRows.Length; i++)
        {
            if (audioRows[i] == null)
                continue;

            rowOriginalPositions[i] = audioRows[i].anchoredPosition;
            rowGroups[i] = GetOrAddCanvasGroup(audioRows[i]);
            rowOriginalAlphas[i] = rowGroups[i].alpha;
        }

        // Connect once. Opening the screen again will not add duplicate listeners.
        if (generalSlider != null) generalSlider.onValueChanged.AddListener(ChangeGeneralVolume);
        if (musicSlider != null) musicSlider.onValueChanged.AddListener(ChangeMusicVolume);
        if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(ChangeSFXVolume);

        initialized = true;
    }

    private void OnEnable()
    {
        RefreshVolumeControls();
        if (initialized)
            PlayAnimation();
    }

    private void Start()
    {
        // All Awake methods have run now, including SoundManager's Awake.
        RefreshVolumeControls();
    }

    private void RefreshVolumeControls()
    {
        SoundManager sound = SoundManager.Instance;
        if (sound == null) return;

        // Read settings without firing slider events or changing the saved volumes.
        if (generalSlider != null) generalSlider.SetValueWithoutNotify(sound.GeneralVolume);
        if (musicSlider != null) musicSlider.SetValueWithoutNotify(sound.MusicVolume);
        if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(sound.SFXVolume);
        UpdatePercentage(generalValueText, sound.GeneralVolume);
        UpdatePercentage(musicValueText, sound.MusicVolume);
        UpdatePercentage(sfxValueText, sound.SFXVolume);
    }

    public void ChangeGeneralVolume(float value)
    {
        if (SoundManager.Instance != null) SoundManager.Instance.SetGeneralVolume(value);
        UpdatePercentage(generalValueText, value);
    }

    public void ChangeMusicVolume(float value)
    {
        // Background music only.
        if (SoundManager.Instance != null) SoundManager.Instance.SetMusicVolume(value);
        UpdatePercentage(musicValueText, value);
    }

    public void ChangeSFXVolume(float value)
    {
        // Buttons, typing, and popup sounds all use SoundManager's SFX source.
        if (SoundManager.Instance != null) SoundManager.Instance.SetSFXVolume(value);
        UpdatePercentage(sfxValueText, value);
    }

    private void UpdatePercentage(TMP_Text label, float value)
    {
        if (label != null) label.text = Mathf.RoundToInt(value * 100f) + "%";
    }

    public void PlayAnimation()
    {
        if (!initialized || !isActiveAndEnabled)
            return;

        CancelAnimations();
        RestoreOriginalState();

        // Prevent clicking controls while they are entering.
        // Continue blocking clicks from reaching screens underneath.
        screenGroup.interactable = false;
        screenGroup.blocksRaycasts = true;

        screenGroup.alpha = 0f;
        panelRect.localScale = panelOriginalScale * panelStartScale;

        // Use the same persistent SoundManager as MainMenu.
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayPanelOpenSound();

        // Fade in the complete screen.
        Fade(screenGroup, screenOriginalAlpha, fadeDuration);

        // Gently grow to normal size, without overshooting or bouncing.
        Track(
            LeanTween.scale(
                panelRect,
                panelOriginalScale,
                panelDuration
            )
            .setEaseOutCubic()
            .setIgnoreTimeScale(true)
        );

        // Title moves down a little while fading in.
        AnimateElement(
            titleRect,
            titleGroup,
            titleOriginalPosition,
            Vector2.up * titleSlideDistance,
            titleOriginalAlpha,
            elementDuration,
            0.08f
        );

        // Back button slides in gently from the left.
        AnimateElement(
            backButtonRect,
            backButtonGroup,
            backOriginalPosition,
            Vector2.left * backSlideDistance,
            backOriginalAlpha,
            elementDuration,
            0.05f
        );

        if (logoRect != null)
        {
            logoRect.localScale = logoOriginalScale * logoStartScale;
            logoGroup.alpha = 0f;

            Fade(
                logoGroup,
                logoOriginalAlpha,
                logoEntranceDuration
            );

            Track(
                LeanTween.scale(
                    logoRect,
                    logoOriginalScale,
                    logoEntranceDuration
                )
                .setEaseOutCubic()
                .setIgnoreTimeScale(true)
                .setOnComplete(StartLogoHeartbeat)
            );
        }

        // Calculate when all entrance animations have finished.
        float entranceEnd = Mathf.Max(
            fadeDuration,
            panelDuration
        );

        if (titleRect != null)
            entranceEnd = Mathf.Max(
                entranceEnd,
                0.08f + elementDuration
            );

        if (backButtonRect != null)
            entranceEnd = Mathf.Max(
                entranceEnd,
                0.05f + elementDuration
            );

        if (logoRect != null)
            entranceEnd = Mathf.Max(
                entranceEnd,
                logoEntranceDuration
            );

        for (int i = 0; i < audioRows.Length; i++)
        {
            if (audioRows[i] == null)
                continue;

            float delay = firstRowDelay + i * rowStagger;

            // Small upward movement plus fading creates a softer entrance.
            AnimateElement(
                audioRows[i],
                rowGroups[i],
                rowOriginalPositions[i],
                Vector2.down * rowSlideDistance,
                rowOriginalAlphas[i],
                rowDuration,
                delay
            );

            entranceEnd = Mathf.Max(
                entranceEnd,
                delay + rowDuration
            );
        }

        // Enable controls once the entrance finishes.
        Track(
            LeanTween.delayedCall(gameObject, entranceEnd, () =>
            {
                screenGroup.interactable = originalInteractable;
                screenGroup.blocksRaycasts = originalBlocksRaycasts;
            })
            .setIgnoreTimeScale(true)
        );
    }

    private void StartLogoHeartbeat()
    {
        if (!isActiveAndEnabled || logoRect == null)
            return;

        // Slowly grow and shrink the logo forever.
        // Sine easing slows down at both ends for a gentle pulse.
        Track(
            LeanTween.scale(
                logoRect,
                logoOriginalScale * heartbeatScale,
                heartbeatHalfDuration
            )
            .setEaseInOutSine()
            .setDelay(heartbeatStartDelay)
            .setLoopPingPong()
            .setIgnoreTimeScale(true)
        );
    }

    private void AnimateElement(
        RectTransform rect,
        CanvasGroup group,
        Vector2 targetPosition,
        Vector2 startOffset,
        float targetAlpha,
        float duration,
        float delay)
    {
        if (rect == null || group == null)
            return;

        Vector2 startPosition = targetPosition + startOffset;

        rect.anchoredPosition = startPosition;
        group.alpha = 0f;

        // Animate a progress value from 0 to 1.
        // Use it to move between the starting and final UI positions.
        Track(
            LeanTween.value(rect.gameObject, 0f, 1f, duration)
                .setOnUpdate((float progress) =>
                {
                    if (rect != null)
                    {
                        rect.anchoredPosition = Vector2.LerpUnclamped(
                            startPosition,
                            targetPosition,
                            progress
                        );
                    }
                })
                .setEaseOutCubic()
                .setDelay(delay)
                .setIgnoreTimeScale(true)
        );

        Fade(group, targetAlpha, duration, delay);
    }

    private void Fade(
        CanvasGroup group,
        float targetAlpha,
        float duration,
        float delay = 0f)
    {
        Track(
            LeanTween.alphaCanvas(group, targetAlpha, duration)
                .setEaseInOutSine()
                .setDelay(delay)
                .setIgnoreTimeScale(true)
        );
    }

    private CanvasGroup GetOrAddCanvasGroup(RectTransform rect)
    {
        // A separate CanvasGroup lets each element fade independently.
        CanvasGroup group = rect.GetComponent<CanvasGroup>();

        if (group == null)
            group = rect.gameObject.AddComponent<CanvasGroup>();

        return group;
    }

    private void Track(LTDescr tween)
    {
        tweenIds.Add(tween.id);
    }

    private void CancelAnimations()
    {
        foreach (int id in tweenIds)
            LeanTween.cancel(id);

        tweenIds.Clear();
    }

    private void RestoreOriginalState()
    {
        panelRect.localScale = panelOriginalScale;

        screenGroup.alpha = screenOriginalAlpha;
        screenGroup.interactable = originalInteractable;
        screenGroup.blocksRaycasts = originalBlocksRaycasts;

        if (titleRect != null)
        {
            titleRect.anchoredPosition = titleOriginalPosition;
            titleGroup.alpha = titleOriginalAlpha;
        }

        if (logoRect != null)
        {
            logoRect.localScale = logoOriginalScale;
            logoGroup.alpha = logoOriginalAlpha;
        }

        if (backButtonRect != null)
        {
            backButtonRect.anchoredPosition = backOriginalPosition;
            backButtonGroup.alpha = backOriginalAlpha;
        }

        for (int i = 0; i < audioRows.Length; i++)
        {
            if (audioRows[i] == null)
                continue;

            audioRows[i].anchoredPosition = rowOriginalPositions[i];
            rowGroups[i].alpha = rowOriginalAlphas[i];
        }
    }

    // Connected to the audio screen's buttons, using MainMenu's SFX source.
    public void PlayButtonSound()
    {
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayButtonSound();
    }

    public void Show()
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);
        else
            PlayAnimation();
    }

    public void Hide()
    {
        // Use this from your existing navigation code to close the screen.
        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        // Save once when closing, instead of writing to disk during every drag.
        PlayerPrefs.Save();
        if (!initialized)
            return;

        // Stop the repeating heartbeat and restore the designed layout.
        CancelAnimations();
        RestoreOriginalState();
    }
}