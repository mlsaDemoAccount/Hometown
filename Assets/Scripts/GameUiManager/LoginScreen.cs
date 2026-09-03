using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class LoginScreen : MonoBehaviour
{
    // =========================================================
    // LOGO
    // =========================================================

    [Header("Logo")]
    public Image logoImg;


    // =========================================================
    // PANELS
    // =========================================================

    [Header("Panels")]
    public GameObject signPanel;
    public GameObject accountCreatePanel;
    public GameObject ForgotPanel;


    // =========================================================
    // LOGO ANIMATION
    // =========================================================

    [Header("Logo Beat Animation")]
    [SerializeField] private float logoBeatAmount = 1.05f;
    [SerializeField] private float logoBeatDuration = 1.5f;


    // =========================================================
    // PANEL ANIMATION
    // =========================================================

    [Header("Panel Pop Animation")]
    [SerializeField] private float panelPopDuration = 0.6f;
    [SerializeField] private float panelDelay = 0.2f;


    // =========================================================
    // SIGN IN
    // =========================================================

    [Header("Sign In Fields")]
    public InputField user;
    public InputField password;


    // =========================================================
    // LOGIN BUTTON
    // =========================================================

    [Header("Login Button")]
    public Button loginButton;

    [Header("Wrong Login Animation")]
    [SerializeField] private float shakeDistance = 15f;
    [SerializeField] private float shakeDuration = 0.06f;


    // =========================================================
    // CREATE ACCOUNT
    // =========================================================

    [Header("Create Account Fields")]
    public InputField firstName;
    public InputField Email;
    public InputField ACpassword;
    public InputField ConfrimACpassword;


    // =========================================================
    // FORGOT PASSWORD
    // =========================================================

    [Header("Forgot Password")]
    public InputField forgotPassword;


    // =========================================================
    // MESSAGE
    // =========================================================

    [Header("Messages")]
    public TMP_Text messageText;


    private Vector3 logoOriginalScale;


    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        // Start with Sign In Panel
        if (signPanel != null)
        {
            signPanel.SetActive(true);
        }

        if (accountCreatePanel != null)
        {
            accountCreatePanel.SetActive(false);
        }

        if (ForgotPanel != null)
        {
            ForgotPanel.SetActive(false);
        }

        if (messageText != null)
        {
            messageText.text = "";
        }


        // Logo Animation
        AnimateLogo();


        // Sign Panel Animation + Sound
        if (signPanel != null)
        {
            AnimateSignPanel(signPanel);
        }


        // =====================================================
        // TYPING SOUND LISTENERS
        // =====================================================

        if (user != null)
        {
            user.onValueChanged.AddListener(PlayTypingSound);
        }

        if (password != null)
        {
            password.onValueChanged.AddListener(PlayTypingSound);
        }

        if (firstName != null)
        {
            firstName.onValueChanged.AddListener(PlayTypingSound);
        }

        if (Email != null)
        {
            Email.onValueChanged.AddListener(PlayTypingSound);
        }

        if (ACpassword != null)
        {
            ACpassword.onValueChanged.AddListener(PlayTypingSound);
        }

        if (ConfrimACpassword != null)
        {
            ConfrimACpassword.onValueChanged.AddListener(PlayTypingSound);
        }

        if (forgotPassword != null)
        {
            forgotPassword.onValueChanged.AddListener(PlayTypingSound);
        }
    }


    // =========================================================
    // LOGO ANIMATION
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
    // PANEL POP ANIMATION + SOUND
    // =========================================================

    private void AnimateSignPanel(GameObject aniPanel)
    {
        if (aniPanel == null)
            return;

        RectTransform panel =
            aniPanel.GetComponent<RectTransform>();

        if (panel == null)
            return;

        LeanTween.cancel(aniPanel);

        Vector3 originalScale = panel.localScale;

        panel.localScale = Vector3.zero;


        // Play Panel Open Sound
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayPanelOpenSound();
        }


        // Panel Pop Animation
        LeanTween.scale(
            aniPanel,
            originalScale,
            panelPopDuration
        )
        .setDelay(panelDelay)
        .setEase(LeanTweenType.easeOutBack);
    }


    // =========================================================
    // LOGIN
    // =========================================================

    public void OnLoginClick()
    {
        string savedEmail =
            PlayerPrefs.GetString("Email", "");

        string savedPassword =
            PlayerPrefs.GetString("Password", "");


        // No Account
        if (savedEmail == "")
        {
            ShowMessage(
                "No account found. Please create an account."
            );

            PlayWrongSound();

            ShakeLoginButton();

            return;
        }


        // Empty Fields
        if (string.IsNullOrEmpty(user.text) ||
            string.IsNullOrEmpty(password.text))
        {
            ShowMessage(
                "Please enter email and password."
            );

            PlayWrongSound();

            ShakeLoginButton();

            return;
        }


        // Correct Login
        if (user.text == savedEmail &&
            password.text == savedPassword)
        {
            ShowMessage("Login Successful!");

            GameUIManager.Instance
                .MainMenuScreenToggle(true);

            GameUIManager.Instance
                .LoginScreenToggle(false);
        }
        else
        {
            ShowMessage(
                "Invalid email or password."
            );

            PlayWrongSound();

            ShakeLoginButton();
        }
    }


    // =========================================================
    // LOGIN BUTTON SHAKE
    // =========================================================

    private void ShakeLoginButton()
    {
        if (loginButton == null)
            return;

        RectTransform buttonRect =
            loginButton.GetComponent<RectTransform>();

        if (buttonRect == null)
            return;

        LeanTween.cancel(loginButton.gameObject);

        float originalX =
            buttonRect.localPosition.x;


        LeanTween.moveLocalX(
            loginButton.gameObject,
            originalX - shakeDistance,
            shakeDuration
        )
        .setEaseLinear()
        .setOnComplete(() =>
        {
            LeanTween.moveLocalX(
                loginButton.gameObject,
                originalX + shakeDistance,
                shakeDuration
            )
            .setEaseLinear()
            .setOnComplete(() =>
            {
                LeanTween.moveLocalX(
                    loginButton.gameObject,
                    originalX - shakeDistance,
                    shakeDuration
                )
                .setEaseLinear()
                .setOnComplete(() =>
                {
                    LeanTween.moveLocalX(
                        loginButton.gameObject,
                        originalX + shakeDistance,
                        shakeDuration
                    )
                    .setEaseLinear()
                    .setOnComplete(() =>
                    {
                        LeanTween.moveLocalX(
                            loginButton.gameObject,
                            originalX,
                            shakeDuration
                        )
                        .setEaseLinear();
                    });
                });
            });
        });
    }


    // =========================================================
    // CREATE ACCOUNT
    // =========================================================

    public void OnCreateAccountClick()
    {
        if (firstName == null ||
            Email == null ||
            ACpassword == null ||
            ConfrimACpassword == null)
        {
            return;
        }


        // Empty Fields
        if (string.IsNullOrEmpty(firstName.text) ||
            string.IsNullOrEmpty(Email.text) ||
            string.IsNullOrEmpty(ACpassword.text) ||
            string.IsNullOrEmpty(ConfrimACpassword.text))
        {
            ShowMessage("Please fill all fields.");

            PlayWrongSound();

            return;
        }


        // Password Confirmation
        if (ACpassword.text !=
            ConfrimACpassword.text)
        {
            ShowMessage(
                "Passwords do not match."
            );

            PlayWrongSound();

            return;
        }


        // Account Already Exists
        if (PlayerPrefs.HasKey("Email"))
        {
            ShowMessage(
                "Account already exists."
            );

            PlayWrongSound();

            return;
        }


        // Save Account
        PlayerPrefs.SetString(
            "FirstName",
            firstName.text
        );

        PlayerPrefs.SetString(
            "Email",
            Email.text
        );

        PlayerPrefs.SetString(
            "Password",
            ACpassword.text
        );

        PlayerPrefs.Save();


        ShowMessage(
            "Account created successfully!"
        );


        // Open Sign Panel
        accountCreatePanel.SetActive(false);
        signPanel.SetActive(true);

        AnimateSignPanel(signPanel);
    }


    // =========================================================
    // OPEN CREATE ACCOUNT PANEL
    // =========================================================

    public void OnCreateAnAccountPanel()
    {
        if (accountCreatePanel == null)
            return;


        accountCreatePanel.SetActive(true);


        if (signPanel != null)
        {
            signPanel.SetActive(false);
        }


        if (ForgotPanel != null)
        {
            ForgotPanel.SetActive(false);
        }


        AnimateSignPanel(accountCreatePanel);


        ShowMessage("");
    }


    // =========================================================
    // OPEN FORGOT PASSWORD PANEL
    // =========================================================

    public void OnForgotPosswordClick()
    {
        if (ForgotPanel == null)
            return;


        ForgotPanel.SetActive(true);


        if (signPanel != null)
        {
            signPanel.SetActive(false);
        }


        if (accountCreatePanel != null)
        {
            accountCreatePanel.SetActive(false);
        }


        AnimateSignPanel(ForgotPanel);


        ShowMessage("");
    }


    // =========================================================
    // FORGOT PASSWORD
    // =========================================================

    public void OnForgotPasswordSubmit()
    {
        string savedEmail =
            PlayerPrefs.GetString(
                "Email",
                ""
            );

        string savedPassword =
            PlayerPrefs.GetString(
                "Password",
                ""
            );


        // No Account
        if (savedEmail == "")
        {
            ShowMessage(
                "No account found."
            );

            PlayWrongSound();

            return;
        }


        // Empty Email
        if (forgotPassword == null ||
            string.IsNullOrEmpty(
                forgotPassword.text
            ))
        {
            ShowMessage(
                "Please enter your email."
            );

            PlayWrongSound();

            return;
        }


        // Correct Email
        if (forgotPassword.text ==
            savedEmail)
        {
            ShowMessage(
                "Your password is: " +
                savedPassword
            );
        }
        else
        {
            ShowMessage(
                "Email not found."
            );

            PlayWrongSound();
        }
    }


    // =========================================================
    // SIGN IN
    // =========================================================

    public void OnSignInClick()
    {
        if (accountCreatePanel != null)
        {
            accountCreatePanel.SetActive(false);
        }


        if (ForgotPanel != null)
        {
            ForgotPanel.SetActive(false);
        }


        if (signPanel != null)
        {
            signPanel.SetActive(true);

            AnimateSignPanel(signPanel);
        }


        ShowMessage("");
    }


    // =========================================================
    // BACK BUTTON
    // =========================================================

    public void OnBackButtonClick()
    {
        // If Create Account Panel Open
        if (accountCreatePanel != null &&
            accountCreatePanel.activeSelf)
        {
            accountCreatePanel.SetActive(false);

            if (signPanel != null)
            {
                signPanel.SetActive(true);

                AnimateSignPanel(signPanel);
            }

            ShowMessage("");

            return;
        }


        // If Forgot Password Panel Open
        if (ForgotPanel != null &&
            ForgotPanel.activeSelf)
        {
            ForgotPanel.SetActive(false);

            if (signPanel != null)
            {
                signPanel.SetActive(true);

                AnimateSignPanel(signPanel);
            }

            ShowMessage("");

            return;
        }


        // Otherwise Back to Main Screen
        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance
                .MainScreenToggle(true);

            GameUIManager.Instance
                .LoginScreenToggle(false);
        }
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


    // =========================================================
    // TYPING SOUND
    // =========================================================

    private void PlayTypingSound(string value)
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance
                .PlayTypingSound();
        }
    }


    // =========================================================
    // WRONG SOUND
    // =========================================================

    private void PlayWrongSound()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance
                .PlayWrongSound();
        }
    }
}