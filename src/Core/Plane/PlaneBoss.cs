using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Core.Plane;

/// <summary>Queen Clam, the Depth 1 boss of the plane game, and her arena (DESIGN-TOPDOWN §6.4).</summary>
public static class PlaneBossTuning
{
    public const string Name = "Queen Clam";
    public const float Hp = 800f;
    /// <summary>Her shell, as a solid disc on the plane (5 m across).</summary>
    public const float BodyRadius = 2.5f;
    public const float ContactDamage = 18f, ContactKnockback = 9f;
    /// <summary>
    /// Her weak spot: the black pearl in her middle, this wide. Only while her shell is open can her light reach it
    /// (over the raised lid); everything else on her is shell, and shots break harmlessly on it.
    /// </summary>
    public const float CoreRadius = 0.9f;
    /// <summary>Freed, her pearl bursts into light and she leaps off the gateway in this long, landing this far aside.</summary>
    public const float LeapSeconds = 0.7f;

    /// <summary>The arena intro: the banner stands, dissolves, a beat, then her shadow grows and she lands.</summary>
    public const float BannerSeconds = 2.5f, DissolveSeconds = 0.6f, DropDelay = 0.5f, ShadowSeconds = 0.8f;
    /// <summary>The landing wave reaches the rim in this long, sweeping mobs out; it nudges her outward.</summary>
    public const float WaveSeconds = 0.5f, WaveNudge = 5f;
    /// <summary>The mud cloud rises over this long when the arena seals, and settles over CloudSettle once she is freed.</summary>
    public const float CloudRise = 1.5f, CloudSettle = 2f;

    public const float PearlDamage = 12f, PearlSpeed = 7f, PearlRadius = 0.3f, PearlLife = 4f;
    public const float OpenTelegraph = 0.9f, VolleyInterval = 1.2f;
    public const int Volleys = 4;
    public const float ClosedPause = 3f, ClosedPause2 = 2.2f, StaggerSeconds = 1.5f;
    public const int RingCount = 18, RingGap = 3;
    public const int WallCount = 15, WallHole = 2;
    public const float WallArcDeg = 120f;
    public const int HelixArms = 3;
    public const float HelixSeconds = 2.5f, HelixRate = 5f, HelixSpinDegPerSec = 110f;
    public const float RoyalSpeed = 3.5f, RoyalLife = 6f, RoyalDamage = 14f, RoyalRadius = 0.6f, RoyalTurnDegPerSec = 90f;
    public const int RoyalHp = 2;
    public const float SlowSeconds = 2f, SlowFactor = 0.6f;
    public const float SnapRange = 7f, SnapWarn = 0.7f, SnapDamage = 18f;
    /// <summary>Freed: she shudders this long as her pearl bursts, then leaps aside off the gateway and stays open.</summary>
    public const float FreedShudder = 0.3f, ShuffleAside = 6.5f;
}

public enum BossStage { Waiting, Banner, Drop, Fight, Freed }

/// <summary>Where the sealed arena's wall keeps a body: inside (Clementine), outside (mobs), or nowhere.</summary>
public enum ArenaBarrier { None, Inside, Outside }

/// <summary>What her shell is doing in the fight.</summary>
public enum ClamAct { Closed, Opening, Volleys, Helix, Stagger }

