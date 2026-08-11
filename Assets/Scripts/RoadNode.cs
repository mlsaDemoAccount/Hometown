using System.Collections.Generic;
using UnityEngine;

/// A single node in the road or footpath graph - one per grid cell detected as road/footpath.
/// Plain C# class (NOT a MonoBehaviour) so a large road network doesn't mean thousands of
/// GameObjects - same reasoning as GridManager using a logical array instead of a GameObject
/// per grid cell.
public class RoadNode
{
    public Vector2Int cell;
    public Vector3 worldPosition;
    public List<RoadEdge> edges = new List<RoadEdge>(4); // up to 4 (N/S/E/W) on a grid-based network

    /// 3+ connections = a real intersection. This is the natural, automatically-discovered
    /// attachment point for traffic lights / stop signs later - no separate authoring needed.
    public bool IsIntersection => edges.Count >= 3;

    /// Reserved for future traffic control (stop sign / traffic light / yield). Not implemented
    /// yet - just here so the traffic system has a place to check without another rewrite.
    public TrafficControlType controlType = TrafficControlType.None;

    public RoadNode(Vector2Int cell, Vector3 worldPosition)
    {
        this.cell = cell;
        this.worldPosition = worldPosition;
    }
}

/// A directed connection from one RoadNode to a neighboring one. A normal two-way road gets
/// an edge in both directions; a future one-way road would simply omit the reverse edge.
public class RoadEdge
{
    public RoadNode from;
    public RoadNode to;
    public float length; // world-space distance - pathfinding cost now, speed/ETA calc later

    public RoadEdge(RoadNode from, RoadNode to, float length)
    {
        this.from = from;
        this.to = to;
        this.length = length;
    }
}

public enum TrafficControlType { None, StopSign, TrafficLight, YieldSign }
