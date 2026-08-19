using UnityEngine;

/// Attach to the base indicator sprite's GameObject (the flat valid/invalid decal under a
/// building's ghost). BuildingPlacementController finds this automatically and swaps between
/// valid/invalid sprites directly, instead of running it through the generic multi-material
/// transparency system - that system doesn't work correctly here since SpriteRenderer's tint
/// and opacity are driven by its own `color` property, not shader color properties.
///
/// The actual sprite IMAGES live centrally on BuildingPlacementController, not here - this
/// component only needs its own SpriteRenderer reference, so changing the indicator's look
/// later means updating one field in one place, not every building prefab individually.
public class GhostBaseIndicator : MonoBehaviour
{
    public SpriteRenderer spriteRenderer;

    void Reset()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
    }

    /// Swaps the sprite between valid/invalid (sprites passed in from the central source),
    /// always staying fully opaque - unlike the building itself, this indicator should stay
    /// crisp/clear at all times, not fade to match the building's own translucent preview.
    public void SetValid(bool valid, Sprite validSprite, Sprite invalidSprite)
    {
        if (spriteRenderer == null) return;

        spriteRenderer.sprite = valid ? validSprite : invalidSprite;

        Color c = spriteRenderer.color;
        c.a = 1f;
        spriteRenderer.color = c;
    }
}