using System.Collections.Generic;
using UnityEngine;

/// Converts a raw road-graph node path into a sideways-offset walking/driving path. Works for
/// straight roads, L-shaped corners, and 4-way intersections without needing to know the
/// intersection's full shape - it only needs "which way am I coming from, which way am I going"
/// at each node, since any single path through an intersection only ever uses 2 of its edges.
/// Shared by TrafficAgent (small offset = lane) and PedestrianAgent (larger offset = footpath
/// alongside the same road, no separate footpath waypoint network needed).
public static class PathOffsetUtility
{
    public static List<Vector3> BuildOffsetWaypoints(List<RoadNode> nodePath, float offset, bool toRightSide)
    {
        var result = new List<Vector3>(nodePath.Count);
        float sideSign = toRightSide ? 1f : -1f;

        for (int i = 0; i < nodePath.Count; i++)
        {
            Vector3 dir;

            if (i == 0)
            {
                dir = nodePath[1].worldPosition - nodePath[0].worldPosition;
            }
            else if (i == nodePath.Count - 1)
            {
                dir = nodePath[i].worldPosition - nodePath[i - 1].worldPosition;
            }
            else
            {
                // Blend incoming and outgoing direction so the offset turns smoothly through a
                // corner/intersection instead of snapping from one side-vector to a very
                // different one. Note: with a large offset (pedestrians), this "kink" at sharp
                // turns is more visually noticeable than it is for a small vehicle lane offset -
                // a known approximation, not a bug.
                Vector3 incoming = (nodePath[i].worldPosition - nodePath[i - 1].worldPosition).normalized;
                Vector3 outgoing = (nodePath[i + 1].worldPosition - nodePath[i].worldPosition).normalized;
                dir = incoming + outgoing;
                if (dir.sqrMagnitude < 0.0001f) dir = outgoing; // near-180 turn (rare on a grid) - fall back
            }

            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) { result.Add(nodePath[i].worldPosition); continue; }
            dir.Normalize();

            // Cross(up, dir) points to the right of the direction of travel in Unity's
            // left-handed, Y-up coordinate system.
            Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
            result.Add(nodePath[i].worldPosition + right * (offset * sideSign));
        }

        return result;
    }
}
