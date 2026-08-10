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
        pos.y = baseY + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
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
