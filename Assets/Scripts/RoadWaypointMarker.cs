using System.Collections.Generic;
using UnityEngine;

/// A hand-placed waypoint for the car/pedestrian movement graph. NOTHING is auto-generated for
/// this graph at runtime anymore - you create these yourself (one GameObject + this component
/// per waypoint) and wire up which ones connect to which by dragging references into
/// `connections` in the Inspector. This lets you build out a small, curated waypoint network
/// for exactly the roads you're testing, instead of the whole map at once.
///
/// A normal two-way road only needs you to link each waypoint to its neighbor ONE direction -
/// the reverse edge is added automatically when the graph builds. (When one-way roads are
/// added later, that's the point where a per-connection "oneWay" flag would skip the auto
/// reverse edge.)
[ExecuteAlways]
public class RoadWaypointMarker : MonoBehaviour
{
    [Tooltip("Auto-computed from this object's position - which grid cell it's sitting in. Informational/debugging only, not used for pathfinding (that's driven by `connections` below). You never need to type this in by hand.")]
    public Vector2Int cell;

    [Tooltip("true = footpath graph (pedestrians), false = road graph (vehicles).")]
    public bool isFootpath;

    [Tooltip("Drag other RoadWaypointMarker objects here that this one connects directly to.")]
    public List<RoadWaypointMarker> connections = new List<RoadWaypointMarker>();

    [Header("Debug")]
    [Tooltip("Shows this waypoint (yellow = road, cyan = footpath) and its connections in the Scene view. Purely visual - turning this off doesn't change any functionality, just hides the gizmo.")]
    public bool showDebugGizmo = true;

    void Update()
    {
        if (Application.isPlaying) return; // position is fixed once you're playing - no need to keep recalculating
        var grid = FindObjectOfType<GridManager>();
        if (grid != null) cell = grid.WorldToGrid(transform.position);
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmo) return;

        Gizmos.color = isFootpath ? new Color(0.2f, 0.8f, 1f) : Color.yellow;
        Gizmos.DrawSphere(transform.position, 0.15f);

        Gizmos.color = Color.white;
        foreach (var other in connections)
            if (other != null) Gizmos.DrawLine(transform.position, other.transform.position);
    }
}