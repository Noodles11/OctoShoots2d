using System.Collections.Generic;
using System.Numerics;

namespace OctoShoots.Core.Bosses;

/// <summary>The body plans a boss can grow from (docs/BOSS-GENERATOR.md §3). Later passes add the rest.</summary>
public enum BodyPlan { Bivalve, Echinoderm, Cnidarian, Gastropod }

/// <summary>The real animal a plan is drawn as.</summary>
public enum BodyForm { GiantClam, SeaStar, Urchin, MoonJelly, Siphonophore, SeaHare, Snail }

/// <summary>The designed fight ideas (§4): every boss is one of these.</summary>
public enum BossConcept { TheLid, SnappingGate, TurningStar, SlamAndSigh, PulseBell, TrailingCurtain, TheSpreader, ShyShell, BoilHeart }

public enum BossTier { Common, Rare, Legendary }

/// <summary>When her light can reach the pearl (§6).</summary>
public enum Exposure
{
    /// <summary>Only while the body is open (lid, bell, mantle), on its own cycle.</summary>
    OpenMoments,
    /// <summary>For a moment after its big attack.</summary>
    Stagger,
    /// <summary>Always, but the pearl travels round the body under the skin: only where it is now takes light.</summary>
    MovingWindow,
    /// <summary>Buried: it surfaces once the ink sacs over it are burst.</summary>
    CoaxOut,
}

/// <summary>What it throws (§7.1). All of it is ink her light cannot clear.</summary>
public enum BossAttack { Ring, Wall, Helix, Homing, Snap, ArcBombs, PulseRing, SacBurst }

public enum BossMove { Hold, Turn, Spin, Creep, DriftPulse, Circle }

public enum BossTempo { Metronome, Relentless, Duelist, SurgeAndRest, Territorial }

/// <summary>How it knows where she is: not at all, through the water, or where she last made the floor shake.</summary>
public enum BossSense { Blind, Water, Vibration }

/// <summary>The Leak's rewrite of the body (§5): one dominant theme per boss.</summary>
public enum MutationTheme { OvergrownShell, InkSacs, ExtraLid, ExtraArms, InvertedSpines, FusedBells, ExtraTentacles, ExtraFlaps }

/// <summary>What changes when a phase begins (§7.3).</summary>
public enum PhaseChange { NewAttack, Quicken, ShorterWindow, MutationAdvances }

/// <summary>The one bounded twist that makes two bosses of a concept differ (§4.1).</summary>
public enum BossSpice { ExtraFiller, FastLongWindows, EarlyAdvance, LopsidedOrigin, OffCentrePearl }

/// <summary>
/// A place on the body, in the body's own polar frame: Angle from its facing (radians, counter-clockwise seen from
/// above) and Out from the middle (0 the centre, 1 the rim). Sacs are ink sacs (targets in a Coax-out fight).
/// </summary>
public readonly record struct BodySite(float Angle, float Out, float Size, bool Sac);

/// <summary>A phase: from this share of the pearl's meter lost, this changes (and maybe this attack joins).</summary>
public readonly record struct BossPhase(float At, PhaseChange Change, BossAttack? Joins);

/// <summary>
/// One generated boss (docs/BOSS-GENERATOR.md): everything the sim needs to run its fight and the view needs to draw
/// it, from the run seed, the loop and the depth alone.
/// </summary>
public sealed class BossSpec
{
    public ulong Seed { get; init; }
    public int Cycle { get; init; }
    /// <summary>The depth as the player sees it (1–6).</summary>
    public int Depth { get; init; }
    public float Menace { get; init; }
    public BossTier Tier { get; init; }
    public BodyPlan Plan { get; init; }
    public BodyForm Form { get; init; }
    public BossConcept Concept { get; init; }
    /// <summary>Queen Clam: the hand-tuned roll of The Lid the others are held to.</summary>
    public bool Reference { get; init; }
    public string Name { get; set; } = "";

