using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuScreen : MonoBehaviour
{
    [Header("UI References")]
    public Image logoImg;
    public Button[] menuButtons;
    public Image profile;
    public Button PlayBtn;


    [Header("Logo Beat Animation")]
    [SerializeField] private float logoBeatAmount = 1.05f;
    [SerializeField] private float logoBeatDuration = 1.5f;


    [Header("Play Button Slide Animation")]
    [SerializeField] private float playSlideDistance = 500f;
    [SerializeField] private float playSlideDuration = 0.7f;
    [SerializeField] private float playDelay = 0.2f;


    [Header("Menu Buttons Slide Animation")]
    [SerializeField] private float menuSlideDistance = 500f;
    [SerializeField] private float menuSlideDuration = 0.6f;
    [SerializeField] private float menuButtonDelay = 0.12f;


    [Header("Profile Pop Animation")]
    [SerializeField] private float profilePopDuration = 0.6f;
    [SerializeField] private float profileDelay = 0.1f;


    private Vector3 logoOriginalScale;
    private Vector3 profileOriginalScale;


    void Start()
    {
        AnimateLogo();
        AnimatePlayButton();
        AnimateMenuButtons();
        AnimateProfile();
    }


    // =========================================================
    // LOGO SLOW BEATING ANIMATION
    // =========================================================

    private void AnimateLogo()
    {
        if (logoImg == null)
            return;

        RectTransform logo = logoImg.GetComponent<RectTransform>();

        logoOriginalScale = logo.localScale;

        LeanTween.scale(
            logo.gameObject,
            logoOriginalScale * logoBeatAmount,
            logoBeatDuration
        )
        .setEase(LeanTweenType.easeInOutSine)
        .setLoopPingPong();
    }


    // =========================================================
    // PLAY BUTTON SLIDE FROM LEFT
    // =========================================================

    private void AnimatePlayButton()
    {
        if (PlayBtn == null)
            return;

        RectTransform playButton = PlayBtn.GetComponent<RectTransform>();

        // Store final position
        Vector2 targetPosition = playButton.anchoredPosition;

        // Move button to the LEFT
        playButton.anchoredPosition =
            targetPosition + Vector2.left * playSlideDistance;

        // Slide into final position
        LeanTween.move(
            playButton,
            targetPosition,
            playSlideDuration
        )
        .setDelay(playDelay)
        .setEase(LeanTweenType.easeOutCubic);
    }


    // =========================================================
    // MENU BUTTONS SLIDE FROM LEFT ONE BY ONE
    // =========================================================

    private void AnimateMenuButtons()
    {
        if (menuButtons == null)
            return;

        for (int i = 0; i < menuButtons.Length; i++)
        {
            if (menuButtons[i] == null)
                continue;

            RectTransform button =
                menuButtons[i].GetComponent<RectTransform>();

            // Store final position
            Vector2 targetPosition = button.anchoredPosition;

            // Start from the LEFT
            button.anchoredPosition =
                targetPosition + Vector2.left * menuSlideDistance;

            // Slide to final position
            LeanTween.move(
                button,
                targetPosition,
                menuSlideDuration
            )
            .setDelay(i * menuButtonDelay)
            .setEase(LeanTweenType.easeOutCubic);
        }
    }


    // =========================================================
    // PROFILE POP ANIMATION
    // =========================================================

    private void AnimateProfile()
    {
        if (profile == null)
            return;

        RectTransform profileRect =
            profile.GetComponent<RectTransform>();

        // Store original scale
        profileOriginalScale = profileRect.localScale;

        // Start small
        profileRect.localScale = Vector3.zero;

        // Pop into place
        LeanTween.scale(
            profileRect.gameObject,
            profileOriginalScale,
            profilePopDuration
        )
        .setDelay(profileDelay)
        .setEase(LeanTweenType.easeOutBack);
    }


    // =========================================================
    // BUTTON FUNCTIONS
    // =========================================================

    public void OnBetterTogetherClick()
    {
        GameUIManager.Instance.MainMenuScreenToggle(false);

        // Put your actual scene name here
        SceneManager.LoadScene(1);
    }


    public void OnTutorialClick()
    {

    }


    public void OnProfileClick()
    {

    }


    public void OnSettingsClick()
    {

    }


    public void OnRequestClick()
    {

    }
}