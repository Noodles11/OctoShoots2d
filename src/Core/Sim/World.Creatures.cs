using System;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Items;
using OctoShoots.Core.Terrain;

namespace OctoShoots.Core.Sim;

/// <summary>
/// The Depth 1 den creatures (docs/DEPTH1-BESTIARY.md): they sleep in dens, notice Clementine from a distance that
/// depends on their senses (even beyond the mist), startle for 0.6 s, then act. Beyond their "gives up" range, or when
/// she is hidden in ink, they lose track of her and go home.
/// </summary>
public sealed partial class World
{
    /// <summary>Creatures farther than this from Clementine are not simulated (dens stream in as she approaches).</summary>
    public const float SimRange = 80f;

    const float StartleTime = 0.6f;
    const float FlowRadius = 60f;
    const float LoudTime = 2f;
    const float SneakSpeed = 0.6f;
    const float CrabGravity = 14f;

    /// <summary>The numbers of every den creature; null means the world has no den creatures.</summary>
    public CreatureCatalog? Creatures { get; }

    FlowField? _flow;
    long _flowTick = long.MinValue / 2;
    bool _nearSurface;

    public static bool IsDenCreature(EnemyKind kind) => kind >= EnemyKind.SpanishDancer;

    /// <summary>Is this a creature that holds still on a surface?</summary>
    public static bool IsClinger(EnemyKind kind) => kind is EnemyKind.SeaUrchin or EnemyKind.Moray;

    /// <summary>Visual and hit size multiplier: bed urchins are small, pufferlings swell.</summary>
    public float ScaleOf(Enemy e)
    {
        if (e.Small) return CreatureDef.BedScale;
        if (e.Kind != EnemyKind.Pufferling || Creatures is null) return 1f;
        const float Swollen = 2.4f;
        return e.State switch
        {
            EnemyState.Attack => Swollen,
            EnemyState.Telegraph => MathUtil.Lerp(1f, Swollen, 1f - MathUtil.Clamp01(e.StateTimer / Creatures[e.Kind].Telegraph)),
            _ => 1f,
        };
    }

    // ───────────────────────── setting up ─────────────────────────

    void SetUpDens()
    {
        if (Creatures is null) return;
        var plan = Cave.Loot;
        var rng = _streams.Dens(plan.Depth, plan.Reef);
        var healthy = _streams.Critters(plan.Depth, plan.Reef);
        for (int g = 0; g < Cave.Dens.Count; g++)
        {
            var den = Cave.Dens[g];
            var d = Creatures[den.Kind];
            for (int k = 0; k < den.Count; k++)
            {
                var e = new Enemy
                {
                    Id = NextId(),
                    Kind = den.Kind,
                    Group = g,
                    Anchor = den.Position,
                    Up = den.Up,
                    Small = den.Bed,
                    Phase = rng.Range(0f, MathF.Tau),
                    Position = den.Position,
                };
                float r = d.Radius * (den.Bed ? CreatureDef.BedScale : 1f);
                e.Position = den.Kind switch
                {
                    EnemyKind.SeaUrchin => SurfaceSpot(den.Position, den.Up, rng, den.Bed ? 1.4f : 0f, r),
                    EnemyKind.Moray => den.Position + den.Up * 0.35f,
                    EnemyKind.Crabby => den.Position + den.Up * (r + 0.1f),
                    _ => SwimmerSpot(den.Position, rng, den.Count > 1 ? 2.5f : 0f, r),
                };
                if (den.Kind == EnemyKind.SeaUrchin) e.Anchor = e.Position - den.Up * (r * 0.7f);
                ResetCreature(e);
                Enemies.Add(e);
            }
            AddHealthyTwins(den, healthy);
        }
    }

    Vector3 SwimmerSpot(Vector3 center, Rng rng, float spread, float radius)
    {
        for (int i = 0; i < 6; i++)
        {
            Vector3 at = center + rng.InsideUnitSphere() * spread;
            if (Sdf.Sample(at) > radius + 0.3f) return at;
        }
        return center;
    }

    /// <summary>A point on the surface around <paramref name="center"/>, found by dropping a ray against the surface normal.</summary>
    Vector3 SurfaceSpot(Vector3 center, Vector3 up, Rng rng, float spread, float radius)
    {
        if (spread <= 0f) return center + up * (radius * 0.7f);
        Vector3 u = Vector3.Normalize(Vector3.Cross(up, MathF.Abs(up.Y) > 0.9f ? Vector3.UnitX : Vector3.UnitY));
        Vector3 v = Vector3.Cross(up, u);
        for (int i = 0; i < 6; i++)
        {
            float a = rng.Range(0f, MathF.Tau);
            float s = spread * MathF.Sqrt(rng.NextFloat());
            Vector3 from = center + (u * MathF.Cos(a) + v * MathF.Sin(a)) * s + up * 1.5f;
            if (Sdf.Raycast(from, -up, 3f, out float hit)) return from - up * hit + up * (radius * 0.7f);
        }
        return center + up * (radius * 0.7f);
    }

