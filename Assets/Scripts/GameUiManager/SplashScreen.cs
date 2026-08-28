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

    private RectTransform logoRect;
    private Vector3 originalScale;

    private void Awake()
    {
        logoRect = logoImg.rectTransform;
        originalScale = logoRect.localScale;
        StartCoroutine(SplashScreenHide());
    }

    private void OnEnable()
    {
        PlayLogoAnimation();

        // Hide splash after 3 seconds

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

    //private void HideSplashScreen()
    //{
    //    GameUIManager.Instance.SplashScreenToggle(false);
    //}

    public void Show()
    {
        gameObject.SetActive(true);
    }

    private IEnumerator SplashScreenHide()
    {
        yield return new WaitForSeconds(3f);

        GameUIManager.Instance.MainScreenToggle(true);

        GameUIManager.Instance.SplashScreenToggle(false);
    }


}