public sealed class PlaneBoss
{
    public Vector2 Position;
    public float Hp = PlaneBossTuning.Hp;
    /// <summary>Unit direction her opening faces.</summary>
    public Vector2 Facing = Vector2.UnitY;
    public BossStage Stage;
    /// <summary>Seconds in the current stage.</summary>
    public float StageTime;
    public int Phase = 1;
    public ClamAct Act;
    public float ActTime;
    /// <summary>Volleys fired this opening.</summary>
    public int Fired;
    public float HelixEmit;
    public bool RoyalSent, SnapUsed;
    /// <summary>Counting down the snap's warning (negative: no snap coming).</summary>
    public float SnapTimer = -1f;
    public float HitFlash;
    /// <summary>The landing wave's radius (negative when there is none).</summary>
    public float Wave = -1f;
    /// <summary>Freed, she leaps off the gateway from here to there.</summary>
    public Vector2 LeapFrom, RestAt;
    /// <summary>How far through her leap she is (0..1; 1 once she has landed aside).</summary>
    public float Leap;
    /// <summary>The mud cloud around the arena: 0 settled, 1 fully risen.</summary>
    public float Cloud;
    /// <summary>Her health at the fight's start (eased by the level cleansed) and how dense her patterns are (1 full).</summary>
    public float MaxHp = PlaneBossTuning.Hp, Pattern = 1f;
    /// <summary>The corruption's crust on her (0..CrustLayers): armour, denser patterns, longer reach. Each fifth of the arena cleansed strips one.</summary>
    public int Crust;
    public bool Freed => Stage == BossStage.Freed;
    /// <summary>Hurt only while the shell is open.</summary>
    public bool Open => Stage == BossStage.Fight && Act is ClamAct.Volleys or ClamAct.Helix;
    /// <summary>On the floor of the arena (solid, and hurting while she fights).</summary>
    public bool Landed => Stage is BossStage.Fight or BossStage.Freed;

    /// <summary>
    /// Every boss holds a corrupted pearl (docs/CORRUPTION.md): it is what makes her fight, the one place her light can
    /// hurt her, and once cleansed it is the reward, left where it sat. Queen Clam's lies in her middle, on her mantle.
    /// </summary>
    public Vector2 PearlAt => Position;
}

public sealed partial class PlaneWorld
{
    public PlaneBoss? Boss { get; private set; }

    /// <summary>While the battle runs the arena is sealed: she cannot leave it, and mobs and their shots cannot come in.</summary>
    public bool ArenaSealed { get; private set; }
    public Vector2 ArenaCenter => Map.Exit.Position;
    public float ArenaRadius => Map.Exit.Radius;

    Rng _bossRng = null!;

    /// <summary>Only a boss level has her: she waits on the Crack, which stays shut until she is freed.</summary>
    void PlaceBoss()
    {
        if (!Map.HasBoss) return;
        Boss = new PlaneBoss { Position = ArenaCenter };
        // She does not see Clementine: she lies as she settled, facing wherever she happens to face, and never turns.
        ExitOpen = false;
        _bossRng = new Rng(Map.Seed ^ 0xB055C1A3UL ^ ((ulong)Map.Level << 24) ^ ((ulong)Map.Depth << 32) ^ ((ulong)Map.Attempt << 50));
        float a = _bossRng.Range(0f, MathF.Tau);
        Boss.Facing = new Vector2(MathF.Cos(a), MathF.Sin(a));
    }

    /// <summary>True when the barrier lets a circle stand here.</summary>
    bool BarrierAllows(Vector2 p, float radius, ArenaBarrier barrier)
    {
        if (!ArenaSealed || barrier == ArenaBarrier.None) return true;
        float d = Vector2.Distance(p, ArenaCenter);
        return barrier == ArenaBarrier.Inside ? d <= ArenaRadius - radius : d >= ArenaRadius + radius;
    }

    /// <summary>The barrier's "uphill" direction at p (into the wall).</summary>
    Vector2 BarrierNormal(Vector2 p, ArenaBarrier barrier)
    {
        Vector2 out_ = SafeNormalize(p - ArenaCenter);
        if (out_ == Vector2.Zero) out_ = Vector2.UnitX;
        return barrier == ArenaBarrier.Inside ? out_ : -out_;
    }

    /// <summary>True when a point crosses the sealed arena's rim between a and b (shots stop there).</summary>
    bool CrossesRim(Vector2 a, Vector2 b)
    {
        if (!ArenaSealed) return false;
        bool ina = Vector2.Distance(a, ArenaCenter) < ArenaRadius, inb = Vector2.Distance(b, ArenaCenter) < ArenaRadius;
        return ina != inb;
    }

