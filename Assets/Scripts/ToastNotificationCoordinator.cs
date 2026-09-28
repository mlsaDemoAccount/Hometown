using UnityEngine;

/// Shared gatekeeper ensuring only ONE toast notification (from ANY system - city events,
/// stat-based suggestions, etc.) is ever visible at once, and that both systems respect the
/// SAME overall interval between notifications - rather than each running its own completely
/// independent timer, which could coincidentally show two toasts on screen simultaneously
/// (which is exactly what was happening before this existed).
public class ToastNotificationCoordinator : MonoBehaviour
{
    public static ToastNotificationCoordinator Instance { get; private set; }

    [Tooltip("Seconds between ANY toast notification - shared by both CityEventManager (event toasts) and CityNotificationManager (stat toasts), so they never overlap and both follow the same pacing.")]
    public float notificationInterval = 120f;

    public bool IsToastActive { get; private set; } = false;

    /// How many seconds have elapsed since the last toast ended - exposed for diagnostics, so
    /// callers can log exactly why TryBeginToast() returned false (still waiting vs. stuck busy).
    public float ElapsedSinceLastToast => timer;

    private float timer = 0f;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Update()
    {
        if (IsToastActive) return; // a toast is currently showing - shared timer doesn't advance
        timer += Time.deltaTime;
    }

    /// Call this before showing a toast. Returns true (and marks the coordinator busy) if
    /// nothing else is currently showing AND the shared interval has elapsed - the caller
    /// should proceed to show its toast in that case. Returns false otherwise - the caller
    /// should just skip this attempt; it naturally gets another chance whenever ITS OWN
    /// underlying trigger next occurs (next event spawn, next stat check, etc.).
    public bool TryBeginToast()
    {
        if (IsToastActive || timer < notificationInterval) return false;

        IsToastActive = true;
        timer = 0f;
        return true;
    }

    /// For toasts announcing something that just happened (e.g. a new city event) - still
    /// enforces one-toast-at-a-time, but does NOT wait for the shared interval, since the
    /// caller is expected to keep retrying (queue) until the slot frees up. Without this, a
    /// caller that only tries once at the moment of the event almost always loses to a caller
    /// polling every frame (the stat toasts), which is exactly what was dropping event toasts.
    /// Like TryBeginToast, EndToast() must be called afterward.
    public bool TryBeginPriorityToast()
    {
        if (IsToastActive) return false;

        IsToastActive = true;
        timer = 0f;
        return true;
    }

    /// Call this once a toast finishes (dismissed, accepted, or auto-timeout) - frees the gate
    /// for the next one. MUST be called on every exit path after TryBeginToast() succeeded,
    /// including early-return/error paths, or the gate stays stuck busy forever.
    public void EndToast()
    {
        IsToastActive = false;
        timer = 0f;
    }
}