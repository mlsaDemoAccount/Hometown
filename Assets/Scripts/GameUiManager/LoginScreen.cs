using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class LoginScreen : MonoBehaviour
{
    [Header("Logo")]
    public Image logoImg;

    [Header("Panels")]
    public GameObject signPanel;
    public GameObject accountCreatePanel;
    public GameObject ForgotPanel;

    [Header("Logo Beat Animation")]
    [SerializeField] private float logoBeatAmount = 1.05f;
    [SerializeField] private float logoBeatDuration = 1.5f;

    [Header("Panel Pop Animation")]
    [SerializeField] private float panelPopDuration = 0.6f;
    [SerializeField] private float panelDelay = 0.2f;

    // =========================================================
    // SIGN IN FIELDS
    // =========================================================

    [Header("Sign In Fields")]
    public InputField user;
    public InputField password;

    // =========================================================
    // CREATE ACCOUNT FIELDS
    // =========================================================

    [Header("Create Account Fields")]
    public InputField firstName;
    //public InputField lasstName;
    public InputField Email;
    public InputField ACpassword;
    public InputField ConfrimACpassword;

    // =========================================================
    // FORGOT PASSWORD
    // =========================================================

    [Header("Forgot Password")]
    public InputField forgotPassword;

    // =========================================================
    // MESSAGES
    // =========================================================

    [Header("Messages")]
    public TMP_Text messageText;

    private Vector3 logoOriginalScale;


    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        // Start with Sign In panel
        if (signPanel != null)
            signPanel.SetActive(true);

        if (accountCreatePanel != null)
            accountCreatePanel.SetActive(false);

        if (ForgotPanel != null)
            ForgotPanel.SetActive(false);

        if (messageText != null)
            messageText.text = "";

        AnimateLogo();

        if (signPanel != null)
            AnimateSignPanel(signPanel);
    }


    // =========================================================
    // LOGO ANIMATION
    // =========================================================

    private void AnimateLogo()
    {
        if (logoImg == null)
            return;

        RectTransform logo = logoImg.GetComponent<RectTransform>();

        logoOriginalScale = logo.localScale;

        // Cancel previous animation
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
    // PANEL POP ANIMATION
    // =========================================================

    private void AnimateSignPanel(GameObject aniPanel)
    {
        if (aniPanel == null)
            return;

        RectTransform panel = aniPanel.GetComponent<RectTransform>();

        if (panel == null)
            return;

        // Cancel previous animation
        LeanTween.cancel(aniPanel);

        // Save original scale
        Vector3 originalScale = panel.localScale;

        // Start small
        panel.localScale = Vector3.zero;

        // Pop animation
        LeanTween.scale(
            aniPanel,
            originalScale,
            panelPopDuration
        )
        .setDelay(panelDelay)
        .setEase(LeanTweenType.easeOutBack);
    }


    // =========================================================
    // LOGIN BUTTON
    // =========================================================

    public void OnLoginClick()
    {
        string savedEmail = PlayerPrefs.GetString("Email", "");
        string savedPassword = PlayerPrefs.GetString("Password", "");

        // Check if account exists
        if (savedEmail == "")
        {
            ShowMessage("No account found. Please create an account.");
            return;
        }

        // Check email and password
        if (user.text == savedEmail && password.text == savedPassword)
        {
            ShowMessage("Login Successful!");

            // Open Main Menu
            GameUIManager.Instance.MainMenuScreenToggle(true);
            GameUIManager.Instance.LoginScreenToggle(false);
        }
        else
        {
            ShowMessage("Invalid email or password.");
        }
    }


    // =========================================================
    // CREATE ACCOUNT BUTTON
    // =========================================================

    //public void OnCreateAccountClick()
    //{
    //    // Check empty fields
    //    if (firstName.text == "" ||
    //        //lasstName.text == "" ||
    //        Email.text == "" ||
    //        ACpassword.text == "")
    //    {
    //        ShowMessage("Please fill all fields.");
    //        return;
    //    }

    //    // Check if account already exists
    //    if (PlayerPrefs.HasKey("Email"))
    //    {
    //        ShowMessage("Account already exists.");
    //        return;
    //    }

    //    // Save account locally
    //    PlayerPrefs.SetString("FirstName", firstName.text);
    //    //PlayerPrefs.SetString("LastName", lasstName.text);
    //    PlayerPrefs.SetString("Email", Email.text);
    //    PlayerPrefs.SetString("Password", ACpassword.text);

    //    PlayerPrefs.Save();

    //    ShowMessage("Account created successfully!");

    //    // Go to Sign In
    //    accountCreatePanel.SetActive(false);
    //    signPanel.SetActive(true);

    //    AnimateSignPanel(signPanel);
    //}

    public void OnCreateAccountClick()
    {
        // Check empty fields
        if (firstName.text == "" ||
            Email.text == "" ||
            ACpassword.text == "" ||
            ConfrimACpassword.text == "")
        {
            ShowMessage("Please fill all fields.");
            return;
        }

        // Check Password and Confirm Password
        if (ACpassword.text != ConfrimACpassword.text)
        {
            ShowMessage("Passwords do not match.");
            return;
        }

        // Check if account already exists
        if (PlayerPrefs.HasKey("Email"))
        {
            ShowMessage("Account already exists.");
            return;
        }

        // Save account locally
        PlayerPrefs.SetString("FirstName", firstName.text);
        PlayerPrefs.SetString("Email", Email.text);
        PlayerPrefs.SetString("Password", ACpassword.text);

        PlayerPrefs.Save();

        ShowMessage("Account created successfully!");

        // Go to Sign In
        accountCreatePanel.SetActive(false);
        signPanel.SetActive(true);

        AnimateSignPanel(signPanel);
    }


    // =========================================================
    // CREATE ACCOUNT PANEL
    // =========================================================

    public void OnCreateAnAccountPanel()
    {
        if (accountCreatePanel == null)
            return;

        accountCreatePanel.SetActive(true);
        signPanel.SetActive(false);
        ForgotPanel.SetActive(false);

        AnimateSignPanel(accountCreatePanel);

        ShowMessage("");
    }


    // =========================================================
    // FORGOT PASSWORD PANEL
    // =========================================================

    public void OnForgotPosswordClick()
    {
        if (ForgotPanel == null)
            return;

        ForgotPanel.SetActive(true);
        signPanel.SetActive(false);
        accountCreatePanel.SetActive(false);

        AnimateSignPanel(ForgotPanel);

        ShowMessage("");
    }


    // =========================================================
    // FORGOT PASSWORD BUTTON
    // =========================================================

    public void OnForgotPasswordSubmit()
    {
        string savedEmail = PlayerPrefs.GetString("Email", "");
        string savedPassword = PlayerPrefs.GetString("Password", "");

        // Check account
        if (savedEmail == "")
        {
            ShowMessage("No account found.");
            return;
        }

        // Check email
        if (forgotPassword.text == savedEmail)
        {
            ShowMessage("Your password is: " + savedPassword);
        }
        else
        {
            ShowMessage("Email not found.");
        }
    }


    // =========================================================
    // ALREADY HAVE ACCOUNT / SIGN IN
    // =========================================================

    public void OnSignInClick()
    {
        accountCreatePanel.SetActive(false);
        ForgotPanel.SetActive(false);
        signPanel.SetActive(true);

        AnimateSignPanel(signPanel);

        ShowMessage("");
    }


    // =========================================================
    // BACK BUTTON
    // =========================================================

    public void OnBackButtonClick()
    {
        // If Create Account is open
        if (accountCreatePanel != null && accountCreatePanel.activeSelf)
        {
            accountCreatePanel.SetActive(false);

            signPanel.SetActive(true);

            AnimateSignPanel(signPanel);

            ShowMessage("");

            return;
        }

        // If Forgot Password is open
        if (ForgotPanel != null && ForgotPanel.activeSelf)
        {
            ForgotPanel.SetActive(false);

            signPanel.SetActive(true);

            AnimateSignPanel(signPanel);

            ShowMessage("");

            return;
        }

        // Otherwise go back to Main Screen
        GameUIManager.Instance.MainScreenToggle(true);
        GameUIManager.Instance.LoginScreenToggle(false);
    }


    // =========================================================
    // MESSAGE
    // =========================================================

    private void ShowMessage(string message)
    {
        if (messageText != null)
        {
            messageText.text = message;
        }
    }
}