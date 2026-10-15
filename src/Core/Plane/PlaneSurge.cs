using System;
using System.Collections.Generic;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;

namespace OctoShoots.Core.Plane;

/// <summary>
/// Firefly Squid (flag "surge", DESIGN-TOPDOWN §combat): while fire is held she pours out a continuous, narrow surge of
/// light instead of throwing shots. It reaches where she aims (as her shots do) and stops on rock, a pot or the first
/// creature it meets. It strikes what it touches in pulses, and its tip and every impact clear a little of the ground's
/// corruption. Her other pearls shape it:
/// <list type="bullet">
/// <item>shot size (and the pearls that change it) widens it; Starfish Arm widens it toward its tip, and hits harder there;</item>
/// <item>every extra shot of a volley (Triple Tentacle, Hammerhead…) opens it into a cone with no gaps, as wide as the
/// volley's fan (Hammerhead's cone wider still); each foe in it takes a little more;</item>
/// <item>Double Helix twists it into two strands spiralling round its line;</item>
/// <item>Swordfish Bill: it pierces every creature in its path; Mirror Scale: it reflects off rock; Anglerfish Lure:
/// it bends toward the nearest foe;</item>
/// <item>Mitosis: where it first strikes something it forks into two short half-strength branches;</item>
/// <item>Starburst Gland: it bursts into light at its tip and wherever it strikes, a few times a second;</item>
/// <item>Pearl Diver: held, it focuses over ChargeSeconds, narrower and harder; Sardine Run: it spins up, striking
/// faster.</item>
/// </list>
/// </summary>
public static class SurgeTuning
{
    /// <summary>Half the width of the plain surge (m), times her shot size.</summary>
    public const float Width = 0.11f;
    /// <summary>How finely it is traced along its way (m).</summary>
    public const float Step = 0.2f;
    /// <summary>It strikes every Pulse seconds; each strike carries Dps × her shots' damage per second for that time.</summary>
    public const float Pulse = 0.2f, Dps = 0.9f;
    /// <summary>Every extra shot of her volley adds this share of the damage to each foe in the cone.</summary>
    public const float ConeShare = 0.25f;
    /// <summary>The cone is traced as rays this far apart (degrees), at most MaxRays.</summary>
    public const float RayEveryDeg = 2.5f;
    public const int MaxRays = 24;
    /// <summary>Anglerfish Lure: it bends toward the nearest foe, at most this many degrees per metre.</summary>
    public const float HomingTurnDegPerM = 28f;
    /// <summary>Pearl Diver, fully focused: this much of its width, this many times its damage.</summary>
    public const float FocusWidth = 0.55f, FocusDamage = 2f;
    /// <summary>Starburst Gland: its bursts come this often, at most MaxBursts at once.</summary>
    public const float BurstEvery = 0.4f;
    public const int MaxBursts = 6;
    /// <summary>The ground its tip and impacts clear on each strike: this reach (plus its width), this much density.</summary>
    public const float TipCleanse = 1f, TipCleanseStrength = 0.45f;
}

/// <summary>One traced line of the surge: its points (her side first), and how wide it is along them.</summary>
public sealed class SurgeRay
{
    public readonly List<Vector2> Points = new();
    /// <summary>Half its width where it starts.</summary>
    public float Width;
    /// <summary>A cone's ray widens this much per metre from her, so the rays meet with no gaps.</summary>
    public float Wedge;
    /// <summary>Starfish Arm: the width grows to GrowMax× over this distance (0: it does not grow).</summary>
    public float GrowOver;
    /// <summary>How far it is from her where this ray starts (a fork starts at its parent's impact).</summary>
    public float StartAt;
    /// <summary>The way it went, start to end (m).</summary>
    public float Length;
    /// <summary>A Mitosis branch.</summary>
    public bool Fork;
    /// <summary>It ended on something (rock, a pot, a creature) rather than at its reach.</summary>
    public bool Blocked;
    public Vector2 End => Points[^1];

    /// <summary>Half its width at distance s along it.</summary>
    public float WidthAt(float s)
    {
        float grow = GrowOver > 0f ? 1f + (PlaneCombatTuning.GrowMax - 1f) * MathUtil.Clamp01((StartAt + s) / GrowOver) : 1f;
        return Width * grow + Wedge * (StartAt + s);
    }
}

