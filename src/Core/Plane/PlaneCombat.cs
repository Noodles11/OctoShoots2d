using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;

namespace OctoShoots.Core.Plane;

/// <summary>
/// Combat numbers for Clementine's shots and the plane's mobs (the corrupted pufferlings; their own numbers are in
/// PufferlingTuning). Provisional, tuned in play.
/// </summary>
public static class PlaneCombatTuning
{
    public const float PlayerMaxHp = 100f;
    /// <summary>Untouchable for a moment after a hit.</summary>
    public const float PlayerHurtGrace = 0.6f;

    /// <summary>
    /// Her shots are light (docs/LIGHT-BUBBLES.md; the rules still call them bubbles): thrown hard at ShotSpeed, light
    /// has no friction, so it flies on undimmed at full speed to where she aims (the pointer, or as far as the right
    /// stick is pushed; never nearer than MinReach) or the end of her range, whichever is nearer, and goes out there like
    /// a match (no hovering, no burst), clearing a little of the ground's corruption where it does. BubbleRange is her
    /// range with no pearls: half the screen's height at the 100% camera zoom (the framing shows 25.6 m of the swim
    /// band top to bottom). ShotLife is only a safety net. With Bubble Coral her light lingers instead (see
    /// LingerDrag): it slows to a hover near the end of its range, so later shots catch up and merge with it.
    /// </summary>
    public const float ShotSpeed = 30f;
    /// <summary>The surge back to her after a hit cleanses a line of ground this wide, by this much density.</summary>
    public const float StrikeWidth = 0.6f, StrikeCleanse = 0.25f;
    public const float BubbleRange = 12.8f, MinReach = 1.5f, LightSettle = 8f;
    /// <summary>
    /// Below this speed a bubble hovers: still drifting and slowing, each bubble for its own time between BubbleHoverMin
    /// and BubbleHoverMax seconds (times her bubble hover stat), then it pops.
    /// </summary>
    public const float BubbleStop = 0.9f;
    public const float BubbleHoverMin = 0.8f, BubbleHoverMax = 1.2f;
    /// <summary>
    /// Bubble Coral: lingering light. Thrown at ShotSpeed, it slows steadily (the deceleration that would stop it at
    /// LingerRange), then — once the water's drag (LingerDrag × its speed, per second) is the gentler — eases off.
    /// Slower than BubbleStop it hovers (still drifting, still slowing) for its own hover time (BubbleHoverMin–Max × her
    /// bubble hover stat: Bubble Coral ×1.5), then goes out. It settles about 6 m from her, well inside the view, so
    /// the shots behind visibly catch up and merge with it.
    /// </summary>
    public const float LingerDrag = 6f, LingerRange = 5.5f;
    /// <summary>
    /// A bubble that bumps into rock or another bubble pops this often; otherwise it bounces off (Mirror Scale's bounces
    /// are sure; with Bubble Coral, bubbles that meet merge instead). A bubble that hits a mob always pops.
    /// </summary>
    public const float BubblePopChance = 0.4f;
    /// <summary>
    /// Two of her bubbles that touch (never two of the same volley) bounce off each other: pushed apart, each turned off
    /// the other with BubbleBounce of the meeting speed kept. With Bubble Coral they merge into one instead: it keeps the
    /// faster one's speed, the longer life, and the summed damage; its radius grows by BubbleGrowth of the base for every
    /// bubble merged in, so three bubbles make one twice the size. Size and damage stop growing at BubbleCap bubbles (it
    /// still merges, taking speed, heading and life); a full bubble shines like a rainbow.
    /// </summary>
    public const float BubbleBounce = 0.8f;
    public const float BubbleGrowth = 0.5f;
    public const int BubbleCap = 15;
    public const float ShotLife = 3f;
    /// <summary>How long a mob flashes when a shot hits it.</summary>
    public const float HitFlash = 0.12f;
    public const float ShotInterval = 0.22f;
    public const float ShotDamage = 10f;
    public const float ShotRadius = 0.15f;

    public const float MobHp = PufferlingTuning.Hp;
    /// <summary>A calm pufferling's body (it grows when it blows up: <see cref="PlaneMob.Radius"/>).</summary>
    public const float MobRadius = PufferlingTuning.CalmRadius;

    /// <summary>Spawn points: at most this many, this far apart, and this far from the start.</summary>
    public const int MaxMobs = 16;
    public const float MobSpacing = 10f;
    public const float MobStartClearance = 30f;

    /// <summary>No diving while a creature that has noticed her is this close (an active battle).</summary>
    public const float DiveSafeDistance = 12f;

    /// <summary>An ambush springs as she enters its clearing: this many pufferlings, in a ring this far around her.</summary>
    public const int AmbushMin = 3, AmbushMax = 4;
    public const float AmbushRing = 5.5f;
    /// <summary>They may first blow up after this long, one after another, so she has a moment to react.</summary>
    public const float AmbushFirstShot = 0.9f, AmbushShotStagger = 0.6f;

    /// <summary>A pearl is picked up within this distance of her edge.</summary>
    public const float PearlReach = 0.8f;
    /// <summary>
    /// Homing shots lock onto the one creature nearest the shot (within reach, ahead of it) and turn this fast toward
    /// it, keeping it until it is freed, struck by a piercing shot, out of reach or passed; then they pick the next.
    /// </summary>
    public const float HomingDegPerSec = 240f;
    public const float HomingReach = 12f;
    /// <summary>Wave shots weave this far either side of their line, this many times a second.</summary>
    public const float WaveAmplitude = 0.35f;
    public const float WaveTurns = 3f;

    /// <summary>
    /// Pearl Diver: holding fire charges the next throw, full after ChargeSeconds; let go and it flies as a pearl with up
    /// to ChargeDamage× the damage and ChargeSize× the size of a bubble (a tap throws a plain one).
    /// </summary>
    public const float ChargeSeconds = 1f, ChargeDamage = 3f, ChargeSize = 2.2f;
    /// <summary>String of Pearls: the stream's shots are charged this far at full spin.</summary>
    public const float StreamCharge = 0.98f;
    /// <summary>Starfish Arm: a bubble grows as it flies, reaching GrowMax× its size and damage at the end of its range.</summary>
    public const float GrowMax = 2f;
    /// <summary>
    /// Starburst Gland (item id ink_sac): wherever a shot pops it bursts into light, cleansing everything within
    /// BurstRadius of its edge for BurstDamage of its own damage (on top of the hit itself), and the ground under it.
    /// </summary>
    public const float BurstRadius = 1.8f, BurstDamage = 0.6f;
    /// <summary>A hit shoves a mob along the shot at Tuning.ShotKnockback × her knockback stat (m/s), easing off at this rate.</summary>
    public const float KnockDecay = 6f;
    /// <summary>
    /// Mitosis: a bubble that pops on a foe, rock or a pot splits into two, SplitAngle either side of its way on, each
    /// with half its damage, SplitSize of its size, thrown at SplitSpeed (or its own speed, if faster) to fly SplitRange.
    /// The halves never split again.
    /// </summary>
    public const float SplitAngleDeg = 40f, SplitSize = 0.75f, SplitSpeed = 9f, SplitRange = 4f;
    /// <summary>Pearl Necklace (Pearl Diver + Mitosis): a full-charge pearl bursts into a ring of this many.</summary>
    public const int NecklaceCount = 8;
    /// <summary>
    /// Sardine Run: held, her fire rate spins up to MinigunMax times its own over SpinUp seconds, and spins down over
    /// SpinDown once let go; each shot strays up to MinigunJitterDeg off her aim.
    /// </summary>
    public const float MinigunMax = 4f, SpinUp = 1.2f, SpinDown = 0.5f, MinigunJitterDeg = 6f;
    /// <summary>
    /// Pistol Shrimp Claw: a bubble collapses with a bang where it pops or goes out, striking everything within
    /// CavitationRadius (plus its own size) for CavitationDamage of its damage. Depth Charge (with Pearl Diver): a
    /// full-charge pearl's blast reaches DepthChargeReach times as far and hits DepthChargeDamage times as hard.
    /// </summary>
    public const float CavitationRadius = 2.2f, CavitationDamage = 0.7f, DepthChargeReach = 2f, DepthChargeDamage = 1.5f;
}

