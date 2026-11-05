using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// Builds the Co-op screens (Main / Host / Join / Session / Away + Co-op button) as normal scene
/// objects under each MultiplayerTestUI, so they can be restyled, re-sprited and moved in the editor.
///   Hometown > UI > Build Co-op UI (open scenes)
/// A MultiplayerTestUI that already has its canvas is left alone - delete the "Multiplayer Canvas"
/// child first if you want a fresh one.
public static class CoopUiBuilder
{
    private static readonly Color Dark = new Color(0.05f, 0.08f, 0.12f, 0.95f);
    private static readonly Color ButtonBlue = new Color(0.2f, 0.55f, 0.95f, 1f);

    [MenuItem("Hometown/UI/Build Co-op UI (open scenes)")]
    public static void BuildAll()
    {
        int built = 0;
        foreach (var ui in Object.FindObjectsByType<MultiplayerTestUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (Build(ui)) built++;
        Debug.Log("[Co-op UI] Built " + built + " screen set(s).");
    }

    /// Returns false when this UI already has its canvas.
    public static bool Build(MultiplayerTestUI ui)
    {
        if (ui.transform.Find("Multiplayer Canvas") != null) return false;
        bool overlay = ui.overlayMode, menu = ui.menuMode;

        var canvasGo = new GameObject("Multiplayer Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(ui.transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        if (overlay || menu) canvas.sortingOrder = 500; // above the game's own UI
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 1f;
        var root = (RectTransform)canvasGo.transform;

        var so = new SerializedObject(ui);

        // Main
        var main = Panel(root, "Main", new Color(0.05f, 0.08f, 0.12f, 0.92f));
        Label(main, "Title", "Play Together", new Vector2(0, 360), new Vector2(1400, 120), 80, true);
        var mainStatus = Label(main, "Status", "", new Vector2(0, 200), new Vector2(1400, 160), 40, false);
        var hostBtn = Btn(main, menu ? "Invite a friend" : "Host a game", new Vector2(-330, -80), new Vector2(560, 170));
        var joinBtn = Btn(main, menu ? "Enter friend's code" : "Join a game", new Vector2(330, -80), new Vector2(560, 170));
        Button closeBtn = null;
        if (overlay || menu) closeBtn = Btn(main, "Close", new Vector2(0, -330), new Vector2(420, 110));

        // Host: QR on the left, details on the right
        var host = Panel(root, "Host", Dark);
        Label(host, "Title", "Show this to the other player", new Vector2(0, 450), new Vector2(1500, 100), 56, true);
        var qr = RawImg(host, "QR Image", new Vector2(-430, -30), new Vector2(660, 660));
        var hostPayload = Label(host, "Payload", "", new Vector2(430, 190), new Vector2(760, 200), 42, false);
        var hostPlayers = Label(host, "Players", "", new Vector2(430, -50), new Vector2(760, 220), 44, false);
        var hostCancel = Btn(host, menu ? "Cancel" : "Stop hosting", new Vector2(430, -300), new Vector2(520, 130));

        // Join: camera on the left, status + manual entry on the right
        var join = Panel(root, "Join", Dark);
        Label(join, "Title", "Scan the host's QR code", new Vector2(0, 450), new Vector2(1500, 100), 56, true);
        RawImg(join, "Camera Frame", new Vector2(-430, -30), new Vector2(660, 660)).color = Color.black;
        var cam = RawImg(join, "Camera Image", new Vector2(-430, -30), new Vector2(660, 660));
        var joinStatus = Label(join, "Status", "", new Vector2(430, 190), new Vector2(760, 220), 40, false);
        var input = Input(join, menu ? "or type the 6-digit invite code" : "or type the 4-digit code", new Vector2(430, 20), new Vector2(760, 110), menu ? 6 : 4);
        var connectBtn = Btn(join, "Connect", new Vector2(430, -130), new Vector2(520, 110));
        var joinCancel = Btn(join, "Cancel", new Vector2(430, -270), new Vector2(520, 110));

        // Session: a slim bar on top so the game stays in view
        var session = new GameObject("Session", typeof(RectTransform), typeof(Image));
        session.transform.SetParent(root, false);
        var sr = (RectTransform)session.transform;
        session.GetComponent<Image>().color = new Color(0.05f, 0.08f, 0.12f, 0.85f);
        TMP_Text sessionText;
        Button sessionLeave;
        if (overlay || menu)
        {
            sr.anchorMin = sr.anchorMax = new Vector2(0.5f, 1f);
            sr.pivot = new Vector2(0.5f, 1f); sr.sizeDelta = new Vector2(820, 90); sr.anchoredPosition = new Vector2(0, -10);
            sessionText = Label(session, "Text", "", new Vector2(-110, -45), new Vector2(560, 80), 34, false);
            TopAnchor(sessionText.rectTransform);
            sessionLeave = Btn(session, "Leave", new Vector2(290, -45), new Vector2(200, 70));
            TopAnchor((RectTransform)sessionLeave.transform);
        }
        else
        {
            sr.anchorMin = new Vector2(0, 1); sr.anchorMax = new Vector2(1, 1);
            sr.pivot = new Vector2(0.5f, 1); sr.sizeDelta = new Vector2(0, 150); sr.anchoredPosition = Vector2.zero;
            sessionText = Label(session, "Text", "", new Vector2(-180, -75), new Vector2(1200, 130), 40, false);
            TopAnchor(sessionText.rectTransform);
            sessionLeave = Btn(session, "Leave", new Vector2(780, -75), new Vector2(260, 100));
            TopAnchor((RectTransform)sessionLeave.transform);
        }

        // A short message line from the host ("Not enough funds.")
        var notice = Label(root, "Notice", "", new Vector2(0, -140), new Vector2(1000, 70), 36, true);
        TopAnchor(notice.rectTransform);
        notice.color = new Color(1f, 0.85f, 0.4f);

        // Co-op button (Gameplay) and the way back to the main menu
        var coop = Btn(root, "Co-op", Vector2.zero, new Vector2(240, 90));
        var coopRt = (RectTransform)coop.transform;
        coopRt.anchorMin = coopRt.anchorMax = coopRt.pivot = new Vector2(1f, 1f);
        coopRt.anchoredPosition = new Vector2(-30, -30);

        var back = Btn(root, "Back to menu", Vector2.zero, new Vector2(520, 100));
        var backRt = (RectTransform)back.transform;
        backRt.anchorMin = backRt.anchorMax = backRt.pivot = new Vector2(0.5f, 1f);
        backRt.anchoredPosition = new Vector2(0, -20);

        // Pause screen while the other player is away (last = drawn on top)
        var away = Panel(root, "Away", new Color(0.03f, 0.05f, 0.08f, 0.9f));
        var awayText = Label(away, "Text", "", new Vector2(0, 150), new Vector2(1500, 360), 56, true);
        var awayTimer = Label(away, "Timer", "", new Vector2(0, -120), new Vector2(1500, 160), 90, true);
        var awayLeave = Btn(away, "Leave game", new Vector2(0, -340), new Vector2(520, 120));

        Set(so, "mainPanel", main); Set(so, "hostPanel", host); Set(so, "joinPanel", join);
        Set(so, "sessionPanel", session); Set(so, "awayPanel", away);
        Set(so, "mainStatus", mainStatus); Set(so, "hostPlayers", hostPlayers); Set(so, "hostPayload", hostPayload);
        Set(so, "joinStatus", joinStatus); Set(so, "sessionText", sessionText); Set(so, "noticeText", notice);
        Set(so, "awayText", awayText); Set(so, "awayTimer", awayTimer);
        Set(so, "qrImage", qr); Set(so, "camImage", cam); Set(so, "manualInput", input);
        Set(so, "hostButton", hostBtn); Set(so, "joinButton", joinBtn); Set(so, "mainCloseButton", closeBtn);
        Set(so, "hostCancelButton", hostCancel); Set(so, "connectButton", connectBtn); Set(so, "joinCancelButton", joinCancel);
        Set(so, "sessionLeaveButton", sessionLeave); Set(so, "awayLeaveButton", awayLeave);
        Set(so, "coopButton", coop); Set(so, "returnButton", back);
        so.ApplyModifiedProperties();

        // Panels start hidden in the saved scene; the script shows what it needs at run time.
        away.SetActive(false);
        notice.gameObject.SetActive(false);
        back.gameObject.SetActive(false);
        coop.gameObject.SetActive(overlay);
        if (overlay || menu) { main.SetActive(false); }
        host.SetActive(false); join.SetActive(false); session.SetActive(false);

        EditorUtility.SetDirty(ui);
        if (ui.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
        return true;
    }

    // ---------- helpers ----------

    private static void Set(SerializedObject so, string field, Object value)
    {
        var property = so.FindProperty(field);
        if (property != null) property.objectReferenceValue = value;
        else Debug.LogWarning("[Co-op UI] Missing field " + field);
    }

    private static void Set(SerializedObject so, string field, GameObject value) => Set(so, field, (Object)value);

    private static void TopAnchor(RectTransform rt)
    {
        var pos = rt.anchoredPosition;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.anchoredPosition = pos;
    }

    private static GameObject Panel(RectTransform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        go.GetComponent<Image>().color = color;
        return go;
    }

    private static TMP_Text Label(Component parent, string name, string text, Vector2 pos, Vector2 size, float fontSize, bool bold)
        => Label(parent.gameObject, name, text, pos, size, fontSize, bold);

    private static TMP_Text Label(GameObject parent, string name, string text, Vector2 pos, Vector2 size, float fontSize, bool bold)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        var t = go.GetComponent<TextMeshProUGUI>();
        t.font = TMP_Settings.defaultFontAsset;
        t.fontSharedMaterial = TMP_Settings.defaultFontAsset.material;
        t.text = text; t.fontSize = fontSize;
        t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
        t.raycastTarget = false;
        return t;
    }

    private static Button Btn(Component parent, string text, Vector2 pos, Vector2 size) => Btn(parent.gameObject, text, pos, size);

    private static Button Btn(GameObject parent, string text, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(text + " Button", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        var image = go.GetComponent<Image>();
        image.color = ButtonBlue;
        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        var label = Label(go, "Label", text, Vector2.zero, size, 44, true);
        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        return button;
    }

    private static RawImage RawImg(GameObject parent, string name, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(parent.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return go.GetComponent<RawImage>();
    }

    private static TMP_InputField Input(GameObject parent, string placeholder, Vector2 pos, Vector2 size, int limit)
    {
        var go = new GameObject("Manual Input", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        go.transform.SetParent(parent.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.95f);

        var area = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
        area.transform.SetParent(go.transform, false);
        var areaRt = (RectTransform)area.transform;
        areaRt.anchorMin = Vector2.zero; areaRt.anchorMax = Vector2.one;
        areaRt.offsetMin = new Vector2(20, 0); areaRt.offsetMax = new Vector2(-20, 0);

        var hint = Label(area, "Placeholder", placeholder, Vector2.zero, Vector2.zero, 34, false);
        hint.color = new Color(0.4f, 0.4f, 0.4f); hint.fontStyle = FontStyles.Italic; hint.alignment = TextAlignmentOptions.MidlineLeft;
        Stretch(hint.rectTransform);
        var text = Label(area, "Text", "", Vector2.zero, Vector2.zero, 40, false);
        text.color = Color.black; text.alignment = TextAlignmentOptions.MidlineLeft;
        Stretch(text.rectTransform);

        var field = go.GetComponent<TMP_InputField>();
        field.textViewport = areaRt;
        field.textComponent = text;
        field.placeholder = hint;
        field.characterLimit = limit;
        field.contentType = TMP_InputField.ContentType.IntegerNumber; // numeric keyboard on phones
        return field;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
