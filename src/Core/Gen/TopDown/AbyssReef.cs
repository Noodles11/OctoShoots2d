using System;
using System.Collections.Generic;
using System.Numerics;

namespace OctoShoots.Core.Gen.TopDown;

/// <summary>
/// The adventure's abyss reef (docs/ADVENTURE.md §5): no seabed. The open floor between the walls drops far below her
/// plane — the walls stand up out of dark water and fall away into the deep, as at the way down — except in the caves
/// and the tunnels (and their mouths), which keep their floors. What grew on the dropped floor (flora, kelp, buried
/// coins) goes with it. The swim plane is untouched: what was open water stays open, what was rock stays rock.
/// <para>
/// Here and there along the walls a reef shelf juts out, a rounded ledge of rock just under her plane, its edge falling
/// away into the deep like the rest (<see cref="LevelMap.Shelves"/>): the anemones grow on them.
/// </para>
/// </summary>
public static class AbyssReef
{
    /// <summary>
    /// The abyss's floor (metres below the plane), reached FallReach metres out from the rock: the walls carry on down
    /// below the plane, steepening as they go (depth ∝ distance^FallPower), so their foot is never a hard step.
    /// </summary>
    public const float Depth = -26f, FallReach = 8f, FallPower = 1.3f;
    /// <summary>How far round a cave or tunnel its floor carries on (grid steps), so a mouth is not a sheer drop.</summary>
    const int KeepMargin = 2;
    /// <summary>
    /// A reef shelf: a ledge ShelfRadius across at ShelfDepth, centred ShelfOut metres out from the wall's foot, its rim
    /// falling to the abyss over ShelfFall; one every ShelfSpacing metres along the walls at most, MaxShelves a level,
    /// none within ShelfClear of her start (the reef's fish keep clear of it too).
    /// </summary>
    public const float ShelfRadius = 3f, ShelfDepth = -1.2f, ShelfOut = 1.6f, ShelfFall = 2.5f, ShelfSpacing = 12f, ShelfClear = 20f;
    const int MaxShelves = 40;

    public static LevelMap Apply(LevelMap map)
    {
        int n = LevelMap.Samples;
        var keep = new bool[n * n];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            int i = y * n + x;
            if (!float.IsNaN(map.LidAt(x, y)) || !float.IsNaN(map.RoofAt(x, y))) keep[i] = true;
        }
        foreach (var cave in map.Caves)
            foreach (var (c, r) in cave.Chambers) Mark(keep, c, r + 1f);
        foreach (var corridor in map.Corridors)
            if (corridor.Roofed)
                foreach (var p in corridor.Points) Mark(keep, p, corridor.HalfWidth + 1f);
        // A light spring (only by her luck, here) keeps the seabed it wells up from.
        map.Abyss = true;
        foreach (var spring in OctoShoots.Core.Plane.LightSprings.Of(map)) Mark(keep, spring, OctoShoots.Core.Plane.SpringTuning.Radius + 3f);
        Dilate(keep, KeepMargin);

