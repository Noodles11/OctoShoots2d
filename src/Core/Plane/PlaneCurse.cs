using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Core.Plane;

/// <summary>
/// The curse den's numbers (DESIGN-TOPDOWN §4.2): a long, narrow dead-end tunnel, safe on the way in, its reward at the
/// end; reaching the end springs the curse, and the whole way back out lies in its dark. First guesses, tuned in play.
/// </summary>
public static class CurseTuning
{
    /// <summary>Sprung, the curse corrupts her this much a second while she is in the tunnel (past its first metre).</summary>
    public const float Drain = 1.5f, Inside = 1f;
    /// <summary>She has found the end once she is this close to the end chamber's centre.</summary>
    public const float EndReach = 1.6f;
    /// <summary>The fountain spouts a shell this long after the last one was taken.</summary>
    public const float FountainEvery = 1f, SpoutSpeed = 2.4f;
    /// <summary>What lies at the end: a pearl this often, else the shell fountain.</summary>
    public const float PearlChance = 0.2f;
    /// <summary>A place counts as visited once she comes this close past its edge.</summary>
    public const float VisitReach = 1f;
}

public enum CurseReward { Fountain, Pearl }

/// <summary>The level's curse den: its tunnel's line, what waits at the end, and how far its curse has gone.</summary>
public sealed class PlaneCurseDen
{
    public int Poi;
    public List<Vector2> Line = new();
    /// <summary>The distance along the line to each of its points.</summary>
    public List<float> At = new();
    public Vector2 End => Line[^1];
    public CurseReward Reward;
    /// <summary>She has reached the end: the reward is out, and the curse sprung.</summary>
    public bool Reached;
    /// <summary>The curse's dark lies over the tunnel (from reaching the end until she is out of it).</summary>
    public bool Dark;
    /// <summary>She is out again: the den is done. It never darkens again, and gives nothing more.</summary>
    public bool Cleared;
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
    /// <summary>How far into its dark she is: her depth in the tunnel while its curse is sprung, else −1.</summary>
    public float CurseDarkDepth => Curse is { Dark: true } ? CurseDepth : -1f;
    /// <summary>The places she has been to on this level (by index into the map's places).</summary>
    public bool[] Visited { get; private set; } = Array.Empty<bool>();

    void PlaceCurse()
    {
        Visited = new bool[Map.Pois.Count];
        // The adventure's curse dens are switched off.
        if (Options.Adventure) return;
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
        den.Reward = rng.NextFloat() < CurseTuning.PearlChance ? CurseReward.Pearl : CurseReward.Fountain;
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
        // Safe on the way in; sprung, the curse drains her all the way back out.
        if (den.Dark && CurseDepth > CurseTuning.Inside) Drain(CurseTuning.Drain * Dt, DamageSource.Curse);
        if (Defeated) return;

        if (!den.Reached && Vector2.Distance(p.Position, den.End) < CurseTuning.EndReach)
        {
            // The end: its reward out, and the curse sprung with a laugh — the dark closes over the whole way back.
            den.Reached = true;
            den.Dark = true;
            Events.Add(new PlaneEvent(PlaneEventType.CurseEndReached, den.End, Vector2.Zero, (float)den.Reward));
            Events.Add(new PlaneEvent(PlaneEventType.CurseLaugh, den.End, Vector2.Zero));
        }
        // Out of the tunnel again: the den is done (its dark lifts, and it never springs again).
        if (den.Dark && CurseDepth < 0f)
        {
            den.Dark = false;
            den.Cleared = true;
            Events.Add(new PlaneEvent(PlaneEventType.CurseCleared, p.Position, Vector2.Zero));
        }
        // The fountain flows while she is still in the den's dark, and is dry once the den is done.
        if (den.Reward == CurseReward.Fountain && den.Reached && !den.Cleared && (den.Spouted is null || den.Spouted.Taken))
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
}
