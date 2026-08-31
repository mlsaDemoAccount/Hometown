using Unity.Android.Gradle;
using UnityEngine;
using UnityEngine.UI;

public class InvitePlayerScreen : MonoBehaviour
{
    public Image logoImg;
    public GameObject[] menuButtons;


    [Header("Logo Beat Animation")]
    [SerializeField] private float logoBeatAmount = 1.05f;
    [SerializeField] private float logoBeatDuration = 1.5f;
    private Vector3 logoOriginalScale;
    private Vector3 panelOriginalScale;

    [Header("Menu Buttons Slide Animation")]
    [SerializeField] private float menuSlideDistance = 500f;
    [SerializeField] private float menuSlideDuration = 0.6f;
    [SerializeField] private float menuButtonDelay = 0.12f;




    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        AnimateLogo();
        AnimateMenuButtons();

    }

    // Update is called once per frame
    void Update()
    {
        
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
    public void OnInviteBtnClick()
    {
        GameUIManager.Instance.LobbyScreenToggle(true);
    }
}
