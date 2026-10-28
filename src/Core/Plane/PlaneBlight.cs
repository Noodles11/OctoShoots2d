using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;

namespace OctoShoots.Core.Plane;

/// <summary>Blightroots and murklings (docs/CORRUPTION.md). Provisional numbers.</summary>
public static class BlightTuning
{
    // Blightroot: the source of corruption in its reach.
    public const float Reach = 9f;
    /// <summary>It regrows its reach toward the field's starting density at this rate (density per second).</summary>
    public const float Regrow = 0.1f;
    /// <summary>
    /// Its corruption meter (shot by her light): about ten default shots empty it; it re-corrupts quickly, RootRegen a
    /// second (more with Menace) once RootDelay has passed without a hit. Empty, it bursts.
    /// </summary>
    public const float RootCorruption = 125f, RootRegen = 30f, RootDelay = 0.5f;
    /// <summary>The ink sac: where her light strikes it.</summary>
    public const float SacRadius = 0.9f;
    /// <summary>Its burst sends a wave of cleansing through its whole reach in this long.</summary>
    public const float BurstSeconds = 0.7f;
    /// <summary>Every PulseEvery seconds: the sac inflates (Telegraph), then a ring of ink runs out at PulseSpeed.</summary>
    public const float PulseEvery = 4f, Telegraph = 0.8f, PulseSpeed = 10f, PulseBand = 0.9f;
    /// <summary>The ring stains cleansed ground back to this density and dims the bubbles it crosses to this much.</summary>
    public const float PulseStain = 0.35f, PulseDim = 0.7f;
    /// <summary>
    /// The ring corrupts her as it passes over her (once per ring, more with Menace). A dash through it (she is
    /// untouchable while dashing) takes nothing. Her shots it crosses are snuffed out.
    /// </summary>
    public const float PulseDamage = 6f, PulseDamagePerMenace = 2f;
    /// <summary>Every creature's corruption meter grows this much per point of Menace (deeper, tougher).</summary>
    public const float MeterPerMenace = 0.2f;
    /// <summary>Where they stand: never near safe places (beyond their radius), apart, more with Menace.</summary>
    public const float SafeDistance = 18f, Spacing = 22f;
    public const int Base = 3, Max = 6;
    public const float PerMenace = 1.5f;

    /// <summary>
    /// Its hammer: when she comes within HammerReach of it, it rears back away from her (HammerWindup, its aim following
    /// her), then swings its head down at her like a hammer (HammerStrike): if she is still within HammerHit of it and in
    /// front of it as it lands, it corrupts her (HammerDamage, more with Menace) and knocks her away at HammerKnock m/s. It
    /// swings again HammerCooldown after it has recovered (HammerRecover), and never while it gathers a pulse.
    /// </summary>
    public const float HammerReach = 4.2f, HammerHit = 4.6f, HammerWindup = 0.5f, HammerStrike = 0.14f, HammerRecover = 0.55f, HammerCooldown = 1.2f;
    public const float HammerDamage = 10f, HammerDamagePerMenace = 3f, HammerKnock = 9f;

    // Murklings: orbs of darkness budding from the corruption.
    public const float MurkRadius = 0.45f, MurkSpeed = 2.6f, MurkSpeedPerMenace = 0.4f, Sense = 14f;
    /// <summary>
    /// Its corruption meter: four default shots in a row empty it (a pause lets it regrow, MurkRegen a second, more
    /// with Menace, once MurkDelay has passed without a hit). One strong enough shot empties it at once.
    /// </summary>
    public const float MurkCorruption = 36f, MurkRegen = 14f, MurkDelay = 0.8f;
    public const float BudSeconds = 0.8f, BudInterval = 15f, BudDensity = 0.6f, BudClearance = 5f;
    /// <summary>
    /// A murkling is made of ink: budding, it drinks Ink (density × m², about a 1.2 m disc of thick ink) from anywhere
    /// on the level, the nearest ink first and then further out until it has it, over its bud. An ambush's buds are the
    /// trap's own ink and drink nothing; they gather a GatherRadius disc.
    /// </summary>
    public const float Ink = 3.6f, GatherRadius = 1.2f;
    /// <summary>
    /// A murkling prime: a rare bud (PrimeChance + PrimeChancePerMenace × Menace), at most one alive per area and
    /// PrimePerLevel a level, at least PrimeClearance metres from her, never from ambushes or guards. It drinks PrimeInk
    /// (twelve murklings' worth) over PrimeBud seconds into a body PrimeScale times a murkling's (colliding as PrimeBody).
    /// </summary>
    public const float PrimeChance = 0.06f, PrimeChancePerMenace = 0.04f, PrimeInk = Ink * 12f, PrimeClearance = 10f, PrimeBud = 2f;
    public const float PrimeScale = 5f, PrimeBody = 1.4f;
    public const int PrimePerLevel = 2;
    /// <summary>
    /// A prime's stats against a murkling's: meter ×PrimeMeter; contact and throws ×PrimeHit (its drop PrimeHit times as
    /// big, splashing PrimeSplash on fish); sense and throw range ×PrimeReach; pace ×PrimeSpeed; its wind-up
    /// PrimeWindup. It rams instead of clinging, shoving her off at PrimeShove.
    /// </summary>
    public const float PrimeMeter = 10f, PrimeHit = 2.5f, PrimeSplash = 2f, PrimeReach = 1.5f, PrimeSpeed = 0.7f, PrimeWindup = 1f, PrimeShove = 9f;
    /// <summary>Freed, a prime's ember blooms PrimeBloom metres; it always leaves PrimeShellsMin–Max shells, and a light orb PrimeOrb of the time.</summary>
    public const float PrimeBloom = 6f, PrimeOrb = 0.3f;
    public const int PrimeShellsMin = 4, PrimeShellsMax = 6;
    /// <summary>Freed, a prime breaks apart into this many murklings, flung out round where it burst, each made of a murkling's ink of its own (the rest is cleansed).</summary>
    public const int PrimeSplit = 5;
    /// <summary>An area stops budding for good once it is cleansed this far (below a fifth of its corruption left).</summary>
    public const float AreaSpent = 0.8f;
    public const int CapBase = 2;
    public const float CapPerMenace = 1.5f;
    public const float ContactDamage = 6f, ClingDrain = 2.5f;
    public const int MaxClingers = 3;
    public const float ThrowEvery = 2.4f, ThrowRange = 9f, ThrowSpeed = 10f, ThrowDamage = 7f, ThrowRadius = 0.1f, ThrowLife = 3f;
    /// <summary>
    /// A throw that flies ThrowFallAt metres without hitting anything falls: it sinks to the sand over ThrowFallSeconds
    /// (harmless now, under her plane) and stains it, corrupting ThrowStainRadius toward ThrowStainInk. One her light
    /// strikes in the air bursts in a splatter instead, staining nothing and cleansing nothing.
    /// </summary>
    public const float ThrowFallAt = 9f, ThrowFallSeconds = 0.45f, ThrowStainRadius = 0.9f, ThrowStainInk = 0.7f;
    /// <summary>
    /// The throw's telegraph, a slingshot: it bends back away from its aim for WindupSeconds less SnapSeconds (its aim
    /// following the target), then snaps forward over SnapSeconds with its aim locked, and the piece of it flies at the
    /// end. It crawls at WindupSlow of its pace meanwhile. Deeper, the bend is shorter (÷ 1 + WindupPerMenace × Menace).
    /// </summary>
    public const float WindupSeconds = 0.55f, SnapSeconds = 0.12f, WindupPerMenace = 0.3f, WindupSlow = 0.25f;
    /// <summary>Deeper, its throws come faster and more often (per point of Menace).</summary>
    public const float ThrowSpeedPerMenace = 1.5f, ThrowRatePerMenace = 0.25f;
    /// <summary>A bubble that bursts a murkling or meets its throw keeps this much of its light.</summary>
    public const float BubbleDim = 0.5f;
    /// <summary>A dash throws a clinging murkling off: one default shot's worth of light, flung this fast, dazed this long.</summary>
    public const float ShakeLight = 10f, ShakeSpeed = 9f, ShakeSeconds = 0.7f;
    /// <summary>A steady drain (a creature feeding on her) shows as one number: all it took over each second.</summary>
    public const float DrainReport = 1f;
    /// <summary>The arena buds faster while the boss fights.</summary>
    public const float ArenaInterval = 6f;
    /// <summary>
    /// Its guardians: while a Blightroot has been struck by her light within GuardWindow seconds, its area buds every
    /// GuardInterval / (1 + GuardPerMenace × Menace) seconds (5 s early, 2 s by the Tank), in a ring GuardNear–GuardFar
    /// metres round it, its cap raised by GuardCap + round(GuardCapPerMenace × Menace).
    /// </summary>
    public const float GuardWindow = 3f, GuardInterval = 5f, GuardPerMenace = 1.5f, GuardNear = 4f, GuardFar = 7f, GuardCapPerMenace = 2f;
    public const int GuardCap = 1;
    /// <summary>An ambush buds this many murklings round her.</summary>
    public const int AmbushBuds = 3;
}

