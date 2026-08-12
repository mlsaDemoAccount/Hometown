using UnityEngine;

/// A hand-tunable road/footpath waypoint, created by RoadGraphBuilder's "Bake Waypoints To
/// GameObjects" tool. Drag this in the Scene view to nudge its position - e.g. centering it in
/// a curve or a wide/irregular intersection for better-looking car movement. If ANY of these
/// exist in the scene when RoadGraphBuilder runs, their current position is used instead of the
/// raw auto-detected grid-cell center for that specific cell.
///
/// IMPORTANT: objects created/edited while in Play Mode are lost when you stop Play - Unity
/// doesn't save Play Mode changes to the scene. See RoadGraphBuilder's bake instructions for
/// the safe workflow (bake in Play Mode, copy-paste the container out before stopping).
public class RoadWaypointMarker : MonoBehaviour
{
    public Vector2Int cell;      // which grid cell this corresponds to - don't change this by hand
    public bool isFootpath;      // true = footpath graph, false = road graph

    private void OnDrawGizmos()
    {
        Gizmos.color = isFootpath ? new Color(0.2f, 0.8f, 1f) : Color.yellow;
        Gizmos.DrawSphere(transform.position, 0.15f);
    }
}