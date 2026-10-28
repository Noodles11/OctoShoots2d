using System;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Bosses;

namespace OctoShoots.Core.Plane;

/// <summary>
/// Drippus Prime's numbers (docs/CORRUPTION.md, "Drippus Prime"): a murkling prime grown huge, whose every birth costs
/// it some of its own body. First guesses, tuned in play.
/// </summary>
public static class DrippusTuning
{
    /// <summary>Its radius at full mass (7 m across), and the share of it that is solid to her and her light (its skin at her plane).</summary>
    public const float Radius = 3.5f, Body = 0.95f;
    /// <summary>Its meter at Depth 1 (more with Menace, BossRoster), and what each birth costs it (a share of its starting meter).</summary>
    public const float Meter = 1100f, BirthCost = 25f / Meter;
    /// <summary>Its mass in murklings' worth of ink, and the least it keeps (it stops birthing there).</summary>
    public const int Mass = 30, Floor = 12;
    /// <summary>How many it births a brood, by phase; at most BroodCap + BroodCapPerMenace × Menace of its brood alive at once.</summary>
    public static readonly int[] Brood = { 2, 3, 4 };
    public const float BroodCap = 3f, BroodCapPerMenace = 2f;
    /// <summary>Its pace at full size, how much quicker at its floor, and its pace while it warns or acts (a share).</summary>
    public const float Pace = 1.2f, PaceSmall = 1.5f, ActSlow = 0.25f;
    /// <summary>Its warnings by attack (shorter with Menace, never under TellFloor); the fan's aim locks for its last FanLock.</summary>
    public const float BroodTell = 0.9f, RingTell = 0.7f, FanTell = 0.9f, SpiralTell = 0.6f, BombTell = 0.8f, TellFloor = 0.45f, FanLock = 0.15f;
    /// <summary>The slingshot fan: this many big drops across this arc, each a prime's throw (its damage ×FanHit, its size), a little faster.</summary>
    public const int FanCount = 5;
    public const float FanArcDeg = 50f, FanHit = 17.5f / 12f, FanRadius = 0.25f, FanSpeed = 1.15f;
    /// <summary>The bomb burst: this many by phase, aimed this far ahead of where she is swimming.</summary>
    public static readonly int[] Bombs = { 3, 5, 7 };
    public const float BombLead = 0.6f;
    /// <summary>The bounce: within Reach of its skin it dimples for Tell, then springs her off at Speed for its damage ×BounceHit; she tumbles for Tumble; then it rests for Cool.</summary>
    public const float Reach = 1.2f, Tell = 0.3f, Speed = 12f, BounceHit = 10f / 12f, Tumble = 0.35f, Cool = 1.5f;
    /// <summary>Her hits tear it: a tear stays open this long; a shot within this angle of an open one reaches the pearl through it, for Through × its light.</summary>
    public const float TearClose = 1.5f, TearWidth = 0.35f, Through = 2f;
    /// <summary>
    /// Its entrance: the arena's ink streams into it over this long (in the adventure, from this far round it); past
    /// this share of it, it swells up and is solid to her.
    /// </summary>
    public const float Rise = 2.5f, RiseReach = 7f, RiseSolid = 0.35f;
    /// <summary>Freed, the reef fish it took swim out of it: this many chromis and tangs; gone once this far from her (or after LeaveMax).</summary>
    public const int Chromis = 14, Tangs = 6;
    public const float LeaveFar = 22f, LeaveMax = 40f;
}

/// <summary>A tear her light opened in Drippus Prime (Angle on the map, from its middle), closing as it ages.</summary>
public sealed class BossTear
{
    public float Angle, Age, Strength;
}

public sealed partial class PlaneWorld
{
    static bool IsDrippus(PlaneBoss boss) => boss.Spec.Concept == BossConcept.TheBrood;

    /// <summary>Its brood alive now, and how many it may have at once.</summary>
    int BroodAlive => Murklings.Count(m => m.Alive && m.Brood);
    int BroodCap(PlaneBoss boss) => (int)(DrippusTuning.BroodCap + DrippusTuning.BroodCapPerMenace * boss.Spec.Menace);

    /// <summary>What one birth costs its meter.</summary>
    static float BirthCost(PlaneBoss boss) => boss.MaxHp * DrippusTuning.BirthCost;

