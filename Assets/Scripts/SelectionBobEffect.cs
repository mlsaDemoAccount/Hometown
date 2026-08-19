using UnityEngine;

/// Added to a building's GameObject while selected: gently lifts it and bobs it up/down.
/// Call Begin() when selecting, EndAndRestore() when deselecting - it removes itself and
/// puts the building back at its original height.
public class SelectionBobEffect : MonoBehaviour
{
    public float bobHeight = 0.15f;
    public float bobSpeed = 2f;

    private float baseY;

    public void Begin()
    {
        baseY = transform.position.y;
    }

    void Update()
    {
        Vector3 pos = transform.position;
        // Remapped from Mathf.Sin's normal -1..1 range to 0..1, so the building only ever
        // lifts UP from baseY and back down to it - never below, which was sinking it into
        // the ground on the downward half of a normal sine wave.
        float bobT = (Mathf.Sin(Time.time * bobSpeed) + 1f) * 0.5f;
        pos.y = baseY + bobT * bobHeight;
        transform.position = pos;
    }

    /// Restores original Y position, then removes this component.
    public void EndAndRestore()
    {
        Vector3 pos = transform.position;
        pos.y = baseY;
        transform.position = pos;

        Destroy(this);
    }
}