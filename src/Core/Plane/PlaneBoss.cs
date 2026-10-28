using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Bosses;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Core.Plane;

/// <summary>
/// The arena every boss shares (DESIGN-TOPDOWN §6.4), and Queen Clam's own hand-tuned numbers — the reference roll of the
/// boss generator (docs/BOSS-GENERATOR.md), whose BossSpec carries them.
/// </summary>
public static class PlaneBossTuning
{
    public const string Name = "Queen Clam";
    public const float Hp = 800f;
    /// <summary>Her shell, as a solid disc on the plane (5 m across).</summary>
    public const float BodyRadius = 2.5f;
    /// <summary>Touching a fighting boss: its damage ×ContactFactor, and a shove.</summary>
    public const float ContactFactor = 1.5f, ContactKnockback = 9f;
    /// <summary>Her weak spot: the black pearl in her middle, this wide.</summary>
    public const float CoreRadius = 0.9f;
    /// <summary>Freed, her pearl bursts into light and she leaps off the gateway in this long, landing this far aside.</summary>
    public const float LeapSeconds = 0.7f;

    /// <summary>
    /// The arena intro: the boss's cut-in plays with the game held (the view's BossIntro, before any of this time runs),
    /// then a beat, then her shadow grows and she lands.
    /// </summary>
    public const float DropDelay = 0.5f, ShadowSeconds = 0.8f;
    /// <summary>The landing wave reaches the rim in this long; it nudges her outward.</summary>
    public const float WaveSeconds = 0.5f, WaveNudge = 5f;

    public const float PearlDamage = 12f, PearlSpeed = 7f, PearlRadius = 0.3f, PearlLife = 4f;
    public const float OpenTelegraph = 0.9f, VolleyInterval = 1.2f;
    public const int Volleys = 4;
    public const float ClosedPause = 3f, StaggerSeconds = 1.5f;
    /// <summary>A Quicken phase change: the rest between cycles ×this (Queen Clam's 3 s → 2.2 s).</summary>
    public const float Quicken = 2.2f / 3f;
    /// <summary>A ShorterWindow phase change: the window ×this.</summary>
    public const float Shorten = 0.8f;
    public const int RingCount = 18, RingGap = 3;
    public const int WallCount = 15, WallHole = 2;
    public const float WallArcDeg = 120f;
    public const int HelixArms = 3;
    public const float HelixSeconds = 2.5f, HelixRate = 5f, HelixSpinDegPerSec = 110f;
    public const float RoyalSpeed = 3.5f, RoyalLife = 6f, RoyalRadius = 0.6f, RoyalTurnDegPerSec = 90f, RoyalFactor = 14f / 12f;
    public const int RoyalHp = 2;
    public const float SlowSeconds = 2f, SlowFactor = 0.6f;
    /// <summary>The snap: within this far of the body's edge (Queen Clam's 7 m from her middle), after this warning.</summary>
    public const float SnapReach = 4.5f, SnapWarn = 0.7f, SnapFactor = 1.5f;
    /// <summary>The ink pulse ring: it runs out at this speed, this thick; a dash slips it.</summary>
    public const float RingSpeed = 8f, RingBand = 0.9f;
    /// <summary>A sac burst: pearls from each sac, flying this long.</summary>
    public const int SacPearls = 6;
    public const float SacLife = 1.4f, SacRadius = 0.55f;
    /// <summary>Arc bombs: thrown this many (more deeper), landing this far off when blind.</summary>
    public const int Bombs = 3;
    public const float BombNear = 4f, BombFar = 11f, BombSpread = 2.5f;
    /// <summary>A shy shell withdraws early once an opening has cost it this share of its meter.</summary>
    public const float ShyLoss = 0.12f;
    /// <summary>A creeping boss stops this far from her and keeps this far inside the rim.</summary>
    public const float CreepStop = 4f, RimKeep = 3f;
    /// <summary>A drifting boss's pulse eases in over this long.</summary>
    public const float DriftEase = 3f;
    /// <summary>Freed, she shudders this long as her pearl bursts, then leaves (a clam leaps aside and stays open).</summary>
    public const float FreedShudder = 0.3f, ShuffleAside = 6.5f, LeaveSpeed = 1.4f, LeaveSeconds = 25f;
}

public enum BossStage { Waiting, Banner, Drop, Fight, Freed }

/// <summary>Where a boss is in its cycle (docs/BOSS-GENERATOR.md §7): rest, the warning, its act (Helix: the helix playing in it), the window after it, and the stagger of a phase change.</summary>
public enum BossAct { Rest, Telegraph, Act, Helix, Window, Stagger }

/// <summary>An ink sac (a Coax-out boss's): a small meter of its own, burst by her light.</summary>
public sealed class BossSac
{
    public BodySite Site;
    public float Hp;
    public bool Alive => Hp > 0f;
}

/// <summary>An ink pulse ring running out from the boss.</summary>
public sealed class BossRing
{
    public float Radius;
    public bool Hit;
}