    /// <summary>
    /// It may brood: ink above its floor, room in its brood, and meter enough that a birth never frees it (and only in
    /// the war: without the corruption there are no murklings).
    /// </summary>
    bool CanBrood(PlaneBoss boss) =>
        Corruption is not null && boss.Mass > DrippusTuning.Floor && BroodAlive < BroodCap(boss) && boss.Hp > BirthCost(boss) * 2f;

    /// <summary>A warning, shorter with Menace (never under the floor).</summary>
    static float Tell(PlaneBoss boss, float seconds) => MathF.Max(DrippusTuning.TellFloor, seconds / (1f + 0.3f * boss.Spec.Menace));

    /// <summary>
    /// Its next act: a brood every other act while it can, and between them the others in turn (the ring, the fan, the
    /// bombs, and the spiral once its second phase brings it). Picked as the warning begins, so the warning is its own.
    /// </summary>
    void PickDrippusAct(PlaneBoss boss)
    {
        var others = boss.Active.Where(a => a != BossAttack.Brood).ToList();
        bool brood = boss.Acts % 2 == 0 && CanBrood(boss);
        boss.Next = brood || others.Count == 0 ? BossAttack.Brood : others[boss.Turn++ % others.Count];
        boss.Acts++;
        boss.TelegraphLength = Tell(boss, boss.Next switch
        {
            BossAttack.Brood => DrippusTuning.BroodTell,
            BossAttack.Ring => DrippusTuning.RingTell,
            BossAttack.Fan => DrippusTuning.FanTell,
            BossAttack.Helix => DrippusTuning.SpiralTell,
            _ => DrippusTuning.BombTell,
        });
        boss.BudDirs.Clear();
        if (boss.Next == BossAttack.Brood)
        {
            // Where the bulges swell: spread round its rim, turned at random.
            int n = Math.Min(DrippusTuning.Brood[Math.Min(boss.Phase, DrippusTuning.Brood.Length) - 1], Math.Min(BroodCap(boss) - BroodAlive, boss.Mass - DrippusTuning.Floor));
            float turn = _bossRng.Range(0f, MathF.Tau);
            for (int i = 0; i < Math.Max(n, 1); i++)
            {
                float a = turn + MathF.Tau * i / Math.Max(n, 1) + _bossRng.Range(-0.3f, 0.3f);
                boss.BudDirs.Add(new Vector2(MathF.Cos(a), MathF.Sin(a)));
            }
        }
        boss.Aim = AimFrom(boss, boss.Position);
    }

    /// <summary>
    /// A brood: each bulge snaps off as a murkling, swimming out at once. Each birth costs it a murkling's worth of its
    /// mass (it shrinks: its area follows its mass) and BirthCost of its meter, which counts toward its phases.
    /// </summary>
    void Brood(PlaneBoss boss)
    {
        foreach (var dir in boss.BudDirs)
        {
            if (!CanBrood(boss)) break;
            var at = boss.Position + dir * (boss.Radius * DrippusTuning.Body + BlightTuning.MurkRadius + 0.2f);
            for (int k = 0; k < 4 && !Clear(at, BlightTuning.MurkRadius); k++) at = Vector2.Lerp(at, boss.Position, 0.25f);
            var m = Bud(at, -1, drink: false, born: true)!;
            m.Brood = true;
            m.Velocity = dir * 5f;
            m.Shaken = 0.4f;
            m.ThrowTimer = BlightTuning.ThrowEvery * _bossRng.Range(0.6f, 1.2f);
            boss.Mass--;
            boss.Size = MathF.Sqrt(boss.Mass / (float)DrippusTuning.Mass);
            boss.Hp -= BirthCost(boss);
            Events.Add(new PlaneEvent(PlaneEventType.BossBrood, at, dir));
        }
        boss.BudDirs.Clear();
        CheckPhase(boss);
    }

    /// <summary>The slingshot fan: big drops slung across an arc along its locked aim.</summary>
    void Fan(PlaneBoss boss)
    {
        float arc = DrippusTuning.FanArcDeg * MathUtil.Deg2Rad;
        for (int i = 0; i < DrippusTuning.FanCount; i++)
        {
            var dir = Rotate(boss.Aim, -arc / 2f + arc * i / (DrippusTuning.FanCount - 1));
            var drop = BossPearl(boss, dir, boss.Spec.PearlSpeed * DrippusTuning.FanSpeed);
            drop.Damage *= DrippusTuning.FanHit;
            drop.Radius = DrippusTuning.FanRadius;
            Shots.Add(drop);
        }
    }

