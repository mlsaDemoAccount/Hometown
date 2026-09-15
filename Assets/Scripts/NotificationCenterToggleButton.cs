using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// Button on the gameplay HUD that opens/closes the Notification Center. Shows a live count of
/// currently pending notifications/tasks (city events + stat warnings + growth-chain
/// suggestion combined) as a badge - polled each frame, no event/delegate involved.
public class NotificationCenterToggleButton : MonoBehaviour
{
    public NotificationCenter notificationCenter;
    public Button button;
    public TMP_Text countText;

    void Start()
    {
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClicked);
        }
    }

    void Update()
    {
        if (countText != null && notificationCenter != null)
            countText.text = notificationCenter.TotalEntryCount.ToString();
    }

    private void OnClicked()
    {
        notificationCenter?.Toggle();
    }
}
