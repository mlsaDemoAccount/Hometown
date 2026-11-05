using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// The "Your Profile" screen: pick an avatar, an alias name, a family role and a gender.
/// The layout is built once by Hometown > UI > Create Profile Screen; after that every element is
/// a normal scene object - restyle or replace them freely, just keep the references below assigned.
///
/// Saved: avatar index and alias go to the server profile (playerImageIndex / firstName). Role and
/// gender are kept on the device only until the backend has fields for them.
public class ProfileScreen : MonoBehaviour
{
    [Header("Avatar")]
    public Image selectedAvatarImage;
    [Tooltip("Shown on the big circle until an avatar is picked.")]
    public TMP_Text selectedAvatarHint;
    [Tooltip("The grid buttons, in order. Each needs a child Image named 'Portrait' for the picture.")]
    public Button[] avatarButtons;
    [Tooltip("One sprite per grid button. Empty entries keep the coloured placeholder.")]
    public Sprite[] avatarSprites;

    [Header("Form")]
    public TMP_InputField aliasInput;
    [Tooltip("Parent, Child, GrandParent - in this order.")]
    public Button[] roleButtons;
    [Tooltip("Male, Female - in this order.")]
    public Button[] genderButtons;
    public Button createButton;
    public Button backButton;
    public TMP_Text statusText;

    [Header("Selection colours")]
    public Color normalColor = new Color(0.04f, 0.03f, 0.03f, 0.9f);
    public Color selectedColor = new Color(0.27f, 0.22f, 0.65f, 1f);
    public Color avatarNormalColor = Color.black;
    public Color avatarSelectedColor = new Color(0.08f, 0.1f, 0.8f, 1f);

    [Header("Selected look (optional)")]
    [Tooltip("Role / gender button picture when not selected. Empty keeps the button's own sprite.")]
    public Sprite optionNormalSprite;
    [Tooltip("Role / gender button picture when selected. Empty = colour change only.")]
    public Sprite optionSelectedSprite;
    public Color optionTextNormalColor = Color.white;
    public Color optionTextSelectedColor = new Color(1f, 0.85f, 0.3f, 1f);
    [Tooltip("Border drawn around the selected avatar, role and gender button.")]
    public Color borderColor = new Color(1f, 0.82f, 0.2f, 1f);
    public float borderSize = 5f;
    [Tooltip("Border thickness for the avatar grid only (avatars never change size).")]
    public float avatarBorderSize = 2f;
    [Tooltip("The selected button grows a little so it stands out.")]
    public float selectedScale = 1.08f;

    private static readonly string[] Roles = { "Parent", "Child", "GrandParent" };
    private static readonly string[] Genders = { "Male", "Female" };

    private int avatarIndex = -1;
    private int roleIndex = 0;
    private int genderIndex = -1;
    private bool wired;

    private static string Key(string name) => "profile_" + name + "_" + UserDetails.SaveScope;

    /// Index of the avatar the player picked, or -1 if the profile hasn't been created yet.
    public static int SavedAvatarIndex => PlayerPrefs.GetInt(Key("avatar"), -1);

    /// 0 = Male, 1 = Female, -1 = not chosen yet (device only until the backend has a gender field).
    public static int SavedGender => PlayerPrefs.GetInt(Key("gender"), -1);

    void Awake() => Wire();

    void OnEnable()
    {
        Wire();
        Load();
        Refresh();
    }

    private void Wire()
    {
        if (wired) return;
        wired = true;
        for (int i = 0; i < avatarButtons.Length; i++)
        {
            int index = i;
            if (avatarButtons[i] != null) avatarButtons[i].onClick.AddListener(() => PickAvatar(index));
        }
        for (int i = 0; i < roleButtons.Length; i++)
        {
            int index = i;
            if (roleButtons[i] != null) roleButtons[i].onClick.AddListener(() => { roleIndex = index; Refresh(); });
        }
        for (int i = 0; i < genderButtons.Length; i++)
        {
            int index = i;
            if (genderButtons[i] != null) genderButtons[i].onClick.AddListener(() => { genderIndex = index; Refresh(); });
        }
        if (createButton != null) createButton.onClick.AddListener(OnCreate);
        if (backButton != null) backButton.onClick.AddListener(Close);
    }

    private static ProfileScreen cached;

    /// The picture for an avatar index (set in this screen's Avatar Sprites), or null while it is
    /// still a placeholder. Works while the screen is hidden.
    public static Sprite AvatarSprite(int index)
    {
        var shared = AvatarLibrary.Avatar(index); // Assets/Resources/AvatarLibrary - works in every scene
        if (shared != null) return shared;
        if (cached == null) cached = FindFirstObjectByType<ProfileScreen>(FindObjectsInactive.Include);
        if (cached == null || cached.avatarSprites == null || index < 0 || index >= cached.avatarSprites.Length) return null;
        return cached.avatarSprites[index];
    }

