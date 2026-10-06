using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Items;

namespace OctoShoots.Core.Sim;

/// <summary>Clementine's weapon: volleys, charge pearls, the Sunbeam laser and every shot modifier (3D §4.3).</summary>
public sealed partial class World
{
    public const float PearlChargeTime = 1f;
    public const float LaserChargeTime = 0.8f;
    public const float BeamDuration = 0.4f;
    public const float BeamTickInterval = 0.1f;
    public const float BeamRange = 25f;
    const float SplitAngleDeg = 25f;

    /// <summary>Fire rate after frenzy.</summary>
    public float EffectiveFireRate => Loadout.Stats.FireRate * (Player.FrenzyTimer > 0f ? Player.FrenzyMult : 1f);

    void StepWeapon(in PlayerInput input)
    {
        var p = Player;
        var spec = Loadout.Shot;
        StepBeam();
        RegrowBubbles();

        if (spec.Laser || spec.Charge)
        {
            // Hold to charge, release to fire; one bubble per pearl or beam. Faster fire rates charge faster.
            float chargeTime = (spec.Laser ? LaserChargeTime : PearlChargeTime) * (Tuning.FireRate / EffectiveFireRate);
            if (input.Fire && p.BeamTimer <= 0f)
            {
                if (p.Bubbles > 0) p.Charge = MathF.Min(1f, p.Charge + Dt / chargeTime);
                else if (p.Charge == 0f) NoBubbles();
            }
            else if (p.Charge > 0f)
            {
                if (spec.Laser)
                {
                    if (p.Charge >= 0.3f && SpendBubbles(1) > 0) FireBeam(p.Charge);
                }
                else if (SpendBubbles(1) > 0)
                {
                    FireVolley(p.Charge >= 0.15f ? p.Charge : 0f);
                }
                p.Charge = 0f;
            }
            return;
        }

        p.FireCooldown -= Dt;
        if (input.Fire)
        {
            if (p.FireCooldown <= 0f)
            {
                // One throw releases bubbles-per-throw bubbles at once, fanned slightly.
                int n = SpendBubbles((int)Loadout.Stats[Stat.BubblesPerThrow]);
                if (n == 0) NoBubbles();
                for (int i = 0; i < n; i++) FireVolley(0f, (i - (n - 1) * 0.5f) * 4f);
                float interval = 1f / EffectiveFireRate;
                p.FireCooldown += interval;
                if (p.FireCooldown < 0f) p.FireCooldown = interval;
            }
        }
        else if (p.FireCooldown < 0f)
        {
            p.FireCooldown = 0f;
        }
    }

    /// <summary>Bubbles regrow one by one on the empty suckers, after a short pause since the last throw.</summary>
    void RegrowBubbles()
    {
        var p = Player;
        int capacity = BubbleCapacity;
        if (p.Bubbles >= capacity)
        {
            p.Bubbles = capacity;
            p.RegrowProgress = 0f;
            return;
        }
        if (p.RegrowDelay > 0f)
        {
            p.RegrowDelay -= Dt;
            return;
        }
        p.RegrowProgress += Dt * Loadout.Stats[Stat.BubbleRegrow] / Tuning.BubbleRegrowInterval;
        if (p.RegrowProgress < 1f) return;
        p.RegrowProgress = 0f;
        p.Bubbles++;
    }

    /// <summary>Takes up to <paramref name="wanted"/> bubbles off the tentacle; returns how many were taken.</summary>
    int SpendBubbles(int wanted)
    {
        var p = Player;
        int n = Math.Min(wanted, p.Bubbles);
        if (n <= 0) return 0;
        p.Bubbles -= n;
        p.RegrowDelay = Tuning.BubbleRegrowDelay;
        p.RegrowProgress = 0f;
        return n;
    }

    void NoBubbles()
    {
        Player.FireCooldown = MathF.Max(Player.FireCooldown, 0.25f); // don't spam the empty click
        Events.Add(new SimEvent(SimEventType.BubblesEmpty, ThrowOrigin(), Player.Forward));
    }

