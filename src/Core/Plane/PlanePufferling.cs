using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Core.Plane;

/// <summary>The Pufferling's numbers (docs/PUFFERLING-PROPOSAL.md §3); first guesses, tuned in play.</summary>
public static class PufferlingTuning
{
    /// <summary>Its body for collision and for her bubbles: calm, and blown up into a ball about 2.2 m across.</summary>
    public const float CalmRadius = 0.5f, InflatedRadius = 1.1f;
    public const float Hp = 60f;

    public const float WanderSpeed = 1.5f, Accel = 3f;
    /// <summary>It turns at most this fast, easing into and out of its turns.</summary>
    public const float TurnDegPerSec = 120f;
    /// <summary>Wandering: the next spot 4–9 m off, within this far of home; a 1–4 s pause at each.</summary>
    public const float HomeRange = 12f;

    /// <summary>It notices her within NoticeRange (in sight), blows up when she is within Reach, and lets her go past GiveUp.</summary>
    public const float NoticeRange = 10f, Reach = 8f, GiveUp = 16f, LoseSightSeconds = 2f;
    /// <summary>In sight but out of reach, it drifts toward her.</summary>
    public const float DriftSpeed = 1.2f;
    /// <summary>It starts blowing up once it faces her within this angle.</summary>
    public const float FaceToleranceDeg = 20f;
    /// <summary>The telegraph: from calm to blown up. Shortens with Menace, never under 0.45 s.</summary>
    public const float Telegraph = 0.8f, MinTelegraph = 0.45f;

    public const int Needles = 8;
    public const float NeedleSpeed = 20f, NeedleRange = 14f, NeedleDamage = 8f, NeedleRadius = 0.18f;
    /// <summary>Blown up, its spines hurt on touch.</summary>
    public const float SpineDamage = 4f;

    /// <summary>After firing: round and open to hits, then the cooldown — it backs away, shrinks back, its spines regrow.</summary>
    public const float RoundSeconds = 0.5f, Cooldown = 3f, RetreatSpeed = 1.15f, RetreatSeconds = 2.6f, DeflateSeconds = 1.2f, SpineRegrow = 2f;

    /// <summary>Healthy pufferlings per level, in ones, twos and threes.</summary>
    public const int HealthyMin = 8, HealthyMax = 12;
    /// <summary>Freed: it shudders, then swims off; once this far from her (off the screen) it is gone.</summary>
    public const float FreedShudder = 0.6f, FreedSpeed = 1.8f, FreedGone = 24f;
}

public enum PufferState { Wander, Face, Inflate, Round, Cooldown }

/// <summary>Anything on the plane that swims like a fish: a home spot to wander about, and a heading it turns smoothly.</summary>
public abstract class PlaneSwimmer
{
    public Vector2 Position;
    public Vector2 Velocity;
    /// <summary>Which way it faces (radians on the map; 0 is east).</summary>
    public float Heading;
    /// <summary>How fast it is turning (radians a second): the view banks it into turns.</summary>
    public float TurnRate;
    public Vector2 Home, Target;
    public float Wait;
    /// <summary>While pausing, it turns slowly to look about.</summary>
    public float IdleHeading;
    /// <summary>0 calm → 1 blown up.</summary>
    public float Inflate;
    /// <summary>Its own random stream, from the level's seed: the same level swims the same way.</summary>
    public Rng Rng = null!;
    public Vector2 Facing => new(MathF.Cos(Heading), MathF.Sin(Heading));
}

/// <summary>A healthy pufferling: reef life, never a target. A freed one swims away and is gone once out of sight.</summary>
public sealed class PlaneFish : PlaneSwimmer
{
    public int Id;
    public bool Freed;
    public float FreedTime;
    public bool Gone;
}

public sealed partial class PlaneWorld
{
    /// <summary>Healthy pufferlings, and the freed ones swimming off.</summary>
    public List<PlaneFish> Fish { get; } = new();

    int _fishIds;

    static float Ease(float t) => MathUtil.Smoothstep(MathUtil.Clamp01(t));

    static float AngleDiff(float to, float from)
    {
        float d = (to - from) % MathF.Tau;
        if (d > MathF.PI) d -= MathF.Tau;
        if (d < -MathF.PI) d += MathF.Tau;
        return d;
    }

