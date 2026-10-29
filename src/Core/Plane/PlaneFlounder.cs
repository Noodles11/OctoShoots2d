using System;
using System.Collections.Generic;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Core.Plane;

/// <summary>
/// The flounder and the Hearthguard it turns into (docs/ADVENTURE.md §4): a flat fish lying on its patch of seabed (a
/// reef shelf just under her plane) from the Kelp Jungle down. Healthy, it lies still and now and then shimmies itself
/// into the sand; over ink it fills and turns. The Hearthguard never leaves its patch and never chases her: it shimmies
/// left and right as if burying itself, flinging a pattern of ink drops round it at short range, and when she comes
/// close the back of its flat disc draws up into a sting that stabs at her.
/// </summary>
public static class FlounderTuning
{
    /// <summary>Its own depth (the Kelp Jungle), where FlounderMin–Max lie on a level (fewer deeper: AdventureTuning.Rate).</summary>
    public const int Home = 2, Min = 3, Max = 5;
    /// <summary>Its disc (for her shots, her arm and the dots it touches).</summary>
    public const float Radius = 0.45f;
    /// <summary>Its gauge: it fills Fill a second with a dot within Sense (AdventureTuning.FishEat for a dot it eats), drains Drain a second.</summary>
    public const float Sense = 1.2f, Fill = 0.1f, Drain = 0.04f;
    /// <summary>Healthy, it shimmies into the sand every SettleMin–Max seconds.</summary>
    public const float SettleMin = 6f, SettleMax = 12f;

    /// <summary>The Hearthguard's meter: Meter (+ BlightTuning.MeterPerMenace per Menace), healing as a murkling's.</summary>
    public const float Meter = 26f;
    /// <summary>Its rest between shimmies: RestMin–Max seconds (÷ 1 + RestPerMenace × Menace).</summary>
    public const float RestMin = 2.5f, RestMax = 4f, RestPerMenace = 0.4f;
    /// <summary>A shimmy: Shimmy seconds of wriggling left and right (Wiggle radians, WiggleRate radians a second).</summary>
    public const float Shimmy = 1.4f, Wiggle = 0.35f, WiggleRate = 22f;
    /// <summary>A shimmy's swings (its wriggle's peaks): the moment of the k-th, and the flank it swings to (+1 its left).</summary>
    public const int Swings = 10;
    public static float SwingAt(int k) => (MathF.PI / 2f + k * MathF.PI) / WiggleRate;
    public static float SwingSide(int k) => k % 2 == 0 ? 1f : -1f;
    /// <summary>Its ink drops: flung off its flanks at InkSpeed, they fall after InkReach metres and leave a dot where they land.</summary>
    public const float InkSpeed = 3.5f, InkReach = 2.4f, InkDamage = 5f, InkRadius = 0.09f;
    /// <summary>
    /// The sting: she within Notice (and in sight) and it Cool since the last, its back draws up into a cone leaning at
    /// her for Windup (the telegraph), stabs out to NeedleReach over Strike, holds Hold, draws back over Retract.
    /// </summary>
    public const float Notice = 2.8f, NeedleReach = 3.2f, Windup = 0.45f, Strike = 0.12f, Hold = 0.15f, Retract = 0.45f, Cool = 1f;
    /// <summary>The stab: Bite damage and a shove of Knock along the needle.</summary>
    public const float Bite = 12f, Knock = 6f;
    /// <summary>Freed, it swims off at LeaveSpeed (gone as any freed fish, ReefFishTuning.LeaveFar).</summary>
    public const float LeaveSpeed = 1.6f;
}

/// <summary>A flounder's moment: lying still, shimmying, and (corrupted) its needle drawn, stabbing, drawing back.</summary>
public enum FlounderStage { Rest, Shimmy, Windup, Strike, Retract }

/// <summary>
/// The ink a Hearthguard flings off its flanks as it shimmies, a side at each swing: a fan of three at four of its
/// swings, one drop at every swing sweeping from its head to its tail, or a burst of five off each side at the height
/// of the shimmy.
/// </summary>
public enum InkPattern { Fan, Sweep, Burst }

