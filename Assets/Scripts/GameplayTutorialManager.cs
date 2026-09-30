using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;

/// First-time-player tutorial for the Gameplay scene. Shown once; completion is stored through
/// TutorialProgressStore (local now, API later). Steps:
///   1. CityStatsPanel highlighted, character explains the bars           (Next button)
///   2. Shop button highlighted, player taps it                           (waits for shop to open)
///   3. Small-house item highlighted in the shop, player taps it          (waits for placement to begin)
///   4. Player drags the house and taps Done                              (waits for placement to end)
///   5. Construction started message                                      (Finish button)
///
/// Everything real (shop, placement ghost, Done/Cancel, construction animation) is the existing
/// game code - this script only WATCHES their state each frame (polling, same approach as
/// ShopUIController's funds label) and points at things. No events, delegates or Actions.
public class GameplayTutorialManager : MonoBehaviour
{
    private enum Step { WaitingToStart, StatsIntro, ClickShop, ClickHouse, DragHouse, Constructing, Finished }

    [Header("Behaviour")]
    [Tooltip("Seconds to wait after the scene starts (lets the camera intro pan finish) before the tutorial appears.")]
    [SerializeField] private float startDelay = 3f;
    [Tooltip("Tick to show the tutorial every time, ignoring the saved flag. For testing only.")]
    [SerializeField] private bool forceShowForTesting = false;

    [Header("Existing game objects to point at")]
    [SerializeField] private RectTransform cityStatsPanel;
    [SerializeField] private RectTransform shopButton;
    [Tooltip("The same Shop Panel object assigned in UiManager. Used only to detect open/closed.")]
    [SerializeField] private GameObject shopPanel;
    [Tooltip("The building the player is told to place (the small house BuildingData asset).")]
    [SerializeField] private BuildingData tutorialBuilding;

    [Header("Tutorial UI")]
    [SerializeField] private TutorialHighlightOverlay highlight;
    [SerializeField] private GameObject dialogRoot;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button nextButton;
    [SerializeField] private TMP_Text nextButtonLabel;
    [SerializeField] private Button skipButton;
    [Tooltip("Where the character/message box sits when it should be at the top of the screen.")]
    [SerializeField] private RectTransform dialogTopAnchor;
    [Tooltip("Where the character/message box sits when it should be at the bottom of the screen.")]
    [SerializeField] private RectTransform dialogBottomAnchor;

    [Header("Hand Pointers")]
    [Tooltip("Hand that points at a button (bobs up and down at the target).")]
    [SerializeField] private RectTransform handPointer;
    [Tooltip("Hand that slides left/right over the map to show 'drag the house'.")]
    [SerializeField] private RectTransform dragHand;
    [SerializeField] private float handBobDistance = 25f;
    [SerializeField] private float handBobSpeed = 4f;
    [SerializeField] private float dragHandDistance = 180f;
    [SerializeField] private float dragHandSpeed = 1.5f;

    [Header("Messages")]
    [TextArea(2, 5)] [SerializeField] private string statsMessage =
        "This is the progress of your city. Every indicator shows points. If a category turns red, it needs your focus - keep working until every bar is green!";
    [TextArea(2, 5)] [SerializeField] private string shopMessage = "Let's build something! Tap the Shop button.";
    [TextArea(2, 5)] [SerializeField] private string houseMessage = "Choose the small house to start building.";
    [TextArea(2, 5)] [SerializeField] private string dragMessage =
        "Drag the house to a free spot on the map. When it shows green, tap Done to place it.";
    [TextArea(2, 5)] [SerializeField] private string constructingMessage =
        "Great job! Your house is now being constructed. Come back when it's finished!";

    private Step step = Step.WaitingToStart;
    private float timer;
    private bool statsPanelWasActive;
    private int buildingsBeforePlacement;
    private bool categoryRequested;

    void Awake()
    {
        if (nextButton != null) nextButton.onClick.AddListener(OnNextClicked);
        if (skipButton != null) skipButton.onClick.AddListener(OnSkipClicked);
        HideAllTutorialUI();
    }

    void Start()
    {
        if (!forceShowForTesting && TutorialProgressStore.IsGameplayTutorialCompleted())
            enabled = false; // already done on a previous run - never shows again
    }