    /// <summary>Steers toward a wished velocity and turns toward a heading (its own course if none), smoothly.</summary>
    void Swim(PlaneSwimmer s, Vector2 wish, float? face, float radius)
    {
        s.Velocity = MoveToward(s.Velocity, wish, PufferlingTuning.Accel * Dt);
        float want = face ?? (s.Velocity.Length() > 0.25f ? MathF.Atan2(s.Velocity.Y, s.Velocity.X) : s.IdleHeading);
        float max = PufferlingTuning.TurnDegPerSec * MathUtil.Deg2Rad;
        // Quick to start, easing out as it comes round.
        s.TurnRate = Math.Clamp(AngleDiff(want, s.Heading) * 5f, -max, max);
        s.Heading += s.TurnRate * Dt;
        Move(ref s.Position, ref s.Velocity, radius, report: false, barrier: ArenaBarrier.Outside);
    }

    /// <summary>Swims from spot to spot about its home, pausing at each to hover and look about.</summary>
    void Wander(PlaneSwimmer s, float speed, float radius)
    {
        if (s.Wait > 0f)
        {
            s.Wait -= Dt;
            Swim(s, Vector2.Zero, null, radius);
            return;
        }
        float dist = Vector2.Distance(s.Position, s.Target);
        if (s.Target == default || dist < 0.8f)
        {
            s.Wait = s.Rng.Range(1f, 4f);
            s.IdleHeading = s.Heading + s.Rng.Range(-1.2f, 1.2f);
            s.Target = PickSpot(s, radius);
            return;
        }
        Vector2 dir = (s.Target - s.Position) / dist;
        Swim(s, dir * speed * MathUtil.Clamp01(dist / 2f + 0.25f), null, radius);
    }

    /// <summary>The next spot to swim to: a few metres off, near home, in open water it can see.</summary>
    Vector2 PickSpot(PlaneSwimmer s, float radius)
    {
        for (int i = 0; i < 10; i++)
        {
            float a = s.Rng.Range(0f, MathF.Tau), d = s.Rng.Range(4f, 9f);
            var q = s.Position + new Vector2(MathF.Cos(a), MathF.Sin(a)) * d;
            if (Vector2.Distance(q, s.Home) > PufferlingTuning.HomeRange) continue;
            if (!Clear(q, radius + 0.3f) || !LineOfSight(s.Position, q)) continue;
            return q;
        }
        return s.Position;
    }

    float MobSpeedScale => 1f + 0.5f * Menace;