    /// <summary>Puts a creature back in its den, asleep and at full health.</summary>
    void ResetCreature(Enemy e)
    {
        var d = Creatures![e.Kind];
        e.Hp = e.Small ? CreatureDef.BedHp : d.Hp;
        e.State = EnemyState.Idle;
        e.StateTimer = 0f;
        e.AttackCooldown = 1f;
        e.HurtTimer = 0f;
        e.RespawnTimer = 0f;
        e.OffscreenAttack = false;
        e.Velocity = Vector3.Zero;
        e.Aux = 0f;
        e.Aim = e.Up;
        e.Grounded = false;
        e.FrozenTimer = e.BurnTimer = e.PoisonTimer = e.CharmTimer = e.SlowTimer = e.StunTimer = 0f;
        e.Facing = IsClinger(e.Kind) ? e.Up : e.Facing;
        if (e.Kind == EnemyKind.Moray) e.Position = e.Anchor + e.Up * 0.35f;
        e.PrevPosition = e.Position;
    }

    /// <summary>Sends a creature back to where it sleeps (after Clementine dies, the reef resets).</summary>
    void SendHome(Enemy e)
    {
        ResetCreature(e);
        if (IsClinger(e.Kind)) return;
        var d = Creatures![e.Kind];
        Vector3 jitter = new Vector3(MathF.Sin(e.Phase), 0.2f, MathF.Cos(e.Phase)) * 1.5f;
        e.Position = e.Kind == EnemyKind.Crabby ? e.Anchor + e.Up * (d.Radius + 0.1f) : e.Anchor + jitter;
        if (e.Kind != EnemyKind.Crabby && Sdf.Sample(e.Position) < d.Radius) e.Position = e.Anchor;
        e.PrevPosition = e.Position;
    }

    // ───────────────────────── the senses ─────────────────────────

    void UpdateSenses()
    {
        var p = Player;
        p.LoudTimer = MathF.Max(0f, p.LoudTimer - Dt);
        foreach (var ev in Events)
            if (ev.Type is SimEventType.ShotFired or SimEventType.DashStarted or SimEventType.BombDropped or SimEventType.Explosion)
                p.LoudTimer = LoudTime;
        _nearSurface = Sdf.Sample(p.Position) < 3f || Sdf.Raycast(p.Position, -Vector3.UnitY, 6f, out _);
    }

    /// <summary>How far this creature can notice Clementine right now, and whether it needs a clear line of sight.</summary>
    float NoticeRange(CreatureDef d, out bool needsSight)
    {
        float range = d.NoticeRange;
        needsSight = d.Sense == Sense.Sight;
        if (d.Sense == Sense.Vibration)
        {
            range = _nearSurface ? d.NoticeRange : d.NoticeRangeAlt;
            needsSight = !_nearSurface;
        }
        if (Player.LoudTimer > 0f) range *= 2f;
        else if (Player.Velocity.LengthSquared() < SneakSpeed * SneakSpeed) range *= 0.5f;
        return range;
    }

    bool CanNotice(Enemy e, CreatureDef d, float dist)
    {
        float range = NoticeRange(d, out bool needsSight);
        if (dist > range) return false;
        return !needsSight || Sdf.LineOfSight(e.Position, Player.Position);
    }

    // ───────────────────────── paths ─────────────────────────

    void EnsureFlow()
    {
        _flow ??= new FlowField(Sdf);
        if (_flow.Built && Tick - _flowTick < 30 && Vector3.DistanceSquared(_flow.Target, Player.Position) < 9f) return;
        _flow.Build(Player.Position, FlowRadius);
        _flowTick = Tick;
    }

    /// <summary>Which way to swim to reach Clementine: straight when it can see her, around the reef otherwise.</summary>
    Vector3 PathDir(Enemy e, bool los)
    {
        Vector3 straight = MathUtil.SafeNormalize(Player.Position - e.Position, Vector3.UnitY);
        if (los) return straight;
        EnsureFlow();
        var flow = _flow!.Direction(e.Position);
        return flow == Vector3.Zero ? straight : flow;
    }

    // ───────────────────────── the brain ─────────────────────────