    void Update()
    {
        switch (step)
        {
            case Step.WaitingToStart: UpdateWaitingToStart(); break;
            case Step.ClickShop: UpdateClickShop(); break;
            case Step.ClickHouse: UpdateClickHouse(); break;
            case Step.DragHouse: UpdateDragHouse(); break;
        }

        AnimateHands();
    }

    // ---------- Step logic ----------

    private void UpdateWaitingToStart()
    {
        timer += Time.deltaTime;
        if (timer < startDelay) return;
        EnterStatsIntro();
    }

    private void EnterStatsIntro()
    {
        step = Step.StatsIntro;

        // Stats panel is normally hidden behind the profile button - show it for the tutorial.
        if (cityStatsPanel != null)
        {
            statsPanelWasActive = cityStatsPanel.gameObject.activeSelf;
            cityStatsPanel.gameObject.SetActive(true);
            CityStatsPanel.Instance?.RefreshDisplay();
        }

        highlight.SetTarget(cityStatsPanel);
        ShowDialog(statsMessage, false, true, "Next");
        SetHandsHidden();
    }

    private void EnterClickShop()
    {
        step = Step.ClickShop;

        if (cityStatsPanel != null && !statsPanelWasActive)
            cityStatsPanel.gameObject.SetActive(false);

        highlight.SetTarget(shopButton);
        ShowDialog(shopMessage, true, false);
        PointHandAt(shopButton);
    }

    private void UpdateClickShop()
    {
        // Safety net: if placement is already running (e.g. cancelled-then-restarted), don't get stuck here.
        if (IsPlacementActive())
        {
            EnterDragHouse();
            return;
        }

        if (shopPanel != null && shopPanel.activeInHierarchy)
            EnterClickHouse();
    }

    private void EnterClickHouse()
    {
        step = Step.ClickHouse;
        categoryRequested = false;
        highlight.Hide();
        ShowDialog(houseMessage, true, false);
        SetHandsHidden();
    }

    private void UpdateClickHouse()
    {
        // Placement started (player tapped the house) - move on. MUST be checked before the
        // "shop closed" test below: tapping an item closes the shop AND starts placement in the
        // same frame, so the shop being closed is expected here, not a reason to go back.
        if (IsPlacementActive())
        {
            EnterDragHouse();
            return;
        }

        // Player closed the shop without choosing - send them back to the shop button.
        if (shopPanel != null && !shopPanel.activeInHierarchy)
        {
            EnterClickShop();
            return;
        }

        // Make sure the house's category tab is showing. Shop rebuilds its list at the end of
        // this frame, so look for the button starting next frame.
        if (!categoryRequested)
        {
            categoryRequested = true;
            if (tutorialBuilding != null && ShopUIController.Instance != null)
                ShopUIController.Instance.SelectCategoryExternally(tutorialBuilding.primaryCategory);
            return;
        }

        RectTransform houseButton = FindHouseButton();
        if (houseButton != null && houseButton != highlight.Target)
        {
            highlight.SetTarget(houseButton);
            PointHandAt(houseButton);
        }
    }

    private static bool IsPlacementActive()
    {
        return BuildingPlacementController.Instance != null && BuildingPlacementController.Instance.IsPlacingOrMoving;
    }

    private RectTransform FindHouseButton()
    {
        if (ShopUIController.Instance == null || ShopUIController.Instance.itemListParent == null) return null;

        var buttons = ShopUIController.Instance.itemListParent.GetComponentsInChildren<ShopItemButton>(false);
        var match = buttons.FirstOrDefault(b => b != null && b.Data == tutorialBuilding);
        return match != null ? match.GetComponent<RectTransform>() : null;
    }

    private void EnterDragHouse()
    {
        step = Step.DragHouse;
        buildingsBeforePlacement = BuildingPlacementController.Instance.AllPlacedBuildings.Count();

        // No dim here - the player has to be able to drag on the map.
        highlight.Hide();
        ShowDialog(dragMessage, true, false);
    }

