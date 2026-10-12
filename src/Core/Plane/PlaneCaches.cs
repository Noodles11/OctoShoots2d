using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Core.Plane;

/// <summary>
/// Buried caches (docs/CORRUPTION.md): with the corruption on, loot lies under the ink instead of in pots. Each shows
/// as a bump in the corrupted floor; cleansing the ground over it (her light, a pop) uncovers it, and what was buried
/// floats up: shells or a light orb.
/// </summary>
public static class CacheTuning
{
    /// <summary>How many per level (inclusive), how far apart, and how far from her start.</summary>
    public const int Min = 8, Max = 12;
    public const float Spacing = 9f, StartClear = 14f;
    /// <summary>It lies under ink at least this thick, and is uncovered once the ground over it (Reach) is cleansed.</summary>
    public const float Buried = 0.55f, Reach = 0.7f;
    /// <summary>What it holds: a light orb (OrbChance), else 2–4 shells.</summary>
    public const float OrbChance = 0.45f;
    public const int ShellsMin = 2, ShellsMax = 4;
}

/// <summary>Loot buried under the corruption: a bump in the ink until the ground over it is cleansed.</summary>
public sealed class BuriedCache
{
    public Vector2 Position;
    public float Seed;
    public bool Revealed;
}

public sealed partial class PlaneWorld
{
    public List<BuriedCache> Caches { get; } = new();
    Rng _cacheRng = null!;

    /// <summary>Caches buried under thick ink, apart, away from her start, the places and the arena.</summary>
    void PlaceCaches(CorruptionField field)
    {
        _cacheRng = new Rng(Map.Seed ^ 0xCAC4EUL ^ ((ulong)Map.Level << 20) ^ ((ulong)Map.Depth << 30) ^ ((ulong)Map.Attempt << 46));
        int want = CacheTuning.Min + _cacheRng.Int(CacheTuning.Max - CacheTuning.Min + 1);
        for (int tries = 0; tries < 2000 && Caches.Count < want; tries++)
        {
            int cell = _cacheRng.Int(field.Density.Length);
            if (field.Density[cell] < CacheTuning.Buried) continue;
            var at = CorruptionField.CellCentre(cell % CorruptionField.N, cell / CorruptionField.N);
            if (!Clear(at, 0.6f) || Vector2.Distance(at, Map.Start.Position) < CacheTuning.StartClear) continue;
            if (Map.HasBoss && Vector2.Distance(at, ArenaCenter) < ArenaRadius + 2f) continue;
            if (Map.Shaft.Contains(at, 2f)) continue;
            if (Map.Pois.Any(p => p.Kind != PoiKind.Start && Vector2.Distance(at, p.Position) < p.Radius + 3f)) continue;
            if (Caches.Any(c => Vector2.Distance(c.Position, at) < CacheTuning.Spacing)) continue;
            Caches.Add(new BuriedCache { Position = at, Seed = _cacheRng.Range(0f, 100f) });
        }
    }

    /// <summary>A cache whose ground is cleansed is uncovered: its loot floats up.</summary>
    void StepCaches(CorruptionField field)
    {
        if (Tick % 6 != 0) return;
        foreach (var c in Caches)
        {
            if (c.Revealed || Covered(field, c.Position)) continue;
            c.Revealed = true;
            bool orb = _cacheRng.NextFloat() < CacheTuning.OrbChance;
            if (orb) Hearts.Add(new PlaneHeart { Position = c.Position, Velocity = Fling(0.8f, 1.6f) });
            else
            {
                int count = CacheTuning.ShellsMin + _cacheRng.Int(CacheTuning.ShellsMax - CacheTuning.ShellsMin + 1);
                for (int i = 0; i < count; i++) Shells.Add(new PlaneShell { Position = c.Position, Velocity = Fling(1.5f, 3f) });
            }
            Events.Add(new PlaneEvent(PlaneEventType.CacheRevealed, c.Position, Vector2.Zero, orb ? 1f : 0f));
        }
    }

    /// <summary>Whether ink still lies over a point: at its middle or a little way round it.</summary>
    static bool Covered(CorruptionField field, Vector2 at)
    {
        if (field.At(at) >= CorruptionTuning.Clean) return true;
        for (int i = 0; i < 4; i++)
        {
            float a = i * MathF.PI * 0.5f;
            if (field.At(at + new Vector2(MathF.Cos(a), MathF.Sin(a)) * CacheTuning.Reach) >= CorruptionTuning.Clean * 2f) return true;
        }
        return false;
    }
}