public sealed class PlaneBoss
{
    public BossSpec Spec = null!;
    public Vector2 Position;
    public float Hp = PlaneBossTuning.Hp;
    /// <summary>Unit direction its opening faces.</summary>
    public Vector2 Facing = Vector2.UnitY;
    public BossStage Stage;
    /// <summary>Seconds in the current stage.</summary>
    public float StageTime;
    /// <summary>1 at the start, +1 for every phase threshold passed.</summary>
    public int Phase = 1;
    public BossAct Act;
    public float ActTime;
    /// <summary>Volleys fired this act.</summary>
    public int Fired;
    public float HelixEmit;
    public bool RoyalSent, SnapUsed;
    /// <summary>Counting down the snap's warning (negative: no snap coming).</summary>
    public float SnapTimer = -1f;
    public float HitFlash;
    /// <summary>The landing wave's radius (negative when there is none).</summary>
    public float Wave = -1f;
    /// <summary>Freed, a clam leaps off the gateway from here to there.</summary>
    public Vector2 LeapFrom, RestAt;
    /// <summary>How far through her leap she is (0..1; 1 once she has landed aside).</summary>
    public float Leap;
    /// <summary>Freed (not a clam): the way it swims off, and whether it is gone from the screen.</summary>
    public Vector2 LeaveDir;
    public bool Gone;
    /// <summary>Its health at the fight's start (eased by the level cleansed) and how dense its patterns are (1 full).</summary>
    public float MaxHp = PlaneBossTuning.Hp, Pattern = 1f;
    /// <summary>The corruption's crust on it (0..CrustLayers): armour, denser patterns, longer reach. Each fifth of the arena cleansed strips one.</summary>
    public int Crust;
    /// <summary>The phase changes so far: the rest ×, the window ×, mutation sites grown since the start.</summary>
    public float RestScale = 1f, WindowScale = 1f;
    public int Grown;
    /// <summary>A moving window's pearl: how far round it has travelled (rad).</summary>
    public float PearlTurn;
    /// <summary>A drifting boss: where its pulse carries it.</summary>
    public Vector2 DriftTo;
    /// <summary>Where it heard her last (a Vibration sense aims there).</summary>
    public Vector2 Heard;
    /// <summary>A shy shell: the meter it has lost this opening.</summary>
    public float LostThisAct;
    public readonly List<BossSac> Sacs = new();
    public readonly List<BossRing> Rings = new();
    /// <summary>How long its current warning lasts (Drippus Prime's is its next attack's own; the others' their spec's).</summary>
    public float TelegraphLength;
    /// <summary>
    /// Drippus Prime (PlaneDrippus.cs): its mass (murklings' worth of ink) and its size (a share of its full radius: its
    /// area follows its mass); its next attack, picked as its warning begins, its acts so far and its turn through the
    /// others; where the brood's bulges swell; the way it aims (a fan's, locked at the end of its warning); the bounce's
    /// warning (counting down, negative: none), its rest, the way it springs and the seconds since it last sprang; her
    /// light's tears in it; the ink it drinks as it rises.
    /// </summary>
    public int Mass;
    public float Size = 1f;
    public BossAttack Next;
    public int Acts, Turn;
    public readonly List<Vector2> BudDirs = new();
    public Vector2 Aim = Vector2.UnitY;
    public float Bounce = -1f, BounceCool, Sprang = 10f;
    public Vector2 BounceDir = Vector2.UnitY;
    public readonly List<BossTear> Tears = new();
    public InkDraw? Draw;
    public bool Freed => Stage == BossStage.Freed;
    /// <summary>The pearl can be struck now (by its exposure rule).</summary>
    public bool Open => Stage == BossStage.Fight && Spec.Exposure switch
    {
        Exposure.OpenMoments => Act is BossAct.Act or BossAct.Helix,
        Exposure.Stagger => Act == BossAct.Window,
        Exposure.MovingWindow => Act != BossAct.Stagger,
        Exposure.Whole => true,
        _ => !Sacs.Exists(s => s.Alive) && Act != BossAct.Stagger,
    };
    /// <summary>On the floor of the arena (solid, and hurting while it fights).</summary>
    public bool Landed => Stage == BossStage.Fight || Stage == BossStage.Freed && !Gone;
    public float Radius => Spec.Radius * Size;

    /// <summary>Where her light must strike it, and how near: its pearl (its whole body, for a body of ink).</summary>
    public Vector2 LightAt => Spec.Exposure == Exposure.Whole ? Position : PearlAt;
    public float LightReach => Spec.Exposure == Exposure.Whole ? Radius * DrippusTuning.Body : Spec.PearlRadius;

    /// <summary>
    /// Every boss holds a corrupted pearl (docs/CORRUPTION.md): it is what makes it fight, the one place her light can
    /// hurt it, and once cleansed it is the reward, left where it sat. A moving window's pearl travels round the body.
    /// </summary>
    public Vector2 PearlAt => Spec.SiteAt(Spec.Pearl, Position, Facing, PearlTurn);

    public Vector2 SacAt(BossSac sac) => Spec.SiteAt(sac.Site, Position, Facing);

    /// <summary>The phase thresholds (shares of the meter lost), distinct and in order.</summary>
    public float[] Thresholds => Spec.Phases.Select(p => p.At).Distinct().OrderBy(a => a).ToArray();

    /// <summary>What it throws now: the signature, the fillers, and what the phases so far have brought.</summary>
    public IEnumerable<BossAttack> Active
    {
        get
        {
            yield return Spec.Signature;
            foreach (var a in Spec.Fillers) yield return a;
            var t = Thresholds;
            float upTo = Phase >= 2 && t.Length > 0 ? t[Math.Min(Phase - 2, t.Length - 1)] : -1f;
            foreach (var p in Spec.Phases)
                if (p.Joins is { } j && p.At <= upTo) yield return j;
        }
    }
}

public sealed partial class PlaneWorld
{
    public PlaneBoss? Boss { get; private set; }

    /// <summary>
    /// The battle runs: from her coming into the arena until the boss is freed. The arena stays open — she may leave it,
    /// and so may a boss that chases her; mobs and shots come and go — unless the boss's author bars it (BossSpec.Blockade,
    /// PlaneSeals' BarArena).
    /// </summary>
    public bool BossBattle { get; private set; }
    public Vector2 ArenaCenter => Map.Exit.Position;
    public float ArenaRadius => Map.Exit.Radius;

    Rng _bossRng = null!;

    static bool IsVolley(BossAttack a) => a is BossAttack.Ring or BossAttack.Wall or BossAttack.Helix or BossAttack.SacBurst or BossAttack.PulseRing or BossAttack.ArcBombs;

