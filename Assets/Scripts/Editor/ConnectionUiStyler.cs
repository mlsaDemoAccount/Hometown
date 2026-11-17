using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// Hometown > UI > Restyle Host & Join Screens (open scenes)
/// Rebuilds the "Host" and "Join" panels of every MultiplayerTestUI in the open scenes in the Connection
/// design (Assets/UiImages/Connection): framed popup, leaf corners, QR card, icon rows, orange buttons.
/// The result is plain scene objects - move, resize or re-sprite them freely. Run it again to reset the look.
/// Layout notes:
///   Host: QR card on the left, rows on the right. Without a QR the rows move to the middle.
///   Join: camera card on the left, status/code/buttons on the right. Without a camera the rows move to the middle.
public static class ConnectionUiStyler
{
    private const string Dir = "Assets/UiImages/Connection/";

    // Which leaf picture goes in which corner (swap the names here if one looks wrong).
    private const string LeafTopLeft = "Group (4)", LeafTopRight = "Group (3)", LeafBottomLeft = "Group (5)", LeafBottomRight = "Group (6)";

    private static readonly Vector2 PopupSize = new Vector2(960f, 645f);
    private static readonly Vector2 CardCenter = new Vector2(-200f, -45f);
    private static readonly Vector2 CardSize = new Vector2(383f, 392f);
    private static readonly Vector2 ColumnCenter = new Vector2(216f, -10f);
    private static readonly Vector2 ColumnSize = new Vector2(351f, 470f);
    private static readonly Color Navy = new Color(0.04f, 0.13f, 0.38f, 1f);

    private class Look
    {
        public Sprite popup, button, field, field2, iconCode, iconClock, iconPlayers, iconScan;
        public Sprite leafTL, leafTR, leafBL, leafBR, rounded;
        public TMP_FontAsset heavy, body;
        public Material titleMat, buttonMat;
    }

    [MenuItem("Hometown/UI/Restyle Host & Join Screens (open scenes)")]
    public static void RestyleAll()
    {
        var look = LoadLook();
        if (look == null) return;
        int done = 0;
        foreach (var ui in Object.FindObjectsByType<MultiplayerTestUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (Restyle(ui, look)) done++;
        AssetDatabase.SaveAssets();
        Debug.Log("[Connection UI] Restyled " + done + " screen set(s).");
    }

    private static Look LoadLook()
    {
        var l = new Look
        {
            popup = LoadSprite("Popup"), button = LoadSprite("Button"), field = LoadSprite("Field"), field2 = LoadSprite("Field 2"),
            iconCode = LoadSprite("Icon Code"), iconClock = LoadSprite("Icon Clock"), iconPlayers = LoadSprite("Icon Players"), iconScan = LoadSprite("Icon ScanQR"),
            leafTL = LoadSprite(LeafTopLeft), leafTR = LoadSprite(LeafTopRight), leafBL = LoadSprite(LeafBottomLeft), leafBR = LoadSprite(LeafBottomRight),
            rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
        };
        if (l.popup == null || l.button == null || l.field == null || l.field2 == null)
        {
            Debug.LogError("[Connection UI] Missing sprites in " + Dir + " (Popup, Button, Field, Field 2 are needed; make sure they are imported as Sprites).");
            return null;
        }
        l.heavy = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Fredoka/Fredoka_Condensed-Bold SDF.asset");
        l.body = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Fredoka/Fredoka-VariableFont_wdth,wght SDF.asset");
        if (l.heavy == null) l.heavy = TMP_Settings.defaultFontAsset;
        if (l.body == null) l.body = l.heavy;
        l.titleMat = OutlineMaterial(l.heavy, "Connection Title Outline", 0.28f, Navy);
        l.buttonMat = OutlineMaterial(l.heavy, "Connection Button Outline", 0.22f, new Color(0.45f, 0.22f, 0.02f, 1f));
        return l;
    }

    private static Sprite LoadSprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Dir + name + ".png");

