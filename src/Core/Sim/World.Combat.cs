using System;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Items;

namespace OctoShoots.Core.Sim;

public sealed partial class World
{
    const float AssistMaxDistance = 40f;
    const float HomingRange = 12f;

    void StepEnemies(in PlayerInput input)
    {
        var t = Tuning;
        var p = Player;
        float cosView = MathF.Cos(input.ViewHalfAngleDeg * MathUtil.Deg2Rad);
        Vector3 view = p.Forward;
        bool hidden = p.HiddenTimer > 0f;

        int offscreenActive = 0;
        foreach (var e in Enemies)
            if (e.State == EnemyState.Telegraph && e.OffscreenAttack) offscreenActive++;

        foreach (var e in Enemies)
        {
            if (e.State == EnemyState.Dead)
            {
                e.RespawnTimer -= Dt;
                if (e.RespawnTimer > 0f) continue;
                if (IsFreeable(e.Kind)) continue; // freed for good until she dies
                SpawnEnemy(e, PickRespawnPoint());
                continue;
            }
            if (e.State == EnemyState.Nested)
            {
                StepNestedNinja(e);
                continue;
            }

            // Dens far away sleep: they cost nothing until she comes within range.
            if (IsDenCreature(e.Kind) && e.State == EnemyState.Idle
                && Vector3.DistanceSquared(e.Position, p.Position) > SimRange * SimRange) continue;

            e.HurtTimer -= Dt;
            e.AttackCooldown -= Dt;
            e.StateTimer -= Dt;
            e.FrozenTimer -= Dt;
            e.StunTimer -= Dt;
            e.CharmTimer -= Dt;
            e.SlowTimer -= Dt;
            e.BurnTimer -= Dt;
            e.PoisonTimer -= Dt;

            float dot = (e.BurnTimer > 0f ? e.BurnDps : 0f) + (e.PoisonTimer > 0f ? e.PoisonDps : 0f);
            if (dot > 0f)
            {
                DamageEnemy(e, dot * Dt, Vector3.Zero, 0f, silent: true);
                if (!e.Alive) continue;
            }
            if (e.BurnTimer <= 0f) e.BurnDps = 0f;
            if (e.PoisonTimer <= 0f) e.PoisonDps = 0f;

            if (e.Disabled)
            {
                // Frozen or stunned: drift to a stop, no attacks, harmless to touch.
                if (IsClinger(e.Kind)) continue;
                e.Velocity = MathUtil.MoveToward(e.Velocity, Vector3.Zero, 12f * Dt);
                MoveSphere(ref e.Position, ref e.Velocity, RadiusOf(e));
                continue;
            }

            if (e.Kind == EnemyKind.ClownNinja)
            {
                StepNinja(e, view, cosView, ref offscreenActive);
                continue;
            }
            if (IsDenCreature(e.Kind))
            {
                StepCreature(e, view, cosView, ref offscreenActive);
                continue;
            }

            Vector3 to = p.Position - e.Position;
            float dist = MathF.Max(to.Length(), 1e-4f);
            Vector3 dir = to / dist;
            bool charmed = e.CharmTimer > 0f;
            bool los = !hidden && dist < t.EnemyAggroRange * 1.4f && Sdf.LineOfSight(e.Position, p.Position);

            Vector3 desired = Vector3.Zero;
            float speedScale = 1f;

            switch (e.State)
            {
                case EnemyState.Idle:
                    if (dist < t.EnemyAggroRange && los && !charmed) e.State = EnemyState.Hunt;
                    break;

                case EnemyState.Hunt:
                    if (dist > t.EnemyAggroRange * 1.4f || hidden || charmed)
                    {
                        e.State = EnemyState.Idle;
                        break;
                    }
                    if (!los)
                    {
                        desired = dir; // No pathing yet: flow fields arrive in step 4.
                        break;
                    }

                    float radial = dist > t.EnemyPreferredDistance + 1.5f ? 1f : dist < t.EnemyPreferredDistance - 1.5f ? -1f : 0f;
                    Vector3 tangent = MathUtil.SafeNormalize(Vector3.Cross(dir, Vector3.UnitY), Vector3.UnitX);
                    desired = dir * radial + tangent * (e.OrbitSign * 0.6f);
                    desired.Y += Math.Clamp((p.Position.Y + 0.5f - e.Position.Y) * 0.3f, -0.5f, 0.5f);
                    if (_rng.NextFloat() < 0.004f) e.OrbitSign = -e.OrbitSign;

                    if (e.AttackCooldown <= 0f && dist < t.EnemyAttackRange)
                    {
                        bool onScreen = Vector3.Dot(view, -dir) >= cosView;
                        if (onScreen || offscreenActive < t.MaxOffscreenAttackers)
                        {
                            e.State = EnemyState.Telegraph;
                            e.StateTimer = t.EnemyTelegraph;
                            e.OffscreenAttack = !onScreen;
                            if (!onScreen) offscreenActive++;
                            Events.Add(new SimEvent(SimEventType.EnemyTelegraph, e.Position, dir, e.Id, t.EnemyTelegraph));
                        }
                    }
                    break;

                case EnemyState.Telegraph:
                    speedScale = 0.25f;
                    if (e.StateTimer <= 0f)
                    {
                        FireEnemyShot(e, dir);
                        e.State = EnemyState.Recover;
                        e.StateTimer = 0.4f;
                        e.AttackCooldown = t.EnemyAttackCooldown + _rng.Range(0f, 0.8f);
                        e.OffscreenAttack = false;
                    }
                    break;

                case EnemyState.Recover:
                    desired = -dir * 0.3f;
                    if (e.StateTimer <= 0f) e.State = EnemyState.Hunt;
                    break;
            }

            // Keep clear of rock and of each other.
            float clearance = Sdf.Sample(e.Position);
            if (clearance < 2f) desired += Sdf.Gradient(e.Position) * (2f - clearance) * 1.2f;
            foreach (var other in Enemies)
            {
                if (other == e || !other.Alive) continue;
                Vector3 away = e.Position - other.Position;
                float d = away.Length();
                if (d < 1.6f && d > 1e-4f) desired += away / d * (1.6f - d);
            }

            foreach (var c in Clouds)
                if (Vector3.DistanceSquared(c.Position, e.Position) < c.Radius * c.Radius) speedScale *= t.InkCloudSlow;
            if (e.SlowTimer > 0f) speedScale *= StatusRules.SlowFactor;

            if (desired.Length() > 1f) desired = Vector3.Normalize(desired);
            e.Velocity = MathUtil.MoveToward(e.Velocity, desired * t.EnemySpeed * speedScale, t.EnemyAccel * Dt);
            MoveSphere(ref e.Position, ref e.Velocity, t.EnemyRadius);
            ClampToSurface(ref e.Position, ref e.Velocity, t.EnemyRadius);

            Vector3 wantFacing = (e.State == EnemyState.Telegraph || e.State == EnemyState.Hunt) && !charmed
                ? dir
                : MathUtil.SafeNormalize(e.Velocity, e.Facing);
            e.Facing = MathUtil.RotateToward(e.Facing, wantFacing, 5f * Dt);

            if (!charmed && dist < t.EnemyRadius + t.PlayerRadius && DamagePlayer(t.EnemyContactDamage, e.Position))
                p.Velocity += dir * 3f;
        }
    }