/// <summary>A flounder on its patch, healthy or a Hearthguard.</summary>
public sealed class PlaneFlounder
{
    /// <summary>Where it is, and its patch (it never leaves it while it lies there).</summary>
    public Vector2 Position, Home, Velocity;
    public float Heading, Seed;
    /// <summary>Its gauge (0..1), the ink creeping over it (seconds left), turned, freed.</summary>
    public float Ink, Turning;
    public bool Corrupted, Freed, Gone;
    /// <summary>Freed: the way it swims off, and the seconds it has swum.</summary>
    public Vector2 Leaving;
    public float Left;
    public FlounderStage Stage;
    /// <summary>Seconds in its stage; how long this rest lasts; until it may stab again.</summary>
    public float Timer, Rest, Cool;
    /// <summary>This shimmy's pattern, and how many of its swings have flung theirs.</summary>
    public InkPattern Pattern;
    public int Flung;
    /// <summary>The sting: the way it points, and how far its tip reaches from its middle (0: drawn in).</summary>
    public Vector2 Aim = Vector2.UnitX;
    public float Reach;
    /// <summary>Whether this stab has struck her.</summary>
    public bool Struck;
    /// <summary>Until her oral arm may sting it again.</summary>
    public float ArmCooldown;
    public CorruptionMeter Corruption = new(FlounderTuning.Meter, BlightTuning.MurkRegen, BlightTuning.MurkDelay);

    /// <summary>The sting's tip.</summary>
    public Vector2 Tip => Home + Aim * Reach;
    /// <summary>How far its back has drawn up into the sting: 0 a flat disc → 1 drawn.</summary>
    public float Needle => Stage switch
    {
        FlounderStage.Windup => Math.Clamp(Timer / FlounderTuning.Windup, 0f, 1f),
        FlounderStage.Strike => 1f,
        FlounderStage.Retract => 1f - Math.Clamp(Timer / FlounderTuning.Retract, 0f, 1f),
        _ => 0f,
    };
    /// <summary>Its wriggle now (radians off its heading): a shimmy's left and right, easing in and out.</summary>
    public float Wriggle => Stage == FlounderStage.Shimmy
        ? FlounderTuning.Wiggle * MathF.Sin(Timer * FlounderTuning.WiggleRate) * MathF.Sin(MathF.PI * Math.Clamp(Timer / FlounderTuning.Shimmy, 0f, 1f))
        : 0f;
}

public sealed partial class PlaneWorld
{
    public List<PlaneFlounder> Flounders { get; } = new();

    /// <summary>
    /// Flounders on their patches: on the reef shelves (an abyss reef; else on the seabed by the rock, clear of the coral
    /// boulders), before the anemones take theirs, away from her start and from each other.
    /// </summary>
    void PlaceFlounders(Rng rng, List<Vector2> anchors, Func<Vector2, bool> allowed)
    {
        float rate = AdventureTuning.Rate(FlounderTuning.Home, Map.Depth);
        int want = (int)MathF.Round((FlounderTuning.Min + rng.Int(FlounderTuning.Max - FlounderTuning.Min + 1)) * rate);
        var hosts = Map.Abyss ? new List<Vector2>(Map.Shelves) : new List<Vector2>(anchors);
        Shuffle(hosts, rng);
        foreach (var anchor in hosts)
        {
            if (Flounders.Count >= want) break;
            for (int t = 0; t < 12; t++)
            {
                float a = rng.Range(0f, MathF.Tau), d = Map.Abyss ? (t == 0 ? 0f : rng.Range(0.3f, 1.2f)) : rng.Range(2f, 7f);
                var q = anchor + new Vector2(MathF.Cos(a), MathF.Sin(a)) * d;
                // Open round it (room for its needle), with rock close by.
                if (!allowed(q) || !Clear(q, 1.2f) || Clear(q, 4f)) continue;
                if (Map.Abyss && Map.HeightAt(q) < AbyssReef.ShelfDepth - 0.5f) continue;
                if (Flounders.Exists(o => Vector2.Distance(o.Home, q) < 12f)) continue;
                // Not on a coral boulder (its patch is open sand).
                if (Map.Decor.Exists(d => d.Kind == DecorKind.Bommie && Vector2.Distance(d.Position, q) < 1.6f + 1.2f * d.Scale)) continue;
                Flounders.Add(new PlaneFlounder
                {
                    Position = q, Home = q, Heading = rng.Range(0f, MathF.Tau), Seed = rng.Range(0f, 100f),
                    Rest = rng.Range(FlounderTuning.SettleMin, FlounderTuning.SettleMax),
                });
                break;
            }
        }
    }

