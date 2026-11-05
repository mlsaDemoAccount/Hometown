using UnityEngine;

/// The pictures every scene shares: the profile avatars (same order as the grid on the Profile
/// screen) and the big lobby characters per gender. Lives in Assets/Resources/AvatarLibrary.asset -
/// drop the sprites into it once and the Profile screen, friends list, lobby and gameplay all use them.
public class AvatarLibrary : ScriptableObject
{
    [Tooltip("One sprite per avatar number (index 0 = first avatar in the Profile screen grid). Empty entries show a coloured placeholder.")]
    public Sprite[] avatars = new Sprite[12];
    [Tooltip("Big character shown in the lobby for a Male player.")]
    public Sprite maleCharacter;
    [Tooltip("Big character shown in the lobby for a Female player.")]
    public Sprite femaleCharacter;

    private static AvatarLibrary cached;

    public static AvatarLibrary Instance
    {
        get
        {
            if (cached == null) cached = Resources.Load<AvatarLibrary>("AvatarLibrary");
            return cached;
        }
    }

    public static Sprite Avatar(int index)
    {
        var library = Instance;
        if (library == null || library.avatars == null || index < 0 || index >= library.avatars.Length) return null;
        return library.avatars[index];
    }

    /// gender: 0 = Male, 1 = Female, anything else = unknown (null: keep what the scene shows).
    public static Sprite Character(int gender)
    {
        var library = Instance;
        if (library == null) return null;
        return gender == 0 ? library.maleCharacter : gender == 1 ? library.femaleCharacter : null;
    }
}
