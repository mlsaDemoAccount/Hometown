using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// Hometown > UI > Create Profile Screen: builds a dummy "Your Profile" screen (layout from the
/// Figma frame) in the MainMenu scene and plugs it into GameUIManager. Everything it makes is an
/// ordinary scene object, so restyle it by hand afterwards.
public static class ProfileScreenBuilder
{
    // Positions below are pixels of the Figma image (1809 x 834); the card starts at (323, 85).
    private const float CardX = 323f, CardY = 85f, CardW = 1325f, CardH = 723f;

    private static Sprite rounded;

    [MenuItem("Hometown/UI/Create Profile Screen")]
    public static void Build()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Single);
        var manager = Object.FindFirstObjectByType<GameUIManager>(FindObjectsInactive.Include);
        var login = Object.FindFirstObjectByType<LoginScreen>(FindObjectsInactive.Include);
        if (manager == null || login == null) { Debug.LogError("[ProfileScreenBuilder] GameUIManager / LoginScreen not found in MainMenu."); return; }

        var existing = Object.FindFirstObjectByType<ProfileScreen>(FindObjectsInactive.Include);
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        Transform parent = login.transform.parent;

        // ---- screen root: full screen, dark backdrop (put the real background picture here later)
        var root = NewUi("Profile Screen", parent);
        Stretch(root);
        var backdrop = root.gameObject.AddComponent<Image>();
        backdrop.color = new Color(0.12f, 0.07f, 0.04f, 1f);
        var screen = root.gameObject.AddComponent<ProfileScreen>();

        // ---- card
        var card = NewUi("Card", root);
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
        card.sizeDelta = new Vector2(CardW, CardH);
        card.anchoredPosition = Vector2.zero;
        var cardImage = card.gameObject.AddComponent<Image>();
        Slice(cardImage, new Color(0.04f, 0.03f, 0.03f, 0.92f));

        Label("Title", card, "Your Profile", 118f, 980f, 168f, 800f, 130f, TextAlignmentOptions.Center, true);

        // ---- back button (outside the card, top-left of the screen)
        var back = NewUi("Back Button", root);
        back.anchorMin = back.anchorMax = back.pivot = new Vector2(0f, 1f);
        back.sizeDelta = new Vector2(64f, 64f);
        back.anchoredPosition = new Vector2(30f, -49f);
        var backImage = back.gameObject.AddComponent<Image>();
        Slice(backImage, new Color(0f, 0f, 0f, 0.35f));
        screen.backButton = back.gameObject.AddComponent<Button>();
        Label("Arrow", back, "<", 44f, 32f, 32f, 64f, 64f, TextAlignmentOptions.Center, true, local: true);

        // ---- big avatar circle
        var big = Box("Selected Avatar", card, 487f, 227f, 170f, 170f, new Color(0f, 0f, 0f, 1f));
        screen.selectedAvatarImage = big.GetComponent<Image>();
        screen.selectedAvatarImage.enabled = false;
        var circle = Box("Selected Avatar Frame", card, 487f, 227f, 170f, 170f, new Color(0.02f, 0.02f, 0.08f, 1f));
        circle.SetSiblingIndex(big.GetSiblingIndex());
        screen.selectedAvatarHint = Label("Select Hint", card, "Select", 30f, 572f, 337f, 170f, 50f, TextAlignmentOptions.Center, true);
        big.SetAsLastSibling(); // picture goes over the frame, hint stays visible while nothing is picked
        screen.selectedAvatarHint.transform.SetAsLastSibling();

        // ---- 4 x 3 avatar grid
        float[] columns = { 378f, 480f, 585f, 687f };
        float[] rows = { 427f, 545f, 663f };
        var buttons = new System.Collections.Generic.List<Button>();
        int n = 0;
        foreach (float y in rows)
        {
            foreach (float x in columns)
            {
                var cell = Box("Avatar " + (n + 1), card, x, y, 86f, 103f, Color.black);
                var button = cell.gameObject.AddComponent<Button>();
                button.targetGraphic = cell.GetComponent<Image>();
                var portrait = Box("Portrait", cell, 0f, 0f, 70f, 90f, Color.HSVToRGB(n / 12f, 0.45f, 0.75f), local: true);
                portrait.anchorMin = portrait.anchorMax = portrait.pivot = new Vector2(0.5f, 0.5f);
                portrait.anchoredPosition = Vector2.zero;
                portrait.GetComponent<Image>().raycastTarget = false;
                Label("Number", portrait, (n + 1).ToString(), 30f, 35f, 45f, 70f, 90f, TextAlignmentOptions.Center, true, local: true);
                buttons.Add(button);
                n++;
            }
        }
        screen.avatarButtons = buttons.ToArray();
        screen.avatarSprites = new Sprite[buttons.Count];

        // ---- alias
        Label("Alias Label", card, "Alias Name", 40f, 881f, 255f, 400f, 50f, TextAlignmentOptions.Left, true, leftAnchored: true);
        screen.aliasInput = Input("Alias Input", card, 866f, 297f, 701f, 64f, "Enter your name");

        // ---- role
        Label("Role Label", card, "Role", 26f, 1214f, 399f, 300f, 40f, TextAlignmentOptions.Center, true);
        string[] roleNames = { "Parent", "Child", "GrandParent" };
        float[] roleX = { 866f, 1115f, 1366f };
        var roleButtons = new Button[3];
        for (int i = 0; i < 3; i++) roleButtons[i] = Pill("Role " + roleNames[i], card, roleNames[i], roleX[i], 438f, 202f, 65f);
        screen.roleButtons = roleButtons;

        // ---- gender
        Label("Gender Label", card, "Gender", 26f, 1212f, 541f, 300f, 40f, TextAlignmentOptions.Center, true);
        screen.genderButtons = new[]
        {
            Pill("Gender Male", card, "Male", 866f, 580f, 319f, 65f),
            Pill("Gender Female", card, "Female", 1240f, 580f, 327f, 65f),
        };

        // ---- create + status
        var create = Pill("Create Button", card, "Create", 1016f, 684f, 387f, 55f, new Color(0.08f, 0.2f, 0.6f, 1f), 40f);
        screen.createButton = create;
        screen.statusText = Label("Status", card, "", 24f, 1210f, 765f, 700f, 36f, TextAlignmentOptions.Center, true);
        screen.statusText.color = new Color(1f, 0.55f, 0.45f);

        // ---- hook into GameUIManager
        var so = new SerializedObject(manager);
        var field = so.FindProperty("profileScreen");
        if (field != null) { field.objectReferenceValue = screen; so.ApplyModifiedPropertiesWithoutUndo(); }
        else Debug.LogWarning("[ProfileScreenBuilder] GameUIManager has no profileScreen field.");

        root.gameObject.SetActive(false);
        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = root.gameObject;
        Debug.Log("[ProfileScreenBuilder] Profile Screen created under '" + parent.name + "' and linked to GameUIManager. Open it from the main menu's profile button.");
    }

    // ---------- helpers ----------

    private static RectTransform NewUi(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static void Slice(Image image, Color color)
    {
        image.sprite = rounded;
        image.type = rounded != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = color;
    }

    /// A rectangle placed by Figma coordinates (card space). local = already in the parent's own space.
    private static RectTransform Box(string name, Transform parent, float x, float y, float w, float h, Color color, bool local = false)
    {
        var rt = NewUi(name, parent);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = local ? new Vector2(x, -y) : new Vector2(x - CardX, -(y - CardY));
        var image = rt.gameObject.AddComponent<Image>();
        image.color = color;
        return rt;
    }

    /// A text centred at (cx, cy) in Figma coordinates.
    private static TMP_Text Label(string name, Transform parent, string text, float size, float cx, float cy, float w, float h,
        TextAlignmentOptions align, bool bold, bool local = false, bool leftAnchored = false)
    {
        var rt = NewUi(name, parent);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(w, h);
        Vector2 center = local ? new Vector2(cx, -cy) : new Vector2(cx - CardX, -(cy - CardY));
        // leftAnchored: cx is the text's LEFT edge instead of its centre
        rt.anchoredPosition = leftAnchored ? new Vector2(center.x, center.y + h * 0.5f) : new Vector2(center.x - w * 0.5f, center.y + h * 0.5f);
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static Button Pill(string name, Transform parent, string text, float x, float y, float w, float h, Color? color = null, float fontSize = 28f)
    {
        var rt = Box(name, parent, x, y, w, h, Color.white);
        var image = rt.GetComponent<Image>();
        Slice(image, color ?? new Color(0.04f, 0.03f, 0.03f, 0.9f));
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var label = Label("Label", rt, text, fontSize, w * 0.5f, h * 0.5f, w, h, TextAlignmentOptions.Center, true, local: true);
        return button;
    }

    private static TMP_InputField Input(string name, Transform parent, float x, float y, float w, float h, string placeholder)
    {
        var rt = Box(name, parent, x, y, w, h, Color.white);
        var image = rt.GetComponent<Image>();
        Slice(image, new Color(0.02f, 0.02f, 0.05f, 1f));

        var area = NewUi("Text Area", rt);
        Stretch(area);
        area.offsetMin = new Vector2(30f, 6f); area.offsetMax = new Vector2(-30f, -6f);
        area.gameObject.AddComponent<RectMask2D>();

        var ph = Label("Placeholder", area, placeholder, 28f, 0f, 0f, 10f, 10f, TextAlignmentOptions.Left, false, local: true);
        Stretch((RectTransform)ph.transform);
        ph.color = new Color(1f, 1f, 1f, 0.55f);
        ph.fontStyle = FontStyles.Italic;
        var text = Label("Text", area, "", 28f, 0f, 0f, 10f, 10f, TextAlignmentOptions.Left, false, local: true);
        Stretch((RectTransform)text.transform);
        text.raycastTarget = true;

        var input = rt.gameObject.AddComponent<TMP_InputField>();
        input.targetGraphic = image;
        input.textViewport = area;
        input.textComponent = text;
        input.placeholder = ph;
        input.characterLimit = 20;
        return input;
    }
}
