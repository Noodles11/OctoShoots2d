using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Core.Plane;

/// <summary>The reef fish's numbers (docs/REEF-FISH-PROPOSAL.md): first guesses, tuned in play.</summary>
public static class ReefFishTuning
{
    /// <summary>Clownfish: a pair or trio to an anemone, darting out at most ClownRange from it and back.</summary>
    public const float ClownRadius = 0.22f, ClownSpeed = 1.1f, ClownDart = 2.6f, ClownAccel = 7f, ClownTurnDeg = 320f, ClownRange = 1.6f;
    /// <summary>She this close (DashRoom when dashing) sends a clownfish into its anemone; it stays until she is ClownSafe off.</summary>
    public const float ClownHide = 2.5f, ClownSafe = 3.5f;

    /// <summary>Blue tang: hops 6–14 m about its home (within TangHomeRange), grazing 1.5–4 s at each spot.</summary>
    public const float TangRadius = 0.32f, TangSpeed = 1.6f, TangAccel = 3.5f, TangTurnDeg = 160f, TangHomeRange = 25f, TangHopMin = 6f, TangHopMax = 14f;
    /// <summary>Startled, a tang veers off at this many times its speed for this long.</summary>
    public const float TangStartleSpeed = 1.9f, TangStartleSeconds = 1.2f;

    /// <summary>Chromis: schooling boids about a slowly roaming centre.</summary>
    public const float ChromisRadius = 0.1f, ChromisMax = 3.6f, ChromisAccel = 9f, ChromisTurnDeg = 480f;
    /// <summary>Each keeps about Spacing from its neighbours, matches those within Align, and stays near the school.</summary>
    public const float Spacing = 0.6f, Align = 1.5f;
    /// <summary>The school's centre roams from spot to spot along the channels, within SchoolRoam of home.</summary>
    public const float SchoolSpeed = 1.1f, SchoolRoam = 30f, SchoolRadius = 1f;

    /// <summary>Every fish eases aside from her within Room, farther (DashRoom) when she dashes.</summary>
    public const float Room = 1.5f, DashRoom = 4f;
    /// <summary>None live this close to the level's start, so the opening stays calm.</summary>
    public const float StartClear = 20f;

    /// <summary>How many live on a level of this depth (THEME-BIBLE §7: the reef empties as she dives).</summary>
    public static (int AnemoneMin, int AnemoneMax, int TangMin, int TangMax, int SchoolMin, int SchoolMax, int SizeMin, int SizeMax) ForDepth(int depth) => depth switch
    {
        1 => (4, 6, 4, 7, 3, 5, 15, 30),
        2 => (2, 3, 2, 4, 2, 3, 10, 20),
        3 => (0, 1, 1, 2, 1, 1, 10, 15),
        _ => (0, 0, 0, 0, 0, 0, 0, 0),
    };
}

public enum ReefFishKind { Clownfish, BlueTang, Chromis }

/// <summary>A clownfish's host: an anemone on the seabed by the rock.</summary>
public sealed class PlaneAnemone
{
    public Vector2 Position;
    public float Scale = 1f;
}

/// <summary>A school's centre: it roams the channels, and its fish swim round it.</summary>
public sealed class PlaneSchool
{
    public Vector2 Position, Velocity, Home, Target;
    public float Wait;
    public Rng Rng = null!;
}

/// <summary>Background reef life: never a target, never in the way. It only makes room for her.</summary>
public sealed class PlaneReefFish : PlaneSwimmer
{
    public ReefFishKind Kind;
    public int Id;
    /// <summary>A clownfish's anemone, a chromis's school (indices into the world's lists); −1 for a tang.</summary>
    public int Host = -1;
    /// <summary>A clownfish tucked into its anemone; a tang's startle (seconds left).</summary>
    public bool Hiding;
    public float Startle;
    /// <summary>A darting clownfish's pace for this hop.</summary>
    public float Pace;
}

public sealed partial class PlaneWorld
{
    /// <summary>The reef's background fish (docs/REEF-FISH-PROPOSAL.md), their anemones and their schools.</summary>
    public List<PlaneReefFish> ReefFish { get; } = new();
    public List<PlaneAnemone> Anemones { get; } = new();
    public List<PlaneSchool> Schools { get; } = new();