    /// <summary>
    /// A corrupted pufferling's turn (docs/PUFFERLING-PROPOSAL.md §3.2): wander; in sight, turn to face her and drift
    /// closer; within reach, blow up (the telegraph), fire 8 needles round itself with a random turn, stay round a
    /// moment, then cool down — back away, shrink, regrow the spines — and go again.
    /// </summary>
    void StepPufferling(PlaneMob mob)
    {
        var p = Player;
        Vector2 to = p.Position - mob.Position;
        float dist = to.Length();
        Vector2 dir = dist > 1e-4f ? to / dist : mob.Facing;
        float faceHer = MathF.Atan2(dir.Y, dir.X);
        bool sees = dist <= PufferlingTuning.GiveUp && LineOfSight(mob.Position, p.Position) && !CrossesRim(mob.Position, p.Position);
        mob.StateTime += Dt;
        mob.FireTimer -= Dt;
        float telegraph = MathF.Max(PufferlingTuning.MinTelegraph, PufferlingTuning.Telegraph - 0.35f * MathF.Min(Menace, 1f));
        float radius = PufferlingTuning.CalmRadius;

        switch (mob.State)
        {
            case PufferState.Wander:
                Wander(mob, PufferlingTuning.WanderSpeed * MobSpeedScale, radius);
                if (sees && dist <= PufferlingTuning.NoticeRange) Enter(mob, PufferState.Face, notice: true);
                break;
            case PufferState.Face:
                mob.LostSight = sees ? 0f : mob.LostSight + Dt;
                if (dist > PufferlingTuning.GiveUp || mob.LostSight > PufferlingTuning.LoseSightSeconds)
                {
                    mob.Aggro = false;
                    mob.Home = mob.Position;
                    mob.Target = default;
                    Enter(mob, PufferState.Wander);
                    break;
                }
                // Out of reach, it drifts closer, facing her all the while.
                Vector2 wish = dist > PufferlingTuning.Reach - 0.5f && sees ? dir * PufferlingTuning.DriftSpeed * MobSpeedScale : Vector2.Zero;
                Swim(mob, wish, faceHer, radius);
                if (sees && dist <= PufferlingTuning.Reach && mob.FireTimer <= 0f && MathF.Abs(AngleDiff(faceHer, mob.Heading)) < PufferlingTuning.FaceToleranceDeg * MathUtil.Deg2Rad)
                {
                    Enter(mob, PufferState.Inflate);
                    Events.Add(new PlaneEvent(PlaneEventType.PufferSwells, mob.Position, dir));
                }
                break;
            case PufferState.Inflate:
                Swim(mob, Vector2.Zero, faceHer, radius);
                mob.Inflate = Ease(mob.StateTime / telegraph);
                if (mob.StateTime >= telegraph)
                {
                    FireNeedles(mob);
                    Enter(mob, PufferState.Round);
                }
                break;
            case PufferState.Round:
                Swim(mob, Vector2.Zero, faceHer, radius);
                if (mob.StateTime >= PufferlingTuning.RoundSeconds) Enter(mob, PufferState.Cooldown);
                break;
            case PufferState.Cooldown:
                // Backs away a little, still facing her, shrinking back; the spines grow back.
                Vector2 back = mob.StateTime < PufferlingTuning.RetreatSeconds ? -dir * PufferlingTuning.RetreatSpeed * MobSpeedScale : Vector2.Zero;
                Swim(mob, back, faceHer, radius);
                mob.Inflate = 1f - Ease(mob.StateTime / PufferlingTuning.DeflateSeconds);
                mob.Spines = MathUtil.Clamp01(mob.StateTime / PufferlingTuning.SpineRegrow);
                if (mob.StateTime >= PufferlingTuning.Cooldown / (1f + 0.6f * Menace))
                {
                    mob.Inflate = 0f;
                    mob.Spines = 1f;
                    if (sees) Enter(mob, PufferState.Face);
                    else
                    {
                        mob.Aggro = false;
                        mob.Home = mob.Position;
                        mob.Target = default;
                        Enter(mob, PufferState.Wander);
                    }
                }
                break;
        }

        // Blown up, its spines hurt on touch.
        if (mob.Inflate > 0.4f && dist < mob.Radius + Radius) HurtPlayer(PufferlingTuning.SpineDamage, dir, DamageSource.PufferSpines);
    }

    void Enter(PlaneMob mob, PufferState state, bool notice = false)
    {
        mob.State = state;
        mob.StateTime = 0f;
        if (notice) Notice(mob);
    }

    /// <summary>Eight needles round it, evenly spaced, the ring turned at random; the spines leave its skin.</summary>
    void FireNeedles(PlaneMob mob)
    {
        float turn = mob.Rng.Range(0f, MathF.Tau / PufferlingTuning.Needles);
        float speed = PufferlingTuning.NeedleSpeed * (1f + 0.4f * Menace);
        for (int k = 0; k < PufferlingTuning.Needles; k++)
        {
            float a = turn + k * MathF.Tau / PufferlingTuning.Needles;
            var d = new Vector2(MathF.Cos(a), MathF.Sin(a));
            Shots.Add(new PlaneShot
            {
                Position = mob.Position + d * (PufferlingTuning.InflatedRadius + 0.1f),
                Velocity = d * speed,
                Life = PufferlingTuning.NeedleRange / speed,
                Damage = PufferlingTuning.NeedleDamage,
                Radius = PufferlingTuning.NeedleRadius,
                Needle = true,
            });
        }
        mob.Spines = 0f;
        Events.Add(new PlaneEvent(PlaneEventType.NeedlesFired, mob.Position, new Vector2(MathF.Cos(turn), MathF.Sin(turn)), PufferlingTuning.InflatedRadius));
    }

