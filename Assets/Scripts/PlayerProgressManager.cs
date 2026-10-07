using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// Tracks and displays the player/city's HUD progress stats: XP, level, and funds - all
/// consolidated in one place since they're all just "numbers shown on the gameplay screen"
/// (formerly split across this class and a separate GameplayFundsDisplay script). Funds
/// themselves are still owned by EconomyManager; this just displays them via polling in
/// Update() - no event/delegate subscription anywhere in this project.
///
/// XP is per-city (see CitySaveManager.currentCityId), not a global player-wide stat. XP
/// amounts are NOT set here - each building's placement XP lives on its own BuildingData asset
/// (placementXP), and each upgrade level's XP lives on its own BuildingUpgradeLevel
/// (upgradeXP), since different buildings/levels should reward different amounts. This class
/// only tracks the running total and derives the city level from it - call AddXP(amount) with
/// whatever amount the specific action earned.
public class PlayerProgressManager : MonoBehaviour
{
    public static PlayerProgressManager Instance { get; private set; }

    [Tooltip("Direct reference to the level-up popup - NOT a static Instance lookup, since that panel starting inactive in the Hierarchy would mean its Awake()/Start() hasn't run yet the first time it's needed (same class of bug fixed earlier on CityNotificationManager). A direct reference works regardless of that timing.")]
    public LevelUpPopup levelUpPopup;

    [Header("Level Thresholds")]
    [Tooltip("XP required to REACH each level - index 0 = level 1 (always 0 XP), index 1 = level 2, etc. Extend this list to add more levels.")]
    public List<int> xpThresholds = new List<int> { 0, 100, 250, 500, 1000, 2000 };

    [Header("Display")]
    public TMP_Text xpText;
    public TMP_Text levelText;   // plain number ("3"), not "Lv. 3"
    public TMP_Text fundsText;   // plain number ("100"), not "100 WP" - shop's own label can format differently
    [Tooltip("Standard Unity Slider - Min/Max Value get set to the current level's XP range each update, and Value tracks currentXP within that range. Shows visual progress toward the next level.")]
    public Slider xpProgressSlider;

    public int currentXP { get; private set; } = 0;
    public int currentLevel { get; private set; } = 1;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        UpdateDisplay();
    }

    void Update()
    {
        // Funds are polled here (not event-driven) - EconomyManager doesn't fire any
        // notification, so this just reads currentFunds directly each frame.
        UpdateFundsDisplay();
    }

    /// Call this whenever the player does something that should earn XP. Shows the level-up
    /// popup if this XP actually pushed the city into a new level - works identically whether
    /// this fires from a live action (placing/upgrading right now) or from an offline-completed
    /// building catching up the moment the game reloads, since both paths call AddXP() the
    /// exact same way.
    public void AddXP(int amount)
    {
        currentXP += amount;
        int previousLevel = currentLevel;
        RecalculateLevel();
        UpdateDisplay();
        CitySaveManager.Instance?.SaveCity();
        AuthManager.Instance?.QueueProfileUpdate("playerXp", currentXP); // only XP is sent

        if (currentLevel > previousLevel)
            levelUpPopup?.Show(currentLevel);
    }

    /// Used by CitySaveManager when loading a city - restores XP directly without re-triggering
    /// a save or the level-up popup (this is re-establishing EXISTING state on load, not a new
    /// level-up event).
    public void RestoreXP(int savedXP)
    {
        currentXP = savedXP;
        RecalculateLevel();
        UpdateDisplay();
    }

    private void RecalculateLevel()
    {
        int newLevel = 1;
        for (int i = 0; i < xpThresholds.Count; i++)
        {
            if (currentXP >= xpThresholds[i]) newLevel = i + 1;
            else break;
        }
        currentLevel = newLevel;
    }

    private void UpdateDisplay()
    {
        if (xpText != null) xpText.text = currentXP.ToString();
        if (levelText != null) levelText.text = currentLevel.ToString();
        UpdateXPSlider();
    }

    private void UpdateFundsDisplay()
    {
        if (fundsText != null && EconomyManager.Instance != null)
            fundsText.text = EconomyManager.Instance.currentFunds.ToString();
    }

    /// Sets the slider's range to [XP where the current level started, XP where the next level
    /// starts], and its value to currentXP within that range - so the bar visually fills up as
    /// you approach the next level, resetting to empty right as you cross into it.
    private void UpdateXPSlider()
    {
        if (xpProgressSlider == null) return;

        int currentLevelFloor = xpThresholds[currentLevel - 1]; // XP where the current level began
        bool hasNextLevel = currentLevel < xpThresholds.Count;

        if (hasNextLevel)
        {
            int nextLevelFloor = xpThresholds[currentLevel]; // XP required to reach the next level
            xpProgressSlider.minValue = currentLevelFloor;
            xpProgressSlider.maxValue = nextLevelFloor;
            xpProgressSlider.value = currentXP;
        }
        else
        {
            // Already at the highest configured level - nothing further to progress toward,
            // show the bar as full rather than leaving it in a stale/undefined state.
            xpProgressSlider.minValue = 0;
            xpProgressSlider.maxValue = 1;
            xpProgressSlider.value = 1;
        }
    }
}