    void StepCreature(Enemy e, Vector3 view, float cosView, ref int offscreen)
    {
        var d = Creatures![e.Kind];
        var p = Player;
        var t = Tuning;
        Vector3 to = p.Position - e.Position;
        float dist = MathF.Max(to.Length(), 1e-4f);
        Vector3 dir = to / dist;
        bool blind = p.HiddenTimer > 0f || e.CharmTimer > 0f;

        float speedScale = 1f;
        foreach (var c in Clouds)
            if (Vector3.DistanceSquared(c.Position, e.Position) < c.Radius * c.Radius) speedScale *= t.InkCloudSlow;
        if (e.SlowTimer > 0f) speedScale *= StatusRules.SlowFactor;

        bool bed = e.Small;
        bool los = false;

        if (e.State == EnemyState.Idle)
        {
            Idle(e, d, speedScale);
            if (!bed && !blind && (Tick + e.Id) % 6 == 0 && CanNotice(e, d, dist))
            {
                e.State = EnemyState.Alert;
                e.StateTimer = StartleTime;
                Events.Add(new SimEvent(SimEventType.CreatureNoticed, e.Position, dir, e.Id, dist, e.Kind.ToString()));
            }
        }
        else
        {
            los = dist < 100f && Sdf.LineOfSight(e.Position, p.Position);

            // Lost her: out of range or hidden. Mid-attack creatures finish what they started.
            bool committed = e.State is EnemyState.Telegraph or EnemyState.Attack;
            if (!committed && (dist > d.GiveUpRange || blind))
            {
                e.State = EnemyState.Idle;
                e.Aux = IsClinger(e.Kind) ? e.Aux : 0f;
                Events.Add(new SimEvent(SimEventType.CreatureLost, e.Position, dir, e.Id, dist, e.Kind.ToString()));
            }
            else
            {
                switch (e.Kind)
                {
                    case EnemyKind.SpanishDancer: StepDancer(e, d, dist, dir, los, speedScale, view, cosView, ref offscreen); break;
                    case EnemyKind.Pufferling: StepPufferling(e, d, dist, dir, los, speedScale, view, cosView, ref offscreen); break;
                    case EnemyKind.Barracuda: StepBarracuda(e, d, dist, dir, los, speedScale, view, cosView, ref offscreen); break;
                    case EnemyKind.MoonJelly: StepJelly(e, d, dist, dir, los, speedScale, view, cosView, ref offscreen); break;
                    case EnemyKind.SeaUrchin: StepUrchin(e, d, dist, dir, los, view, cosView, ref offscreen); break;
                    case EnemyKind.Crabby: StepCrabby(e, d, dist, dir, los, speedScale, view, cosView, ref offscreen); break;
                    case EnemyKind.Moray: StepMoray(e, d, dist, dir, los, view, cosView, ref offscreen); break;
                }
            }
        }

        // Startled: stop and look at her; then get on with it.
        if (e.State == EnemyState.Alert)
        {
            AlertPose(e, d, dir, speedScale);
            if (blind) e.State = EnemyState.Idle;
            else if (e.StateTimer <= 0f)
            {
                e.State = EnemyState.Hunt;
                e.AttackCooldown = MathF.Max(e.AttackCooldown, 0.5f);
                e.Aux = e.Kind == EnemyKind.MoonJelly ? _rng.Range(0.5f, 2f) : _rng.Range(2f, 3f);
            }
        }

        // Touch damage.
        if (TouchDangerous(e))
        {
            float reach = RadiusOf(e) + t.PlayerRadius + (e.Kind == EnemyKind.Crabby ? 0.8f : 0f);
            if (dist < reach && e.CharmTimer <= 0f)
            {
                float damage = e.Small ? CreatureDef.BedThorn : d.Damage;
                if (DamagePlayer(damage, e.Position)) p.Velocity += dir * 4f;
            }
        }
    }

    static bool TouchDangerous(Enemy e) => e.Kind switch
    {
        EnemyKind.SpanishDancer or EnemyKind.MoonJelly or EnemyKind.SeaUrchin or EnemyKind.Crabby => true,
        EnemyKind.Pufferling or EnemyKind.Barracuda => e.State == EnemyState.Attack,
        EnemyKind.Moray => e.State is EnemyState.Attack or EnemyState.Recover,
        _ => false,
    };

    bool TryTelegraph(Enemy e, CreatureDef d, Vector3 dir, Vector3 view, float cosView, ref int offscreen)
    {
        bool onScreen = Vector3.Dot(view, -dir) >= cosView;
        if (!onScreen && offscreen >= Tuning.MaxOffscreenAttackers) return false;
        e.State = EnemyState.Telegraph;
        e.StateTimer = d.Telegraph;
        e.OffscreenAttack = !onScreen;
        if (!onScreen) offscreen++;
        Events.Add(new SimEvent(SimEventType.EnemyTelegraph, e.Position, dir, e.Id, d.Telegraph, e.Kind.ToString()));
        return true;
    }

    /// <summary>Pairs and packs take turns: only one of a group winds up at a time.</summary>
    bool GroupBusy(Enemy e)
    {
        foreach (var o in Enemies)
            if (o != e && o.Group == e.Group && o.Kind == e.Kind && o.State is EnemyState.Telegraph or EnemyState.Attack) return true;
        return false;
    }

