using UnityEngine;
using UnityEngine.UI;

public class LoginScreen : MonoBehaviour
{
    [Header("Logo")]
    public Image logoImg;

    [Header("Sign In Panel")]
    public GameObject signPanel;

    [Header("Logo Beat Animation")]
    [SerializeField] private float logoBeatAmount = 1.05f;
    [SerializeField] private float logoBeatDuration = 1.5f;

    [Header("Sign In Panel Pop Animation")]
    [SerializeField] private float panelPopDuration = 0.6f;
    [SerializeField] private float panelDelay = 0.2f;

    [Header("Login Fields")]
    public InputField user;
    public InputField password;

    private Vector3 logoOriginalScale;
    private Vector3 panelOriginalScale;


    void Start()
    {
        AnimateLogo();
        AnimateSignPanel();
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
    // SIGN IN PANEL POP ANIMATION
    // -----------------------------
    private void AnimateSignPanel()
    {
        if (signPanel == null)
            return;

        RectTransform panel = signPanel.GetComponent<RectTransform>();

        // Save original scale
        panelOriginalScale = panel.localScale;

        // Start small
        panel.localScale = Vector3.zero;

        // Pop up
        LeanTween.scale(
            panel.gameObject,
            panelOriginalScale,
            panelPopDuration
        )
        .setDelay(panelDelay)
        .setEase(LeanTweenType.easeOutBack);
    }


    // -----------------------------
    // LOGIN BUTTON
    // -----------------------------
    public void OnLoginClick()
    {
        GameUIManager.Instance.MainMenuScreenToggle(true);
        GameUIManager.Instance.LoginScreenToggle(false);
    }


    // -----------------------------
    // BACK BUTTON
    // -----------------------------
    public void OnBackButtonClick()
    {
        GameUIManager.Instance.MainScreenToggle(true);
        GameUIManager.Instance.LoginScreenToggle(false);
    }
}