/// <summary>Her surge this step (null while she is not pouring it out).</summary>
public sealed class PlaneSurge
{
    public readonly List<SurgeRay> Rays = new();
    /// <summary>Where it struck something this step (creatures, pots, rock).</summary>
    public readonly List<Vector2> Impacts = new();
    /// <summary>Double Helix: drawn as two strands spiralling round each ray.</summary>
    public bool Helix;
    /// <summary>How hard it strikes against a plain surge (focus, spin, cone): its brightness.</summary>
    public float Power = 1f;
    /// <summary>Seconds it has been pouring out.</summary>
    public float Age;
}

public sealed partial class PlaneWorld
{
    /// <summary>Firefly Squid's surge while she pours it out; null otherwise.</summary>
    public PlaneSurge? LightSurge { get; private set; }

    float _surgePulse, _surgeBurst;
    /// <summary>What the surge touches this step and how hard (the strongest touch counts once per strike).</summary>
    readonly Dictionary<object, float> _surgeStruck = new();

    /// <summary>The surge: traced every step while fire is held; it strikes every SurgeTuning.Pulse seconds.</summary>
    void StepSurge(bool on)
    {
        var p = Player;
        var spec = Run.Loadout.Shot;
        // Sardine Run: it spins up while she holds fire, and down when she lets go.
        if (Run.Loadout.Flags.Contains("minigun"))
            p.Spin = on ? MathF.Min(1f, p.Spin + Dt / PlaneCombatTuning.SpinUp) : MathF.Max(0f, p.Spin - Dt / PlaneCombatTuning.SpinDown);
        else p.Spin = 0f;
        if (!on)
        {
            LightSurge = null;
            p.Charge = 0f;
            _surgePulse = _surgeBurst = 0f;
            return;
        }
        // Pearl Diver: held, it focuses.
        p.Charge = spec.Charge ? MathF.Min(p.Charge + Dt, PlaneCombatTuning.ChargeSeconds) : 0f;
        float focus = spec.Charge ? p.Charge / PlaneCombatTuning.ChargeSeconds : 0f;

        var surge = LightSurge ??= new PlaneSurge();
        surge.Age += Dt;
        surge.Rays.Clear();
        surge.Impacts.Clear();
        _surgeStruck.Clear();

        int count = Math.Max(1, spec.Multishot);
        surge.Helix = spec.Wave;
        // Double Helix's second shot is the helix's second strand; the others open the cone.
        int fan = spec.Wave ? count - 1 : count;
        float spread = (fan - 1) * (spec.Pattern == ShotPattern.Cone ? spec.SpreadDeg * 1.6f : spec.SpreadDeg) * MathUtil.Deg2Rad;
        int rays = spread <= 0f ? 1 : Math.Clamp((int)MathF.Ceiling(spread / (SurgeTuning.RayEveryDeg * MathUtil.Deg2Rad)) + 1, 2, SurgeTuning.MaxRays);
        float wedge = rays > 1 ? MathF.Tan(spread / (rays - 1) * 0.5f) * 1.1f : 0f;
        float width = SurgeTuning.Width * spec.SizeMult * Run.Loadout.Stats[Stat.ShotSize] * (1f + (SurgeTuning.FocusWidth - 1f) * focus);
        float reach = ThrowReach;
        var from = p.Position + p.Aim * (Radius + 0.1f);
        for (int i = 0; i < rays; i++)
        {
            float a = rays == 1 ? 0f : -spread * 0.5f + spread * i / (rays - 1);
            var ray = new SurgeRay { Width = width, Wedge = wedge, GrowOver = spec.Grow ? reach : 0f };
            surge.Rays.Add(ray);
            Trace(surge, ray, from, Rotate(p.Aim, a), reach, spec, null);
        }

        float cone = 1f + SurgeTuning.ConeShare * (count - 1);
        surge.Power = (1f + (SurgeTuning.FocusDamage - 1f) * focus) * (1f + (PlaneCombatTuning.MinigunMax - 1f) * p.Spin) * MathF.Sqrt(cone);

        _surgePulse -= Dt;
        if (_surgePulse <= 0f)
        {
            _surgePulse += SurgeTuning.Pulse;
            float shot = PlaneCombatTuning.ShotDamage * spec.DamageMult * DamageScale;
            float strike = shot / ShotInterval * SurgeTuning.Dps * SurgeTuning.Pulse * (1f + (SurgeTuning.FocusDamage - 1f) * focus) * cone;
            StrikeWithSurge(surge, strike, spec);
        }
        if (spec.Explosive)
        {
            _surgeBurst -= Dt;
            if (_surgeBurst <= 0f)
            {
                _surgeBurst += SurgeTuning.BurstEvery;
                float shot = PlaneCombatTuning.ShotDamage * spec.DamageMult * DamageScale;
                BurstSurge(surge, width, shot * PlaneCombatTuning.BurstDamage);
            }
        }
    }

