using UnityEngine;
using UnityEngine.UI;

/// Put on the ProfileButton. Clicking it shows CityStatsPanel; clicking again hides it.
[RequireComponent(typeof(Button))]
public class CityStatsPanelToggle : MonoBehaviour
{
    [SerializeField] private GameObject cityStatsPanel;
    [SerializeField] private bool startHidden = true;

    void Awake()
    {
        GetComponent<Button>().onClick.AddListener(Toggle);
    }

    void Start()
    {
        // Hide in Start (not Awake) so CityStatsPanel.Awake has already registered its Instance.
        if (startHidden && cityStatsPanel != null)
            cityStatsPanel.SetActive(false);
    }

    public void Toggle()
    {
        if (cityStatsPanel == null) return;

        bool show = !cityStatsPanel.activeSelf;
        cityStatsPanel.SetActive(show);

        if (show)
            CityStatsPanel.Instance?.RefreshDisplay();
    }
}