    /// <summary>One bubble's worth of shots: every shot of the pattern, plus the rear shot.</summary>
    void FireVolley(float charge, float yawOffsetDeg = 0f)
    {
        var p = Player;
        var spec = Loadout.Shot;
        Vector3 eye = p.Position;
        Vector3 forward = p.Forward;
        Vector3 right = MathUtil.Right(p.Yaw);
        Vector3 up = CameraUp();
        Vector3 origin = ThrowOrigin();

        Vector3 aimPoint = ReticlePoint(eye, forward, 60f);
        var (cone, _, _) = Tuning.AimProfile;
        if (cone > 0f && FindAssistTarget(eye, forward, cone) is { } target) aimPoint = target.Position;
        Vector3 aim = (aimPoint - origin).Length() < 0.6f ? forward : MathUtil.SafeNormalize(aimPoint - origin, forward);
        aim = Rotate(aim, up, yawOffsetDeg);

        int count = Math.Max(1, spec.Multishot + (p.GlowBurstTimer > 0f ? p.GlowBurstShots : 0));
        var shots = new List<(Vector3 Dir, HelixMode Helix, float Phase)>();
        HelixMode helix = spec.Wave ? HelixMode.Wave : spec.Spiral ? HelixMode.Spiral : HelixMode.None;

        if (spec.Wave)
        {
            // Double helix: all shots share the aim line, spread evenly around it in phase.
            int n = Math.Max(count, 2);
            for (int i = 0; i < n; i++) shots.Add((aim, HelixMode.Wave, MathF.Tau * i / n));
        }
        else if (spec.Pattern == ShotPattern.Cone && count > 1)
        {
            shots.Add((aim, helix, 0f));
            for (int k = 0; k < count - 1; k++)
            {
                float a = MathF.Tau * k / (count - 1);
                Vector3 axis = up * MathF.Cos(a) + right * MathF.Sin(a);
                shots.Add((Rotate(aim, axis, spec.SpreadDeg), helix, a));
            }
        }
        else
        {
            for (int i = 0; i < count; i++)
                shots.Add((Rotate(aim, up, (i - (count - 1) * 0.5f) * spec.SpreadDeg), helix, MathF.Tau * i / count));
        }

        if (spec.Pattern == ShotPattern.Kraken8)
        {
            // Six view axes and the two forward diagonals (3D §4.3); the forward one is already in the volley.
            foreach (var d in new[] { -forward, right, -right, up, -up, Vector3.Normalize(forward + right), Vector3.Normalize(forward - right) })
                shots.Add((d, helix, 0f));
        }

        int firstId = -1;
        foreach (var (dir, mode, phase) in shots)
        {
            int id = SpawnPlayerShot(origin, dir, charge, rear: false, mode, phase);
            if (firstId < 0) firstId = id;
        }
        if (spec.Rear) SpawnPlayerShot(origin, -forward, charge, rear: true, helix, 0f);

        Events.Add(new SimEvent(SimEventType.ShotFired, origin, aim, firstId, charge, charge > 0f ? "pearl" : null));
    }

    int SpawnPlayerShot(Vector3 origin, Vector3 dir, float charge, bool rear, HelixMode helix, float phase)
    {
        var t = Tuning;
        var stats = Loadout.Stats;
        var spec = Loadout.Shot;
        var (cone, bend, _) = t.AimProfile;
        bool pearl = charge > 0f;

        float damage = stats.Damage * spec.DamageMult * (pearl ? 1f + 2f * charge : 1f);
        float radius = t.ShotRadius * stats[Stat.ShotSize] * spec.SizeMult * (pearl ? 1f + charge : 1f);
        var shot = new Projectile
        {
            Id = NextId(),
            Kind = pearl ? ProjectileKind.Pearl : ProjectileKind.Bubble,
            FromPlayer = true,
            Position = origin,
            PrevPosition = origin,
            AxisPosition = origin,
            Velocity = dir * (t.ShotSpeedScale * stats.ShotSpeed) + Player.Velocity * t.InheritVelocity,
            Radius = radius,
            BaseRadius = radius,
            Damage = damage,
            BaseDamage = damage,
            MaxRange = stats.Range * t.RangeScale * (pearl ? 1.3f : 1f),
            BendDegPerSec = bend,
            ConeDeg = cone,
            HasMods = true,
            Homing = spec.Homing,
            HomingDegPerSec = Loadout.Has("will_o_wisp") ? 480f : 240f,
            Pierce = spec.Pierce,
            Spectral = spec.Spectral,
            BouncesLeft = spec.Bounces,
            CanSplit = spec.Split,
            Boomerang = spec.Boomerang,
            Grow = spec.Grow,
            Explosive = spec.Explosive || (rear && Loadout.Has("broadside")),
            Rear = rear,
            Charge = charge,
            PoisonChance = spec.PoisonChance,
            Helix = helix,
            HelixPhase = phase,
        };
        if (shot.Pierce || shot.Boomerang) shot.HitEnemies = new HashSet<int>();
        AddProjectile(shot);
        return shot.Id;
    }

