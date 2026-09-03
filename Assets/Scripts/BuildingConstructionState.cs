using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum ConstructionPhase { Complete, UnderConstruction, Upgrading }

/// Optional component - add to a building prefab (usually as a child object, or alongside the
/// main visual) to give it a timed construction/upgrade sequence.
///
/// FRESH PLACEMENT (UnderConstruction) uses two stages:
/// - 0% to 50% progress: constructionStageInitial active (e.g. workers only, no structure yet)
/// - 50%+ progress: constructionStageHalf active (e.g. half-built structure + workers)
///
/// UPGRADE (Upgrading) uses ONE separate, single visual instead - upgradeStageVisual - shown
/// for the whole upgrade duration, completely independent of the two construction stages above.
///
/// 100% (either case): real building visual shown, all stage objects hidden.
///
/// If this component isn't present on a building, BuildingPlacementController skips the whole
/// construction sequence and places it instantly - so existing buildings keep working exactly
/// as before until you specifically set this up for them.
public class BuildingConstructionState : MonoBehaviour
{
    [Header("Construction Stage Objects (fresh placement only)")]
    [Tooltip("The building's real, completed visual - hidden during both fresh construction AND upgrade, shown again on completion.")]
    public GameObject mainVisualRoot;
    [Tooltip("Shown from 0% to 50% progress during a FRESH placement - e.g. workers only, no structure yet.")]
    public GameObject constructionStageInitial;
    [Tooltip("Shown from 50% to 100% progress during a FRESH placement - e.g. half-built structure with workers.")]
    public GameObject constructionStageHalf;

    [Header("Upgrade Stage Object (upgrade only - separate from the two above)")]
    [Tooltip("Shown for the ENTIRE duration of an upgrade - completely separate from constructionStageInitial/Half, which are only used for a fresh placement. Leave unassigned if you don't want a distinct upgrade visual.")]
    public GameObject upgradeStageVisual;

    [Header("Timer Display (optional - both live on a World Space Canvas)")]
    [Tooltip("Floating text above the building showing remaining time - leave unassigned if you don't want one.")]
    public TMP_Text timerText;
    [Tooltip("Circular/watch-style progress image - set its Image Type to Filled, Fill Method to Radial 360 in the Inspector. This script drives fillAmount automatically.")]
    public Image radialTimerImage;
    [Tooltip("If true, the radial image empties out as time passes (starts full, drains to 0) - like a countdown pie. If false, it fills UP as construction progresses.")]
    public bool radialDrainsAsTimeElapses = true;

    public ConstructionPhase phase { get; private set; } = ConstructionPhase.Complete;
    public float remainingTime { get; private set; }
    public float totalDuration { get; private set; }
    public int PendingUpgradeLevelIndex => pendingUpgradeLevelIndex;

    private int pendingUpgradeLevelIndex;

    void Update()
    {
        if (phase == ConstructionPhase.Complete) return;

        remainingTime -= Time.deltaTime;
        if (remainingTime <= 0f)
        {
            CompleteCurrentPhase();
            return;
        }

        if (phase == ConstructionPhase.UnderConstruction)
            UpdateConstructionStage(); // only the fresh-placement path switches between two stages

        UpdateTimerDisplay();
    }

    /// Starts a fresh construction sequence (new placement). Hides the real building until done.
    public void BeginConstruction(float duration)
    {
        phase = ConstructionPhase.UnderConstruction;
        totalDuration = Mathf.Max(duration, 0.01f);
        remainingTime = totalDuration;

        if (mainVisualRoot != null) mainVisualRoot.SetActive(false);
        if (upgradeStageVisual != null) upgradeStageVisual.SetActive(false); // make sure the upgrade visual isn't left on from a previous cycle
        SetTimerVisualsActive(true);
        UpdateConstructionStage();
    }

    /// Starts an upgrade sequence. Hides the real building and shows ONLY upgradeStageVisual -
    /// never touches constructionStageInitial/Half.
    public void BeginUpgrade(float duration, int newLevelIndex)
    {
        phase = ConstructionPhase.Upgrading;
        totalDuration = Mathf.Max(duration, 0.01f);
        remainingTime = totalDuration;
        pendingUpgradeLevelIndex = newLevelIndex;

        if (mainVisualRoot != null) mainVisualRoot.SetActive(false);
        if (constructionStageInitial != null) constructionStageInitial.SetActive(false);
        if (constructionStageHalf != null) constructionStageHalf.SetActive(false);
        if (upgradeStageVisual != null) upgradeStageVisual.SetActive(true);
        SetTimerVisualsActive(true);
    }

