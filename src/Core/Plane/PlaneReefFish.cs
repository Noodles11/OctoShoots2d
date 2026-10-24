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
    public const float ChromisRadius = 0.13f, ChromisMax = 3.6f, ChromisAccel = 9f, ChromisTurnDeg = 480f;
    /// <summary>Each keeps about Spacing from its neighbours, matches those within Align, and stays near the school.</summary>
    public const float Spacing = 0.7f, Align = 1.6f;
    /// <summary>
    /// The school as a guide: on a level with a secret room she has not found, swimming into a school (within its
    /// radius plus GuideEnter) makes it gather into an arrow pointing at the secret's way in; it holds while she stays
    /// within its radius plus GuideStay, forming over FormSeconds and loosening again over LooseSeconds.
    /// </summary>
    public const float GuideEnter = 0.6f, GuideStay = 2.5f, FormSeconds = 0.8f, LooseSeconds = 1.2f;
    /// <summary>
    /// The school's centre roams from spot to spot along the channels, within SchoolRoam of home. A school spreads over
    /// SchoolSpread × √(its fish) metres (never under SchoolRadius), loose enough to part round her and close again.
    /// </summary>
    public const float SchoolSpeed = 1.1f, SchoolRoam = 30f, SchoolRadius = 1f, SchoolSpread = 0.38f;

    /// <summary>Every fish eases aside from her within Room, farther (DashRoom) when she dashes.</summary>
    public const float Room = 1.5f, DashRoom = 4f;
    /// <summary>None live this close to the level's start, so the opening stays calm.</summary>
    public const float StartClear = 20f;

    /// <summary>
    /// How many live on a level of this depth (THEME-BIBLE §7: the reef empties as she dives): plenty in the Shallows
    /// and Depth 1, thinning to a lone school and a tang by Depth 5, none in Depth 6 (internal depth = shown + 1).
    /// </summary>
    public static (int AnemoneMin, int AnemoneMax, int TangMin, int TangMax, int SchoolMin, int SchoolMax, int SizeMin, int SizeMax) ForDepth(int depth) => depth switch
    {
        // Schools are few but big, so their splitting and joining reads.
        1 => (4, 6, 4, 7, 1, 2, 45, 90),
        2 => (2, 3, 2, 4, 1, 1, 30, 60),
        3 => (0, 1, 1, 2, 0, 1, 30, 45),
        4 => (0, 1, 1, 2, 0, 1, 25, 40),
        5 => (0, 1, 0, 2, 0, 1, 20, 35),
        6 => (0, 0, 0, 1, 1, 1, 20, 30),
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
    /// <summary>How far its fish spread round the centre (grows with how many there are).</summary>
    public float Radius = ReefFishTuning.SchoolRadius;
    /// <summary>How many fish it has (their Slot runs 0 → Count − 1).</summary>
    public int Count;
    /// <summary>Guiding her to the secret: the arrow it is forming (0 a loose school → 1 the full arrow), and its heading.</summary>
    public bool Guiding;
    public float Form;
    public Vector2 Pointing = Vector2.UnitX;
    public Rng Rng = null!;
}

/// <summary>
/// Reef life: never her target, never in her way; it makes room for her and flees murklings. A murkling's throw may turn
/// it into a lesser murkling (docs/CORRUPTION.md); freed, it is a fish again, and can't be turned again that level.
/// </summary>
public sealed class PlaneReefFish : PlaneSwimmer
{
    /// <summary>Struck and turning (seconds left), a lesser murkling now (out of the reef until freed), freed once.</summary>
    public float Turning;
    public bool Corrupted, Freed;
    /// <summary>Healthy, out in the water: one a murkling may hunt (a clownfish tucked in its anemone is safe).</summary>
    public bool Huntable => !Corrupted && !Freed && Turning <= 0f && !(Kind == ReefFishKind.Clownfish && Hiding);
    /// <summary>A startled chromis darting off (seconds left), and which way.</summary>
    public float Dart;
    public Vector2 DartAway;
    public ReefFishKind Kind;
    public int Id;
    /// <summary>A clownfish's anemone, a chromis's school (indices into the world's lists); −1 for a tang.</summary>
    public int Host = -1;
    /// <summary>A clownfish tucked into its anemone; a tang's startle (seconds left).</summary>
    public bool Hiding;
    public float Startle;
    /// <summary>A darting clownfish's pace for this hop.</summary>
    public float Pace;
    /// <summary>A chromis's place in its school (its spot in the arrow).</summary>
    public int Slot;
}

public sealed partial class PlaneWorld
{
    /// <summary>The reef's background fish (docs/REEF-FISH-PROPOSAL.md), their anemones and their schools.</summary>
    public List<PlaneReefFish> ReefFish { get; } = new();
    public List<PlaneAnemone> Anemones { get; } = new();
    public List<PlaneSchool> Schools { get; } = new();