    void StepBoss()
    {
        var boss = Boss;
        if (boss is null) return;
        var p = Player;
        boss.StageTime += Dt;
        boss.HitFlash -= Dt;
        const float intro = PlaneBossTuning.BannerSeconds + PlaneBossTuning.DissolveSeconds + PlaneBossTuning.DropDelay;

        switch (boss.Stage)
        {
            case BossStage.Waiting:
                // She seals the arena the moment Clementine is wholly inside it.
                if (Vector2.Distance(p.Position, ArenaCenter) <= ArenaRadius - Radius - 0.3f)
                {
                    boss.Stage = BossStage.Banner;
                    boss.StageTime = 0f;
                    ArenaSealed = true;
                    ClearArenaOfCreatures();
                    Events.Add(new PlaneEvent(PlaneEventType.ArenaSealed, ArenaCenter, Vector2.Zero));
                }
                break;
            case BossStage.Banner:
                if (boss.StageTime >= intro)
                {
                    boss.Stage = BossStage.Drop;
                    boss.StageTime = 0f;
                }
                break;
            case BossStage.Drop:
                if (boss.StageTime >= PlaneBossTuning.ShadowSeconds) Land(boss);
                break;
            case BossStage.Fight:
                StepClam(boss);
                break;
            case BossStage.Freed:
                if (boss.StageTime >= PlaneBossTuning.FreedShudder && ArenaSealed) Release(boss);
                else if (!ArenaSealed && boss.Leap < 1f)
                {
                    // One leap aside (the view lifts her in an arc), then she rests there, open.
                    boss.Leap = MathF.Min(1f, boss.Leap + Dt / PlaneBossTuning.LeapSeconds);
                    float k = boss.Leap * boss.Leap * (3f - 2f * boss.Leap);
                    boss.Position = Vector2.Lerp(boss.LeapFrom, boss.RestAt, k);
                }
                break;
        }

        // The mud cloud rises while sealed and settles afterwards.
        float rate = ArenaSealed ? Dt / PlaneBossTuning.CloudRise : -Dt / PlaneBossTuning.CloudSettle;
        boss.Cloud = Math.Clamp(boss.Cloud + rate, 0f, 1f);

        // The landing wave runs out to the rim, sweeping mobs before it.
        if (boss.Wave >= 0f)
        {
            boss.Wave += ArenaRadius / PlaneBossTuning.WaveSeconds * Dt;
            foreach (var mob in Mobs)
            {
                if (!mob.Alive) continue;
                Vector2 off = mob.Position - ArenaCenter;
                float d = off.Length();
                if (d >= ArenaRadius + PlaneCombatTuning.MobRadius) continue;
                Vector2 dir = d > 1e-3f ? off / d : Vector2.UnitX;
                if (d < boss.Wave + PlaneCombatTuning.MobRadius) mob.Position = ArenaCenter + dir * MathF.Min(boss.Wave + PlaneCombatTuning.MobRadius, ArenaRadius);
            }
            if (boss.Wave >= ArenaRadius)
            {
                boss.Wave = -1f;
                // Set down just outside the wall, where there is water.
                foreach (var mob in Mobs)
                    if (mob.Alive && Vector2.Distance(mob.Position, ArenaCenter) < ArenaRadius + PlaneCombatTuning.MobRadius)
                        mob.Position = OutsideArena(mob.Position);
            }
        }

        // Her shell is solid: Clementine is pushed off it (and hurt by it while the fight runs).
        if (boss.Landed)
        {
            Vector2 off = p.Position - boss.Position;
            float d = off.Length(), min = PlaneBossTuning.BodyRadius + Radius;
            if (d < min)
            {
                Vector2 n = d > 1e-3f ? off / d : boss.Facing;
                p.Position = boss.Position + n * min;
                float vInto = Vector2.Dot(p.Velocity, n);
                if (vInto < 0f) p.Velocity -= n * vInto;
                // Touching her shell while she fights hurts and bounces her off.
                if (boss.Stage == BossStage.Fight && boss.StageTime > Dt && HurtPlayer(PlaneBossTuning.ContactDamage, n, DamageSource.BossContact))
                    p.Velocity += n * PlaneBossTuning.ContactKnockback;
            }
        }
    }

