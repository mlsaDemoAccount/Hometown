using UnityEngine;

/// Single place that knows WHERE tutorial progress is stored. Right now it's local (PlayerPrefs).
/// When the API is ready, change only the bodies of these methods (e.g. read the flag from the
/// login response held in UserDetails, and POST it when marking complete) - GameplayTutorialManager
/// and everything else keeps calling the same two methods and never needs to change.
public static class TutorialProgressStore
{
    private const string GameplayTutorialKey = "tutorial_gameplay_completed";

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
}
