using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using OctoShoots.Core.Creatures;

namespace OctoShoots.Core.Gen.TopDown;

// Map coordinates: X runs east, Y runs south (world X and world Z). North (−Y) is up on screen.

/// <summary>A named place on the level (DESIGN-TOPDOWN §4.1 stage 1).</summary>
/// <summary>Ambush: an open clearing where, the first time Clementine enters, mobs spawn around her and attack.</summary>
public enum PoiKind { Start, ItemSpawn, Rift, Shop, Secret, CurseDen, TreasureCave, Ambush }

public sealed class Poi
{
    public PoiKind Kind;
    public Vector2 Position;
    /// <summary>Radius of the open ground (or cave) the place occupies.</summary>
    public float Radius;
    /// <summary>The one optional POI allowed 20–35 m from the start.</summary>
    public bool Early;
    /// <summary>Index into <see cref="LevelMap.Caves"/> for cave-hosted places, else −1.</summary>
    public int Cave = -1;

    /// <summary>Special rooms live in caves (DESIGN-TOPDOWN §4.2).</summary>
    public static bool IsCaveHosted(PoiKind kind) => kind is PoiKind.Shop or PoiKind.Secret or PoiKind.CurseDen or PoiKind.TreasureCave;
}

/// <summary>Main: a route canyon start→rift. Spur: to a place. Pass: a narrow cut. Side: a labyrinth canyon (dead end or loop).</summary>
public enum CorridorKind { Main, Spur, Pass, Side }

/// <summary>A swimmable channel pinned at h = 0: a polyline with a width.</summary>
public sealed class Corridor
{
    public CorridorKind Kind;
    public List<Vector2> Points = new();
    public float Width;
    /// <summary>For spurs: the POI it connects.</summary>
    public int Poi = -1;

    public float HalfWidth => Width * 0.5f;

    public float Length
    {
        get
        {
            float l = 0f;
            for (int i = 1; i < Points.Count; i++) l += Vector2.Distance(Points[i - 1], Points[i]);
            return l;
        }
    }

    /// <summary>Distance from p to the centre line.</summary>
    public float DistanceToCentre(Vector2 p)
    {
        float best = float.MaxValue;
        for (int i = 1; i < Points.Count; i++) best = MathF.Min(best, Geo.SegmentDistance(p, Points[i - 1], Points[i]));
        return best;
    }

    public bool Contains(Vector2 p) => DistanceToCentre(p) <= HalfWidth;
}

/// <summary>An open crossing where two corridors meet (~14 m).</summary>
public readonly record struct Plaza(Vector2 Center, float Radius);


/// <summary>A deep: a canyon cut 4–8 m below 0, 8–14 m wide.</summary>
public sealed class Trench
{
    public List<Vector2> Points = new();
    public float Width;
    public float Depth;

    public float DistanceToCentre(Vector2 p)
    {
        float best = float.MaxValue;
        for (int i = 1; i < Points.Count; i++) best = MathF.Min(best, Geo.SegmentDistance(p, Points[i - 1], Points[i]));
        return best;
    }
}

public enum CaveKind { Pocket, MultiChamber }

/// <summary>A cave hollowed out inside a mountain: floor under the swim level, roofed by the mountain (<see cref="LevelMap.Lid"/>), opening through its mouth.</summary>
public sealed class CaveSite
{
    public CaveKind Kind;
    public int Poi;
    public List<(Vector2 Center, float Radius)> Chambers = new();
    /// <summary>Where the spur enters the cave.</summary>
    public Vector2 Mouth;
    /// <summary>Unit direction from the cave out through its mouth.</summary>
    public Vector2 Facing;
}

public enum CanopyKind { Arch, CaveRoof, Overhang }

