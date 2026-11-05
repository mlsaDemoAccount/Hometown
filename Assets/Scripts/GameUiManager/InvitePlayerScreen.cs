//using Unity.Android.Gradle;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InvitePlayerScreen : MonoBehaviour
{
    public Image logoImg;
    public GameObject[] menuButtons;

    [Header("Back + connection status")]
    [Tooltip("Returns to the main menu.")]
    [SerializeField] private Button backButton;
    [Tooltip("The banner's big line ('Connected Network').")]
    [SerializeField] private TMP_Text networkTitleText;
    [Tooltip("The banner's small line ('Status : Connected').")]
    [SerializeField] private TMP_Text networkStatusText;
    [Tooltip("The orange banner picture - turns grey when there is no connection.")]
    [SerializeField] private Image networkBanner;


    [Header("Logo Beat Animation")]
    [SerializeField] private float logoBeatAmount = 1.05f;
    [SerializeField] private float logoBeatDuration = 1.5f;
    private Vector3 logoOriginalScale;
    private Vector3 panelOriginalScale;

    [Header("Menu Buttons Slide Animation")]
    [SerializeField] private float menuSlideDistance = 500f;
    [SerializeField] private float menuSlideDuration = 0.6f;
    [SerializeField] private float menuButtonDelay = 0.12f;




    void Awake()
    {
        if (backButton != null) backButton.onClick.AddListener(OnBackClick);
    }

    private void OnBackClick()
    {
        if (CityLauncher.Instance != null && (CityLauncher.Instance.Busy || CityLauncher.Instance.Active)) CityLauncher.Instance.Cancel();
        GameUIManager.Instance.InvitePlayerScreenToggle(false);
        GameUIManager.Instance.MainMenuScreenToggle(true);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        AnimateLogo();
        AnimateMenuButtons();

    }

    // Update is called once per frame
    void Update()
    {
        UpdateNetworkStatus();
        if (renameRoot != null && renameRoot.activeSelf) return;
        foreach (var row in rows)
            if (row.cityId != null && row.longPress != null && row.longPress.ConsumeLongPress()) { OpenRename(row.cityId, row.cityName); break; }
    }

    // -----------------------------
    // LOGO SLOW BEATING ANIMATION
    // -----------------------------

    private void AnimateLogo()
    {
        if (logoImg == null)
            return;

        RectTransform logo = logoImg.GetComponent<RectTransform>();

        logoOriginalScale = logo.localScale;

        LeanTween.scale(
            logo.gameObject,
            logoOriginalScale * logoBeatAmount,
            logoBeatDuration
        )
        .setEase(LeanTweenType.easeInOutSine)
        .setLoopPingPong();
    }

    // =========================================================
    // MENU BUTTONS SLIDE FROM LEFT ONE BY ONE
    // =========================================================

    private void AnimateMenuButtons()
    {
        if (menuButtons == null)
            return;

        for (int i = 0; i < menuButtons.Length; i++)
        {
            if (menuButtons[i] == null)
                continue;

            RectTransform button =
                menuButtons[i].GetComponent<RectTransform>();

            // Store final position
            Vector2 targetPosition = button.anchoredPosition;

            // Start from the LEFT
            button.anchoredPosition =
                targetPosition + Vector2.left * menuSlideDistance;

            // Slide to final position
            LeanTween.move(
                button,
                targetPosition,
                menuSlideDuration
            )
            .setDelay(i * menuButtonDelay)
            .setEase(LeanTweenType.easeOutCubic);
        }
    }
    /// Kept only because the prefab's buttons still point here (they also play the click sound).
    /// What a row's button does is set per row, see Fill().
    public void OnInviteBtnClick() { }

    // =========================================================
    // PLAYERS LIST - cities you share with friends, with their online state
    // =========================================================

    private class Row
    {
        public GameObject root;
        public Button button;
        public TMP_Text nameText, statusText;
        public Image buttonImage;
        public Sprite inviteSprite;   // the prefab's own "INVITE" button art (the text is part of the picture)
        public Color inviteColor;
        public TMP_Text playLabel;    // made at runtime, only shown on Play rows
        public List<TMP_Text> staticStatusLabels = new List<TMP_Text>(); // the fixed "Status:" captions
        public Image avatarImage;     // made at runtime, left of the name
        public TMP_Text avatarInitial;
        public LongPressRow longPress;
        public string cityId;         // the city this row shows (null for the "New friend" row)
        public string cityName;
    }

    private static readonly Color OnlineColor = new Color(0.25f, 0.8f, 0.35f);
    private static readonly Color OfflineColor = new Color(0.6f, 0.6f, 0.6f);
    private const float RefreshSeconds = 8f;

    [Tooltip("Optional: button art that says PLAY. Without it, Play rows use a plain orange button with a PLAY label.")]
    [SerializeField] private Sprite playButtonSprite;

    private readonly List<Row> rows = new List<Row>();

    void OnEnable()
    {
        BindRows();
        StartCoroutine(RefreshLoop());
    }

    /// Finds the three player rows (P1..P3) in the prefab by their child names, top row first.
    private void BindRows()
    {
        if (rows.Count > 0) return;
        var panel = transform.Find("Panel");
        if (panel == null) { Debug.LogWarning("[InvitePlayerScreen] No 'Panel' with rows found."); return; }

        var found = new List<RectTransform>();
        foreach (Transform child in panel)
            if (child.name.StartsWith("P") && child.name.Length == 2) found.Add((RectTransform)child);
        found.Sort((a, b) => b.anchoredPosition.y.CompareTo(a.anchoredPosition.y)); // highest first
        if (found.Count > 0) found = MakeScrollable((RectTransform)panel, found);

        foreach (var rt in found)
        {
            var row = new Row { root = rt.gameObject };
            foreach (Transform c in rt)
            {
                if (c.name.StartsWith("InviteButton")) { row.button = c.GetComponent<Button>(); row.buttonImage = c.GetComponent<Image>(); }
                else if (c.name.StartsWith("Connected Text")) row.statusText = c.GetComponent<TMP_Text>();
                else if (c.name.StartsWith("Status")) { var t = c.GetComponent<TMP_Text>(); if (t != null) row.staticStatusLabels.Add(t); }
                else if (c.name.StartsWith("Text (TMP)") && row.nameText == null) row.nameText = c.GetComponent<TMP_Text>();
            }
            if (row.button != null && row.nameText != null && row.statusText != null)
            {
                row.avatarImage = FindAvatarArt(rt);
                if (row.buttonImage != null) { row.inviteSprite = row.buttonImage.sprite; row.inviteColor = row.buttonImage.color; }
                row.playLabel = MakePlayLabel(row);
                if (row.avatarImage == null) MakeAvatar(row);
                row.longPress = row.root.GetComponent<LongPressRow>();
                if (row.longPress == null) row.longPress = row.root.AddComponent<LongPressRow>();
                rows.Add(row);
            }
        }

        // The old caption talked about a shared Wi-Fi network.
        foreach (var t in GetComponentsInChildren<TMP_Text>(true))
            if (!string.IsNullOrEmpty(t.text) && t.text.Contains("Wi-Wi")) t.text = "Friends you've played with (hold a row to rename the city)";
    }

    private const int MaxRows = 12;      // cities that fit in the list (it scrolls)
    private const float RowPitch = 139f; // distance between two rows
    private const float RowPad = 2f;

    private RectTransform rowsContent;
    private RectTransform rowsViewport;

    /// Puts the prefab's 3 rows in a scroll view and adds copies of the last one, so any number
    /// of cities fit. Returns all rows, top first.
    private List<RectTransform> MakeScrollable(RectTransform panel, List<RectTransform> found)
    {
        var viewportGo = new GameObject("Rows Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
        viewportGo.transform.SetParent(panel, false);
        rowsViewport = (RectTransform)viewportGo.transform;
        rowsViewport.anchorMin = Vector2.zero; rowsViewport.anchorMax = Vector2.one;
        rowsViewport.offsetMin = rowsViewport.offsetMax = Vector2.zero;
        var hit = viewportGo.GetComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f); // invisible, but catches drags between rows

        var contentGo = new GameObject("Rows Content", typeof(RectTransform));
        contentGo.transform.SetParent(rowsViewport, false);
        rowsContent = (RectTransform)contentGo.transform;
        rowsContent.anchorMin = new Vector2(0f, 1f); rowsContent.anchorMax = new Vector2(1f, 1f);
        rowsContent.pivot = new Vector2(0.5f, 1f);
        rowsContent.anchoredPosition = Vector2.zero;

        var scroll = viewportGo.AddComponent<ScrollRect>();
        scroll.viewport = rowsViewport;
        scroll.content = rowsContent;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.scrollSensitivity = 40f;

        var template = found[found.Count - 1];
        var all = new List<RectTransform>(found);
        while (all.Count < MaxRows)
        {
            var copy = Instantiate(template.gameObject, panel);
            copy.name = "P" + (all.Count + 1);
            all.Add((RectTransform)copy.transform);
        }
        for (int i = 0; i < all.Count; i++)
        {
            var rt = all[i];
            rt.SetParent(rowsContent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -(RowPad + rt.rect.height * 0.5f + i * RowPitch));
        }
        SetContentHeight(all.Count);
        return all;
    }

    private void SetContentHeight(int visibleRows)
    {
        if (rowsContent == null || rowsViewport == null || rows.Count == 0) return;
        float rowHeight = ((RectTransform)rows[0].root.transform).rect.height;
        float height = RowPad * 2f + rowHeight + Mathf.Max(0, visibleRows - 1) * RowPitch;
        rowsContent.sizeDelta = new Vector2(0f, Mathf.Max(height, rowsViewport.rect.height));
    }

    /// The prefab already has a picture slot per row (AvatarImg); use it instead of making one.
    private static Image FindAvatarArt(Transform row)
    {
        foreach (Transform c in row)
            if (c.name.StartsWith("AvatarImg")) return c.GetComponent<Image>();
        return null;
    }

    /// A round picture just left of the friend's name: their profile avatar, or a coloured circle
    /// with their first letter until avatar sprites are assigned on the Profile screen.
    private static void MakeAvatar(Row row)
    {
        var go = new GameObject("Avatar", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(row.root.transform, false);
        var rt = (RectTransform)go.transform;
        var name = row.nameText.rectTransform;
        const float size = 56f;
        rt.anchorMin = name.anchorMin; rt.anchorMax = name.anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        float leftEdge = name.anchoredPosition.x - name.sizeDelta.x * name.pivot.x;
        float centerY = name.anchoredPosition.y + name.sizeDelta.y * (0.5f - name.pivot.y);
        rt.anchoredPosition = new Vector2(leftEdge - 10f - size * 0.5f, centerY);

        row.avatarImage = go.GetComponent<Image>();
        row.avatarImage.sprite = PlayerIdentity.Disc;
        row.avatarImage.raycastTarget = false;

        var textGo = new GameObject("Initial", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);
        var trt = (RectTransform)textGo.transform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
        row.avatarInitial = textGo.GetComponent<TextMeshProUGUI>();
        row.avatarInitial.font = row.nameText.font;
        row.avatarInitial.fontSize = 28; row.avatarInitial.fontStyle = FontStyles.Bold;
        row.avatarInitial.alignment = TextAlignmentOptions.Center;
        row.avatarInitial.color = Color.white;
        row.avatarInitial.raycastTarget = false;
        go.SetActive(false);
    }

    private static void ShowAvatar(Row row, ParticipantDto partner)
    {
        if (row.avatarImage == null) return;
        row.avatarImage.gameObject.SetActive(partner != null);
        if (partner == null) return;
        if (row.avatarInitial == null) row.avatarInitial = PlayerIdentity.EnsureInitial(row.avatarImage, row.nameText.font);
        PlayerIdentity.ApplyAvatar(row.avatarImage, row.avatarInitial, partner.playerImageIndex, partner.firstName);
    }

    private static TMP_Text MakePlayLabel(Row row)
    {
        // a text the designer put under the button is used as is (its text is set to Invite / Play)
        var placed = row.button.GetComponentInChildren<TMP_Text>(true);
        if (placed != null) return placed;

        var go = new GameObject("PlayLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(row.button.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var label = go.GetComponent<TextMeshProUGUI>();
        label.font = row.nameText.font;
        label.fontSize = 26; label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        label.text = "PLAY";
        go.SetActive(false);
        return label;
    }

    /// Invite rows keep the prefab's "INVITE" art; Play rows show PLAY instead.
    private void StyleButton(Row row, bool play, bool enabled = true)
    {
        if (row.buttonImage != null)
        {
            if (!play) { row.buttonImage.sprite = row.inviteSprite; row.buttonImage.color = row.inviteColor; }
            else if (playButtonSprite != null) { row.buttonImage.sprite = playButtonSprite; row.buttonImage.color = Color.white; }
            else { row.buttonImage.sprite = null; row.buttonImage.color = new Color(0.95f, 0.55f, 0.1f); }
        }
        // a label placed in the scene always shows; the runtime-made one only when there is no art for Play
        bool placed = row.playLabel.name != "PlayLabel";
        row.playLabel.gameObject.SetActive(placed || (play && playButtonSprite == null));
        row.playLabel.text = play ? "PLAY" : "INVITE";

        // an offline friend can't be played with: the button is greyed out and does nothing
        row.button.interactable = enabled;
        if (!enabled && row.buttonImage != null) row.buttonImage.color *= new Color(0.5f, 0.5f, 0.5f, 1f);
        row.playLabel.alpha = enabled ? 1f : 0.5f;
    }

    private enum ListState { Connecting, Ok, Failed }
    private ListState listState = ListState.Connecting;
    private float nextStatusUpdate;

    /// The banner follows the real connection: no internet, server not answering, or connected.
    private void UpdateNetworkStatus()
    {
        if (Time.unscaledTime < nextStatusUpdate) return;
        nextStatusUpdate = Time.unscaledTime + 0.5f;

        string title, status;
        bool good = false;
        if (Application.internetReachability == NetworkReachability.NotReachable) { title = "No Network"; status = "Status : No internet"; }
        else if (listState == ListState.Failed) { title = "Network Problem"; status = "Status : Server not reachable"; }
        else if (listState == ListState.Connecting) { title = "Connecting..."; status = "Status : Connecting"; }
        else { title = "Connected Network"; status = "Status : Connected"; good = true; }

        if (networkTitleText != null) networkTitleText.text = title;
        if (networkStatusText != null) networkStatusText.text = status;
        if (networkBanner != null) networkBanner.color = good ? Color.white : new Color(0.65f, 0.65f, 0.65f, 1f);
    }

    private IEnumerator RefreshLoop()
    {
        listState = ListState.Connecting;
        while (true)
        {
            var call = CityApi.Backend.GetMyCities();
            while (!call.isDone) yield return null;
            if (call.ok) { listState = ListState.Ok; Fill(call.result.cities); }
            else { listState = ListState.Failed; Debug.LogWarning("[InvitePlayerScreen] Couldn't load cities: " + call.error); }
            float until = Time.unscaledTime + RefreshSeconds;
            refreshNow = false;
            while (Time.unscaledTime < until && !refreshNow) yield return null;
        }
    }

    private bool refreshNow;

    private void Fill(CityDto[] cities)
    {
        string me = CityApi.Backend.MyUserId;
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            row.button.onClick.RemoveAllListeners(); // runtime listeners only; the prefab's are untouched
            row.nameText.alpha = 1f;

            if (i < cities.Length)
            {
                var city = cities[i];
                var partner = city.Partner(me);
                row.root.SetActive(true);
                SetStatusCaption(row, true);
                row.cityId = city.cityId;
                row.cityName = city.name;
                ShowAvatar(row, partner);
                if (partner == null)
                {
                    row.nameText.text = "Waiting for a friend";
                    row.statusText.text = "Invite pending";
                    row.statusText.color = OfflineColor;
                    StyleButton(row, false);
                    string id = city.cityId;
                    row.button.onClick.AddListener(() => OpenInvite(id));
                }
                else
                {
                    bool renamed = !string.IsNullOrEmpty(city.name) && city.name != "Our city";
                    row.nameText.text = renamed ? city.name : partner.firstName;
                    row.statusText.text = (renamed ? partner.firstName + " - " : "") + (partner.online ? "Online" : "Offline");
                    row.statusText.color = partner.online ? OnlineColor : OfflineColor;
                    StyleButton(row, true, partner.online);
                    if (!partner.online) row.nameText.alpha = 0.55f;
                    else
                    {
                        string id = city.cityId;
                        row.button.onClick.AddListener(() => Play(id));
                    }
                }
            }
            else if (i == cities.Length)
            {
                // the next free slot: start something new with another friend
                row.root.SetActive(true);
                SetStatusCaption(row, false);
                row.cityId = null;
                ShowAvatar(row, null);
                row.nameText.text = "New friend";
                row.statusText.text = "";
                StyleButton(row, false);
                row.button.onClick.AddListener(OpenNewFriend);
            }
            else { row.cityId = null; row.root.SetActive(false); }
        }
        SetContentHeight(Mathf.Min(cities.Length + 1, rows.Count));
    }

    private static void SetStatusCaption(Row row, bool visible)
    {
        foreach (var t in row.staticStatusLabels) t.gameObject.SetActive(visible);
    }

    private void OpenNewFriend()
    {
        // The Host / Join screen: invite a friend (QR + code) or enter a friend's code.
        if (MultiplayerTestUI.Instance != null) MultiplayerTestUI.Instance.Open();
        else Debug.LogWarning("[InvitePlayerScreen] Co-op isn't set up in this scene (Hometown > Multiplayer > Add Co-op To Main Menu Scene).");
    }

    private void OpenInvite(string cityId)
    {
        if (MultiplayerTestUI.Instance == null) { OpenNewFriend(); return; }
        MultiplayerTestUI.Instance.Open();
        CityLauncher.Instance.BeginNewCity(cityId);
    }

    private void Play(string cityId)
    {
        if (MultiplayerTestUI.Instance == null) { OpenNewFriend(); return; }
        MultiplayerTestUI.Instance.Open();
        CityLauncher.Instance.BeginPlay(cityId);
    }

    // =========================================================
    // RENAME A CITY (hold a row) - a small popup made at runtime
    // =========================================================

    private GameObject renameRoot;
    private TMP_InputField renameInput;
    private TMP_Text renameMessage;
    private string renameCityId;
    private bool renameBusy;

    private void OpenRename(string cityId, string currentName)
    {
        if (renameRoot == null) BuildRenamePopup();
        renameCityId = cityId;
        renameInput.text = currentName == "Our city" ? "" : currentName;
        renameMessage.text = "";
        renameRoot.SetActive(true);
        renameRoot.transform.SetAsLastSibling();
    }

    private void BuildRenamePopup()
    {
        var canvas = GetComponentInParent<Canvas>();
        Transform parent = canvas != null ? canvas.rootCanvas.transform : transform;
        var font = rows.Count > 0 ? rows[0].nameText.font : null;

        renameRoot = new GameObject("Rename City Popup", typeof(RectTransform), typeof(Image));
        renameRoot.transform.SetParent(parent, false);
        var rootRt = (RectTransform)renameRoot.transform;
        rootRt.anchorMin = Vector2.zero; rootRt.anchorMax = Vector2.one; rootRt.offsetMin = rootRt.offsetMax = Vector2.zero;
        renameRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f); // dims and blocks what's behind

        var box = NewImage("Box", rootRt, new Vector2(760f, 360f), new Color(0.07f, 0.06f, 0.1f, 1f));
        NewText("Title", box, font, "Rename your city", 40, new Vector2(0f, 120f), new Vector2(700f, 60f), FontStyles.Bold);

        var inputBg = NewImage("Input", box, new Vector2(640f, 70f), new Color(0.02f, 0.02f, 0.05f, 1f));
        ((RectTransform)inputBg.transform).anchoredPosition = new Vector2(0f, 30f);
        var area = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
        area.transform.SetParent(inputBg, false);
        var areaRt = (RectTransform)area.transform;
        areaRt.anchorMin = Vector2.zero; areaRt.anchorMax = Vector2.one;
        areaRt.offsetMin = new Vector2(20f, 6f); areaRt.offsetMax = new Vector2(-20f, -6f);
        var placeholder = NewText("Placeholder", areaRt, font, "City name", 30, Vector2.zero, Vector2.zero, FontStyles.Italic);
        placeholder.color = new Color(1f, 1f, 1f, 0.5f);
        var text = NewText("Text", areaRt, font, "", 30, Vector2.zero, Vector2.zero, FontStyles.Normal);
        foreach (var t in new[] { placeholder, text })
        {
            var r = (RectTransform)t.transform;
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            t.alignment = TextAlignmentOptions.Left;
        }
        text.raycastTarget = true;
        renameInput = inputBg.gameObject.AddComponent<TMP_InputField>();
        renameInput.targetGraphic = inputBg.GetComponent<Image>();
        renameInput.textViewport = areaRt;
        renameInput.textComponent = text;
        renameInput.placeholder = placeholder;
        renameInput.characterLimit = 24;

        renameMessage = NewText("Message", box, font, "", 26, new Vector2(0f, -35f), new Vector2(700f, 40f), FontStyles.Normal);
        renameMessage.color = new Color(1f, 0.55f, 0.45f);

        var save = NewButton("Save", box, font, "Save", new Vector2(-170f, -115f), new Color(0.95f, 0.55f, 0.1f));
        save.onClick.AddListener(OnRenameSave);
        var cancel = NewButton("Cancel", box, font, "Cancel", new Vector2(170f, -115f), new Color(0.25f, 0.25f, 0.3f));
        cancel.onClick.AddListener(() => renameRoot.SetActive(false));
        renameRoot.SetActive(false);
    }

    private void OnRenameSave()
    {
        if (renameBusy) return;
        StartCoroutine(RenameRoutine());
    }

    private IEnumerator RenameRoutine()
    {
        renameBusy = true;
        var call = CityApi.Backend.RenameCity(renameCityId, renameInput.text);
        while (!call.isDone) yield return null;
        renameBusy = false;
        if (!call.ok) { renameMessage.text = call.error; yield break; }
        renameRoot.SetActive(false);
        refreshNow = true;
    }

    private static RectTransform NewImage(string name, Transform parent, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        go.GetComponent<Image>().color = color;
        return rt;
    }

    private static TMP_Text NewText(string name, Transform parent, TMP_FontAsset font, string text, float size, Vector2 pos, Vector2 box, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = box;
        var t = go.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text; t.fontSize = size; t.fontStyle = style;
        t.alignment = TextAlignmentOptions.Center; t.color = Color.white; t.raycastTarget = false;
        return t;
    }

    private static Button NewButton(string name, Transform parent, TMP_FontAsset font, string label, Vector2 pos, Color color)
    {
        var rt = NewImage(name, parent, new Vector2(300f, 80f), color);
        rt.anchoredPosition = pos;
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = rt.GetComponent<Image>();
        var t = NewText("Label", rt, font, label, 34, Vector2.zero, new Vector2(300f, 80f), FontStyles.Bold);
        return button;
    }
}
