using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// This screen owns its animations and calls the MainMenu SoundManager directly.
public class MemoryDiaryScreen : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform diaryPanel;
    public RectTransform[] pageElements;

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
    private int enabledFrame;

    [Header("Diary (found by name if left empty)")]
    public Image mainPhoto;          // "Family Photo (1)" - the large photo
    public Image smallPhoto;         // "Family Photo" - the small one under the story
    public TMP_Text captionText;     // memory title ("Memory Caption")
    public TMP_Text storyText;       // memory description ("Story")
    public Button previousButton;
    public Button nextButton;
    public Button closeButton;

    private Sprite photoSprite;
    private Sprite photoPlaceholder;
    private Sprite smallPlaceholder;
    private Coroutine photoRoutine;

    private void Awake()
    {
        if (pageElements == null) pageElements = new RectTransform[0];
        if (diaryPanel != null) panelOriginalScale = diaryPanel.localScale;
        originalPositions = new Vector2[pageElements.Length];
        for (int i = 0; i < pageElements.Length; i++)
        {
            if (pageElements[i] != null) originalPositions[i] = pageElements[i].anchoredPosition;
        }
        initialized = true;
        BindMemoryUi();
    }

    private void OnEnable()
    {
        enabledFrame = Time.frameCount;
        PlayAnimation(); // Runs again every time this screen is shown.
        ShowCurrent();
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
        if (diaryPanel == null) return;
        diaryPanel.localScale = panelOriginalScale * panelStartScale;
        tweenIds.Add(LeanTween.scale(diaryPanel.gameObject, panelOriginalScale, panelDuration)
            .setEase(LeanTweenType.easeOutBack)
            .setIgnoreTimeScale(true).id);
    }

    private void AnimateElements()
    {
        // Bring the diary content and page arrows into view.
        for (int i = 0; i < pageElements.Length; i++)
        {
            RectTransform element = pageElements[i];
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
        if (diaryPanel != null) diaryPanel.localScale = panelOriginalScale;
        for (int i = 0; i < pageElements.Length; i++)
        {
            if (pageElements[i] != null) pageElements[i].anchoredPosition = originalPositions[i];
        }
    }

    private void OnDisable()
    {
        if (!initialized) return;
        StopAnimations();
        RestoreLayout(); // Avoid drifting or shrinking when reopened during an animation.
        ReleaseSprite();
    }

    // =========================================================
    // DIARY
    // =========================================================

    private void BindMemoryUi()
    {
        if (mainPhoto == null) mainPhoto = MemoryUi.Find<Image>(transform, "Family Photo (1)");
        if (smallPhoto == null) smallPhoto = MemoryUi.Find<Image>(transform, "Family Photo");
        if (captionText == null) captionText = MemoryUi.Find<TMP_Text>(transform, "Memory Caption");
        if (storyText == null) storyText = MemoryUi.Find<TMP_Text>(transform, "Story");
        if (previousButton == null) previousButton = MemoryUi.Find<Button>(transform, "Previous Memory");
        if (nextButton == null) nextButton = MemoryUi.Find<Button>(transform, "Next Memory");
        if (closeButton == null) closeButton = MemoryUi.Find<Button>(transform, "Close");

        if (previousButton != null) previousButton.onClick.AddListener(OnPreviousClick);
        if (nextButton != null) nextButton.onClick.AddListener(OnNextClick);
        if (closeButton != null) closeButton.onClick.AddListener(Close);

        if (mainPhoto != null) photoPlaceholder = mainPhoto.sprite;
        if (smallPhoto != null) smallPlaceholder = smallPhoto.sprite;
    }

    /// Opens the diary at MemoryManager.CurrentIndex.
    public void Open()
    {
        if (gameObject.activeSelf) ShowCurrent(); // already open: OnEnable won't run again
        else gameObject.SetActive(true);          // OnEnable shows the memory
    }

    /// Back to the landmark's memories list.
    public void Close()
    {
        PlayButtonSound();
        gameObject.SetActive(false);
        var list = MemoryUi.FindScreen<OceanParkMemoriesScreen>();
        if (list != null) list.Open(MemoryManager.Instance.CurrentLandmark);
    }

    private void Update()
    {
        // Ignore the press that opened this screen (the previous screen closes on the same key).
        if (Input.GetKeyDown(KeyCode.Escape) && Time.frameCount != enabledFrame) Close(); // Android back button
    }

    public void OnPreviousClick() => Step(-1);
    public void OnNextClick() => Step(1);

    private void Step(int delta)
    {
        var marker = MemoryManager.Instance.CurrentLandmark;
        if (marker == null) return;
        int count = MemoryManager.Instance.GetCached(marker.landmarkId).Count;
        int target = Mathf.Clamp(MemoryManager.Instance.CurrentIndex + delta, 0, Mathf.Max(0, count - 1));
        if (target == MemoryManager.Instance.CurrentIndex) return;

        PlayButtonSound();
        MemoryManager.Instance.CurrentIndex = target;
        ShowCurrent();
    }

    private void ShowCurrent()
    {
        var marker = MemoryManager.Instance.CurrentLandmark;
        var list = marker != null ? MemoryManager.Instance.GetCached(marker.landmarkId) : null;
        if (photoRoutine != null) StopCoroutine(photoRoutine);
        ReleaseSprite();
        // Hide the prefab's sample pictures while the real photo loads.
        if (mainPhoto != null) mainPhoto.enabled = false;
        if (smallPhoto != null) smallPhoto.enabled = false;

        if (list == null || list.Count == 0)
        {
            if (captionText != null) captionText.text = "No memories yet";
            if (storyText != null) storyText.text = "";
            if (previousButton != null) previousButton.interactable = false;
            if (nextButton != null) nextButton.interactable = false;
            return;
        }

        int index = Mathf.Clamp(MemoryManager.Instance.CurrentIndex, 0, list.Count - 1);
        MemoryManager.Instance.CurrentIndex = index;
        var entry = list[index];

        if (captionText != null) captionText.text = entry.title;
        if (storyText != null) storyText.text = entry.description;
        if (previousButton != null) previousButton.interactable = index > 0;
        if (nextButton != null) nextButton.interactable = index < list.Count - 1;

        photoRoutine = StartCoroutine(LoadPhoto(entry));
    }

    private IEnumerator LoadPhoto(MemoryEntry entry)
    {
        var handle = MemoryManager.Instance.LoadTexture(entry);
        while (!handle.isDone) yield return null;
        if (handle.texture == null) yield break;

        photoSprite = MemoryUi.SetPhoto(mainPhoto, handle.texture);
        if (smallPhoto != null)
        {
            smallPhoto.sprite = photoSprite;
            smallPhoto.preserveAspect = true;
            smallPhoto.enabled = true;
        }
    }

    /// Frees the current sprite and restores the prefab's placeholder pictures.
    private void ReleaseSprite()
    {
        if (mainPhoto != null) { mainPhoto.sprite = photoPlaceholder; mainPhoto.enabled = true; }
        if (smallPhoto != null) { smallPhoto.sprite = smallPlaceholder; smallPhoto.enabled = true; }
        if (photoSprite != null) Destroy(photoSprite);
        photoSprite = null;
    }
}