/// <summary>
/// A corrupted creature's corruption meter (docs/CORRUPTION.md): full at first; each shot of her light takes its damage
/// (illuminance) off; after Delay without a hit it regrows at Regen a second. Emptied, the creature is cleansed. It is
/// shown (a ring round the creature) only while partly emptied: not when full, not once empty.
/// </summary>
public sealed class CorruptionMeter
{
    public float Max, Value, Regen, Delay, Since;
    /// <summary>What the last hit took off (for the view's after-image of the chunk).</summary>
    public float LastHit;

    public CorruptionMeter(float max, float regen, float delay)
    {
        Max = Value = max;
        Regen = regen;
        Delay = delay;
    }

    public float Fraction => Max <= 0f ? 0f : Value / Max;
    public bool Shown => Value > 0f && Value < Max;
    public bool Empty => Value <= 0f;

    /// <summary>A shot of her light: true when it empties the meter.</summary>
    public bool Hit(float light)
    {
        if (Empty) return false;
        LastHit = MathF.Min(light, Value);
        Value = MathF.Max(0f, Value - light);
        Since = 0f;
        return Empty;
    }

    public void Step(float dt)
    {
        if (Empty) return;
        Since += dt;
        if (Since >= Delay) Value = MathF.Min(Max, Value + Regen * dt);
    }
}

/// <summary>A Blightroot: a blackened stump crowned with a pulsing ink sac.</summary>
public sealed class Blightroot
{
    public Vector2 Position;
    public bool Alive = true;
    /// <summary>Its area (index into Areas): the cells of its reach.</summary>
    public int Area;
    /// <summary>Its corruption meter: her shots empty it; empty, it bursts.</summary>
    public CorruptionMeter Corruption = new(BlightTuning.RootCorruption, BlightTuning.RootRegen, BlightTuning.RootDelay);
    /// <summary>Distance of each of its area's cells from it (same order as the area's cells).</summary>
    public float[] CellDistance = Array.Empty<float>();
    public float PulseTimer;
    /// <summary>The ring's radius while one runs out (negative: none); its number (each bubble is dimmed once per ring).</summary>
    public float PulseRadius = -1f;
    public int PulseId;
    /// <summary>The sac's inflation for the telegraph, 0..1.</summary>
    public float Inflate;
    /// <summary>Its hammer: seconds into a swing (negative: none), the way it swings (at her), and how long until it may swing again.</summary>
    public float Swing = -1f, SwingCooldown;
    public Vector2 SwingDir;

    /// <summary>The burst's cleansing wave radius (negative: not bursting).</summary>
    public float BurstRadius = -1f;
    public float Dead;
    /// <summary>Under attack: struck by her light within GuardWindow seconds (its guardians awake).</summary>
    public bool Guarded => Alive && Corruption.LastHit > 0f && Corruption.Since < BlightTuning.GuardWindow;
    /// <summary>Whether its guardians were awake last step (to tell when they wake).</summary>
    public bool WasGuarded;
    /// <summary>Where its guardians bud round (its level template may set a place it guards; else itself).</summary>
    public Vector2? GuardAt;
}

/// <summary>A murkling: a wobbling orb of darkness with a dim stolen ember inside.</summary>
public sealed class Murkling
{
    /// <summary>Shoved by her hits (PlaneKnock): this velocity, easing off.</summary>
    public Vector2 Knock;
    public Vector2 Position, Velocity;
    /// <summary>
    /// A lesser murkling: the reef fish it was, turned by a murkling's throw (null for a murkling budded from the ink).
    /// It never throws, chases her a little faster and clings; freed, it is that fish again.
    /// </summary>
    public PlaneReefFish? Fish;
    public bool Lesser => Fish is not null;
    /// <summary>A lesser murkling's swarm (the school it was turned from), -1 for none: a swarm keeps together.</summary>
    public int Swarm = -1;
    /// <summary>Its body's radius (a lesser one's follows the fish it was), and what it drains a second while clinging.</summary>
    public float Radius = BlightTuning.MurkRadius, ClingDrain = BlightTuning.ClingDrain;
    /// <summary>The body it moves as (a prime collides smaller than it looks, so it can squeeze through the passes).</summary>
    public float Body = BlightTuning.MurkRadius;
    /// <summary>A murkling prime (rare, five times the size, every stat scaled up); its bud's length.</summary>
    public bool Prime;
    /// <summary>One of Drippus Prime's brood: born out of its body (it pops when Drippus is freed).</summary>
    public bool Brood;
    public float BudTotal = BlightTuning.BudSeconds;
    /// <summary>The ink it is drinking while it buds (null once drunk, or for one that drinks nothing), and the ink it is made of (field units, freed when it bursts).</summary>
    public InkDraw? Draw;
    public float Matter;
    /// <summary>Metres swum since it last left a dot of ink behind (the adventure's trails); until her oral arm may sting it again.</summary>
    public float Trail, ArmCooldown;
    /// <summary>How far it senses her (and fish), and how far it throws.</summary>
    public float Sense => Prime ? BlightTuning.Sense * BlightTuning.PrimeReach : BlightTuning.Sense;
    public float ThrowRange => Prime ? BlightTuning.ThrowRange * BlightTuning.PrimeReach : BlightTuning.ThrowRange;
    /// <summary>A murkling hunting the reef's fish while she is away: the one it is after, and when it looks again.</summary>
    public PlaneReefFish? Prey;
    public float PreyTimer;
    /// <summary>
    /// Winding up a throw (seconds left, out of WindupTotal; 0 when not), which way it aims, and the fish it throws at
    /// (null: at her). ThrowAge counts the seconds since its last throw left it (the view's recoil).
    /// </summary>
    public float Windup, WindupTotal, ThrowAge = 10f;
    public Vector2 Aim;
    public PlaneReefFish? WindupPrey;
    /// <summary>Budding out of the ground (counting down; above zero it is not yet out).</summary>
    public float Bud = BlightTuning.BudSeconds;
    public int Area = -1;
    public bool Alive = true, Clinging;
    public Vector2 ClingOffset;
    public float ThrowTimer, Seed;
    /// <summary>The Blotter it guards (it budded round one), if any.</summary>
    public Blotter? Guarding;
    /// <summary>A den's murkling: its nave (centre and radius), where it drifts back to while she is away.</summary>
    public Vector2? Home;
    public float HomeRadius;
    /// <summary>Thrown off her by a dash (counting down): it tumbles away and cannot cling or chase until it recovers.</summary>
    public float Shaken;
    /// <summary>Its corruption meter: her shots empty it; empty, it bursts.</summary>
    public CorruptionMeter Corruption = new(BlightTuning.MurkCorruption, BlightTuning.MurkRegen, BlightTuning.MurkDelay);
    /// <summary>Drifting toward this light (her, or a hanging bubble of hers).</summary>
    public Vector2 Target;
    public bool Budding => Bud > 0f;
}