    void EndAttack(Enemy e, CreatureDef d, float recover)
    {
        e.State = EnemyState.Recover;
        e.StateTimer = recover;
        e.AttackCooldown = d.Cooldown + _rng.Range(0f, 0.8f);
        e.OffscreenAttack = false;
    }

    // ───────────────────────── movement helpers ─────────────────────────

    /// <summary>Swimming: steer to a desired velocity, keeping clear of rock and of each other.</summary>
    void Swim(Enemy e, CreatureDef d, Vector3 desired, float accel, float speedScale = 1f)
    {
        foreach (var other in Enemies)
        {
            if (other == e || !other.Alive || IsClinger(other.Kind) || other.Kind == EnemyKind.Crabby) continue;
            Vector3 away = e.Position - other.Position;
            if (MathF.Abs(away.X) > 1.8f || MathF.Abs(away.Y) > 1.8f || MathF.Abs(away.Z) > 1.8f) continue;
            float dd = away.Length();
            if (dd < 1.6f && dd > 1e-4f) desired += away / dd * (1.6f - dd) * 1.2f;
        }
        SwimBody(ref e.Position, ref e.Velocity, RadiusOf(e), desired * speedScale, accel);
    }

    /// <summary>Any swimming body: steer to a desired velocity, keeping clear of rock and below the surface.</summary>
    void SwimBody(ref Vector3 position, ref Vector3 velocity, float radius, Vector3 desired, float accel)
    {
        float clearance = Sdf.Sample(position);
        if (clearance < 2f) desired += Sdf.Gradient(position) * (2f - clearance) * 1.5f;
        velocity = MathUtil.MoveToward(velocity, desired, accel * Dt);
        MoveSphere(ref position, ref velocity, radius);
        ClampToSurface(ref position, ref velocity, radius);
    }

    /// <summary>Where a swimmer drifts while at rest: a slow loop around its home (wider for barracuda).</summary>
    Vector3 WanderTarget(EnemyKind kind, Vector3 anchor, float phase, out float radius)
    {
        float time = Tick * Dt;
        radius = kind == EnemyKind.Barracuda ? 12f : 3f;
        return anchor + new Vector3(
            MathF.Sin(time * 0.13f + phase),
            0.3f * MathF.Sin(time * 0.2f + phase * 2f),
            MathF.Cos(time * 0.11f + phase * 1.7f)) * radius;
    }

    void Face(Enemy e, Vector3 want, float rate = 6f) =>
        e.Facing = MathUtil.RotateToward(e.Facing, MathUtil.SafeNormalize(want, e.Facing), rate * Dt);

    void FaceTravel(Enemy e, float rate = 6f) => Face(e, e.Velocity, rate);

    /// <summary>At rest in or around its den.</summary>
    void Idle(Enemy e, CreatureDef d, float speedScale)
    {
        switch (e.Kind)
        {
            case EnemyKind.SeaUrchin:
                return;
            case EnemyKind.Moray:
                e.Aux = MathF.Max(0f, e.Aux - 3f * Dt);
                e.Aim = MathUtil.SafeNormalize(Vector3.Lerp(e.Aim, e.Up, 4f * Dt), e.Up);
                e.Position = MorayHead(e);
                e.Velocity = Vector3.Zero;
                return;
            case EnemyKind.Crabby:
                Walk(e, Vector3.Zero, 0f);
                return;
        }

        Vector3 target = WanderTarget(e.Kind, e.Anchor, e.Phase, out float radius);
        Vector3 want = target - e.Position;
        float len = want.Length();
        bool farHome = Vector3.DistanceSquared(e.Position, e.Anchor) > radius * radius * 4f;
        float speed = d.Speed * (farHome ? 0.9f : e.Kind == EnemyKind.Barracuda ? 0.5f : 0.3f);
        Vector3 desired = len > 1e-3f ? want / len * MathF.Min(speed, len * 0.8f + 0.2f) : Vector3.Zero;
        Swim(e, d, desired, 4f, speedScale);
        FaceTravel(e, 3f);
    }

    void AlertPose(Enemy e, CreatureDef d, Vector3 dir, float speedScale)
    {
        switch (e.Kind)
        {
            case EnemyKind.SeaUrchin:
                return;
            case EnemyKind.Moray:
                e.Aux = MathUtil.Lerp(e.Aux, 1f, 6f * Dt);
                e.Aim = MathUtil.SafeNormalize(Vector3.Lerp(e.Aim, ClampToHole(e, dir), 5f * Dt), e.Up);
                e.Position = MorayHead(e);
                return;
            case EnemyKind.Crabby:
                Walk(e, Vector3.Zero, 0f);
                Face(e, new Vector3(dir.X, 0f, dir.Z));
                return;
        }
        e.Velocity = MathUtil.MoveToward(e.Velocity, Vector3.Zero, 10f * Dt);
        float r = RadiusOf(e);
        MoveSphere(ref e.Position, ref e.Velocity, r);
        Face(e, dir, 10f);
    }

