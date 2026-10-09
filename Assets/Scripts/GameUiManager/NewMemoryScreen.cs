using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// This screen owns its animations and calls the MainMenu SoundManager directly.
public class NewMemoryScreen : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform memoryPanel;
    public RectTransform[] formElements;

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

    [Header("New memory (found by name if left empty)")]
    public TMP_Text titleText;
    public Button captureButton;     // "Capture Photo" - phone camera
    public Button galleryButton;     // "Upload Photo" - photo library
    public Image photoPreview;
    public TMP_InputField titleInput;
    public TMP_InputField descriptionInput;
    public Button saveButton;
    public TMP_Text statusText;
    [Tooltip("Optional. Empty = a button named 'Close' if this screen has one. The Android back button always works.")]
    public Button closeButton;

    private Texture2D pickedPhoto;
    private Sprite previewSprite;
    private Sprite previewPlaceholder;
    private Color previewPlaceholderColor;
    private bool busy;

    private void Awake()
    {
        if (formElements == null) formElements = new RectTransform[0];
        if (memoryPanel != null) panelOriginalScale = memoryPanel.localScale;
        originalPositions = new Vector2[formElements.Length];
        for (int i = 0; i < formElements.Length; i++)
        {
            if (formElements[i] != null) originalPositions[i] = formElements[i].anchoredPosition;
        }
        initialized = true;
        BindMemoryUi();
    }

    private void OnEnable()
    {
        enabledFrame = Time.frameCount;
        PlayAnimation(); // Runs again every time this screen is shown.
        ResetForm();
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
        if (memoryPanel == null) return;
        memoryPanel.localScale = panelOriginalScale * panelStartScale;
        tweenIds.Add(LeanTween.scale(memoryPanel.gameObject, panelOriginalScale, panelDuration)
            .setEase(LeanTweenType.easeOutBack)
            .setIgnoreTimeScale(true).id);
    }

    private void AnimateElements()
    {
        // Reveal the photo controls, description field, and Save button in order.
        for (int i = 0; i < formElements.Length; i++)
        {
            RectTransform element = formElements[i];
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
        if (memoryPanel != null) memoryPanel.localScale = panelOriginalScale;
        for (int i = 0; i < formElements.Length; i++)
        {
            if (formElements[i] != null) formElements[i].anchoredPosition = originalPositions[i];
        }
    }

    private void OnDisable()
    {
        if (!initialized) return;
        StopAnimations();
        RestoreLayout(); // Avoid drifting or shrinking when reopened during an animation.
        ReleasePhoto();
    }

    // =========================================================
    // NEW MEMORY
    // =========================================================

    private void BindMemoryUi()
    {
        if (titleText == null) titleText = MemoryUi.Find<TMP_Text>(transform, "Title");
        if (captureButton == null) captureButton = MemoryUi.Find<Button>(transform, "Capture Photo");
        if (galleryButton == null) galleryButton = MemoryUi.Find<Button>(transform, "Upload Photo");
        if (photoPreview == null) photoPreview = MemoryUi.Find<Image>(transform, "Photo Preview");
        if (titleInput == null) titleInput = MemoryUi.Find<TMP_InputField>(transform, "Title Input");
        if (descriptionInput == null) descriptionInput = MemoryUi.Find<TMP_InputField>(transform, "Description Input");
        if (saveButton == null) saveButton = MemoryUi.Find<Button>(transform, "Save Memory");
        if (statusText == null) statusText = MemoryUi.Find<TMP_Text>(transform, "Status");
        if (closeButton == null) closeButton = MemoryUi.Find<Button>(transform, "Close");

        if (captureButton != null) captureButton.onClick.AddListener(OnCaptureClick);
        if (galleryButton != null) galleryButton.onClick.AddListener(OnGalleryClick);
        if (saveButton != null) saveButton.onClick.AddListener(OnSaveClick);
        if (closeButton != null) closeButton.onClick.AddListener(Close);

        if (photoPreview != null)
        {
            previewPlaceholder = photoPreview.sprite;
            previewPlaceholderColor = photoPreview.color;
        }
    }

    public void Open()
    {
        gameObject.SetActive(true); // OnEnable resets the form
    }

    /// Back to the landmark's memories list.
    public void Close()
    {
        if (busy) return;
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

    private void ResetForm()
    {
        busy = false;
        ReleasePhoto();
        if (titleInput != null) titleInput.text = "";
        if (descriptionInput != null) descriptionInput.text = "";
        SetBusy(false);
        SetStatus("");
        var marker = MemoryManager.Instance.CurrentLandmark;
        if (titleText != null && marker != null) titleText.text = marker.displayName;
    }

    public void OnCaptureClick()
    {
        if (busy) return;
        PlayButtonSound();
        StartCoroutine(PickRoutine(PhotoPicker.TakePhoto()));
    }

    public void OnGalleryClick()
    {
        if (busy) return;
        PlayButtonSound();
        StartCoroutine(PickRoutine(PhotoPicker.PickFromGallery()));
    }

    private IEnumerator PickRoutine(PhotoPickHandle handle)
    {
        while (!handle.isDone) yield return null;

        if (handle.texture == null)
        {
            SetStatus("No photo selected. If you denied access, allow Camera / Photos in your phone's Settings.");
            yield break;
        }

        ReleasePhoto();
        pickedPhoto = handle.texture;
        if (photoPreview != null)
        {
            previewSprite = MemoryUi.SetPhoto(photoPreview, pickedPhoto);
        }
        SetStatus("");
    }

    public void OnSaveClick()
    {
        if (busy) return;

        string title = titleInput != null ? titleInput.text.Trim() : "";
        string description = descriptionInput != null ? descriptionInput.text.Trim() : "";
        var marker = MemoryManager.Instance.CurrentLandmark;

        if (marker == null) { SetStatus("No landmark selected."); return; }
        if (pickedPhoto == null) { SetStatus("Add a photo first."); return; }
        if (title.Length == 0) { SetStatus("Please enter a title."); return; }

        PlayButtonSound();
        StartCoroutine(SaveRoutine(marker, title, description));
    }

    private IEnumerator SaveRoutine(LandmarkMarker marker, string title, string description)
    {
        busy = true;
        SetBusy(true);
        SetStatus("Saving memory...");

        byte[] jpg = pickedPhoto.EncodeToJPG(85);
        var handle = MemoryManager.Instance.AddMemory(marker.landmarkId, title, description, jpg);
        while (!handle.isDone) yield return null;

        busy = false;
        SetBusy(false);

        if (!handle.success)
        {
            SetStatus(handle.message);
            yield break;
        }

        Close(); // back to the list - the new memory is already cached at the top
    }

    private void SetBusy(bool value)
    {
        if (saveButton != null) saveButton.interactable = !value;
        if (captureButton != null) captureButton.interactable = !value;
        if (galleryButton != null) galleryButton.interactable = !value;
    }

    private void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message ?? "";
    }

    /// Frees the picked photo + preview sprite and puts the placeholder back.
    private void ReleasePhoto()
    {
        if (previewSprite != null) Destroy(previewSprite);
        if (pickedPhoto != null) Destroy(pickedPhoto);
        previewSprite = null;
        pickedPhoto = null;
        if (photoPreview != null)
        {
            photoPreview.sprite = previewPlaceholder;
            photoPreview.color = previewPlaceholderColor;
        }
    }
}
