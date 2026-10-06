using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Items;
using OctoShoots.Core.Terrain;

namespace OctoShoots.Core.Sim;

/// <summary>
/// Anemone nests (§7.5): a school of 3–5 neutral clownfish circling an anemone, with a clownfish
/// ninja hiding among them. The ninja sleeps inside the anemone until Clementine comes close,
/// swims out and blends into the school, then ambushes her with tiny throwing starfish.
/// </summary>
public sealed partial class World
{
    const float FishRadius = 0.22f;

    public List<Clownfish> Clownfish { get; } = new();
    public IReadOnlyList<AnemoneSpot> Anemones => Cave.Anemones;

    int ReefDepth => Math.Max(1, Cave.Loot.Depth);

    /// <summary>Wind-up time of a creature's attack, for telegraph visuals and danger markers.</summary>
    public float TelegraphTime(Enemy e) =>
        IsDenCreature(e.Kind) && Creatures is not null ? Creatures[e.Kind].Telegraph
        : e.Kind == EnemyKind.ClownNinja ? Tuning.NinjaTelegraph : Tuning.EnemyTelegraph;

    /// <summary>Hit radius of a creature.</summary>
    public float RadiusOf(Enemy e) =>
        IsDenCreature(e.Kind) && Creatures is not null ? Creatures[e.Kind].Radius * ScaleOf(e)
        : e.Kind == EnemyKind.ClownNinja ? Tuning.NinjaRadius : Tuning.EnemyRadius;

    void SetUpNests()
    {
        if (!Tuning.NestsEnabled) return;
        var plan = Cave.Loot;
        var rng = _streams.Nests(plan.Depth, plan.Reef);
        for (int i = 0; i < Cave.Anemones.Count; i++)
        {
            var anemone = Cave.Anemones[i];
            if (!anemone.Nest) continue;

            // The ninja takes one place in the school; everyone circles at their own pace.
            int slots = anemone.SchoolSize + 1;
            int ninjaSlot = rng.Int(slots);
            for (int k = 0; k < slots; k++)
            {
                var slot = new SchoolSlot
                {
                    Home = i,
                    Angle = k * MathF.Tau / slots + rng.Range(-0.3f, 0.3f),
                    Radius = rng.Range(0.5f, 1.6f),
                    Height = rng.Range(0.3f, 1.4f),
                    Speed = rng.Range(0.35f, 0.6f) * (rng.NextFloat() < 0.5f ? -1f : 1f),
                    Phase = rng.Range(0f, MathF.Tau),
                };
                if (k == ninjaSlot)
                {
                    var ninja = new Enemy { Id = NextId(), Kind = EnemyKind.ClownNinja, HomeAnemone = i, Slot = slot };
                    ResetNinja(ninja);
                    Enemies.Add(ninja);
                }
                else
                {
                    Vector3 at = SchoolTarget(slot);
                    Clownfish.Add(new Clownfish { Id = NextId(), Slot = slot, Position = at, PrevPosition = at });
                }
            }
        }
    }

    /// <summary>Puts a ninja back to sleep inside its anemone.</summary>
    void ResetNinja(Enemy e)
    {
        var anemone = Cave.Anemones[e.HomeAnemone];
        e.Position = e.PrevPosition = anemone.Mouth;
        e.Velocity = Vector3.Zero;
        e.Hp = Tuning.NinjaHp;
        e.State = EnemyState.Nested;
        e.StateTimer = 0f;
        e.AttackCooldown = Tuning.NinjaFirstDelay;
        e.HurtTimer = 0f;
        e.OffscreenAttack = false;
        e.FrozenTimer = e.BurnTimer = e.PoisonTimer = e.CharmTimer = e.SlowTimer = e.StunTimer = 0f;
    }

