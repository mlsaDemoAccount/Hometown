using System.Collections.Generic;
using UnityEngine;

/// Reduces a filled area of cells (e.g. a road mesh wider than 1 grid cell) down to a
/// single-cell-wide centerline/skeleton, using the Zhang-Suen thinning algorithm - a standard
/// image-processing technique for exactly this problem. Without this, a road wider than 1 cell
/// makes almost every interior cell look like a 4-way intersection (it has road neighbors on
/// all sides), which is not what "intersection" should mean for traffic/pedestrian purposes.
public static class GridSkeletonizer
{
    // Clockwise from North: P2, P3, P4, P5, P6, P7, P8, P9 (standard Zhang-Suen neighbor order).
    private static readonly Vector2Int[] NeighborOffsets =
    {
        new Vector2Int(0, 1),   // P2 - N
        new Vector2Int(1, 1),   // P3 - NE
        new Vector2Int(1, 0),   // P4 - E
        new Vector2Int(1, -1),  // P5 - SE
        new Vector2Int(0, -1),  // P6 - S
        new Vector2Int(-1, -1), // P7 - SW
        new Vector2Int(-1, 0),  // P8 - W
        new Vector2Int(-1, 1),  // P9 - NW
    };

    public static HashSet<Vector2Int> Thin(HashSet<Vector2Int> cells)
    {
        var img = new HashSet<Vector2Int>(cells);
        bool changed;

        do
        {
            bool removedStep1 = ThinPass(img, isStepOne: true);
            bool removedStep2 = ThinPass(img, isStepOne: false);
            changed = removedStep1 || removedStep2;
        } while (changed);

        return img;
    }

    private static bool ThinPass(HashSet<Vector2Int> img, bool isStepOne)
    {
        var toRemove = new List<Vector2Int>();

        foreach (var cell in img)
        {
            bool[] p = new bool[8];
            for (int i = 0; i < 8; i++) p[i] = img.Contains(cell + NeighborOffsets[i]);

            int B = 0;
            foreach (var v in p) if (v) B++;
            if (B < 2 || B > 6) continue; // condition (a): must have 2-6 filled neighbors

            int A = 0; // number of 0->1 transitions going around the 8 neighbors in order
            for (int i = 0; i < 8; i++)
                if (!p[i] && p[(i + 1) % 8]) A++;
            if (A != 1) continue; // condition (b): exactly one transition = safe to remove without breaking connectivity

            // p[0]=P2(N) p[1]=P3(NE) p[2]=P4(E) p[3]=P5(SE) p[4]=P6(S) p[5]=P7(SW) p[6]=P8(W) p[7]=P9(NW)
            bool conditionsOK = isStepOne
                ? (!(p[0] && p[2] && p[4]) && !(p[2] && p[4] && p[6]))   // P2*P4*P6==0 and P4*P6*P8==0
                : (!(p[0] && p[2] && p[6]) && !(p[0] && p[4] && p[6]));  // P2*P4*P8==0 and P2*P6*P8==0

            if (conditionsOK) toRemove.Add(cell);
        }

        foreach (var c in toRemove) img.Remove(c);
        return toRemove.Count > 0;
    }
}