    /// <summary>
    /// Traces one ray from start along dir for length: it bends toward foes (Anglerfish Lure), reflects off rock (Mirror
    /// Scale), and stops on rock, a pot, or a creature (unless it pierces). Mitosis forks the main rays where they first
    /// strike. What it touches goes in _surgeStruck.
    /// </summary>
    void Trace(PlaneSurge surge, SurgeRay ray, Vector2 start, Vector2 dir, float length, ShotSpec spec, object? spare)
    {
        ray.Points.Add(start);
        var at = start;
        float gone = 0f;
        int bounces = spec.Bounces;
        bool forked = ray.Fork || !spec.Split;
        float strength = ray.Fork ? 0.5f : 1f;
        float homing = PlaneCombatTuning.HomingReach;
        // Anglerfish Lure: it locks onto the one creature nearest it, and bends for that one only.
        object? quarry = null;
        while (gone < length - 1e-4f)
        {
            float step = MathF.Min(SurgeTuning.Step, length - gone);
            if (spec.Homing)
            {
                if (quarry is not null && (SurgeTarget(quarry) is not { } still || Vector2.Dot(still - at, dir) <= 0f || _surgeTouched.Contains(quarry))) quarry = null;
                quarry ??= SurgeQuarry(at, dir, homing);
                if (quarry is not null && SurgeTarget(quarry) is { } q)
                {
                    dir = Turn(dir, SafeNormalize(q - at), SurgeTuning.HomingTurnDegPerM * MathUtil.Deg2Rad * step);
                    ray.Points.Add(at);
                }
            }
            var next = at + dir * step;
            float w = ray.WidthAt(gone + step);
            bool rim = CrossesRim(at, next);
            if (rim || !Map.IsOpen(next) || _rocks.Exists(r => Vector2.Distance(next, r.Center) < r.Radius) || ClamShellAt(next))
            {
                if (bounces > 0 && !rim)
                {
                    bounces--;
                    Vector2 wall = Map.Gradient(next);
                    wall = wall.LengthSquared() > 1e-6f ? Vector2.Normalize(wall) : dir;
                    dir = SafeNormalize(dir - 2f * Vector2.Dot(dir, wall) * wall);
                    ray.Points.Add(at);
                    gone += step * 0.5f;
                    continue;
                }
                Stop(at);
                break;
            }
            if (VaseAt(next, w) is { } vase)
            {
                Struck(vase, gone + step);
                Stop(next);
                break;
            }
            bool stopped = false;
            foreach (var target in SurgeTouches(next, w))
            {
                if (target == spare || _surgeTouched.Contains(target)) continue;
                _surgeTouched.Add(target);
                Struck(target, gone + step);
                surge.Impacts.Add(next);
                if (!forked)
                {
                    forked = true;
                    Fork(next, target);
                }
                if (!spec.Pierce)
                {
                    stopped = true;
                    break;
                }
            }
            at = next;
            gone += step;
            if (stopped)
            {
                ray.Blocked = true;
                break;
            }
        }
        _surgeTouched.Clear();
        if (ray.Points[^1] != at || ray.Points.Count == 1) ray.Points.Add(at);
        ray.Length = gone;

        void Stop(Vector2 end)
        {
            at = end;
            ray.Blocked = true;
            surge.Impacts.Add(end);
            if (!forked)
            {
                forked = true;
                Fork(end, null);
            }
        }

        void Struck(object target, float s)
        {
            float k = strength * (ray.GrowOver > 0f ? 1f + (PlaneCombatTuning.GrowMax - 1f) * MathUtil.Clamp01((ray.StartAt + s) / ray.GrowOver) : 1f);
            if (!_surgeStruck.TryGetValue(target, out float had) || had < k) _surgeStruck[target] = k;
        }

        // Mitosis: two short half-strength branches either side of its way on (they never fork again).
        void Fork(Vector2 point, object? struck)
        {
            float angle = PlaneCombatTuning.SplitAngleDeg * MathUtil.Deg2Rad;
            for (int side = -1; side <= 1; side += 2)
            {
                var branch = new SurgeRay { Width = ray.Width * PlaneCombatTuning.SplitSize, Fork = true, StartAt = 0f };
                surge.Rays.Add(branch);
                var saved = new HashSet<object>(_surgeTouched);
                _surgeTouched.Clear();
                Trace(surge, branch, point, Rotate(dir, side * angle), PlaneCombatTuning.SplitRange, spec, struck);
                foreach (var o in saved) _surgeTouched.Add(o);
            }
        }
    }