    /// <summary>Where a school member wants to be right now: a wobbling circle above the anemone.</summary>
    Vector3 SchoolTarget(SchoolSlot slot)
    {
        var a = Cave.Anemones[slot.Home];
        float time = Tick * Dt;
        float angle = slot.Angle + time * slot.Speed;
        float radius = a.Radius * 0.6f + slot.Radius + 0.3f * MathF.Sin(time * 0.7f + slot.Phase);
        Vector3 side = Vector3.Normalize(Vector3.Cross(a.Up, MathF.Abs(a.Up.Y) > 0.9f ? Vector3.UnitX : Vector3.UnitY));
        Vector3 side2 = Vector3.Cross(a.Up, side);
        float height = a.Height * 0.6f + slot.Height + 0.35f * MathF.Sin(time * 1.1f + slot.Phase);
        return a.Position + a.Up * height + (side * MathF.Cos(angle) + side2 * MathF.Sin(angle)) * radius;
    }

    /// <summary>
    /// One tick of school swimming, shared by the neutral fish and the disguised ninja so they move
    /// alike: follow the circle, keep clear of rock, scatter from Clementine and from her bubbles.
    /// </summary>
    void SwimInSchool(SchoolSlot slot, ref Vector3 position, ref Vector3 velocity, ref Vector3 facing, float speed)
    {
        Vector3 want = SchoolTarget(slot) - position;
        float dist = want.Length();
        Vector3 desired = dist > 1e-3f ? want / dist * MathF.Min(speed, dist * 2.5f + 0.4f) : Vector3.Zero;

        Vector3 fromPlayer = position - Player.Position;
        float d = fromPlayer.Length();
        if (d < 3f && d > 1e-3f) desired += fromPlayer / d * (3f - d) * 1.4f;
        foreach (var shot in Projectiles)
        {
            if (!shot.FromPlayer) continue;
            Vector3 away = position - shot.Position;
            float da = away.Length();
            if (da < 1.6f && da > 1e-3f) desired += away / da * (1.6f - da) * 3f;
        }

        float clearance = Sdf.Sample(position);
        if (clearance < 1f) desired += Sdf.Gradient(position) * (1f - clearance) * 3f;

        velocity = MathUtil.MoveToward(velocity, desired, 9f * Dt);
        MoveSphere(ref position, ref velocity, FishRadius);
        ClampToSurface(ref position, ref velocity, FishRadius);
        facing = MathUtil.RotateToward(facing, MathUtil.SafeNormalize(velocity, facing), 7f * Dt);
    }

    void StepClownfish()
    {
        foreach (var f in Clownfish)
        {
            f.PrevPosition = f.Position;
            SwimInSchool(f.Slot, ref f.Position, ref f.Velocity, ref f.Facing, Tuning.SchoolSpeed);
        }
    }

    // ───────────────────────── the ninja ─────────────────────────

    /// <summary>A sleeping ninja wakes when Clementine comes near: it swims out of its anemone into the school.</summary>
    void StepNestedNinja(Enemy e)
    {
        Vector3 away = Player.Position - e.Position;
        if (Player.HiddenTimer > 0f || away.Length() > Tuning.NinjaEmergeRange) return;
        var anemone = Cave.Anemones[e.HomeAnemone];
        e.State = EnemyState.Idle;
        e.Position = e.PrevPosition = anemone.Mouth;
        e.Velocity = anemone.Up * 2.5f;
        e.AttackCooldown = Tuning.NinjaFirstDelay + _rng.Range(0f, 1f);
        Events.Add(new SimEvent(SimEventType.NinjaEmerged, e.Position, anemone.Up, e.Id));
    }

