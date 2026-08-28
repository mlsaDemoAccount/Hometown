using UnityEngine;
using static UnityEngine.Rendering.DebugUI;

public class GameUIManager : MonoBehaviour
{
    public static GameUIManager Instance;


    [Header("Screens")]
    [SerializeField] private SplashScreen splashScreen;
     [SerializeField] private LoginScreen loginScreen;
     [SerializeField] private MainScreen mainScreen;
     [SerializeField] private MainMenuScreen mainMenuScreen;
     [SerializeField] private InvitePlayerScreen invitePlayerScreen;
     [SerializeField] private LobbyScreen lobbyScreen;

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

    public void LobbyScreenToggle(bool value)
    {
        lobbyScreen.gameObject.SetActive(value);
    }
    public void Init()
    {
        splashScreen.gameObject.SetActive(true);
        mainScreen.gameObject.SetActive(false);
        loginScreen.gameObject.SetActive(false);
        mainMenuScreen.gameObject.SetActive(false);
        invitePlayerScreen.gameObject.SetActive(false);
        lobbyScreen.gameObject.SetActive(false);

    }
  
}