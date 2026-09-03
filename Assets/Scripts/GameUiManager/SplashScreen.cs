using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class SplashScreen : MonoBehaviour
{
    [Header("Logo")]
    [SerializeField] private Image logoImg;

    [Header("Animation Settings")]
    [SerializeField] private float popDuration = 0.8f;
    [SerializeField] private float zoomDuration = 2.5f;
    [SerializeField] private float zoomAmount = 1.08f;

    [Header("Version Check")]
    [Tooltip("Minimum time the splash stays up regardless of how fast the version check responds - keeps your branding/animation from flashing by instantly. The version check runs in PARALLEL with this, not after it.")]
    [SerializeField] private float minimumSplashTime = 3f;
    [Tooltip("Shown OVER the splash screen if an update is needed - splash stays active underneath until the player chooses Later.")]
    [SerializeField] private UpdateRequiredPanel updateRequiredPanel;

    private RectTransform logoRect;
    private Vector3 originalScale;

    private void Awake()
    {
        logoRect = logoImg.rectTransform;
        originalScale = logoRect.localScale;
    }

    private void Start()
    {
        // Started here, not Awake() - GameUIManager.Init() activates this object for the first
        // time from inside ITS OWN Awake(), which runs a coroutine's code synchronously up to
        // its first yield. If SplashFlow() started from Awake(), VersionCheckManager.Instance
        // could still be null (Unity doesn't guarantee Awake() order between different
        // GameObjects). Start() is guaranteed to run only after every object's Awake() in the
        // scene has already completed, which sidesteps the race entirely.
        StartCoroutine(SplashFlow());
    }

    private void OnEnable()
    {
        PlayLogoAnimation();
    }

    private void PlayLogoAnimation()
    {
        LeanTween.cancel(logoRect.gameObject);

        logoRect.localScale = Vector3.zero;

        LeanTween.scale(
            logoRect,
            originalScale,
            popDuration
        )
        .setEaseOutBack()
        .setOnComplete(StartZoomAnimation);
    }

    private void StartZoomAnimation()
    {
        LeanTween.scale(
            logoRect,
            originalScale * zoomAmount,
            zoomDuration
        )
        .setEaseInOutSine()
        .setLoopPingPong();
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    /// Splash appears -> version check runs in parallel with a minimum display time -> if no
    /// update needed, proceeds straight to Main Screen (same as your original 3s-timer
    /// behavior). If an update IS needed, the popup shows OVER the splash (which stays active)
    /// and this waits for the player's choice before proceeding - Update Now opens the store
    /// but keeps them here; only Later actually moves forward.
    private IEnumerator SplashFlow()
    {
        var versionHandle = VersionCheckManager.Instance.CheckVersion();
        float elapsed = 0f;

        while (elapsed < minimumSplashTime || !versionHandle.isDone)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (versionHandle.updateRequired && updateRequiredPanel != null)
        {
            var choiceHandle = updateRequiredPanel.Show(versionHandle.storeUrl, versionHandle.message, versionHandle.forceUpdate);
            yield return new WaitUntil(() => choiceHandle.isDone);
            // Only reaches here once the player taps Later - Update Now keeps them on the popup.
        }

        GameUIManager.Instance.MainScreenToggle(true);
        GameUIManager.Instance.SplashScreenToggle(false);
    }
}