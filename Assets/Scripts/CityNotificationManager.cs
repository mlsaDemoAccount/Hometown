using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Periodically suggests the player place a building to help the city. For a LOW-is-bad
/// category (Jobs, Lifestyle) that's struggling, placing more of that SAME category is the
/// correct fix, so it's suggested directly. For a HIGH-is-bad category (Population, Pollution,
/// CrimeRate, Traffic) that's struggling, placing more of ITSELF would make it worse - instead,
/// this searches your actual BuildingData assets for whichever OTHER category has a building
/// with a genuinely negative effect on the troubled stat (e.g. a Lifestyle building with
/// populationOutput = -20), and suggests that category instead. This is data-driven from
/// whatever values you've actually set, not a hardcoded assumption.
///
/// Once the whole city is healthy, follows a growth chain (Population -> Lifestyle -> Jobs ->
/// Population -> ...) instead of picking randomly.
///
/// Accept: opens the shop directly to the suggested category's tab, then waits for the SHARED
/// notification interval again (see ToastNotificationCoordinator). Dismiss: just closes the
/// popup, nothing else changes.
///
/// Toast timing/display is coordinated through ToastNotificationCoordinator, shared with
/// CityEventManager's event toasts - this is what guarantees only one toast is ever visible at
/// once, and both systems follow the same overall pacing instead of two independent timers.
public class CityNotificationManager : MonoBehaviour
{
    public static CityNotificationManager Instance { get; private set; }

    [Tooltip("Shown for Critical-status notifications - a more alarming design. Direct reference, not looked up via Instance (see notes on the other panel field for why).")]
    public CityNotificationPanel criticalPanel;
    [Tooltip("Shown for Warning/Healthy-status notifications - a calmer design. Same script as criticalPanel, just a different UI prefab - both share identical fields (message, buttons, timer text/slider).")]
    public CityNotificationPanel normalPanel;

    [Tooltip("Persistent list where every currently-troubled category gets an entry with NO countdown - it stays until that stat actually leaves Warning/Critical, independent of the toast's own timing.")]
    public NotificationCenter notificationCenter;

    /// Which category to suggest next once the whole city is healthy - keyed by whichever
    /// growth category was suggested most recently.
    private static readonly Dictionary<BuildingCategory, BuildingCategory> GrowthChain = new Dictionary<BuildingCategory, BuildingCategory>
    {
        { BuildingCategory.Population, BuildingCategory.Lifestyle }, // more lifestyle raises capacity for more population
        { BuildingCategory.Lifestyle,  BuildingCategory.Jobs },       // a livable city needs jobs to match
        { BuildingCategory.Jobs,       BuildingCategory.Population }, // more jobs support more residents - closes the loop
    };

    private BuildingCategory? lastGrowthSuggestion = null; // tracks progress along the growth chain

    /// Every category currently showing a persistent Notification Center entry - lets us tell
    /// apart "already listed, just refresh its text" from "newly troubled, create an entry" and
    /// "no longer troubled, remove its entry".
    private Dictionary<BuildingCategory, NotificationCenterEntry> activeStatEntries = new Dictionary<BuildingCategory, NotificationCenterEntry>();

    /// The last suggestion actually built (by the toast's own timer, via BuildSuggestion()) -
    /// used to keep a matching Notification Center entry for the healthy-city growth-chain
    /// case WITHOUT calling BuildSuggestion() again, which would incorrectly advance the chain
    /// every frame instead of only when the toast fires.
    private Suggestion? currentSuggestion = null;
    private NotificationCenterEntry growthChainEntry = null;

    private struct Suggestion
    {
        public BuildingCategory troubledCategory;   // whose status triggered this (for the message)
        public BuildingCategory suggestedCategory;   // which shop tab to actually recommend
        public CityMetricStatus status;
    }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private bool warnedMissingCoordinator = false; // log this once, not every frame

    void Update()
    {
        RefreshStatEntries();       // independent of the toast timing below - runs continuously
        RefreshGrowthChainEntry();  // same - just reflects the last-built suggestion, never advances it

        // Uses the SHARED ToastNotificationCoordinator instead of its own independent timer -
        // this is what makes stat toasts and event toasts (CityEventManager) follow the SAME
        // interval and never show simultaneously. TryBeginToast() only returns true when
        // nothing else is currently showing AND the shared interval has elapsed.
        if (ToastNotificationCoordinator.Instance == null)
        {
            if (!warnedMissingCoordinator)
            {
                Debug.LogWarning("[CityNotificationManager] ToastNotificationCoordinator not found in scene - stat toasts will never show (only Notification Center list entries will appear). Add a ToastNotificationCoordinator component to your scene.");
                warnedMissingCoordinator = true;
            }
            return;
        }

        if (ToastNotificationCoordinator.Instance.TryBeginToast())
            StartCoroutine(ShowNextNotificationRoutine());
    }