    void StepNinja(Enemy e, Vector3 view, float cosView, ref int offscreenActive)
    {
        var t = Tuning;
        var p = Player;
        Vector3 to = p.Position - e.Position;
        float dist = MathF.Max(to.Length(), 1e-4f);
        Vector3 dir = to / dist;
        bool blind = p.HiddenTimer > 0f || e.CharmTimer > 0f;

        float speedScale = 1f;
        foreach (var c in Clouds)
            if (Vector3.DistanceSquared(c.Position, e.Position) < c.Radius * c.Radius) speedScale *= t.InkCloudSlow;
        if (e.SlowTimer > 0f) speedScale *= StatusRules.SlowFactor;

        switch (e.State)
        {
            case EnemyState.Idle:
                // Disguised: swims exactly like the neutral fish around it.
                SwimInSchool(e.Slot!, ref e.Position, ref e.Velocity, ref e.Facing, t.SchoolSpeed * speedScale);
                if (dist > t.NinjaReturnRange)
                {
                    var home = Cave.Anemones[e.HomeAnemone];
                    e.State = EnemyState.Nested;
                    e.Position = home.Mouth;
                    e.Velocity = Vector3.Zero;
                    Events.Add(new SimEvent(SimEventType.NinjaHid, e.Position, home.Up, e.Id));
                    break;
                }
                if (!blind && e.AttackCooldown <= 0f && dist < t.NinjaAttackRange && Sdf.LineOfSight(e.Position, p.Position))
                {
                    bool onScreen = Vector3.Dot(view, -dir) >= cosView;
                    if (onScreen || offscreenActive < t.MaxOffscreenAttackers)
                    {
                        e.State = EnemyState.Telegraph;
                        e.StateTimer = t.NinjaTelegraph;
                        e.OffscreenAttack = !onScreen;
                        if (!onScreen) offscreenActive++;
                        Events.Add(new SimEvent(SimEventType.EnemyTelegraph, e.Position, dir, e.Id, t.NinjaTelegraph, "ninja"));
                    }
                }
                break;

            case EnemyState.Telegraph:
                // Stops and takes aim.
                e.Velocity = MathUtil.MoveToward(e.Velocity, Vector3.Zero, 8f * Dt);
                MoveSphere(ref e.Position, ref e.Velocity, t.NinjaRadius);
                if (e.StateTimer <= 0f)
                {
                    FireNinjaStars(e, dir, dist);
                    e.State = EnemyState.Recover;
                    e.StateTimer = 0.7f;
                    e.AttackCooldown = t.NinjaCooldown + _rng.Range(0f, 1.2f);
                    e.OffscreenAttack = false;
                }
                break;

            case EnemyState.Recover:
                // Darts back into the school.
                SwimInSchool(e.Slot!, ref e.Position, ref e.Velocity, ref e.Facing, t.SchoolSpeed * 2.6f * speedScale);
                if (e.StateTimer <= 0f) e.State = EnemyState.Idle;
                break;
        }

        Vector3 wantFacing = e.State == EnemyState.Telegraph && !blind ? dir : MathUtil.SafeNormalize(e.Velocity, e.Facing);
        e.Facing = MathUtil.RotateToward(e.Facing, wantFacing, 8f * Dt);
    }

    /// <summary>One volley of tiny starfish (1 at Depth 1, up to 3 deeper), aimed with a half lead so it can be dodged.</summary>
    void FireNinjaStars(Enemy e, Vector3 dir, float dist)
    {
        var t = Tuning;
        var p = Player;
        int count = Math.Clamp(1 + (ReefDepth - 1) / 2, 1, 3);
        Vector3 lead = p.Position + p.Velocity * (dist / t.NinjaShotSpeed) * 0.5f;
        Vector3 aim = MathUtil.SafeNormalize(lead - e.Position, dir);
        Vector3 origin = e.Position + aim * (t.NinjaRadius + 0.15f);
        for (int i = 0; i < count; i++)
        {
            Vector3 d = Rotate(aim, Vector3.UnitY, (i - (count - 1) * 0.5f) * 12f);
            AddProjectile(new Projectile
            {
                Id = NextId(),
                Kind = ProjectileKind.Star,
                FromPlayer = false,
                Position = origin,
                PrevPosition = origin,
                AxisPosition = origin,
                Velocity = d * t.NinjaShotSpeed,
                Radius = t.NinjaShotRadius,
                Damage = t.NinjaShotDamage,
                MaxRange = t.NinjaShotRange,
            });
        }
        Events.Add(new SimEvent(SimEventType.EnemyShotFired, origin, aim, e.Id, count, "star"));
    }
}
