using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// A small "avatar + name" display that stays in sync with a player's profile: put it on any
/// profile card (main menu, lobby, gameplay HUD) and assign the pieces it should fill. Me = this
/// phone's player, Partner = the friend of the current city. Polled twice a second (no events).
public class ProfileBadge : MonoBehaviour
{
    public enum Who { Me, Partner }

    public Who who = Who.Me;
    [Tooltip("Optional. The avatar picture (library sprite, or a coloured disc with the first letter).")]
    public Image avatarImage;
    [Tooltip("Optional. Created automatically under the avatar when left empty.")]
    public TMP_Text initialText;
    [Tooltip("Optional. The player's name.")]
    public TMP_Text nameText;
    [Tooltip("Name shown for the Partner while the friend is not known yet.")]
    public string unknownName = "Waiting...";

    private float nextRefresh;

    void OnEnable() => Refresh();

    void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.5f;
        Refresh();
    }

    public void Refresh()
    {
        string name;
        int avatar;
        if (who == Who.Me)
        {
            name = PlayerIdentity.MyName;
            avatar = PlayerIdentity.MyAvatarIndex;
        }
        else
        {
            var partner = PlayerIdentity.Partner;
            name = partner != null ? partner.firstName : unknownName;
            avatar = partner != null ? partner.playerImageIndex : 0;
        }

        if (nameText != null) nameText.text = name;
        if (avatarImage != null)
        {
            if (initialText == null) initialText = PlayerIdentity.EnsureInitial(avatarImage, nameText != null ? nameText.font : null);
            PlayerIdentity.ApplyAvatar(avatarImage, initialText, avatar, name);
        }
    }
}