    private bool warnedMissingNotificationCenter = false; // log this once, not every frame

    /// Keeps the Notification Center's stat entries matching reality: creates one the moment a
    /// category becomes Warning/Critical, keeps its text current if the severity changes, and
    /// removes it the moment that category returns to Healthy - entirely independent of the
    /// toast popup's own interval/timing.
    private void RefreshStatEntries()
    {
        if (CityStatsManager.Instance == null) return;
        if (notificationCenter == null)
        {
            if (!warnedMissingNotificationCenter)
            {
                Debug.LogWarning("[CityNotificationManager] Notification Center is not assigned in the Inspector - stat warnings will only show as a 5s toast, never as a persistent list entry.");
                warnedMissingNotificationCenter = true;
            }
            return;
        }

        foreach (BuildingCategory category in System.Enum.GetValues(typeof(BuildingCategory)))
        {
            var status = CityStatsManager.Instance.GetStatus(category);
            bool isTroubled = status == CityMetricStatus.Critical || status == CityMetricStatus.Warning;

            if (isTroubled)
            {
                BuildingCategory suggestedCategory = ResolveSuggestionFor(category); // same counteracting-category logic the toast uses
                var entrySuggestion = new Suggestion { troubledCategory = category, suggestedCategory = suggestedCategory, status = status };
                string message = BuildMessage(entrySuggestion); // reuse the SAME detailed wording the toast uses, not a separate bare-bones string

                if (activeStatEntries.TryGetValue(category, out var existingEntry))
                {
                    existingEntry.UpdateMessage(message); // keep wording current if Warning -> Critical
                }
                else
                {
                    var entry = notificationCenter.AddGenericEntry(message, suggestedCategory);
                    if (entry != null) activeStatEntries[category] = entry;
                }
            }
            else if (activeStatEntries.TryGetValue(category, out var entry))
            {
                notificationCenter.RemoveGenericEntry(entry);
                activeStatEntries.Remove(category);
            }
        }
    }

    /// Keeps a single persistent entry reflecting the CURRENT growth-chain suggestion (e.g.
    /// "place something from Lifestyle next") whenever the whole city is healthy - created the
    /// first time a suggestion has been built, text updated as the chain advances, removed the
    /// moment the city is no longer fully healthy (the per-category Warning/Critical entries
    /// take over from RefreshStatEntries at that point instead).
    private void RefreshGrowthChainEntry()
    {
        if (CityStatsManager.Instance == null || notificationCenter == null) return;

        bool cityFullyHealthy = true;
        foreach (BuildingCategory category in System.Enum.GetValues(typeof(BuildingCategory)))
        {
            if (CityStatsManager.Instance.GetStatus(category) != CityMetricStatus.Healthy) { cityFullyHealthy = false; break; }
        }

        bool shouldShow = cityFullyHealthy && currentSuggestion.HasValue && currentSuggestion.Value.status == CityMetricStatus.Healthy;

        if (shouldShow)
        {
            string message = BuildMessage(currentSuggestion.Value);
            if (growthChainEntry == null)
                growthChainEntry = notificationCenter.AddGenericEntry(message, currentSuggestion.Value.suggestedCategory);
            else
                growthChainEntry.UpdateMessage(message);
        }
        else if (growthChainEntry != null)
        {
            notificationCenter.RemoveGenericEntry(growthChainEntry);
            growthChainEntry = null;
        }
    }

    private IEnumerator ShowNextNotificationRoutine()
    {
        if (CityStatsManager.Instance == null)
        {
            Debug.LogWarning("[CityNotificationManager] CityStatsManager not found - skipping this notification attempt.");
            ToastNotificationCoordinator.Instance?.EndToast();
            yield break;
        }

        Suggestion suggestion = BuildSuggestion();
        currentSuggestion = suggestion; // remember for RefreshGrowthChainEntry - built ONLY here, per interval
        CityNotificationPanel targetPanel = suggestion.status == CityMetricStatus.Critical ? criticalPanel : normalPanel;

        if (targetPanel == null)
        {
            string which = suggestion.status == CityMetricStatus.Critical ? "criticalPanel" : "normalPanel";
            Debug.LogWarning($"[CityNotificationManager] {which} is not assigned in the Inspector - skipping this notification attempt.");
            ToastNotificationCoordinator.Instance?.EndToast();
            yield break;
        }

        var handle = targetPanel.Show(BuildMessage(suggestion));
        yield return new WaitUntil(() => handle.isDone);

        ToastNotificationCoordinator.Instance?.EndToast();

        if (handle.accepted)
        {
            if (UiManager.Instance != null)
            {
                UiManager.Instance.ShowHideGameplayPanel(false);
                UiManager.Instance.ShowHideShopPanel(true);
            }
            ShopUIController.Instance?.SelectCategoryExternally(suggestion.suggestedCategory);
        }
        // Dismissed or auto-timeout: nothing else changes.
    }

