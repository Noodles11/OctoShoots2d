using System;
using System.Collections.Generic;
using System.Numerics;
using OctoShoots.Core.Terrain;

namespace OctoShoots.Core.Sim;

/// <summary>
/// Healthy sea life and freeing (DEPTH1-BESTIARY §8). Every corrupted creature has healthy twins of its species
/// living nearby: natural colours, harmless, never a target. Defeating a corrupted creature frees it: it becomes
/// one of them. The share of corrupted creatures grows with depth, so the reef feels lonelier the deeper she goes.
/// </summary>
public sealed partial class World
{
    /// <summary>Healthy creatures farther than this from Clementine are not simulated.</summary>
    public const float CritterRange = 60f;

    /// <summary>A freed creature shudders this long, still in tank colours, before the "poof".</summary>
    public const float FreeShudder = 0.3f;

    public List<Critter> Critters { get; } = new();

    /// <summary>Share of each species that is corrupted: one in three at Depth 1, three in four at Depth 6.</summary>
    public static float CorruptedShare(int depth) => MathUtil.Lerp(1f / 3f, 0.75f, MathUtil.Clamp01((depth - 1) / 5f));

    public static bool IsFreeable(EnemyKind kind) => IsDenCreature(kind) || kind == EnemyKind.ClownNinja;

    // ───────────────────────── healthy twins ─────────────────────────

    /// <summary>Adds the healthy members of a den's species around it, in the ratio for this depth.</summary>
    void AddHealthyTwins(DenSpot den, Rng rng)
    {
        float share = CorruptedShare(ReefDepth);
        float wanted = den.Count * (1f - share) / share;
        int count = (int)wanted + (rng.NextFloat() < wanted - (int)wanted ? 1 : 0);
        var d = Creatures![den.Kind];
        float r = d.Radius * (den.Bed ? CreatureDef.BedScale : 1f);
        for (int k = 0; k < count; k++)
        {
            var c = new Critter
            {
                Id = NextId(),
                Kind = den.Kind,
                Up = den.Up,
                Small = den.Bed,
                Phase = rng.Range(0f, MathF.Tau),
            };
            switch (den.Kind)
            {
                case EnemyKind.SeaUrchin:
                    c.Position = SurfaceSpot(den.Position, den.Up, rng, den.Bed ? 1.8f : 3.5f, r);
                    c.Anchor = c.Position - den.Up * (r * 0.7f);
                    c.Facing = den.Up;
                    break;
                case EnemyKind.Moray:
                    // Its own hole in the same wall, a few metres along.
                    c.Anchor = SurfaceSpot(den.Position, den.Up, rng, 4.5f, 0f);
                    if (Vector3.DistanceSquared(c.Anchor, den.Position) < 1.5f) continue;
                    c.Position = c.Anchor + den.Up * 0.35f;
                    break;
                case EnemyKind.Crabby:
                    c.Anchor = SurfaceSpot(den.Position, den.Up, rng, 4f, 0f);
                    c.Position = c.Anchor + den.Up * (r + 0.1f);
                    break;
                default:
                    c.Anchor = SwimmerSpot(den.Position, rng, 5f, r);
                    c.Position = c.Anchor;
                    break;
            }
            c.PrevPosition = c.Position;
            Critters.Add(c);
        }
    }

    // ───────────────────────── freeing ─────────────────────────

    /// <summary>The corruption washes out of a defeated creature: it carries on as healthy reef life.</summary>
    void Free(Enemy e, Vector3 dir, bool frozen)
    {
        if (e.Kind == EnemyKind.ClownNinja)
        {
            // The ninja loses its headband and joins the school as an ordinary clownfish.
            var fish = new Clownfish
            {
                Id = NextId(),
                Slot = e.Slot!,
                Position = e.Position,
                PrevPosition = e.Position,
                Velocity = e.Velocity * 0.3f,
                Facing = e.Facing,
                Freed = true,
            };
            Clownfish.Add(fish);
            Events.Add(new SimEvent(SimEventType.CreatureFreed, e.Position, dir, fish.Id, frozen ? 1f : 0f, e.Kind.ToString()));
            return;
        }

        var c = new Critter
        {
            Id = NextId(),
            Kind = e.Kind,
            Position = e.Position,
            PrevPosition = e.Position,
            Velocity = e.Velocity * 0.3f,
            Facing = e.Facing,
            Up = e.Up,
            Aim = e.Aim,
            Aux = e.Aux,
            Small = e.Small,
            Phase = e.Phase,
            FreedAge = 0f,
            // Swimmers settle where they were freed (jellies drift up a little); clingers and crabs keep their homes.
            Anchor = e.Kind switch
            {
                EnemyKind.SeaUrchin or EnemyKind.Moray or EnemyKind.Crabby => e.Anchor,
                EnemyKind.MoonJelly => e.Position + Vector3.UnitY * 3f,
                _ => e.Position,
            },
        };
        Critters.Add(c);
        Events.Add(new SimEvent(SimEventType.CreatureFreed, e.Position, dir, c.Id, frozen ? 1f : 0f, e.Kind.ToString()));
    }