    void Land(PlaneBoss boss)
    {
        if (Corruption is not null)
        {
            // An explorer who cleansed the level meets an easier boss (up to BossEase less health and pattern).
            float ease = 1f - CorruptionTuning.BossEase * Cleansed;
            boss.MaxHp = boss.Hp = boss.Hp * ease;
            boss.Pattern = ease;
        }
        boss.Stage = BossStage.Fight;
        boss.StageTime = 0f;
        boss.Act = ClamAct.Closed;
        boss.ActTime = PlaneBossTuning.ClosedPause - 1f;
        boss.Wave = 0f;
        var p = Player;
        Vector2 off = p.Position - boss.Position;
        Vector2 dir = off.LengthSquared() > 1e-6f ? Vector2.Normalize(off) : -Vector2.UnitY;
        p.Velocity += dir * PlaneBossTuning.WaveNudge;
        Events.Add(new PlaneEvent(PlaneEventType.BossLanded, boss.Position, dir));
    }

    /// <summary>Freed: the arena opens, her pearl floats down and the Crack opens.</summary>
    void Release(PlaneBoss boss)
    {
        ArenaSealed = false;
        // The Crack opens once the arena is cleansed enough too (StepCondensation).
        ExitOpen = Corruption is null || ArenaCleansed >= CorruptionTuning.ArenaToOpen;
        if (ExitOpen && Corruption is not null) Events.Add(new PlaneEvent(PlaneEventType.CrackOpened, ArenaCenter, Vector2.Zero));
        var offer = PlaneRun.PortedPearls.Where(id => Run.CanOffer(id) && Pearls.All(q => q.ItemId != id || q.Taken)).ToList();
        var boss_ = offer.Where(id => Run.Catalog!.TryGet(id, out var item) && item.Pools.Contains("boss")).ToList();
        var pick = boss_.Count > 0 ? boss_ : offer;
        // She leaps aside off the gateway (to her side, or the other if there is no room); the reward floats down on
        // the gateway's far side.
        var side = new Vector2(boss.Facing.Y, -boss.Facing.X);
        boss.LeapFrom = boss.Position;
        boss.RestAt = boss.Position + side * PlaneBossTuning.ShuffleAside;
        if (!Clear(boss.RestAt, PlaneBossTuning.BodyRadius)) boss.RestAt = boss.Position - side * PlaneBossTuning.ShuffleAside;
        if (!Clear(boss.RestAt, PlaneBossTuning.BodyRadius)) boss.RestAt = boss.Position - boss.Facing * PlaneBossTuning.ShuffleAside;
        boss.Leap = 0f;
        Events.Add(new PlaneEvent(PlaneEventType.BossLeap, boss.Position, boss.RestAt - boss.Position));
        // Her pearl, cleansed, is the reward (every boss's rule, docs/CORRUPTION.md): it stays exactly where it sat in
        // her as she leaps away from under it, for Clementine to take.
        if (pick.Count > 0) Pearls.Add(new PlanePearl { ItemId = Run.Pick(pick, _bossRng), Position = boss.PearlAt, Placed = true });
        Stats.BossesFreed++;
        Events.Add(new PlaneEvent(PlaneEventType.BossFreed, boss.Position, Vector2.Zero));
    }

