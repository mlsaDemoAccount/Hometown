using UnityEngine;
using UnityEngine.UI;

public class MainScreen : MonoBehaviour
{
    // =========================================================
    // REFERENCES
    // =========================================================

    [Header("Logo")]
    public Image logoImg;

    [Header("Buttons")]
    public Button[] LoginButtons;
    public Button[] IconButtons;


    // =========================================================
    // LOGIN BUTTON ANIMATION
    // =========================================================

    [Header("Login Button Animation")]
    [SerializeField] private float popDuration = 0.45f;
    [SerializeField] private float delayBetweenButtons = 0.12f;


    // =========================================================
    // LOGO ANIMATION
    // =========================================================

    [Header("Logo Beat Animation")]
    [SerializeField] private float logoBeatAmount = 1.05f;
    [SerializeField] private float logoBeatDuration = 1.5f;


    // =========================================================
    // ICON ANIMATION
    // =========================================================

    [Header("Icon Slide Animation")]
    [SerializeField] private float iconSlideDistance = 300f;
    [SerializeField] private float iconSlideDuration = 0.6f;
    [SerializeField] private float iconDelay = 0.1f;


    private Vector3 logoOriginalScale;


    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        AnimateLoginButtons();
        AnimateLogo();
        AnimateIcons();
    }


    // =========================================================
    // LOGIN BUTTONS POP ANIMATION + ARRIVAL SOUND
    // =========================================================

    private void AnimateLoginButtons()
    {
        if (LoginButtons == null)
            return;

        for (int i = 0; i < LoginButtons.Length; i++)
        {
            if (LoginButtons[i] == null)
                continue;

            RectTransform button =
                LoginButtons[i].GetComponent<RectTransform>();

            if (button == null)
                continue;


            // Start from zero scale
            button.localScale = Vector3.zero;


            // Pop animation
            LeanTween.scale(
                button.gameObject,
                Vector3.one,
                popDuration
            )
            .setDelay(i * delayBetweenButtons)
            .setEase(LeanTweenType.easeOutBack)
            .setOnStart(() =>
            {
                //// Play arrival sound only for login buttons
                //if (SoundManager.Instance != null)
                //{
                //    SoundManager.Instance
                //        .PlayButtonArrivalSound();
                //}
            });
        }
    }


    // =========================================================
    // LOGO SLOW BEATING ANIMATION
    // =========================================================

    private void AnimateLogo()
    {
        if (logoImg == null)
            return;

        RectTransform logo =
            logoImg.GetComponent<RectTransform>();

        if (logo == null)
            return;

        logoOriginalScale = logo.localScale;

        LeanTween.cancel(logo.gameObject);

        LeanTween.scale(
            logo.gameObject,
            logoOriginalScale * logoBeatAmount,
            logoBeatDuration
        )
        .setEase(LeanTweenType.easeInOutSine)
        .setLoopPingPong();
    }


    // =========================================================
    // ICON SLIDE ANIMATION
    // NO SOUND HERE
    // =========================================================

    private void AnimateIcons()
    {
        if (IconButtons == null)
            return;

        for (int i = 0; i < IconButtons.Length; i++)
        {
            if (IconButtons[i] == null)
                continue;

            RectTransform icon =
                IconButtons[i].GetComponent<RectTransform>();

            if (icon == null)
                continue;


            // Store final position
            Vector2 targetPosition =
                icon.anchoredPosition;


            // Start from left side
            icon.anchoredPosition =
                targetPosition +
                Vector2.left * iconSlideDistance;


            // Slide to original position
            LeanTween.move(
                icon,
                targetPosition,
                iconSlideDuration
            )
            .setDelay(i * iconDelay)
            .setEase(LeanTweenType.easeOutCubic);


            // IMPORTANT:
            // No sound is played for IconButtons
        }
    }


    // =========================================================
    // GOOGLE LOGIN
    // =========================================================

    public void GoogleLogin()
    {
        // Add Google login functionality here later
    }


    // =========================================================
    // CLAUDE LOGIN
    // =========================================================

    public void ClaudeLogin()
    {
        // Add Claude login functionality here later
    }


    // =========================================================
    // EMAIL LOGIN
    // =========================================================

    public void EmailLogin()
    {
        if (GameUIManager.Instance == null)
            return;

        GameUIManager.Instance
            .MainScreenToggle(false);

        GameUIManager.Instance
            .LoginScreenToggle(true);
    }


    // =========================================================
    // GUEST LOGIN
    // =========================================================

    public void GuestLogin()
    {
        // Add Guest login functionality here later
    }
}