    void StepFlounders(InkDots dots)
    {
        foreach (var f in Flounders)
        {
            if (f.Gone) continue;
            f.ArmCooldown -= Dt;
            if (f.Freed)
            {
                SwimOff(f);
                continue;
            }
            if (!f.Corrupted)
            {
                StepHealthyFlounder(f, dots);
                continue;
            }
            f.Corruption.Step(Dt);
            StepHearthguard(f);
        }

        // Her shots of light on a Hearthguard (its wardens may turn them aside).
        foreach (var shot in Shots)
        {
            if (!shot.FromPlayer || shot.Life <= 0f) continue;
            foreach (var f in Flounders)
            {
                if (!f.Corrupted || shot.Struck == f || !Reaches(shot, f.Position, FlounderTuning.Radius)) continue;
                float dealt = Ward(f, f.Position, HitDamage(shot));
                HitFlounder(f, dealt, SafeNormalize(shot.Velocity));
                if (shot.Pierce) shot.Struck = f;
                else
                {
                    Pop(shot, f.Position - shot.Position);
                    break;
                }
            }
        }
        Flounders.RemoveAll(f => f.Gone);
    }

    /// <summary>Light on a Hearthguard (her shot, her arm): its meter emptied, it is a flounder again.</summary>
    void HitFlounder(PlaneFlounder f, float light, Vector2 dir)
    {
        bool emptied = light > 0f && f.Corruption.Hit(light);
        Events.Add(new PlaneEvent(emptied ? PlaneEventType.MobDefeated : PlaneEventType.MobHit, f.Position, dir, light));
        if (emptied) FreeFlounder(f);
    }

    /// <summary>Healthy: it lies on its patch, shimmying into the sand now and then; it eats the dots it touches and fills over ink.</summary>
    void StepHealthyFlounder(PlaneFlounder f, InkDots dots)
    {
        f.Timer += Dt;
        if (f.Stage == FlounderStage.Rest && f.Timer >= f.Rest) Enter(f, FlounderStage.Shimmy);
        else if (f.Stage == FlounderStage.Shimmy && f.Timer >= FlounderTuning.Shimmy)
        {
            Enter(f, FlounderStage.Rest);
            f.Rest = _blightRng.Range(FlounderTuning.SettleMin, FlounderTuning.SettleMax);
        }
        if (f.Turning > 0f)
        {
            f.Turning -= Dt;
            if (f.Turning <= 0f) TurnFlounder(f);
            return;
        }
        int near = dots.Nearest(f.Position, FlounderTuning.Sense);
        if (near >= 0 && Vector2.Distance(InkDots.Centre(near), f.Position) < FlounderTuning.Radius + AdventureTuning.DotRadius + 0.1f)
        {
            dots.Remove(near);
            Events.Add(new PlaneEvent(PlaneEventType.DotEaten, InkDots.Centre(near), Vector2.Zero));
            f.Ink += AdventureTuning.FishEat;
        }
        if (f.Ink < 1f) f.Ink = Math.Clamp(f.Ink + (near >= 0 ? FlounderTuning.Fill : -FlounderTuning.Drain) * Dt, 0f, 1f);
        if (f.Ink >= 1f)
        {
            f.Ink = 1f;
            f.Turning = MurkFishTuning.TurnSeconds;
        }
    }

