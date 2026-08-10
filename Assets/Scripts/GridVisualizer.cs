using UnityEngine;

public class GridVisualizer : MonoBehaviour
{
    [Header("Grid Appearance")]
    public Color emptyColor = new Color(0f, 0f, 0f, 0.08f);
    public Color roadColor = new Color(0.6f, 0.6f, 0.6f, 0.35f);
    public Color gridLineColor = new Color(0f, 0f, 0f, 0.25f);
    public float gizmoHeight = 0.02f;
    public bool drawGridLines = true;

    public Color[] categoryColors = new Color[]
    {
        new Color(0.22f, 0.62f, 0.95f, 0.35f), // Population
        new Color(0.09f, 0.76f, 0.16f, 0.35f), // Jobs
        new Color(0.95f, 0.32f, 0.24f, 0.35f), // Pollution
        new Color(0.98f, 0.76f, 0.18f, 0.35f), // Happiness
        new Color(0.67f, 0.12f, 0.76f, 0.35f)  // Garbage
    };

    private void OnDrawGizmos()
    {
        if (GridManager.Instance == null) return;

        int width = GridManager.Instance.width;
        int height = GridManager.Instance.height;
        float cellSize = GridManager.Instance.cellSize;
        Vector3 origin = GridManager.Instance.origin;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                Color fillColor = GetCellColor(cell);
                if (fillColor.a <= 0f) continue;

                Gizmos.color = fillColor;
                Vector3 center = origin + new Vector3((x + 0.5f) * cellSize, gizmoHeight, (y + 0.5f) * cellSize);
                Vector3 size = new Vector3(cellSize, gizmoHeight, cellSize);
                Gizmos.DrawCube(center, size);

                if (drawGridLines)
                {
                    Gizmos.color = gridLineColor;
                    Gizmos.DrawWireCube(center, size);
                }
            }
        }
    }

    private Color GetCellColor(Vector2Int cell)
    {
        if (!GridManager.Instance.InBounds(cell))
            return new Color(0f, 0f, 0f, 0f);

        var type = GridManager.Instance.GetType(cell);
        switch (type)
        {
            case GridManager.CellType.Road:
                return roadColor;
            case GridManager.CellType.Building:
            {
                int occupantId = GridManager.Instance.GetOccupant(cell);
                if (BuildingPlacementController.Instance != null)
                {
                    var instance = BuildingPlacementController.Instance.GetInstanceById(occupantId);
                    if (instance != null)
                        return GetCategoryColor(instance.data.primaryCategory);
                }
                return new Color(1f, 1f, 1f, 0.35f);
            }
            case GridManager.CellType.Blocked:
                return new Color(0f, 0f, 0f, 0.22f);
            case GridManager.CellType.Empty:
            default:
                return emptyColor;
        }
    }

    private Color GetCategoryColor(BuildingCategory category)
    {
        int index = (int)category;
        if (categoryColors != null && index >= 0 && index < categoryColors.Length)
            return categoryColors[index];
        return new Color(1f, 1f, 1f, 0.35f);
    }
}