    /// <summary>Walking along the ground: steer along the surface, fall when there is none.</summary>
    void Walk(Enemy e, Vector3 wantDir, float speed, float gravity = CrabGravity) =>
        e.Grounded = WalkBody(ref e.Position, ref e.Velocity, RadiusOf(e), wantDir, speed, gravity);

    /// <summary>Any walking body: steer along the ground, fall when there is none. Returns true when on the ground.</summary>
    bool WalkBody(ref Vector3 position, ref Vector3 velocity, float r, Vector3 wantDir, float speed, float gravity = CrabGravity)
    {
        float clearance = Sdf.Sample(position) - r;
        Vector3 n = Sdf.Gradient(position);
        bool grounded = clearance < 0.3f && n.Y > 0.3f;
        Vector3 v = velocity;
        if (grounded)
        {
            Vector3 h = new(wantDir.X, 0f, wantDir.Z);
            h = h.LengthSquared() > 1e-6f ? Vector3.Normalize(h) : Vector3.Zero;
            Vector3 move = h - n * Vector3.Dot(h, n);
            if (n.Y < 0.5f && move.Y > 0f) move.Y = 0f;
            move = MathUtil.SafeNormalize(move, Vector3.Zero) * speed;
            v = MathUtil.MoveToward(v, move - n * 1.5f, 30f * Dt);
        }
        else
        {
            v.Y = MathF.Max(v.Y - gravity * Dt, -9f);
        }
        velocity = v;
        MoveSphere(ref position, ref velocity, r);
        return grounded;
    }

    static Vector3 ClampToHole(Enemy e, Vector3 dir) => ClampToHole(e.Up, dir);

    /// <summary>A moray can only reach out of the front of its hole.</summary>
    static Vector3 ClampToHole(Vector3 holeUp, Vector3 dir)
    {
        float up = Vector3.Dot(dir, holeUp);
        if (up < 0.3f) dir = Vector3.Normalize(dir + holeUp * (0.3f - up));
        return dir;
    }

    static Vector3 MorayHead(Enemy e) => e.Anchor + e.Up * 0.35f + e.Aim * e.Aux;

    // ───────────────────────── projectiles ─────────────────────────

    void FireCreatureShot(Enemy e, CreatureDef d, ProjectileKind kind, Vector3 origin, Vector3 dir)
    {
        AddProjectile(new Projectile
        {
            Id = NextId(),
            Kind = kind,
            FromPlayer = false,
            Position = origin,
            PrevPosition = origin,
            AxisPosition = origin,
            Velocity = dir * d.ShotSpeed,
            Radius = d.ShotRadius,
            Damage = d.ShotDamage,
            MaxRange = d.ShotRange,
        });
    }

    /// <summary>A direction on a spherical cap around <paramref name="up"/>, evenly spread (a Fibonacci spiral).</summary>
    static Vector3 CapDir(int index, int count, float minCos, float roll, Vector3 up)
    {
        float cosT = 1f - (index + 0.5f) / count * (1f - minCos);
        float sinT = MathF.Sqrt(MathF.Max(0f, 1f - cosT * cosT));
        float phi = index * 2.3999632f + roll;
        Vector3 u = Vector3.Normalize(Vector3.Cross(up, MathF.Abs(up.Y) > 0.9f ? Vector3.UnitX : Vector3.UnitY));
        Vector3 v = Vector3.Cross(up, u);
        return up * cosT + (u * MathF.Cos(phi) + v * MathF.Sin(phi)) * sinT;
    }

    // ───────────────────────── Spanish Dancer ─────────────────────────

    void StepDancer(Enemy e, CreatureDef d, float dist, Vector3 dir, bool los, float speedScale, Vector3 view, float cosView, ref int offscreen)
    {
        switch (e.State)
        {
            case EnemyState.Hunt:
            {
                Vector3 path = PathDir(e, los);
                float want = dist > 2.5f ? d.Speed : d.Speed * 0.2f;
                Swim(e, d, path * want, 3f, speedScale);
                FaceTravel(e);
                if (los && e.AttackCooldown <= 0f && dist < d.AttackRange) TryTelegraph(e, d, dir, view, cosView, ref offscreen);
                break;
            }
            case EnemyState.Telegraph:
                Swim(e, d, Vector3.Zero, 6f, speedScale);
                Face(e, dir, 4f);
                if (e.StateTimer <= 0f)
                {
                    FlareBurst(e, d);
                    EndAttack(e, d, d.Recover);
                }
                break;
            case EnemyState.Recover:
                // Spinning: dizzy and open to hits.
                Swim(e, d, Vector3.Zero, 6f, speedScale);
                if (e.StateTimer <= 0f) e.State = EnemyState.Hunt;
                break;
        }
    }

