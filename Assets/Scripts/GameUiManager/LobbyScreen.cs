
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;

public class LobbyScreen : MonoBehaviour
{
    [Header("UI References")]
    public Image logo;
    public Image[] elements;

    [Header("Players (right = you, left = your friend)")]
    [SerializeField] private TMP_Text myNameText;
    [SerializeField] private TMP_Text friendNameText;
    [Tooltip("The big character on the right. Its sprite follows the gender chosen on the Profile screen.")]
    [SerializeField] private Image myCharacter;
    [SerializeField] private Image friendCharacter;

    [Header("Countdown")]
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private int gameSceneName = 1;
    [SerializeField] private float countdownDuration = 3f;

    [Header("Logo Beat Animation")]
    [SerializeField] private float logoBeatAmount = 1.05f;
    [SerializeField] private float logoBeatDuration = 1.5f;

    [Header("Elements Pop Animation")]
    [SerializeField] private float elementPopDuration = 0.5f;
    [SerializeField] private float elementDelay = 0.12f;

    private Vector3 logoOriginalScale;
    private Vector3[] elementOriginalScales;

    private float nextPlayersRefresh;

    private void OnEnable()
    {
        if (CityLauncher.IsCityMode) CityLauncher.Instance.RefreshPartner();
        RefreshPlayers();
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayLobbyMusic();
        }
    }
    private void OnDisable()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayMainMusic();
        }
    }

    // The friend's profile can arrive a moment after the lobby opens: keep the names and characters current.
    private void Update()
    {
        if (Time.unscaledTime < nextPlayersRefresh) return;
        nextPlayersRefresh = Time.unscaledTime + 0.5f;
        RefreshPlayers();
    }

    private void RefreshPlayers()
    {
        var partner = PlayerIdentity.Partner;
        if (myNameText != null) myNameText.text = PlayerIdentity.MyName;
        if (friendNameText != null) friendNameText.text = partner != null ? partner.firstName : "Waiting...";
        ShowCharacter(myCharacter, PlayerIdentity.MyGender);
        ShowCharacter(friendCharacter, partner != null ? partner.gender : -1);
    }

    // Unknown gender: leave whatever picture the scene already shows.
    private static void ShowCharacter(Image slot, int gender)
    {
        if (slot == null) return;
        var sprite = AvatarLibrary.Character(gender);
        if (sprite == null) return;
        slot.sprite = sprite;
        slot.preserveAspect = true;
    }

    private void Start()
    {
        AnimateLogo();
        AnimateElements();

        StartCoroutine(CountdownAndLoadGame());
    }

    // =========================================================
    // LOGO CONTINUOUS BEAT ANIMATION
    // =========================================================

    private void AnimateLogo()
    {
        if (logo == null)
            return;

        RectTransform logoRect = logo.GetComponent<RectTransform>();

        logoOriginalScale = logoRect.localScale;

        LeanTween.scale(
            logoRect.gameObject,
            logoOriginalScale * logoBeatAmount,
            logoBeatDuration
        )
        .setEase(LeanTweenType.easeInOutSine)
        .setLoopPingPong();
    }

    // =========================================================
    // ELEMENTS POP UP ONE BY ONE
    // =========================================================

    private void AnimateElements()
    {
        if (elements == null)
            return;

        elementOriginalScales = new Vector3[elements.Length];

        for (int i = 0; i < elements.Length; i++)
        {
            if (elements[i] == null)
                continue;

            RectTransform element =
                elements[i].GetComponent<RectTransform>();

            // Store original scale
            elementOriginalScales[i] = element.localScale;

            // Start small
            element.localScale = Vector3.zero;

            // Pop into place
            LeanTween.scale(
                element.gameObject,
                elementOriginalScales[i],
                elementPopDuration
            )
            .setDelay(i * elementDelay)
            .setEase(LeanTweenType.easeOutBack);
        }
    }

    // =========================================================
    // COUNTDOWN THEN LOAD GAME SCENE
    // =========================================================

    private IEnumerator CountdownAndLoadGame()
    {
        if (countdownText == null)
        {
            Debug.LogWarning("Countdown Text is not assigned!");

            yield return new WaitForSeconds(countdownDuration);

            SceneManager.LoadScene(gameSceneName);
            yield break;
        }

        // 3 seconds
        countdownText.text = "Game start in 3 sec";
        yield return new WaitForSeconds(1f);

        // 2 seconds
        countdownText.text = "Game start in 2 sec";
        yield return new WaitForSeconds(1f);

        // 1 second
        countdownText.text = "Game start in 1 sec";
        yield return new WaitForSeconds(1f);

        // Clear text
        countdownText.text = "";

        // Load Game Scene
        SceneManager.LoadScene(gameSceneName);
    }
}

