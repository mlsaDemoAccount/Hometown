using UnityEngine;
using UnityEngine.UI;

public class MainScreen : MonoBehaviour
{
    public Image logoImg;

    public Button[] LoginButtons;
    public Button[] IconButtons;

    [Header("Login Button Animation")]
    [SerializeField] private float popDuration = 0.45f;
    [SerializeField] private float delayBetweenButtons = 0.12f;

    [Header("Logo Beat Animation")]
    [SerializeField] private float logoBeatAmount = 1.05f;
    [SerializeField] private float logoBeatDuration = 1.5f;

    [Header("Icon Slide Animation")]
    [SerializeField] private float iconSlideDistance = 300f;
    [SerializeField] private float iconSlideDuration = 0.6f;
    [SerializeField] private float iconDelay = 0.1f;

    private Vector3 logoOriginalScale;

    void Start()
    {
        AnimateLoginButtons();
        AnimateLogo();
        AnimateIcons();
    }

    // -----------------------------
    // LOGIN BUTTONS POP ANIMATION
    // -----------------------------
    private void AnimateLoginButtons()
    {
        for (int i = 0; i < LoginButtons.Length; i++)
        {
            if (LoginButtons[i] == null)
                continue;

            RectTransform button = LoginButtons[i].GetComponent<RectTransform>();

            button.localScale = Vector3.zero;

            LeanTween.scale(
                button.gameObject,
                Vector3.one,
                popDuration
            )
            .setDelay(i * delayBetweenButtons)
            .setEase(LeanTweenType.easeOutBack);
        }
    }


    // -----------------------------
    // LOGO SLOW BEATING ANIMATION
    // -----------------------------
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


    // -----------------------------
    // ICON SLIDE LEFT ANIMATION
    // -----------------------------
    private void AnimateIcons()
    {
        for (int i = 0; i < IconButtons.Length; i++)
        {
            if (IconButtons[i] == null)
                continue;

            RectTransform icon = IconButtons[i].GetComponent<RectTransform>();

            // Store the icon's final position
            Vector2 targetPosition = icon.anchoredPosition;

            // Move icon to the LEFT before animation starts
            icon.anchoredPosition = targetPosition +
                                    Vector2.left * iconSlideDistance;

            // Slide from LEFT to its original position
            LeanTween.move(
                icon,
                targetPosition,
                iconSlideDuration
            )
            .setDelay(i * iconDelay)
            .setEase(LeanTweenType.easeOutCubic);
        }
    }



    // -----------------------------
    // LOGIN FUNCTIONS
    // -----------------------------

    public void GoogleLogin()
    {
        //GameUIManager.Instance.MainScreenToggle(false);
        //GameUIManager.Instance.LoginScreenToggle(true);
    }

    public void ClaudeLogin()
    {
        //GameUIManager.Instance.MainScreenToggle(false);
        //GameUIManager.Instance.LoginScreenToggle(true);
    }

    public void EmailLogin()
    {
        GameUIManager.Instance.MainScreenToggle(false);
        GameUIManager.Instance.LoginScreenToggle(true);
    }

    public void GuestLogin()
    {
        //GameUIManager.Instance.MainScreenToggle(false);
        //GameUIManager.Instance.LoginScreenToggle(true);
    }
}