/// <summary>
/// A corrupted pufferling (docs/PUFFERLING-PROPOSAL.md): the plane's mob. Freed when its health runs out, it carries on
/// as a healthy pufferling (a <see cref="PlaneFish"/>); the mob itself never comes back.
/// </summary>
public sealed class PlaneMob : PlaneSwimmer
{
    public int Spawn;
    public float Hp = PlaneCombatTuning.MobHp;
    /// <summary>It may not blow up until this runs out (an ambush staggers its pufferlings with it).</summary>
    public float FireTimer;
    public bool Aggro;
    /// <summary>Counts down from HitFlash when it is hit (the view flashes it meanwhile).</summary>
    public float HitFlash;
    public PufferState State;
    public float StateTime;
    /// <summary>How much of its spines are on its skin: 0 just after it has fired them, 1 grown back.</summary>
    public float Spines = 1f;
    /// <summary>How long it has not seen her while facing her.</summary>
    public float LostSight;
    /// <summary>Knocked back by her hits: this velocity, easing off (KnockDecay).</summary>
    public Vector2 Knock;
    public bool Alive => Hp > 0f;
    /// <summary>Its body: calm, or the ball it blows up into.</summary>
    public float Radius => PufferlingTuning.CalmRadius + (PufferlingTuning.InflatedRadius - PufferlingTuning.CalmRadius) * Inflate;
}

public sealed class PlaneShot
{
    public Vector2 Position;
    public Vector2 Velocity;
    public float Life;
    public bool FromPlayer;

    // Her shots, shaped by her pearls (ShotSpec).
    public float Damage = PufferlingTuning.NeedleDamage;
    public float Radius = PlaneCombatTuning.ShotRadius;
    public float Age;
    public float Traveled;
    public bool Homing, Pierce, Boomerang, Returning, Wave;
    /// <summary>A homing shot's locked target (a pufferling or a murkling).</summary>
    public object? Quarry;
    /// <summary>Starfish Arm: it grows as it flies. Starburst Gland: it bursts into light where it pops. Mitosis: it splits on a pop.</summary>
    public bool Grow, Explosive, Split;
    /// <summary>Pistol Shrimp Claw: it collapses with a bang where it pops or goes out.</summary>
    public bool Cavitation;
    /// <summary>Bubble Coral: the light slows to a hover near the end of its range instead of going out there at speed.</summary>
    public bool Lingers;
    /// <summary>Pearl Diver: how charged it was when thrown, 0–1 (the view draws a charged one as a pearl).</summary>
    public float Charged;
    /// <summary>Bubbles: thrown at Speed0, slowing as if to stop at Range, then easing off with drag (0: a plain shot at constant speed).</summary>
    public float Speed0, Range;
    /// <summary>How long a bubble has hovered (drifting below BubbleStop), and how long it will before it pops.</summary>
    public float Rest, Hover = 1f;
    /// <summary>Merging: the volley it came from, its unmerged radius, and how many bubbles it is made of.</summary>
    public int Volley;
    public float BaseRadius = PlaneCombatTuning.ShotRadius;
    public int Bubbles = 1;
    public int BouncesLeft;
    public float WavePhase;
    /// <summary>Wave shots: the point on their line (Position weaves around it).</summary>
    public Vector2 Line;
    /// <summary>Queen Clam's pearls: corruption, which her light cannot clear; the royal one homes. (PearlHp is no longer used.)</summary>
    public bool BossPearl, Royal;
    /// <summary>Her bubbles' light, 1 → dimmer (a murkling, its throw, a Blightroot's ring); damage dims with it.</summary>
    public float Dim = 1f;
    /// <summary>The last Blightroot ring that dimmed it (each ring dims a bubble once).</summary>
    public int Pulse;
    /// <summary>A piercing shot's last corrupted creature struck (it is struck once as the shot passes through).</summary>
    public object? Struck;
    /// <summary>It has struck a secret's seal (a bubble that bounces off counts once).</summary>
    public bool SealStruck;
    /// <summary>A pufferling's needle: fast, and it pops any bubble it meets (flying on).</summary>
    public bool Needle;
    public int PearlHp;
    /// <summary>Piercing and boomerang shots hit each mob once (per pass).</summary>
    public HashSet<PlaneMob>? Hit;
}

/// <summary>An ambush clearing: it springs (spawning mobs around her) the first time she enters.</summary>
public sealed class PlaneAmbush
{
    public int Poi;
    public Vector2 Center;
    public float Radius;
    public bool Sprung;
    public int Spawned;
}

/// <summary>A pearl lying in a treasure room until Clementine swims over it.</summary>
public sealed class PlanePearl
{
    public string ItemId = "";
    public Vector2 Position;
    public bool Taken;
    /// <summary>
    /// The absorbing ritual (docs/PEARLS.md): seconds since she touched it, or below zero while it waits. It is hers
    /// only once the ritual completes (Taken).
    /// </summary>
    public float Ritual = -1f;
    /// <summary>Set where it lies (a boss's cleansed pearl): it does not float down into place.</summary>
    public bool Placed;
    public bool Absorbing => Ritual >= 0f && !Taken;
}

/// <summary>
/// The absorbing ritual's beats (all while she swims on): she takes on the pearl's colours, glow and pattern as it
/// pales (Mimic), holds the new look (Hold), the pearl shatters and dissolves (Shatter), and her light bursts out as
/// she returns to her own colours: that burst is the moment it is absorbed.
/// </summary>
public static class PearlRitual
{
    public const float Mimic = 1.2f, Hold = 0.45f, Shatter = 0.3f;
    public const float ShatterAt = Mimic + Hold, Total = Mimic + Hold + Shatter;
}

public sealed partial class PlaneWorld
{
    public List<PlaneMob> Mobs { get; } = new();
    public List<PlaneShot> Shots { get; } = new();
    public List<PlanePearl> Pearls { get; } = new();

    /// <summary>The ambush places of the level, and whether each has sprung (it springs once).</summary>
    public List<PlaneAmbush> Ambushes { get; } = new();

    void PlaceAmbushes()
    {
        for (int i = 0; i < Map.Pois.Count; i++)
            if (Map.Pois[i].Kind == PoiKind.Ambush) Ambushes.Add(new PlaneAmbush { Poi = i, Center = Map.Pois[i].Position, Radius = Map.Pois[i].Radius });
    }