/// <summary>
/// Something solid overhead, on the canopy layer above the swim band (DESIGN-TOPDOWN §3): it fades while
/// Clementine is underneath. Footprint is a disc (Radius > 0) or an oriented rectangle.
/// </summary>
public sealed class Canopy
{
    public CanopyKind Kind;
    public Vector2 Center;
    /// <summary>Unit direction of the rectangle's length (for an arch: from one footing to the other).</summary>
    public Vector2 Axis = Vector2.UnitX;
    public float HalfLength;
    public float HalfWidth;
    /// <summary>Disc footprint radius, or 0 for a rectangle.</summary>
    public float Radius;
    public float Bottom;
    public float Top;
    /// <summary>Arches: the corridor they span.</summary>
    public int Corridor = -1;

    public Vector2 EndA => Center - Axis * HalfLength;
    public Vector2 EndB => Center + Axis * HalfLength;

    public bool Contains(Vector2 p)
    {
        Vector2 d = p - Center;
        if (Radius > 0f) return d.LengthSquared() <= Radius * Radius;
        float along = Vector2.Dot(d, Axis);
        float across = Vector2.Dot(d, new Vector2(-Axis.Y, Axis.X));
        return MathF.Abs(along) <= HalfLength && MathF.Abs(across) <= HalfWidth;
    }
}

/// <summary>Weak rock plugging a secret cave's mouth: blocks swimming until an ink bomb breaks it.</summary>
public readonly record struct WeakRock(Vector2 Center, float Radius, int Cave);

public enum DecorKind
{
    SandChannel, GrassMeadow, Boulder, Bommie,
    SeaRod, SeaFan, TubeSponge,
    CliffCoral, EncrustingCoral, CrownGarden,
    TrenchSponge, BiolumAccent,
}

/// <summary>Cosmetic reef dressing, chosen by height band (DESIGN-TOPDOWN §4.1 stage 5). Never affects play.</summary>
public readonly record struct DecorSpot(DecorKind Kind, Vector2 Position, float Height, float Scale, float Yaw);

public readonly record struct SealedPocket(Vector2 Position, float Radius);

public readonly record struct BuriedCoins(Vector2 Position, int Amount);

public enum SpawnRole { Den, Ambush, Patrol, Guardian }

/// <summary>DESIGN-2D §12.1 movement classes.</summary>
public enum MoveClass { Swimmer, Walker, Clinger, Burrower }

/// <summary>One row of the level's spawn table (DESIGN-TOPDOWN §4.1 stage 6).</summary>
public sealed class SpawnEntry
{
    public SpawnRole Role;
    public EnemyKind Kind;
    public MoveClass Class;
    public int Count;
    public Vector2 Position;
    /// <summary>Patrols: the corridor stretch they loop along.</summary>
    public List<Vector2>? Path;
    /// <summary>Guardians and nests: the POI they belong to.</summary>
    public int Poi = -1;
    /// <summary>Secrets are trap-heavy (mimics).</summary>
    public bool Trap;
}

/// <summary>
/// One generated level (DESIGN-TOPDOWN §4): a 150×150 m heightfield on a 1 m grid plus everything the generator
/// decided. Produced only by <see cref="TopDownGenerator"/>; the game reads it and never generates or scatters.
/// </summary>
public sealed class LevelMap
{
    public const int Size = 150;
    public const int Samples = Size + 1;

    /// <summary>Where the bell is drawn: floating just above the swim level (0), which every height is measured from.</summary>
    public const float SwimBand = 0.7f;

    /// <summary>The swim level: terrain above it blocks; anything at or below it (seabed, deeps) is open water to float over.</summary>
    public const float BlockHeight = 0f;

    /// <summary>Width of the impassable reef rim around the square.</summary>
    public const float RimWidth = 8f;

    public ulong Seed;
    public int Depth;
    public int Reef;
    public int Attempt;

    /// <summary>Heights in metres at the 151×151 grid points, row-major (index = y * Samples + x).</summary>
    public float[] Heights = new float[Samples * Samples];

    /// <summary>
    /// The mountain over each cave as it stood before the chambers were hollowed out (NaN elsewhere): the cave's roof,
    /// drawn as intact rock until Clementine swims inside.
    /// </summary>
    public float[] Lid = NewLid();