    void PlaceReefFish()
    {
        var rng = new Rng(Map.Seed ^ 0x2EEF15UL ^ ((ulong)Map.Level << 20) ^ ((ulong)Map.Depth << 28) ^ ((ulong)Map.Attempt << 44));
        var n = ReefFishTuning.ForDepth(Map.Depth);
        int ids = 0;
        var exit = Map.Exit;
        bool Allowed(Vector2 q) => Vector2.Distance(q, Map.Start.Position) >= ReefFishTuning.StartClear
            && Vector2.Distance(q, exit.Position) >= exit.Radius + 4f;

        var anchors = Map.Plazas.Select(z => z.Center).Concat(Map.Spawns.Where(s => s.Role != SpawnRole.Ambush).Select(s => s.Position)).ToList();
        var channel = new List<Vector2>();
        foreach (var c in Map.Corridors.Where(c => c.Kind is CorridorKind.Main or CorridorKind.Side))
            for (int i = 4; i < c.Points.Count - 4; i += 7) channel.Add(c.Points[i]);
        anchors.AddRange(channel);
        Shuffle(anchors, rng);
        Shuffle(channel, rng);

        // Anemones on the seabed at the foot of the rock, a pair or trio of clownfish in each.
        int want = n.AnemoneMin + rng.Int(n.AnemoneMax - n.AnemoneMin + 1);
        foreach (var anchor in anchors)
        {
            if (Anemones.Count >= want) break;
            for (int t = 0; t < 12; t++)
            {
                float a = rng.Range(0f, MathF.Tau), d = rng.Range(2f, 7f);
                var q = anchor + new Vector2(MathF.Cos(a), MathF.Sin(a)) * d;
                // Open round it, with rock close by.
                if (!Allowed(q) || !Clear(q, ReefFishTuning.ClownRange) || Clear(q, ReefFishTuning.ClownRange + 2.5f)) continue;
                if (Anemones.Any(o => Vector2.Distance(o.Position, q) < 12f)) continue;
                Anemones.Add(new PlaneAnemone { Position = q, Scale = rng.Range(0.95f, 1.3f) });
                int host = Anemones.Count - 1, size = 2 + rng.Int(2);
                for (int k = 0; k < size; k++)
                    ReefFish.Add(new PlaneReefFish
                    {
                        Kind = ReefFishKind.Clownfish, Id = ids++, Host = host, Home = q,
                        Position = q + new Vector2(rng.Range(-0.5f, 0.5f), rng.Range(-0.5f, 0.5f)),
                        Heading = rng.Range(0f, MathF.Tau), Wait = rng.Range(0f, 2f), Rng = new Rng(rng.NextU64()),
                    });
                break;
            }
        }

        // Blue tangs, alone or in pairs.
        want = n.TangMin + rng.Int(n.TangMax - n.TangMin + 1);
        var groups = new List<Vector2>();
        int tangs = 0;
        foreach (var anchor in anchors)
        {
            if (tangs >= want) break;
            if (!Allowed(anchor) || !Clear(anchor, ReefFishTuning.TangRadius + 0.6f) || groups.Any(g => Vector2.Distance(g, anchor) < 12f)) continue;
            groups.Add(anchor);
            int size = Math.Min(1 + rng.Int(2), want - tangs);
            for (int k = 0; k < size; k++, tangs++)
            {
                var at = anchor + new Vector2(rng.Range(-1.5f, 1.5f), rng.Range(-1.5f, 1.5f));
                if (!Clear(at, ReefFishTuning.TangRadius + 0.1f)) at = anchor;
                ReefFish.Add(new PlaneReefFish
                {
                    Kind = ReefFishKind.BlueTang, Id = ids++, Home = anchor, Position = at,
                    Heading = rng.Range(0f, MathF.Tau), Wait = rng.Range(0f, 3f), Rng = new Rng(rng.NextU64()),
                });
            }
        }

        // Chromis schools over the channels.
        want = n.SchoolMin + rng.Int(n.SchoolMax - n.SchoolMin + 1);
        foreach (var at in channel)
        {
            if (Schools.Count >= want) break;
            if (!Allowed(at) || !Clear(at, ReefFishTuning.SchoolRadius + 0.5f) || Schools.Any(s => Vector2.Distance(s.Home, at) < 25f)) continue;
            Schools.Add(new PlaneSchool { Position = at, Home = at, Target = at, Wait = rng.Range(0f, 2f), Rng = new Rng(rng.NextU64()) });
            int school = Schools.Count - 1, size = n.SizeMin + rng.Int(n.SizeMax - n.SizeMin + 1);
            for (int k = 0; k < size; k++)
            {
                float a = rng.Range(0f, MathF.Tau), r = ReefFishTuning.SchoolRadius * MathF.Sqrt(rng.NextFloat());
                var q = at + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
                if (!Clear(q, ReefFishTuning.ChromisRadius)) q = at;
                float heading = rng.Range(-0.4f, 0.4f);
                ReefFish.Add(new PlaneReefFish
                {
                    Kind = ReefFishKind.Chromis, Id = ids++, Host = school, Home = at, Position = q, Heading = heading,
                    Velocity = new Vector2(MathF.Cos(heading), MathF.Sin(heading)) * 0.5f, Rng = new Rng(rng.NextU64()),
                });
            }
        }
    }