    /// <summary>A spawn 10–35 m from Clementine if there is one (out of her face, but close enough to matter).</summary>
    Vector3 PickRespawnPoint()
    {
        var spawns = Cave.EnemySpawns;
        var near = spawns.Where(s => Vector3.Distance(s, Player.Position) is >= 10f and <= 35f).ToArray();
        if (near.Length > 0) return near[_rng.Int(near.Length)];
        for (int attempt = 0; attempt < 8; attempt++)
        {
            Vector3 s = spawns[_rng.Int(spawns.Length)];
            if (Vector3.Distance(s, Player.Position) > 8f) return s;
        }
        return spawns[_rng.Int(spawns.Length)];
    }

    void FireEnemyShot(Enemy e, Vector3 dir)
    {
        var t = Tuning;
        Vector3 origin = e.Position + dir * (t.EnemyRadius + 0.1f);
        var shot = new Projectile
        {
            Id = NextId(),
            Kind = ProjectileKind.Orb,
            FromPlayer = false,
            Position = origin,
            PrevPosition = origin,
            AxisPosition = origin,
            Velocity = dir * t.EnemyShotSpeed * Loadout.Stats[Stat.EnemyShotSpeed],
            Radius = t.EnemyShotRadius,
            Damage = t.EnemyShotDamage,
            MaxRange = t.EnemyShotRange,
        };
        AddProjectile(shot);
        Events.Add(new SimEvent(SimEventType.EnemyShotFired, origin, dir, e.Id));
    }

