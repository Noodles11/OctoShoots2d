using System;
using System.Collections.Generic;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Run;

namespace OctoShoots.Core.Plane;

/// <summary>The reef director's numbers (DESIGN-TOPDOWN §4.7); first guesses, tuned in play.</summary>
public static class ReefDirectorTuning
{
    /// <summary>The schedule is drawn this far ahead when the level is built.</summary>
    public const float Horizon = 20f * 60f;
    /// <summary>The first surge comes this long into the level; each next one this long after the last has ended.</summary>
    public const float FirstMin = 20f, FirstMax = 40f, GapMin = 25f, GapMax = 50f;
    /// <summary>A surge: it builds over Rise, holds, and eases off over Fall, Seconds in all.</summary>
    public const float SurgeSeconds = 20f, SurgeRise = 5f, SurgeFall = 5f;
    /// <summary>Its flow at full strength (m/s); her cruise is 6.</summary>
    public const float SurgeSpeedMin = 3f, SurgeSpeedMax = 4.5f;
    /// <summary>
    /// The flow pushes at full strength inside the canyon's channel, fading to nothing this far past its walls; out
    /// to CosmeticReach past them it is only seen (streaks), never felt.
    /// </summary>
    public const float ForceFeather = 2f, CosmeticReach = 5f;
}

public enum ReefEventKind { CurrentSurge }

/// <summary>Something the reef does on its own, at a set time in the level.</summary>
public sealed class ReefEvent
{
    public ReefEventKind Kind;
    /// <summary>Level time (s) it starts, and how long it lasts.</summary>
    public float Start, Duration;
    /// <summary>A surge: the canyon it runs through (an index into the map's corridors), which way, and its full flow (m/s).</summary>
    public int Corridor;
    public bool Reverse;
    public float Speed;

    public float End => Start + Duration;

    /// <summary>
    /// A surge's strength over its life, 0 → 1 → 0: it begins slow and accelerates (an eased rise), holds, and
    /// decelerates at the end (an eased fall).
    /// </summary>
    public float Envelope(float time)
    {
        float t = time - Start;
        if (t <= 0f || t >= Duration) return 0f;
        float rise = MathUtil.Clamp01(t / ReefDirectorTuning.SurgeRise);
        float fall = MathUtil.Clamp01((Duration - t) / ReefDirectorTuning.SurgeFall);
        return MathUtil.Smoothstep(rise) * MathUtil.Smoothstep(fall);
    }
}

/// <summary>
/// The reef director (DESIGN-TOPDOWN §4.7): the level acts on its own. At level build it draws a schedule of world
/// events from the run's "events" stream into a priority queue of (time, event); the sim pops them as level time
/// reaches them, whether Clementine is near or not. Deterministic: the same level always schedules the same events.
/// </summary>
public sealed class ReefDirector
{
    readonly PriorityQueue<ReefEvent, float> _queue = new();
    readonly List<ReefEvent> _active = new();
    readonly List<ReefEvent> _all = new();

    public ReefDirector(LevelMap map)
    {
        Map = map;
        var rng = new RunStreams(SeedCode.FromValue(map.Seed)).Events(map.Id);
        // Canyons a surge can run through: the main routes and the side passages (spurs are short dead ends).
        var canyons = new List<int>();
        for (int i = 0; i < map.Corridors.Count; i++)
            if (map.Corridors[i].Kind is CorridorKind.Main or CorridorKind.Side && map.Corridors[i].Points.Count >= 2) canyons.Add(i);
        if (canyons.Count == 0) return;
        float t = rng.Range(ReefDirectorTuning.FirstMin, ReefDirectorTuning.FirstMax);
        while (t < ReefDirectorTuning.Horizon)
        {
            var e = new ReefEvent
            {
                Kind = ReefEventKind.CurrentSurge,
                Start = t,
                Duration = ReefDirectorTuning.SurgeSeconds,
                Corridor = canyons[rng.Int(canyons.Count)],
                Reverse = rng.NextFloat() < 0.5f,
                Speed = rng.Range(ReefDirectorTuning.SurgeSpeedMin, ReefDirectorTuning.SurgeSpeedMax),
            };
            _queue.Enqueue(e, e.Start);
            _all.Add(e);
            t = e.End + rng.Range(ReefDirectorTuning.GapMin, ReefDirectorTuning.GapMax);
        }
    }

    public LevelMap Map { get; }

    /// <summary>Everything scheduled for the level, in order (for tests and debug views).</summary>
    public IReadOnlyList<ReefEvent> Schedule => _all;

    /// <summary>The events running now.</summary>
    public IReadOnlyList<ReefEvent> Active => _active;

    /// <summary>Adds an event to the schedule out of turn (verification: `--dbg-surge`).</summary>
    public void Inject(ReefEvent e)
    {
        _queue.Enqueue(e, e.Start);
        _all.Add(e);
    }

    /// <summary>Advances to level time <paramref name="time"/>: events whose time has come start, finished ones end.</summary>
    public void Step(float time, List<ReefEvent> started, List<ReefEvent> ended)
    {
        while (_queue.TryPeek(out var next, out float at) && at <= time)
        {
            _queue.Dequeue();
            _active.Add(next);
            started.Add(next);
        }
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            if (_active[i].End > time) continue;
            ended.Add(_active[i]);
            _active.RemoveAt(i);
        }
    }

    /// <summary>
    /// The water's flow at p from the surges running now (m/s): along the canyon's course, at the surge's strength,
    /// full inside the channel and fading out ForceFeather past its walls. Zero where none runs.
    /// </summary>
    public Vector2 FlowAt(Vector2 p, float time)
    {
        Vector2 flow = Vector2.Zero;
        foreach (var e in _active)
        {
            if (e.Kind != ReefEventKind.CurrentSurge) continue;
            float k = e.Envelope(time);
            if (k <= 0f) continue;
            var c = Map.Corridors[e.Corridor];
            Nearest(c, p, out float dist, out Vector2 along);
            float reach = 1f - MathUtil.Clamp01((dist - c.HalfWidth) / ReefDirectorTuning.ForceFeather);
            if (reach <= 0f) continue;
            flow += along * (e.Reverse ? -1f : 1f) * e.Speed * k * reach;
        }
        return flow;
    }

    /// <summary>The nearest point of a canyon's course to p: how far it is, and the course's direction there (unit).</summary>
    public static void Nearest(Corridor c, Vector2 p, out float dist, out Vector2 along)
    {
        dist = float.MaxValue;
        along = Vector2.UnitX;
        for (int i = 0; i + 1 < c.Points.Count; i++)
        {
            Vector2 a = c.Points[i], b = c.Points[i + 1];
            float d = Geo.SegmentDistance(p, a, b);
            if (d >= dist) continue;
            dist = d;
            along = Geo.Normalize(b - a, along);
        }
    }
}
