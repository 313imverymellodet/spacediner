using System.Collections.Generic;
using UnityEngine;

// Grid A* over the diner floor, rasterised from the real colliders. Rebuilt whenever something is built.
public static class NavGrid
{
    const float MinX = -9.5f, MaxX = 9.5f, MinZ = -11.5f, MaxZ = 9.3f, Cell = 0.4f, Clear = 0.3f;
    public const int IgnoreLayer = 2; // "Ignore Raycast": movers live here so they never block the grid
    static int W, H;
    static bool[] blocked;
    static bool dirty = true;

    public static void MarkDirty() => dirty = true;

    static void Build()
    {
        dirty = false;
        Physics.SyncTransforms();
        W = Mathf.CeilToInt((MaxX - MinX) / Cell);
        H = Mathf.CeilToInt((MaxZ - MinZ) / Cell);
        blocked = new bool[W * H];
        int mask = ~(1 << IgnoreLayer);
        var half = new Vector3(Cell * 0.5f + Clear, 0.45f, Cell * 0.5f + Clear);
        for (int z = 0; z < H; z++)
            for (int x = 0; x < W; x++)
                blocked[z * W + x] = Physics.CheckBox(Center(x, z) + Vector3.up * 0.6f, half, Quaternion.identity, mask, QueryTriggerInteraction.Ignore);
    }

    static Vector3 Center(int x, int z) => new Vector3(MinX + (x + 0.5f) * Cell, 0, MinZ + (z + 0.5f) * Cell);
    static Vector2Int ToCell(Vector3 p) => new Vector2Int(Mathf.Clamp(Mathf.FloorToInt((p.x - MinX) / Cell), 0, W - 1), Mathf.Clamp(Mathf.FloorToInt((p.z - MinZ) / Cell), 0, H - 1));
    static bool Free(int x, int z) => x >= 0 && z >= 0 && x < W && z < H && !blocked[z * W + x];

    static Vector2Int NearestFree(Vector2Int c)
    {
        if (Free(c.x, c.y)) return c;
        for (int r = 1; r < 12; r++)
            for (int dz = -r; dz <= r; dz++)
                for (int dx = -r; dx <= r; dx++)
                    if (Mathf.Abs(dx) == r || Mathf.Abs(dz) == r)
                        if (Free(c.x + dx, c.y + dz)) return new Vector2Int(c.x + dx, c.y + dz);
        return c;
    }

    // Straight-line walkability between two cells (for path smoothing).
    static bool Line(Vector2Int a, Vector2Int b)
    {
        int n = Mathf.Max(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y)) * 2;
        for (int i = 0; i <= n; i++)
        {
            float t = n == 0 ? 0 : i / (float)n;
            if (!Free(Mathf.RoundToInt(Mathf.Lerp(a.x, b.x, t)), Mathf.RoundToInt(Mathf.Lerp(a.y, b.y, t)))) return false;
        }
        return true;
    }

    static readonly int[] DX = { 1, -1, 0, 0, 1, 1, -1, -1 }, DZ = { 0, 0, 1, -1, 1, -1, 1, -1 };

    public static List<Vector3> Path(Vector3 from, Vector3 to)
    {
        if (dirty || blocked == null) Build();
        var s = NearestFree(ToCell(from)); var g = NearestFree(ToCell(to));
        var result = new List<Vector3>();
        if (Line(s, g)) { result.Add(new Vector3(to.x, 0, to.z)); return result; }

        int n = W * H;
        var cost = new float[n]; var prev = new int[n]; var closed = new bool[n];
        for (int i = 0; i < n; i++) { cost[i] = float.MaxValue; prev[i] = -1; }
        int si = s.y * W + s.x, gi = g.y * W + g.x;
        cost[si] = 0;
        var open = new List<int> { si };
        while (open.Count > 0)
        {
            int bi = 0; float bf = float.MaxValue;
            for (int i = 0; i < open.Count; i++)
            {
                int c = open[i];
                float f = cost[c] + Vector2.Distance(new Vector2(c % W, c / W), g);
                if (f < bf) { bf = f; bi = i; }
            }
            int cur = open[bi]; open.RemoveAt(bi);
            if (cur == gi) break;
            if (closed[cur]) continue;
            closed[cur] = true;
            int cx = cur % W, cz = cur / W;
            for (int d = 0; d < 8; d++)
            {
                int nx = cx + DX[d], nz = cz + DZ[d];
                if (!Free(nx, nz)) continue;
                if (d >= 4 && (!Free(cx + DX[d], cz) || !Free(cx, cz + DZ[d]))) continue; // no corner cutting
                int ni = nz * W + nx;
                float nc = cost[cur] + (d < 4 ? 1f : 1.414f);
                if (nc < cost[ni]) { cost[ni] = nc; prev[ni] = cur; open.Add(ni); }
            }
        }
        if (prev[gi] < 0) { result.Add(new Vector3(to.x, 0, to.z)); return result; }

        var cells = new List<Vector2Int>();
        for (int c = gi; c >= 0; c = prev[c]) cells.Add(new Vector2Int(c % W, c / W));
        cells.Reverse();
        // string-pull: keep only the corners we actually need
        var anchor = cells[0];
        for (int i = 1; i < cells.Count - 1; i++)
            if (!Line(anchor, cells[i + 1])) { anchor = cells[i]; result.Add(Center(anchor.x, anchor.y)); }
        result.Add(new Vector3(to.x, 0, to.z));
        return result;
    }
}