    /// <summary>A ring of slow spores around the dancer, in the horizontal plane.</summary>
    void FlareBurst(Enemy e, CreatureDef d)
    {
        float roll = _rng.Range(0f, MathF.Tau);
        int n = Math.Max(1, d.ShotCount);
        for (int i = 0; i < n; i++)
        {
            float a = roll + i * MathF.Tau / n;
            Vector3 dir = new(MathF.Cos(a), 0f, MathF.Sin(a));
            FireCreatureShot(e, d, ProjectileKind.Spore, e.Position + dir * (d.Radius + 0.2f), dir);
        }
        Events.Add(new SimEvent(SimEventType.EnemyShotFired, e.Position, Vector3.UnitY, e.Id, n, "spore"));
    }

    // ───────────────────────── Pufferling ─────────────────────────

    void StepPufferling(Enemy e, CreatureDef d, float dist, Vector3 dir, bool los, float speedScale, Vector3 view, float cosView, ref int offscreen)
    {
        switch (e.State)
        {
            case EnemyState.Hunt:
            {
                Swim(e, d, PathDir(e, los) * (dist > 1.5f ? d.Speed : 0.5f), 5f, speedScale);
                FaceTravel(e);
                if (los && e.AttackCooldown <= 0f && dist < d.AttackRange) TryTelegraph(e, d, dir, view, cosView, ref offscreen);
                break;
            }
            case EnemyState.Telegraph:
                // Inflating.
                Swim(e, d, dir * d.Speed * 0.3f, 6f, speedScale);
                Face(e, dir, 6f);
                if (e.StateTimer <= 0f)
                {
                    e.State = EnemyState.Attack;
                    e.StateTimer = d.HoldTime;
                    e.OffscreenAttack = false;
                }
                break;
            case EnemyState.Attack:
                // Swollen and chasing; bursts when the time is up (or when a bubble hits it).
                Swim(e, d, PathDir(e, los) * d.BurstSpeed, 6f, speedScale);
                FaceTravel(e);
                if (e.StateTimer <= 0f)
                {
                    SpineBurst(e, d);
                    EndAttack(e, d, d.Recover);
                }
                break;
            case EnemyState.Recover:
                Swim(e, d, Vector3.Zero, 5f, speedScale);
                if (e.StateTimer <= 0f) e.State = EnemyState.Hunt;
                break;
        }
    }

    void SpineBurst(Enemy e, CreatureDef d)
    {
        float roll = _rng.Range(0f, MathF.Tau);
        int n = Math.Max(1, d.ShotCount);
        float r = RadiusOf(e);
        for (int i = 0; i < n; i++)
        {
            Vector3 dir = CapDir(i, n, -1f, roll, Vector3.UnitY);
            FireCreatureShot(e, d, ProjectileKind.Spine, e.Position + dir * (r + 0.1f), dir);
        }
        Events.Add(new SimEvent(SimEventType.EnemyShotFired, e.Position, Vector3.UnitY, e.Id, n, "burst"));
    }

    // ───────────────────────── Barracuda ─────────────────────────

    void StepBarracuda(Enemy e, CreatureDef d, float dist, Vector3 dir, bool los, float speedScale, Vector3 view, float cosView, ref int offscreen)
    {
        switch (e.State)
        {
            case EnemyState.Hunt:
            {
                e.Aux -= Dt;
                if (!los)
                {
                    Swim(e, d, PathDir(e, false) * d.Speed * 1.3f, 5f, speedScale);
                }
                else
                {
                    // Circle her at 12–18 m, sizing her up.
                    Vector3 tangent = MathUtil.SafeNormalize(Vector3.Cross(dir, Vector3.UnitY), Vector3.UnitX) * e.OrbitSign;
                    float radial = Math.Clamp((dist - 15f) * 0.3f, -1f, 1f);
                    Vector3 desired = tangent * d.Speed + dir * radial * d.Speed * 0.8f;
                    desired.Y += Math.Clamp((Player.Position.Y - e.Position.Y) * 0.4f, -2f, 2f);
                    Swim(e, d, desired, 6f, speedScale);
                    if (e.Aux <= 0f && e.AttackCooldown <= 0f && dist < d.AttackRange && !GroupBusy(e))
                        TryTelegraph(e, d, dir, view, cosView, ref offscreen);
                }
                FaceTravel(e, 5f);
                break;
            }
            case EnemyState.Telegraph:
                // Nose pointed at her, quivering.
                Swim(e, d, Vector3.Zero, 10f, speedScale);
                Face(e, dir, 10f);
                if (e.StateTimer <= 0f)
                {
                    e.Aim = dir;
                    e.Aux = 0f;
                    e.State = EnemyState.Attack;
                    e.OffscreenAttack = false;
                    Events.Add(new SimEvent(SimEventType.EnemyShotFired, e.Position, dir, e.Id, d.BurstSpeed, "charge"));
                }
                break;
            case EnemyState.Attack:
            {
                // A straight dash. It does not steer: that is the dodge.
                float speed = d.BurstSpeed * speedScale;
                e.Velocity = e.Aim * speed;
                Vector3 before = e.Position;
                float r = RadiusOf(e);
                MoveSphere(ref e.Position, ref e.Velocity, r);
                e.Aux += Vector3.Distance(before, e.Position);
                e.Facing = e.Aim;
                bool blocked = Vector3.Dot(e.Velocity, e.Aim) < speed * 0.3f;
                if (e.Aux >= d.Reach || blocked)
                {
                    EndAttack(e, d, d.Recover);
                    e.OrbitSign = _rng.NextFloat() < 0.5f ? -1f : 1f;
                }
                break;
            }
            case EnemyState.Recover:
                // Overshot: slowly turns around, a flank window.
                Swim(e, d, e.Aim * d.Speed * 0.3f, 4f, speedScale);
                Face(e, dir, 2f);
                if (e.StateTimer <= 0f)
                {
                    e.State = EnemyState.Hunt;
                    e.Aux = _rng.Range(2f, 3f);
                }
                break;
        }
    }

