using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Core.Plane;

/// <summary>Light springs (docs/CORRUPTION.md): pools of clear water where her corruption washes away.</summary>
public static class SpringTuning
{
    /// <summary>The chance a level has one.</summary>
    public const float Chance = 0.5f;
    /// <summary>The pool's radius: inside it, her corruption drains at CleansePerSecond of her whole meter a second.</summary>
    public const float Radius = 3f, CleansePerSecond = 0.3f;
    /// <summary>Where it may lie: open water this wide round it, away from the start and the way down.</summary>
    public const float Clearance = 3.2f, FromStart = 22f, FromExit = 14f;
}

/// <summary>Where a level's light springs are: worked out from the map alone (the field keeps them clean, the world uses them).</summary>
public static class LightSprings
{
    public static List<Vector2> Of(LevelMap map)
    {
        var found = new List<Vector2>();
        var rng = new Rng(map.Seed ^ 0x5B21C5UL ^ ((ulong)map.Level << 18) ^ ((ulong)map.Depth << 26) ^ ((ulong)map.Cycle << 34) ^ ((ulong)map.Attempt << 50));
        // As many as the level's template says, else the general chance of one.
        int want = map.Template?.Rules.Springs ?? (rng.NextFloat() < SpringTuning.Chance ? 1 : 0);
        if (want <= 0) return found;
        var candidates = map.Plazas.Select(p => p.Center).ToList();
        foreach (var c in map.Corridors.Where(c => c.Kind is CorridorKind.Main or CorridorKind.Side))
            for (int i = 2; i < c.Points.Count; i += 4) candidates.Add(c.Points[i]);
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = rng.Int(i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }
        var places = map.Pois.Where(p => p.Kind is not (PoiKind.Start or PoiKind.Exit)).ToList();
        foreach (var at in candidates)
        {
            if (Vector2.Distance(at, map.Start.Position) < SpringTuning.FromStart) continue;
            if (Vector2.Distance(at, map.Exit.Position) < map.Exit.Radius + SpringTuning.FromExit) continue;
            if (map.Shaft.Contains(at, 4f) || places.Any(p => Vector2.Distance(at, p.Position) < p.Radius + 5f)) continue;
            bool open = map.IsOpen(at);
            for (int k = 0; k < 8 && open; k++)
            {
                float a = k * MathF.Tau / 8f;
                open = map.IsOpen(at + new Vector2(MathF.Cos(a), MathF.Sin(a)) * SpringTuning.Clearance);
            }
            if (!open) continue;
            if (found.Any(f => Vector2.Distance(f, at) < 25f)) continue;
            found.Add(at);
            if (found.Count >= want) break;
        }
        return found;
    }
}

/// <summary>
/// A light spring in the world: whether she is in it now, and whether its one washing is used. It heals her once a
/// level: from the first time it washes her until she leaves it; then it is spent, and fades away.
/// </summary>
public sealed class LightSpring
{
    public Vector2 Position;
    /// <summary>She is in it, and whether it is healing her this moment (she is not yet wholly clean).</summary>
    public bool Bathing, Healing;
    /// <summary>It has washed her on this visit: when she leaves, it is spent.</summary>
    public bool Used;
    /// <summary>Its washing is gone: it heals no more, and fades away.</summary>
    public bool Spent;
}

public sealed partial class PlaneWorld
{
    public List<LightSpring> Springs { get; } = new();

    void PlaceSprings()
    {
        foreach (var at in LightSprings.Of(Map)) Springs.Add(new LightSpring { Position = at });
    }

    /// <summary>
    /// A light spring washes her corruption away: inside it, her meter drains (a share of the whole each second) until
    /// she is wholly clean. It does so once: the first visit on which it washes her uses it, and when she leaves the
    /// water it is spent (it fades away). A visit while she is already clean does not use it.
    /// </summary>
    void StepSprings()
    {
        var p = Player;
        foreach (var spring in Springs)
        {
            bool inside = !spring.Spent && Vector2.Distance(p.Position, spring.Position) <= SpringTuning.Radius;
            bool was = spring.Bathing;
            spring.Bathing = inside;
            spring.Healing = false;
            if (!inside && spring.Used && !spring.Spent)
            {
                spring.Spent = true;
                Events.Add(new PlaneEvent(PlaneEventType.SpringSpent, spring.Position, Vector2.Zero));
            }
            if (!inside || Defeated) continue;
            float max = Run.MaxHp;
            if (p.Hp >= max) continue;
            spring.Used = true;
            spring.Healing = true;
            if (!was) Events.Add(new PlaneEvent(PlaneEventType.SpringEntered, spring.Position, Vector2.Zero));
            p.Hp = MathF.Min(max, p.Hp + max * SpringTuning.CleansePerSecond * Dt);
            Run.Hp = p.Hp;
            if (p.Hp >= max) Events.Add(new PlaneEvent(PlaneEventType.SpringCleansed, spring.Position, Vector2.Zero));
        }
    }
}