    private Suggestion BuildSuggestion()
    {
        BuildingCategory? critical = null;
        BuildingCategory? warning = null;

        foreach (BuildingCategory category in System.Enum.GetValues(typeof(BuildingCategory)))
        {
            var status = CityStatsManager.Instance.GetStatus(category);
            if (status == CityMetricStatus.Critical && critical == null) critical = category;
            else if (status == CityMetricStatus.Warning && warning == null) warning = category;
        }

        BuildingCategory? troubled = critical ?? warning;
        if (troubled.HasValue)
        {
            var status = critical.HasValue ? CityMetricStatus.Critical : CityMetricStatus.Warning;
            BuildingCategory suggested = ResolveSuggestionFor(troubled.Value);
            return new Suggestion { troubledCategory = troubled.Value, suggestedCategory = suggested, status = status };
        }

        // Everything healthy - follow the growth chain instead of picking fully at random.
        BuildingCategory next = (lastGrowthSuggestion.HasValue && GrowthChain.TryGetValue(lastGrowthSuggestion.Value, out var linked))
            ? linked
            : BuildingCategory.Population; // sensible default starting point for the chain
        lastGrowthSuggestion = next;

        return new Suggestion { troubledCategory = next, suggestedCategory = next, status = CityMetricStatus.Healthy };
    }

    /// LOW-is-bad categories are correctly fixed by placing more of themselves. HIGH-is-bad
    /// categories need a counteracting category found from real building data instead.
    private BuildingCategory ResolveSuggestionFor(BuildingCategory troubled)
    {
        var config = CityStatsManager.Instance.metricConfigs.Find(c => c.category == troubled);
        bool highIsBad = config != null && config.highIsBad;

        if (!highIsBad) return troubled; // LOW-is-bad - placing more of itself is the correct fix

        return FindBestCounteractingCategory(troubled) ?? troubled; // fall back to itself if nothing counteracts it
    }

    /// Scans every known BuildingData for one whose level-1 output on the troubled category is
    /// negative, and returns whichever building's category has the STRONGEST negative effect.
    private BuildingCategory? FindBestCounteractingCategory(BuildingCategory troubled)
    {
        if (BuildingDatabase.Instance == null || BuildingDatabase.Instance.allBuildings == null) return null;

        BuildingCategory? best = null;
        float bestValue = 0f; // looking for the most negative value found

        foreach (var building in BuildingDatabase.Instance.allBuildings)
        {
            if (building == null || building.primaryCategory == troubled) continue; // don't suggest the troubled category itself
            if (building.levels == null || building.levels.Length == 0) continue;

            float output = GetOutputForCategory(building.levels[0], troubled);
            if (output < bestValue)
            {
                bestValue = output;
                best = building.primaryCategory;
            }
        }

        return best;
    }

    private float GetOutputForCategory(BuildingUpgradeLevel level, BuildingCategory category) => category switch
    {
        BuildingCategory.Population => level.populationOutput,
        BuildingCategory.Jobs => level.jobsOutput,
        BuildingCategory.Pollution => level.pollutionOutput,
        BuildingCategory.CrimeRate => level.crimeRateOutput,
        BuildingCategory.Traffic => level.trafficOutput,
        BuildingCategory.Lifestyle => level.lifestyleOutput,
        _ => 0f
    };

    private string BuildMessage(Suggestion s)
    {
        if (s.status == CityMetricStatus.Healthy)
            return $"{s.suggestedCategory} could use some development. Place an item from the {s.suggestedCategory} tab in the shop.";

        string urgency = s.status == CityMetricStatus.Critical ? "is in a critical state" : "needs attention";

        if (s.troubledCategory == s.suggestedCategory)
            return $"{s.troubledCategory} {urgency}. Place an item from the {s.troubledCategory} tab in the shop.";

        return $"{s.troubledCategory} {urgency}. Try placing something from the {s.suggestedCategory} tab to help bring it down.";
    }
}