    void StepProjectiles()
    {
        foreach (var shot in Projectiles)
        {
            if (!shot.Alive) continue;
            shot.Age += Dt;
            if (shot.FromPlayer) StepPlayerShot(shot);
            else StepEnemyShot(shot);
        }
        Projectiles.RemoveAll(s => !s.Alive);
    }

    void StepEnemyShot(Projectile shot)
    {
        var t = Tuning;
        var p = Player;
        Vector3 step = shot.Velocity * Dt;
        shot.Position += step;
        shot.Traveled += step.Length();
        Vector3 dir = MathUtil.SafeNormalize(shot.Velocity, Vector3.UnitZ);

        float r = t.PlayerHurtRadius + shot.Radius;
        if (Vector3.DistanceSquared(p.Position, shot.Position) <= r * r && p.DashInvulnerableTimer <= 0f)
        {
            DamagePlayer(shot.Damage, shot.Position);
            shot.Alive = false;
            return;
        }
        if (Sdf.Sample(shot.Position) < 0.08f)
        {
            Events.Add(new SimEvent(SimEventType.ShotHitTerrain, shot.Position, Sdf.Gradient(shot.Position), shot.Id, 0f, shot.Kind switch { ProjectileKind.Star => "star", ProjectileKind.Spore => "spore", ProjectileKind.Spine => "spine", _ => null }));
            shot.Alive = false;
            return;
        }
        if (shot.Traveled >= shot.MaxRange || shot.Position.Y > Cave.SurfaceY)
        {
            Events.Add(new SimEvent(SimEventType.ShotExpired, shot.Position, dir, shot.Id, 0f));
            shot.Alive = false;
        }
    }