    /// <summary>
    /// The first time she enters an ambush clearing, mobs appear in a ring around her, already hunting her, their first
    /// shots staggered. Afterwards they are ordinary mobs: if she swims away they stay behind.
    /// </summary>
    void StepAmbushes()
    {
        var p = Player;
        foreach (var ambush in Ambushes)
        {
            if (ambush.Sprung || Vector2.Distance(p.Position, ambush.Center) > ambush.Radius) continue;
            ambush.Sprung = true;
            if (!Options.Pufferlings)
            {
                // The pufferlings are retracted: the clearing's corruption buds murklings round her instead.
                // (A quiet level's template springs none.)
                if (Corruption is not null && !Quiet) AmbushBuds(ambush);
                Events.Add(new PlaneEvent(PlaneEventType.AmbushSprung, ambush.Center, Vector2.Zero));
                continue;
            }
            var rng = new Rng(Map.Seed ^ 0xA3B05UL ^ ((ulong)ambush.Poi << 16) ^ ((ulong)Map.Level << 32) ^ ((ulong)Map.Depth << 40) ^ ((ulong)Map.Attempt << 48));
            int count = PlaneCombatTuning.AmbushMin + rng.Int(PlaneCombatTuning.AmbushMax - PlaneCombatTuning.AmbushMin + 1);
            float turn = rng.Range(0f, MathF.Tau);
            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                float a = turn + MathF.Tau * i / count + rng.Range(-0.2f, 0.2f);
                Vector2 dir = new(MathF.Cos(a), MathF.Sin(a));
                // The ring where there is water; closer in where rock is near.
                foreach (float r in new[] { PlaneCombatTuning.AmbushRing, PlaneCombatTuning.AmbushRing * 0.75f, PlaneCombatTuning.AmbushRing * 0.5f })
                {
                    Vector2 at = p.Position + dir * r;
                    if (!Clear(at, PlaneCombatTuning.MobRadius) || !LineOfSight(p.Position, at)) continue;
                    Mobs.Add(new PlaneMob
                    {
                        Spawn = Mobs.Count,
                        Position = at,
                        Home = at,
                        Heading = MathF.Atan2(-dir.Y, -dir.X),
                        Aggro = true,
                        State = PufferState.Face,
                        FireTimer = PlaneCombatTuning.AmbushFirstShot + spawned * PlaneCombatTuning.AmbushShotStagger,
                        Rng = new Rng(rng.NextU64()),
                    });
                    Events.Add(new PlaneEvent(PlaneEventType.MobNoticed, at, Vector2.Zero));
                    spawned++;
                    break;
                }
            }
            ambush.Spawned = spawned;
            Events.Add(new PlaneEvent(PlaneEventType.AmbushSprung, ambush.Center, Vector2.Zero));
        }
    }

    /// <summary>She ran out of HP: the run is over and the world no longer steps.</summary>
    public bool Defeated { get; private set; }

    /// <summary>What she carries through the rift: HP and pearls.</summary>
    public PlaneRun Run { get; private set; } = null!;

    /// <summary>The last pearl she picked up (for the HUD), with the event that announces it.</summary>
    public string? LastPearl { get; private set; }

    /// <summary>A treasure room holds one pearl she may take: one she lacks, or another copy of a non-unique one (from the room's own seed).</summary>
    void PlacePearls()
    {
        var rng = new Rng(Map.Seed * 0x9E3779B97F4A7C15UL ^ (ulong)(Map.Cycle * 100000 + Map.Depth * 1000 + Map.Level) ^ ((ulong)Map.Attempt << 40));
        foreach (var poi in Map.Pois)
        {
            if (poi.Kind != PoiKind.TreasureCave) continue;
            var offer = PlaneRun.PortedPearls.Where(id => Run.CanOffer(id) && Pearls.All(q => q.ItemId != id)).ToList();
            if (offer.Count == 0) break;
            Pearls.Add(new PlanePearl { ItemId = Run.Pick(offer, rng), Position = poi.Position });
        }
    }

    /// <summary>The way down: open, except on a boss level until the boss is freed.</summary>
    public bool ExitOpen { get; set; } = true;

    /// <summary>She floats over the shaft.</summary>
    public bool OverShaft => Map.Shaft.Contains(Player.Position);

    /// <summary>An active battle: the arena is sealed, or a creature that has noticed her is close.</summary>
    public bool InBattle => ArenaSealed || Mobs.Any(m => m.Alive && m.Aggro && Vector2.Distance(m.Position, Player.Position) < PlaneCombatTuning.DiveSafeDistance)
                            || Murklings.Any(m => m.Alive && !m.Budding && Vector2.Distance(m.Position, Player.Position) < PlaneCombatTuning.DiveSafeDistance);

    /// <summary>She may dive now (DESIGN-TOPDOWN §4.6): over the shaft, the way open, and no battle on.</summary>
    public bool CanDive => OverShaft && ExitOpen && !InBattle && !Defeated;

    /// <summary>Menace as the sim applies it (DESIGN-TOPDOWN §6.2), capped so later loops stay playable.</summary>
    public float Menace => MathF.Min(Map.Menace, 3f);

    /// <summary>
    /// Mob spawn points: the level's spawn table first, then spots in the canyons she can swim to (in a seeded order) —
    /// open water, apart, clear of the start and the rift's arena.
    /// </summary>
    void PlaceMobs()
    {
        var reach = LevelValidator.Distances(Map, Map.Start.Position);
        var spots = Map.Spawns.Select(s => s.Position).ToList();
        var extra = new List<Vector2>();
        for (float y = LevelMap.RimWidth + 2f; y < LevelMap.Size - LevelMap.RimWidth - 2f; y += 4f)
        for (float x = LevelMap.RimWidth + 2f; x < LevelMap.Size - LevelMap.RimWidth - 2f; x += 4f)
            extra.Add(new Vector2(x, y));
        var rng = new Rng(Map.Seed ^ 0x6D0B5UL ^ ((ulong)Map.Level << 20) ^ ((ulong)Map.Depth << 28) ^ ((ulong)Map.Attempt << 44));
        for (int i = extra.Count - 1; i > 0; i--)
        {
            int j = rng.Int(i + 1);
            (extra[i], extra[j]) = (extra[j], extra[i]);
        }
        spots.AddRange(extra);
        foreach (var p in spots)
        {
            if (Mobs.Count >= (int)(PlaneCombatTuning.MaxMobs * (1f + 0.4f * Menace))) break;
            if (float.IsPositiveInfinity(LevelValidator.DistanceAt(reach, p))) continue;
            if (Vector2.Distance(p, Map.Start.Position) < PlaneCombatTuning.MobStartClearance) continue;
            if (Vector2.Distance(p, Map.Exit.Position) < Map.Exit.Radius + 4f) continue;
            if (!Clear(p, PlaneCombatTuning.MobRadius)) continue;
            if (Mobs.Any(m => Vector2.Distance(m.Position, p) < PlaneCombatTuning.MobSpacing)) continue;
            Mobs.Add(new PlaneMob { Spawn = Mobs.Count, Position = p, Home = p, Heading = rng.Range(0f, MathF.Tau), Wait = rng.Range(0f, 3f), Rng = new Rng(rng.NextU64()) });
        }
    }

    /// <summary>True when nothing solid stands between two points on the plane.</summary>
    public bool LineOfSight(Vector2 a, Vector2 b)
    {
        float d = Vector2.Distance(a, b);
        for (float s = 0.5f; s < d; s += 0.5f)
        {
            var q = Vector2.Lerp(a, b, s / d);
            if (!Map.IsOpen(q) || _rocks.Any(r => Vector2.Distance(q, r.Center) < r.Radius)) return false;
        }
        return true;
    }

    void StepCombat(in PlaneInput input)
    {
        var p = Player;
        p.ShotTimer -= Dt;
        p.HurtTimer -= Dt;
        _shieldPing -= Dt;
        // Coral Polyp: her corruption slowly ebbs on its own.
        float regen = Run.Loadout.Stats[Stat.Regen];
        if (regen > 0f && p.Hp < Run.MaxHp)
        {
            p.Hp = MathF.Min(Run.MaxHp, p.Hp + regen * Dt);
            Run.Hp = p.Hp;
        }

        StepAmbushes();
        StepActive(input);

        // Clementine's shots, toward her aim while fire is held, shaped by her pearls. Firefly Squid pours them out as
        // one continuous surge instead (PlaneSurge.cs).
        bool aimed = p.Aim.LengthSquared() > 0.5f;
        // String of Pearls (Sardine Run + Pearl Diver): no holding to charge — the stream fires as Sardine Run's does,
        // and each shot is a pearl charged as far as the stream has spun up.
        bool pearlStream = Run.Loadout.Has("string_of_pearls");
        if (Run.Loadout.Flags.Contains("surge")) StepSurge(input.Fire && aimed);
        else if (Run.Loadout.Shot.Charge && !pearlStream)
        {
            // Pearl Diver: held, the next throw charges; let go, it flies.
            if (input.Fire && aimed)
            {
                if (p.ShotTimer <= 0f)
                {
                    float before = p.Charge;
                    p.Charge = MathF.Min(p.Charge + Dt, PlaneCombatTuning.ChargeSeconds);
                    if (before < PlaneCombatTuning.ChargeSeconds && p.Charge >= PlaneCombatTuning.ChargeSeconds)
                        Events.Add(new PlaneEvent(PlaneEventType.ChargeFull, p.Position, p.Aim));
                }
            }
            else if (p.Charge > 0f)
            {
                p.ShotTimer = ShotInterval;
                int thrown = Volley(p.Aim, p.Charge / PlaneCombatTuning.ChargeSeconds);
                p.Charge = 0f;
                Events.Add(new PlaneEvent(PlaneEventType.Shot, p.Position, p.Aim, thrown));
            }
        }
        else
        {
            p.Charge = 0f;
            // Sardine Run: the stream spins up while she holds fire, and down when she lets go.
            if (Run.Loadout.Flags.Contains("minigun"))
                p.Spin = input.Fire && aimed ? MathF.Min(1f, p.Spin + Dt / PlaneCombatTuning.SpinUp) : MathF.Max(0f, p.Spin - Dt / PlaneCombatTuning.SpinDown);
            else p.Spin = 0f;
            if (input.Fire && p.ShotTimer <= 0f && aimed)
            {
                p.ShotTimer = ShotInterval;
                // Just short of a full charge: the full-charge pearl's own tricks (Pearl Necklace, Depth Charge) stay
                // the held throw's.
                int thrown = Volley(p.Aim, pearlStream ? p.Spin * PlaneCombatTuning.StreamCharge : 0f);
                Events.Add(new PlaneEvent(PlaneEventType.Shot, p.Position, p.Aim, thrown));
            }
        }
        p.WasFiring = input.Fire;

        // Pearls: touching one begins the absorbing ritual (one at a time); it plays round her as she swims on, and
        // the pearl is hers when its light bursts.
        StepRituals();
        if (!Pearls.Exists(q => q.Absorbing))
            foreach (var pearl in Pearls)
            {
                if (pearl.Taken || pearl.Absorbing || Vector2.Distance(pearl.Position, p.Position) > Radius + PlaneCombatTuning.PearlReach) continue;
                BeginRitual(pearl);
                break;
            }

        foreach (var mob in Mobs)
        {
            if (!mob.Alive) continue;
            mob.HitFlash -= Dt;
            // Knocked back by her hits, easing off.
            if (mob.Knock.LengthSquared() > 1e-4f)
            {
                Move(ref mob.Position, ref mob.Knock, PufferlingTuning.CalmRadius, report: false, barrier: ArenaBarrier.Outside);
                mob.Knock *= MathF.Exp(-PlaneCombatTuning.KnockDecay * Dt);
            }
            StepPufferling(mob);
        }

        foreach (var shot in Shots)
        {
            if (shot.FromPlayer) StepHerShot(shot);
            else
            {
                // The royal pearl homes on her.
                if (shot.Royal)
                {
                    Vector2 to = SafeNormalize(p.Position - shot.Position);
                    if (to != Vector2.Zero)
                        shot.Velocity = Turn(SafeNormalize(shot.Velocity), to, PlaneBossTuning.RoyalTurnDegPerSec * MathUtil.Deg2Rad * Dt) * shot.Velocity.Length();
                }
                Vector2 was = shot.Position;
                shot.Position += shot.Velocity * Dt;
                shot.Life -= Dt;
                // A needle pops any bubble it meets, and flies on.
                if (shot.Needle)
                    foreach (var bubble in Shots)
                        if (bubble.FromPlayer && bubble.Life > 0f && Geo.SegmentDistance(bubble.Position, was, shot.Position) < bubble.Radius + shot.Radius)
                            Pop(bubble, was - bubble.Position);
                // Rock and pots stop shots, and so does the sealed arena's wall.
                if (!Map.IsOpen(shot.Position) || _rocks.Any(r => Vector2.Distance(shot.Position, r.Center) < r.Radius) || VaseAt(shot.Position, shot.Radius) is not null || CrossesRim(was, shot.Position)) shot.Life = 0f;
            }
            if (shot.Life <= 0f) continue;
            if (shot.FromPlayer)
            {
                if (BubbleHitsBoss(shot)) continue;
                // Queen Clam's pearls are corruption: her light cannot clear them, and passes through.
                foreach (var mob in Mobs)
                {
                    if (!mob.Alive || Vector2.Distance(mob.Position, shot.Position) > mob.Radius + shot.Radius) continue;
                    if (shot.Hit is not null && !shot.Hit.Add(mob)) continue;
                    bool fresh = mob.Hp >= PlaneCombatTuning.MobHp - 0.01f;
                    DamageMob(mob, HitDamage(shot), shot.Velocity);
                    // A full bubble freeing a foe at full health in one hit (Big Bubble Energy).
                    if (!mob.Alive && fresh && shot.Bubbles >= PlaneCombatTuning.BubbleCap)
                        Events.Add(new PlaneEvent(PlaneEventType.FullBubbleFreed, mob.Position, shot.Velocity, shot.Bubbles));
                    if (!shot.Pierce && !shot.Boomerang)
                    {
                        Pop(shot, mob.Position - shot.Position);
                        SplitBubble(shot, SafeNormalize(shot.Velocity), mob);
                    }
                    if (shot.Life <= 0f) break;
                }
            }
            else if (Vector2.Distance(p.Position, shot.Position) < Radius + shot.Radius)
            {
                shot.Life = 0f;
                var source = shot.Royal ? DamageSource.RoyalPearl : shot.BossPearl ? DamageSource.BossPearl : DamageSource.PufferNeedle;
                if (HurtPlayer(shot.Damage, shot.Velocity, source) && shot.Royal) p.SlowTimer = PlaneBossTuning.SlowSeconds;
            }
        }
        // Halves split off during the step join now (the shots were being walked).
        Shots.AddRange(_spawned);
        _spawned.Clear();
        TouchBubbles();
        Shots.RemoveAll(s => s.Life <= 0f);

        // Out of HP: the run is over (the world stops; the game shows the death splash and starts a new run).
        if (p.Hp <= 0f)
        {
            p.Hp = 0f;
            p.Velocity = Vector2.Zero;
            Defeated = true;
            Events.Add(new PlaneEvent(PlaneEventType.PlayerDefeated, p.Position, Vector2.Zero, 0f, LastHitSource));
        }

        Run.Hp = p.Hp;

        if (input.Dive && CanDive)
        {
            // A ritual still playing completes as she dives: the pearl is never lost to the dive.
            foreach (var pearl in Pearls)
                if (pearl.Absorbing) FinishRitual(pearl);
            Events.Add(new PlaneEvent(PlaneEventType.Dived, p.Position, Vector2.Zero));
        }
    }

    /// <summary>What hurt her last (on her defeat: what ended the run).</summary>
    public DamageSource LastHitSource { get; private set; }

    /// <summary>A mob notices her: it hunts her from now on.</summary>
    void Notice(PlaneMob mob)
    {
        mob.Aggro = true;
        Events.Add(new PlaneEvent(PlaneEventType.MobNoticed, mob.Position, Vector2.Zero));
    }

    /// <summary>A mob takes a hit: it notices her, flashes, is shoved along dir, and is freed when its health runs out.</summary>
    /// <remarks>Quiet (Firefly Squid's surge, which tells its hits summed): no hit event unless it frees the mob.</remarks>
    void DamageMob(PlaneMob mob, float damage, Vector2 dir, bool quiet = false)
    {
        mob.Hp -= damage;
        if (!mob.Aggro) Notice(mob);
        mob.HitFlash = PlaneCombatTuning.HitFlash;
        mob.Knock += SafeNormalize(dir) * Tuning.ShotKnockback * Run.Loadout.Stats[Stat.Knockback];
        if (!quiet || !mob.Alive)
            Events.Add(new PlaneEvent(mob.Alive ? PlaneEventType.MobHit : PlaneEventType.MobDefeated, mob.Position, dir, damage));
        if (mob.Alive) return;
        Stats.MobsDefeated++;
        Drop(mob.Position);
        Free(mob);
    }

    /// <summary>
    /// Starburst Gland: a popped shot bursts into light, striking every creature within reach (pufferlings, murklings,
    /// Blotters, a Blightroot's sac, Queen Clam while open), breaking pots, and cleansing the ground it floods.
    /// </summary>
    void LightBurst(PlaneShot shot) =>
        LightBurst(shot.Position, PlaneCombatTuning.BurstRadius + shot.Radius, shot.Damage * GrowFactor(shot) * PlaneCombatTuning.BurstDamage);

    void LightBurst(Vector2 at, float reach, float damage)
    {
        Events.Add(new PlaneEvent(PlaneEventType.LightBurst, at, Vector2.Zero, reach));
        BurstOnCorruption(at, reach, damage);
        foreach (var mob in Mobs)
            if (mob.Alive && Vector2.Distance(mob.Position, at) < reach + mob.Radius)
                DamageMob(mob, damage, mob.Position - at);
        BlastBoss(at, reach, damage);
        BlastVases(at, reach);
    }

    void BeginRitual(PlanePearl pearl)
    {
        pearl.Ritual = 0f;
        Events.Add(new PlaneEvent(PlaneEventType.PearlTouched, pearl.Position, Vector2.Zero));
    }

    void StepRituals()
    {
        foreach (var pearl in Pearls)
        {
            if (!pearl.Absorbing) continue;
            float was = pearl.Ritual;
            pearl.Ritual += Dt;
            // The pearl stays by her while the ritual plays.
            pearl.Position = Player.Position;
            if (was < PearlRitual.ShatterAt && pearl.Ritual >= PearlRitual.ShatterAt)
                Events.Add(new PlaneEvent(PlaneEventType.PearlShattered, pearl.Position, Vector2.Zero));
            if (pearl.Ritual >= PearlRitual.Total) FinishRitual(pearl);
        }
    }

    /// <summary>The ritual completes (or is cut short by a dive): her light bursts and the pearl is hers.</summary>
    void FinishRitual(PlanePearl pearl)
    {
        if (pearl.Taken) return;
        pearl.Taken = true;
        Absorb(pearl.ItemId, Player.Position);
    }

    /// <summary>A pearl taken (found or bought): absorbed into her loadout; one that heals on pickup heals her now.</summary>
    void Absorb(string itemId, Vector2 at)
    {
        var p = Player;
        Run.Add(itemId);
        if (Run.Catalog is { } catalog && catalog.TryGet(itemId, out var item))
            foreach (var e in item.Effects)
            {
                if (e.Trigger != Trigger.OnPickup) continue;
                if (e.Action == EffectAction.Heal) p.Hp += e.Value;
                if (e.Action == EffectAction.Coins) Run.Shells += (int)e.Value;
            }
        p.Hp = MathF.Min(p.Hp, Run.MaxHp);
        LastPearl = itemId;
        Stats.PearlsFound++;
        Events.Add(new PlaneEvent(PlaneEventType.PearlCollected, at, Vector2.Zero));
    }

    /// <summary>
    /// The active pearl (F): it recharges over its listed time (faster with her recharge stat), and when charged, F uses
    /// it. Bubble Shield: untouchable for its duration. Whale Song: heals (not at full HP).
    /// </summary>
    void StepActive(in PlaneInput input)
    {
        var p = Player;
        p.ShieldTimer -= Dt;
        if (Run.Active?.Active is not { } spec) return;
        if (Run.ActiveCharge < 1f)
            Run.ActiveCharge = MathF.Min(1f, Run.ActiveCharge + Dt * Run.Loadout.Stats[Stat.ActiveRecharge] / MathF.Max(spec.Recharge, 0.1f));
        if (!input.UseActive) return;
        if (Run.ActiveCharge < 1f)
        {
            Events.Add(new PlaneEvent(PlaneEventType.ActiveNotReady, p.Position, Vector2.Zero));
            return;
        }
        float size = 0f;
        switch (spec.Action)
        {
            case ActiveAction.BubbleShield:
                p.ShieldTimer = spec.Duration;
                break;
            case ActiveAction.WhaleSong:
                if (p.Hp >= Run.MaxHp)
                {
                    Events.Add(new PlaneEvent(PlaneEventType.ActiveDenied, p.Position, Vector2.Zero));
                    return;
                }
                size = MathF.Min(spec.Value, Run.MaxHp - p.Hp);
                p.Hp += size;
                break;
            // The other actives are not on the plane yet (they are never offered).
            default:
                return;
        }
        Run.ActiveCharge = 0f;
        Events.Add(new PlaneEvent(PlaneEventType.ActiveUsed, p.Position, Vector2.Zero, size));
    }

    /// <summary>Bubble Shield turned a hit away: told at most this often.</summary>
    float _shieldPing;

    /// <summary>She is hurt, unless shielded, dashing or still in the grace after the last hit.</summary>
    bool HurtPlayer(float damage, Vector2 dir, DamageSource source)
    {
        var p = Player;
        if (Defeated) return false;
        if (p.ShieldTimer > 0f)
        {
            if (_shieldPing <= 0f) Events.Add(new PlaneEvent(PlaneEventType.ShieldBlocked, p.Position, dir));
            _shieldPing = 0.2f;
            return false;
        }
        if (p.DashInvulnerableTimer > 0f || p.HurtTimer > 0f) return false;
        p.Hp -= damage;
        Stats.DamageTaken += damage;
        p.HurtTimer = PlaneCombatTuning.PlayerHurtGrace;
        LastHitSource = source;
        Events.Add(new PlaneEvent(PlaneEventType.PlayerHit, p.Position, dir, damage, source));
        return true;
    }

    /// <summary>Her damage stat as a factor on a bubble's damage (1 with no pearls; Shark Tooth and Coral Crown raise it).</summary>
    float DamageScale => Run.Loadout.Stats.Damage / MathF.Max(Tuning.Damage, 0.01f);

    /// <summary>Her stats against the tuning's own (1 with no pearls): how fast she throws, how fast and far her light flies.</summary>
    float FireScale => Run.Loadout.Stats.FireRate / MathF.Max(Tuning.FireRate, 0.01f);
    float ShotSpeedScale => Run.Loadout.Stats.ShotSpeed / MathF.Max(Tuning.ShotSpeed, 0.01f);
    float RangeScale => Run.Loadout.Stats.Range / MathF.Max(Tuning.Range, 0.01f);

    /// <summary>How far this throw flies: to where she aims (never nearer than MinReach), at most her range.</summary>
    float ThrowReach
    {
        get
        {
            float range = PlaneCombatTuning.BubbleRange * RangeScale;
            return Player.AimReach > 0f ? Math.Clamp(Player.AimReach, PlaneCombatTuning.MinReach, range) : range;
        }
    }

    /// <summary>The time to her next throw: her fire rate's, and quicker as Sardine Run's stream spins up.</summary>
    float ShotInterval => PlaneCombatTuning.ShotInterval / FireScale / (1f + (PlaneCombatTuning.MinigunMax - 1f) * Player.Spin);

    /// <summary>
    /// One volley along the aim: her loadout's shot count, fan or cone, and every flag on each shot. Charge (Pearl
    /// Diver, 0–1) makes each one bigger and harder.
    /// </summary>
    int Volley(Vector2 aim, float charge = 0f)
    {
        LastVolleyDamage = 0f;
        var p = Player;
        var spec = Run.Loadout.Shot;
        int count = Math.Max(1, spec.Multishot);
        float speed = PlaneCombatTuning.ShotSpeed * ShotSpeedScale;
        float damage = PlaneCombatTuning.ShotDamage * spec.DamageMult * DamageScale * (1f + (PlaneCombatTuning.ChargeDamage - 1f) * charge);
        float radius = PlaneCombatTuning.ShotRadius * spec.SizeMult * Run.Loadout.Stats[Stat.ShotSize] * (1f + (PlaneCombatTuning.ChargeSize - 1f) * charge);
        bool minigun = Run.Loadout.Flags.Contains("minigun"), cavitation = Run.Loadout.Flags.Contains("cavitation");
        // Sardine Run: the stream strays a little off her aim, shot by shot.
        if (minigun) aim = Rotate(aim, _bubbleRng.Range(-1f, 1f) * PlaneCombatTuning.MinigunJitterDeg * MathUtil.Deg2Rad);
        _volleys++;
        for (int i = 0; i < count; i++)
        {
            // Wave shots all fly along the aim, weaving out of phase; the rest fan out (a cone spreads wider).
            float spread = spec.Pattern == ShotPattern.Cone ? spec.SpreadDeg * 1.6f : spec.SpreadDeg;
            Vector2 dir = spec.Wave || count == 1 ? aim : Rotate(aim, (i - (count - 1) * 0.5f) * spread * MathUtil.Deg2Rad);
            Vector2 at = p.Position + dir * (Radius + 0.2f);
            float hover = _bubbleRng.Range(PlaneCombatTuning.BubbleHoverMin, PlaneCombatTuning.BubbleHoverMax) * Run.Loadout.Stats[Stat.BubbleHover];
            var shot = new PlaneShot
            {
                Position = at,
                Line = at,
                Velocity = dir * speed,
                Speed0 = speed,
                Range = ThrowReach,
                Life = PlaneCombatTuning.ShotLife + hover,
                FromPlayer = true,
                Damage = damage,
                Radius = radius,
                BaseRadius = radius,
                Grow = spec.Grow,
                Explosive = spec.Explosive,
                Cavitation = cavitation,
                Split = spec.Split,
                Lingers = Run.Loadout.Flags.Contains("mergeBubbles"),
                Charged = charge,
                Hover = hover,
                Volley = _volleys,
                Homing = spec.Homing,
                Pierce = spec.Pierce,
                Boomerang = spec.Boomerang,
                Wave = spec.Wave,
                WavePhase = MathF.Tau * i / count,
                BouncesLeft = spec.Bounces,
            };
            if (shot.Pierce || shot.Boomerang) shot.Hit = new HashSet<PlaneMob>();
            Shots.Add(shot);
            LastVolleyDamage += shot.Damage;
        }
        return count;
    }

    /// <summary>The damage of every shot in her last volley, added up (the controller's kick on firing scales with it).</summary>
    public float LastVolleyDamage { get; private set; }

    int _volleys;

    /// <summary>
    /// Her bubbles that touch (not two of one volley, which leave her side by side) bounce off each other; with Bubble
    /// Coral they merge: the faster one carries on, with the longer life, the summed damage, and a radius grown by
    /// BubbleGrowth per bubble merged in.
    /// </summary>
    void TouchBubbles()
    {
        if (!Run.Loadout.Flags.Contains("mergeBubbles"))
        {
            BounceBubbles();
            return;
        }
        for (int i = 0; i < Shots.Count; i++)
        {
            var a = Shots[i];
            if (!a.FromPlayer || a.Life <= 0f) continue;
            for (int j = i + 1; j < Shots.Count; j++)
            {
                var b = Shots[j];
                if (!b.FromPlayer || b.Life <= 0f || a.Volley == b.Volley) continue;
                if (Vector2.Distance(a.Position, b.Position) > a.Radius + b.Radius) continue;
                var (keep, gone) = a.Velocity.LengthSquared() >= b.Velocity.LengthSquared() ? (a, b) : (b, a);
                float wk = keep.Bubbles, wg = gone.Bubbles, w = wk + wg;
                // Heading: the two momenta (bubbles × velocity) added; speed: the faster one's.
                Vector2 momentum = keep.Velocity * wk + gone.Velocity * wg;
                float speed = keep.Velocity.Length();
                if (momentum.LengthSquared() > 1e-8f && speed > 1e-4f) keep.Velocity = Vector2.Normalize(momentum) * speed;
                keep.Position = (keep.Position * wk + gone.Position * wg) / w;
                keep.Line = (keep.Line * wk + gone.Line * wg) / w;
                keep.Life = MathF.Max(keep.Life, gone.Life);
                keep.Rest = MathF.Min(keep.Rest, gone.Rest);
                keep.Hover = MathF.Max(keep.Hover, gone.Hover);
                // Past the cap only speed, heading and life carry over.
                int added = Math.Min(gone.Bubbles, PlaneCombatTuning.BubbleCap - keep.Bubbles);
                if (added < 0) added = 0;
                keep.Damage += gone.Damage * added / gone.Bubbles;
                keep.Bubbles += added;
                if (added > 0 && keep.Bubbles == PlaneCombatTuning.BubbleCap) Events.Add(new PlaneEvent(PlaneEventType.BubbleFull, keep.Position, keep.Velocity));
                keep.BaseRadius = MathF.Max(keep.BaseRadius, gone.BaseRadius);
                keep.Charged = MathF.Max(keep.Charged, gone.Charged);
                Resize(keep);
                // Absorbed, no pop.
                gone.Life = -1000f;
                if (gone == a) break;
            }
        }
    }

    /// <summary>
    /// Bubbles that touch push each other apart (the bigger one moving less) and, when they are closing, turn off each
    /// other like two balls of their size (weight by area). Their speed stays the bubble's own easing-out speed, so a
    /// bounce turns them; a bubble at rest is only nudged aside.
    /// </summary>
    void BounceBubbles()
    {
        for (int i = 0; i < Shots.Count; i++)
        {
            var a = Shots[i];
            if (!a.FromPlayer || a.Life <= 0f) continue;
            for (int j = i + 1; j < Shots.Count; j++)
            {
                var b = Shots[j];
                if (!b.FromPlayer || b.Life <= 0f || a.Volley == b.Volley) continue;
                Vector2 off = b.Position - a.Position;
                float dist = off.Length(), reach = a.Radius + b.Radius;
                if (dist >= reach) continue;
                Vector2 n = dist > 1e-4f ? off / dist : new Vector2(MathF.Cos(i * 2.4f + j), MathF.Sin(i * 2.4f + j));
                float ma = a.Radius * a.Radius, mb = b.Radius * b.Radius;
                // Apart: each moves by its share of the overlap (Line too, so a weaving bubble keeps its weave).
                float overlap = reach - dist;
                Vector2 pa = -n * overlap * mb / (ma + mb), pb = n * overlap * ma / (ma + mb);
                a.Position += pa;
                a.Line += pa;
                b.Position += pb;
                b.Line += pb;
                // Closing: a soft elastic knock along the line between them; each may pop on the bump.
                float closing = Vector2.Dot(a.Velocity - b.Velocity, n);
                if (closing <= 0f) continue;
                float impulse = (1f + PlaneCombatTuning.BubbleBounce) * closing / (1f / ma + 1f / mb);
                a.Velocity -= n * impulse / ma;
                b.Velocity += n * impulse / mb;
                if (_bubbleRng.NextFloat() < PlaneCombatTuning.BubblePopChance) Pop(b, -n);
                if (_bubbleRng.NextFloat() < PlaneCombatTuning.BubblePopChance)
                {
                    Pop(a, n);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// What one of her bubbles deals on a hit: its damage, grown with distance (Starfish Arm), and now and then a
    /// critical hit (Giant Squid Eye: CritChance of hits do CritMult×, from the level's seeded bubble rolls).
    /// </summary>
    float HitDamage(PlaneShot shot)
    {
        float damage = shot.Damage * GrowFactor(shot);
        var spec = Run.Loadout.Shot;
        if (spec.CritChance > 0f && _bubbleRng.NextFloat() < spec.CritChance) damage *= spec.CritMult;
        return damage;
    }

    /// <summary>Bubbles split off this step (Mitosis), added once the shots have all been walked.</summary>
    readonly List<PlaneShot> _spawned = new();

    /// <summary>
    /// Mitosis: a bubble that has just popped on something splits into two halves flying on either side of
    /// <paramref name="heading"/> (its way on: on through a foe, off a wall). The foe it popped on is not hit again by them.
    /// </summary>
    void SplitBubble(PlaneShot shot, Vector2 heading, PlaneMob? spare = null)
    {
        if (!shot.Split || !shot.FromPlayer) return;
        if (heading == Vector2.Zero) heading = Player.Aim;
        float speed = MathF.Max(PlaneCombatTuning.SplitSpeed, shot.Velocity.Length());
        int volley = ++_volleys;
        // Pearl Necklace (Pearl Diver + Mitosis): a full-charge pearl bursts into a ring all round; otherwise two halves.
        bool necklace = shot.Charged >= 0.99f && Run.Loadout.Has("pearl_necklace");
        var turns = necklace
            ? Enumerable.Range(0, PlaneCombatTuning.NecklaceCount).Select(i => MathF.Tau * i / PlaneCombatTuning.NecklaceCount).ToArray()
            : new[] { -PlaneCombatTuning.SplitAngleDeg * MathUtil.Deg2Rad, PlaneCombatTuning.SplitAngleDeg * MathUtil.Deg2Rad };
        if (necklace) Events.Add(new PlaneEvent(PlaneEventType.Necklace, shot.Position, heading, shot.Radius));
        foreach (float turn in turns)
        {
            Vector2 dir = Rotate(heading, turn);
            Vector2 at = shot.Position + dir * (shot.Radius + 0.15f);
            if (!Map.IsOpen(at)) continue;
            float radius = shot.BaseRadius * PlaneCombatTuning.SplitSize;
            var half = new PlaneShot
            {
                Position = at,
                Line = at,
                Velocity = dir * speed,
                Speed0 = speed,
                Range = PlaneCombatTuning.SplitRange,
                Life = PlaneCombatTuning.ShotLife,
                FromPlayer = true,
                Damage = shot.Damage * GrowFactor(shot) * 0.5f,
                Radius = radius,
                BaseRadius = radius,
                Volley = volley,
                Explosive = shot.Explosive,
                Cavitation = shot.Cavitation,
                Lingers = shot.Lingers,
                Hover = _bubbleRng.Range(PlaneCombatTuning.BubbleHoverMin, PlaneCombatTuning.BubbleHoverMax) * Run.Loadout.Stats[Stat.BubbleHover],
            };
            if (spare is not null) half.Hit = new HashSet<PlaneMob> { spare };
            _spawned.Add(half);
        }
    }

    /// <summary>Starfish Arm: how much a bubble has grown with the distance flown (1 when it does not grow).</summary>
    static float GrowFactor(PlaneShot shot)
    {
        if (!shot.Grow) return 1f;
        float range = shot.Range > 0f ? shot.Range : PlaneCombatTuning.BubbleRange;
        return 1f + (PlaneCombatTuning.GrowMax - 1f) * MathUtil.Clamp01(shot.Traveled / range);
    }

    /// <summary>A bubble's radius: its own, grown by the bubbles merged into it and by the distance it has flown.</summary>
    static void Resize(PlaneShot shot) =>
        shot.Radius = shot.BaseRadius * (1f + PlaneCombatTuning.BubbleGrowth * (shot.Bubbles - 1)) * GrowFactor(shot);

    static Vector2 Rotate(Vector2 v, float a) => new(v.X * MathF.Cos(a) - v.Y * MathF.Sin(a), v.X * MathF.Sin(a) + v.Y * MathF.Cos(a));

    /// <summary>Moves one of her shots: boomerang return, homing turn, wave weave, bounces off rock.</summary>
    void StepHerShot(PlaneShot shot)
    {
        var p = Player;
        shot.Age += Dt;
        shot.Life -= Dt;
        float speed = shot.Velocity.Length();
        // Light has no friction: it flies at full speed to the end of its range and goes out there, like a match, and
        // it does not burst (unless a pearl makes it: the Starburst Gland's burst). With Bubble Coral it lingers instead.
        bool lingers = shot.Lingers && shot.Range > 0f && !shot.Boomerang;
        if (shot.Life <= 0f || !lingers && shot.Range > 0f && !shot.Boomerang && shot.Traveled >= shot.Range)
        {
            Extinguish(shot);
            return;
        }
        if (lingers)
        {
            // Steady slowing while fast, the water's drag once that is gentler; slow, it hovers its own time, then goes out.
            float decel = shot.Speed0 * shot.Speed0 / (2f * MathF.Min(shot.Range, PlaneCombatTuning.LingerRange));
            speed = MathF.Max(speed - MathF.Min(decel, PlaneCombatTuning.LingerDrag * speed) * Dt, 0f);
            if (speed < PlaneCombatTuning.BubbleStop)
            {
                shot.Rest += Dt;
                if (shot.Rest >= shot.Hover)
                {
                    Extinguish(shot);
                    return;
                }
            }
        }
        Vector2 dir = shot.Velocity.LengthSquared() > 1e-8f ? Vector2.Normalize(shot.Velocity) : p.Aim;
        float range = shot.Range > 0f ? shot.Range : PlaneCombatTuning.BubbleRange;

        if (shot.Boomerang)
        {
            if (!shot.Returning && shot.Traveled >= range * 0.75f)
            {
                shot.Returning = true;
                shot.Hit?.Clear();
            }
            if (shot.Returning)
            {
                Vector2 home = p.Position - shot.Line;
                if (home.Length() < Radius + 0.4f)
                {
                    // Back home: absorbed, no pop.
                    shot.Life = -1000f;
                    return;
                }
                dir = Turn(dir, Vector2.Normalize(home), 540f * MathUtil.Deg2Rad * Dt);
            }
        }
        else if (shot.Homing)
        {
            // Locked on one creature: kept while it can still be had, else the nearest one ahead is taken.
            if (shot.Quarry is not null && HomingOn(shot, shot.Quarry, dir) is null) shot.Quarry = null;
            if (shot.Quarry is null)
            {
                float best = PlaneCombatTuning.HomingReach;
                foreach (var mob in Mobs)
                    Consider(mob);
                // Murklings and Thumpsquirts draw homing bubbles too.
                foreach (var m in Murklings)
                    Consider(m);
                foreach (var t in Thumpsquirts)
                    Consider(t);
                foreach (var w in Inkwardens)
                    Consider(w);
                foreach (var l in Lashjaws)
                    Consider(l);

                void Consider(object o)
                {
                    if (HomingOn(shot, o, dir) is not { } at) return;
                    float d = Vector2.Distance(at, shot.Line);
                    if (d >= best) return;
                    best = d;
                    shot.Quarry = o;
                }
            }
            if (shot.Quarry is not null && HomingOn(shot, shot.Quarry, dir) is { } homeOn && Vector2.DistanceSquared(homeOn, shot.Line) > 1e-6f)
                dir = Turn(dir, Vector2.Normalize(homeOn - shot.Line), PlaneCombatTuning.HomingDegPerSec * MathUtil.Deg2Rad * Dt);
        }
        shot.Velocity = dir * speed;

        Vector2 next = shot.Line + shot.Velocity * Dt;
        // A pot in the way: broken if the bubble is strong enough, else it rocks and the bubble pops or bounces off.
        if (VaseAt(next, shot.Radius) is { } vase && !(shot.Boomerang && shot.Returning))
        {
            BubbleMeetsVase(shot, vase, dir);
            return;
        }
        bool rim = CrossesRim(shot.Line, next);
        if (rim || !Map.IsOpen(next) || _rocks.Any(r => Vector2.Distance(next, r.Center) < r.Radius))
        {
            // Her light striking a secret's seal lights its emblem (once a bubble, whatever it carries).
            if (!rim && !shot.SealStruck && _rocks.Any(r => Vector2.Distance(next, r.Center) < r.Radius))
            {
                shot.SealStruck = true;
                StrikeSeal(next);
            }
            if (rim)
            {
                Pop(shot, dir);
                return;
            }
            // A returning boomerang passes back over what it cleared.
            if (!(shot.Boomerang && shot.Returning))
            {
                // Mirror Scale's bounces are sure; otherwise the bump may pop it, or it bounces off.
                bool bounce = shot.BouncesLeft > 0 || _bubbleRng.NextFloat() >= PlaneCombatTuning.BubblePopChance;
                if (!bounce)
                {
                    Pop(shot, dir);
                    Vector2 wall = Map.Gradient(next);
                    wall = wall.LengthSquared() > 1e-6f ? Vector2.Normalize(wall) : dir;
                    SplitBubble(shot, SafeNormalize(dir - 2f * Vector2.Dot(dir, wall) * wall));
                    return;
                }
                if (shot.BouncesLeft > 0) shot.BouncesLeft--;
                // Off the rock face: reflect about the slope's normal (uphill = into the wall).
                Vector2 n = Map.Gradient(next);
                n = n.LengthSquared() > 1e-6f ? Vector2.Normalize(n) : dir;
                if (Vector2.Dot(shot.Velocity, n) > 0f) shot.Velocity -= 2f * Vector2.Dot(shot.Velocity, n) * n;
                else shot.Velocity = -shot.Velocity;
                return;
            }
        }
        shot.Traveled += Vector2.Distance(shot.Line, next);
        if (shot.Grow) Resize(shot);
        shot.Line = next;
        shot.Position = next;
        if (shot.Wave)
        {
            Vector2 side = new(-dir.Y, dir.X);
            shot.Position += side * (MathF.Sin(shot.WavePhase + shot.Age * PlaneCombatTuning.WaveTurns * MathF.Tau) * PlaneCombatTuning.WaveAmplitude);
        }
    }

    /// <summary>
    /// A bubble bursts where it is (on a mob, on rock, or where it hovered); a Starburst Gland one bursts into light. toward: from
    /// its centre to where it was struck (the film tears open there first); none, it gives way at a random spot.
    /// </summary>
    /// <summary>
    /// A shot of her light at the end of its flight goes out like a match: no burst, only a wisp (the view), but its last
    /// light still clears a little of the ground's corruption there (as a pop does). With the Starburst Gland it still
    /// bursts into light, over a wider reach.
    /// </summary>
    void Extinguish(PlaneShot shot)
    {
        if (shot.Life < -100f) return;
        if (shot.Explosive || shot.Cavitation && shot.FromPlayer)
        {
            Pop(shot);
            return;
        }
        shot.Life = -1000f;
        Events.Add(new PlaneEvent(PlaneEventType.ShotOut, shot.Position, SafeNormalize(shot.Velocity), shot.Radius));
        LightReleased(shot);
    }

    /// <summary>
    /// Her light struck something: the surge that arcs back to her (the view's lightning) is bright enough to cleanse a
    /// thin line of ground on its way.
    /// </summary>
    void StrikeBack()
    {
        if (Corruption is not { } field) return;
        var her = Player.Position;
        foreach (var e in Events)
        {
            if (e.Type is not (PlaneEventType.MobHit or PlaneEventType.MobDefeated or PlaneEventType.BossHit or PlaneEventType.VaseHit) || e.Direction == Vector2.Zero) continue;
            var from = e.Position;
            float length = Vector2.Distance(from, her);
            int steps = Math.Max(1, (int)(length / 0.5f));
            for (int i = 0; i <= steps; i++)
                field.Cleanse(Vector2.Lerp(from, her, i / (float)steps), PlaneCombatTuning.StrikeWidth, PlaneCombatTuning.StrikeCleanse);
        }
    }

    void Pop(PlaneShot shot, Vector2? toward = null)
    {
        if (shot.Life < -100f) return;
        shot.Life = -1000f;
        Vector2 at = toward is { } t ? SafeNormalize(t) : Vector2.Zero;
        if (at == Vector2.Zero)
        {
            float a = _bubbleRng.Range(0f, MathF.Tau);
            at = new Vector2(MathF.Cos(a), MathF.Sin(a));
        }
        Events.Add(new PlaneEvent(PlaneEventType.ShotPopped, shot.Position, at, shot.Radius));
        if (shot.FromPlayer && shot.Explosive) LightBurst(shot);
        if (shot.FromPlayer && shot.Cavitation) Cavitate(shot);
        LightReleased(shot);
    }

    /// <summary>
    /// Pistol Shrimp Claw: the bubble collapses with a bang, striking every creature within reach (pufferlings, the
    /// corrupted, Queen Clam while open) and breaking pots. Unlike the Starburst Gland's light it does not cleanse the
    /// ground. Depth Charge makes a full-charge pearl's blast wider and harder.
    /// </summary>
    void Cavitate(PlaneShot shot)
    {
        bool depth = shot.Charged >= 0.99f && Run.Loadout.Has("depth_charge");
        float reach = (PlaneCombatTuning.CavitationRadius + shot.Radius) * (depth ? PlaneCombatTuning.DepthChargeReach : 1f);
        float damage = shot.Damage * GrowFactor(shot) * PlaneCombatTuning.CavitationDamage * (depth ? PlaneCombatTuning.DepthChargeDamage : 1f);
        var at = shot.Position;
        Events.Add(new PlaneEvent(PlaneEventType.Cavitation, at, Vector2.Zero, reach));
        BurstOnCorruption(at, reach, damage, cleanse: false);
        foreach (var mob in Mobs)
            if (mob.Alive && Vector2.Distance(mob.Position, at) < reach + mob.Radius)
                DamageMob(mob, damage, mob.Position - at);
        BlastBoss(at, reach, damage);
        BlastVases(at, reach);
    }

    /// <summary>Her bubbles' own rolls (hover times, pops on a bump, where a film gives way), from the level's seed.</summary>
    Rng _bubbleRng = null!;

    /// <summary>
    /// Where a homing shot would steer for this creature, or null if it cannot have it: freed, budding or clinging, struck
    /// already by this (piercing) shot, out of reach, or behind it.
    /// </summary>
    Vector2? HomingOn(PlaneShot shot, object o, Vector2 dir)
    {
        Vector2 at;
        switch (o)
        {
            case PlaneMob mob when mob.Alive && shot.Hit?.Contains(mob) != true:
                at = mob.Position;
                break;
            case Murkling m when m.Alive && !m.Budding && !m.Clinging && shot.Struck != m:
                at = m.Position;
                break;
            case Thumpsquirt t when t.Alive && !t.Airborne && shot.Struck != t:
                at = t.Position;
                break;
            case Inkwarden w when w.Alive && shot.Struck != w:
                at = w.Position;
                break;
            case Lashjaw l when l.Alive && shot.Struck != l:
                at = l.Position;
                break;
            default:
                return null;
        }
        if (Vector2.Distance(at, shot.Line) > PlaneCombatTuning.HomingReach || Vector2.Dot(at - shot.Line, dir) <= 0f) return null;
        return at;
    }

    /// <summary>Turns a unit direction toward another by at most maxRadians.</summary>
    static Vector2 Turn(Vector2 from, Vector2 to, float maxRadians)
    {
        float a = MathF.Atan2(from.X * to.Y - from.Y * to.X, Vector2.Dot(from, to));
        return Rotate(from, Math.Clamp(a, -maxRadians, maxRadians));
    }
}