    static float[] NewLid()
    {
        var lid = new float[Samples * Samples];
        Array.Fill(lid, float.NaN);
        return lid;
    }

    /// <summary>The cave roof's height at a grid point, or NaN where no cave is.</summary>
    public float LidAt(int x, int y) => Lid[y * Samples + x];

    public List<Poi> Pois = new();
    public List<Corridor> Corridors = new();
    public List<Plaza> Plazas = new();
    public List<Trench> Trenches = new();
    public List<CaveSite> Caves = new();
    public List<Canopy> Canopies = new();
    public List<WeakRock> WeakRocks = new();
    public List<DecorSpot> Decor = new();
    public List<SealedPocket> Pockets = new();
    public List<BuriedCoins> Coins = new();
    public List<SpawnEntry> Spawns = new();

    public Poi Start => Pois.Find(p => p.Kind == PoiKind.Start)!;
    public Poi Rift => Pois.Find(p => p.Kind == PoiKind.Rift)!;

    public float this[int x, int y]
    {
        get => Heights[y * Samples + x];
        set => Heights[y * Samples + x] = value;
    }

    /// <summary>Bilinear height at a map position (clamped to the square).</summary>
    public float[] CopyHeights() => (float[])Heights.Clone();

    public void SetHeights(float[] heights) => Array.Copy(heights, Heights, Heights.Length);

    public float HeightAt(Vector2 p)
    {
        float x = Math.Clamp(p.X, 0f, Size - 0.0001f), y = Math.Clamp(p.Y, 0f, Size - 0.0001f);
        int ix = (int)x, iy = (int)y;
        float tx = x - ix, ty = y - iy;
        float a = MathUtil.Lerp(this[ix, iy], this[ix + 1, iy], tx);
        float b = MathUtil.Lerp(this[ix, iy + 1], this[ix + 1, iy + 1], tx);
        return MathUtil.Lerp(a, b, ty);
    }

    /// <summary>Uphill direction of the terrain (not normalised).</summary>
    public Vector2 Gradient(Vector2 p)
    {
        const float e = 0.5f;
        return new Vector2(
            HeightAt(p + new Vector2(e, 0f)) - HeightAt(p - new Vector2(e, 0f)),
            HeightAt(p + new Vector2(0f, e)) - HeightAt(p - new Vector2(0f, e))) / (2f * e);
    }

    /// <summary>True when the swim band is open at p (terrain low enough; weak rock not counted).</summary>
    public bool IsOpen(Vector2 p) => p.X >= 0f && p.Y >= 0f && p.X <= Size && p.Y <= Size && HeightAt(p) <= BlockHeight;

    /// <summary>Index of the canopy overhead at p, or −1.</summary>
    public int CanopyAt(Vector2 p)
    {
        for (int i = 0; i < Canopies.Count; i++)
            if (Canopies[i].Contains(p)) return i;
        return -1;
    }

    public bool InsideAnyCorridor(Vector2 p)
    {
        foreach (var c in Corridors)
            if (c.Contains(p)) return true;
        return false;
    }