        // How far each open point is from the nearest rock (above the plane), out to the cliff's foot.
        int reach = (int)MathF.Ceiling(FallReach);
        var rock = new List<(int Dx, int Dy, float D)>();
        for (int dy = -reach; dy <= reach; dy++)
        for (int dx = -reach; dx <= reach; dx++)
        {
            float d = MathF.Sqrt(dx * dx + dy * dy);
            if (d > 0f && d <= FallReach) rock.Add((dx, dy, d));
        }
        rock.Sort((a, b) => a.D.CompareTo(b.D));
        // Each open point's distance from the nearest rock (above the plane), out to the cliff's foot.
        var near = new float[n * n];
        for (int i = 0; i < n * n; i++)
        {
            near[i] = FallReach;
            if (map.Heights[i] > LevelMap.BlockHeight) continue;
            int x = i % n, y = i / n;
            foreach (var (dx, dy, d) in rock)
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= n || ny >= n || map.Heights[ny * n + nx] <= LevelMap.BlockHeight) continue;
                near[i] = d;
                break;
            }
        }
        bool Dropped(int i) => map.Heights[i] <= LevelMap.BlockHeight && !keep[i] && !map.Shaft.Contains(new Vector2(i % n, i / n), 2f);

        // The shelves: at the foot of a wall, out from it, apart, clear of her start and the way down.
        var candidates = new List<int>();
        for (int i = 0; i < n * n; i++)
            if (Dropped(i) && near[i] >= 0.9f && near[i] <= 1.6f) candidates.Add(i);
        var rng = new Rng(map.Seed ^ 0x5E1F5UL ^ ((ulong)map.Level << 22) ^ ((ulong)map.Depth << 30) ^ ((ulong)map.Attempt << 46));
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = rng.Int(i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }
        foreach (int i in candidates)
        {
            if (map.Shelves.Count >= MaxShelves) break;
            var foot = new Vector2(i % n, i / n);
            var uphill = map.Gradient(foot);
            if (uphill.LengthSquared() < 1e-6f) continue;
            var at = foot - Vector2.Normalize(uphill) * ShelfOut;
            if (!map.IsOpen(at) || Vector2.Distance(at, map.Start.Position) < ShelfClear) continue;
            if (Vector2.Distance(at, map.Exit.Position) < map.Exit.Radius + 6f || map.Shaft.Contains(at, 4f)) continue;
            bool apart = true;
            foreach (var other in map.Shelves) apart &= Vector2.Distance(other, at) >= ShelfSpacing;
            if (apart) map.Shelves.Add(at);
        }

        for (int i = 0; i < n * n; i++)
        {
            if (!Dropped(i)) continue;
            var p = new Vector2(i % n, i / n);
            float fall = Depth * MathF.Pow(near[i] / FallReach, FallPower);
            float floor = MathF.Min(map.Heights[i], fall);
            // On a shelf: its ledge (whatever lay there before), its rim easing down into the fall. Always under the
            // plane, so the water stays as open as it was.
            foreach (var shelf in map.Shelves)
            {
                float s = Vector2.Distance(p, shelf);
                if (s >= ShelfRadius + ShelfFall) continue;
                float t = s <= ShelfRadius ? 0f : (s - ShelfRadius) / ShelfFall;
                floor = MathF.Max(floor, ShelfDepth + (fall - ShelfDepth) * t * t * (3f - 2f * t));
            }
            map.Heights[i] = floor;
        }

        bool Kept(Vector2 p)
        {
            int x = Math.Clamp((int)MathF.Round(p.X), 0, n - 1), y = Math.Clamp((int)MathF.Round(p.Y), 0, n - 1);
            return keep[y * n + x];
        }
        map.Decor.RemoveAll(d => d.Height <= LevelMap.BlockHeight && !Kept(d.Position));
        map.Kelp.RemoveAll(k => !Kept(k.Center));
        map.Coins.RemoveAll(c => !Kept(c.Position));
        map.Abyss = true;
        return map;
    }

    static void Mark(bool[] keep, Vector2 c, float r)
    {
        int n = LevelMap.Samples;
        int x0 = Math.Max(0, (int)(c.X - r)), x1 = Math.Min(n - 1, (int)MathF.Ceiling(c.X + r));
        int y0 = Math.Max(0, (int)(c.Y - r)), y1 = Math.Min(n - 1, (int)MathF.Ceiling(c.Y + r));
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
            if (Vector2.DistanceSquared(new Vector2(x, y), c) <= r * r) keep[y * n + x] = true;
    }

    static void Dilate(bool[] keep, int times)
    {
        int n = LevelMap.Samples;
        for (int t = 0; t < times; t++)
        {
            var was = (bool[])keep.Clone();
            for (int y = 1; y < n - 1; y++)
            for (int x = 1; x < n - 1; x++)
                if (!was[y * n + x] && (was[y * n + x - 1] || was[y * n + x + 1] || was[(y - 1) * n + x] || was[(y + 1) * n + x])) keep[y * n + x] = true;
        }
    }
}