    /// <summary>The way into the level's secret room (its weak rock, else the room), and which place it is (−1: none).</summary>
    public Vector2 SecretWay { get; private set; }
    int _secret = -1;

    /// <summary>A secret she has not found yet: the schools can guide her to it.</summary>
    bool SecretHidden => _secret >= 0 && !(Visited.Length > _secret && Visited[_secret]);

    void PlaceReefFish()
    {
        _secret = Map.Pois.FindIndex(p => p.Kind == PoiKind.Secret);
        if (_secret >= 0)
        {
            var poi = Map.Pois[_secret];
            var plug = Map.WeakRocks.FirstOrDefault(w => w.Cave == poi.Cave && poi.Cave >= 0);
            SecretWay = plug.Radius > 0f ? plug.Center : poi.Position;
        }
        var rng = new Rng(Map.Seed ^ 0x2EEF15UL ^ ((ulong)Map.Level << 20) ^ ((ulong)Map.Depth << 28) ^ ((ulong)Map.Attempt << 44));
        var n = ReefFishTuning.ForDepth(Map.Depth);
        // The level's template may bring more fish (its fish rule).
        float more = Map.Template?.Rules.Fish is > 0f and var f ? f : 1f;
        int More(int count) => (int)MathF.Round(count * more);
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
        int want = More(n.AnemoneMin + rng.Int(n.AnemoneMax - n.AnemoneMin + 1));
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
        want = More(n.TangMin + rng.Int(n.TangMax - n.TangMin + 1));
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
        want = More(n.SchoolMin + rng.Int(n.SchoolMax - n.SchoolMin + 1));
        foreach (var at in channel)
        {
            if (Schools.Count >= want) break;
            if (!Allowed(at) || !Clear(at, 2f) || Schools.Any(s => Vector2.Distance(s.Home, at) < 25f)) continue;
            int size = n.SizeMin + rng.Int(n.SizeMax - n.SizeMin + 1);
            float spread = MathF.Max(ReefFishTuning.SchoolRadius, ReefFishTuning.SchoolSpread * MathF.Sqrt(size));
            Schools.Add(new PlaneSchool { Position = at, Home = at, Target = at, Wait = rng.Range(0f, 2f), Radius = spread, Count = size, Rng = new Rng(rng.NextU64()) });
            int school = Schools.Count - 1;
            for (int k = 0; k < size; k++)
            {
                float a = rng.Range(0f, MathF.Tau), r = spread * MathF.Sqrt(rng.NextFloat());
                var q = at + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
                if (!Clear(q, ReefFishTuning.ChromisRadius)) q = at;
                float heading = rng.Range(-0.4f, 0.4f);
                ReefFish.Add(new PlaneReefFish
                {
                    Kind = ReefFishKind.Chromis, Id = ids++, Host = school, Slot = k, Home = at, Position = q, Heading = heading,
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
            if (f.Corrupted) continue;
            if (f.Turning > 0f)
            {
                // Struck: it falters as the ink creeps over it, then it is a lesser murkling.
                f.Velocity = MoveToward(f.Velocity, Vector2.Zero, 4f * Dt);
                f.Position += f.Velocity * Dt;
                f.Turning -= Dt;
                if (f.Turning <= 0f) TurnFish(f);
                continue;
            }
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
        // A murkling near the anemone sends it in too, until the murkling is gone.
        bool murk = MurklingNear(host, MurkFishTuning.ClownHide);
        if (her < hide || murk) f.Hiding = true;
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
        Vector2 room = Room(f.Position, RoomNow) + Fear(f.Position);
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
        // Guiding: she is in the school and a secret is still hidden. It holds still and gathers into the arrow.
        float her = Vector2.Distance(Player.Position, s.Position);
        bool guide = SecretHidden && her < s.Radius + (s.Guiding ? ReefFishTuning.GuideStay : ReefFishTuning.GuideEnter);
        if (guide && !s.Guiding) Events.Add(new PlaneEvent(PlaneEventType.SchoolPoints, s.Position, Vector2.Normalize(SecretWay - s.Position)));
        s.Guiding = guide;
        s.Form = guide ? MathF.Min(1f, s.Form + Dt / ReefFishTuning.FormSeconds) : MathF.Max(0f, s.Form - Dt / ReefFishTuning.LooseSeconds);
        if (guide)
        {
            Vector2 to = SecretWay - s.Position;
            if (to.LengthSquared() > 1e-4f) s.Pointing = Vector2.Normalize(to);
            s.Velocity = MoveToward(s.Velocity, Vector2.Zero, 3f * Dt);
            Move(ref s.Position, ref s.Velocity, ReefFishTuning.SchoolRadius * 0.6f, report: false);
            return;
        }
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
                    if (!Clear(q, MathF.Min(s.Radius, 2f) + 0.3f) || !LineOfSight(s.Position, q)) continue;
                    s.Target = q;
                    break;
                }
            }
            else s.Velocity = MoveToward(s.Velocity, to / d * ReefFishTuning.SchoolSpeed * MathUtil.Clamp01(d / 3f + 0.3f), 1.5f * Dt);
        }
        // The centre keeps to water wide enough for the school.
        Move(ref s.Position, ref s.Velocity, ReefFishTuning.SchoolRadius * 0.6f, report: false);
    }

    /// <summary>A chromis in its school: apart from its neighbours, along with them, round the centre; aside from her.</summary>
    void StepChromis(PlaneReefFish f)
    {
        var school = Schools[f.Host];
        Vector2 apart = Vector2.Zero, along = Vector2.Zero;
        int near = 0;
        foreach (var o in ReefFish)
        {
            if (o == f || o.Kind != ReefFishKind.Chromis || o.Host != f.Host || o.Corrupted || o.Turning > 0f) continue;
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
        Vector2 cohesion = toCentre * (c > school.Radius ? 1.4f : 0.35f);
        float form = school.Form;
        // Forming the arrow, they give her less room (the shape must hold round her).
        Vector2 wish = school.Velocity + cohesion + apart * 3f + Room(f.Position, RoomNow * (1f - 0.6f * form)) * 7f + Fear(f.Position) * 7f;
        if (f.Dart > 0f)
        {
            // Startled by a throw that did not take: off and away for a moment.
            f.Dart -= Dt;
            wish += f.DartAway * ReefFishTuning.ChromisMax;
        }
        if (near > 0) wish += (along / near - f.Velocity) * 0.6f;
        if (form > 0f)
        {
            // Each to its place in the arrow, facing the way it points.
            Vector2 slot = school.Position + ArrowSlot(school, f.Slot);
            Vector2 toSlot = slot - f.Position;
            // Close-packed in the arrow (they keep apart only a little), so the shape reads.
            Vector2 arrow = toSlot * 4.5f + apart * 0.25f;
            wish = Vector2.Lerp(wish, arrow, form);
            f.IdleHeading = MathF.Atan2(school.Pointing.Y, school.Pointing.X);
        }
        // Always on the move: a school mills even when its centre rests (but an arrow holds still).
        if (wish.LengthSquared() < 0.16f && form < 0.5f) wish = f.Facing * 0.4f;
        if (wish.Length() > ReefFishTuning.ChromisMax) wish = Vector2.Normalize(wish) * ReefFishTuning.ChromisMax;
        // In the arrow, they all face along it.
        float? face = form > 0.6f && SlotDistance(f, school) < 0.5f ? MathF.Atan2(school.Pointing.Y, school.Pointing.X) : null;
        Swim(f, wish, face, ReefFishTuning.ChromisRadius, ReefFishTuning.ChromisAccel, ReefFishTuning.ChromisTurnDeg);
    }

    /// <summary>How far a chromis is from its place in the arrow.</summary>
    static float SlotDistance(PlaneReefFish f, PlaneSchool s) => Vector2.Distance(f.Position, s.Position + ArrowSlot(s, f.Slot));

    /// <summary>
    /// A fish's place in its school's arrow (from the school's centre): the head, a short chevron of two lines swept
    /// back from the tip at about 55°, takes the first 35% of the fish; the rest make the long shaft behind it, two
    /// close lanes. Sized to the school, so a bigger school makes a bigger arrow.
    /// </summary>
    public static Vector2 ArrowSlot(PlaneSchool s, int slot)
    {
        Vector2 d = s.Pointing, across = new(-d.Y, d.X);
        float r = s.Radius;
        int n = Math.Max(s.Count, 2);
        int head = Math.Max(2, (int)(n * 0.35f));
        Vector2 tip = d * (r * 1.1f);
        if (slot < head)
        {
            // Alternating sides, stepping back from the tip.
            int k = slot / 2;
            float side = slot % 2 == 0 ? 1f : -1f;
            float t = (k + 1f) / (head / 2f + 1f);
            Vector2 back = Rotate(-d, side * 0.95f);
            return tip + back * (r * 0.9f * t);
        }
        int m = slot - head, shaft = n - head;
        int lane = m % 2;
        float u = (m / 2 + 0.5f) / MathF.Max(shaft / 2f, 1f);
        return tip - d * (r * 0.15f + r * 2.4f * u) + across * (lane == 0 ? 0.16f : -0.16f);
    }
}