    /// Restores an in-progress construction/upgrade exactly as saved - used when loading a city
    /// so a building resumes from where it was, instead of resetting.
    public void RestoreState(ConstructionPhase savedPhase, float savedRemaining, float savedTotal, int savedPendingLevelIndex)
    {
        phase = savedPhase;
        totalDuration = Mathf.Max(savedTotal, 0.01f);
        remainingTime = Mathf.Max(savedRemaining, 0f);
        pendingUpgradeLevelIndex = savedPendingLevelIndex;

        if (phase == ConstructionPhase.Complete)
        {
            if (mainVisualRoot != null) mainVisualRoot.SetActive(true);
            if (constructionStageInitial != null) constructionStageInitial.SetActive(false);
            if (constructionStageHalf != null) constructionStageHalf.SetActive(false);
            if (upgradeStageVisual != null) upgradeStageVisual.SetActive(false);
            SetTimerVisualsActive(false);
            return;
        }

        if (phase == ConstructionPhase.Upgrading)
        {
            if (mainVisualRoot != null) mainVisualRoot.SetActive(false);
            if (constructionStageInitial != null) constructionStageInitial.SetActive(false);
            if (constructionStageHalf != null) constructionStageHalf.SetActive(false);
            if (upgradeStageVisual != null) upgradeStageVisual.SetActive(true);
        }
        else // UnderConstruction
        {
            if (mainVisualRoot != null) mainVisualRoot.SetActive(false);
            if (upgradeStageVisual != null) upgradeStageVisual.SetActive(false);
            UpdateConstructionStage(); // sets constructionStageInitial/Half correctly based on progress
        }

        SetTimerVisualsActive(true);
        UpdateTimerDisplay();
    }

    /// ONLY used during a fresh placement (UnderConstruction) - switches between the two
    /// construction stages based on progress. Never called during an upgrade.
    private void UpdateConstructionStage()
    {
        float progress01 = 1f - (remainingTime / totalDuration);
        bool halfDone = progress01 >= 0.5f;

        if (constructionStageInitial != null) constructionStageInitial.SetActive(!halfDone);
        if (constructionStageHalf != null) constructionStageHalf.SetActive(halfDone);
    }

    private void UpdateTimerDisplay()
    {
        if (timerText != null)
        {
            int seconds = Mathf.CeilToInt(Mathf.Max(remainingTime, 0f));
            timerText.text = $"{seconds}s";
        }

        if (radialTimerImage != null)
        {
            float progress01 = 1f - (remainingTime / totalDuration); // 0 = just started, 1 = complete
            radialTimerImage.fillAmount = radialDrainsAsTimeElapses ? (1f - progress01) : progress01;
        }
    }

    private void SetTimerVisualsActive(bool active)
    {
        if (timerText != null) timerText.gameObject.SetActive(active);
        if (radialTimerImage != null)
        {
            radialTimerImage.gameObject.SetActive(active);
            if (active) radialTimerImage.fillAmount = radialDrainsAsTimeElapses ? 1f : 0f; // start full/empty correctly
        }
    }

    private void CompleteCurrentPhase()
    {
        bool wasUpgrading = phase == ConstructionPhase.Upgrading;
        phase = ConstructionPhase.Complete;

        if (constructionStageInitial != null) constructionStageInitial.SetActive(false);
        if (constructionStageHalf != null) constructionStageHalf.SetActive(false);
        if (upgradeStageVisual != null) upgradeStageVisual.SetActive(false);
        if (mainVisualRoot != null) mainVisualRoot.SetActive(true);
        SetTimerVisualsActive(false);

        // Fetched fresh here, NOT cached in Awake() - BuildingInstance is added dynamically via
        // AddComponent AFTER this component's Awake() already ran (this component is baked into
        // the prefab, BuildingInstance is added later by BuildingPlacementController), so an
        // Awake()-time GetComponent call would always find null and silently no-op forever.
        var instance = GetComponent<BuildingInstance>();
        if (instance == null)
        {
            Debug.LogWarning($"[BuildingConstructionState] No BuildingInstance found on '{gameObject.name}' at completion - level/stats will NOT update.");
            return;
        }

        if (wasUpgrading)
            instance.CompleteUpgrade(pendingUpgradeLevelIndex);
        else
            instance.CompleteInitialConstruction();
    }
}