/// <summary>A murkling's throw: a small black orb that drains light.</summary>
public sealed class MurkShot
{
    public Vector2 Position, Velocity;
    /// <summary>Where it left the murkling's body (the view draws it pinching off from there).</summary>
    public Vector2 From;
    /// <summary>Its size, what it does to her, its splash on fish and its stain where it lands (a prime's are bigger).</summary>
    public float Radius = BlightTuning.ThrowRadius, Damage = BlightTuning.ThrowDamage, Splash = MurkFishTuning.Splash, Stain = BlightTuning.ThrowStainRadius;
    public float Life = BlightTuning.ThrowLife;
    /// <summary>How far it has flown.</summary>
    public float Traveled;
    /// <summary>Seconds it has been falling to the sand (0: still flying on her plane).</summary>
    public float Fall;
    /// <summary>An Inkwarden's spike (the adventure's spiky ring), not a murkling's throw: drawn as a thin spine.</summary>
    public bool Spike;
    public bool Airborne => Fall <= 0f;
}

/// <summary>Where murklings bud: a Blightroot's reach, or the arena.</summary>
public sealed class CorruptArea
{
    public Vector2 Center;
    public float Radius;
    public List<int> Cells = new();
    public bool Arena, Spent;
    public float Cleansed, BudTimer;
}

public sealed partial class PlaneWorld
{
    public PlaneOptions Options { get; private set; } = PlaneOptions.Default;
    /// <summary>The corruption field (null when the corruption is off).</summary>
    public CorruptionField? Corruption { get; private set; }
    public List<Blightroot> Blightroots { get; } = new();
    public List<Murkling> Murklings { get; } = new();
    /// <summary>Murklings made during a step that join Murklings at its end (a prime's pieces: Bud while _budLater).</summary>
    readonly List<Murkling> _madeLater = new();
    bool _budLater;
    public List<MurkShot> MurkShots { get; } = new();
    public List<CorruptArea> Areas { get; } = new();
    /// <summary>The arena's area (boss levels), or null.</summary>
    public CorruptArea? ArenaArea { get; private set; }
    /// <summary>The level cleansed, 0..1.</summary>
    public float Cleansed => Corruption?.Cleansed ?? 1f;
    /// <summary>The share of the arena cleansed, 0..1.</summary>
    public float ArenaCleansed => ArenaArea?.Cleansed ?? 1f;
    /// <summary>Murklings clinging to her (each dims her glow); lesser ones are counted apart.</summary>
    public int Clingers => Murklings.Count(m => m.Alive && m.Clinging && !m.Lesser);
    public int LesserClingers => Murklings.Count(m => m.Alive && m.Clinging && m.Lesser);

    Rng _blightRng = null!;
    /// <summary>Murkling primes budded this level (at most PrimePerLevel).</summary>
    int _primesBorn;
    readonly List<(Vector2 At, int Bubbles, float Dim)> _pops = new();
    float _linger, _drained, _drainedBefore, _drainClock, _drainIdle;
    DamageSource _drainSource;

    IEnumerable<(Vector2 Centre, float Radius)> SafePlaces() => CorruptionField.SafeOf(Map);

    void PlaceCorruption()
    {
        _blightRng = new Rng(Map.Seed ^ 0xB1167UL ^ ((ulong)Map.Level << 22) ^ ((ulong)Map.Depth << 30) ^ ((ulong)Map.Attempt << 46));
        // The adventure's ink is in the water (InkDots), not on the ground: its field starts clean.
        var field = Corruption = Options.Adventure ? new CorruptionField() : CorruptionField.ForMap(Map);
        if (Options.Adventure) return;
        // The tutorial's open rift has no arena to cleanse: its Crack is open from the start.
        if (Map.HasBoss && !Map.Rift)
        {
            ArenaArea = new CorruptArea { Center = ArenaCenter, Radius = ArenaRadius, Arena = true, Cells = field.CellsWithin(ArenaCenter, ArenaRadius) };
            Areas.Add(ArenaArea);
        }
        PlaceBlightroots();
        PlaceBlotters(field);
        PlaceThumpsquirts();
        PlaceInkwardens();
        PlaceLashjaws();
        PlaceTemplateGuards();
        PlaceDens(field);
        if (Boss is { } boss) boss.Crust = CorruptionTuning.CrustLayers;
        foreach (var area in Areas)
        {
            area.Cleansed = field.CleansedOf(area.Cells);
            area.BudTimer = _blightRng.Range(3f, BudInterval());
        }
    }

    void PlaceBlightroots()
    {
        var field = Corruption!;
        var reach = LevelValidator.Distances(Map, Map.Start.Position);
        var cast = CastOf(CastKind.Blightroot);
        if (cast is { None: true }) return;
        var candidates = Map.Plazas.Select(p => p.Center).ToList();
        foreach (var c in Map.Corridors.Where(c => c.Kind is CorridorKind.Main or CorridorKind.Side || cast is not null && c.Kind == CorridorKind.Spur))
            for (int i = 0; i < c.Points.Count; i += cast is null ? 3 : 1) candidates.Add(c.Points[i]);
        // With a template, every chamber's ring too, so its anchors have spots to choose from.
        if (cast is not null)
            foreach (var ch in Map.Chambers)
                for (int k = 0; k < 8; k++)
                    candidates.Add(ch.At + new Vector2(MathF.Cos(k * MathF.Tau / 8f), MathF.Sin(k * MathF.Tau / 8f)) * ch.Radius * 0.6f);
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = _blightRng.Int(i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }
        var safes = SafePlaces().ToList();
        int want = cast is not null ? CastCount(cast, BlightTuning.Max)
            : Math.Min(BlightTuning.Max, BlightTuning.Base + (int)MathF.Round(Menace * BlightTuning.PerMenace));
        foreach (var at in candidates)
        {
            if (Blightroots.Count >= want) break;
            if (float.IsPositiveInfinity(LevelValidator.DistanceAt(reach, at)) || !Clear(at, 1.2f)) continue;
            if (!Anchored(cast, Blightroots.Count, at)) continue;
            // It stands in the corruption, never on clean floor.
            if (field.At(at) < 0.5f) continue;
            // Never near a safe place, but for the one its template has it guard (it may stand at that one's door).
            var guards = AnchorPlace(cast, Blightroots.Count);
            if (safes.Any(s => Vector2.Distance(at, s.Centre) < s.Radius + (guards is { } g && Map.Pois.Any(q => q.Kind == g && q.Position == s.Centre) ? 4f : BlightTuning.SafeDistance))) continue;
            if (Map.HasBoss && Vector2.Distance(at, ArenaCenter) < ArenaRadius + BlightTuning.Reach + 2f) continue;
            if (Map.Shaft.Contains(at, BlightTuning.Reach)) continue;
            if (Blightroots.Any(b => Vector2.Distance(b.Position, at) < BlightTuning.Spacing)) continue;
            AddBlightroot(at, field);
        }
    }

    /// <summary>A Blightroot at a spot: its area of ground, its meter.</summary>
    Blightroot AddBlightroot(Vector2 at, CorruptionField field)
    {
        var root = new Blightroot { Position = at, PulseTimer = _blightRng.Range(1.5f, BlightTuning.PulseEvery) };
        var area = new CorruptArea { Center = at, Radius = BlightTuning.Reach, Cells = field.CellsWithin(at, BlightTuning.Reach) };
        root.Area = Areas.Count;
        Areas.Add(area);
        root.Corruption.Regen *= 1f + 0.5f * Menace;
        root.Corruption.Max = root.Corruption.Value = BlightTuning.RootCorruption * (1f + BlightTuning.MeterPerMenace * Menace);
        root.CellDistance = area.Cells.Select(i => Vector2.Distance(CorruptionField.CellCentre(i % CorruptionField.N, i / CorruptionField.N), at)).ToArray();
        Blightroots.Add(root);
        return root;
    }