    void StepPlayerShot(Projectile shot)
    {
        var p = Player;
        float speed = shot.Velocity.Length();
        Vector3 dir = MathUtil.SafeNormalize(shot.Velocity, p.Forward);

        // Steering: boomerang return, homing, or the aim-assist bend.
        if (shot.Boomerang)
        {
            if (!shot.Returning && shot.Traveled >= shot.MaxRange * 0.5f)
            {
                shot.Returning = true;
                if (Loadout.Has("tuna_rang"))
                {
                    shot.HitEnemies?.Clear();
                    shot.BaseRadius *= 1.5f;
                    shot.Radius *= 1.5f;
                }
            }
            if (shot.Returning)
            {
                Vector3 home = p.Position - shot.AxisPosition;
                if (home.Length() < 0.6f)
                {
                    Events.Add(new SimEvent(SimEventType.ShotReturned, shot.Position, dir, shot.Id));
                    shot.Alive = false;
                    return;
                }
                dir = MathUtil.RotateToward(dir, Vector3.Normalize(home), 540f * MathUtil.Deg2Rad * Dt);
            }
        }
        if (!shot.Returning)
        {
            if (shot.Homing && FindHomingTarget(shot.AxisPosition, dir) is { } target)
                dir = MathUtil.RotateToward(dir, Vector3.Normalize(target.Position - shot.AxisPosition), shot.HomingDegPerSec * MathUtil.Deg2Rad * Dt);
            else if (shot.BendDegPerSec > 0f && FindTargetInCone(shot.AxisPosition, dir, shot.ConeDeg, requireSight: false) is { } bendTarget)
                dir = MathUtil.RotateToward(dir, Vector3.Normalize(bendTarget.Position - shot.AxisPosition), shot.BendDegPerSec * MathUtil.Deg2Rad * Dt);
        }
        shot.Velocity = dir * speed;

        Vector3 step = shot.Velocity * Dt;
        shot.AxisPosition += step;
        shot.Traveled += step.Length();
        shot.Position = shot.AxisPosition + HelixOffset(shot, dir);

        if (shot.Grow)
        {
            float f = 1f + MathF.Min(1f, shot.Traveled / shot.MaxRange);
            shot.Radius = shot.BaseRadius * f;
            shot.Damage = shot.BaseDamage * f;
        }

        // Chests burst open when hit.
        if (ChestAt(shot.Position, shot.Radius) is { } chest)
        {
            OpenChest(chest);
            Events.Add(new SimEvent(SimEventType.ShotHitTerrain, shot.Position, -dir, shot.Id, 1f, KindTag(shot)));
            PopPlayerShot(shot);
            return;
        }

        // Creatures.
        foreach (var e in Enemies)
        {
            if (!e.Alive || shot.HitEnemies?.Contains(e.Id) == true) continue;
            float r = RadiusOf(e) + shot.Radius;
            if (Vector3.DistanceSquared(e.Position, shot.Position) > r * r) continue;

            if (shot.HasMods) ApplyHit(e, shot.Damage, dir, shot.Position, shot.PoisonChance, shot.PoisonBoost);
            else DamageEnemy(e, shot.Damage, dir, Tuning.ShotKnockback * 0.5f);
            Events.Add(new SimEvent(SimEventType.ShotHitEnemy, shot.Position, dir, e.Id, shot.Damage));

            if (shot.CanSplit) SpawnSplit(shot, e);
            if (shot.Pierce)
            {
                shot.HitEnemies?.Add(e.Id);
                continue;
            }
            PopPlayerShot(shot);
            return;
        }

        // Rock: bounce or splat. Spectral shots pass through.
        if (!shot.Spectral && Sdf.Sample(shot.Position) < 0.08f)
        {
            Vector3 n = Sdf.Gradient(shot.Position);
            if (shot.BouncesLeft > 0)
            {
                shot.BouncesLeft--;
                Vector3 reflected = dir - 2f * Vector3.Dot(dir, n) * n;
                shot.Velocity = reflected * speed;
                shot.AxisPosition += n * 0.15f;
                shot.Position += n * 0.15f;
                Events.Add(new SimEvent(SimEventType.ShotBounced, shot.Position, n, shot.Id));
                if (Loadout.Has("pinball_storm")) Chain(shot.Position, null, 2, 4f, shot.Damage * 0.5f);
            }
            else
            {
                Events.Add(new SimEvent(SimEventType.ShotHitTerrain, shot.Position, n, shot.Id, 1f, KindTag(shot)));
                if (shot.Kind == ProjectileKind.Pearl && shot.Charge >= 0.5f) Carve(shot.Position, Tuning.PearlCraterRadius);
                PopPlayerShot(shot);
                return;
            }
        }

        float maxTravel = shot.Boomerang ? shot.MaxRange * 2.2f : shot.MaxRange;
        if (shot.Traveled >= maxTravel || shot.Position.Y > Cave.SurfaceY)
        {
            Events.Add(new SimEvent(SimEventType.ShotExpired, shot.Position, dir, shot.Id, 1f, KindTag(shot)));
            PopPlayerShot(shot);
        }
    }

    static string? KindTag(Projectile shot) => shot.Kind == ProjectileKind.Bubble ? null : shot.Kind.ToString().ToLowerInvariant();

    /// <summary>Wave shots trace a tight double helix; spiral shots widen as they fly.</summary>
    Vector3 HelixOffset(Projectile shot, Vector3 dir)
    {
        if (shot.Helix == HelixMode.None) return Vector3.Zero;
        Vector3 u = Vector3.Normalize(Vector3.Cross(dir, MathF.Abs(dir.Y) > 0.95f ? Vector3.UnitX : Vector3.UnitY));
        Vector3 v = Vector3.Cross(dir, u);
        float radius, turnsPerSecond;
        if (shot.Helix == HelixMode.Wave)
        {
            radius = 0.35f;
            turnsPerSecond = 3f;
        }
        else
        {
            radius = MathF.Min(1.2f, shot.Traveled * 0.22f);
            turnsPerSecond = 2f;
        }
        float a = shot.HelixPhase + shot.Age * turnsPerSecond * MathF.Tau;
        return (u * MathF.Cos(a) + v * MathF.Sin(a)) * radius;
    }

    Enemy? FindHomingTarget(Vector3 from, Vector3 dir)
    {
        Enemy? best = null;
        float bestScore = float.MaxValue;
        foreach (var e in Enemies)
        {
            if (!e.Alive) continue;
            Vector3 to = e.Position - from;
            float d = to.Length();
            if (d > HomingRange || d < 0.05f) continue;
            float angle = MathUtil.AngleBetween(dir, to);
            if (angle > 100f * MathUtil.Deg2Rad) continue;
            float score = d * (1f + angle);
            if (score < bestScore)
            {
                bestScore = score;
                best = e;
            }
        }
        return best;
    }

