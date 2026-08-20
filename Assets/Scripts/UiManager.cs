using UnityEngine;
using UnityEngine.UI;


public class UiManager : MonoBehaviour
{
    public static UiManager Instance { get; private set; }

    [SerializeField] private GameObject ShopPanel;
    [SerializeField] private GameObject GameplayPanel;
    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }
    void ResetUI()
    {
        ShowHideShopPanel(false);
        ShowHideGameplayPanel(false);
    }
    public void ShopButton()
    {
        ResetUI();
        ShowHideShopPanel(true);
    }

    public void ShowHideShopPanel(bool toggle)
    {
        ShopPanel.SetActive(toggle);
    }

     public void ShowHideGameplayPanel(bool toggle)
    {
        GameplayPanel.SetActive(toggle);
    }
}
