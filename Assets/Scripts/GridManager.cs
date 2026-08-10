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
    public event System.Action OnGridChanged;

    [Header("Grid Settings")]
    public int width = 100;   // try 100 first, bump to 1000 once perf is validated
    public int height = 100;
    public float cellSize = 1f; // meters per cell
    public Vector3 origin = Vector3.zero;

    public enum CellType { Empty, Building, Road, Blocked }

public GameObject GridGenerateObject; //sprite generated on each grid
public Transform GridsSpriteParent; //parent of all grid sprites
    private struct Cell
    {
        public CellType type;
        public int occupantId; // id into BuildingInstance/Road registry, -1 if none
    }

    private Cell[,] grid;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        grid = new Cell[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                grid[x, y] = new Cell { type = CellType.Empty, occupantId = -1 };
                // GameObject gameObject = Instantiate(GridGenerateObject, new Vector3((float)x + 0.5f, 0.1f, (float)y + 0.5f), GridGenerateObject.transform.rotation, GridsSpriteParent);
                // GridCellScript cell = gameObject.GetComponent<GridCellScript>();
				// cell.isDark = (x + y) % 2 != 0;


				// grid[x, y] = cell;	
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

    public bool IsFree(Vector2Int c) =>
        InBounds(c) && grid[c.x, c.y].type == CellType.Empty;

    public CellType GetType(Vector2Int c) =>
        InBounds(c) ? grid[c.x, c.y].type : CellType.Blocked;

    public int GetOccupant(Vector2Int c) =>
        InBounds(c) ? grid[c.x, c.y].occupantId : -1;

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

    public void SetFootprint(Vector2Int origin, Vector2Int size, CellType type, int occupantId)
    {
        for (int x = 0; x < size.x; x++)
            for (int y = 0; y < size.y; y++)
            {
                Vector2Int c = origin + new Vector2Int(x, y);
                if (!InBounds(c)) continue;
                grid[c.x, c.y] = new Cell { type = type, occupantId = occupantId };
            }
        OnGridChanged?.Invoke();
    }

    public void ClearFootprint(Vector2Int origin, Vector2Int size)
    {
        SetFootprint(origin, size, CellType.Empty, -1);
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
