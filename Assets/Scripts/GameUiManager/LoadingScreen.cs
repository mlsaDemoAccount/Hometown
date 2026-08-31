using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadingScreen : MonoBehaviour
{
    public static LoadingScreen Instance;
    public Image LoadingIcon;

    [Header("Pop Animation")]
    [SerializeField] private float popScale = 1.1f;
    [SerializeField] private float popDuration = 0.25f;

    [Header("Loading Screen")]
    [SerializeField] private float displayDuration = 3f;

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        // Reset scale
        LoadingIcon.transform.localScale = Vector3.zero;

        // Pop up
        LeanTween.scale(
            LoadingIcon.gameObject,
            Vector3.one * popScale,
            popDuration
        ).setEaseOutBack();

        // Return to normal size
        LeanTween.scale(
            LoadingIcon.gameObject,
            Vector3.one,
            popDuration
        ).setDelay(popDuration)
         .setEaseInOutSine();

        // Automatically disable after 3 seconds
        Invoke(nameof(HideLoadingScreen), displayDuration);
    }

    private void HideLoadingScreen()
    {
        GameUIManager.Instance.gameObject.SetActive(true);
        gameObject.SetActive(false);
    }
    
}