    /// <summary>
    /// The arena seals: no creature but her may be inside during the fight. Murklings (clinging ones too) and Blotters
    /// in it are set down just outside; bombs bound for it are spent.
    /// </summary>
    void ClearArenaOfCreatures()
    {
        float Inside(Vector2 at, float radius) => Vector2.Distance(at, ArenaCenter) - (ArenaRadius + radius);
        foreach (var m in Murklings)
            if (m.Alive && Inside(m.Position, BlightTuning.MurkRadius) < 0f)
            {
                m.Clinging = false;
                m.Position = OutsideArena(m.Position);
                m.Velocity = Vector2.Zero;
            }
        foreach (var b in Blotters)
            if (b.Alive && Inside(b.Position, BlotterTuning.Radius) < 0f)
            {
                b.Position = OutsideArena(b.Position);
                b.Trail.Clear();
                b.Track();
                b.Target = b.Position;
            }
        Bombs.RemoveAll(bomb => Inside(bomb.To, 0f) < 0f);
        MurkShots.Clear();
    }

    /// <summary>The nearest open water just outside the arena's rim, along the way out from p (then turning a little).</summary>
    Vector2 OutsideArena(Vector2 p)
    {
        Vector2 dir = SafeNormalize(p - ArenaCenter);
        if (dir == Vector2.Zero) dir = Vector2.UnitX;
        for (int turn = 0; turn < 36; turn++)
        {
            float a = (turn % 2 == 0 ? 1 : -1) * (turn + 1) / 2 * 10f * MathUtil.Deg2Rad;
            Vector2 d = Rotate(dir, a);
            for (float r = ArenaRadius + PlaneCombatTuning.MobRadius + 0.6f; r < ArenaRadius + 12f; r += 1f)
            {
                Vector2 q = ArenaCenter + d * r;
                if (Clear(q, PlaneCombatTuning.MobRadius)) return q;
            }
        }
        return ArenaCenter + dir * (ArenaRadius + 2f);
    }

    /// <summary>Her fight: open (telegraph), volleys (and in phase 2 the helix and the royal pearl), shut; the snap.</summary>
    void StepClam(PlaneBoss boss)
    {
        var p = Player;
        boss.ActTime += Dt;
        // She never turns: she does not see Clementine, only throws out what the corruption makes in her.

        switch (boss.Act)
        {
            case ClamAct.Closed:
                if (boss.ActTime >= (boss.Phase == 1 ? PlaneBossTuning.ClosedPause : PlaneBossTuning.ClosedPause2)) SetAct(boss, ClamAct.Opening);
                break;
            case ClamAct.Opening:
                if (boss.ActTime >= PlaneBossTuning.OpenTelegraph)
                {
                    SetAct(boss, ClamAct.Volleys);
                    boss.Fired = 0;
                    boss.RoyalSent = boss.SnapUsed = false;
                    boss.ActTime = PlaneBossTuning.VolleyInterval;
                }
                break;
            case ClamAct.Volleys:
                if (boss.ActTime >= PlaneBossTuning.VolleyInterval)
                {
                    if (boss.Fired >= PlaneBossTuning.Volleys)
                    {
                        SetAct(boss, ClamAct.Closed);
                        Events.Add(new PlaneEvent(PlaneEventType.BossClosed, boss.Position, boss.Facing));
                        break;
                    }
                    boss.ActTime = 0f;
                    // Phase 2: the second volley is the helix instead, and the royal pearl comes with the first.
                    if (boss.Phase == 2 && boss.Fired == 1)
                    {
                        boss.Fired++;
                        SetAct(boss, ClamAct.Helix);
                        boss.HelixEmit = 0f;
                        break;
                    }
                    if (boss.Fired % 2 == 0) PearlRing(boss);
                    else PearlWall(boss);
                    if (boss.Phase == 2 && !boss.RoyalSent)
                    {
                        boss.RoyalSent = true;
                        Vector2 d = SafeNormalize(p.Position - boss.Position);
                        Shots.Add(BossPearl(boss, d == Vector2.Zero ? boss.Facing : d, PlaneBossTuning.RoyalSpeed, royal: true));
                    }
                    boss.Fired++;
                    Events.Add(new PlaneEvent(PlaneEventType.BossVolley, boss.Position, boss.Facing));
                }
                break;
            case ClamAct.Helix:
                boss.HelixEmit += Dt * PlaneBossTuning.HelixRate;
                while (boss.HelixEmit >= 1f)
                {
                    boss.HelixEmit -= 1f;
                    float spin = boss.ActTime * PlaneBossTuning.HelixSpinDegPerSec * MathUtil.Deg2Rad;
                    for (int arm = 0; arm < PlaneBossTuning.HelixArms; arm++)
                    {
                        float a = MathF.Atan2(boss.Facing.Y, boss.Facing.X) + spin + MathF.Tau * arm / PlaneBossTuning.HelixArms;
                        Shots.Add(BossPearl(boss, new Vector2(MathF.Cos(a), MathF.Sin(a)), PlaneBossTuning.PearlSpeed));
                    }
                }
                if (boss.ActTime >= PlaneBossTuning.HelixSeconds)
                {
                    SetAct(boss, ClamAct.Volleys);
                    boss.ActTime = PlaneBossTuning.VolleyInterval;
                }
                break;
            case ClamAct.Stagger:
                if (boss.ActTime >= PlaneBossTuning.StaggerSeconds) SetAct(boss, ClamAct.Opening);
                break;
        }

        // Phase 2: she snaps at Clementine when she comes close while the shell is open (once per opening).
        if (boss.Phase == 2 && boss.Open && !boss.SnapUsed && boss.SnapTimer < 0f
            && Vector2.Distance(p.Position, boss.Position) < PlaneBossTuning.SnapRange)
        {
            boss.SnapUsed = true;
            boss.SnapTimer = PlaneBossTuning.SnapWarn;
            Events.Add(new PlaneEvent(PlaneEventType.BossSnapWarning, boss.Position, boss.Facing));
        }
        if (boss.SnapTimer >= 0f)
        {
            boss.SnapTimer -= Dt;
            if (boss.SnapTimer < 0f)
            {
                Events.Add(new PlaneEvent(PlaneEventType.BossSnap, boss.Position, boss.Facing));
                if (Vector2.Distance(p.Position, boss.Position) < PlaneBossTuning.SnapRange)
                    HurtPlayer(PlaneBossTuning.SnapDamage, SafeNormalize(p.Position - boss.Position), DamageSource.BossSnap);
            }
        }
    }