    // ───────────────────────── behaviour ─────────────────────────

    void StepCritters()
    {
        if (Creatures is null) return;
        var p = Player;
        foreach (var c in Critters)
        {
            if (c.FreedAge >= 0f) c.FreedAge += Dt;
            Vector3 to = p.Position - c.Position;
            float dist = to.Length();
            if (dist > CritterRange) continue;
            var d = Creatures[c.Kind];
            float r = d.Radius * (c.Small ? CreatureDef.BedScale : 1f);
            Vector3 dir = dist > 1e-4f ? to / dist : Vector3.UnitY;

            // Just freed: it shudders in place while the tank washes out of it.
            if (c.FreedAge >= 0f && c.FreedAge < FreeShudder)
            {
                c.Velocity = MathUtil.MoveToward(c.Velocity, Vector3.Zero, 12f * Dt);
                if (!IsClinger(c.Kind)) MoveSphere(ref c.Position, ref c.Velocity, r);
                continue;
            }

            switch (c.Kind)
            {
                case EnemyKind.SeaUrchin:
                    break;

                case EnemyKind.Moray:
                {
                    // Peeks out and watches her when she is near; otherwise rests just inside the hole.
                    bool watching = dist < 9f;
                    float want = watching ? 0.9f : 0.15f + 0.15f * MathF.Sin(Tick * Dt * 0.4f + c.Phase);
                    c.Aux = MathUtil.Lerp(c.Aux, want, 3f * Dt);
                    Vector3 look = watching ? ClampToHole(c.Up, dir) : c.Up;
                    c.Aim = MathUtil.SafeNormalize(Vector3.Lerp(c.Aim, look, 3f * Dt), c.Up);
                    c.Position = c.Anchor + c.Up * 0.35f + c.Aim * c.Aux;
                    c.Facing = c.Aim;
                    break;
                }

                case EnemyKind.Crabby:
                {
                    // Scuttles about near home and hurries away from her.
                    Vector3 target = WanderTarget(c.Kind, c.Anchor, c.Phase, out _);
                    Vector3 want = target - c.Position;
                    float speed = d.Speed * 0.35f;
                    Vector3 flat = new(to.X, 0f, to.Z);
                    if (flat.Length() < 4f)
                    {
                        want = -flat;
                        speed = d.Speed * 0.8f;
                    }
                    c.Grounded = WalkBody(ref c.Position, ref c.Velocity, r, want, want.Length() > 0.4f ? speed : 0f);
                    if (want.LengthSquared() > 0.16f)
                        c.Facing = MathUtil.RotateToward(c.Facing, MathUtil.SafeNormalize(new Vector3(want.X, 0f, want.Z), c.Facing), 6f * Dt);
                    break;
                }

                default:
                {
                    // Swimmers drift around home, keep clear of her and of her bubbles; a barracuda turns to watch her.
                    Vector3 target = WanderTarget(c.Kind, c.Anchor, c.Phase, out float radius);
                    Vector3 want = target - c.Position;
                    float len = want.Length();
                    bool farHome = Vector3.DistanceSquared(c.Position, c.Anchor) > radius * radius * 4f;
                    float speed = d.Speed * (farHome ? 0.8f : c.Kind == EnemyKind.Barracuda ? 0.45f : 0.3f);
                    Vector3 desired = len > 1e-3f ? want / len * MathF.Min(speed, len * 0.8f + 0.2f) : Vector3.Zero;

                    float shy = c.Kind == EnemyKind.Barracuda ? 6f : 3.5f;
                    if (dist < shy) desired -= dir * (shy - dist) * 1.2f;
                    foreach (var shot in Projectiles)
                    {
                        if (!shot.FromPlayer) continue;
                        Vector3 away = c.Position - shot.Position;
                        float da = away.Length();
                        if (da < 1.8f && da > 1e-3f) desired += away / da * (1.8f - da) * 3f;
                    }
                    if (c.FreedAge >= 0f && c.FreedAge < 2f) desired -= dir * 1.5f; // freed: swims off, away from her

                    SwimBody(ref c.Position, ref c.Velocity, r, desired, 3.5f);
                    bool watch = c.Kind == EnemyKind.Barracuda && dist < 15f;
                    Vector3 face = watch ? dir : MathUtil.SafeNormalize(c.Velocity, c.Facing);
                    c.Facing = MathUtil.RotateToward(c.Facing, face, (watch ? 2f : 3f) * Dt);
                    break;
                }
            }
        }
    }
}