    /// <summary>What one ray has touched so far (each once per ray).</summary>
    readonly HashSet<object> _surgeTouched = new();

    /// <summary>Queen Clam's shell, while it is shut: the surge stops on it.</summary>
    bool ClamShellAt(Vector2 at) =>
        Boss is { Stage: BossStage.Fight, Open: false } boss && Vector2.Distance(at, boss.PearlAt) < PlaneBossTuning.BodyRadius;

    /// <summary>Every creature a circle at p (radius r) touches: pufferlings, murklings, Blotters, Blightroot sacs, Queen Clam's open core.</summary>
    IEnumerable<object> SurgeTouches(Vector2 p, float r)
    {
        foreach (var m in Mobs)
            if (m.Alive && Vector2.Distance(m.Position, p) < m.Radius + r) yield return m;
        foreach (var m in Murklings)
            if (m.Alive && !m.Clinging && Vector2.Distance(m.Position, p) < BlightTuning.MurkRadius + r) yield return m;
        foreach (var b in Blotters)
            if (b.Alive && b.Rise <= 0f && b.Touches(p, r)) yield return b;
        foreach (var root in Blightroots)
            if (root.Alive && Vector2.Distance(root.Position, p) < BlightTuning.SacRadius + r) yield return root;
        if (Boss is { Stage: BossStage.Fight, Open: true } boss && Vector2.Distance(boss.PearlAt, p) < PlaneBossTuning.CoreRadius + r) yield return boss;
    }

    /// <summary>Anglerfish Lure: the one creature nearest a point, ahead of it and within reach.</summary>
    object? SurgeQuarry(Vector2 at, Vector2 dir, float reach)
    {
        object? best = null;
        float nearest = reach;
        void Consider(object o)
        {
            if (SurgeTarget(o) is not { } q) return;
            float d = Vector2.Distance(q, at);
            if (d < nearest && d > 0.3f && Vector2.Dot(q - at, dir) > 0f)
            {
                nearest = d;
                best = o;
            }
        }
        foreach (var m in Mobs) Consider(m);
        foreach (var m in Murklings) Consider(m);
        foreach (var b in Blotters) Consider(b);
        foreach (var root in Blightroots) Consider(root);
        if (Boss is { } boss) Consider(boss);
        return best;
    }

    /// <summary>Where the surge would bend for a creature, or null once it cannot be struck.</summary>
    Vector2? SurgeTarget(object o) => o switch
    {
        PlaneMob m when m.Alive => m.Position,
        Murkling m when m.Alive && !m.Clinging => m.Position,
        Blotter b when b.Alive && b.Rise <= 0f => b.Position,
        Blightroot r when r.Alive => r.Position,
        PlaneBoss { Stage: BossStage.Fight, Open: true } boss => boss.PearlAt,
        _ => null,
    };

