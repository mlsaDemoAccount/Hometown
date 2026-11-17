using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Who is playing: this phone's player (name, avatar, gender from the Profile screen / account) and
/// the friend (from the cities backend, kept fresh by CityLauncher). Screens ask here instead of
/// each reading UserDetails / PlayerPrefs themselves.
public static class PlayerIdentity
{
    public static string MyName
    {
        get
        {
            var user = UserDetails.Instance;
            if (user == null) return "You";
            if (!string.IsNullOrEmpty(user.firstName)) return user.firstName;
            if (!string.IsNullOrEmpty(user.username)) return user.username;
            return "You";
        }
    }

    /// The avatar picked on the Profile screen, else the one stored on the account (getProfile).
    public static int MyAvatarIndex
    {
        get
        {
            int saved = ProfileScreen.SavedAvatarIndex;
            if (saved >= 0) return saved;
            var user = UserDetails.Instance;
            return user != null && user.isLoggedIn ? Mathf.Max(0, user.playerImageIndex) : 0;
        }
    }

    /// 0 = Male, 1 = Female, -1 = not chosen yet.
    public static int MyGender => ProfileScreen.SavedGender;

    /// The friend of the city being played (null until known).
    public static ParticipantDto Partner => CityLauncher.IsCityMode ? CityLauncher.Instance.Partner : null;

    private static Sprite disc;

    /// A plain white disc (runtime has no access to the editor's built-in sprites).
    public static Sprite Disc
    {
        get
        {
            if (disc != null) return disc;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(n / 2f - d)));
                }
            tex.Apply();
            disc = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
            return disc;
        }
    }

    /// Shows an avatar: the library's picture, or a coloured disc with the first letter of the name
    /// until that avatar has art. initial may be null.
    public static void ApplyAvatar(Image image, TMP_Text initial, int index, string name)
    {
        if (image == null) return;
        image.preserveAspect = true;
        var sprite = AvatarLibrary.Icon(index); // cropped small icon (Profile screen keeps the full picture)
        if (sprite != null)
        {
            image.sprite = sprite;
            image.color = Color.white;
            if (initial != null) initial.text = "";
        }
        else
        {
            image.sprite = Disc;
            image.color = ProfileScreen.AvatarColor(index);
            if (initial != null) initial.text = string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpperInvariant();
        }
    }

    /// A centred white bold letter on top of an avatar image (made once per image).
    public static TMP_Text EnsureInitial(Image image, TMP_FontAsset font = null)
    {
        var existing = image.transform.Find("Initial");
        if (existing != null) return existing.GetComponent<TMP_Text>();
        var go = new GameObject("Initial", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(image.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var text = go.GetComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.enableAutoSizing = true; text.fontSizeMin = 10; text.fontSizeMax = 60;
        text.fontStyle = FontStyles.Bold; text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white; text.raycastTarget = false; text.text = "";
        return text;
    }
}
