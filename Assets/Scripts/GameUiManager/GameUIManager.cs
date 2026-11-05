using UnityEngine;
using static UnityEngine.Rendering.DebugUI;

public class GameUIManager : MonoBehaviour
{
    public static GameUIManager Instance;

    /// Set before loading the menu from a game: a signed-in player lands on the main menu instead of the splash and login.
    public static bool ReturningFromGame;

    [Header("Screens")]
    [SerializeField] private SplashScreen splashScreen;
     [SerializeField] private LoginScreen loginScreen;
     [SerializeField] private MainScreen mainScreen;
     [SerializeField] private MainMenuScreen mainMenuScreen;
     [SerializeField] private InvitePlayerScreen invitePlayerScreen;
     [SerializeField] private LobbyScreen lobbyScreen;
     [SerializeField] private LoadingScreen loadingScreen;
     [SerializeField] private GameObject settingScreen;
     [SerializeField] private ProfileScreen profileScreen;

    private void Awake()
    {
        Instance = this;

        if (splashScreen == null)
        {
            Debug.LogError("GameUIManager: SplashScreen is NOT assigned in Inspector!");
        }

        Init();

    }

    public void SplashScreenToggle(bool value)
    {
        splashScreen.gameObject.SetActive(value);
    }

    public void MainScreenToggle(bool value)
    {
        mainScreen.gameObject.SetActive(value);
    }

    public void LoginScreenToggle(bool value)
    {
        loginScreen.gameObject.SetActive(value);
    }

    public void MainMenuScreenToggle(bool value)
    {
        mainMenuScreen.gameObject.SetActive(value);
    }

    public void InvitePlayerScreenToggle(bool value)
    {
        invitePlayerScreen.gameObject.SetActive(value);
    }

    public void LoadingScreenToggle(bool value)
    {
        loadingScreen.gameObject.SetActive(value);
    }

    public void LobbyScreenToggle(bool value)
    {
        lobbyScreen.gameObject.SetActive(value);
    }

    public void SettingScreenToggle(bool value)
    {
        settingScreen.gameObject.SetActive(value);
    }
    public void ProfileScreenToggle(bool value)
    {
        if (profileScreen != null) profileScreen.gameObject.SetActive(value);
    }

    public void Init()
    {
        bool skipSplash = ReturningFromGame && UserDetails.Instance != null && UserDetails.Instance.isLoggedIn;
        ReturningFromGame = false;
        splashScreen.gameObject.SetActive(!skipSplash);
        mainScreen.gameObject.SetActive(false);
        loginScreen.gameObject.SetActive(false);
        mainMenuScreen.gameObject.SetActive(skipSplash);
        invitePlayerScreen.gameObject.SetActive(false);
        lobbyScreen.gameObject.SetActive(false);
        loadingScreen.gameObject.SetActive(false);
        settingScreen.gameObject.SetActive(false);
        if (profileScreen != null) profileScreen.gameObject.SetActive(false);

    }
  
}