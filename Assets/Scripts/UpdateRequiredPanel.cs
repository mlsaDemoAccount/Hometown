using UnityEngine;
using UnityEngine.UI;

/// Result of showing the update popup - the caller waits on isDone from its own coroutine, then
/// reads userChoseUpdate. No callbacks/delegates - same handle pattern as ApiRequestHandle /
/// VersionCheckHandle.
public class UpdatePopupChoiceHandle
{
    public bool isDone = false;
    public bool userChoseUpdate = false; // only ever true right before the store link opens - Later/Skip is what actually resolves this
}

/// The "update available" popup - shown OVER whatever screen called it (typically the splash
/// screen, which stays visible/active underneath). "Update Now" opens the store link but does
/// NOT resolve the handle - the player stays on this popup, since opening the store shouldn't
/// silently let them back into the game. "Later" resolves the handle and the caller proceeds -
/// UNLESS the server sent forceUpdate = true, in which case Later is disabled and updating is
/// mandatory.
public class UpdateRequiredPanel : MonoBehaviour
{
    public GameObject panelRoot; // can be this.gameObject
    public Text messageText;
    public Button updateButton;
    public Button laterButton;

    [Tooltip("Shown if the server's message field is null/empty.")]
    public string defaultMessage = "A new version is available. Please update from the store to get the latest features and fixes.";

    private string pendingStoreUrl;
    private UpdatePopupChoiceHandle currentHandle;

    void Start()
    {
        if (panelRoot == null) panelRoot = gameObject;

        updateButton.onClick.AddListener(OnUpdateClicked);
        laterButton.onClick.AddListener(OnLaterClicked);

        // IMPORTANT: do NOT call Hide() here - if panelRoot starts inactive in the scene (as it
        // should), Start() only runs the moment it's FIRST activated, and calling SetActive(false)
        // from inside that same Start() would undo the very SetActive(true) that triggered it,
        // silently cancelling the first-ever Show(). Just make sure panelRoot is unchecked
        // (inactive) in the Hierarchy by default.
    }

    /// message: optional server-provided text (can be null) - falls back to defaultMessage.
    /// forceUpdate: if true, Later is disabled - the player must update to continue.
    /// Returns a handle - wait on its isDone from your own coroutine to know when/if the
    /// player chose to proceed.
    public UpdatePopupChoiceHandle Show(string storeUrl, string message, bool forceUpdate)
    {
        pendingStoreUrl = storeUrl;
        if (messageText != null)
            messageText.text = string.IsNullOrEmpty(message) ? defaultMessage : message;

        if (laterButton != null)
            laterButton.interactable = !forceUpdate;

        panelRoot.SetActive(true);

        currentHandle = new UpdatePopupChoiceHandle();
        return currentHandle;
    }

    public void Hide()
    {
        panelRoot.SetActive(false);
    }

    private void OnUpdateClicked()
    {
        if (!string.IsNullOrEmpty(pendingStoreUrl))
            Application.OpenURL(pendingStoreUrl);
        // Deliberately does NOT resolve the handle - the player stays on this popup after
        // opening the store, rather than silently proceeding into the game.
    }

    private void OnLaterClicked()
    {
        Hide();
        if (currentHandle != null)
        {
            currentHandle.userChoseUpdate = false;
            currentHandle.isDone = true;
        }
    }
}