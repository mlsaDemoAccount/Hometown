
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

    [Header("Countdown")]
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private float countdownDuration = 3f;

    [Header("Logo Beat Animation")]
    [SerializeField] private float logoBeatAmount = 1.05f;
    [SerializeField] private float logoBeatDuration = 1.5f;

    [Header("Elements Pop Animation")]
    [SerializeField] private float elementPopDuration = 0.5f;
    [SerializeField] private float elementDelay = 0.12f;

    private Vector3 logoOriginalScale;
    private Vector3[] elementOriginalScales;

    private void OnEnable()
    {
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