    float BudInterval()
    {
        float interval = BlightTuning.BudInterval / (1f + 0.6f * Menace) / BudRateFactor;
        return Cleansed >= CorruptionTuning.SlowBudding ? interval * 2f : interval;
    }

    float GuardInterval() => BlightTuning.GuardInterval / (1f + BlightTuning.GuardPerMenace * Menace);

    int BudCap => BlightTuning.CapBase + (int)(Menace * BlightTuning.CapPerMenace) + BudCapBonus;

    /// <summary>The war's turn, after combat: her light, pops, Blightroots, murklings, the arena.</summary>
    void StepCorruption()
    {
        var field = Corruption;
        if (field is null) return;
        var p = Player;

        // Her light cleanses the ground beneath her (Lantern Pearl's glow reaches farther).
        float glow = Run.Loadout.Stats[Stat.Glow];
        field.Cleanse(p.Position, CorruptionTuning.HerRadius * (0.8f + 0.2f * glow), CorruptionTuning.HerRate * Dt);
        LightDots(p.Position, CorruptionTuning.HerRadius * (0.8f + 0.2f * glow), CorruptionTuning.HerRate * Dt);
        // Her bubbles cleanse a burst where they pop, wider for a bigger bubble.
        foreach (var (at, bubbles, dim) in _pops)
        {
            float popReach = MathF.Min(CorruptionTuning.PopRadius + CorruptionTuning.PopPerBubble * bubbles, CorruptionTuning.PopMax);
            field.Cleanse(at, popReach, CorruptionTuning.PopStrength * dim);
            LightDots(at, popReach, CorruptionTuning.PopStrength * dim * AdventureTuning.PopBite);
        }

        StepBlightroots(field);
        StepBlotters(field);
        StepThumpsquirts(field);
        StepInkwardens();
        StepLashjaws();
        StepMurklings(field);
        StepDens(field);
        _pops.Clear();

        if (Tick % 15 == 0)
            foreach (var area in Areas)
            {
                area.Cleansed = field.CleansedOf(area.Cells);
                if (!area.Arena && area.Cleansed >= BlightTuning.AreaSpent) area.Spent = true;
            }
        StepCondensation();

        // Drains (clinging, coiling) show as one hit a second, summed over it. The second starts with the
        // first drop drunk; whatever is left shows once the feeding has stopped a moment, or she is lost.
        if (_drained > 0f) _drainClock += Dt;
        _drainIdle = _drained != _drainedBefore ? 0f : _drainIdle + Dt;
        _drainedBefore = _drained;
        if (_drained > 0f && (_drainClock >= BlightTuning.DrainReport - 1e-4f || _drainIdle >= 0.25f || Defeated))
        {
            Events.Add(new PlaneEvent(PlaneEventType.PlayerDrained, p.Position, Vector2.Zero, _drained, _drainSource));
            _drained = _drainedBefore = 0f;
            _drainClock = 0f;
        }
    }

    /// <summary>Her light drunk away (no hit grace: a steady drain), unless shielded or dashing.</summary>
    void Drain(float amount, DamageSource source)
    {
        var p = Player;
        if (Defeated || amount <= 0f || p.ShieldTimer > 0f || p.DashInvulnerableTimer > 0f) return;
        p.Hp -= amount;
        Stats.DamageTaken += amount;
        LastHitSource = _drainSource = source;
        _drained += amount;
        if (p.Hp <= 0f)
        {
            p.Hp = 0f;
            p.Velocity = Vector2.Zero;
            Defeated = true;
            Events.Add(new PlaneEvent(PlaneEventType.PlayerDefeated, p.Position, Vector2.Zero, 0f, source));
        }
        Run.Hp = p.Hp;
    }

    /// <summary>
    /// A burst of her light (the Starburst Gland): it cleanses the ground it floods and strikes every corrupted
    /// creature in reach, each taking the burst's light off its meter.
    /// </summary>
    /// <summary>Tests: a burst of her light here (as the Starburst Gland's).</summary>
    public void DebugBurst(Vector2 at, float reach, float light) => BurstOnCorruption(at, reach, light);

    void BurstOnCorruption(Vector2 at, float reach, float light, bool cleanse = true)
    {
        if (Corruption is not { } field) return;
        if (cleanse)
        {
            field.Cleanse(at, reach, 0.9f);
            LightDots(at, reach, 0.9f * AdventureTuning.BurstBite);
        }
        // Over a copy of each list: freeing one may change them.
        foreach (var m in Murklings.ToArray())
        {
            if (!m.Alive || m.Clinging || Vector2.Distance(m.Position, at) > reach + m.Radius) continue;
            float dealt = Ward(m, m.Position, light);
            bool emptied = dealt > 0f && m.Corruption.Hit(dealt);
            Events.Add(new PlaneEvent(emptied ? PlaneEventType.MobDefeated : PlaneEventType.MobHit, m.Position, Vector2.Zero, dealt));
            if (emptied) BurstMurkling(m, ember: true);
        }
        foreach (var b in Blotters.ToArray())
        {
            if (!b.Alive || b.Rise > 0f || !b.Touches(at, reach)) continue;
            b.SinceStruck = 0f;
            float dealt = Ward(b, b.Position, light);
            bool emptied = dealt > 0f && b.Corruption.Hit(dealt);
            Events.Add(new PlaneEvent(emptied ? PlaneEventType.MobDefeated : PlaneEventType.MobHit, b.Position, Vector2.Zero, dealt));
            if (emptied) BurstBlotter(b, field);
        }
        foreach (var l in Lashjaws)
        {
            if (!l.Alive || Vector2.Distance(l.Position, at) > reach + LashTuning.Head) continue;
            float dealt = Ward(l, l.Position, light);
            bool emptied = dealt > 0f && l.Corruption.Hit(dealt);
            Events.Add(new PlaneEvent(emptied && l.Phase == 2 ? PlaneEventType.MobDefeated : PlaneEventType.MobHit, l.Position, Vector2.Zero, dealt));
            if (emptied) BreakLashjaw(l);
        }
        foreach (var w in Inkwardens)
        {
            if (!w.Alive || Vector2.Distance(w.Position, at) > reach + WardenTuning.Radius) continue;
            bool emptied = w.Corruption.Hit(light);
            Events.Add(new PlaneEvent(emptied ? PlaneEventType.MobDefeated : PlaneEventType.MobHit, w.Position, Vector2.Zero, light));
            if (emptied) FreeWarden(w);
        }
        foreach (var t in Thumpsquirts.ToArray())
        {
            if (!t.Alive || t.Airborne || Vector2.Distance(t.Position, at) > reach + ThumpTuning.Radius) continue;
            float dealt = Ward(t, t.Position, light);
            bool emptied = dealt > 0f && t.Corruption.Hit(dealt);
            Events.Add(new PlaneEvent(emptied ? PlaneEventType.MobDefeated : PlaneEventType.MobHit, t.Position, Vector2.Zero, dealt));
            if (emptied) BurstThump(t);
        }
        foreach (var root in Blightroots)
        {
            if (!root.Alive || Vector2.Distance(root.Position, at) > reach + BlightTuning.SacRadius) continue;
            // Emptied, it bursts on its own next step.
            float dealt = Ward(root, root.Position, light);
            if (dealt > 0f) root.Corruption.Hit(dealt);
            Events.Add(new PlaneEvent(PlaneEventType.MobHit, root.Position, Vector2.Zero, dealt));
        }
    }

    /// <summary>One of her bubbles popped: its light cleanses after combat.</summary>
    void LightReleased(PlaneShot shot)
    {
        if (Corruption is null || !shot.FromPlayer) return;
        _pops.Add((shot.Position, shot.Bubbles, shot.Dim));
    }

