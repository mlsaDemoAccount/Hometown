using UnityEngine.UI;
using TMPro;
using System.Collections;
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
    private int enabledFrame;

    [Header("Memories (found by name if left empty)")]
    public TMP_Text titleText;
    public TMP_Text subtitleText;
    public RectTransform listContent;
    public Button uploadButton;
    public Button viewDiaryButton;
    public Button closeButton;
    [Tooltip("Card for one memory (Memory.prefab). Empty = the first card already under Content is used as the template.")]
    public GameObject memoryCardPrefab;
    [SerializeField] private float cardHeight = 170f;

    private GameObject cardTemplate;
    private readonly List<GameObject> cards = new List<GameObject>();
    private readonly List<Sprite> cardSprites = new List<Sprite>();
    private Coroutine refreshRoutine;

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
        BindMemoryUi();
    }

    private void OnEnable()
    {
        enabledFrame = Time.frameCount;
        PlayAnimation(); // Runs again every time this screen is shown.
        RefreshMemories();
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

    // =========================================================
    // MEMORIES (per landmark)
    // =========================================================

    private void BindMemoryUi()
    {
        if (titleText == null) titleText = MemoryUi.Find<TMP_Text>(transform, "Title");
        if (subtitleText == null) subtitleText = MemoryUi.Find<TMP_Text>(transform, "Subtitle");
        if (listContent == null) listContent = MemoryUi.Find<RectTransform>(transform, "Content");
        if (uploadButton == null) uploadButton = MemoryUi.Find<Button>(transform, "Upload");
        if (viewDiaryButton == null) viewDiaryButton = MemoryUi.Find<Button>(transform, "View Diary");
        if (closeButton == null) closeButton = MemoryUi.Find<Button>(transform, "Close");

        if (uploadButton != null) uploadButton.onClick.AddListener(OnUploadClick);
        if (viewDiaryButton != null) viewDiaryButton.onClick.AddListener(OnViewDiaryClick);
        if (closeButton != null) closeButton.onClick.AddListener(Close);

        if (listContent != null)
        {
            // Template = the prefab, or the first sample card the designer left under Content.
            if (memoryCardPrefab == null && listContent.childCount > 0)
            {
                cardTemplate = listContent.GetChild(0).gameObject;
                cardTemplate.SetActive(false);
            }

            if (listContent.GetComponent<VerticalLayoutGroup>() == null)
            {
                var layout = listContent.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.spacing = 12f;
                layout.padding = new RectOffset(12, 12, 12, 12);
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
            }
            if (listContent.GetComponent<ContentSizeFitter>() == null)
            {
                var fitter = listContent.gameObject.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
        }
    }

    /// Opens this screen for a landmark (called by LandmarkMarker's button).
    public void Open(LandmarkMarker marker)
    {
        MemoryManager.Instance.CurrentLandmark = marker;
        if (gameObject.activeSelf) RefreshMemories(); // already open: OnEnable won't run again
        else gameObject.SetActive(true);              // OnEnable refreshes
    }

    public void Close()
    {
        PlayButtonSound();
        gameObject.SetActive(false);
    }

    private void Update()
    {
        // Ignore the press that opened this screen (the previous screen closes on the same key).
        if (Input.GetKeyDown(KeyCode.Escape) && Time.frameCount != enabledFrame) Close(); // Android back button
    }

    private void RefreshMemories()
    {
        var marker = MemoryManager.Instance.CurrentLandmark;
        if (marker == null) return;

        SetText(titleText, marker.displayName);
        ClearCards();
        if (refreshRoutine != null) StopCoroutine(refreshRoutine);

        if (!MemoryUi.IsLoggedIn)
        {
            SetText(subtitleText, "Log in to view and add memories.");
            SetButtons(false, false);
            return;
        }
        refreshRoutine = StartCoroutine(RefreshRoutine(marker));
    }

    private IEnumerator RefreshRoutine(LandmarkMarker marker)
    {
        SetText(subtitleText, "Loading memories...");
        SetButtons(false, false);

        var handle = MemoryManager.Instance.LoadMemories(marker.landmarkId);
        while (!handle.isDone) yield return null;

        if (!handle.success)
        {
            SetText(subtitleText, handle.message);
            SetButtons(false, false);
            yield break;
        }

        int count = handle.items.Count;
        SetText(subtitleText, count == 0 ? "No memories yet. Tap Upload to add one."
                            : count == 1 ? "1 memory uploaded" : count + " memories uploaded");
        SetButtons(true, count > 0);
        BuildCards(handle.items);
    }

    private void BuildCards(List<MemoryEntry> items)
    {
        if (listContent == null) return;
        GameObject template = memoryCardPrefab != null ? memoryCardPrefab : cardTemplate;
        if (template == null) return;

        for (int i = 0; i < items.Count; i++)
        {
            int index = i;
            var entry = items[i];
            var card = Instantiate(template, listContent);
            card.SetActive(true);
            card.name = "Memory " + (i + 1);
            cards.Add(card);

            SetText(MemoryUi.Find<TMP_Text>(card.transform, "Title"), entry.title);
            SetText(MemoryUi.Find<TMP_Text>(card.transform, "Date"), entry.DisplayDate);

            var photo = MemoryUi.Find<Image>(card.transform, "Photo");
            if (photo != null) photo.enabled = false; // hide the sample picture until the real one loads

            var layoutElement = card.GetComponent<LayoutElement>();
            if (layoutElement == null) layoutElement = card.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = cardHeight;

            var button = card.GetComponent<Button>();
            if (button == null) button = card.AddComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OpenDiary(index));

            StartCoroutine(LoadCardPhoto(entry, photo));
        }
    }

    private IEnumerator LoadCardPhoto(MemoryEntry entry, Image photo)
    {
        var handle = MemoryManager.Instance.LoadTexture(entry);
        while (!handle.isDone) yield return null;
        if (photo == null || handle.texture == null) yield break; // card was cleared / photo failed
        var sprite = MemoryUi.SetPhoto(photo, handle.texture);
        if (sprite != null) cardSprites.Add(sprite);
    }

    private void ClearCards()
    {
        foreach (var card in cards) if (card != null) Destroy(card);
        cards.Clear();
        foreach (var sprite in cardSprites) if (sprite != null) Destroy(sprite);
        cardSprites.Clear();

        // Sample cards the designer left in the prefab (everything except the hidden template).
        if (listContent != null)
            for (int i = listContent.childCount - 1; i >= 0; i--)
            {
                var child = listContent.GetChild(i).gameObject;
                if (child != cardTemplate && !cards.Contains(child)) Destroy(child);
            }
    }

    private void SetButtons(bool uploadEnabled, bool diaryEnabled)
    {
        if (uploadButton != null) uploadButton.interactable = uploadEnabled;
        if (viewDiaryButton != null) viewDiaryButton.interactable = diaryEnabled;
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null) text.text = value ?? "";
    }

    // ---------- Navigation ----------

    public void OnUploadClick()
    {
        var screen = MemoryUi.FindScreen<NewMemoryScreen>();
        if (screen == null) return;
        PlayButtonSound();
        gameObject.SetActive(false);
        screen.Open();
    }

    public void OnViewDiaryClick() => OpenDiary(0);

    private void OpenDiary(int index)
    {
        var screen = MemoryUi.FindScreen<MemoryDiaryScreen>();
        if (screen == null) return;
        PlayButtonSound();
        MemoryManager.Instance.CurrentIndex = index;
        gameObject.SetActive(false);
        screen.Open();
    }
}