    private static Material OutlineMaterial(TMP_FontAsset font, string name, float width, Color color)
    {
        string path = Dir + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(font.material); AssetDatabase.CreateAsset(m, path); }
        m.EnableKeyword("OUTLINE_ON");
        m.SetFloat("_OutlineWidth", width);
        m.SetColor("_OutlineColor", color);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static bool Restyle(MultiplayerTestUI ui, Look look)
    {
        var canvas = ui.transform.Find("Multiplayer Canvas");
        if (canvas == null)
        {
            Debug.LogWarning("[Connection UI] " + ui.name + " has no 'Multiplayer Canvas'. Run Hometown > UI > Build Co-op UI first.");
            return false;
        }
        var root = (RectTransform)canvas;
        bool menu = ui.menuMode;
        var so = new SerializedObject(ui);

        int hostIndex = RemoveOld(root, "Host", 1), joinIndex = RemoveOld(root, "Join", 2);

        // ---------------- HOST ----------------
        var host = Dim(root, "Host");
        var popup = Popup(host.transform, look);
        Title(popup, "Show this to the other Player", look);

        var qrCard = Card(popup, "QR Card", look);
        var qr = new GameObject("QR Image", typeof(RectTransform), typeof(RawImage));
        qr.transform.SetParent(qrCard.transform, false);
        Stretch((RectTransform)qr.transform, 30f);

        var column = Column(popup);
        var codeRow = Row(column, "Invite Code Row", look.field2, 80f);
        Icon(codeRow, look.iconCode, 56f, 48f);
        var codeLabel = Text(codeRow.transform, "Label", "Invite code", 26, look.body, null, Color.white, TextAlignmentOptions.BottomLeft);
        Anchor(codeLabel.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 1), new Vector2(104, 0), new Vector2(-10, -2));
        var codeValue = Text(codeRow.transform, "Value", "000000", 46, look.heavy, null, Color.white, TextAlignmentOptions.TopLeft);
        Anchor(codeValue.rectTransform, new Vector2(0, 0), new Vector2(1, 0.55f), new Vector2(104, 2), new Vector2(-10, 0));

        var scanRow = Row(column, "Scan Row", look.field, 60f);
        Icon(scanRow, look.iconScan, 34f, 34f);
        var scanText = RowText(scanRow, "Or scan the QR code.", look);

        var playersRow = Row(column, "Players Row", look.field, 60f);
        Icon(playersRow, look.iconPlayers, 42f, 35f);
        var playersText = RowText(playersRow, "Player connected: <b><color=#FFB02E>1</color></b>", look);

        var waitRow = Row(column, "Wait Row", look.field, 60f);
        Icon(waitRow, look.iconClock, 34f, 36f);
        var waitText = RowText(waitRow, "Waiting for the other player...", look);

        Spacer(column, 10f);
        var hostCancel = OrangeButton(column, "Cancel Button", menu ? "Cancel" : "Stop hosting", look, 72f, Color.white);
        Leaves(popup, look);

        // ---------------- JOIN ----------------
        var join = Dim(root, "Join");
        var jPopup = Popup(join.transform, look);
        Title(jPopup, "Scan the host's QR code", look);

        var camCard = Card(jPopup, "Camera Card", look);
        var frame = new GameObject("Camera Frame", typeof(RectTransform), typeof(Image));
        frame.transform.SetParent(camCard.transform, false);
        Stretch((RectTransform)frame.transform, 16f);
        var fi = frame.GetComponent<Image>(); fi.sprite = look.rounded; fi.type = Image.Type.Sliced; fi.color = Color.black; fi.raycastTarget = false;
        var cam = new GameObject("Camera Image", typeof(RectTransform), typeof(RawImage));
        cam.transform.SetParent(frame.transform, false);
        Stretch((RectTransform)cam.transform, 6f);

        var jColumn = Column(jPopup);
        var statusRow = Row(jColumn, "Status Row", look.field2, 96f);
        Icon(statusRow, look.iconScan, 34f, 34f);
        var statusText = RowText(statusRow, "Point the camera at the host's QR code.", look);
        statusText.textWrappingMode = TextWrappingModes.Normal;

        var inputRow = Row(jColumn, "Code Row", look.field, 64f);
        Icon(inputRow, look.iconCode, 44f, 38f);
        var input = MakeInput(inputRow, menu ? "Type the 6-digit invite code" : "Type the 4-digit room code", menu ? 6 : 4, look);