    static void SetAct(PlaneBoss boss, ClamAct act)
    {
        boss.Act = act;
        boss.ActTime = 0f;
    }

    /// <summary>The crust's reach: her pearls fly this much of their full life (shorter as layers fall).</summary>
    static float Reach(PlaneBoss boss) => 0.65f + 0.07f * boss.Crust;

    /// <summary>How many of a pattern's pearls she sends: thinner as the crust falls and for an explorer.</summary>
    static int Thinned(PlaneBoss boss, int count) => Math.Max(3, (int)MathF.Round(count * boss.Pattern * (0.6f + 0.08f * boss.Crust)));

    PlaneShot BossPearl(PlaneBoss boss, Vector2 dir, float speed, bool royal = false) => new()
    {
        Position = boss.Position + dir * (PlaneBossTuning.BodyRadius * 0.8f),
        Velocity = dir * speed,
        Life = (royal ? PlaneBossTuning.RoyalLife : PlaneBossTuning.PearlLife) * (Corruption is null ? 1f : Reach(boss)),
        Damage = royal ? PlaneBossTuning.RoyalDamage : PlaneBossTuning.PearlDamage,
        Radius = royal ? PlaneBossTuning.RoyalRadius : PlaneBossTuning.PearlRadius,
        BossPearl = true,
        Royal = royal,
        PearlHp = royal ? PlaneBossTuning.RoyalHp : 1,
    };