    static Vector3 Rotate(Vector3 v, Vector3 axis, float degrees)
    {
        if (degrees == 0f || axis.LengthSquared() < 1e-8f) return v;
        var q = Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), degrees * MathUtil.Deg2Rad);
        return Vector3.Normalize(Vector3.Transform(v, q));
    }

    // ───────────────────────── laser ─────────────────────────

    void FireBeam(float charge)
    {
        Player.BeamTimer = BeamDuration * charge;
        Player.BeamTickTimer = 0f;
        Player.BeamSweep = 0f;
        Events.Add(new SimEvent(SimEventType.ShotFired, ThrowOrigin(), Player.Forward, -1, charge, "beam"));
    }

    void StepBeam()
    {
        var p = Player;
        if (p.BeamTimer <= 0f) return;
        p.BeamTimer -= Dt;
        p.BeamSweep += Dt;

        Vector3 dir = p.Forward;
        if (Loadout.Has("lighthouse"))
        {
            // The beam sweeps a rotating cone around the reticle.
            float a = p.BeamSweep * MathF.Tau * 1.5f;
            Vector3 axis = CameraUp() * MathF.Cos(a) + MathUtil.Right(p.Yaw) * MathF.Sin(a);
            dir = Rotate(dir, axis, 9f);
        }
        Vector3 origin = ThrowOrigin();
        Sdf.Raycast(origin, dir, BeamRange, out float length);
        Events.Add(new SimEvent(SimEventType.BeamTick, origin, dir, -1, length, "beam"));

        p.BeamTickTimer -= Dt;
        if (p.BeamTickTimer > 0f) return;
        p.BeamTickTimer += BeamTickInterval;
        if (length < BeamRange - 0.05f) Carve(origin + dir * length, Tuning.BeamCraterRadius); // beams burn rock slowly
        float damage = Loadout.Stats.Damage * Loadout.Shot.DamageMult * 0.9f;
        foreach (var e in EnemiesAlongRay(origin, dir, length, 0.2f))
            ApplyHit(e, damage, dir, e.Position, Loadout.Shot.PoisonChance, 1f);
    }

    List<Enemy> EnemiesAlongRay(Vector3 origin, Vector3 dir, float length, float width)
    {
        var hits = new List<Enemy>();
        foreach (var e in Enemies)
        {
            if (!e.Alive) continue;
            float r = RadiusOf(e) + width;
            float along = Vector3.Dot(e.Position - origin, dir);
            if (along < 0f || along > length) continue;
            if (Vector3.DistanceSquared(origin + dir * along, e.Position) <= r * r) hits.Add(e);
        }
        return hits;
    }

    // ───────────────────────── hits, statuses, splash ─────────────────────────

    bool Roll(float chance) => chance > 0f && _combat.NextFloat() < StatusRules.WithLuck(chance, Loadout.Stats.Luck);

    /// <summary>A modded hit from Clementine: crit, statuses, damage, chain lightning.</summary>
    void ApplyHit(Enemy e, float damage, Vector3 dir, Vector3 at, float poisonChance, float poisonBoost)
    {
        var spec = Loadout.Shot;
        bool wasFrozen = e.FrozenTimer > 0f;

        if (Roll(spec.CritChance))
        {
            damage *= spec.CritMult;
            Events.Add(new SimEvent(SimEventType.EnemyCrit, at, dir, e.Id, damage));
        }
        if (Roll(spec.FreezeChance)) Inflict(e, "freeze", () => e.FrozenTimer = StatusRules.FreezeTime);
        bool burned = Roll(spec.BurnChance);
        if (burned) Inflict(e, "burn", () =>
        {
            e.BurnTimer = StatusRules.BurnTime;
            e.BurnDps = MathF.Max(e.BurnDps, damage * StatusRules.BurnDpsFactor);
        });
        if (Roll(poisonChance)) Inflict(e, "poison", () =>
        {
            e.PoisonTimer = StatusRules.PoisonTime;
            e.PoisonDps = MathF.Max(e.PoisonDps, damage * StatusRules.PoisonDpsFactor * poisonBoost);
        });
        if (Roll(spec.CharmChance)) Inflict(e, "charm", () => e.CharmTimer = StatusRules.CharmTime);
        if (Roll(spec.SlowChance)) Inflict(e, "slow", () => e.SlowTimer = StatusRules.SlowTime);

        if (wasFrozen && burned && Loadout.Has("steam_vent"))
        {
            e.FrozenTimer = 0f;
            Explode(e.Position, 2.5f, damage * 2f + 4f, "steam");
        }

        DamageEnemy(e, damage, dir, Tuning.ShotKnockback * Loadout.Stats[Stat.Knockback]);
        if (spec.ChainTargets > 0) Chain(e.Position, e, spec.ChainTargets, spec.ChainRange, damage * spec.ChainDamage);
    }

    void Inflict(Enemy e, string status, Action apply)
    {
        if (!e.Alive) return;
        apply();
        if (e.Disabled || e.CharmTimer > 0f)
        {
            if (e.State == EnemyState.Telegraph) e.State = EnemyState.Hunt;
            e.AttackCooldown = MathF.Max(e.AttackCooldown, 0.8f);
        }
        Events.Add(new SimEvent(SimEventType.StatusApplied, e.Position, Vector3.Zero, e.Id, 0f, status));
    }

    /// <summary>Area damage that never hurts Clementine (2D §17).</summary>
    void Explode(Vector3 at, float radius, float damage, string? tag = null)
    {
        Events.Add(new SimEvent(SimEventType.Explosion, at, Vector3.UnitY, -1, radius, tag));
        OpenChestsInBlast(at, radius);
        foreach (var e in Enemies)
        {
            if (!e.Alive) continue;
            Vector3 away = e.Position - at;
            if (away.Length() > radius + RadiusOf(e)) continue;
            DamageEnemy(e, damage, MathUtil.SafeNormalize(away, Vector3.UnitY), 3f);
        }
        if (Loadout.Has("guided_inkfish"))
            Clouds.Add(new InkCloud { Id = NextId(), Position = at, Radius = 1.6f, Life = 2.5f, MaxLife = 2.5f });
    }

    /// <summary>Electric Eel Tail: lightning jumps to the nearest other creatures.</summary>
    void Chain(Vector3 from, Enemy? exclude, int count, float range, float damage)
    {
        var targets = Enemies
            .Where(e => e.Alive && e != exclude && Vector3.Distance(e.Position, from) <= range)
            .OrderBy(e => Vector3.DistanceSquared(e.Position, from))
            .Take(count)
            .ToList();
        foreach (var e in targets)
        {
            Events.Add(new SimEvent(SimEventType.ChainArc, from, e.Position - from, e.Id, damage));
            DamageEnemy(e, damage, MathUtil.SafeNormalize(e.Position - from, Vector3.UnitY), 0.5f);
        }
    }

    // ───────────────────────── secondary projectiles ─────────────────────────

    /// <summary>Mitosis: two children at ±25° in the plane facing the camera (3D §4.3).</summary>
    void SpawnSplit(Projectile shot, Enemy hit)
    {
        Vector3 dir = MathUtil.SafeNormalize(shot.Velocity, Player.Forward);
        float speed = shot.Velocity.Length();
        bool toxic = Loadout.Has("toxic_bloom");
        foreach (float angle in new[] { -SplitAngleDeg, SplitAngleDeg })
        {
            Vector3 d = Rotate(dir, CameraUp(), angle);
            Vector3 at = hit.Position + d * (Tuning.EnemyRadius + shot.Radius + 0.05f);
            var child = new Projectile
            {
                Id = NextId(),
                Kind = ProjectileKind.Bubble,
                FromPlayer = true,
                Position = at,
                PrevPosition = at,
                AxisPosition = at,
                Velocity = d * speed,
                Radius = shot.Radius * 0.8f,
                BaseRadius = shot.BaseRadius * 0.8f,
                Damage = shot.Damage * 0.5f,
                BaseDamage = shot.BaseDamage * 0.5f,
                MaxRange = shot.MaxRange * 0.5f,
                HasMods = true,
                Homing = shot.Homing,
                HomingDegPerSec = shot.HomingDegPerSec,
                Pierce = shot.Pierce,
                Spectral = shot.Spectral,
                BouncesLeft = shot.BouncesLeft,
                Explosive = shot.Explosive,
                Grow = shot.Grow || Loadout.Has("cell_bloom"),
                PoisonChance = toxic ? MathF.Min(1f, shot.PoisonChance * 2f) : shot.PoisonChance,
                PoisonBoost = toxic ? 2f : 1f,
                HitEnemies = new HashSet<int> { hit.Id },
            };
            AddProjectile(child);
        }
    }

    /// <summary>Plain fragments (spines, pellets, ring pearls): fly straight, deal flat damage.</summary>
    void SpawnFragment(ProjectileKind kind, Vector3 at, Vector3 dir, float damage, float speed, float range, float radius)
    {
        var p = new Projectile
        {
            Id = NextId(),
            Kind = kind,
            FromPlayer = true,
            Position = at,
            PrevPosition = at,
            AxisPosition = at,
            Velocity = dir * speed,
            Radius = radius,
            BaseRadius = radius,
            Damage = damage,
            BaseDamage = damage,
            MaxRange = range,
        };
        AddProjectile(p);
    }

    /// <summary>Fire Urchin Spine and frozen shatter: spines fan out around a point.</summary>
    void SpawnShards(Vector3 at, int count)
    {
        for (int i = 0; i < count; i++)
        {
            float a = MathF.Tau * i / count;
            var dir = Vector3.Normalize(new Vector3(MathF.Cos(a), 0.25f * MathF.Sin(a * 3f), MathF.Sin(a)));
            SpawnFragment(ProjectileKind.Shard, at + dir * 0.3f, dir, Loadout.Stats.Damage * 0.5f, 10f, 4f, 0.15f);
        }
    }

    /// <summary>What happens when one of Clementine's shots ends: explosions and synergy bursts.</summary>
    void PopPlayerShot(Projectile shot)
    {
        shot.Alive = false;
        if (!shot.HasMods) return;
        Vector3 at = shot.Position;
        Vector3 dir = MathUtil.SafeNormalize(shot.Velocity, Player.Forward);
        Vector3 up = CameraUp();
        Vector3 right = Vector3.Normalize(Vector3.Cross(dir, up));

        if (shot.Explosive)
        {
            Explode(at, 1.5f, shot.Damage);
            Carve(at, Tuning.InkCraterRadius);
        }

        if (shot.Kind == ProjectileKind.Pearl && shot.Charge >= 0.9f)
        {
            if (Loadout.Has("pearl_necklace"))
            {
                for (int i = 0; i < 8; i++)
                {
                    float a = MathF.Tau * i / 8;
                    SpawnFragment(ProjectileKind.Pellet, at, right * MathF.Cos(a) + up * MathF.Sin(a), shot.Damage * 0.4f, 9f, 5f, 0.2f);
                }
            }
            if (Loadout.Has("prism_pearl"))
            {
                foreach (var d in new[] { up, -up, right, -right })
                {
                    Sdf.Raycast(at, d, 8f, out float len);
                    Events.Add(new SimEvent(SimEventType.BeamTick, at, d, -1, len, "prism"));
                    foreach (var e in EnemiesAlongRay(at, d, len, 0.2f)) DamageEnemy(e, shot.Damage * 0.5f, d, 1f);
                }
            }
        }

        if (shot.Kind == ProjectileKind.Bubble && Loadout.Has("big_mad_puff"))
        {
            for (int i = 0; i < 5; i++)
            {
                Vector3 d = Rotate(Rotate(dir, up, (i - 2) * 9f), right, (i % 2 == 0 ? 1f : -1f) * 6f);
                SpawnFragment(ProjectileKind.Pellet, at, d, shot.Damage * 0.3f, 12f, 3f, 0.12f);
            }
        }
    }
}
