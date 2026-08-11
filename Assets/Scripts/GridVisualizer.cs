using UnityEngine;

/// Renders the grid as a checkerboard on a single quad - NOT one GameObject per cell.
/// This scales to 1000x1000 with zero extra draw calls or per-tile overhead, unlike
/// instantiating a tile prefab per cell (which gets expensive fast on mobile).
///
/// How it works: builds a small tileable texture (2x2 pixels: light/dark) and stretches it
/// across the whole ground quad using point (no filtering) sampling + texture Repeat wrap mode,
/// so each texel-pair maps exactly to alternating grid cells. Cheap, sharp, no shader writing needed.
[RequireComponent(typeof(MeshRenderer))]
public class GridVisualizer : MonoBehaviour
{
    [Header("Colors")]
    public Color lightColor = new Color(0.85f, 0.85f, 0.85f);
    public Color darkColor = new Color(0.75f, 0.75f, 0.75f);

    [Header("Optional: tint locked/occupied cells")]
    public bool showOccupancyOverlay = false; // turn on later once you want debug overlays
    public Color lockedColor = new Color(0.6f, 0.4f, 0.2f, 0.5f);   // trees/rocks
    public Color buildingColor = new Color(0.2f, 0.6f, 1f, 0.3f);  // has a building

    private MeshRenderer meshRenderer;
    private Texture2D checkerTexture;

    void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        BuildCheckerTexture();
        FitQuadToGrid();
    }

    private void BuildCheckerTexture()
    {
        // 2x2 texture: [0,0]=dark [1,0]=light [0,1]=light [1,1]=dark - classic checker.
        checkerTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        checkerTexture.filterMode = FilterMode.Point; // crisp edges, no blur between cells
        checkerTexture.wrapMode = TextureWrapMode.Repeat;

        checkerTexture.SetPixel(0, 0, darkColor);
        checkerTexture.SetPixel(1, 0, lightColor);
        checkerTexture.SetPixel(0, 1, lightColor);
        checkerTexture.SetPixel(1, 1, darkColor);
        checkerTexture.Apply();

        var mat = meshRenderer.material; // instance, safe to modify without affecting the asset
        mat.mainTexture = checkerTexture;

        int w = GridManager.Instance.width;
        int h = GridManager.Instance.height;
        // Tiling = half the grid dimensions, since our texture already encodes a 2x2 checker
        // pattern per repeat - so each texture repeat covers exactly 2 grid cells per axis.
        mat.mainTextureScale = new Vector2(w / 2f, h / 2f);
    }

    private void FitQuadToGrid()
    {
        var grid = GridManager.Instance;
        float worldW = grid.width * grid.cellSize;
        float worldH = grid.height * grid.cellSize;

        // Assumes a default Unity Quad (1x1 at scale 1) or Plane (10x10 at scale 1) - adjust
        // the divisor below to 10f if you're using Unity's built-in Plane primitive instead of Quad.
        transform.localScale = new Vector3(worldW, worldH, 1f);
        transform.position = grid.origin + new Vector3(worldW / 2f, 0.01f, worldH / 2f); // tiny Y offset avoids z-fighting with ground
        transform.rotation = Quaternion.Euler(90f, 0f, 0f); // lay flat if using a Quad (skip this line if using a Plane)
    }

    /// Call this if you enable showOccupancyOverlay and want to refresh it after placing/demolishing.
    /// NOTE: this is a simple full-texture rebuild for clarity - fine for occasional refreshes,
    /// but don't call it every frame at large grid sizes.
    public void RefreshOccupancyOverlay()
    {
        if (!showOccupancyOverlay) return;
        // Left as an extension point: build a second texture sized to grid.width x grid.height
        // (1 pixel per cell) with FilterMode.Point, color each pixel by GridManager.GetType(cell),
        // and set it as a second material property (e.g. a blend texture in a custom shader) or
        // as an overlay quad rendered slightly above this one. Ask me for this when you're ready -
        // it's a natural next step once base placement/demolish is solid.
    }
}