    /// <summary>
    /// A dash: her light bursts out. Clinging murklings are thrown off, each losing one shot's worth of corruption (one
    /// already worn down bursts), and come back for her once they recover; a coiling vine lets go.
    /// </summary>
    void DashFreed()
    {
        if (Corruption is null) return;
        var p = Player;
        foreach (var m in Murklings)
        {
            if (!m.Alive || !m.Clinging) continue;
            m.Clinging = false;
            var away = SafeNormalize(m.ClingOffset);
            if (away == Vector2.Zero) away = -SafeNormalize(p.Velocity);
            m.Velocity = away * BlightTuning.ShakeSpeed;
            m.Shaken = BlightTuning.ShakeSeconds;
            float dealt = Ward(m, m.Position, BlightTuning.ShakeLight);
            bool emptied = dealt > 0f && m.Corruption.Hit(dealt);
            Events.Add(new PlaneEvent(emptied ? PlaneEventType.MobDefeated : PlaneEventType.MobHit, m.Position, Vector2.Zero, dealt));
            if (emptied) BurstMurkling(m, ember: true);
        }
    }

    // ───────────────────────── Blightroots ─────────────────────────

    void StepBlightroots(CorruptionField field)
    {
        bool regrow = field.Cleansed < CorruptionTuning.StopRegrowth && Tick % 4 == 0;
        foreach (var root in Blightroots)
        {
            var area = Areas[root.Area];
            if (!root.Alive)
            {
                root.Dead += Dt;
                // The burst: a wave of cleansing through its whole reach.
                if (root.BurstRadius >= 0f && root.BurstRadius < BlightTuning.Reach)
                {
                    root.BurstRadius = MathF.Min(BlightTuning.Reach, root.BurstRadius + BlightTuning.Reach / BlightTuning.BurstSeconds * Dt);
                    field.Cleanse(root.Position, root.BurstRadius, 0.5f);
                }
                continue;
            }
            if (regrow)
                for (int k = 0; k < area.Cells.Count; k++)
                {
                    int i = area.Cells[k];
                    // Strongest at the stump, still creeping at the rim.
                    float near = 1f - 0.6f * root.CellDistance[k] / BlightTuning.Reach;
                    field.Raise(i, field.Initial[i], BlightTuning.Regrow * near * Dt * 4f);
                }

            // Her light on its sac: each shot takes its damage off the corruption; emptied, it bursts. It re-corrupts quickly.
            root.Corruption.Step(Dt);
            foreach (var shot in Shots)
            {
                if (!shot.FromPlayer || shot.Life <= 0f) continue;
                if (!Reaches(shot, root.Position, BlightTuning.SacRadius)) continue;
                float light = HitDamage(shot);
                float dealt = Ward(root, root.Position, light);
                bool emptied = dealt > 0f && root.Corruption.Hit(dealt);
                Events.Add(new PlaneEvent(PlaneEventType.MobHit, root.Position, SafeNormalize(shot.Velocity), dealt));
                Pop(shot, root.Position - shot.Position);
                if (emptied) break;
            }
            if (root.Corruption.Empty)
            {
                // Cleansed: it shrivels and bursts into motes, a wave of cleansing runs through its reach.
                root.Alive = false;
                root.BurstRadius = 0f;
                root.PulseRadius = -1f;
                Stats.MobsDefeated++;
                Events.Add(new PlaneEvent(PlaneEventType.BlightrootBurst, root.Position, Vector2.Zero, BlightTuning.Reach));
                continue;
            }

            // The pulse: the sac inflates (the telegraph), then a ring of ink runs out over its reach.
            root.PulseTimer -= Dt;
            root.Inflate = root.PulseTimer < BlightTuning.Telegraph ? 1f - MathF.Max(root.PulseTimer, 0f) / BlightTuning.Telegraph : MathF.Max(0f, root.Inflate - Dt * 4f);
            if (root.PulseTimer <= BlightTuning.Telegraph && root.PulseTimer + Dt > BlightTuning.Telegraph)
                Events.Add(new PlaneEvent(PlaneEventType.BlightrootInflate, root.Position, Vector2.Zero));
            if (root.PulseTimer <= 0f)
            {
                root.PulseTimer = BlightTuning.PulseEvery;
                root.PulseRadius = 0.6f;
                root.PulseId = ++_pulses;
                Events.Add(new PlaneEvent(PlaneEventType.BlightrootPulse, root.Position, Vector2.Zero, BlightTuning.Reach));
            }
            if (root.PulseRadius >= 0f)
            {
                float r0 = root.PulseRadius;
                float r1 = root.PulseRadius = r0 + BlightTuning.PulseSpeed * Dt;
                // It stains what it crosses...
                for (int k = 0; k < area.Cells.Count; k++)
                {
                    float d = root.CellDistance[k];
                    if (d >= r0 && d < r1) field.Raise(area.Cells[k], MathF.Min(BlightTuning.PulseStain, field.Initial[area.Cells[k]]), 1f);
                }
                // ...snuffs out her shots it crosses (by index: a popping shot may throw more)...
                for (int si = 0; si < Shots.Count; si++)
                {
                    var shot = Shots[si];
                    if (!shot.FromPlayer || shot.Life <= 0f || shot.Pulse == root.PulseId) continue;
                    float d = Vector2.Distance(shot.Position, root.Position);
                    if (MathF.Abs(d - r1) > BlightTuning.PulseBand * 0.5f + shot.Radius) continue;
                    shot.Pulse = root.PulseId;
                    Pop(shot, shot.Position - root.Position);
                }
                // ...and corrupts her as it passes over her (once per ring; a dash slips through it).
                if (_pulseHit != root.PulseId)
                {
                    float her = Vector2.Distance(Player.Position, root.Position);
                    if (MathF.Abs(her - (r0 + r1) * 0.5f) <= BlightTuning.PulseBand * 0.5f + Radius + (r1 - r0) * 0.5f)
                    {
                        _pulseHit = root.PulseId;
                        HurtPlayer(BlightTuning.PulseDamage + BlightTuning.PulseDamagePerMenace * Menace, SafeNormalize(Player.Position - root.Position), DamageSource.Blightroot);
                    }
                }
                if (r1 >= BlightTuning.Reach) root.PulseRadius = -1f;
            }
            StepHammer(root);
        }
    }

    /// <summary>
    /// A Blightroot's hammer (BlightTuning.HammerReach and the rest): she comes too close, it rears back (its aim following
    /// her), swings its head down at her and, if she is still in reach and in front of it as it lands, it corrupts her and
    /// knocks her away; it recovers, then waits before it may swing again. Never while it gathers a pulse.
    /// </summary>
    void StepHammer(Blightroot root)
    {
        var p = Player;
        if (root.SwingCooldown > 0f) root.SwingCooldown -= Dt;
        var to = p.Position - root.Position;
        float her = to.Length();
        if (root.Swing < 0f)
        {
            if (root.SwingCooldown > 0f || root.Inflate > 0f || Defeated || her > BlightTuning.HammerReach) return;
            root.Swing = 0f;
            root.SwingDir = SafeNormalize(to);
            Events.Add(new PlaneEvent(PlaneEventType.BlightrootSwing, root.Position, root.SwingDir));
            return;
        }
        float was = root.Swing;
        root.Swing += Dt;
        // Rearing back, it keeps her in its aim.
        if (root.Swing < BlightTuning.HammerWindup && her > 1e-3f) root.SwingDir = to / her;
        float lands = BlightTuning.HammerWindup + BlightTuning.HammerStrike;
        if (was < lands && root.Swing >= lands)
        {
            bool inFront = her < 1e-3f || Vector2.Dot(to / her, root.SwingDir) > 0.3f;
            bool on = her <= BlightTuning.HammerHit + Radius && inFront;
            // Where it comes down: on her, else as far as it reaches her way.
            var at = on ? p.Position : root.Position + root.SwingDir * MathF.Min(MathF.Max(her, 1f), BlightTuning.HammerHit);
            Events.Add(new PlaneEvent(PlaneEventType.BlightrootSlam, at, root.SwingDir, on ? 1f : 0f));
            if (on
                && HurtPlayer(BlightTuning.HammerDamage + BlightTuning.HammerDamagePerMenace * Menace, root.SwingDir, DamageSource.Blightroot))
                p.Velocity += root.SwingDir * BlightTuning.HammerKnock;
        }
        if (root.Swing >= lands + BlightTuning.HammerRecover)
        {
            root.Swing = -1f;
            root.SwingCooldown = BlightTuning.HammerCooldown;
        }
    }