    /// <summary>Every byte of the map, for determinism checks.</summary>
    public byte[] ToBytes()
    {
        using var stream = new MemoryStream();
        using var w = new BinaryWriter(stream);
        void V(Vector2 v)
        {
            w.Write(v.X);
            w.Write(v.Y);
        }
        w.Write(1); // format version
        w.Write(Seed);
        w.Write(Depth);
        w.Write(Reef);
        w.Write(Attempt);
        foreach (float h in Heights) w.Write(h);
        foreach (float h in Lid) w.Write(h);
        w.Write(Pois.Count);
        foreach (var p in Pois)
        {
            w.Write((int)p.Kind);
            V(p.Position);
            w.Write(p.Radius);
            w.Write(p.Early);
            w.Write(p.Cave);
        }
        w.Write(Corridors.Count);
        foreach (var c in Corridors)
        {
            w.Write((int)c.Kind);
            w.Write(c.Width);
            w.Write(c.Poi);
            w.Write(c.Points.Count);
            foreach (var p in c.Points) V(p);
        }
        w.Write(Plazas.Count);
        foreach (var p in Plazas)
        {
            V(p.Center);
            w.Write(p.Radius);
        }
        w.Write(Trenches.Count);
        foreach (var t in Trenches)
        {
            w.Write(t.Width);
            w.Write(t.Depth);
            w.Write(t.Points.Count);
            foreach (var p in t.Points) V(p);
        }
        w.Write(Caves.Count);
        foreach (var c in Caves)
        {
            w.Write((int)c.Kind);
            w.Write(c.Poi);
            V(c.Mouth);
            V(c.Facing);
            w.Write(c.Chambers.Count);
            foreach (var (center, radius) in c.Chambers)
            {
                V(center);
                w.Write(radius);
            }
        }
        w.Write(Canopies.Count);
        foreach (var c in Canopies)
        {
            w.Write((int)c.Kind);
            V(c.Center);
            V(c.Axis);
            w.Write(c.HalfLength);
            w.Write(c.HalfWidth);
            w.Write(c.Radius);
            w.Write(c.Bottom);
            w.Write(c.Top);
            w.Write(c.Corridor);
        }
        w.Write(WeakRocks.Count);
        foreach (var r in WeakRocks)
        {
            V(r.Center);
            w.Write(r.Radius);
            w.Write(r.Cave);
        }
        w.Write(Decor.Count);
        foreach (var d in Decor)
        {
            w.Write((int)d.Kind);
            V(d.Position);
            w.Write(d.Height);
            w.Write(d.Scale);
            w.Write(d.Yaw);
        }
        w.Write(Pockets.Count);
        foreach (var p in Pockets)
        {
            V(p.Position);
            w.Write(p.Radius);
        }
        w.Write(Coins.Count);
        foreach (var c in Coins)
        {
            V(c.Position);
            w.Write(c.Amount);
        }
        w.Write(Spawns.Count);
        foreach (var s in Spawns)
        {
            w.Write((int)s.Role);
            w.Write((int)s.Kind);
            w.Write((int)s.Class);
            w.Write(s.Count);
            V(s.Position);
            w.Write(s.Poi);
            w.Write(s.Trap);
            w.Write(s.Path?.Count ?? 0);
            if (s.Path is not null) foreach (var p in s.Path) V(p);
        }
        w.Flush();
        return stream.ToArray();
    }
}

/// <summary>Small 2D geometry helpers for the generator.</summary>
public static class Geo
{
    public static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float len2 = ab.LengthSquared();
        float t = len2 > 1e-9f ? Math.Clamp(Vector2.Dot(p - a, ab) / len2, 0f, 1f) : 0f;
        return Vector2.Distance(p, a + ab * t);
    }

    /// <summary>Intersection point of segments ab and cd, if they cross.</summary>
    public static bool SegmentIntersection(Vector2 a, Vector2 b, Vector2 c, Vector2 d, out Vector2 at)
    {
        at = default;
        Vector2 r = b - a, s = d - c;
        float denom = r.X * s.Y - r.Y * s.X;
        if (MathF.Abs(denom) < 1e-9f) return false;
        Vector2 ca = c - a;
        float t = (ca.X * s.Y - ca.Y * s.X) / denom;
        float u = (ca.X * r.Y - ca.Y * r.X) / denom;
        if (t < 0f || t > 1f || u < 0f || u > 1f) return false;
        at = a + r * t;
        return true;
    }

    public static Vector2 Perp(Vector2 v) => new(-v.Y, v.X);

    public static Vector2 Normalize(Vector2 v, Vector2 fallback)
    {
        float l = v.Length();
        return l > 1e-6f ? v / l : fallback;
    }
}
