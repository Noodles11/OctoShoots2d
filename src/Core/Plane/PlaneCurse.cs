using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Core.Plane;

/// <summary>The curse den's numbers (DESIGN-TOPDOWN §4.2): first guesses, tuned in play.</summary>
public static class CurseTuning
{
    /// <summary>Inside the tunnel (past its first metre), the curse corrupts her this much a second.</summary>
    public const float Drain = 1.5f, Inside = 1f;
    /// <summary>She has found the end once she is this close to the end chamber's centre.</summary>
    public const float EndReach = 1.6f;
    /// <summary>The fountain spouts a shell this long after the last one was taken.</summary>
    public const float FountainEvery = 1f, SpoutSpeed = 2.4f;
    /// <summary>What lies at the end: a teleport, a pearl, a shell fountain; nothing otherwise (30%).</summary>
    public const float TeleportChance = 0.01f, PearlChance = 0.04f, FountainChance = 0.65f;
    /// <summary>A place counts as visited once she comes this close past its edge.</summary>
    public const float VisitReach = 1f;
}

public enum CurseReward { Nothing, Fountain, Pearl, Teleport }

/// <summary>The level's curse den: its tunnel's line, and what waits at the end.</summary>
public sealed class PlaneCurseDen
{
    public int Poi;
    public List<Vector2> Line = new();
    /// <summary>The distance along the line to each of its points.</summary>
    public List<float> At = new();
    public Vector2 End => Line[^1];
    public CurseReward Reward;
    /// <summary>She has reached the end (the reward is out: the laugh played, the fountain flowing, or she was sent on).</summary>
    public bool Reached;
    public float FountainClock;
    public PlaneShell? Spouted;
    public int Spouts;
    public Rng Rng = null!;
}

public sealed partial class PlaneWorld
{
    public PlaneCurseDen? Curse { get; private set; }
    /// <summary>How far into the curse den's tunnel she is (metres along it), or −1 outside it.</summary>
    public float CurseDepth { get; private set; } = -1f;
    /// <summary>The places she has been to on this level (by index into the map's places).</summary>
    public bool[] Visited { get; private set; } = Array.Empty<bool>();

    void PlaceCurse()
    {
        Visited = new bool[Map.Pois.Count];
        int pi = Map.Pois.FindIndex(p => p.Kind == PoiKind.CurseDen && p.Cave >= 0 && Map.Caves[p.Cave].Kind == CaveKind.Tunnel);
        if (pi < 0) return;
        var cave = Map.Caves[Map.Pois[pi].Cave];
        var rng = new Rng(Map.Seed ^ 0xC0125EDUL ^ ((ulong)Map.Level << 20) ^ ((ulong)Map.Depth << 28) ^ ((ulong)Map.Attempt << 44));
        var den = new PlaneCurseDen { Poi = pi, Rng = rng };
        float sum = 0f;
        foreach (var (c, _) in cave.Chambers)
        {
            if (den.Line.Count > 0) sum += Vector2.Distance(den.Line[^1], c);
            den.Line.Add(c);
            den.At.Add(sum);
        }
        float roll = rng.NextFloat();
        den.Reward = roll < CurseTuning.TeleportChance ? CurseReward.Teleport
            : roll < CurseTuning.TeleportChance + CurseTuning.PearlChance ? CurseReward.Pearl
            : roll < CurseTuning.TeleportChance + CurseTuning.PearlChance + CurseTuning.FountainChance ? CurseReward.Fountain
            : CurseReward.Nothing;
        if (den.Reward == CurseReward.Pearl)
        {
            // Any pearl she could be offered.
            var offer = PlaneRun.PortedPearls.Where(id => Run.CanOffer(id) && Pearls.All(q => q.ItemId != id)).ToList();
            if (offer.Count > 0) Pearls.Add(new PlanePearl { ItemId = Run.Pick(offer, rng), Position = den.End, Placed = true });
            else den.Reward = CurseReward.Fountain;
        }
        Curse = den;
    }