    /// <summary>
    /// One strike of the surge: everything it touches takes the strike's light (scaled by how it was touched), pots warm
    /// toward breaking, throws it crosses are snuffed, and its tip and impacts clear a little of the ground.
    /// </summary>
    void StrikeWithSurge(PlaneSurge surge, float strike, ShotSpec spec)
    {
        var aim = Player.Aim;
        foreach (var (target, k) in _surgeStruck)
        {
            float light = strike * k;
            if (spec.CritChance > 0f && _bubbleRng.NextFloat() < spec.CritChance) light *= spec.CritMult;
            switch (target)
            {
                case PlaneMob mob:
                    if (mob.Alive) DamageMob(mob, light, Vector2.Zero);
                    break;
                case Murkling m:
                    if (!m.Alive) break;
                    bool freed = m.Corruption.Hit(light);
                    Events.Add(new PlaneEvent(freed ? PlaneEventType.MobDefeated : PlaneEventType.MobHit, m.Position, Vector2.Zero, light));
                    if (freed) BurstMurkling(m, ember: true);
                    break;
                case Blotter b:
                    if (!b.Alive || Corruption is not { } field) break;
                    b.SinceStruck = 0f;
                    bool emptied = b.Corruption.Hit(light);
                    Events.Add(new PlaneEvent(emptied ? PlaneEventType.MobDefeated : PlaneEventType.MobHit, b.Position, Vector2.Zero, light));
                    if (emptied) BurstBlotter(b, field);
                    break;
                case Blightroot root:
                    if (!root.Alive) break;
                    // Emptied, it bursts on its own next step.
                    root.Corruption.Hit(light);
                    Events.Add(new PlaneEvent(PlaneEventType.MobHit, root.Position, Vector2.Zero, light));
                    break;
                case PlaneBoss boss:
                    // The crust absorbs most of it while it holds (each layer a sixth), as it does a shot's.
                    DamageBoss(boss, light * (1f - 0.17f * boss.Crust), Vector2.Zero);
                    break;
                case PlaneVase vase:
                    if (vase.Broken) break;
                    vase.Warmth += light;
                    if (vase.Warmth >= VaseTuning.BreakImpact) BreakVase(vase, aim);
                    else Events.Add(new PlaneEvent(PlaneEventType.VaseHit, vase.Position, Vector2.Zero));
                    break;
            }
        }
        // The murklings' throws it crosses in the air burst.
        foreach (var s in MurkShots)
            if (s.Life > 0f && s.Airborne && OnSurge(surge, s.Position, BlightTuning.ThrowRadius)) SplatterThrow(s);
        // Its last light clears a little of the ground where it ends and where it strikes.
        if (Corruption is not { } ground) return;
        var cleared = new List<Vector2>();
        foreach (var ray in surge.Rays) Clear(ray.End, ray.WidthAt(ray.Length));
        foreach (var at in surge.Impacts) Clear(at, surge.Rays[0].Width);

        void Clear(Vector2 at, float w)
        {
            foreach (var c in cleared)
                if (Vector2.DistanceSquared(c, at) < 0.64f) return;
            cleared.Add(at);
            ground.Cleanse(at, SurgeTuning.TipCleanse + w, SurgeTuning.TipCleanseStrength);
        }
    }

    /// <summary>Starburst Gland: the surge bursts into light at its tips and impacts (a few at once, not on top of each other).</summary>
    void BurstSurge(PlaneSurge surge, float width, float damage)
    {
        var at = new List<Vector2>();
        foreach (var p in surge.Impacts) Add(p);
        // Where a ray ended on something; and the tip: a cone's open front bursts only at its middle.
        var main = surge.Rays.FindAll(r => !r.Fork);
        var middle = main.Count > 0 ? main[main.Count / 2] : null;
        foreach (var ray in surge.Rays)
            if (ray.Blocked || ray == middle) Add(ray.End);
        foreach (var p in at) LightBurst(p, PlaneCombatTuning.BurstRadius + width, damage);

        void Add(Vector2 p)
        {
            if (at.Count >= SurgeTuning.MaxBursts) return;
            foreach (var q in at)
                if (Vector2.Distance(p, q) < PlaneCombatTuning.BurstRadius) return;
            at.Add(p);
        }
    }

    /// <summary>A circle (at, radius) lies on any ray of the surge.</summary>
    static bool OnSurge(PlaneSurge surge, Vector2 at, float radius)
    {
        foreach (var ray in surge.Rays)
        {
            float s = 0f;
            for (int i = 1; i < ray.Points.Count; i++)
            {
                var a = ray.Points[i - 1];
                var b = ray.Points[i];
                if (Geo.SegmentDistance(at, a, b) < radius + ray.WidthAt(s)) return true;
                s += Vector2.Distance(a, b);
            }
        }
        return false;
    }
}
