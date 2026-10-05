using System.Collections;
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
    // VERIFY ACCOUNT / RESET PASSWORD (API flow)
    // =========================================================

    [Header("Verify Account (OTP after registering)")]
    public GameObject otpPanel;
    public InputField otpField;

    [Header("Reset Password (OTP + new password)")]
    public GameObject resetPanel;
    public InputField resetOtpField;
    public InputField resetNewPassword;
    public InputField resetConfirmPassword;


    // =========================================================
    // MESSAGE
    // =========================================================

    [Header("Messages")]
    public TMP_Text messageText;


    private Vector3 logoOriginalScale;

    // True while an API call is in flight - ignores extra taps so nothing is sent twice.
    private bool isBusy = false;
    private string pendingEmail = "";
    private string pendingPassword = "";


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

        if (otpPanel != null) otpPanel.SetActive(false);
        if (resetPanel != null) resetPanel.SetActive(false);

        if (messageText != null)
        {
            messageText.text = "";
        }

        // The old local-only "account" (email + PLAINTEXT password in PlayerPrefs) is gone -
        // accounts live on the server now, so wipe any leftover copy.
        PlayerPrefs.DeleteKey("Email");
        PlayerPrefs.DeleteKey("Password");
        PlayerPrefs.DeleteKey("FirstName");


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
        if (isBusy) return;

        if (string.IsNullOrEmpty(user.text) ||
            string.IsNullOrEmpty(password.text))
        {
            ShowMessage("Please enter email and password.");
            PlayWrongSound();
            ShakeLoginButton();
            return;
        }

        StartCoroutine(LoginFlow(user.text.Trim(), password.text, true));
    }

    /// Shared by the Sign In button and the auto-login right after account verification.
    /// showShakeOnFail = false when the Sign In button wasn't what started this, so there's nothing to shake.
    private IEnumerator LoginFlow(string email, string pass, bool showShakeOnFail)
    {
        isBusy = true;
        ShowMessage("Signing in...");

        var handle = AuthManager.Instance.Login(email, pass);
        while (!handle.isDone) yield return null;

        isBusy = false;

        if (handle.success)
        {
            ShowMessage("");
            GameUIManager.Instance.MainMenuScreenToggle(true);
            GameUIManager.Instance.LoginScreenToggle(false);
            yield break;
        }

        ShowMessage(handle.message);
        PlayWrongSound();
        if (showShakeOnFail) ShakeLoginButton();
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
        if (isBusy) return;

        if (firstName == null ||
            Email == null ||
            ACpassword == null ||
            ConfrimACpassword == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(firstName.text) ||
            string.IsNullOrWhiteSpace(Email.text) ||
            string.IsNullOrEmpty(ACpassword.text) ||
            string.IsNullOrEmpty(ConfrimACpassword.text))
        {
            ShowMessage("Please fill all fields.");
            PlayWrongSound();
            return;
        }

        if (!IsValidEmail(Email.text.Trim()))
        {
            ShowMessage("Please enter a valid email address.");
            PlayWrongSound();
            return;
        }

        if (ACpassword.text != ConfrimACpassword.text)
        {
            ShowMessage("Passwords do not match.");
            PlayWrongSound();
            return;
        }

        StartCoroutine(RegisterFlow(firstName.text.Trim(), Email.text.Trim(), ACpassword.text));
    }

    private IEnumerator RegisterFlow(string name, string email, string pass)
    {
        isBusy = true;
        ShowMessage("Creating account...");

        var handle = AuthManager.Instance.Register(name, email, pass);
        while (!handle.isDone) yield return null;

        isBusy = false;

        if (!handle.success)
        {
            ShowMessage(handle.message);
            PlayWrongSound();
            yield break;
        }

        // Kept only in memory so we can sign the player in automatically once the code is verified.
        pendingEmail = email;
        pendingPassword = pass;

        ShowPanelOnly(otpPanel);
        if (otpField != null) otpField.text = "";
        ShowMessage("We sent a verification code to " + email + ".");
    }


    // =========================================================
    // VERIFY ACCOUNT (OTP after registering)
    // =========================================================

    public void OnVerifyAccountClick()
    {
        if (isBusy) return;

        if (otpField == null || string.IsNullOrWhiteSpace(otpField.text))
        {
            ShowMessage("Please enter the verification code.");
            PlayWrongSound();
            return;
        }

        StartCoroutine(VerifyAccountFlow(otpField.text.Trim()));
    }

    private IEnumerator VerifyAccountFlow(string otp)
    {
        isBusy = true;
        ShowMessage("Verifying...");

        var handle = AuthManager.Instance.VerifyAccount(pendingEmail, otp);
        while (!handle.isDone) yield return null;

        isBusy = false;

        if (!handle.success)
        {
            ShowMessage(handle.message);
            PlayWrongSound();
            yield break;
        }

        // Verified - sign in with the details they just registered with.
        string email = pendingEmail;
        string pass = pendingPassword;
        pendingPassword = "";

        yield return LoginFlow(email, pass, false);

        // Only reached if the automatic sign-in failed (on success the Login screen has already
        // been closed, which ends this coroutine) - fall back to Sign In with their email filled in.
        ShowPanelOnly(signPanel);
        if (user != null) user.text = email;
        ShowMessage("Account verified. Please sign in.");
    }


    // =========================================================
    // OPEN CREATE ACCOUNT PANEL
    // =========================================================

    public void OnCreateAnAccountPanel()
    {
        if (accountCreatePanel == null)
            return;

        HideApiPanels();


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

        HideApiPanels();


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
        if (isBusy) return;

        if (forgotPassword == null ||
            string.IsNullOrWhiteSpace(forgotPassword.text))
        {
            ShowMessage("Please enter your email.");
            PlayWrongSound();
            return;
        }

        if (!IsValidEmail(forgotPassword.text.Trim()))
        {
            ShowMessage("Please enter a valid email address.");
            PlayWrongSound();
            return;
        }

        StartCoroutine(ForgotPasswordFlow(forgotPassword.text.Trim()));
    }

    private IEnumerator ForgotPasswordFlow(string email)
    {
        isBusy = true;
        ShowMessage("Sending code...");

        var handle = AuthManager.Instance.ForgotPassword(email);
        while (!handle.isDone) yield return null;

        isBusy = false;

        if (!handle.success)
        {
            ShowMessage(handle.message);
            PlayWrongSound();
            yield break;
        }

        pendingEmail = email;
        ShowPanelOnly(resetPanel);
        if (resetOtpField != null) resetOtpField.text = "";
        if (resetNewPassword != null) resetNewPassword.text = "";
        if (resetConfirmPassword != null) resetConfirmPassword.text = "";
        ShowMessage("If an account exists for " + email + ", we sent a code to it.");
    }


    // =========================================================
    // RESET PASSWORD (OTP + new password)
    // =========================================================

    public void OnResetPasswordClick()
    {
        if (isBusy) return;

        if (resetOtpField == null || resetNewPassword == null || resetConfirmPassword == null) return;

        if (string.IsNullOrWhiteSpace(resetOtpField.text) ||
            string.IsNullOrEmpty(resetNewPassword.text) ||
            string.IsNullOrEmpty(resetConfirmPassword.text))
        {
            ShowMessage("Please fill all fields.");
            PlayWrongSound();
            return;
        }

        if (resetNewPassword.text != resetConfirmPassword.text)
        {
            ShowMessage("Passwords do not match.");
            PlayWrongSound();
            return;
        }

        StartCoroutine(ResetPasswordFlow(resetOtpField.text.Trim(), resetNewPassword.text));
    }

    private IEnumerator ResetPasswordFlow(string otp, string newPass)
    {
        isBusy = true;
        ShowMessage("Updating password...");

        var handle = AuthManager.Instance.ResetPassword(pendingEmail, otp, newPass);
        while (!handle.isDone) yield return null;

        isBusy = false;

        if (!handle.success)
        {
            ShowMessage(handle.message);
            PlayWrongSound();
            yield break;
        }

        ShowPanelOnly(signPanel);
        if (user != null) user.text = pendingEmail;
        if (password != null) password.text = "";
        ShowMessage("Password updated. Please sign in.");
    }


    // =========================================================
    // SIGN IN
    // =========================================================

    public void OnSignInClick()
    {
        HideApiPanels();

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
        if (isBusy) return;

        // OTP / reset panels go back to Sign In
        if ((otpPanel != null && otpPanel.activeSelf) ||
            (resetPanel != null && resetPanel.activeSelf))
        {
            ShowPanelOnly(signPanel);
            ShowMessage("");
            return;
        }

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

    private void HideApiPanels()
    {
        if (otpPanel != null) otpPanel.SetActive(false);
        if (resetPanel != null) resetPanel.SetActive(false);
    }

    /// Shows exactly one of the five panels (sign in / create / forgot / otp / reset) and pops it in.
    private void ShowPanelOnly(GameObject panel)
    {
        if (signPanel != null) signPanel.SetActive(false);
        if (accountCreatePanel != null) accountCreatePanel.SetActive(false);
        if (ForgotPanel != null) ForgotPanel.SetActive(false);
        HideApiPanels();

        if (panel == null) return;
        panel.SetActive(true);
        AnimateSignPanel(panel);
    }

    private static bool IsValidEmail(string email)
    {
        int at = email.IndexOf('@');
        return at > 0 && email.IndexOf('.', at) > at + 1 && !email.EndsWith(".") && !email.Contains(" ");
    }

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