    /// <summary>Its pace: quicker the smaller it is, a crawl while it warns or acts.</summary>
    static float DrippusPace(PlaneBoss boss)
    {
        float shrunk = (DrippusTuning.Mass - boss.Mass) / (float)(DrippusTuning.Mass - DrippusTuning.Floor);
        float pace = boss.Spec.MoveRate * (1f + (DrippusTuning.PaceSmall - 1f) * shrunk);
        return boss.Act is BossAct.Telegraph or BossAct.Act or BossAct.Helix ? pace * DrippusTuning.ActSlow : pace;
    }

    /// <summary>
    /// The bounce: she comes within Reach of its skin and its near side dimples (the warning; a dash gets her out), then
    /// springs her off, hurting her and tumbling her; touching it springs at once. It rests a moment after each. Its
    /// body stays solid to her throughout.
    /// </summary>
    void StepBounce(PlaneBoss boss)
    {
        var p = Player;
        boss.BounceCool -= Dt;
        boss.Sprang += Dt;
        float skin = boss.Radius * DrippusTuning.Body;
        var off = p.Position - boss.Position;
        float d = off.Length(), gap = d - skin - Radius;
        if (d > 1e-3f) boss.BounceDir = off / d;
        if (boss.Bounce >= 0f)
        {
            boss.Bounce -= Dt;
            if (boss.Bounce < 0f) Spring(boss, gap);
        }
        else if (boss.BounceCool <= 0f && gap < DrippusTuning.Reach)
        {
            if (gap <= 0.02f) Spring(boss, gap);
            else
            {
                boss.Bounce = DrippusTuning.Tell;
                Events.Add(new PlaneEvent(PlaneEventType.BossBounceWarn, boss.Position, boss.BounceDir));
            }
        }
        PushOff(boss, skin);
    }

    void Spring(PlaneBoss boss, float gap)
    {
        boss.Bounce = -1f;
        boss.BounceCool = DrippusTuning.Cool;
        boss.Sprang = 0f;
        var n = boss.BounceDir;
        Events.Add(new PlaneEvent(PlaneEventType.BossBounce, boss.Position + n * boss.Radius * DrippusTuning.Body, n));
        // Out of its reach by now (a dash), she is spared.
        if (gap > DrippusTuning.Reach + 0.3f) return;
        var p = Player;
        if (!HurtPlayer(boss.Spec.Damage * DrippusTuning.BounceHit, n, DamageSource.BossContact)) return;
        p.Velocity = n * DrippusTuning.Speed;
        p.TumbleTimer = DrippusTuning.Tumble;
        p.TumbleDir = n;
    }

    /// <summary>
    /// Her light on its body: every shot counts (its whole body takes light, as a murkling's does) and tears it there;
    /// one through a tear still open reaches the pearl, for Through × its light.
    /// </summary>
    bool ShotHitsDrippus(PlaneBoss boss, PlaneShot shot)
    {
        if (!Reaches(shot, boss.Position, boss.Radius * DrippusTuning.Body)) return false;
        var from = shot.Position - boss.Position;
        float a = MathF.Atan2(from.Y, from.X);
        bool through = boss.Tears.Any(t => t.Age < DrippusTuning.TearClose && MathF.Abs(AngleBetween(t.Angle, a)) < DrippusTuning.TearWidth);
        float damage = HitDamage(shot) * (1f - 0.17f * boss.Crust) * (through ? DrippusTuning.Through : 1f);
        Tear(boss, a, damage);
        DamageBoss(boss, damage, shot.Velocity);
        Pop(shot, boss.Position - shot.Position);
        return true;
    }

    /// <summary>A tear opened (or one near it torn open afresh, wider).</summary>
    void Tear(PlaneBoss boss, float angle, float damage)
    {
        float share = MathF.Min(1f, damage / MathF.Max(boss.MaxHp * 0.02f, 1f));
        var near = boss.Tears.FirstOrDefault(t => MathF.Abs(AngleBetween(t.Angle, angle)) < DrippusTuning.TearWidth);
        if (near is not null)
        {
            near.Age = 0f;
            near.Strength = MathF.Min(1f, near.Strength + share * 0.5f);
            return;
        }
        if (boss.Tears.Count >= 6) boss.Tears.Remove(boss.Tears.OrderByDescending(t => t.Age).First());
        boss.Tears.Add(new BossTear { Angle = angle, Strength = MathF.Max(share, 0.25f) });
    }

