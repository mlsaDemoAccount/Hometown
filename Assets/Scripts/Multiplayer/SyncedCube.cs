using Fusion;
using UnityEngine;

/// The one shared object of the proof of concept (the real game uses a single CityNetworkSync
/// the same way - see MultiplayerDesign.md). Follows request -> validate -> apply:
///   1. a player drags the cube locally and only sends a REQUEST (RPC_RequestMove);
///   2. the host (state authority) VALIDATES it - keeps the cube inside the play area;
///   3. the host applies it, and NetworkTransform shows it on both phones.
/// The non-host player also shows the cube where they are dragging it right away (prediction), so
/// their own drag doesn't wait for the round trip to the host and back.
/// Needs a NetworkObject and a NetworkTransform on the same prefab.
[DefaultExecutionOrder(10000)] // after Fusion has interpolated the network position this frame
public class SyncedCube : NetworkBehaviour
{
    private const float PredictionHold = 0.4f; // keep our own position this long after the last drag

    public static SyncedCube Instance { get; private set; }

    [Tooltip("Half the size of the square play area, in metres.")]
    public float limit = 6f;

    private Vector3 predicted;
    private float predictedUntil;
    private Vector3 pending;
    private bool hasPending;

    public override void Spawned() => Instance = this;

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this) Instance = null;
    }

    /// Called by the local player's drag. The host moves the cube directly; the other player
    /// shows it moving at once and asks the host to do the real move.
    public void RequestMove(Vector3 target)
    {
        target = Validate(target);
        if (Object.HasStateAuthority)
        {
            pending = target; // applied inside the network tick (see FixedUpdateNetwork)
            hasPending = true;
            return;
        }
        predicted = target;
        predictedUntil = Time.unscaledTime + PredictionHold;
        RPC_RequestMove(target);
    }

    // The host must move the cube inside the network tick: moved from Update, Fusion's
    // NetworkTransform overwrites the change with the old networked position before it is captured.
    public override void FixedUpdateNetwork()
    {
        if (!hasPending || !Object.HasStateAuthority) return;
        transform.position = pending;
        hasPending = false;
    }

    void LateUpdate()
    {
        if (Time.unscaledTime < predictedUntil) transform.position = predicted;
    }

    private Vector3 Validate(Vector3 target)
    {
        target.x = Mathf.Clamp(target.x, -limit, limit);
        target.z = Mathf.Clamp(target.z, -limit, limit);
        target.y = transform.position.y;
        return target;
    }

    // Either player may ask; only the host (state authority) runs it.
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestMove(Vector3 target)
    {
        pending = Validate(target);
        hasPending = true;
    }
}
