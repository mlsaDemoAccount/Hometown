using UnityEngine;
using UnityEngine.UI;

/// Put this on a landmark's world-space canvas (e.g. "Budha Canvas"). Its "Landmark - Button"
/// opens the memories for this landmark.
///
/// landmarkId is what the server uses to keep each landmark's memories separate - keep it stable
/// once memories exist. Left empty, it is derived from the object name ("Budha Canvas" -> "budha").
public class LandmarkMarker : MonoBehaviour
{
    [Tooltip("Stable id sent to the server, e.g. 'budha'. Empty = derived from this object's name.")]
    public string landmarkId;
    [Tooltip("Shown as the Memories screen title. Empty = this object's name without 'Canvas'.")]
    public string displayName;
    [Tooltip("The landmark's button. Empty = the child named 'Landmark - Button'.")]
    public Button button;

    void Awake()
    {
        string baseName = name.Replace("Canvas", "").Trim();
        if (string.IsNullOrEmpty(landmarkId)) landmarkId = baseName.ToLowerInvariant().Replace(' ', '-');
        if (string.IsNullOrEmpty(displayName)) displayName = baseName;
        if (button == null) button = MemoryUi.Find<Button>(transform, "Landmark - Button");
        if (button != null) button.onClick.AddListener(OnButtonClick);
        else Debug.LogWarning($"[LandmarkMarker] '{name}' has no 'Landmark - Button' child.", this);
    }

    void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(OnButtonClick);
    }

    public void OnButtonClick()
    {
        var screen = MemoryUi.FindScreen<OceanParkMemoriesScreen>();
        if (screen == null) { Debug.LogWarning("[LandmarkMarker] MemoriesScreen not found in the scene."); return; }
        screen.Open(this);
    }
}
