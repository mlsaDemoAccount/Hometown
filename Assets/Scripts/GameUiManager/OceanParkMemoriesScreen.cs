using System.Collections.Generic;
using UnityEngine;

// This screen owns its animations and calls the MainMenu SoundManager directly.
public class OceanParkMemoriesScreen : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform memoriesPanel;
    public RectTransform[] memoryCards;

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
        if (memoryCards == null) memoryCards = new RectTransform[0];
        if (memoriesPanel != null) panelOriginalScale = memoriesPanel.localScale;
        originalPositions = new Vector2[memoryCards.Length];
        for (int i = 0; i < memoryCards.Length; i++)
        {
            if (memoryCards[i] != null) originalPositions[i] = memoryCards[i].anchoredPosition;
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
        if (memoriesPanel == null) return;
        memoriesPanel.localScale = panelOriginalScale * panelStartScale;
        tweenIds.Add(LeanTween.scale(memoriesPanel.gameObject, panelOriginalScale, panelDuration)
            .setEase(LeanTweenType.easeOutBack)
            .setIgnoreTimeScale(true).id);
    }

    private void AnimateElements()
    {
        // Reveal the memory cards and action buttons one by one.
        for (int i = 0; i < memoryCards.Length; i++)
        {
            RectTransform element = memoryCards[i];
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
        if (memoriesPanel != null) memoriesPanel.localScale = panelOriginalScale;
        for (int i = 0; i < memoryCards.Length; i++)
        {
            if (memoryCards[i] != null) memoryCards[i].anchoredPosition = originalPositions[i];
        }
    }

    private void OnDisable()
    {
        if (!initialized) return;
        StopAnimations();
        RestoreLayout(); // Avoid drifting or shrinking when reopened during an animation.
    }
}
