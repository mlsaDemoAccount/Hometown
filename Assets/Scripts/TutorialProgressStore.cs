using UnityEngine;

/// Single place that knows WHERE tutorial progress is stored. Right now it's local (PlayerPrefs).
/// When the API is ready, change only the bodies of these methods (e.g. read the flag from the
/// login response held in UserDetails, and POST it when marking complete) - GameplayTutorialManager
/// and everything else keeps calling the same two methods and never needs to change.
public static class TutorialProgressStore
{
    // Per player (user id, or "guest"), so a new account/guest sees the tutorial again.
    private static string GameplayTutorialKey => "tutorial_gameplay_completed_" + UserDetails.SaveScope;

    public static bool IsGameplayTutorialCompleted()
    {
        return PlayerPrefs.GetInt(GameplayTutorialKey, 0) == 1;
    }

    public static void MarkGameplayTutorialCompleted()
    {
        PlayerPrefs.SetInt(GameplayTutorialKey, 1);
        PlayerPrefs.Save();
    }

    /// For testing - makes the tutorial show again next time.
    public static void ResetGameplayTutorial()
    {
        PlayerPrefs.DeleteKey(GameplayTutorialKey);
        PlayerPrefs.Save();
    }

    /// Starts a guest session with the tutorial unseen, like a new player.
    public static void ResetGuestTutorial()
    {
        PlayerPrefs.DeleteKey("tutorial_gameplay_completed_guest");
        PlayerPrefs.Save();
    }
}