    /// Placeholder colour for an avatar index (same as the grid in this screen).
    public static Color AvatarColor(int index) => Color.HSVToRGB((Mathf.Abs(index) % 12) / 12f, 0.45f, 0.75f);

    private void Load()
    {
        var user = UserDetails.Instance;
        // The server profile (getProfile) already carries playerImageIndex - it is the default until
        // the player has picked one here on this device.
        avatarIndex = PlayerPrefs.GetInt(Key("avatar"), user != null && user.isLoggedIn ? user.playerImageIndex : -1);
        if (avatarIndex >= avatarButtons.Length) avatarIndex = -1;
        roleIndex = Mathf.Clamp(PlayerPrefs.GetInt(Key("role"), 0), 0, Roles.Length - 1);
        genderIndex = PlayerPrefs.GetInt(Key("gender"), -1);
        if (aliasInput != null) aliasInput.text = user != null ? user.firstName : "";
        if (statusText != null) statusText.text = "";
    }

    private void PickAvatar(int index)
    {
        avatarIndex = index;
        Refresh();
    }

    private void Refresh()
    {
        for (int i = 0; i < avatarButtons.Length; i++)
        {
            if (avatarButtons[i] == null) continue;
            var image = avatarButtons[i].GetComponent<Image>();
            if (image != null) image.color = i == avatarIndex ? avatarSelectedColor : avatarNormalColor;
            Border(avatarButtons[i], i == avatarIndex, avatarBorderSize, false);
        }
        for (int i = 0; i < roleButtons.Length; i++) Tint(roleButtons[i], i == roleIndex);
        for (int i = 0; i < genderButtons.Length; i++) Tint(genderButtons[i], i == genderIndex);

        if (selectedAvatarImage != null)
        {
            bool has = avatarIndex >= 0 && avatarIndex < avatarButtons.Length;
            var portrait = has ? Portrait(avatarIndex) : null;
            selectedAvatarImage.enabled = has && portrait != null;
            if (has && portrait != null)
            {
                selectedAvatarImage.sprite = portrait.sprite;
                selectedAvatarImage.color = portrait.color;
            }
            if (selectedAvatarHint != null) selectedAvatarHint.gameObject.SetActive(!has);
        }
    }

    private void Tint(Button button, bool selected)
    {
        if (button == null) return;
        var image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = selected ? selectedColor : normalColor;
            var sprite = selected ? optionSelectedSprite : optionNormalSprite;
            if (sprite != null) image.sprite = sprite;
        }
        var label = button.GetComponentInChildren<TMP_Text>();
        if (label != null) label.color = selected ? optionTextSelectedColor : optionTextNormalColor;
        Border(button, selected, borderSize, true);
    }

    // Border (UI Outline on the button's picture) + optionally a slight size bump on the selected button.
    private void Border(Button button, bool selected, float size, bool grow)
    {
        if (button == null) return;
        var image = button.GetComponent<Image>();
        if (image != null)
        {
            var outline = image.GetComponent<Outline>();
            if (outline == null) outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = borderColor;
            outline.effectDistance = new Vector2(size, -size);
            outline.enabled = selected;
        }
        button.transform.localScale = selected && grow ? Vector3.one * selectedScale : Vector3.one;
    }

    private Image Portrait(int index)
    {
        var button = avatarButtons[index];
        if (button == null) return null;
        var child = button.transform.Find("Portrait");
        var image = child != null ? child.GetComponent<Image>() : null;
        var sprite = AvatarSprite(index);
        if (image != null && sprite != null)
        {
            image.sprite = sprite;
            image.color = Color.white;
        }
        return image;
    }

    private void OnCreate()
    {
        string alias = aliasInput != null ? aliasInput.text.Trim() : "";
        if (avatarIndex < 0) { Say("Pick an avatar."); return; }
        if (alias.Length == 0) { Say("Enter your name."); return; }
        if (genderIndex < 0) { Say("Pick a gender."); return; }

        PlayerPrefs.SetInt(Key("avatar"), avatarIndex);
        PlayerPrefs.SetInt(Key("role"), roleIndex);
        PlayerPrefs.SetInt(Key("gender"), genderIndex);
        PlayerPrefs.Save();

        var user = UserDetails.Instance;
        if (user != null)
        {
            user.firstName = alias;
            user.playerImageIndex = avatarIndex;
        }
        AuthManager.Instance?.QueueProfileUpdate("playerImageIndex", avatarIndex);
        AuthManager.Instance?.QueueProfileUpdate("firstName", alias);

        Close();
    }

    private void Say(string message)
    {
        if (statusText != null) statusText.text = message;
    }

    private void Close()
    {
        GameUIManager.Instance.ProfileScreenToggle(false);
        GameUIManager.Instance.MainMenuScreenToggle(true);
    }
}