    int _pulses, _pulseHit;

    /// <summary>A bubble loses some of its light (and with it, damage); one too dim pops.</summary>
    void DimBubble(PlaneShot shot, float keep)
    {
        shot.Dim *= keep;
        shot.Damage *= keep;
        if (shot.Dim < 0.12f) Pop(shot);
    }

    // ───────────────────────── murklings ─────────────────────────

    void StepMurklings(CorruptionField field)
    {
        var p = Player;
        // Budding: from the dense ground of each living area. Never in the arena: during the fight only she is in it.
        foreach (var area in Areas)
        {
            if (area.Arena) continue;
            var root = Blightroots.FirstOrDefault(b => b.Alive && Areas[b.Area] == area);
            if (root is null) continue;
            // Under attack, its guardians wake: the area buds fast round it (even an area cleansed out of budding).
            bool guarded = root.Guarded;
            if (guarded && !root.WasGuarded)
            {
                area.BudTimer = MathF.Min(area.BudTimer, GuardInterval());
                Events.Add(new PlaneEvent(PlaneEventType.GuardiansWoke, root.Position, Vector2.Zero));
            }
            root.WasGuarded = guarded;
            if (area.Spent && !guarded) continue;
            area.BudTimer -= Dt;
            if (area.BudTimer > 0f) continue;
            area.BudTimer = guarded ? GuardInterval() : BudInterval();
            int index = Areas.IndexOf(area);
            int cap = BudCap + (guarded ? BlightTuning.GuardCap + (int)MathF.Round(BlightTuning.GuardCapPerMenace * Menace) : 0);
            if (Murklings.Count(m => m.Alive && m.Area == index) >= cap) continue;
            if (guarded)
            {
                // A guardian buds in a ring round it, drinking its ink as any bud does.
                for (int tries = 0; tries < 12; tries++)
                {
                    float a = _blightRng.Range(0f, MathF.Tau), r = _blightRng.Range(BlightTuning.GuardNear, BlightTuning.GuardFar);
                    var at = (root.GuardAt ?? root.Position) + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
                    if (Vector2.Distance(at, p.Position) < BlightTuning.BudClearance || !Clear(at, BlightTuning.MurkRadius)) continue;
                    if (Bud(at, index) is not null) break;
                }
                continue;
            }
            for (int tries = 0; tries < 12 && area.Cells.Count > 0; tries++)
            {
                int cell = area.Cells[_blightRng.Int(area.Cells.Count)];
                var at = CorruptionField.CellCentre(cell % CorruptionField.N, cell / CorruptionField.N);
                if (field.Density[cell] < BlightTuning.BudDensity || Vector2.Distance(at, p.Position) < BlightTuning.BudClearance || !Clear(at, BlightTuning.MurkRadius)) continue;
                // Now and then a prime (else a murkling), where the level still holds ink enough for it.
                bool prime = _primesBorn < BlightTuning.PrimePerLevel && !Murklings.Any(x => x.Alive && x.Prime && x.Area == index)
                    && _blightRng.NextFloat() < BlightTuning.PrimeChance + BlightTuning.PrimeChancePerMenace * Menace
                    && Vector2.Distance(at, p.Position) > BlightTuning.PrimeClearance && Clear(at, BlightTuning.PrimeBody);
                if ((prime ? Bud(at, index, prime: true) : null) is null && Bud(at, index) is null) continue;
                break;
            }
        }

        foreach (var m in Murklings)
        {
            if (!m.Alive) continue;
            if (m.Budding)
            {
                // It drinks the ink round it up into itself: the ground it takes is left empty (no gold: she did not cleanse it).
                m.Bud -= Dt;
                if (m.Draw is { } draw)
                {
                    m.Matter += field.Drink(draw, m.Budding ? Dt / m.BudTotal : 1f);
                    if (!m.Budding) m.Draw = null;
                }
                if (!m.Budding) Events.Add(new PlaneEvent(PlaneEventType.MurklingEmerged, m.Position, Vector2.Zero));
                continue;
            }
            if (m.Clinging)
            {
                m.Position = p.Position + m.ClingOffset;
                Drain(m.ClingDrain * Dt, DamageSource.Murkling);
                continue;
            }
            if (m.Shaken > 0f)
            {
                m.Shaken -= Dt;
                m.Windup = 0f;
                m.Velocity = MoveToward(m.Velocity, Vector2.Zero, BlightTuning.ShakeSpeed * 1.2f * Dt);
                Move(ref m.Position, ref m.Velocity, m.Body, report: false);
                continue;
            }
            if (m.Lesser)
            {
                StepLesser(m);
                continue;
            }
            // Toward the brightest light: her, or a bubble of hers hanging at the end of its range (bait).
            Vector2 target = p.Position;
            float best = 1f / (1f + 0.15f * Vector2.Distance(m.Position, p.Position));
            bool her = true;
            foreach (var shot in Shots)
            {
                if (!shot.FromPlayer || shot.Life <= 0f || shot.Rest <= 0f) continue;
                float d = Vector2.Distance(m.Position, shot.Position);
                float bright = MathF.Min(0.4f + 0.06f * shot.Bubbles, 1.3f) * shot.Dim;
                float score = bright / (1f + 0.15f * d);
                if (d < m.Sense && score > best)
                {
                    best = score;
                    target = shot.Position;
                    her = false;
                }
            }
            m.Target = target;
            var to = target - m.Position;
            float dist = to.Length();
            Vector2 wish = dist < m.Sense ? SafeNormalize(to) : Vector2.Zero;
            // She is away: a den's murkling drifts home into its nave (DenTuning.Leash); else it hunts the reef's fish,
            // keeping a throw's distance from the one it is after.
            bool away = her && dist >= m.Sense;
            if (away && m.Home is { } home && Vector2.Distance(m.Position, home) > m.HomeRadius) wish = SafeNormalize(home - m.Position) * 0.6f;
            var prey = away && (m.Home is not { } h2 || Vector2.Distance(m.Position, h2) <= m.HomeRadius) ? Prey(m) : null;
            float preyDist = 0f;
            if (prey is not null)
            {
                var toPrey = prey.Position - m.Position;
                preyDist = toPrey.Length();
                wish = SafeNormalize(toPrey) * (preyDist > MurkFishTuning.Keep + 1f ? 1f : preyDist < MurkFishTuning.Keep - 1f ? -0.6f : 0f);
            }
            // A wobble off the line, as if the dark were reluctant.
            wish += new Vector2(MathF.Sin(Time * 1.7f + m.Seed), MathF.Cos(Time * 1.3f + m.Seed * 2f)) * 0.35f;
            float pace = (BlightTuning.MurkSpeed + BlightTuning.MurkSpeedPerMenace * Menace) * (m.Windup > 0f ? BlightTuning.WindupSlow : 1f) * (m.Prime ? BlightTuning.PrimeSpeed : 1f);
            m.Velocity = MoveToward(m.Velocity, SafeNormalize(wish) * pace, 4f * Dt);
            Move(ref m.Position, ref m.Velocity, m.Body, report: false);

            // A prime is too big to cling: it rams her, shoving her off.
            if (m.Prime && Vector2.Distance(m.Position, p.Position) < m.Radius * 0.8f + Radius)
            {
                var shove = SafeNormalize(p.Position - m.Position);
                if (HurtPlayer(BlightTuning.ContactDamage * BlightTuning.PrimeHit, shove, DamageSource.Murkling)) p.Velocity += shove * BlightTuning.PrimeShove;
            }
            // It clings on touch.
            else if (Vector2.Distance(m.Position, p.Position) < BlightTuning.MurkRadius + Radius)
            {
                if (HurtPlayer(BlightTuning.ContactDamage, SafeNormalize(p.Position - m.Position), DamageSource.Murkling) && Clingers < BlightTuning.MaxClingers)
                {
                    m.Clinging = true;
                    m.Windup = 0f;
                    m.ClingOffset = SafeNormalize(m.Position - p.Position) * (Radius * 0.8f);
                    Events.Add(new PlaneEvent(PlaneEventType.MurklingCling, m.Position, Vector2.Zero));
                }
                continue;
            }
            // It throws at her when she is near and in sight (or at the fish it hunts), winding up first.
            m.ThrowAge += Dt;
            if (m.Windup > 0f) StepWindup(m);
            else
            {
                m.ThrowTimer -= Dt;
                if (her && dist < m.ThrowRange && m.ThrowTimer <= 0f && LineOfSight(m.Position, p.Position)) BeginThrow(m, null);
                else if (prey is not null && preyDist < m.ThrowRange && m.ThrowTimer <= 0f && LineOfSight(m.Position, prey.Position)) BeginThrow(m, prey);
            }
        }

        // Its throws: they drain light on hit; rock stops them. Flown their range, they fall and stain the sand.
        foreach (var s in MurkShots)
        {
            var was = s.Position;
            s.Life -= Dt;
            if (!s.Airborne)
            {
                s.Fall += Dt;
                s.Velocity *= MathF.Exp(-3f * Dt);
                s.Position += s.Velocity * Dt;
                if (s.Fall >= BlightTuning.ThrowFallSeconds)
                {
                    s.Life = 0f;
                    if (Dots is { } dots) dots.Add(InkDots.CellOf(s.Position));
                    else Corruption?.Spread(s.Position, s.Stain, BlightTuning.ThrowStainInk, 1f);
                    Events.Add(new PlaneEvent(PlaneEventType.MurkShotLanded, s.Position, Vector2.Zero, s.Stain));
                }
                continue;
            }
            s.Position += s.Velocity * Dt;
            s.Traveled += s.Velocity.Length() * Dt;
            if (s.Traveled >= BlightTuning.ThrowFallAt) s.Fall = Dt;
            if (!Map.IsOpen(s.Position)) s.Life = 0f;
            else if (ReachesHer(s.Position, s.Radius, out var struck))
            {
                s.Life = 0f;
                HurtPlayer(s.Damage, SafeNormalize(s.Velocity), DamageSource.MurkShot, struck);
            }
            else if (StrikeFish(s)) s.Life = 0f;
        }

        // Her shots of light: each takes its damage off a murkling's (or a bud's) corruption and bursts on it (a piercing
        // one flies on); emptied, the murkling bursts. A throw it meets costs it half its light.
        foreach (var m in Murklings)
            if (m.Alive) m.Corruption.Step(Dt);
        foreach (var shot in Shots)
        {
            if (!shot.FromPlayer || shot.Life <= 0f) continue;
            foreach (var m in Murklings)
            {
                if (!m.Alive || m.Clinging || shot.Struck == m || !Reaches(shot, m.Position, m.Radius)) continue;
                float light = HitDamage(shot);
                float dealt = Ward(m, m.Position, light);
                bool emptied = dealt > 0f && m.Corruption.Hit(dealt);
                Events.Add(new PlaneEvent(emptied ? PlaneEventType.MobDefeated : PlaneEventType.MobHit, m.Position, SafeNormalize(shot.Velocity), dealt));
                Shove(ref m.Knock, shot.Velocity, m.Radius);
                if (emptied) BurstMurkling(m, ember: true);
                if (shot.Pierce) shot.Struck = m;
                else
                {
                    Pop(shot, m.Position - shot.Position);
                    break;
                }
            }
            if (shot.Life <= 0f) continue;
            foreach (var s in MurkShots)
            {
                if (s.Life <= 0f || !s.Airborne || !Reaches(shot, s.Position, s.Radius)) continue;
                SplatterThrow(s);
                DimBubble(shot, BlightTuning.BubbleDim);
                if (shot.Life <= 0f) break;
            }
        }
        MurkShots.RemoveAll(s => s.Life <= 0f);
        Murklings.RemoveAll(m => !m.Alive);
    }

