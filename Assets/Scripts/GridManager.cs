using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Core grid data structure. Works for 100x100 or 1000x1000 -
/// for 1000x1000 use CHUNKING (see notes at bottom) so you don't
/// spawn 1,000,000 GameObjects for the ground/visual grid.
/// This class only tracks LOGICAL data, not visuals, so it scales fine either way.
/// </summary>
public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    [Header("Grid Settings")]
    public int width = 100;   // try 100 first, bump to 1000 once perf is validated
    public int height = 100;
    public float cellSize = 1f; // meters per cell
    public Vector3 origin = Vector3.zero;

    public enum CellType { Empty, Building, Road, Blocked, Locked }

    /// What kind of thing occupies a cell - lets you distinguish "a building" from
    /// "an environment prop" (tree/rock) when both just show up as an occupied cell.
    public enum OccupantKind { None, Building, Road, EnvironmentProp }

    private struct Cell
    {
        public CellType type;
        public int occupantId; // id into BuildingInstance/Road registry, -1 if none
        public OccupantKind occupantKind;
        public bool hasFootpath; // independent of type - a Road cell can ALSO have a footpath attached
    }

    [Header("Baked blocked cells")]
    [Tooltip("Filler buildings, water, mountains... baked by Tools > Grid > Bake. Applied once in Awake, so it is ready before any building is placed or loaded.")]
    public GridBlockData blockData;

    private Cell[,] grid;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        grid = new Cell[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                grid[x, y] = new Cell { type = CellType.Empty, occupantId = -1, occupantKind = OccupantKind.None };
        ApplyBlockData();
    }

    /// Number of baked blocked cells that were applied (for debugging).
    public int BlockedCellCount { get; private set; }

    private void ApplyBlockData()
    {
        if (blockData == null) return;
        if (!blockData.Matches(width, height))
        {
            Debug.LogWarning("GridManager: GridBlockData size does not match the grid - re-bake it (Tools > Grid > Bake).");
            return;
        }
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (blockData.Get(x, y) != GridBlockData.Free)
                {
                    LockCell(new Vector2Int(x, y), -1);
                    BlockedCellCount++;
                }
    }

    public bool InBounds(Vector2Int c) =>
        c.x >= 0 && c.y >= 0 && c.x < width && c.y < height;

    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        Vector3 local = worldPos - origin;
        int x = Mathf.FloorToInt(local.x / cellSize);
        int y = Mathf.FloorToInt(local.z / cellSize);
        return new Vector2Int(x, y);
    }

    public Vector3 GridToWorld(Vector2Int cell)
    {
        return origin + new Vector3(
            (cell.x + 0.5f) * cellSize,
            0f,
            (cell.y + 0.5f) * cellSize);
    }

    /// Correct world position for a MULTI-CELL building's center, given its origin (bottom-left)
    /// cell and footprint size. Do NOT use GridToWorld() for anything bigger than 1x1 - that
    /// only returns the center of the single origin cell, which visibly drifts off-center
    /// (by half a cell for even sizes, a whole cell for odd sizes) as footprint size grows.
    public Vector3 FootprintCenterToWorld(Vector2Int footprintOrigin, Vector2Int size)
    {
        float worldX = footprintOrigin.x * cellSize + (size.x * cellSize) / 2f;
        float worldZ = footprintOrigin.y * cellSize + (size.y * cellSize) / 2f;
        return origin + new Vector3(worldX, 0f, worldZ);
    }

    public bool IsFree(Vector2Int c) =>
        InBounds(c) && grid[c.x, c.y].type == CellType.Empty;

    public CellType GetType(Vector2Int c) =>
        InBounds(c) ? grid[c.x, c.y].type : CellType.Blocked;

    public int GetOccupant(Vector2Int c) =>
        InBounds(c) ? grid[c.x, c.y].occupantId : -1;

    public OccupantKind GetOccupantKind(Vector2Int c) =>
        InBounds(c) ? grid[c.x, c.y].occupantKind : OccupantKind.None;

    public bool HasFootpath(Vector2Int c) => InBounds(c) && grid[c.x, c.y].hasFootpath;

    /// Marks/unmarks footpath presence on a cell WITHOUT touching its CellType - a road cell
    /// can also have a footpath attached (they're fused into the same 1-wide cell now), so this
    /// stays independent of SetFootprint's type marking.
    public void SetFootpathFlag(Vector2Int c, bool value)
    {
        if (!InBounds(c)) return;
        var cellData = grid[c.x, c.y];
        cellData.hasFootpath = value;
        grid[c.x, c.y] = cellData;
    }

    /// Checks a whole footprint (for multi-cell buildings), returns true only if ALL cells free.
    public bool CanPlaceFootprint(Vector2Int origin, Vector2Int size)
    {
        for (int x = 0; x < size.x; x++)
            for (int y = 0; y < size.y; y++)
            {
                Vector2Int c = origin + new Vector2Int(x, y);
                if (!IsFree(c)) return false;
            }
        return true;
    }

    public void SetFootprint(Vector2Int origin, Vector2Int size, CellType type, int occupantId, OccupantKind kind = OccupantKind.None)
    {
        for (int x = 0; x < size.x; x++)
            for (int y = 0; y < size.y; y++)
            {
                Vector2Int c = origin + new Vector2Int(x, y);
                if (!InBounds(c)) continue;
                grid[c.x, c.y] = new Cell { type = type, occupantId = occupantId, occupantKind = kind };
            }
    }

    public void ClearFootprint(Vector2Int origin, Vector2Int size)
    {
        SetFootprint(origin, size, CellType.Empty, -1, OccupantKind.None);
    }

    /// Marks a single cell as permanently locked (tree, rock, water, out-of-bounds terrain
    /// feature). Locked cells behave like Blocked for placement purposes but are semantically
    /// distinct so you can tell "can't build here because of scenery" apart from "occupied".
    public void LockCell(Vector2Int cell, int propId)
    {
        if (!InBounds(cell)) return;
        grid[cell.x, cell.y] = new Cell { type = CellType.Locked, occupantId = propId, occupantKind = OccupantKind.EnvironmentProp };
    }

    /// World-space clamp bounds - used by the camera controller so it can't pan off the map.
    public Bounds GetWorldBounds()
    {
        Vector3 size = new Vector3(width * cellSize, 0, height * cellSize);
        return new Bounds(origin + size / 2f, size);
    }
}

/*
 NOTES FOR SCALING TO 1000x1000:
 - Keep GridManager's logical array as-is (a 1000x1000 struct array is small, ~4MB, totally fine).
 - Do NOT instantiate one GameObject per cell for the visual ground. Instead:
   a) Use a single large plane/mesh with a shader/material that draws grid lines, OR
   b) Split visuals into 32x32 or 64x64 chunks, and only render/enable chunks near the camera
      (simple frustum or distance check in a ChunkManager - ask me for this script if needed).
 - Road and building GameObjects are only ever instantiated where the player actually builds,
   so those counts stay reasonable regardless of grid size.
*/