    /// <summary>
    /// Only a boss level has one: it waits on the Crack, which stays shut until it is freed. It is the roster's boss for
    /// this loop's depth (BossRoster), unless the options name one. The tutorial's open rift has the arena but no boss,
    /// and its Crack is open.
    /// </summary>
    void PlaceBoss()
    {
        if (!Map.HasBoss || Map.Rift) return;
        var spec = Options.Boss ?? BossRoster.For(Map.Seed, Map.Cycle, Math.Max(1, Map.Depth - LevelPlan.Tutorial));
        Boss = new PlaneBoss { Spec = spec, Position = ArenaCenter, Hp = spec.Hp, MaxHp = spec.Hp, TelegraphLength = spec.Telegraph };
        if (IsDrippus(Boss)) Boss.Mass = DrippusTuning.Mass;
        // It lies with its opening toward the screen (south, the camera's side), so the pearl shows.
        ExitOpen = false;
        _bossRng = new Rng(Map.Seed ^ 0xB055C1A3UL ^ ((ulong)Map.Level << 24) ^ ((ulong)Map.Depth << 32) ^ ((ulong)Map.Attempt << 50));
        Boss.Facing = Vector2.UnitY;
        Boss.Heard = ArenaCenter;
        if (spec.Exposure == Exposure.CoaxOut)
            foreach (var site in spec.Sites.Where(s => s.Sac)) Boss.Sacs.Add(new BossSac { Site = site, Hp = spec.SacHp });
    }

    void StepBoss()
    {
        var boss = Boss;
        if (boss is null) return;
        var p = Player;
        boss.StageTime += Dt;
        boss.HitFlash -= Dt;
        const float intro = PlaneBossTuning.DropDelay;

        switch (boss.Stage)
        {
            case BossStage.Waiting:
                // The battle begins the moment Clementine is wholly inside the arena; a barred one shuts behind her.
                if (Vector2.Distance(p.Position, ArenaCenter) <= ArenaRadius - Radius - 0.3f)
                {
                    boss.Stage = BossStage.Banner;
                    boss.StageTime = 0f;
                    BossBattle = true;
                    if (boss.Spec.Blockade) BarArena();
                    Events.Add(new PlaneEvent(PlaneEventType.BattleBegan, ArenaCenter, Vector2.Zero));
                }
                break;
            case BossStage.Banner:
                if (boss.StageTime >= intro)
                {
                    boss.Stage = BossStage.Drop;
                    boss.StageTime = 0f;
                    // Drippus Prime does not drop in: it rises out of the arena's ink.
                    if (IsDrippus(boss)) BeginRise(boss);
                }
                break;
            case BossStage.Drop:
                if (IsDrippus(boss)) StepRise(boss, boss.StageTime / DrippusTuning.Rise);
                if (boss.StageTime >= (IsDrippus(boss) ? DrippusTuning.Rise : PlaneBossTuning.ShadowSeconds)) Land(boss);
                break;
            case BossStage.Fight:
                StepFight(boss);
                break;
            case BossStage.Freed:
                if (boss.StageTime >= PlaneBossTuning.FreedShudder && BossBattle) Release(boss);
                else if (!BossBattle) StepLeaving(boss);
                break;
        }

        // The landing wave runs out to the rim.
        if (boss.Wave >= 0f)
        {
            boss.Wave += ArenaRadius / PlaneBossTuning.WaveSeconds * Dt;
            if (boss.Wave >= ArenaRadius) boss.Wave = -1f;
        }

        // Drippus Prime bounces her off (its body is solid to her too).
        if (IsDrippus(boss))
        {
            if (boss.Stage == BossStage.Fight) StepBounce(boss);
            return;
        }
        // Its body is solid: Clementine is pushed off it (and hurt by it while the fight runs). Freed, only a clam
        // lying open stays solid; the others leave.
        bool solid = boss.Stage == BossStage.Fight || boss.Freed && boss.Spec.Form == BodyForm.GiantClam;
        if (solid)
        {
            Vector2 off = p.Position - boss.Position;
            float d = off.Length(), min = boss.Radius * (boss.Freed && !boss.Spec.Reference ? boss.Spec.TrueScale : 1f) + Radius;
            if (d < min)
            {
                Vector2 n = d > 1e-3f ? off / d : boss.Facing;
                p.Position = boss.Position + n * min;
                float vInto = Vector2.Dot(p.Velocity, n);
                if (vInto < 0f) p.Velocity -= n * vInto;
                // Touching it while it fights hurts and bounces her off.
                if (boss.Stage == BossStage.Fight && boss.StageTime > Dt
                    && HurtPlayer(boss.Spec.Damage * PlaneBossTuning.ContactFactor, n, DamageSource.BossContact))
                    p.Velocity += n * PlaneBossTuning.ContactKnockback;
            }
        }
    }

    void Land(PlaneBoss boss)
    {
        if (Corruption is not null && !Options.Adventure)
        {
            // An explorer who cleansed the level meets an easier boss (up to BossEase less health and pattern). (The
            // adventure's ink is in the water, not on the ground: its boss is never eased.)
            float ease = 1f - CorruptionTuning.BossEase * Cleansed;
            boss.MaxHp = boss.Hp = boss.Hp * ease;
            boss.Pattern = ease;
        }
        // Risen, Drippus Prime has drunk all its ink.
        if (boss.Draw is { } draw && Corruption is { } ground) ground.Drink(draw, 1f);
        boss.Draw = null;
        boss.Stage = BossStage.Fight;
        boss.StageTime = 0f;
        boss.Act = BossAct.Rest;
        boss.ActTime = boss.Spec.Rest - 1f;
        boss.Wave = 0f;
        boss.DriftTo = boss.Position;
        var p = Player;
        Vector2 off = p.Position - boss.Position;
        Vector2 dir = off.LengthSquared() > 1e-6f ? Vector2.Normalize(off) : -Vector2.UnitY;
        p.Velocity += dir * PlaneBossTuning.WaveNudge;
        Events.Add(new PlaneEvent(PlaneEventType.BossLanded, boss.Position, dir));
    }

