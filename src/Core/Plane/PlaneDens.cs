using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Core.Plane;

/// <summary>
/// Dens (DESIGN-TOPDOWN §4.9, docs/CORRUPTION.md): every nave (a chamber off the main route) that holds no place holds
/// a den, a knot of the corruption to clear. Its ground lies thick with ink, a few murklings wait in it, and most dens
/// have a heart: a Blightroot, a Thumpsquirt, or an Inkwarden warding the murklings. Once every creature of it is freed
/// and none is left in the nave, the battle is over: the whole nave is cleansed at once and it pays out, shells and
/// often a light orb.
/// </summary>
public static class DenTuning
{
    /// <summary>Its murklings: Murklings + MurkPerMenace × Menace (rounded).</summary>
    public const int Murklings = 1;
    public const float MurkPerMenace = 1f;
    /// <summary>
    /// Its heart, rolled in turn: a Blightroot, a Thumpsquirt (from its depth), an Inkwarden (from its depth); else
    /// murklings only. Never past the kind's own limit on a level, and never in a nave under SmallNave metres across
    /// (that holds a single murkling).
    /// </summary>
    public const float Root = 0.4f, Thump = 0.3f, Warden = 0.3f, SmallNave = 6f;
    /// <summary>Its ground: inked to at least Ink out to its radius × InkReach.</summary>
    public const float Ink = 0.8f, InkReach = 1.1f;
    /// <summary>Cleared: cleansed out to its radius + Clear metres.</summary>
    public const float Clear = 3f;
    /// <summary>The payout: Shells + ShellsPerMenace × Menace shells (give or take one), and a light orb this often.</summary>
    public const int Shells = 4;
    public const float ShellsPerMenace = 2f, OrbChance = 0.4f;
    /// <summary>Its murklings drift back home within their nave's radius × Leash while she is away.</summary>
    public const float Leash = 0.8f;
}

/// <summary>What a den's heart is.</summary>
public enum DenHeart { None, Blightroot, Thumpsquirt, Inkwarden }

/// <summary>A den: its nave, its heart, the creatures of it, and whether its battle is over.</summary>
public sealed class Den
{
    public Vector2 At;
    public float Radius;
    public DenHeart Heart;
    /// <summary>Whether each of its creatures is still corrupted.</summary>
    public readonly List<Func<bool>> Members = new();
    public bool Cleared;
}

public sealed partial class PlaneWorld
{
    public List<Den> Dens { get; } = new();

    /// <summary>
    /// A den in every nave that holds no place (the tutorial's too), but for one in the arena or in a safe place's clean
    /// water; a template's absent kinds stay absent, and a quiet level's dens and the small naves' hold a single murkling.
    /// </summary>
    void PlaceDens(CorruptionField field)
    {
        var reach = LevelValidator.Distances(Map, Map.Start.Position);
        foreach (var nave in Map.Naves)
        {
            if (!nave.Den) continue;
            if (Map.HasBoss && Vector2.Distance(nave.At, ArenaCenter) < ArenaRadius + nave.Radius) continue;
            // A nave in a safe place's clean water (round the start, a shop, a clam room) holds no den.
            if (field.Guard[CorruptionField.Index((int)(nave.At.X / CorruptionTuning.Cell), (int)(nave.At.Y / CorruptionTuning.Cell))] < 0.5f) continue;
            var den = new Den { At = nave.At, Radius = nave.Radius };
            // Its ground lies thick with ink.
            field.Spread(nave.At, nave.Radius * DenTuning.InkReach, DenTuning.Ink, 1f);
            // A spot in it: well inside, clear of rock, where she can reach.
            Vector2? Spot(float body)
            {
                for (int tries = 0; tries < 40; tries++)
                {
                    float a = _blightRng.Range(0f, MathF.Tau), r = _blightRng.Range(0f, nave.Radius * 0.55f);
                    var at = nave.At + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
                    if (!Clear(at, body) || float.IsPositiveInfinity(LevelValidator.DistanceAt(reach, at))) continue;
                    if (Map.Shaft.Contains(at, 3f)) continue;
                    return at;
                }
                return null;
            }
            bool small = nave.Radius < DenTuning.SmallNave;
            den.Heart = Quiet || small ? DenHeart.None : RollHeart();
            switch (den.Heart)
            {
                // A Blightroot never stands near a safe place (it would seed its corruption there).
                case DenHeart.Blightroot when Spot(1.2f) is { } at && !SafePlaces().Any(sp => Vector2.Distance(at, sp.Centre) < sp.Radius + BlightTuning.SafeDistance):
                    var root = AddBlightroot(at, field);
                    den.Members.Add(() => root.Alive);
                    break;
                case DenHeart.Thumpsquirt when Spot(1.2f) is { } at:
                    var squirt = AddThumpsquirt(at);
                    den.Members.Add(() => squirt.Alive);
                    break;
                case DenHeart.Inkwarden when Spot(WardenTuning.Radius) is { } at:
                    var warden = NewWarden(at, roams: false);
                    Inkwardens.Add(warden);
                    den.Members.Add(() => warden.Alive);
                    break;
                default:
                    den.Heart = DenHeart.None;
                    break;
            }
            int murks = Quiet || small ? 1 : DenTuning.Murklings + (int)MathF.Round(DenTuning.MurkPerMenace * Menace);
            for (int i = 0; i < murks; i++)
            {
                if (Spot(BlightTuning.MurkRadius) is not { } at || Bud(at, -1, drink: false) is not { } m) continue;
                // Already out of the ground, waiting in its nave.
                Events.RemoveAt(Events.Count - 1);
                m.Bud = 0f;
                m.Home = nave.At;
                m.HomeRadius = nave.Radius * DenTuning.Leash;
                den.Members.Add(() => m.Alive);
            }
            if (den.Members.Count > 0) Dens.Add(den);
        }
    }

