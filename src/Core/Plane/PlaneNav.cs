using System;
using System.Collections.Generic;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Core.Plane;

/// <summary>
/// The way to her through the water: a creature after her that cannot swim straight at her (rock between) goes round the
/// rock instead of pushing into it. The level's open water is a grid of Cell-metre cells (those a body can pass, kept
/// Clearance off the rock and the sealed walls), and each knows its steps to her cell, counted outward from her —
/// rebuilt as she moves (at most every RebuildTicks) and when a sealed wall opens or shuts.
/// </summary>
public static class NavTuning
{
    public const float Cell = 1f, Clearance = 0.5f;
    /// <summary>Half a body's width: the water either side of a straight line that must be open for it to swim that line.</summary>
    public const float Width = 0.45f;
    public const int RebuildTicks = 6;
    /// <summary>How many steps ahead along the way a creature looks for the farthest cell it can swim to in a straight line.</summary>
    public const int LookAhead = 14;
}

public sealed partial class PlaneWorld
{
    const int NavN = (int)(LevelMap.Size / NavTuning.Cell);
    const int NavUnreached = int.MaxValue;
    bool[]? _navOpen;
    int[]? _navSteps;
    int _navHerCell = -1, _navRockKey;
    long _navBuilt = long.MinValue;
    readonly Queue<int> _navWalk = new();

    /// <summary>
    /// The way for a creature at <paramref name="from"/> to swim toward her (a unit direction): straight at her while the
    /// water between is open, else along the way round the rock — toward the farthest cell ahead on it that it can reach in
    /// a straight line, so it rounds a corner rather than hugging every step.
    /// </summary>
    public Vector2 WayToHer(Vector2 from)
    {
        var her = Player.Position;
        if (NavLine(from, her)) return SafeNormalize(her - from);
        BuildNav();
        var steps = _navSteps!;
        int cur = NavNearest(from);
        if (cur < 0) return SafeNormalize(her - from);
        Vector2? aim = null;
        if (cur != NavCellOf(from)) aim = NavCentre(cur);
        for (int s = 0; s < NavTuning.LookAhead && steps[cur] > 0; s++)
        {
            int next = NavDownhill(cur);
            if (next < 0) break;
            cur = next;
            var c = NavCentre(cur);
            if (aim is null || NavLine(from, c)) aim = c;
        }
        if (steps[cur] == 0 && NavLine(from, her)) aim = her;
        return aim is { } a ? SafeNormalize(a - from) : SafeNormalize(her - from);
    }

    /// <summary>Whether there is a way through the water from a point to her at all.</summary>
    public bool WayExists(Vector2 from)
    {
        BuildNav();
        return NavNearest(from) >= 0;
    }

    /// <summary>
    /// Whether a body can swim straight from a to b: along the line its open cells, and a body's width of open water
    /// either side of it (a cell is open at its middle; the line may run along its edge).
    /// </summary>
    public bool NavLine(Vector2 a, Vector2 b)
    {
        BuildOpen();
        float d = Vector2.Distance(a, b);
        if (d < 1e-3f) return true;
        var side = new Vector2(-(b.Y - a.Y), b.X - a.X) / d * NavTuning.Width;
        for (float s = 0.5f; s < d; s += 0.4f)
        {
            var q = Vector2.Lerp(a, b, s / d);
            if (!NavOpenAt(q) || !Map.IsOpen(q + side) || !Map.IsOpen(q - side)) return false;
        }
        return true;
    }

    /// <summary>The terrain's open cells (once a level; the sealed walls are laid over them as they stand).</summary>
    void BuildOpen()
    {
        if (_navOpen is not null && _navRockKey == RockKey()) return;
        _navOpen ??= new bool[NavN * NavN];
        _navRockKey = RockKey();
        for (int i = 0; i < NavN * NavN; i++)
        {
            var c = NavCentre(i);
            bool open = TerrainOpen(c);
            if (open)
                foreach (var rock in _rocks)
                    if (rock.Contains(c, NavTuning.Clearance))
                    {
                        open = false;
                        break;
                    }
            _navOpen[i] = open;
        }
        // The walls changed: the way must be found again.
        _navBuilt = long.MinValue;
    }

    bool TerrainOpen(Vector2 c)
    {
        if (!Map.IsOpen(c)) return false;
        for (int k = 0; k < 8; k++)
        {
            float a = k * MathF.Tau / 8f;
            if (!Map.IsOpen(c + new Vector2(MathF.Cos(a), MathF.Sin(a)) * NavTuning.Clearance)) return false;
        }
        return true;
    }