        Spacer(jColumn, 6f);
        var connect = OrangeButton(jColumn, "Connect Button", "Connect", look, 72f, Color.white);
        var joinCancel = OrangeButton(jColumn, "Cancel Button", "Cancel", look, 64f, new Color(0.62f, 0.78f, 1f, 1f));
        Leaves(jPopup, look);

        // ---------------- wire up ----------------
        Set(so, "hostPanel", host); Set(so, "joinPanel", join);
        Set(so, "qrImage", qr.GetComponent<RawImage>()); Set(so, "camImage", cam.GetComponent<RawImage>());
        Set(so, "manualInput", input); Set(so, "hostPayload", scanText); Set(so, "hostPlayers", playersText); Set(so, "joinStatus", statusText);
        Set(so, "hostCancelButton", hostCancel); Set(so, "connectButton", connect); Set(so, "joinCancelButton", joinCancel);
        Set(so, "qrCard", qrCard); Set(so, "hostColumn", (RectTransform)column.transform);
        Set(so, "hostCodeRow", codeRow); Set(so, "hostScanRow", scanRow); Set(so, "hostPlayersRow", playersRow);
        Set(so, "hostCodeLabel", codeLabel); Set(so, "hostCodeValue", codeValue); Set(so, "hostWaitText", waitText);
        Set(so, "joinCamCard", camCard); Set(so, "joinColumn", (RectTransform)jColumn.transform);
        so.ApplyModifiedProperties();

        host.transform.SetSiblingIndex(Mathf.Min(hostIndex, root.childCount - 1));
        join.transform.SetSiblingIndex(Mathf.Min(joinIndex, root.childCount - 1));
        host.SetActive(false); join.SetActive(false);   // the script shows them when needed

        EditorUtility.SetDirty(ui);
        if (ui.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
        return true;
    }

    // ---------- pieces ----------

    private static int RemoveOld(RectTransform root, string name, int fallbackIndex)
    {
        var old = root.Find(name);
        if (old == null) return fallbackIndex;
        int index = old.GetSiblingIndex();
        Object.DestroyImmediate(old.gameObject);
        return index;
    }