    /// <summary>
    /// A den's heart, by the depth's kinds: never one the level's template leaves out, nor past the most of it the
    /// template casts (else the kind's own limit on a level).
    /// </summary>
    DenHeart RollHeart()
    {
        bool Room(CastKind kind, int have, int max)
        {
            if (CastOf(kind) is not { } cast) return have < max;
            return !cast.None && have < Math.Min(max, cast.Count[^1] + (int)MathF.Round(cast.PerMenace * Menace));
        }
        float roll = _blightRng.NextFloat();
        if (roll < DenTuning.Root) return Room(CastKind.Blightroot, Blightroots.Count, BlightTuning.Max) ? DenHeart.Blightroot : DenHeart.None;
        roll -= DenTuning.Root;
        if (roll < DenTuning.Thump)
            return Map.Depth >= ThumpTuning.FirstDepth && Room(CastKind.Thumpsquirt, Thumpsquirts.Count, ThumpTuning.Max) ? DenHeart.Thumpsquirt : DenHeart.None;
        return Map.Depth >= WardenTuning.FirstDepth && Room(CastKind.Inkwarden, Inkwardens.Count, WardenTuning.Max) ? DenHeart.Inkwarden : DenHeart.None;
    }

    /// <summary>
    /// A den's battle is over once every creature of it is freed and no murkling is left in its nave: it is cleansed
    /// whole, and it pays out.
    /// </summary>
    void StepDens(CorruptionField field)
    {
        if (Tick % 6 != 0) return;
        foreach (var den in Dens)
        {
            if (den.Cleared || den.Members.Any(alive => alive())) continue;
            if (Murklings.Any(m => m.Alive && Vector2.Distance(m.Position, den.At) < den.Radius)) continue;
            den.Cleared = true;
            // Whole: strong enough that even its rim, where a cleansing fades, is left clean.
            field.Cleanse(den.At, den.Radius + DenTuning.Clear, 3f);
            int shells = DenTuning.Shells + (int)MathF.Round(DenTuning.ShellsPerMenace * Menace) + _blightRng.Int(3) - 1;
            for (int i = 0; i < shells; i++) Shells.Add(new PlaneShell { Position = den.At, Velocity = Fling(2f, 5f) });
            bool orb = _blightRng.NextFloat() < DenTuning.OrbChance;
            if (orb) Hearts.Add(new PlaneHeart { Position = den.At, Velocity = Fling(0.8f, 1.6f) });
            Events.Add(new PlaneEvent(PlaneEventType.DenCleared, den.At, new Vector2(den.Radius, orb ? 1f : 0f), den.Radius + DenTuning.Clear));
        }
    }
}