    // ───────────────────────── Moon jelly ─────────────────────────

    void StepJelly(Enemy e, CreatureDef d, float dist, Vector3 dir, bool los, float speedScale, Vector3 view, float cosView, ref int offscreen)
    {
        switch (e.State)
        {
            case EnemyState.Hunt:
            {
                e.Aux -= Dt;
                float time = Tick * Dt;
                Vector3 bob = new Vector3(MathF.Sin(time * 1.3f + e.Phase), MathF.Sin(time * 0.9f + e.Phase * 2f), MathF.Cos(time * 1.1f + e.Phase)) * 0.6f;
                Swim(e, d, PathDir(e, los) * d.Speed + bob, 3f, speedScale);
                FaceTravel(e, 3f);
                if (e.Aux <= 0f && dist < d.AttackRange) TryTelegraph(e, d, dir, view, cosView, ref offscreen);
                break;
            }
            case EnemyState.Telegraph:
                // The bell contracts.
                Swim(e, d, Vector3.Zero, 6f, speedScale);
                if (e.StateTimer <= 0f)
                {
                    e.Aim = dir;
                    e.State = EnemyState.Attack;
                    e.StateTimer = d.Reach / MathF.Max(0.1f, d.BurstSpeed);
                    e.OffscreenAttack = false;
                }
                break;
            case EnemyState.Attack:
                e.Velocity = e.Aim * d.BurstSpeed * speedScale;
                MoveSphere(ref e.Position, ref e.Velocity, RadiusOf(e));
                if (e.StateTimer <= 0f)
                {
                    e.State = EnemyState.Recover;
                    e.StateTimer = d.Recover;
                    e.Aux = d.Cooldown + _rng.Range(0f, 1.5f);
                }
                break;
            case EnemyState.Recover:
                Swim(e, d, Vector3.Zero, 4f, speedScale);
                if (e.StateTimer <= 0f) e.State = EnemyState.Hunt;
                break;
        }
    }

    // ───────────────────────── Sea urchin ─────────────────────────

    void StepUrchin(Enemy e, CreatureDef d, float dist, Vector3 dir, bool los, Vector3 view, float cosView, ref int offscreen)
    {
        // Beds only hurt when touched.
        if (e.Small) return;
        e.Velocity = Vector3.Zero;
        switch (e.State)
        {
            case EnemyState.Hunt:
                e.Aux = los ? 0f : e.Aux + Dt;
                if (e.Aux > 2f)
                {
                    e.State = EnemyState.Idle;
                    e.Aux = 0f;
                    break;
                }
                if (los && e.AttackCooldown <= 0f && dist < d.ShotRange)
                    TryTelegraph(e, d, dir, view, cosView, ref offscreen);
                break;
            case EnemyState.Telegraph:
                // The spines stand and rattle.
                if (e.StateTimer <= 0f)
                {
                    SpineBloom(e, d);
                    EndAttack(e, d, 0.5f);
                }
                break;
            case EnemyState.Recover:
                if (e.StateTimer <= 0f) e.State = EnemyState.Hunt;
                break;
        }
    }

    void SpineBloom(Enemy e, CreatureDef d)
    {
        float roll = _rng.Range(0f, MathF.Tau);
        int n = Math.Max(1, d.ShotCount);
        for (int i = 0; i < n; i++)
        {
            Vector3 dir = CapDir(i, n, -0.3f, roll, e.Up);
            FireCreatureShot(e, d, ProjectileKind.Spine, e.Position + dir * (d.Radius + 0.1f), dir);
        }
        Events.Add(new SimEvent(SimEventType.EnemyShotFired, e.Position, e.Up, e.Id, n, "bloom"));
    }

    // ───────────────────────── Crabby ─────────────────────────