    /// <summary>The sealed walls standing now, as a key (changes when one opens or shuts).</summary>
    int RockKey()
    {
        int key = _rocks.Count;
        foreach (var r in _rocks) key = key * 31 + (int)(r.Center.X * 10f) * 7 + (int)(r.Center.Y * 10f) + (int)(r.Radius * 10f);
        return key;
    }

    /// <summary>Each open cell's steps to her cell (8 ways round, never cutting a rock's corner), counted outward from her.</summary>
    void BuildNav()
    {
        BuildOpen();
        int herCell = NavNearest(Player.Position, seeding: true);
        // As she swims, found again at most every RebuildTicks; at once when she has jumped (a start, a dive).
        bool jumped = _navHerCell < 0 || herCell < 0 || Math.Abs(herCell % NavN - _navHerCell % NavN) + Math.Abs(herCell / NavN - _navHerCell / NavN) > 3;
        if (_navSteps is not null && (herCell == _navHerCell || Tick - _navBuilt < NavTuning.RebuildTicks && !jumped)) return;
        _navSteps ??= new int[NavN * NavN];
        Array.Fill(_navSteps, NavUnreached);
        _navHerCell = herCell;
        _navBuilt = Tick;
        if (herCell < 0) return;
        _navWalk.Clear();
        _navSteps[herCell] = 0;
        _navWalk.Enqueue(herCell);
        while (_navWalk.Count > 0)
        {
            int c = _navWalk.Dequeue();
            int x = c % NavN, y = c / NavN;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if ((dx | dy) == 0 || !NavStep(x, y, dx, dy)) continue;
                int n = (y + dy) * NavN + x + dx;
                if (_navSteps[n] != NavUnreached) continue;
                _navSteps[n] = _navSteps[c] + 1;
                _navWalk.Enqueue(n);
            }
        }
    }

    /// <summary>Whether a body can step from a cell to its neighbour (a diagonal only when both cells beside it are open).</summary>
    bool NavStep(int x, int y, int dx, int dy)
    {
        int nx = x + dx, ny = y + dy;
        if (nx < 0 || ny < 0 || nx >= NavN || ny >= NavN || !_navOpen![ny * NavN + nx]) return false;
        return dx == 0 || dy == 0 || _navOpen[y * NavN + nx] && _navOpen[ny * NavN + x];
    }

    /// <summary>The neighbour one step nearer her (−1: none).</summary>
    int NavDownhill(int c)
    {
        int x = c % NavN, y = c / NavN, best = -1, bestSteps = _navSteps![c];
        for (int dy = -1; dy <= 1; dy++)
        for (int dx = -1; dx <= 1; dx++)
        {
            if ((dx | dy) == 0 || !NavStep(x, y, dx, dy)) continue;
            int n = (y + dy) * NavN + x + dx;
            if (_navSteps[n] < bestSteps)
            {
                bestSteps = _navSteps[n];
                best = n;
            }
        }
        return best;
    }

    /// <summary>
    /// The open cell to go by for a point: its own, or (pressed against the rock) the nearest open one round it — when
    /// finding the way, the nearest that has one.
    /// </summary>
    int NavNearest(Vector2 p, bool seeding = false)
    {
        int c = NavCellOf(p);
        if (c < 0) return -1;
        bool Usable(int i) => _navOpen![i] && (seeding || _navSteps![i] != NavUnreached);
        if (Usable(c)) return c;
        int x = c % NavN, y = c / NavN, best = -1;
        float bestD = float.MaxValue;
        for (int dy = -2; dy <= 2; dy++)
        for (int dx = -2; dx <= 2; dx++)
        {
            int nx = x + dx, ny = y + dy;
            if (nx < 0 || ny < 0 || nx >= NavN || ny >= NavN) continue;
            int n = ny * NavN + nx;
            if (!Usable(n)) continue;
            float d = Vector2.DistanceSquared(NavCentre(n), p) + (seeding ? 0f : _navSteps![n] * 0.01f);
            if (d < bestD)
            {
                bestD = d;
                best = n;
            }
        }
        return best;
    }

    bool NavOpenAt(Vector2 p) => NavCellOf(p) is var c and >= 0 && _navOpen![c];

    static int NavCellOf(Vector2 p)
    {
        int x = (int)MathF.Floor(p.X / NavTuning.Cell), y = (int)MathF.Floor(p.Y / NavTuning.Cell);
        return x < 0 || y < 0 || x >= NavN || y >= NavN ? -1 : y * NavN + x;
    }

    static Vector2 NavCentre(int i) => new((i % NavN + 0.5f) * NavTuning.Cell, (i / NavN + 0.5f) * NavTuning.Cell);
}