    /// <summary>Her light strikes a throw in the air: it bursts in a splatter, like a water balloon, staining nothing.</summary>
    void SplatterThrow(MurkShot s)
    {
        s.Life = 0f;
        Events.Add(new PlaneEvent(PlaneEventType.MurkShotBurst, s.Position, SafeNormalize(s.Velocity)));
    }

    /// <summary>A throw begins: it bends back away from its target (at her, or at a fish), the telegraph.</summary>
    void BeginThrow(Murkling m, PlaneReefFish? prey)
    {
        m.ThrowTimer = BlightTuning.ThrowEvery / (1f + BlightTuning.ThrowRatePerMenace * Menace) * _blightRng.Range(0.8f, 1.2f);
        m.WindupTotal = m.Windup = (m.Prime ? BlightTuning.PrimeWindup : BlightTuning.WindupSeconds) / (1f + BlightTuning.WindupPerMenace * Menace);
        m.WindupPrey = prey;
        m.Aim = AimOf(m);
        Events.Add(new PlaneEvent(PlaneEventType.MurklingWindup, m.Position, m.Aim, m.WindupTotal));
    }

    /// <summary>Its aim follows the target while it bends back; the snap forward is locked; at its end the piece flies.</summary>
    void StepWindup(Murkling m)
    {
        if (m.WindupPrey is { Huntable: false })
        {
            // The fish it meant is gone (turning, freed, hidden): it lets the bend go.
            m.Windup = 0f;
            m.WindupPrey = null;
            return;
        }
        m.Windup -= Dt;
        if (m.Windup > BlightTuning.SnapSeconds) m.Aim = AimOf(m);
        if (m.Windup > 0f) return;
        m.Windup = 0f;
        m.WindupPrey = null;
        m.ThrowAge = 0f;
        float speed = BlightTuning.ThrowSpeed + BlightTuning.ThrowSpeedPerMenace * Menace;
        var at = m.Position + m.Aim * m.Radius;
        var shot = new MurkShot { Position = at, From = at, Velocity = m.Aim * speed };
        if (m.Prime)
        {
            shot.Radius *= BlightTuning.PrimeHit;
            shot.Damage *= BlightTuning.PrimeHit;
            shot.Splash = BlightTuning.PrimeSplash;
            shot.Stain *= BlightTuning.PrimeHit;
        }
        MurkShots.Add(shot);
        Events.Add(new PlaneEvent(PlaneEventType.MurklingThrow, m.Position, m.Aim));
    }

    /// <summary>Which way a winding-up murkling aims: at her, or at its fish, led a little to where it is swimming.</summary>
    Vector2 AimOf(Murkling m)
    {
        if (m.WindupPrey is not { } prey) return SafeNormalize(Player.Position - m.Position);
        float speed = BlightTuning.ThrowSpeed + BlightTuning.ThrowSpeedPerMenace * Menace;
        return SafeNormalize(prey.Position + prey.Velocity * (Vector2.Distance(prey.Position, m.Position) / speed) - m.Position);
    }

