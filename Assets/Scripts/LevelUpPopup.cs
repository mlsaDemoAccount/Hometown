using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// "Congratulations, Level X!" popup - shown by PlayerProgressManager whenever earned XP pushes the city
/// into a new level. Fires identically whether that happens live (placing/upgrading right now)
/// or from a building that finished offline while the app was closed and is catching up the
/// moment the game reloads - both paths go through PlayerProgressManager.AddXP() the same way.
public class LevelUpPopup : MonoBehaviour
{
    public GameObject panelRoot; // can be this.gameObject
    public TMP_Text levelText;
    public Button continueButton;

    void Start()
    {
        if (panelRoot == null) panelRoot = gameObject;
        continueButton.onClick.AddListener(OnContinueClicked);

        // IMPORTANT: do NOT call Hide() here - if panelRoot starts inactive in the scene (as it
        // should), Start() only runs the moment it's FIRST activated, and calling SetActive(false)
        // from inside that same Start() would undo the very SetActive(true) that triggered it,
        // silently cancelling the first-ever Show(). Just make sure panelRoot is unchecked
        // (inactive) in the Hierarchy by default.
    }

    public void Show(int newLevel)
    {
        if (levelText != null) levelText.text = newLevel.ToString();
        panelRoot.SetActive(true);
    }

    public void Hide()
    {
        panelRoot.SetActive(false);
    }

    private void OnContinueClicked()
    {
        Hide();
    }
}