    /// Full-screen dim layer that also blocks clicks on whatever is behind the popup.
    private static GameObject Dim(RectTransform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Stretch((RectTransform)go.transform, 0f);
        go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);
        return go;
    }

    private static GameObject Popup(Transform parent, Look look)
    {
        var go = new GameObject("Popup", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = PopupSize; rt.anchoredPosition = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.sprite = look.popup; img.color = Color.white;
        return go;
    }

    private static void Title(GameObject popup, string text, Look look)
    {
        var t = Text(popup.transform, "Title", text, 66, look.heavy, look.titleMat, Color.white, TextAlignmentOptions.Center);
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(900f, 100f); rt.anchoredPosition = new Vector2(0f, 218f);
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
    }

    private static GameObject Card(GameObject popup, string name, Look look)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(popup.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = CardSize; rt.anchoredPosition = CardCenter;
        var img = go.GetComponent<Image>();
        img.sprite = look.rounded; img.type = Image.Type.Sliced; img.color = Color.white; img.pixelsPerUnitMultiplier = 1.6f;
        return go;
    }

    private static GameObject Column(GameObject popup)
    {
        var go = new GameObject("Column", typeof(RectTransform), typeof(VerticalLayoutGroup));
        go.transform.SetParent(popup.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = ColumnSize; rt.anchoredPosition = ColumnCenter;
        var v = go.GetComponent<VerticalLayoutGroup>();
        v.spacing = 12f; v.childAlignment = TextAnchor.MiddleCenter;
        v.childControlWidth = true; v.childControlHeight = true;
        v.childForceExpandWidth = true; v.childForceExpandHeight = false;
        return go;
    }

    private static GameObject Row(GameObject column, string name, Sprite sprite, float height)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(column.transform, false);
        var img = go.GetComponent<Image>(); img.sprite = sprite; img.color = Color.white;
        go.GetComponent<LayoutElement>().preferredHeight = height;
        return go;
    }

    private static void Spacer(GameObject column, float height)
    {
        var go = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
        go.transform.SetParent(column.transform, false);
        go.GetComponent<LayoutElement>().preferredHeight = height;
    }

    private static void Icon(GameObject row, Sprite sprite, float w, float h)
    {
        var go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(row.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h); rt.anchoredPosition = new Vector2(46f, 0f);
        var img = go.GetComponent<Image>(); img.sprite = sprite; img.preserveAspect = true; img.raycastTarget = false;
        img.enabled = sprite != null;
    }

    private static TMP_Text RowText(GameObject row, string text, Look look)
    {
        var t = Text(row.transform, "Text", text, 26, look.body, null, Color.white, TextAlignmentOptions.MidlineLeft);
        Anchor(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(88f, 2f), new Vector2(-12f, -2f));
        t.enableAutoSizing = true; t.fontSizeMin = 16f; t.fontSizeMax = 26f;
        t.richText = true;
        return t;
    }

    private static Button OrangeButton(GameObject column, string name, string label, Look look, float height, Color tint)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(column.transform, false);
        var img = go.GetComponent<Image>(); img.sprite = look.button; img.color = tint;
        var btn = go.GetComponent<Button>(); btn.targetGraphic = img;
        go.GetComponent<LayoutElement>().preferredHeight = height;
        var t = Text(go.transform, "Label", label, 44, look.heavy, look.buttonMat, Color.white, TextAlignmentOptions.Center);
        Anchor(t.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        t.raycastTarget = false;
        return btn;
    }

    private static TMP_InputField MakeInput(GameObject row, string placeholder, int limit, Look look)
    {
        var field = row.AddComponent<TMP_InputField>();
        var area = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
        area.transform.SetParent(row.transform, false);
        var areaRt = (RectTransform)area.transform;
        Anchor(areaRt, Vector2.zero, Vector2.one, new Vector2(88f, 4f), new Vector2(-14f, -4f));

        var hint = Text(area.transform, "Placeholder", placeholder, 24, look.body, null, new Color(1f, 1f, 1f, 0.55f), TextAlignmentOptions.MidlineLeft);
        hint.fontStyle = FontStyles.Italic; Stretch(hint.rectTransform, 0f);
        var text = Text(area.transform, "Text", "", 30, look.heavy, null, Color.white, TextAlignmentOptions.MidlineLeft);
        Stretch(text.rectTransform, 0f);

        field.textViewport = areaRt;
        field.textComponent = text;
        field.placeholder = hint;
        field.characterLimit = limit;
        field.contentType = TMP_InputField.ContentType.IntegerNumber;   // numeric keyboard on phones
        field.customCaretColor = true; field.caretColor = Color.white;
        field.selectionColor = new Color(1f, 0.69f, 0.18f, 0.45f);
        return field;
    }

    private static void Leaves(GameObject popup, Look look)
    {
        var half = PopupSize * 0.5f;
        Leaf(popup, "Leaf TL", look.leafTL, new Vector2(-half.x + 38f, half.y - 36f));
        Leaf(popup, "Leaf TR", look.leafTR, new Vector2(half.x - 42f, half.y - 22f));
        Leaf(popup, "Leaf BL", look.leafBL, new Vector2(-half.x + 30f, -half.y + 34f));
        Leaf(popup, "Leaf BR", look.leafBR, new Vector2(half.x - 32f, -half.y + 34f));
    }

    private static void Leaf(GameObject popup, string name, Sprite sprite, Vector2 pos)
    {
        if (sprite == null) return;
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(popup.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(sprite.rect.width, sprite.rect.height) * 1.1f; rt.anchoredPosition = pos;
        var img = go.GetComponent<Image>(); img.sprite = sprite; img.preserveAspect = true; img.raycastTarget = false;
    }

    // ---------- helpers ----------

    private static TextMeshProUGUI Text(Transform parent, string name, string text, float size, TMP_FontAsset font, Material mat, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.font = font;
        t.fontSharedMaterial = mat != null ? mat : font.material;
        t.text = text; t.fontSize = size; t.color = color; t.alignment = align;
        t.raycastTarget = false;
        return t;
    }

    private static void Stretch(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
    }

    private static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
    }

    private static void Set(SerializedObject so, string field, Object value)
    {
        var p = so.FindProperty(field);
        if (p != null) p.objectReferenceValue = value;
        else Debug.LogWarning("[Connection UI] Missing field " + field);
    }
}
