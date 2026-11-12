using UnityEngine;

/// Baked "can't build here" cells of the gameplay grid (filler buildings, water, mountains...).
/// Written by the Grid Bake window (Tools > Grid > Bake) and read by GridManager at startup, so
/// every device - host and guest alike - gets exactly the same blocked cells with no runtime scanning.
[CreateAssetMenu(menuName = "Hometown/Grid Block Data", fileName = "GridBlockData")]
public class GridBlockData : ScriptableObject
{
    public const byte Free = 0;
    public const byte Filler = 1;       // a pre-placed filler building
    public const byte Terrain = 2;      // too steep / under water
    public const byte Manual = 3;       // painted blocked by hand

    public int width;
    public int height;

    [Tooltip("Final result, one byte per cell (index = y * width + x): 0 free, 1 filler, 2 terrain, 3 manual.")]
    public byte[] cells = new byte[0];

    [Tooltip("Auto-baked part only (fillers + terrain), before hand edits. Editor use.")]
    public byte[] baseCells = new byte[0];

    [Tooltip("Hand edits that survive every re-bake (index = y * width + x): 0 none, 1 force blocked, 2 force free.")]
    public byte[] manual = new byte[0];

    public bool Matches(int w, int h) => w == width && h == height && cells != null && cells.Length == w * h;

    public byte Get(int x, int y) => cells[y * width + x];
}