    /// <summary>Freed: the battle ends (a barred arena opens), its pearl floats free and the Crack opens; a clam leaps aside, the others leave.</summary>
    void Release(PlaneBoss boss)
    {
        BossBattle = false;
        if (boss.Spec.Blockade) UnbarArena();
        // The Crack opens once the arena is cleansed enough too (StepCondensation); in the adventure, once every vent is
        // sealed too (SealVent).
        if (Options.Adventure)
        {
            ExitOpen = VentsOpen == 0;
            if (ExitOpen) Events.Add(new PlaneEvent(PlaneEventType.ExitOpened, Map.Exit.Position, Vector2.Zero));
        }
        else
        {
            ExitOpen = Corruption is null || ArenaCleansed >= CorruptionTuning.ArenaToOpen;
            if (ExitOpen && Corruption is not null) Events.Add(new PlaneEvent(PlaneEventType.CrackOpened, ArenaCenter, Vector2.Zero));
        }
        var offer = PlaneRun.PortedPearls.Where(id => Run.CanOffer(id) && Pearls.All(q => q.ItemId != id || q.Taken)).ToList();
        var boss_ = offer.Where(id => Run.Catalog!.TryGet(id, out var item) && item.Pools.Contains("boss")).ToList();
        var pick = boss_.Count > 0 ? boss_ : offer;
        var pearlAt = boss.PearlAt;
        boss.LeapFrom = boss.Position;
        if (IsDrippus(boss)) ReleaseSwallowed(boss);
        else if (boss.Spec.Form == BodyForm.GiantClam)
        {
            // A clam leaps aside off the gateway (to its side, or the other if there is no room) and lies there, open.
            var side = new Vector2(boss.Facing.Y, -boss.Facing.X);
            boss.RestAt = boss.Position + side * PlaneBossTuning.ShuffleAside;
            if (!Clear(boss.RestAt, boss.Radius)) boss.RestAt = boss.Position - side * PlaneBossTuning.ShuffleAside;
            if (!Clear(boss.RestAt, boss.Radius)) boss.RestAt = boss.Position - boss.Facing * PlaneBossTuning.ShuffleAside;
            boss.Leap = 0f;
            Events.Add(new PlaneEvent(PlaneEventType.BossLeap, boss.Position, boss.RestAt - boss.Position));
        }
        else
        {
            // The others move off from under the pearl, away from her, and are gone once out of sight.
            var away = SafeNormalize(boss.Position - Player.Position);
            boss.LeaveDir = away == Vector2.Zero ? -Vector2.UnitY : away;
            boss.Leap = 0f;
        }
        // Its pearl, cleansed, is the reward (every boss's rule, docs/CORRUPTION.md): it stays exactly where it sat as
        // the creature moves away from under it, for Clementine to take.
        if (pick.Count > 0) Pearls.Add(new PlanePearl { ItemId = Run.Pick(pick, _bossRng), Position = pearlAt, Placed = true });
        Stats.BossesFreed++;
        Events.Add(new PlaneEvent(PlaneEventType.BossFreed, boss.Position, Vector2.Zero));
    }

    /// <summary>Freed and released: a clam's one leap aside; anything else swimming or creeping away until it is gone.</summary>
    void StepLeaving(PlaneBoss boss)
    {
        if (boss.Spec.Form == BodyForm.GiantClam)
        {
            if (boss.Leap >= 1f) return;
            boss.Leap = MathF.Min(1f, boss.Leap + Dt / PlaneBossTuning.LeapSeconds);
            float k = boss.Leap * boss.Leap * (3f - 2f * boss.Leap);
            boss.Position = Vector2.Lerp(boss.LeapFrom, boss.RestAt, k);
            return;
        }
        if (boss.Gone) return;
        boss.Leap = MathF.Min(1f, boss.Leap + Dt / PlaneBossTuning.LeaveSeconds);
        // It sets off slowly.
        float speed = PlaneBossTuning.LeaveSpeed * MathF.Min(1f, boss.Leap * 8f);
        boss.Position += boss.LeaveDir * speed * Dt;
        boss.Facing = Turn(boss.Facing, boss.LeaveDir, 1.2f * Dt);
        if (boss.Leap >= 1f) boss.Gone = true;
    }