    private void UpdateDragHouse()
    {
        var placement = BuildingPlacementController.Instance;
        if (placement == null) return;

        // Placement ended: either the house was placed (Done) or cancelled.
        if (!placement.IsPlacingOrMoving)
        {
            int now = placement.AllPlacedBuildings.Count();
            if (now > buildingsBeforePlacement) EnterConstructing();
            else EnterClickShop(); // cancelled - start the shop part again
            return;
        }

        // Ghost on a valid spot -> point at Done. Otherwise -> show the drag gesture on the map.
        RectTransform done = placement.controlPanel != null && placement.controlPanel.doneButton != null
            ? placement.controlPanel.doneButton.GetComponent<RectTransform>()
            : null;

        if (placement.IsCurrentGhostValid() && done != null)
        {
            dragHand.gameObject.SetActive(false);
            PointHandAt(done);
        }
        else
        {
            handPointer.gameObject.SetActive(false);
            dragHand.gameObject.SetActive(true);
        }
    }

    private void EnterConstructing()
    {
        step = Step.Constructing;
        highlight.Hide();
        SetHandsHidden();
        ShowDialog(constructingMessage, true, true, "Finish");
    }

    // ---------- Buttons ----------

    private void OnNextClicked()
    {
        if (step == Step.StatsIntro) EnterClickShop();
        else if (step == Step.Constructing) FinishTutorial();
    }

    private void OnSkipClicked()
    {
        // Undo anything the tutorial changed, then mark it done so it doesn't come back.
        if (cityStatsPanel != null && step == Step.StatsIntro && !statsPanelWasActive)
            cityStatsPanel.gameObject.SetActive(false);
        FinishTutorial();
    }

    private void FinishTutorial()
    {
        step = Step.Finished;
        HideAllTutorialUI();
        TutorialProgressStore.MarkGameplayTutorialCompleted();
        enabled = false;
    }

    // ---------- UI helpers ----------

    private void ShowDialog(string message, bool atTop, bool showNext, string nextLabel = "Next")
    {
        dialogRoot.SetActive(true);
        messageText.text = message;

        RectTransform anchor = atTop ? dialogTopAnchor : dialogBottomAnchor;
        if (anchor != null) dialogRoot.transform.position = anchor.position;

        if (nextButton != null) nextButton.gameObject.SetActive(showNext);
        if (nextButtonLabel != null) nextButtonLabel.text = nextLabel;
        if (skipButton != null) skipButton.gameObject.SetActive(true);
    }

    private void PointHandAt(RectTransform target)
    {
        if (target == null) return;
        handPointer.gameObject.SetActive(true);
        handPointer.position = target.position; // bob offset is added each frame in AnimateHands
        handBasePosition = handPointer.position;
    }

    private Vector3 handBasePosition;

    private void SetHandsHidden()
    {
        handPointer.gameObject.SetActive(false);
        dragHand.gameObject.SetActive(false);
    }

    private void HideAllTutorialUI()
    {
        if (highlight != null) highlight.Hide();
        if (dialogRoot != null) dialogRoot.SetActive(false);
        if (handPointer != null) handPointer.gameObject.SetActive(false);
        if (dragHand != null) dragHand.gameObject.SetActive(false);
    }

    private void AnimateHands()
    {
        if (handPointer.gameObject.activeSelf)
        {
            // Keep following the target (it can move/scroll), then bob up and down on top of it.
            RectTransform followed = highlight.Target;
            if (step == Step.DragHouse && BuildingPlacementController.Instance != null
                && BuildingPlacementController.Instance.controlPanel != null
                && BuildingPlacementController.Instance.controlPanel.doneButton != null)
                followed = BuildingPlacementController.Instance.controlPanel.doneButton.GetComponent<RectTransform>();
            else if (step == Step.ClickShop)
                followed = shopButton;

            if (followed != null) handBasePosition = followed.position;

            handPointer.position = handBasePosition;
            handPointer.anchoredPosition += Vector2.up * (Mathf.Sin(Time.unscaledTime * handBobSpeed) * handBobDistance);
        }

        if (dragHand.gameObject.activeSelf)
        {
            // Slides left/right around the screen centre.
            float x = Mathf.PingPong(Time.unscaledTime * dragHandSpeed, 1f) * 2f - 1f;
            dragHand.position = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
            dragHand.anchoredPosition += Vector2.right * (x * dragHandDistance);
        }
    }

    [ContextMenu("Reset Tutorial (show again next run)")]
    private void ResetTutorialFlag()
    {
        TutorialProgressStore.ResetGameplayTutorial();
        Debug.Log("[GameplayTutorialManager] Tutorial flag cleared - it will show on the next Play.");
    }
}
