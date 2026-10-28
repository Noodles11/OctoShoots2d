using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Core.Plane;

/// <summary>Light springs (docs/CORRUPTION.md): pools of light where she heals, until their store of healing runs dry.</summary>
public static class SpringTuning
{
    /// <summary>The chance a level has one.</summary>
    public const float Chance = 0.5f;
    /// <summary>The pool's radius: inside it, she heals CleansePerSecond of her whole health a second.</summary>
    public const float Radius = 3f, CleansePerSecond = 0.3f;
    /// <summary>How much healing a spring holds, in health points (more with her luck): it lasts until all of it is given.</summary>
    public const float Pool = 100f;
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
        // As many as the level's template says, else the general chance of one, likelier with her luck. An adventure's
        // abyss reef has one only by her luck.
        float bonus = OctoShoots.Core.Items.LuckTuning.SpringBonus(map.Luck);
        int want = map.Abyss ? (rng.NextFloat() < bonus ? 1 : 0)
            : map.Template?.Rules.Springs ?? (rng.NextFloat() < MathF.Min(SpringTuning.Chance + bonus, 0.95f) ? 1 : 0);
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
/// A light spring in the world: whether she is in it now, and how much healing it still holds. She may come back to it
/// as often as she likes; once it has given all of it, it is spent, and fades away.
/// </summary>
public sealed class LightSpring
{
    public Vector2 Position;
    /// <summary>She is in it, and whether it is healing her this moment (she is not yet whole).</summary>
    public bool Bathing, Healing;
    /// <summary>The healing it still holds, and all it held (health points).</summary>
    public float Pool, PoolMax;
    /// <summary>Its healing is all given: it heals no more, and fades away; the creatures of ink may cross it again.</summary>
    public bool Spent;
}

public sealed partial class PlaneWorld
{
    public List<LightSpring> Springs { get; } = new();

    void PlaceSprings()
    {
        float pool = SpringTuning.Pool * (1f + OctoShoots.Core.Items.LuckTuning.SpringPool(Map.Luck));
        foreach (var at in LightSprings.Of(Map)) Springs.Add(new LightSpring { Position = at, Pool = pool, PoolMax = pool });
    }

    /// <summary>
    /// A light spring heals her: inside it, while she is hurt, it gives her a share of her whole health each second out
    /// of its store. She may leave and come back; when the store runs dry it is spent and fades away. While it shines,
    /// it is too bright for the creatures of ink: none may enter it (WardSprings).
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
            if (!inside || Defeated) continue;
            float max = Run.MaxHp;
            if (p.Hp >= max) continue;
            spring.Healing = true;
            if (!was) Events.Add(new PlaneEvent(PlaneEventType.SpringEntered, spring.Position, Vector2.Zero));
            float heal = MathF.Min(MathF.Min(max * SpringTuning.CleansePerSecond * Dt, max - p.Hp), spring.Pool);
            p.Hp += heal;
            Run.Hp = p.Hp;
            spring.Pool -= heal;
            if (p.Hp >= max) Events.Add(new PlaneEvent(PlaneEventType.SpringCleansed, spring.Position, Vector2.Zero));
            if (spring.Pool <= 1e-3f)
            {
                spring.Pool = 0f;
                spring.Spent = true;
                spring.Bathing = spring.Healing = false;
                Events.Add(new PlaneEvent(PlaneEventType.SpringSpent, spring.Position, Vector2.Zero));
            }
        }
        WardSprings();
    }

    /// <summary>
    /// A shining spring is too bright for the creatures of ink: any that swims into it is put back at its edge, its way
    /// in stopped; one clinging to her is thrown off as she enters.
    /// </summary>
    void WardSprings()
    {
        foreach (var spring in Springs)
        {
            if (spring.Spent) continue;
            foreach (var m in Murklings)
            {
                if (!m.Alive) continue;
                if (m.Clinging)
                {
                    if (Vector2.Distance(m.Position, spring.Position) > SpringTuning.Radius) continue;
                    m.Clinging = false;
                    m.Shaken = BlightTuning.ShakeSeconds;
                }
                KeepOut(spring.Position, ref m.Position, ref m.Velocity, m.Body);
            }
            foreach (var w in Inkwardens)
                if (w.Alive) KeepOut(spring.Position, ref w.Position, ref w.Velocity, WardenTuning.Radius);
            foreach (var mob in Mobs)
                if (mob.Alive) KeepOut(spring.Position, ref mob.Position, ref mob.Velocity, mob.Radius);
        }
    }

    /// <summary>Puts a creature back at a spring's edge (its body just outside), stopping its way in.</summary>
    static void KeepOut(Vector2 centre, ref Vector2 at, ref Vector2 velocity, float body)
    {
        float reach = SpringTuning.Radius + body;
        var from = at - centre;
        float d = from.Length();
        if (d >= reach) return;
        var away = d > 1e-4f ? from / d : new Vector2(1f, 0f);
        at = centre + away * reach;
        float inward = Vector2.Dot(velocity, away);
        if (inward < 0f) velocity -= away * inward;
    }
}
