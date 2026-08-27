using UnityEngine;

/// Attach to any UI panel or button that should robustly block world-space taps/clicks,
/// independent of EventSystem timing (see UIPointerUtil for why that matters). Auto-registers
/// this object's RectTransform while active, un-registers when inactive/destroyed - so a
/// hidden panel never blocks anything, exactly matching what you'd want.
///
/// Add this to: your Done/Cancel/Rotate panel, the Move/Demolish/Upgrade inspector panel, the
/// Shop panel, category tabs, shop item buttons, notification popup - anywhere a tap should
/// never also register as a world-space placement/selection tap underneath it.
[RequireComponent(typeof(RectTransform))]
public class UIBlockerRegistrar : MonoBehaviour
{
    private RectTransform rect;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
    }

    void OnEnable()
    {
        UIPointerUtil.RegisterBlocker(rect);
    }

    void OnDisable()
    {
        UIPointerUtil.UnregisterBlocker(rect);
    }
}
