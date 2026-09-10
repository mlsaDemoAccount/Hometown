using System.Collections.Generic;
using UnityEngine;

// This screen owns its animations and calls the MainMenu SoundManager directly.
public class LevelUpScreen : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform rewardPanel;
    public RectTransform[] rewardElements;

    [Header("Panel Pop Animation")]
    [SerializeField] private float panelDuration = 0.5f;
    [SerializeField] private float panelStartScale = 0.92f;

    [Header("Elements Slide Animation")]
    [SerializeField] private float slideDistance = 25f;
    [SerializeField] private float slideDuration = 0.4f;
    [SerializeField] private float elementDelay = 0.1f;

    private Vector3 panelOriginalScale;
    private Vector2[] originalPositions;
    // Remember only this script's tweens, so other animations are not cancelled.
    private readonly List<int> tweenIds = new List<int>();
    private bool initialized;

    private void Awake()
    {
        if (rewardElements == null) rewardElements = new RectTransform[0];
        if (rewardPanel != null) panelOriginalScale = rewardPanel.localScale;
        originalPositions = new Vector2[rewardElements.Length];
        for (int i = 0; i < rewardElements.Length; i++)
        {
            if (rewardElements[i] != null) originalPositions[i] = rewardElements[i].anchoredPosition;
        }
        initialized = true;
    }

    private void OnEnable()
    {
        PlayAnimation(); // Runs again every time this screen is shown.
    }

    public void PlayAnimation()
    {
        if (!initialized || !isActiveAndEnabled) return;
        StopAnimations();
        RestoreLayout();

        // SoundManager was created in MainMenu and survives scene changes.
        if (SoundManager.Instance != null) SoundManager.Instance.PlayPanelOpenSound();
        AnimatePanel();
        AnimateElements();
    }

    private void AnimatePanel()
    {
        if (rewardPanel == null) return;
        rewardPanel.localScale = panelOriginalScale * panelStartScale;
        tweenIds.Add(LeanTween.scale(rewardPanel.gameObject, panelOriginalScale, panelDuration)
            .setEase(LeanTweenType.easeOutBack)
            .setIgnoreTimeScale(true).id);
    }

    private void AnimateElements()
    {
        // Reveal the reward caption and Collect button after the panel opens.
        for (int i = 0; i < rewardElements.Length; i++)
        {
            RectTransform element = rewardElements[i];
            if (element == null) continue;
            element.anchoredPosition = originalPositions[i] + Vector2.down * slideDistance;
            tweenIds.Add(LeanTween.move(element, originalPositions[i], slideDuration)
                .setDelay(0.1f + i * elementDelay)
                .setEase(LeanTweenType.easeOutCubic)
                .setIgnoreTimeScale(true).id); // Works even when the game is paused.
        }
    }

    // Connected to this screen's buttons in the Inspector, like MainMenu's On Click sounds.
    public void PlayButtonSound()
    {
        if (SoundManager.Instance != null) SoundManager.Instance.PlayButtonSound();
    }

    private void StopAnimations()
    {
        foreach (int id in tweenIds) LeanTween.cancel(id);
        tweenIds.Clear();
    }

    private void RestoreLayout()
    {
        if (rewardPanel != null) rewardPanel.localScale = panelOriginalScale;
        for (int i = 0; i < rewardElements.Length; i++)
        {
            if (rewardElements[i] != null) rewardElements[i].anchoredPosition = originalPositions[i];
        }
    }

    private void OnDisable()
    {
        if (!initialized) return;
        StopAnimations();
        RestoreLayout(); // Avoid drifting or shrinking when reopened during an animation.
    }
}