    void DamageEnemy(Enemy e, float damage, Vector3 dir, float knockback, bool silent = false)
    {
        if (!e.Alive) return;
        e.Hp -= damage;
        if (!silent)
        {
            e.HurtTimer = 0.12f;
            e.Velocity += dir * knockback;
            Events.Add(new SimEvent(SimEventType.EnemyHurt, e.Position, dir, e.Id, damage));
        }
        if (e.Hp > 0f)
        {
            // A bubble on a swollen pufferling makes it burst at once.
            if (e.Kind == EnemyKind.Pufferling && e.State == EnemyState.Attack) e.StateTimer = 0f;
            return;
        }

        bool frozen = e.FrozenTimer > 0f;
        e.State = EnemyState.Dead;
        if (IsFreeable(e.Kind))
        {
            // Corrupted sea life is freed, not killed: it carries on healthy (DEPTH1-BESTIARY §8.2).
            e.RespawnTimer = float.PositiveInfinity;
            Free(e, dir, frozen);
        }
        else
        {
            e.RespawnTimer = Tuning.EnemyRespawnDelay;
            Events.Add(new SimEvent(SimEventType.EnemyDied, e.Position, dir, e.Id, 0f, frozen ? "shatter" : null));
        }
        if (frozen) SpawnShards(e.Position, 4); // frozen foes shatter into shards (2D §9.3)
        DropLoot(e);
        OnEnemyKilled(e);
    }

    void StepClouds()
    {
        foreach (var c in Clouds) c.Life -= Dt;
        Clouds.RemoveAll(c => c.Life <= 0f);
    }

    /// <summary>Aim-assist magnetism: the visible enemy closest (by angle) to the view ray, inside the cone.</summary>
    public Enemy? FindAssistTarget(Vector3 eye, Vector3 dir, float coneDeg) =>
        FindTargetInCone(eye, dir, coneDeg, requireSight: true);

    Enemy? FindTargetInCone(Vector3 from, Vector3 dir, float coneDeg, bool requireSight)
    {
        Enemy? best = null;
        float bestAngle = coneDeg * MathUtil.Deg2Rad;
        foreach (var e in Enemies)
        {
            if (!e.Alive) continue;
            Vector3 to = e.Position - from;
            float dist = to.Length();
            if (dist < 0.3f || dist > AssistMaxDistance) continue;
            float angle = MathUtil.AngleBetween(dir, to);
            if (angle > bestAngle) continue;
            if (requireSight && !Sdf.LineOfSight(from, e.Position)) continue;
            best = e;
            bestAngle = angle;
        }
        return best;
    }

    /// <summary>The point under the reticle: the first enemy or rock along the view ray.</summary>
    public Vector3 ReticlePoint(Vector3 eye, Vector3 dir, float maxDist)
    {
        Sdf.Raycast(eye, dir, maxDist, out float hit);
        if (RayEnemy(eye, dir, hit, 1f, out float enemyHit, out _)) hit = enemyHit;
        return eye + dir * hit;
    }

    /// <summary>True when the reticle rests on a visible enemy (drives the sensitivity slowdown).</summary>
    public bool ReticleOverEnemy(Vector3 eye, Vector3 dir)
    {
        Sdf.Raycast(eye, dir, AssistMaxDistance, out float wall);
        return RayEnemy(eye, dir, wall, 1.15f, out _, out _);
    }

    bool RayEnemy(Vector3 origin, Vector3 dir, float maxDist, float inflate, out float hitDist, out Enemy? hitEnemy)
    {
        hitDist = maxDist;
        hitEnemy = null;
        foreach (var e in Enemies)
        {
            if (!e.Alive) continue;
            float r = RadiusOf(e) * inflate;
            Vector3 oc = origin - e.Position;
            float b = Vector3.Dot(oc, dir);
            float c = oc.LengthSquared() - r * r;
            float disc = b * b - c;
            if (disc < 0f) continue;
            float tHit = -b - MathF.Sqrt(disc);
            if (tHit < 0f || tHit >= hitDist) continue;
            hitDist = tHit;
            hitEnemy = e;
        }
        return hitEnemy is not null;
    }
}