    /// <summary>The ink has crept over it: a Hearthguard, lying where it lay.</summary>
    void TurnFlounder(PlaneFlounder f)
    {
        f.Turning = 0f;
        f.Corrupted = true;
        f.Corruption.Max = f.Corruption.Value = FlounderTuning.Meter * (1f + BlightTuning.MeterPerMenace * Menace);
        f.Corruption.Regen *= 1f + 0.5f * Menace;
        Enter(f, FlounderStage.Rest);
        f.Rest = HearthRest();
        Events.Add(new PlaneEvent(PlaneEventType.FishTurned, f.Position, Vector2.Zero, FlounderTuning.Radius, DamageSource.None));
    }

    /// <summary>Verification: this flounder a Hearthguard at once.</summary>
    public void DebugTurnFlounder(PlaneFlounder f) => TurnFlounder(f);

    float HearthRest() => _blightRng.Range(FlounderTuning.RestMin, FlounderTuning.RestMax) / (1f + FlounderTuning.RestPerMenace * Menace);

    /// <summary>
    /// The Hearthguard: on its patch, never chasing. It rests, then shimmies, flinging its pattern of ink round it; she
    /// close by (and in sight), its back draws up into a sting leaning at her, and stabs.
    /// </summary>
    void StepHearthguard(PlaneFlounder f)
    {
        var p = Player;
        f.Timer += Dt;
        f.Cool -= Dt;
        bool close = !Defeated && f.Cool <= 0f && Vector2.Distance(p.Position, f.Home) < FlounderTuning.Notice + Radius && LineOfSight(f.Home, p.Position);
        switch (f.Stage)
        {
            case FlounderStage.Rest:
                if (close) BeginNeedle(f);
                else if (f.Timer >= f.Rest)
                {
                    Enter(f, FlounderStage.Shimmy);
                    f.Pattern = (InkPattern)(((int)f.Pattern + 1 + _blightRng.Int(2)) % 3);
                    f.Flung = 0;
                    Events.Add(new PlaneEvent(PlaneEventType.FlounderShimmy, f.Position, Vector2.Zero, FlounderTuning.Shimmy));
                }
                break;
            case FlounderStage.Shimmy:
                FlingInk(f);
                if (close) BeginNeedle(f);
                else if (f.Timer >= FlounderTuning.Shimmy)
                {
                    Enter(f, FlounderStage.Rest);
                    f.Rest = HearthRest();
                }
                break;
            case FlounderStage.Windup:
                if (f.Timer < FlounderTuning.Windup) break;
                Enter(f, FlounderStage.Strike);
                f.Struck = false;
                Events.Add(new PlaneEvent(PlaneEventType.FlounderStrike, f.Home, f.Aim));
                break;
            case FlounderStage.Strike:
            {
                // The tip flies out along the sting; it stabs her if it passes through her.
                var was = f.Tip;
                float reach = FlounderTuning.Radius + (FlounderTuning.NeedleReach - FlounderTuning.Radius) * MathF.Min(1f, f.Timer / FlounderTuning.Strike);
                // Rock stops it.
                for (float r = f.Reach; r < reach; r += 0.2f)
                    if (!Map.IsOpen(f.Home + f.Aim * MathF.Min(r + 0.2f, reach)))
                    {
                        reach = r;
                        break;
                    }
                f.Reach = MathF.Max(f.Reach, reach);
                if (!f.Struck && !Defeated && Geo.SegmentDistance(p.Position, was, f.Tip) < Radius + 0.1f)
                {
                    f.Struck = true;
                    if (HurtPlayer(FlounderTuning.Bite, f.Aim, DamageSource.Hearthguard)) p.Velocity += f.Aim * FlounderTuning.Knock;
                    Events.Add(new PlaneEvent(PlaneEventType.FlounderBite, f.Tip, f.Aim, FlounderTuning.Bite));
                }
                if (f.Timer >= FlounderTuning.Strike + FlounderTuning.Hold) Enter(f, FlounderStage.Retract);
                break;
            }
            case FlounderStage.Retract:
            {
                float u = Math.Clamp(f.Timer / FlounderTuning.Retract, 0f, 1f);
                f.Reach = MathF.Min(f.Reach, FlounderTuning.Radius + (FlounderTuning.NeedleReach - FlounderTuning.Radius) * (1f - u));
                if (u >= 1f)
                {
                    f.Reach = 0f;
                    f.Cool = FlounderTuning.Cool;
                    Enter(f, FlounderStage.Rest);
                    f.Rest = HearthRest();
                }
                break;
            }
        }
    }

