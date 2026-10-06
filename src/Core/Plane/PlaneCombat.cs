using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;

namespace OctoShoots.Core.Plane;

/// <summary>
/// Placeholder combat numbers for the first plane mob (a shooting dot) and Clementine's shots. Stand-ins until the
/// Depth-1 bestiary is ported onto the plane; every value here is provisional.
/// </summary>
public static class PlaneCombatTuning
{
    public const float PlayerMaxHp = 100f;
    /// <summary>Untouchable for a moment after a hit.</summary>
    public const float PlayerHurtGrace = 0.6f;

    /// <summary>
    /// Her shots are bubbles: thrown at this speed, they slow steadily (constant deceleration) to a stop about BubbleRange
    /// away and pop there, unless they hit a mob or rock first. ShotLife is only a safety net.
    /// </summary>
    public const float ShotSpeed = 17f;
    public const float BubbleRange = 11f;
    /// <summary>Below this speed a bubble has stopped: it rests in place for BubbleRest, then pops.</summary>
    public const float BubbleStop = 0.9f;
    public const float BubbleRest = 0.3f;
    /// <summary>
    /// Two of her bubbles that touch merge into one (bubbles of the same volley never do): it keeps the faster one's speed,
    /// the longer life, and the summed damage; its radius grows by BubbleGrowth of the base for every bubble merged in,
    /// so three bubbles make one twice the size. Size and damage stop growing at BubbleCap bubbles (it still merges,
    /// taking speed, heading and life); a full bubble shines like a rainbow.
    /// </summary>
    public const float BubbleGrowth = 0.5f;
    public const int BubbleCap = 15;
    public const float ShotLife = 3f;
    /// <summary>How long a mob flashes when a shot hits it.</summary>
    public const float HitFlash = 0.12f;
    public const float ShotInterval = 0.22f;
    public const float ShotDamage = 10f;
    public const float ShotRadius = 0.15f;

    public const float MobHp = 30f;
    public const float MobRadius = 0.5f;
    public const float MobSpeed = 3.2f;
    public const float MobAccel = 10f;
    /// <summary>Within this range a mob notices Clementine and follows; it loses her past MobGiveUp.</summary>
    public const float MobNotice = 14f;
    public const float MobGiveUp = 20f;
    /// <summary>It stops closing in this far from her, and shoots.</summary>
    public const float MobKeepDistance = 5f;
    public const float MobFireInterval = 1.4f;
    public const float MobShotSpeed = 8f;
    public const float MobShotLife = 2.5f;
    public const float MobShotDamage = 10f;

    /// <summary>Spawn points: at most this many, this far apart, and this far from the start.</summary>
    public const int MaxMobs = 22;
    public const float MobSpacing = 10f;
    public const float MobStartClearance = 30f;

    public const float GatewayRadius = 2.2f;

    /// <summary>An ambush springs as she enters its clearing: this many mobs, in a ring this far around her.</summary>
    public const int AmbushMin = 5, AmbushMax = 7;
    public const float AmbushRing = 5.5f;
    /// <summary>Their first shots come staggered after this long, so she has a moment to react.</summary>
    public const float AmbushFirstShot = 0.9f, AmbushShotStagger = 0.18f;

    /// <summary>A pearl is picked up within this distance of her edge.</summary>
    public const float PearlReach = 0.8f;
    /// <summary>Homing shots turn this fast toward the nearest mob within reach.</summary>
    public const float HomingDegPerSec = 240f;
    public const float HomingReach = 12f;
    /// <summary>Wave shots weave this far either side of their line, this many times a second.</summary>
    public const float WaveAmplitude = 0.35f;
    public const float WaveTurns = 3f;
}

/// <summary>A shooting dot: the one placeholder mob. Defeated mobs stay defeated (they never respawn).</summary>
public sealed class PlaneMob
{
    public int Spawn;
    public Vector2 Position;
    public Vector2 Velocity;
    public float Hp = PlaneCombatTuning.MobHp;
    public float FireTimer = PlaneCombatTuning.MobFireInterval;
    public bool Aggro;
    /// <summary>Counts down from HitFlash when it is hit (the view flashes it meanwhile).</summary>
    public float HitFlash;
    public bool Alive => Hp > 0f;
}