    static float AngleBetween(float a, float b)
    {
        float d = (b - a) % MathF.Tau;
        if (d > MathF.PI) d -= MathF.Tau;
        if (d < -MathF.PI) d += MathF.Tau;
        return d;
    }

    /// <summary>Its tears close as they age.</summary>
    static void StepTears(PlaneBoss boss)
    {
        foreach (var t in boss.Tears) t.Age += Dt;
        boss.Tears.RemoveAll(t => t.Age >= DrippusTuning.TearClose);
    }

    /// <summary>
    /// Its entrance: the arena's ink streams into one point, where it swells up (a prime's bud writ large); where it
    /// drank, the floor is left empty and dark. (The adventure's floor holds no ink: it streams in out of the water
    /// round it, RiseReach across.)
    /// </summary>
    void BeginRise(PlaneBoss boss)
    {
        float radius = DrippusTuning.RiseReach, ink = 0.8f;
        if (Corruption is { } ground && ground.Plan(boss.Position, DrippusTuning.Mass * BlightTuning.Ink) is { } draw)
        {
            boss.Draw = draw;
            radius = draw.Radius;
            ink = draw.Ink;
        }
        Events.Add(new PlaneEvent(PlaneEventType.MurklingBud, boss.Position, new Vector2(radius, ink), DrippusTuning.Rise));
    }

    /// <summary>Rising (k: 0 → 1 through it): it drinks its ink, and once it has begun to swell up it is solid to her.</summary>
    void StepRise(PlaneBoss boss, float k)
    {
        if (boss.Draw is { } draw && Corruption is { } field) field.Drink(draw, Dt / DrippusTuning.Rise);
        if (k > DrippusTuning.RiseSolid) PushOff(boss, boss.Radius * DrippusTuning.Body * (k - DrippusTuning.RiseSolid) / (1f - DrippusTuning.RiseSolid));
    }

    /// <summary>Its body is solid to her: she is pushed off its skin (this far from its middle).</summary>
    void PushOff(PlaneBoss boss, float skin)
    {
        var p = Player;
        var off = p.Position - boss.Position;
        float d = off.Length();
        if (d >= skin + Radius) return;
        var n = d > 1e-3f ? off / d : boss.Facing;
        p.Position = boss.Position + n * (skin + Radius);
        float into = Vector2.Dot(p.Velocity, n);
        if (into < 0f) p.Velocity -= n * into;
    }

    /// <summary>
    /// Freed: its brood still alive pop with it (they were its ink); its body bursts in a great shower of its ink, and
    /// the reef fish it took into itself swim out of it.
    /// </summary>
    void FreeDrippus(PlaneBoss boss)
    {
        boss.Draw = null;
        foreach (var m in Murklings)
            if (m.Alive && m.Brood) BurstMurkling(m, ember: true);
        Events.Add(new PlaneEvent(PlaneEventType.MurklingBurst, boss.Position, new Vector2(1f, boss.Radius), 0f));
    }

    /// <summary>The fish it took: they scatter out of the burst and swim slowly off, gone once out of sight.</summary>
    void ReleaseSwallowed(PlaneBoss boss)
    {
        boss.Gone = true;
        int id = ReefFish.Count == 0 ? 0 : ReefFish.Max(f => f.Id) + 1;
        var away = SafeNormalize(boss.Position - Player.Position);
        if (away == Vector2.Zero) away = -Vector2.UnitY;
        for (int i = 0; i < DrippusTuning.Chromis + DrippusTuning.Tangs; i++)
        {
            var kind = i < DrippusTuning.Chromis ? ReefFishKind.Chromis : ReefFishKind.BlueTang;
            float a = _bossRng.Range(0f, MathF.Tau), r = MathF.Sqrt(_bossRng.NextFloat()) * boss.Radius * 0.6f;
            var at = boss.Position + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
            // Away from her, fanned wide; each scattering out of the burst first.
            var dir = Rotate(away, _bossRng.Range(-1.1f, 1.1f));
            float heading = MathF.Atan2(dir.Y, dir.X);
            ReefFish.Add(new PlaneReefFish
            {
                Kind = kind, Id = id++, Position = at, Home = at, Heading = heading, Freed = true, Leaving = dir,
                Velocity = new Vector2(MathF.Cos(a), MathF.Sin(a)) * _bossRng.Range(2f, 4f), Rng = new Rng(_bossRng.NextU64()),
            });
        }
    }
}