    /// <summary>A full ring of pearls with a gap of a few at a random angle.</summary>
    void PearlRing(PlaneBoss boss)
    {
        int n = Corruption is null ? PlaneBossTuning.RingCount : Thinned(boss, PlaneBossTuning.RingCount), gap = _bossRng.Int(n);
        float turn = _bossRng.Range(0f, MathF.Tau);
        for (int i = 0; i < n; i++)
        {
            if ((i - gap + n) % n < PlaneBossTuning.RingGap) continue;
            float a = turn + MathF.Tau * i / n;
            Shots.Add(BossPearl(boss, new Vector2(MathF.Cos(a), MathF.Sin(a)), PlaneBossTuning.PearlSpeed));
        }
    }

    /// <summary>An arc of pearls thrown out of her opening (she cannot see where Clementine is), with one small hole along it.</summary>
    void PearlWall(PlaneBoss boss)
    {
        var aim = Rotate(boss.Facing, _bossRng.Range(-1.2f, 1.2f));
        int n = Corruption is null ? PlaneBossTuning.WallCount : Math.Max(PlaneBossTuning.WallHole + 3, Thinned(boss, PlaneBossTuning.WallCount)), hole = 1 + _bossRng.Int(n - PlaneBossTuning.WallHole - 1);
        float arc = PlaneBossTuning.WallArcDeg * MathUtil.Deg2Rad;
        for (int i = 0; i < n; i++)
        {
            if (i >= hole && i < hole + PlaneBossTuning.WallHole) continue;
            Shots.Add(BossPearl(boss, Rotate(aim, -arc / 2f + arc * i / (n - 1)), PlaneBossTuning.PearlSpeed));
        }
    }

    /// <summary>
    /// One of her shots reached Queen Clam. Shut, she is all shell: it breaks on her harmlessly. Open, her lid is
    /// raised and the light flies over her shell to the black pearl in her middle, the only thing that hurts her.
    /// </summary>
    bool BubbleHitsBoss(PlaneShot shot)
    {
        var boss = Boss;
        if (boss is null || boss.Stage != BossStage.Fight) return false;
        float d = Vector2.Distance(shot.Position, boss.PearlAt);
        if (boss.Open)
        {
            if (d > PlaneBossTuning.CoreRadius + shot.Radius) return false;
            // The crust absorbs most of a shot's light while it holds (each layer a sixth).
            DamageBoss(boss, HitDamage(shot) * (1f - 0.17f * boss.Crust), shot.Velocity);
        }
        else if (d > PlaneBossTuning.BodyRadius + shot.Radius) return false;
        Pop(shot, boss.Position - shot.Position);
        return true;
    }

    /// <summary>She hurts Queen Clam while her shell is open: past half health she staggers; at none she is freed.</summary>
    void DamageBoss(PlaneBoss boss, float damage, Vector2 dir)
    {
        if (boss.Stage != BossStage.Fight) return;
        boss.Hp -= damage;
        boss.HitFlash = PlaneCombatTuning.HitFlash;
        Events.Add(new PlaneEvent(PlaneEventType.BossHit, boss.Position, dir, damage));
        if (boss.Hp <= 0f) FreeBoss(boss);
        else if (boss.Phase == 1 && boss.Hp <= boss.MaxHp * 0.5f)
        {
            boss.Phase = 2;
            SetAct(boss, ClamAct.Stagger);
            boss.SnapTimer = -1f;
            Events.Add(new PlaneEvent(PlaneEventType.BossStagger, boss.Position, boss.Facing));
        }
    }

    void FreeBoss(PlaneBoss boss)
    {
        boss.Hp = 0f;
        boss.Stage = BossStage.Freed;
        boss.StageTime = 0f;
        boss.SnapTimer = -1f;
        // Her black pearl bursts into light: it washes the whole arena clean of corruption and turns white. Her pearls
        // still in the water burst with it.
        foreach (var s in Shots)
            if (s.BossPearl) s.Life = 0f;
        if (Corruption is { } field && ArenaArea is { } arena) field.Clear(arena.Cells);
        Events.Add(new PlaneEvent(PlaneEventType.BossDefeated, boss.Position, boss.Facing, ArenaRadius));
    }
}