    void StepCurse()
    {
        var p = Player;
        for (int i = 0; i < Map.Pois.Count; i++)
            if (!Visited[i] && Near(Map.Pois[i], p.Position)) Visited[i] = true;

        var den = Curse;
        if (den is null) return;
        CurseDepth = DepthIn(den, p.Position);
        if (CurseDepth > CurseTuning.Inside) Drain(CurseTuning.Drain * Dt, DamageSource.Curse);
        if (Defeated) return;

        if (!den.Reached && Vector2.Distance(p.Position, den.End) < CurseTuning.EndReach)
        {
            den.Reached = true;
            Events.Add(new PlaneEvent(PlaneEventType.CurseEndReached, den.End, Vector2.Zero, (float)den.Reward));
            if (den.Reward == CurseReward.Nothing) Events.Add(new PlaneEvent(PlaneEventType.CurseLaugh, den.End, Vector2.Zero));
            else if (den.Reward == CurseReward.Teleport) Teleport(den);
        }
        if (den.Reward == CurseReward.Fountain && den.Reached && (den.Spouted is null || den.Spouted.Taken))
        {
            den.FountainClock += Dt;
            if (den.FountainClock >= CurseTuning.FountainEvery)
            {
                den.FountainClock = 0f;
                float a = den.Rng.Range(0f, MathF.Tau);
                var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
                den.Spouted = new PlaneShell { Position = den.End, Velocity = dir * CurseTuning.SpoutSpeed };
                den.Spouts++;
                Shells.Add(den.Spouted);
                Events.Add(new PlaneEvent(PlaneEventType.FountainSpout, den.End, dir));
            }
        }
    }

    /// <summary>She has been to the place: inside its clearing, or any of its cave's chambers.</summary>
    bool Near(Poi poi, Vector2 at)
    {
        if (Vector2.Distance(at, poi.Position) < poi.Radius + CurseTuning.VisitReach) return true;
        if (poi.Cave < 0) return false;
        foreach (var (c, r) in Map.Caves[poi.Cave].Chambers)
            if (Vector2.Distance(at, c) < r + CurseTuning.VisitReach) return true;
        return false;
    }

    /// <summary>How far along the tunnel the point is, or −1 when it is not in it.</summary>
    static float DepthIn(PlaneCurseDen den, Vector2 at)
    {
        if (Vector2.Distance(at, den.End) < TopDownGenerator.TunnelEndRadius + 0.6f) return den.At[^1];
        float best = -1f, nearest = TopDownGenerator.TunnelHalfWidth + 0.8f;
        for (int i = 1; i < den.Line.Count; i++)
        {
            Vector2 a = den.Line[i - 1], b = den.Line[i], ab = b - a;
            float t = Math.Clamp(Vector2.Dot(at - a, ab) / MathF.Max(ab.LengthSquared(), 1e-6f), 0f, 1f);
            float d = Vector2.Distance(at, a + ab * t);
            if (d > nearest) continue;
            nearest = d;
            best = den.At[i - 1] + ab.Length() * t;
        }
        return best;
    }

    /// <summary>
    /// Sent on: to a place she has not been to yet (never the way down, the boss or the start), else to one she has,
    /// else back to the start; just outside it, in open water.
    /// </summary>
    void Teleport(PlaneCurseDen den)
    {
        var from = Player.Position;
        var places = Enumerable.Range(0, Map.Pois.Count)
            .Where(i => i != den.Poi && Map.Pois[i].Kind is not (PoiKind.Exit or PoiKind.Start)).ToList();
        var fresh = places.Where(i => !Visited[i]).ToList();
        var pool = fresh.Count > 0 ? fresh : places;
        var poi = pool.Count > 0 ? Map.Pois[pool[den.Rng.Int(pool.Count)]] : Map.Start;
        Vector2 to = poi.Cave >= 0 ? Map.Caves[poi.Cave].Mouth + Map.Caves[poi.Cave].Facing * 2.5f : poi.Position;
        for (int i = 0; i < 24 && !Clear(to, Radius + 0.2f); i++)
        {
            float a = i * 2.4f, r = 1f + i * 0.25f;
            var q = to + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
            if (Clear(q, Radius + 0.2f)) to = q;
        }
        Player.Position = Player.PrevPosition = to;
        Player.Velocity = Vector2.Zero;
        CurseDepth = -1f;
        Events.Add(new PlaneEvent(PlaneEventType.CurseTeleport, to, from - to));
    }
}