    /// <summary>Healthy pufferlings in ones, twos and threes near the reef: by the plazas and the dens, away from the start.</summary>
    void PlaceFish()
    {
        var rng = new Rng(Map.Seed ^ 0xF15A11UL ^ ((ulong)Map.Level << 20) ^ ((ulong)Map.Depth << 28) ^ ((ulong)Map.Attempt << 44));
        int want = PufferlingTuning.HealthyMin + rng.Int(PufferlingTuning.HealthyMax - PufferlingTuning.HealthyMin + 1);
        var anchors = Map.Plazas.Select(z => z.Center).Concat(Map.Spawns.Where(s => s.Role != SpawnRole.Ambush).Select(s => s.Position)).ToList();
        foreach (var c in Map.Corridors.Where(c => c.Kind == CorridorKind.Main))
            for (int i = 6; i < c.Points.Count - 6; i += 9) anchors.Add(c.Points[i]);
        for (int i = anchors.Count - 1; i > 0; i--)
        {
            int j = rng.Int(i + 1);
            (anchors[i], anchors[j]) = (anchors[j], anchors[i]);
        }
        var groups = new List<Vector2>();
        foreach (var anchor in anchors)
        {
            if (Fish.Count >= want) break;
            if (Vector2.Distance(anchor, Map.Start.Position) < 20f || groups.Any(g => Vector2.Distance(g, anchor) < 14f)) continue;
            if (!Clear(anchor, PufferlingTuning.CalmRadius + 0.5f)) continue;
            groups.Add(anchor);
            int size = Math.Min(1 + rng.Int(3), want - Fish.Count);
            for (int k = 0; k < size; k++)
            {
                var at = anchor + new Vector2(rng.Range(-2.5f, 2.5f), rng.Range(-2.5f, 2.5f));
                if (!Clear(at, PufferlingTuning.CalmRadius + 0.2f)) at = anchor;
                Fish.Add(new PlaneFish
                {
                    Id = _fishIds++,
                    Position = at,
                    Home = anchor,
                    Heading = rng.Range(0f, MathF.Tau),
                    Wait = rng.Range(0f, 3f),
                    Rng = new Rng(rng.NextU64()),
                });
            }
        }
    }

    /// <summary>Freed: it becomes a healthy pufferling, shudders, then swims off (it never helps her).</summary>
    void Free(PlaneMob mob)
    {
        Fish.Add(new PlaneFish
        {
            Id = _fishIds++,
            Freed = true,
            Position = mob.Position,
            Home = mob.Position,
            Heading = mob.Heading,
            Inflate = mob.Inflate,
            Rng = new Rng(mob.Rng.NextU64()),
        });
    }

    /// <summary>The reef's pufferlings: wandering, unbothered by her; the freed ones swimming off until out of sight.</summary>
    void StepFish()
    {
        foreach (var f in Fish)
        {
            if (!f.Freed)
            {
                Wander(f, PufferlingTuning.WanderSpeed, PufferlingTuning.CalmRadius);
                continue;
            }
            f.FreedTime += Dt;
            f.Inflate = MathF.Max(0f, f.Inflate - Dt / PufferlingTuning.DeflateSeconds);
            Vector2 away = f.Position - Player.Position;
            float dist = away.Length();
            if (f.FreedTime < PufferlingTuning.FreedShudder) Swim(f, Vector2.Zero, null, PufferlingTuning.CalmRadius);
            else
            {
                // Off along open water, away from her, slowly.
                if (f.Target == default || Vector2.Distance(f.Position, f.Target) < 1.5f || !LineOfSight(f.Position, f.Target))
                {
                    f.Target = f.Position;
                    Vector2 off = dist > 1e-3f ? away / dist : f.Facing;
                    for (int i = 0; i < 12; i++)
                    {
                        float a = MathF.Atan2(off.Y, off.X) + f.Rng.Range(-1.1f, 1.1f);
                        var q = f.Position + new Vector2(MathF.Cos(a), MathF.Sin(a)) * f.Rng.Range(5f, 10f);
                        if (Clear(q, PufferlingTuning.CalmRadius + 0.3f) && LineOfSight(f.Position, q))
                        {
                            f.Target = q;
                            break;
                        }
                    }
                }
                Vector2 to = f.Target - f.Position;
                Swim(f, to.LengthSquared() > 0.01f ? Vector2.Normalize(to) * PufferlingTuning.FreedSpeed : Vector2.Zero, null, PufferlingTuning.CalmRadius);
            }
            if (dist > PufferlingTuning.FreedGone) f.Gone = true;
        }
        Fish.RemoveAll(f => f.Gone);
    }
}