    void BeginNeedle(PlaneFlounder f)
    {
        f.Aim = SafeNormalize(Player.Position - f.Home);
        if (f.Aim == Vector2.Zero) f.Aim = new Vector2(MathF.Cos(f.Heading), MathF.Sin(f.Heading));
        f.Reach = 0f;
        Enter(f, FlounderStage.Windup);
        Events.Add(new PlaneEvent(PlaneEventType.FlounderWindup, f.Home, f.Aim, FlounderTuning.Windup));
    }

    /// <summary>
    /// Its shimmy's ink, flung off its flanks: at each swing of the shimmy (its wriggle at a peak, one side then the
    /// other) the side it swings to throws that swing's drops of its pattern.
    /// </summary>
    void FlingInk(PlaneFlounder f)
    {
        while (f.Flung < FlounderTuning.Swings && f.Timer >= FlounderTuning.SwingAt(f.Flung))
        {
            int k = f.Flung++;
            float side = FlounderTuning.SwingSide(k);
            // Straight out of that flank (0), toward its head (+) or its tail (−), in radians.
            float[] fan = f.Pattern switch
            {
                InkPattern.Fan => k is 2 or 3 or 6 or 7 ? new[] { -0.6f, 0f, 0.6f } : Array.Empty<float>(),
                InkPattern.Sweep => new[] { 1f - 2f * k / (FlounderTuning.Swings - 1f) },
                _ => k is 4 or 5 ? new[] { -1.2f, -0.6f, 0f, 0.6f, 1.2f } : Array.Empty<float>(),
            };
            float flank = f.Heading + side * MathF.PI / 2f;
            var from = f.Home + new Vector2(MathF.Cos(flank), MathF.Sin(flank)) * FlounderTuning.Radius;
            foreach (float off in fan)
            {
                float a = flank - side * off;
                var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
                MurkShots.Add(new MurkShot
                {
                    Position = from, From = from, Velocity = dir * FlounderTuning.InkSpeed, Radius = FlounderTuning.InkRadius,
                    Damage = FlounderTuning.InkDamage, FallAt = FlounderTuning.InkReach, Lobbed = true,
                });
            }
        }
    }

    /// <summary>Freed: a flounder again; it lifts off its patch and swims off away from her, and is gone.</summary>
    void FreeFlounder(PlaneFlounder f)
    {
        f.Corrupted = false;
        f.Freed = true;
        f.Ink = 0f;
        f.Reach = 0f;
        Enter(f, FlounderStage.Rest);
        f.Leaving = SafeNormalize(f.Position - Player.Position);
        if (f.Leaving == Vector2.Zero) f.Leaving = new Vector2(MathF.Cos(f.Heading), MathF.Sin(f.Heading));
        f.Left = 0f;
        Stats.MobsDefeated++;
        Events.Add(new PlaneEvent(PlaneEventType.FishFreed, f.Position, Vector2.Zero, FlounderTuning.Radius));
    }

    void SwimOff(PlaneFlounder f)
    {
        f.Left += Dt;
        // A little wander in it, sliding along the rock.
        float wander = 0.4f * MathF.Sin(f.Left * 1.3f + f.Seed);
        var way = Vector2.Transform(f.Leaving, Matrix3x2.CreateRotation(wander));
        f.Velocity = MoveToward(f.Velocity, way * FlounderTuning.LeaveSpeed, 2f * Dt);
        Move(ref f.Position, ref f.Velocity, FlounderTuning.Radius * 0.6f, report: false);
        if (f.Velocity.LengthSquared() > 0.01f) f.Heading = MathF.Atan2(f.Velocity.Y, f.Velocity.X);
        if (Vector2.Distance(f.Position, Player.Position) > ReefFishTuning.LeaveFar || f.Left > ReefFishTuning.LeaveMax) f.Gone = true;
    }

    static void Enter(PlaneFlounder f, FlounderStage stage)
    {
        f.Stage = stage;
        f.Timer = 0f;
    }
}