    static void Shuffle<T>(List<T> list, Rng rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Int(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    /// <summary>How far she makes them give way right now: farther while she dashes.</summary>
    float RoomNow => Player.DashTimer > 0f ? ReefFishTuning.DashRoom : ReefFishTuning.Room;

    void StepReefFish()
    {
        foreach (var s in Schools) StepSchool(s);
        foreach (var f in ReefFish)
        {
            switch (f.Kind)
            {
                case ReefFishKind.Clownfish: StepClownfish(f); break;
                case ReefFishKind.BlueTang: StepTang(f); break;
                default: StepChromis(f); break;
            }
        }
    }

    /// <summary>The push away from her: none past the reach, growing to full right beside her.</summary>
    Vector2 Room(Vector2 at, float reach)
    {
        Vector2 away = at - Player.Position;
        float d = away.Length();
        if (d >= reach) return Vector2.Zero;
        Vector2 dir = d > 1e-3f ? away / d : Vector2.UnitX;
        return dir * (1f - d / reach);
    }

    /// <summary>Darts out from its anemone and back, nestles in; she comes close and it tucks in until she is gone.</summary>
    void StepClownfish(PlaneReefFish f)
    {
        var host = Anemones[f.Host].Position;
        float her = Vector2.Distance(Player.Position, host);
        float hide = Player.DashTimer > 0f ? ReefFishTuning.DashRoom : ReefFishTuning.ClownHide;
        if (her < hide) f.Hiding = true;
        else if (f.Hiding && her > ReefFishTuning.ClownSafe)
        {
            f.Hiding = false;
            f.Wait = f.Rng.Range(0.8f, 2f);
        }
        float max = ReefFishTuning.ClownTurnDeg;
        if (f.Hiding)
        {
            Vector2 to = host - f.Position;
            float d = to.Length();
            Vector2 wish = d > 0.15f ? to / d * ReefFishTuning.ClownDart * MathUtil.Clamp01(d / 0.6f + 0.2f) : Vector2.Zero;
            Swim(f, wish, null, ReefFishTuning.ClownRadius, ReefFishTuning.ClownAccel, max);
            return;
        }
        if (f.Wait > 0f)
        {
            f.Wait -= Dt;
            Swim(f, Room(f.Position, RoomNow) * ReefFishTuning.ClownDart, null, ReefFishTuning.ClownRadius, ReefFishTuning.ClownAccel, max);
            return;
        }
        float dist = Vector2.Distance(f.Position, f.Target);
        if (f.Target == default || dist < 0.2f)
        {
            // Back home to nestle every other hop or so; otherwise out to a spot near it.
            f.Wait = f.Rng.Range(0.4f, 2.4f);
            f.IdleHeading = f.Heading + f.Rng.Range(-1.5f, 1.5f);
            f.Pace = f.Rng.NextFloat() < 0.35f ? ReefFishTuning.ClownDart : ReefFishTuning.ClownSpeed;
            f.Target = host;
            if (f.Rng.NextFloat() < 0.6f)
                for (int i = 0; i < 6; i++)
                {
                    float a = f.Rng.Range(0f, MathF.Tau), r = f.Rng.Range(0.5f, ReefFishTuning.ClownRange);
                    var q = host + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
                    if (!Clear(q, ReefFishTuning.ClownRadius)) continue;
                    f.Target = q;
                    break;
                }
            return;
        }
        Vector2 dir = (f.Target - f.Position) / dist;
        Swim(f, dir * f.Pace * MathUtil.Clamp01(dist / 0.8f + 0.3f) + Room(f.Position, RoomNow) * ReefFishTuning.ClownDart, null,
            ReefFishTuning.ClownRadius, ReefFishTuning.ClownAccel, max);
    }

    /// <summary>Hops about its home, grazing at each spot; she comes close and it veers off, then carries on.</summary>
    void StepTang(PlaneReefFish f)
    {
        const float max = ReefFishTuning.TangTurnDeg;
        Vector2 room = Room(f.Position, RoomNow);
        if (room != Vector2.Zero && f.Startle <= 0f)
        {
            // Off and away from her: a new spot on her far side.
            f.Startle = ReefFishTuning.TangStartleSeconds;
            f.Wait = 0f;
            f.Target = PickTangSpot(f, Vector2.Normalize(room));
        }
        f.Startle = MathF.Max(0f, f.Startle - Dt);
        float speed = ReefFishTuning.TangSpeed * (f.Startle > 0f ? ReefFishTuning.TangStartleSpeed : 1f);
        if (f.Wait > 0f)
        {
            f.Wait -= Dt;
            Swim(f, room * speed, null, ReefFishTuning.TangRadius, ReefFishTuning.TangAccel, max);
            return;
        }
        float dist = Vector2.Distance(f.Position, f.Target);
        if (f.Target == default || dist < 0.8f)
        {
            f.Wait = f.Rng.Range(1.5f, 4f);
            f.IdleHeading = f.Heading + f.Rng.Range(-1f, 1f);
            f.Target = PickTangSpot(f, null);
            return;
        }
        Vector2 dir = (f.Target - f.Position) / dist;
        Swim(f, dir * speed * MathUtil.Clamp01(dist / 2f + 0.3f) + room * speed, null, ReefFishTuning.TangRadius, ReefFishTuning.TangAccel, max);
    }

    /// <summary>A tang's next spot: a hop off, near home, in open water it can see; toward <paramref name="bias"/> if given.</summary>
    Vector2 PickTangSpot(PlaneReefFish f, Vector2? bias)
    {
        for (int i = 0; i < 12; i++)
        {
            float a = bias is { } b ? MathF.Atan2(b.Y, b.X) + f.Rng.Range(-0.9f, 0.9f) : f.Rng.Range(0f, MathF.Tau);
            float d = bias is null ? f.Rng.Range(ReefFishTuning.TangHopMin, ReefFishTuning.TangHopMax) : f.Rng.Range(3f, 6f);
            var q = f.Position + new Vector2(MathF.Cos(a), MathF.Sin(a)) * d;
            if (bias is null && Vector2.Distance(q, f.Home) > ReefFishTuning.TangHomeRange) continue;
            if (!Clear(q, ReefFishTuning.TangRadius + 0.3f) || !LineOfSight(f.Position, q)) continue;
            return q;
        }
        return f.Position;
    }

    /// <summary>A school's centre: roams from spot to spot along the channels near home, lingering at each.</summary>
    void StepSchool(PlaneSchool s)
    {
        if (s.Wait > 0f)
        {
            s.Wait -= Dt;
            s.Velocity = MoveToward(s.Velocity, Vector2.Zero, 1.5f * Dt);
        }
        else
        {
            Vector2 to = s.Target - s.Position;
            float d = to.Length();
            if (d < 1f)
            {
                s.Wait = s.Rng.Range(1f, 4f);
                for (int i = 0; i < 10; i++)
                {
                    float a = s.Rng.Range(0f, MathF.Tau), r = s.Rng.Range(6f, 14f);
                    var q = s.Position + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
                    if (Vector2.Distance(q, s.Home) > ReefFishTuning.SchoolRoam) continue;
                    if (!Clear(q, ReefFishTuning.SchoolRadius + 0.3f) || !LineOfSight(s.Position, q)) continue;
                    s.Target = q;
                    break;
                }
            }
            else s.Velocity = MoveToward(s.Velocity, to / d * ReefFishTuning.SchoolSpeed * MathUtil.Clamp01(d / 3f + 0.3f), 1.5f * Dt);
        }
        // The centre keeps to water wide enough for the school.
        Move(ref s.Position, ref s.Velocity, ReefFishTuning.SchoolRadius * 0.6f, report: false, barrier: ArenaBarrier.Outside);
    }

    /// <summary>A chromis in its school: apart from its neighbours, along with them, round the centre; aside from her.</summary>
    void StepChromis(PlaneReefFish f)
    {
        var school = Schools[f.Host];
        Vector2 apart = Vector2.Zero, along = Vector2.Zero;
        int near = 0;
        foreach (var o in ReefFish)
        {
            if (o == f || o.Kind != ReefFishKind.Chromis || o.Host != f.Host) continue;
            Vector2 d = f.Position - o.Position;
            float l2 = d.LengthSquared();
            if (l2 > ReefFishTuning.Align * ReefFishTuning.Align) continue;
            float l = MathF.Sqrt(l2);
            if (l < ReefFishTuning.Spacing) apart += (l > 1e-3f ? d / l : new Vector2(MathF.Cos(f.Id), MathF.Sin(f.Id))) * (1f - l / ReefFishTuning.Spacing);
            along += o.Velocity;
            near++;
        }
        Vector2 toCentre = school.Position - f.Position;
        float c = toCentre.Length();
        // Loose inside the school, pulled in firmly from outside it.
        Vector2 cohesion = toCentre * (c > ReefFishTuning.SchoolRadius ? 1.4f : 0.35f);
        Vector2 wish = school.Velocity + cohesion + apart * 3f + Room(f.Position, RoomNow) * 7f;
        if (near > 0) wish += (along / near - f.Velocity) * 0.6f;
        // Always on the move: a school mills even when its centre rests.
        if (wish.LengthSquared() < 0.16f) wish = f.Facing * 0.4f;
        if (wish.Length() > ReefFishTuning.ChromisMax) wish = Vector2.Normalize(wish) * ReefFishTuning.ChromisMax;
        Swim(f, wish, null, ReefFishTuning.ChromisRadius, ReefFishTuning.ChromisAccel, ReefFishTuning.ChromisTurnDeg);
    }
}