    void StepCrabby(Enemy e, CreatureDef d, float dist, Vector3 dir, bool los, float speedScale, Vector3 view, float cosView, ref int offscreen)
    {
        Vector3 flat = new(dir.X, 0f, dir.Z);
        float flatDist = flat.Length();
        float above = Player.Position.Y - e.Position.Y;
        switch (e.State)
        {
            case EnemyState.Hunt:
            {
                Vector3 path = los ? flat : PathDir(e, false);
                float speed = flatDist > 1.2f ? d.Speed * speedScale : 0f;
                Walk(e, path, speed);
                Face(e, flat, 8f);
                if (e.AttackCooldown <= 0f && los && dist < d.AttackRange && above > -0.5f && above < d.Reach + 2f && e.Grounded)
                    TryTelegraph(e, d, dir, view, cosView, ref offscreen);
                break;
            }
            case EnemyState.Telegraph:
                // Crouches.
                Walk(e, Vector3.Zero, 0f);
                Face(e, flat, 10f);
                if (e.StateTimer <= 0f)
                {
                    float jump = MathF.Sqrt(2f * CrabGravity * MathF.Max(1f, d.Reach));
                    float horizontal = MathF.Min(d.BurstSpeed * 0.5f, flatDist * 2f);
                    Vector3 h = flatDist > 1e-3f ? flat / flatDist : Vector3.Zero;
                    e.Velocity = h * horizontal + Vector3.UnitY * jump;
                    e.Grounded = false;
                    e.Aux = 0f;
                    e.State = EnemyState.Attack;
                    e.OffscreenAttack = false;
                    Events.Add(new SimEvent(SimEventType.EnemyShotFired, e.Position, Vector3.UnitY, e.Id, jump, "leap"));
                }
                break;
            case EnemyState.Attack:
            {
                // Airborne: it sinks back slowly on the way down.
                e.Aux += Dt;
                Walk(e, Vector3.Zero, 0f, e.Velocity.Y > 0f ? CrabGravity : 5f);
                if (e.Aux > 0.3f && e.Grounded)
                {
                    EndAttack(e, d, d.Recover);
                    e.Velocity = Vector3.Zero;
                }
                break;
            }
            case EnemyState.Recover:
                Walk(e, Vector3.Zero, 0f);
                if (e.StateTimer <= 0f) e.State = EnemyState.Hunt;
                break;
        }
    }

    // ───────────────────────── Moray ─────────────────────────

    void StepMoray(Enemy e, CreatureDef d, float dist, Vector3 dir, bool los, Vector3 view, float cosView, ref int offscreen)
    {
        e.Velocity = Vector3.Zero;
        switch (e.State)
        {
            case EnemyState.Hunt:
            {
                e.Aux = MathUtil.Lerp(e.Aux, 1f, 5f * Dt);
                e.Aim = MathUtil.SafeNormalize(Vector3.Lerp(e.Aim, ClampToHole(e, dir), 4f * Dt), e.Up);
                e.Position = MorayHead(e);
                if (los && e.AttackCooldown <= 0f && dist < d.AttackRange)
                    TryTelegraph(e, d, dir, view, cosView, ref offscreen);
                break;
            }
            case EnemyState.Telegraph:
                // Head sways out, jaw gapes.
                e.Aux = MathUtil.Lerp(e.Aux, 1.4f, 6f * Dt);
                e.Aim = MathUtil.SafeNormalize(Vector3.Lerp(e.Aim, ClampToHole(e, dir), 6f * Dt), e.Up);
                e.Position = MorayHead(e);
                if (e.StateTimer <= 0f)
                {
                    e.Aim = ClampToHole(e, MathUtil.SafeNormalize(Player.Position - e.Anchor, e.Up));
                    e.State = EnemyState.Attack;
                    e.OffscreenAttack = false;
                    Events.Add(new SimEvent(SimEventType.EnemyShotFired, e.Position, e.Aim, e.Id, d.BurstSpeed, "lunge"));
                }
                break;
            case EnemyState.Attack:
            {
                e.Aux += d.BurstSpeed * Dt;
                Vector3 head = MorayHead(e);
                bool rock = e.Aux > 1.5f && Sdf.Sample(head) < 0.1f;
                if (e.Aux >= d.Reach || rock)
                {
                    e.Aux = MathF.Min(e.Aux, d.Reach);
                    EndAttack(e, d, d.Recover);
                }
                e.Position = MorayHead(e);
                break;
            }
            case EnemyState.Recover:
                // Stays out after a miss, then slides back.
                if (e.StateTimer <= 0f)
                {
                    e.Aux = MathF.Max(1f, e.Aux - 6f * Dt);
                    if (e.Aux <= 1.01f) e.State = EnemyState.Hunt;
                }
                e.Position = MorayHead(e);
                break;
        }
        e.Facing = e.Aim;
    }
}