public sealed class PlaneShot
{
    public Vector2 Position;
    public Vector2 Velocity;
    public float Life;
    public bool FromPlayer;

    // Her shots, shaped by her pearls (ShotSpec).
    public float Damage = PlaneCombatTuning.MobShotDamage;
    public float Radius = PlaneCombatTuning.ShotRadius;
    public float Age;
    public float Traveled;
    public bool Homing, Pierce, Boomerang, Returning, Wave;
    /// <summary>Bubbles: thrown at Speed0, easing to a stop at Range (0: a plain shot at constant speed).</summary>
    public float Speed0, Range;
    /// <summary>How long a bubble has rested since it stopped.</summary>
    public float Rest;
    /// <summary>Merging: the volley it came from, its unmerged radius, and how many bubbles it is made of.</summary>
    public int Volley;
    public float BaseRadius = PlaneCombatTuning.ShotRadius;
    public int Bubbles = 1;
    public int BouncesLeft;
    public float WavePhase;
    /// <summary>Wave shots: the point on their line (Position weaves around it).</summary>
    public Vector2 Line;
    /// <summary>Queen Clam's pearls: any bubble pops one (the royal pearl takes PearlHp bubbles); the royal one homes.</summary>
    public bool BossPearl, Royal;
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
            var rng = new Rng(Map.Seed ^ 0xA3B05UL ^ ((ulong)ambush.Poi << 16) ^ ((ulong)Map.Reef << 32) ^ ((ulong)Map.Attempt << 48));
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
                        Aggro = true,
                        FireTimer = PlaneCombatTuning.AmbushFirstShot + spawned * PlaneCombatTuning.AmbushShotStagger,
                    });
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

    /// <summary>A treasure room holds one pearl she does not have yet (chosen from the room's own seed).</summary>
    void PlacePearls()
    {
        var rng = new Rng(Map.Seed * 0x9E3779B97F4A7C15UL ^ (ulong)(Map.Depth * 1000 + Map.Reef) ^ ((ulong)Map.Attempt << 40));
        foreach (var poi in Map.Pois)
        {
            if (poi.Kind != PoiKind.TreasureCave) continue;
            var offer = PlaneRun.ShotPearls.Where(id => Run.CanOffer(id) && Pearls.All(q => q.ItemId != id)).ToList();
            if (offer.Count == 0) break;
            Pearls.Add(new PlanePearl { ItemId = offer[rng.Int(offer.Count)], Position = poi.Position });
        }
    }

    /// <summary>The gateway in the rift's arena to the next room: shut until Queen Clam is freed.</summary>
    public bool GatewayOpen { get; set; } = true;
    public Vector2 GatewayPosition => Map.Rift.Position;

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
        var rng = new Rng(Map.Seed ^ 0x6D0B5UL ^ ((ulong)Map.Reef << 20) ^ ((ulong)Map.Attempt << 44));
        for (int i = extra.Count - 1; i > 0; i--)
        {
            int j = rng.Int(i + 1);
            (extra[i], extra[j]) = (extra[j], extra[i]);
        }
        spots.AddRange(extra);
        foreach (var p in spots)
        {
            if (Mobs.Count >= PlaneCombatTuning.MaxMobs) break;
            if (float.IsPositiveInfinity(LevelValidator.DistanceAt(reach, p))) continue;
            if (Vector2.Distance(p, Map.Start.Position) < PlaneCombatTuning.MobStartClearance) continue;
            if (Vector2.Distance(p, Map.Rift.Position) < Map.Rift.Radius + 4f) continue;
            if (!Clear(p, PlaneCombatTuning.MobRadius)) continue;
            if (Mobs.Any(m => Vector2.Distance(m.Position, p) < PlaneCombatTuning.MobSpacing)) continue;
            Mobs.Add(new PlaneMob { Spawn = Mobs.Count, Position = p });
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

        StepAmbushes();

        // Clementine's shots, toward her aim while fire is held, shaped by her pearls.
        if (input.Fire && p.ShotTimer <= 0f && p.Aim.LengthSquared() > 0.5f)
        {
            p.ShotTimer = PlaneCombatTuning.ShotInterval;
            Volley(p.Aim);
            Events.Add(new PlaneEvent(PlaneEventType.Shot, p.Position, p.Aim));
        }

        // Pearls: swim over one to take it.
        foreach (var pearl in Pearls)
        {
            if (pearl.Taken || Vector2.Distance(pearl.Position, p.Position) > Radius + PlaneCombatTuning.PearlReach) continue;
            pearl.Taken = true;
            float before = Run.MaxHp;
            Run.Add(pearl.ItemId);
            p.Hp = MathF.Min(p.Hp + MathF.Max(Run.MaxHp - before, 0f), Run.MaxHp);
            LastPearl = pearl.ItemId;
            Stats.PearlsFound++;
            Events.Add(new PlaneEvent(PlaneEventType.PearlCollected, pearl.Position, Vector2.Zero));
        }

        foreach (var mob in Mobs)
        {
            if (!mob.Alive) continue;
            Vector2 to = p.Position - mob.Position;
            float dist = to.Length();
            if (!mob.Aggro && dist < PlaneCombatTuning.MobNotice) mob.Aggro = true;
            else if (mob.Aggro && dist > PlaneCombatTuning.MobGiveUp) mob.Aggro = false;

            mob.HitFlash -= Dt;
            Vector2 dir = dist > 1e-4f ? to / dist : Vector2.Zero;
            Vector2 wish = mob.Aggro && dist > PlaneCombatTuning.MobKeepDistance ? dir * PlaneCombatTuning.MobSpeed : Vector2.Zero;
            mob.Velocity = MoveToward(mob.Velocity, wish, PlaneCombatTuning.MobAccel * Dt);
            Move(ref mob.Position, ref mob.Velocity, PlaneCombatTuning.MobRadius, report: false, barrier: ArenaBarrier.Outside);

            mob.FireTimer -= Dt;
            if (mob.Aggro && mob.FireTimer <= 0f && dist < PlaneCombatTuning.MobGiveUp && LineOfSight(mob.Position, p.Position) && !CrossesRim(mob.Position, p.Position))
            {
                mob.FireTimer = PlaneCombatTuning.MobFireInterval;
                Shots.Add(new PlaneShot { Position = mob.Position + dir * (PlaneCombatTuning.MobRadius + 0.2f), Velocity = dir * PlaneCombatTuning.MobShotSpeed, Life = PlaneCombatTuning.MobShotLife });
            }
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
                // Rock stops shots, and so does the sealed arena's wall.
                if (!Map.IsOpen(shot.Position) || _rocks.Any(r => Vector2.Distance(shot.Position, r.Center) < r.Radius) || CrossesRim(was, shot.Position)) shot.Life = 0f;
            }
            if (shot.Life <= 0f) continue;
            if (shot.FromPlayer)
            {
                if (BubbleHitsBoss(shot)) continue;
                // A bubble pops on one of Queen Clam's pearls, like on a mob, and pops it (the royal one takes two).
                PlaneShot? pearlHit = null;
                foreach (var other in Shots)
                    if (other.BossPearl && other.Life > 0f && Vector2.Distance(other.Position, shot.Position) < other.Radius + shot.Radius)
                    {
                        pearlHit = other;
                        break;
                    }
                if (pearlHit is not null)
                {
                    pearlHit.PearlHp -= shot.Bubbles;
                    if (pearlHit.PearlHp <= 0)
                    {
                        pearlHit.Life = 0f;
                        Events.Add(new PlaneEvent(PlaneEventType.ShotPopped, pearlHit.Position, pearlHit.Velocity, pearlHit.Radius));
                    }
                    Pop(shot);
                    continue;
                }
                foreach (var mob in Mobs)
                {
                    if (!mob.Alive || Vector2.Distance(mob.Position, shot.Position) > PlaneCombatTuning.MobRadius + shot.Radius) continue;
                    if (shot.Hit is not null && !shot.Hit.Add(mob)) continue;
                    mob.Hp -= shot.Damage;
                    mob.Aggro = true;
                    mob.HitFlash = PlaneCombatTuning.HitFlash;
                    if (!shot.Pierce && !shot.Boomerang) Pop(shot);
                    Events.Add(new PlaneEvent(mob.Alive ? PlaneEventType.MobHit : PlaneEventType.MobDefeated, mob.Position, shot.Velocity));
                    if (!mob.Alive)
                    {
                        Stats.MobsDefeated++;
                        DropShells(mob.Position);
                    }
                    if (shot.Life <= 0f) break;
                }
            }
            else if (Vector2.Distance(p.Position, shot.Position) < Radius + shot.Radius)
            {
                shot.Life = 0f;
                if (HurtPlayer(shot.Damage, shot.Velocity) && shot.Royal) p.SlowTimer = PlaneBossTuning.SlowSeconds;
            }
        }
        MergeBubbles();
        Shots.RemoveAll(s => s.Life <= 0f);

        // Out of HP: the run is over (the world stops; the game shows the death splash and starts a new run).
        if (p.Hp <= 0f)
        {
            p.Hp = 0f;
            p.Velocity = Vector2.Zero;
            Defeated = true;
            Events.Add(new PlaneEvent(PlaneEventType.PlayerDefeated, p.Position, Vector2.Zero));
        }

        Run.Hp = p.Hp;

        if (GatewayOpen && Vector2.Distance(p.Position, GatewayPosition) < PlaneCombatTuning.GatewayRadius)
            Events.Add(new PlaneEvent(PlaneEventType.GatewayEntered, GatewayPosition, Vector2.Zero));
    }

    /// <summary>She is hurt, unless dashing or still in the grace after the last hit.</summary>
    bool HurtPlayer(float damage, Vector2 dir)
    {
        var p = Player;
        if (p.DashInvulnerableTimer > 0f || p.HurtTimer > 0f || Defeated) return false;
        p.Hp -= damage;
        Stats.DamageTaken += damage;
        p.HurtTimer = PlaneCombatTuning.PlayerHurtGrace;
        Events.Add(new PlaneEvent(PlaneEventType.PlayerHit, p.Position, dir));
        return true;
    }

    /// <summary>One volley along the aim: her loadout's shot count, fan or cone, and every flag on each shot.</summary>
    void Volley(Vector2 aim)
    {
        var p = Player;
        var spec = Run.Loadout.Shot;
        int count = Math.Max(1, spec.Multishot);
        float speed = PlaneCombatTuning.ShotSpeed;
        _volleys++;
        for (int i = 0; i < count; i++)
        {
            // Wave shots all fly along the aim, weaving out of phase; the rest fan out (a cone spreads wider).
            float spread = spec.Pattern == ShotPattern.Cone ? spec.SpreadDeg * 1.6f : spec.SpreadDeg;
            Vector2 dir = spec.Wave || count == 1 ? aim : Rotate(aim, (i - (count - 1) * 0.5f) * spread * MathUtil.Deg2Rad);
            Vector2 at = p.Position + dir * (Radius + 0.2f);
            var shot = new PlaneShot
            {
                Position = at,
                Line = at,
                Velocity = dir * speed,
                Speed0 = speed,
                Range = PlaneCombatTuning.BubbleRange,
                Life = PlaneCombatTuning.ShotLife,
                FromPlayer = true,
                Damage = PlaneCombatTuning.ShotDamage * spec.DamageMult,
                Radius = PlaneCombatTuning.ShotRadius * spec.SizeMult,
                BaseRadius = PlaneCombatTuning.ShotRadius * spec.SizeMult,
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
        }
    }

    int _volleys;

    /// <summary>
    /// Her bubbles that touch merge (not two of one volley, which leave her side by side): the faster one carries on, with
    /// the longer life, the summed damage, and a radius grown by BubbleGrowth per bubble merged in.
    /// </summary>
    void MergeBubbles()
    {
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
                // Past the cap only speed, heading and life carry over.
                int added = Math.Min(gone.Bubbles, PlaneCombatTuning.BubbleCap - keep.Bubbles);
                if (added < 0) added = 0;
                keep.Damage += gone.Damage * added / gone.Bubbles;
                keep.Bubbles += added;
                keep.BaseRadius = MathF.Max(keep.BaseRadius, gone.BaseRadius);
                keep.Radius = keep.BaseRadius * (1f + PlaneCombatTuning.BubbleGrowth * (keep.Bubbles - 1));
                // Absorbed, no pop.
                gone.Life = -1000f;
                if (gone == a) break;
            }
        }
    }

    static Vector2 Rotate(Vector2 v, float a) => new(v.X * MathF.Cos(a) - v.Y * MathF.Sin(a), v.X * MathF.Sin(a) + v.Y * MathF.Cos(a));

    /// <summary>Moves one of her shots: boomerang return, homing turn, wave weave, bounces off rock.</summary>
    void StepHerShot(PlaneShot shot)
    {
        var p = Player;
        shot.Age += Dt;
        shot.Life -= Dt;
        if (shot.Life <= 0f)
        {
            Pop(shot);
            return;
        }
        float speed = shot.Velocity.Length();
        Vector2 dir = speed > 1e-4f ? shot.Velocity / speed : p.Aim;
        float range = shot.Range > 0f ? shot.Range : PlaneCombatTuning.BubbleRange;
        // A bubble eases out: constant deceleration, so v² falls linearly with the distance flown, reaching 0 at its range.
        if (shot.Range > 0f && !shot.Boomerang)
        {
            speed = shot.Speed0 * MathF.Sqrt(MathF.Max(0f, 1f - shot.Traveled / shot.Range));
            if (speed < PlaneCombatTuning.BubbleStop)
            {
                // Stopped: it hangs still a moment, then pops.
                shot.Velocity = Vector2.Zero;
                shot.Rest += Dt;
                if (shot.Rest >= PlaneCombatTuning.BubbleRest) Pop(shot);
                return;
            }
        }

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
            PlaneMob? target = null;
            float best = PlaneCombatTuning.HomingReach;
            foreach (var mob in Mobs)
            {
                if (!mob.Alive || shot.Hit?.Contains(mob) == true) continue;
                float d = Vector2.Distance(mob.Position, shot.Line);
                if (d < best && Vector2.Dot(mob.Position - shot.Line, dir) > 0f)
                {
                    best = d;
                    target = mob;
                }
            }
            if (target is not null) dir = Turn(dir, Vector2.Normalize(target.Position - shot.Line), PlaneCombatTuning.HomingDegPerSec * MathUtil.Deg2Rad * Dt);
        }
        shot.Velocity = dir * speed;

        Vector2 next = shot.Line + shot.Velocity * Dt;
        bool rim = CrossesRim(shot.Line, next);
        if (rim || !Map.IsOpen(next) || _rocks.Any(r => Vector2.Distance(next, r.Center) < r.Radius))
        {
            if (rim)
            {
                Pop(shot);
                return;
            }
            if (shot.BouncesLeft > 0)
            {
                // Off the rock face: reflect about the slope's normal (uphill = into the wall).
                Vector2 n = Map.Gradient(next);
                n = n.LengthSquared() > 1e-6f ? Vector2.Normalize(n) : dir;
                if (Vector2.Dot(shot.Velocity, n) > 0f) shot.Velocity -= 2f * Vector2.Dot(shot.Velocity, n) * n;
                else shot.Velocity = -shot.Velocity;
                shot.BouncesLeft--;
                return;
            }
            // A returning boomerang passes back over what it cleared; anything else pops on the rock.
            if (!(shot.Boomerang && shot.Returning))
            {
                Pop(shot);
                return;
            }
        }
        shot.Traveled += Vector2.Distance(shot.Line, next);
        shot.Line = next;
        shot.Position = next;
        if (shot.Wave)
        {
            Vector2 side = new(-dir.Y, dir.X);
            shot.Position += side * (MathF.Sin(shot.WavePhase + shot.Age * PlaneCombatTuning.WaveTurns * MathF.Tau) * PlaneCombatTuning.WaveAmplitude);
        }
    }

    /// <summary>A bubble bursts where it is (on a mob, on rock, or where it came to rest).</summary>
    void Pop(PlaneShot shot)
    {
        if (shot.Life < -100f) return;
        shot.Life = -1000f;
        Events.Add(new PlaneEvent(PlaneEventType.ShotPopped, shot.Position, shot.Velocity, shot.Radius));
    }

    /// <summary>Turns a unit direction toward another by at most maxRadians.</summary>
    static Vector2 Turn(Vector2 from, Vector2 to, float maxRadians)
    {
        float a = MathF.Atan2(from.X * to.Y - from.Y * to.X, Vector2.Dot(from, to));
        return Rotate(from, Math.Clamp(a, -maxRadians, maxRadians));
    }
}
