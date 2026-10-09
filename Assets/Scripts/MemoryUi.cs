using UnityEngine;
using UnityEngine.UI;

/// Small helpers shared by the memory screens.
public static class MemoryUi
{
    /// Finds a component on a descendant (active or not) by object name. The memory screens use
    /// this to wire themselves up from the prefab's own hierarchy, so nothing has to be dragged
    /// into the Inspector (explicitly assigned fields still win).
    public static T Find<T>(Transform root, string objectName) where T : Component
    {
        if (root == null) return null;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name != objectName) continue;
            var c = t.GetComponent<T>();
            if (c != null) return c;
        }
        return null;
    }

    /// Finds a screen in the open scene even while its GameObject is inactive
    /// (FindObjectOfType skips inactive objects; prefab assets are filtered out).
    public static T FindScreen<T>() where T : Component
    {
        foreach (var c in Resources.FindObjectsOfTypeAll<T>())
            if (c != null && c.gameObject.scene.IsValid()) return c;
        return null;
    }

    public static bool IsLoggedIn => AuthManager.Instance != null && AuthManager.Instance.IsLoggedIn;

    /// Shows a downloaded photo on an Image. The Sprite is returned so the caller can destroy it
    /// later (a Sprite created from a Texture2D isn't freed automatically).
    public static Sprite SetPhoto(Image image, Texture2D texture)
    {
        if (image == null || texture == null) return null;
        var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        image.sprite = sprite;
        image.color = Color.white;
        image.preserveAspect = true;
        image.enabled = true;
        return sprite;
    }
}