    // ── the body ──
    /// <summary>The body's solid disc on the plane (m), mutated.</summary>
    public float Radius { get; set; }
    /// <summary>Stretched along its facing (1 none).</summary>
    public float Stretch { get; set; } = 1f;
    /// <summary>Arms (star), spine rows (urchin), oral arms (jelly), bells (siphonophore), flaps (sea hare), shell whorls (snail).</summary>
    public int Count { get; set; }
    /// <summary>The plan's main proportion, 0..1: valve depth, arm length, spine length, bell depth, mantle spread, shell height.</summary>
    public float Bulk { get; set; }
    /// <summary>One side grown out: −1 left … 1 right (0 symmetric).</summary>
    public float Lopsided { get; set; }
    /// <summary>Its true colours (linear RGB): body and accent.</summary>
    public Vector3 TrueColor { get; set; }
    public Vector3 Accent { get; set; }
    /// <summary>The freed creature's size against the mutated body (0.55–0.7).</summary>
    public float TrueScale { get; set; }

    // ── the corruption ──
    public MutationTheme Theme { get; set; }
    public MutationTheme? Minor { get; set; }
    /// <summary>How far the mutation has gone: 0 a touch, 1 most of the mass (Menace-driven).</summary>
    public float Mutation { get; set; }
    public List<BodySite> Sites { get; } = new();
    public bool HasSacs => Sites.Exists(s => s.Sac);

    // ── the pearl ──
    public Exposure Exposure { get; set; }
    /// <summary>Where it sits (for a moving window: the radius of its round, angle 0 at the start).</summary>
    public BodySite Pearl { get; set; }
    public float PearlRadius { get; set; }
    /// <summary>A moving window's pace round the body (rad/s).</summary>
    public float PearlOrbit { get; set; }
    /// <summary>Each ink sac's meter (Coax out).</summary>
    public float SacHp { get; set; }

    // ── the fight ──
    public float Hp { get; set; }
    public float Damage { get; set; }
    public float Telegraph { get; set; }
    /// <summary>The pause between cycles.</summary>
    public float Rest { get; set; }
    /// <summary>Volleys in one act, and the time between them.</summary>
    public int Volleys { get; set; }
    public float VolleyInterval { get; set; }
    /// <summary>The window after its act (Stagger, Coax out); Open moments are open while it fires.</summary>
    public float Window { get; set; }
    public float PearlSpeed { get; set; }
    /// <summary>Pearls in a ring (before the gap) and in a wall.</summary>
    public int RingCount { get; set; }
    public int WallCount { get; set; }
    public int HelixArms { get; set; }
    public BossAttack Signature { get; set; }
    public List<BossAttack> Fillers { get; } = new();
    public BossMove Move { get; set; }
    /// <summary>Creep speed (m/s) or turn rate (rad/s), by its move.</summary>
    public float MoveRate { get; set; }
    public BossTempo Tempo { get; set; }
    public BossSense Sense { get; set; }
    public List<BossPhase> Phases { get; } = new();
    public BossSpice Spice { get; set; }

    /// <summary>Every attack it can throw in this fight (the signature, the fillers, and those phases bring).</summary>
    public IEnumerable<BossAttack> Kit
    {
        get
        {
            yield return Signature;
            foreach (var a in Fillers) yield return a;
            foreach (var p in Phases)
                if (p.Joins is { } j) yield return j;
        }
    }

    /// <summary>A body site's place in the world, the body at <paramref name="at"/> facing <paramref name="facing"/>.</summary>
    public Vector2 SiteAt(BodySite site, Vector2 at, Vector2 facing, float angleOffset = 0f)
    {
        float a = System.MathF.Atan2(facing.Y, facing.X) + site.Angle + angleOffset;
        var local = new Vector2(System.MathF.Cos(a), System.MathF.Sin(a)) * (site.Out * Radius);
        // Stretched along the facing.
        float along = Vector2.Dot(local, facing);
        return at + local + facing * (along * (Stretch - 1f));
    }
}