    /// <summary>
    /// A murkling (or a prime) buds here, made of the nearest ink (<paramref name="drink"/>): null when the level no
    /// longer holds that much. Without drink it is made of nothing from the ground (an ambush's, a prime's pieces).
    /// Born, it is out at once, out of a body rather than the ground (Drippus Prime's brood): no bud.
    /// </summary>
    Murkling? Bud(Vector2 at, int area, bool prime = false, bool drink = true, bool born = false)
    {
        InkDraw? draught = null;
        if (drink && Corruption is { } ground)
        {
            draught = ground.Plan(at, prime ? BlightTuning.PrimeInk : BlightTuning.Ink);
            if (draught is null) return null;
        }
        var m = new Murkling { Position = at, Area = area, Seed = _blightRng.Range(0f, 100f), ThrowTimer = BlightTuning.ThrowEvery * _blightRng.Range(0.5f, 1f) };
        m.Corruption.Regen *= 1f + 0.5f * Menace;
        m.Corruption.Max = m.Corruption.Value = BlightTuning.MurkCorruption * (1f + BlightTuning.MeterPerMenace * Menace) * (prime ? BlightTuning.PrimeMeter : 1f);
        if (prime)
        {
            m.Prime = true;
            m.Radius = BlightTuning.MurkRadius * BlightTuning.PrimeScale;
            m.Body = BlightTuning.PrimeBody;
            m.Bud = m.BudTotal = BlightTuning.PrimeBud;
            _primesBorn++;
        }
        m.Draw = draught;
        (_budLater ? _madeLater : Murklings).Add(m);
        if (born)
        {
            m.Bud = 0f;
            return m;
        }
        // The bud: its time, how far out it drinks and how thick that ink is (the view pulls dots in from the cells it drinks).
        float radius = draught?.Radius ?? BlightTuning.GatherRadius;
        float ink = draught?.Ink ?? (Corruption is { } field ? DenseShare(field, at, radius, 0.15f) : 0f);
        Events.Add(new PlaneEvent(PlaneEventType.MurklingBud, at, new Vector2(radius, ink), m.BudTotal));
        return m;
    }

    /// <summary>Verification: a murkling (or a prime) budding here now (of nothing, where there is not ink enough).</summary>
    public Murkling DebugBud(Vector2 at, bool prime) => Bud(at, -1, prime) ?? Bud(at, -1, prime, drink: false)!;

    /// <summary>The share of a disc's open floor whose ink is at least so dense (sampled on a grid).</summary>
    float DenseShare(CorruptionField field, Vector2 at, float radius, float dense = BlightTuning.BudDensity)
    {
        int open = 0, inked = 0;
        for (float y = -radius; y <= radius; y += 0.5f)
        for (float x = -radius; x <= radius; x += 0.5f)
        {
            if (x * x + y * y > radius * radius) continue;
            var q = at + new Vector2(x, y);
            if (!Map.IsOpen(q)) continue;
            open++;
            if (field.At(q) >= dense) inked++;
        }
        return open == 0 ? 0f : inked / (float)open;
    }

    /// <summary>
    /// A murkling bursts. Over cleansed ground its stolen ember falls and blooms a patch of colour (not when shaken off
    /// into the ink cloud by a dash).
    /// </summary>
    void BurstMurkling(Murkling m, bool ember, float damage = 0f)
    {
        m.Alive = false;
        Stats.MobsDefeated++;
        // The ink it was made of is cleansed with it (a prime's pieces keep theirs).
        float matter = m.Matter;
        m.Draw = null;
        if (m.Lesser)
        {
            // A lesser one is a fish again.
            Events.Add(new PlaneEvent(PlaneEventType.MurklingBurst, m.Position, Vector2.Zero, 0f));
            FreeFish(m);
            if (damage > 0f) Events.Add(new PlaneEvent(PlaneEventType.MobDefeated, m.Position, Vector2.Zero, damage));
            return;
        }
        if (m.Prime)
        {
            // A prime: its great ember blooms wide wherever it bursts, and it always leaves shells (and maybe a light orb).
            Corruption?.Cleanse(m.Position, BlightTuning.PrimeBloom, 1f);
            Events.Add(new PlaneEvent(PlaneEventType.MurklingBurst, m.Position, Vector2.UnitX, BlightTuning.PrimeBloom));
            int shells = BlightTuning.PrimeShellsMin + _drops.Int(BlightTuning.PrimeShellsMax - BlightTuning.PrimeShellsMin + 1);
            for (int i = 0; i < shells; i++) Shells.Add(new PlaneShell { Position = m.Position, Velocity = Fling(2f, 5f) });
            if (_drops.NextFloat() < BlightTuning.PrimeOrb) Hearts.Add(new PlaneHeart { Position = m.Position, Velocity = Fling(1.5f, 3f) });
            // It breaks apart: murklings flung out of it, already out of the ground, each made of a murkling's ink of its own.
            float share = MathF.Min(BlightTuning.Ink / CorruptionTuning.CellArea, matter / BlightTuning.PrimeSplit);
            for (int i = 0; i < BlightTuning.PrimeSplit; i++)
            {
                float a = i * MathF.Tau / BlightTuning.PrimeSplit + _blightRng.Range(-0.3f, 0.3f);
                var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
                var at = m.Position + dir * 1.2f;
                for (int k = 0; k < 4 && !Clear(at, BlightTuning.MurkRadius); k++) at = Vector2.Lerp(at, m.Position, 0.35f);
                // (Joining the others at the step's end: a prime bursts while loops walk them.)
                _budLater = true;
                var piece = Bud(at, m.Area, drink: false)!;
                _budLater = false;
                piece.Bud = 0f;
                piece.Matter = share;
                matter -= share;
                piece.Velocity = dir * 6f;
                piece.Shaken = 0.5f;
                piece.ThrowTimer = BlightTuning.ThrowEvery * _blightRng.Range(0.6f, 1.2f);
            }
            Corruption?.Release(matter);
            if (damage > 0f) Events.Add(new PlaneEvent(PlaneEventType.MobDefeated, m.Position, Vector2.Zero, damage));
            return;
        }
        Corruption?.Release(matter);
        bool blooms = ember && Corruption is { } field && field.At(m.Position) < CorruptionTuning.Clean;
        if (blooms) Corruption!.Cleanse(m.Position, CorruptionTuning.EmberRadius, 1f);
        Events.Add(new PlaneEvent(PlaneEventType.MurklingBurst, m.Position, Vector2.Zero, blooms ? CorruptionTuning.EmberRadius : 0f));
        if (damage > 0f) Events.Add(new PlaneEvent(PlaneEventType.MobDefeated, m.Position, Vector2.Zero, damage));
    }

    /// <summary>An ambush clearing springs: murklings bud in a ring round her (the pufferlings are retracted).</summary>
    void AmbushBuds(PlaneAmbush ambush)
    {
        var p = Player;
        float turn = _blightRng.Range(0f, MathF.Tau);
        int spawned = 0;
        for (int i = 0; i < BlightTuning.AmbushBuds; i++)
        {
            float a = turn + MathF.Tau * i / BlightTuning.AmbushBuds;
            var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
            foreach (float r in new[] { 5f, 3.8f, 2.8f })
            {
                var at = p.Position + dir * r;
                if (!Clear(at, BlightTuning.MurkRadius)) continue;
                // The trap's own ink: an ambush's buds drink nothing from the ground.
                Bud(at, -1, drink: false);
                spawned++;
                break;
            }
        }
        ambush.Spawned = spawned;
    }

    // ───────────────────────── the arena ─────────────────────────

    /// <summary>The condensation: the boss's crust follows the arena cleansed; the Crack opens once she is freed and it is clean enough.</summary>
    void StepCondensation()
    {
        if (Boss is not { } boss || ArenaArea is not { } arena) return;
        int crust = Math.Clamp(CorruptionTuning.CrustLayers - (int)(arena.Cleansed / (1f / CorruptionTuning.CrustLayers) + 1e-4f), 0, CorruptionTuning.CrustLayers);
        if (crust < boss.Crust)
            Events.Add(new PlaneEvent(PlaneEventType.CrustBroken, boss.Position, Vector2.Zero, crust));
        boss.Crust = crust;
        if (boss.Freed && !ExitOpen && arena.Cleansed >= CorruptionTuning.ArenaToOpen)
        {
            ExitOpen = true;
            Events.Add(new PlaneEvent(PlaneEventType.CrackOpened, ArenaCenter, Vector2.Zero));
        }
    }
}