    /// <summary>
    /// The fight (docs/BOSS-GENERATOR.md §7): rest → telegraph → act (its volleys) → window (Stagger and Coax-out
    /// bosses) → rest; it moves and turns by its kind meanwhile, its rings run out, and a clam snaps up close.
    /// </summary>
    void StepFight(PlaneBoss boss)
    {
        var p = Player;
        var spec = boss.Spec;
        boss.ActTime += Dt;
        // A Vibration sense hears her shots and dashes.
        if (p.IsDashing || Events.Exists(e => e.Type == PlaneEventType.Shot)) boss.Heard = p.Position;
        if (spec.Exposure == Exposure.MovingWindow && boss.Act != BossAct.Stagger) boss.PearlTurn += spec.PearlOrbit * Dt;
        if (IsDrippus(boss))
        {
            StepTears(boss);
            // A fan's aim follows her through its warning, locked for the last moment of it.
            if (boss.Act == BossAct.Telegraph && boss.Next == BossAttack.Fan && boss.TelegraphLength - boss.ActTime > DrippusTuning.FanLock)
                boss.Aim = AimFrom(boss, boss.Position);
        }

        switch (boss.Act)
        {
            case BossAct.Rest:
                if (boss.ActTime >= spec.Rest * boss.RestScale) BeginTelegraph(boss);
                break;
            case BossAct.Telegraph:
                if (boss.ActTime >= boss.TelegraphLength)
                {
                    SetAct(boss, BossAct.Act);
                    boss.Fired = 0;
                    boss.RoyalSent = boss.SnapUsed = false;
                    boss.LostThisAct = 0f;
                    boss.ActTime = spec.VolleyInterval;
                }
                break;
            case BossAct.Act:
                if (boss.ActTime >= spec.VolleyInterval)
                {
                    if (boss.Fired >= spec.Volleys)
                    {
                        EndAct(boss);
                        break;
                    }
                    boss.ActTime = 0f;
                    var attack = VolleyAt(boss, boss.Fired);
                    boss.Fired++;
                    if (attack == BossAttack.Helix)
                    {
                        SetAct(boss, BossAct.Helix);
                        boss.HelixEmit = 0f;
                        break;
                    }
                    Throw(boss, attack);
                    // The homing pearl (when it is not the signature) comes with the act's first volley.
                    if (!boss.RoyalSent && spec.Signature != BossAttack.Homing && boss.Active.Contains(BossAttack.Homing))
                    {
                        boss.RoyalSent = true;
                        Throw(boss, BossAttack.Homing);
                    }
                    Events.Add(new PlaneEvent(PlaneEventType.BossVolley, boss.Position, boss.Facing, (float)attack));
                }
                break;
            case BossAct.Helix:
                boss.HelixEmit += Dt * PlaneBossTuning.HelixRate;
                while (boss.HelixEmit >= 1f)
                {
                    boss.HelixEmit -= 1f;
                    float spin = boss.ActTime * PlaneBossTuning.HelixSpinDegPerSec * MathUtil.Deg2Rad;
                    for (int arm = 0; arm < spec.HelixArms; arm++)
                    {
                        float a = MathF.Atan2(boss.Facing.Y, boss.Facing.X) + spin + MathF.Tau * arm / spec.HelixArms;
                        Shots.Add(BossPearl(boss, new Vector2(MathF.Cos(a), MathF.Sin(a)), spec.PearlSpeed));
                    }
                }
                if (boss.ActTime >= PlaneBossTuning.HelixSeconds)
                {
                    SetAct(boss, BossAct.Act);
                    boss.ActTime = spec.VolleyInterval;
                }
                break;
            case BossAct.Window:
                if (boss.ActTime >= spec.Window * boss.WindowScale)
                {
                    SetAct(boss, BossAct.Rest);
                    Events.Add(new PlaneEvent(PlaneEventType.BossClosed, boss.Position, boss.Facing));
                }
                break;
            case BossAct.Stagger:
                if (boss.ActTime >= PlaneBossTuning.StaggerSeconds) BeginTelegraph(boss);
                break;
        }

        Move(boss);
        StepRings(boss);

        // A clam snaps at Clementine when she comes close while it is open (once per opening).
        if (boss.Active.Contains(BossAttack.Snap) && boss.Open && !boss.SnapUsed && boss.SnapTimer < 0f
            && Vector2.Distance(p.Position, boss.Position) < boss.Radius + PlaneBossTuning.SnapReach)
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
                if (Vector2.Distance(p.Position, boss.Position) < boss.Radius + PlaneBossTuning.SnapReach)
                    HurtPlayer(spec.Damage * PlaneBossTuning.SnapFactor, SafeNormalize(p.Position - boss.Position), DamageSource.BossSnap);
            }
        }
    }

    void BeginTelegraph(PlaneBoss boss)
    {
        if (IsDrippus(boss)) PickDrippusAct(boss);
        else boss.TelegraphLength = boss.Spec.Telegraph;
        SetAct(boss, BossAct.Telegraph);
        Events.Add(new PlaneEvent(PlaneEventType.BossTelegraph, boss.Position, boss.Facing));
        // A drifting body pulses on each telegraph: toward her if it senses her, else somewhere across the arena.
        if (boss.Spec.Move == BossMove.DriftPulse)
        {
            var aim = AimFrom(boss, boss.Position);
            var to = boss.Position + aim * boss.Spec.MoveRate * (boss.Spec.Sense == BossSense.Blind ? 1f : 0.7f);
            boss.DriftTo = KeepInArena(boss, to);
        }
    }

    /// <summary>The act is over: rest, or (Stagger, Coax out) open its window first.</summary>
    void EndAct(PlaneBoss boss)
    {
        if (boss.Spec.Exposure is Exposure.Stagger or Exposure.CoaxOut)
        {
            SetAct(boss, BossAct.Window);
            Events.Add(new PlaneEvent(PlaneEventType.BossStagger, boss.Position, boss.Facing, 0f));
            return;
        }
        SetAct(boss, BossAct.Rest);
        Events.Add(new PlaneEvent(PlaneEventType.BossClosed, boss.Position, boss.Facing));
    }

    /// <summary>
    /// The act's i-th volley: the signature on the even ones, and on the odd ones in turn what the phases brought and
    /// the fillers (Queen Clam: ring, wall, ring, wall; in her second phase ring, helix, ring, wall).
    /// </summary>
    BossAttack VolleyAt(PlaneBoss boss, int i)
    {
        var spec = boss.Spec;
        if (IsDrippus(boss)) return boss.Next;
        var others = boss.Active.Skip(1 + spec.Fillers.Count).Where(IsVolley).Concat(spec.Fillers.Where(IsVolley)).ToList();
        bool signature = IsVolley(spec.Signature) || spec.Signature == BossAttack.Homing;
        if (i % 2 == 0 && signature || others.Count == 0) return spec.Signature;
        return others[(i / 2) % others.Count];
    }

    static void SetAct(PlaneBoss boss, BossAct act)
    {
        boss.Act = act;
        boss.ActTime = 0f;
    }

    /// <summary>Where its attacks go: straight out of its opening (blind), at her (water), or where it heard her (vibration).</summary>
    Vector2 AimFrom(PlaneBoss boss, Vector2 from)
    {
        var target = boss.Spec.Sense switch
        {
            BossSense.Water => Player.Position,
            BossSense.Vibration => boss.Heard,
            _ => from + boss.Facing,
        };
        var d = SafeNormalize(target - from);
        return d == Vector2.Zero ? boss.Facing : d;
    }

    /// <summary>It keeps to its arena — unless it chases her, when it may follow her out.</summary>
    Vector2 KeepInArena(PlaneBoss boss, Vector2 at)
    {
        if (boss.Spec.Chaser) return at;
        float max = ArenaRadius - boss.Radius - PlaneBossTuning.RimKeep;
        var off = at - ArenaCenter;
        return off.Length() > max ? ArenaCenter + SafeNormalize(off) * max : at;
    }

    /// <summary>Its movement by kind (§7.2); it stands still through a window and a stagger.</summary>
    void Move(PlaneBoss boss)
    {
        var spec = boss.Spec;
        var p = Player;
        bool still = boss.Act is BossAct.Window or BossAct.Stagger;
        switch (spec.Move)
        {
            case BossMove.Turn:
                if (!still) boss.Facing = Turn(boss.Facing, AimFrom(boss, boss.Position), spec.MoveRate * Dt);
                break;
            case BossMove.Spin:
                if (!still) boss.Facing = Rotate(boss.Facing, spec.MoveRate * Dt);
                break;
            case BossMove.Creep:
                if (still) break;
                var to = p.Position - boss.Position;
                float rate = IsDrippus(boss) ? DrippusPace(boss) : spec.MoveRate;
                if (to.Length() > boss.Radius + PlaneBossTuning.CreepStop)
                    boss.Position = KeepInArena(boss, boss.Position + SafeNormalize(to) * rate * Dt);
                boss.Facing = Turn(boss.Facing, SafeNormalize(to) == Vector2.Zero ? boss.Facing : SafeNormalize(to), 1.2f * Dt);
                break;
            case BossMove.DriftPulse:
                boss.Position = Vector2.Lerp(boss.Position, boss.DriftTo, 1f - MathF.Exp(-PlaneBossTuning.DriftEase * Dt));
                break;
            case BossMove.Circle:
                if (still) break;
                float r = MathF.Min(7f, ArenaRadius - boss.Radius - PlaneBossTuning.RimKeep);
                var off = boss.Position - ArenaCenter;
                float a = MathF.Atan2(off.Y, off.X) + spec.MoveRate * Dt;
                float d = off.Length() + (r - off.Length()) * MathF.Min(1f, Dt * 0.8f);
                boss.Position = ArenaCenter + new Vector2(MathF.Cos(a), MathF.Sin(a)) * d;
                boss.Facing = Turn(boss.Facing, SafeNormalize(p.Position - boss.Position) == Vector2.Zero ? boss.Facing : SafeNormalize(p.Position - boss.Position), 1f * Dt);
                break;
        }
    }

    /// <summary>One attack (§7.1), from the anatomy that makes it.</summary>
    void Throw(PlaneBoss boss, BossAttack attack)
    {
        var spec = boss.Spec;
        switch (attack)
        {
            case BossAttack.Ring:
                PearlRing(boss);
                break;
            case BossAttack.Wall:
                PearlWall(boss);
                break;
            case BossAttack.Homing:
            {
                var d = SafeNormalize(Player.Position - boss.Position);
                Shots.Add(BossPearl(boss, d == Vector2.Zero ? boss.Facing : d, PlaneBossTuning.RoyalSpeed, royal: true));
                break;
            }
            case BossAttack.PulseRing:
                boss.Rings.Add(new BossRing { Radius = boss.Radius });
                Events.Add(new PlaneEvent(PlaneEventType.BossPulse, boss.Position, boss.Facing));
                break;
            case BossAttack.SacBurst:
            {
                var from = spec.Exposure == Exposure.CoaxOut
                    ? boss.Sacs.Where(s => s.Alive).Select(boss.SacAt).ToList()
                    : spec.Sites.Where(s => s.Sac).Select(s => spec.SiteAt(s, boss.Position, boss.Facing)).ToList();
                foreach (var at in from)
                {
                    float turn = _bossRng.Range(0f, MathF.Tau);
                    for (int i = 0; i < PlaneBossTuning.SacPearls; i++)
                    {
                        float a = turn + MathF.Tau * i / PlaneBossTuning.SacPearls;
                        var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
                        var pearl = BossPearl(boss, dir, spec.PearlSpeed * 0.8f);
                        pearl.Position = at + dir * 0.4f;
                        pearl.Life = PlaneBossTuning.SacLife;
                        Shots.Add(pearl);
                    }
                }
                break;
            }
            case BossAttack.Brood:
                Brood(boss);
                break;
            case BossAttack.Fan:
                Fan(boss);
                break;
            case BossAttack.ArcBombs:
            {
                // Drippus Prime's burst grows by phase, and leads her.
                bool drippus = IsDrippus(boss);
                int n = drippus ? DrippusTuning.Bombs[Math.Min(boss.Phase, DrippusTuning.Bombs.Length) - 1] : PlaneBossTuning.Bombs + (spec.Menace >= 0.6f ? 1 : 0);
                for (int i = 0; i < n; i++)
                {
                    Vector2 to;
                    if (spec.Sense == BossSense.Blind)
                    {
                        float a = _bossRng.Range(0f, MathF.Tau);
                        to = boss.Position + new Vector2(MathF.Cos(a), MathF.Sin(a)) * _bossRng.Range(PlaneBossTuning.BombNear, PlaneBossTuning.BombFar);
                    }
                    else
                    {
                        var target = spec.Sense == BossSense.Water ? Player.Position : boss.Heard;
                        if (drippus) target += Player.Velocity * DrippusTuning.BombLead;
                        float a = _bossRng.Range(0f, MathF.Tau);
                        to = target + new Vector2(MathF.Cos(a), MathF.Sin(a)) * (i == 0 ? 0f : _bossRng.Range(1f, PlaneBossTuning.BombSpread));
                    }
                    var off = to - ArenaCenter;
                    float max = ArenaRadius - 1f;
                    if (!spec.Chaser && off.Length() > max) to = ArenaCenter + SafeNormalize(off) * max;
                    Bombs.Add(new CorruptionBomb { From = boss.Position, To = to, FromBoss = true });
                }
                Events.Add(new PlaneEvent(PlaneEventType.BombThrown, boss.Position, boss.Facing));
                break;
            }
        }
    }

    /// <summary>Its ink pulse rings run out to the rim: each hurts her once as it passes (a dash slips it) and snuffs her shots.</summary>
    void StepRings(PlaneBoss boss)
    {
        var p = Player;
        foreach (var ring in boss.Rings)
        {
            float r0 = ring.Radius;
            ring.Radius += PlaneBossTuning.RingSpeed * Dt;
            float her = Vector2.Distance(p.Position, boss.Position);
            if (!ring.Hit && !p.IsDashing && MathF.Abs(her - (r0 + ring.Radius) * 0.5f) <= PlaneBossTuning.RingBand * 0.5f + Radius + (ring.Radius - r0) * 0.5f)
            {
                ring.Hit = true;
                HurtPlayer(boss.Spec.Damage, SafeNormalize(p.Position - boss.Position), DamageSource.BossPearl);
            }
            foreach (var shot in Shots)
            {
                if (!shot.FromPlayer || shot.Life <= 0f) continue;
                float d = Vector2.Distance(shot.Position, boss.Position);
                if (d >= r0 - shot.Radius && d <= ring.Radius + shot.Radius) Pop(shot, shot.Position - boss.Position);
            }
        }
        boss.Rings.RemoveAll(r => r.Radius >= ArenaRadius);
    }

    /// <summary>The crust's reach: its pearls fly this much of their full life (shorter as layers fall).</summary>
    static float Reach(PlaneBoss boss) => 0.65f + 0.07f * boss.Crust;

    /// <summary>How many of a pattern's pearls it sends: thinner as the crust falls and for an explorer.</summary>
    static int Thinned(PlaneBoss boss, int count) => Math.Max(3, (int)MathF.Round(count * boss.Pattern * (0.6f + 0.08f * boss.Crust)));

    PlaneShot BossPearl(PlaneBoss boss, Vector2 dir, float speed, bool royal = false) => new()
    {
        Position = boss.Position + dir * (boss.Radius * 0.8f),
        Velocity = dir * speed,
        Life = (royal ? PlaneBossTuning.RoyalLife : PlaneBossTuning.PearlLife) * (Corruption is null ? 1f : Reach(boss)),
        Damage = boss.Spec.Damage * (royal ? PlaneBossTuning.RoyalFactor : 1f),
        Radius = royal ? PlaneBossTuning.RoyalRadius : PlaneBossTuning.PearlRadius,
        BossPearl = true,
        Royal = royal,
        PearlHp = royal ? PlaneBossTuning.RoyalHp : 1,
    };

    /// <summary>A full ring of pearls with a gap of a few at a random angle.</summary>
    void PearlRing(PlaneBoss boss)
    {
        int full = boss.Spec.RingCount;
        int n = Corruption is null ? full : Thinned(boss, full), gap = _bossRng.Int(n);
        float turn = _bossRng.Range(0f, MathF.Tau);
        for (int i = 0; i < n; i++)
        {
            if ((i - gap + n) % n < PlaneBossTuning.RingGap) continue;
            float a = turn + MathF.Tau * i / n;
            Shots.Add(BossPearl(boss, new Vector2(MathF.Cos(a), MathF.Sin(a)), boss.Spec.PearlSpeed));
        }
    }

    /// <summary>An arc of pearls thrown out of its opening (or at her, if it senses her), with one small hole along it.</summary>
    void PearlWall(PlaneBoss boss)
    {
        var aim = boss.Spec.Sense == BossSense.Blind ? Rotate(boss.Facing, _bossRng.Range(-1.2f, 1.2f)) : AimFrom(boss, boss.Position);
        int full = boss.Spec.WallCount;
        int n = Corruption is null ? full : Math.Max(PlaneBossTuning.WallHole + 3, Thinned(boss, full)), hole = 1 + _bossRng.Int(n - PlaneBossTuning.WallHole - 1);
        float arc = PlaneBossTuning.WallArcDeg * MathUtil.Deg2Rad;
        for (int i = 0; i < n; i++)
        {
            if (i >= hole && i < hole + PlaneBossTuning.WallHole) continue;
            Shots.Add(BossPearl(boss, Rotate(aim, -arc / 2f + arc * i / (n - 1)), boss.Spec.PearlSpeed));
        }
    }

    /// <summary>
    /// One of her shots reached the boss. A live ink sac it hits takes its light (and bursts when emptied). Open, the
    /// pearl takes it — the only thing that hurts the boss. Anywhere else on the body it breaks harmlessly.
    /// </summary>
    bool BubbleHitsBoss(PlaneShot shot)
    {
        var boss = Boss;
        if (boss is null || boss.Stage != BossStage.Fight) return false;
        if (IsDrippus(boss)) return ShotHitsDrippus(boss, shot);
        foreach (var sac in boss.Sacs)
        {
            if (!sac.Alive || !Reaches(shot, boss.SacAt(sac), PlaneBossTuning.SacRadius * (0.6f + sac.Site.Size))) continue;
            DamageSac(boss, sac, HitDamage(shot), shot.Velocity);
            Pop(shot, boss.SacAt(sac) - shot.Position);
            return true;
        }
        if (boss.Open && Reaches(shot, boss.PearlAt, boss.Spec.PearlRadius))
        {
            // The crust absorbs most of a shot's light while it holds (each layer a sixth).
            DamageBoss(boss, HitDamage(shot) * (1f - 0.17f * boss.Crust), shot.Velocity);
            Pop(shot, boss.PearlAt - shot.Position);
            return true;
        }
        // An open clam's raised lid lets light over its shell to the pearl; a moving-window body is all skin; a Coax-out
        // body is soft, and light passes over it to the sacs and the pearl.
        if (boss.Open || boss.Spec.Exposure == Exposure.CoaxOut) return false;
        if (!Reaches(shot, boss.Position, boss.Radius)) return false;
        Pop(shot, boss.Position - shot.Position);
        return true;
    }

    void DamageSac(PlaneBoss boss, BossSac sac, float damage, Vector2 dir, bool quiet = false)
    {
        sac.Hp -= damage;
        boss.HitFlash = PlaneCombatTuning.HitFlash;
        if (!quiet) Events.Add(new PlaneEvent(PlaneEventType.BossHit, boss.SacAt(sac), dir, damage));
        if (sac.Alive) return;
        Events.Add(new PlaneEvent(PlaneEventType.BossSacBurst, boss.SacAt(sac), dir, sac.Site.Size));
        if (!boss.Sacs.Exists(s => s.Alive)) Events.Add(new PlaneEvent(PlaneEventType.BossExposed, boss.PearlAt, Vector2.Zero));
    }

    /// <summary>A blast of her light (Starburst Gland, a cavitation) at a point: it bursts sacs and strikes an open pearl within reach.</summary>
    void BlastBoss(Vector2 at, float reach, float damage)
    {
        if (Boss is not { Stage: BossStage.Fight } boss) return;
        foreach (var sac in boss.Sacs)
            if (sac.Alive && Vector2.Distance(boss.SacAt(sac), at) < reach + PlaneBossTuning.SacRadius) DamageSac(boss, sac, damage, boss.SacAt(sac) - at);
        if (boss.Open && Vector2.Distance(boss.LightAt, at) < reach + boss.LightReach) DamageBoss(boss, damage, boss.LightAt - at);
    }

    /// <summary>Her light hurts the boss through its pearl; past each phase threshold it staggers into the next phase; at none it is freed.</summary>
    /// <remarks>Quiet (Firefly Squid's surge, which tells its hits summed): no hit event.</remarks>
    void DamageBoss(PlaneBoss boss, float damage, Vector2 dir, bool quiet = false)
    {
        if (boss.Stage != BossStage.Fight) return;
        boss.Hp -= damage;
        boss.LostThisAct += damage;
        boss.HitFlash = PlaneCombatTuning.HitFlash;
        if (!quiet) Events.Add(new PlaneEvent(PlaneEventType.BossHit, boss.Position, dir, damage));
        if (boss.Hp <= 0f)
        {
            FreeBoss(boss);
            return;
        }
        // A shy shell withdraws early when an opening costs it too much.
        if (boss.Spec.Concept == BossConcept.ShyShell && boss.Act is BossAct.Act && boss.LostThisAct >= boss.MaxHp * PlaneBossTuning.ShyLoss)
        {
            SetAct(boss, BossAct.Rest);
            Events.Add(new PlaneEvent(PlaneEventType.BossClosed, boss.Position, boss.Facing, 1f));
        }
        CheckPhase(boss);
    }

    /// <summary>Past its next phase threshold (the share of its meter lost, however it was lost), it staggers into that phase.</summary>
    void CheckPhase(PlaneBoss boss)
    {
        var t = boss.Thresholds;
        if (boss.Phase - 1 < t.Length && 1f - boss.Hp / boss.MaxHp >= t[boss.Phase - 1]) NextPhase(boss, t[boss.Phase - 1]);
    }

    /// <summary>A phase begins (§7.3): it staggers, and what the phase brings changes it.</summary>
    void NextPhase(PlaneBoss boss, float at)
    {
        boss.Phase++;
        foreach (var phase in boss.Spec.Phases.Where(p => p.At == at))
            switch (phase.Change)
            {
                case PhaseChange.Quicken:
                    boss.RestScale *= PlaneBossTuning.Quicken;
                    break;
                case PhaseChange.ShorterWindow:
                    boss.WindowScale = MathF.Max(boss.WindowScale * PlaneBossTuning.Shorten,
                        BossValidator.WindowFloor(boss.Spec.Menace) / MathF.Max(boss.Spec.Window, 0.01f));
                    break;
                case PhaseChange.MutationAdvances:
                    boss.Grown += 2;
                    // A fresh sac splits open over the pearl: the Coax-out sacs grow back (one fewer each time).
                    if (boss.Spec.Exposure == Exposure.CoaxOut)
                    {
                        int regrow = Math.Max(1, boss.Sacs.Count - boss.Phase + 1);
                        foreach (var sac in boss.Sacs.Take(regrow)) sac.Hp = boss.Spec.SacHp * 0.7f;
                    }
                    Events.Add(new PlaneEvent(PlaneEventType.BossMutates, boss.Position, boss.Facing));
                    break;
            }
        SetAct(boss, BossAct.Stagger);
        boss.SnapTimer = -1f;
        Events.Add(new PlaneEvent(PlaneEventType.BossStagger, boss.Position, boss.Facing, 1f));
    }

    /// <summary>Verification: the fighting boss is freed at once.</summary>
    public void DebugFreeBoss()
    {
        if (Boss is { Stage: BossStage.Fight } boss) FreeBoss(boss);
    }

    void FreeBoss(PlaneBoss boss)
    {
        boss.Hp = 0f;
        boss.Stage = BossStage.Freed;
        boss.StageTime = 0f;
        boss.SnapTimer = -1f;
        boss.Rings.Clear();
        // Its pearl bursts into light: it washes the whole arena clean of corruption and turns white. Its pearls still in
        // the water burst with it.
        foreach (var s in Shots)
            if (s.BossPearl) s.Life = 0f;
        Bombs.RemoveAll(b => b.FromBoss);
        if (IsDrippus(boss)) FreeDrippus(boss);
        if (Corruption is { } field && ArenaArea is { } arena) field.Clear(arena.Cells);
        Events.Add(new PlaneEvent(PlaneEventType.BossDefeated, boss.Position, boss.Facing, ArenaRadius));
    }
}
