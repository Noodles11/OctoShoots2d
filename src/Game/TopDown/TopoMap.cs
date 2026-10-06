using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// A topographic map of the level, drawn once the level is generated: grayscale contours from the heightfield (the
/// terrain shader), dashed routes, arches, cave chambers, boulders, peaks with spot heights, and the places in their
/// landmark colours. Shows any window of the level: <see cref="Center"/> and <see cref="MetresAcross"/>.
/// </summary>
public partial class TopoMap : Control
{
    static readonly Color Route = new(0.38f, 0.38f, 0.38f);
    static readonly Color Spur = new(0.55f, 0.55f, 0.55f);
    static readonly Color Ink = new(0.18f, 0.18f, 0.18f);
    static readonly Color Pebble = new(0.5f, 0.5f, 0.5f);

    LevelMap? _map;
    ShaderMaterial _terrain = null!;
    readonly List<(System.Numerics.Vector2 At, float Height)> _peaks = new();
    readonly List<(System.Numerics.Vector2 At, float Height)> _soundings = new();

    /// <summary>The world point (metres) at the middle of the control.</summary>
    public System.Numerics.Vector2 Center = new(LevelMap.Size / 2f);
    /// <summary>How many metres the control's width spans.</summary>
    public float MetresAcross = LevelMap.Size;
    public System.Numerics.Vector2 Player;
    public System.Numerics.Vector2 PlayerHeading = new(0f, -1f);
    public bool ShowLegend;
    /// <summary>Dashed routes, arch symbols, area outlines (the rift's arena, cave chambers), peak crosses and spot heights — the minimap leaves them out.</summary>
    public bool ShowDetail = true;
    /// <summary>Places Clementine has been to (indices into the level's POIs): the rest show only as "?".</summary>
    public IReadOnlySet<int> Visited = new HashSet<int>();
    /// <summary>Metres between contour lines (every fifth is an index contour).</summary>
    public float ContourInterval = 2f;
    /// <summary>Only the wall edges (the swim-level coastline), no contours or shading — the minimap.</summary>
    public bool EdgesOnly;
    /// <summary>Fog of war over everything not yet seen (null: no fog).</summary>
    public FogOfWar? Fog;
    /// <summary>What she has seen, for places: a shop or treasure room shows its icon once revealed (or visited).</summary>
    public FogOfWar? Seen;
    /// <summary>Unvisited places she has come near enough to spot show as "?"; null shows every unvisited place.</summary>
    public IReadOnlySet<int>? Spotted;
    /// <summary>Clementine's arrow: size and fill.</summary>
    public float ArrowScale = 1f;
    public Color ArrowColor = Colors.White;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ClipContents = true;
        _terrain = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/topo_map.gdshader") };
        var rect = new ColorRect { Material = _terrain, ShowBehindParent = true, MouseFilter = MouseFilterEnum.Ignore };
        rect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(rect);
    }

    public void SetMap(LevelMap map)
    {
        _map = map;
        var image = Image.CreateEmpty(LevelMap.Samples, LevelMap.Samples, false, Image.Format.Rf);
        for (int y = 0; y < LevelMap.Samples; y++)
        for (int x = 0; x < LevelMap.Samples; x++)
            image.SetPixel(x, y, new Color(float.IsNaN(map.LidAt(x, y)) ? map[x, y] : map.LidAt(x, y), 0f, 0f));
        _terrain.SetShaderParameter("heights", ImageTexture.CreateFromImage(image));
        _terrain.SetShaderParameter("samples", (float)LevelMap.Samples);
        _terrain.SetShaderParameter("shore", LevelMap.BlockHeight);
        Extremes(map, _peaks, 8f, 14f, 1f);
        Extremes(map, _soundings, 6f, 18f, -1f);
        QueueRedraw();
    }

    /// <summary>
    /// Summits worth a cross (<paramref name="sign"/> +1: local maxima of at least <paramref name="min"/> m) or soundings
    /// for the deeps (−1: local minima at least that deep), inside the rim and <paramref name="spacing"/> m apart.
    /// </summary>
    static void Extremes(LevelMap map, List<(System.Numerics.Vector2 At, float Height)> into, float min, float spacing, float sign)
    {
        into.Clear();
        var candidates = new List<(System.Numerics.Vector2, float)>();
        int lo = (int)LevelMap.RimWidth + 2, hi = LevelMap.Size - lo;
        for (int y = lo; y <= hi; y++)
        for (int x = lo; x <= hi; x++)
        {
            float h = map[x, y] * sign;
            if (h < min) continue;
            bool top = true;
            for (int dy = -5; dy <= 5 && top; dy++)
            for (int dx = -5; dx <= 5 && top; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx > LevelMap.Size || ny > LevelMap.Size || (dx == 0 && dy == 0)) continue;
                float o = map[nx, ny] * sign;
                // Ties (a flat summit or floor) go to the first cell in scan order.
                if (o > h || (o == h && (dy < 0 || (dy == 0 && dx < 0)))) top = false;
            }
            if (top) candidates.Add((new System.Numerics.Vector2(x, y), h));
        }
        foreach (var (at, h) in candidates.OrderByDescending(c => c.Item2))
            if (into.All(p => System.Numerics.Vector2.Distance(p.At, at) >= spacing)) into.Add((at, h));
    }

    /// <summary>The mud cloud around a sealed boss arena (centre x, y, rim, how risen), drawn over the fog.</summary>
    public Vector4 Mud;

    float PixelsPerMetre => Size.X / MetresAcross;

    System.Numerics.Vector2 Origin => Center - new System.Numerics.Vector2(Size.X, Size.Y) * 0.5f / PixelsPerMetre;

    Vector2 P(System.Numerics.Vector2 p) => new Vector2(p.X - Origin.X, p.Y - Origin.Y) * PixelsPerMetre;

    public override void _Process(double delta)
    {
        if (_map is null || !IsVisibleInTree() || Size.X < 1f) return;
        var o = Origin;
        _terrain.SetShaderParameter("view_origin", new Vector2(o.X, o.Y));
        _terrain.SetShaderParameter("view_size", Size / PixelsPerMetre);
        _terrain.SetShaderParameter("interval", ContourInterval);
        _terrain.SetShaderParameter("edges_only", EdgesOnly);
        _terrain.SetShaderParameter("use_fog", Fog is not null);
        if (Fog is not null) _terrain.SetShaderParameter("fog", Fog.Texture);
        _terrain.SetShaderParameter("mud", Mud);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_map is null || Size.X < 1f) return;
        var map = _map;
        float ppm = PixelsPerMetre;
        var font = ThemeDB.FallbackFont;

        // Boulders and bommies as the little squares of a survey map.
        foreach (var d in map.Decor)
            if (d.Kind is DecorKind.Boulder or DecorKind.Bommie)
            {
                float s = d.Kind == DecorKind.Bommie ? 4f : 2.5f;
                DrawRect(new Rect2(P(d.Position) - Vector2.One * s * 0.5f, Vector2.One * s), Pebble);
            }

        // Routes as dashed trails; spurs and passes finer.
        foreach (var c in ShowDetail ? map.Corridors.Where(c => c.Kind != CorridorKind.Side) : Enumerable.Empty<Corridor>())
        {
            var (col, width, dash, gap) = c.Kind switch
            {
                CorridorKind.Main => (Route, 1.6f, 7f, 4f),
                CorridorKind.Pass => (Ink, 1.4f, 3f, 3f),
                _ => (Spur, 1.2f, 3f, 3f),
            };
            Dashed(c.Points, col, width, dash, gap);
        }

        // Cave chambers under the rock: dotted outlines.
        foreach (var cave in ShowDetail ? map.Caves : Enumerable.Empty<CaveSite>())
            foreach (var (center, radius) in cave.Chambers)
                DottedCircle(P(center), radius * ppm, Ink);

        // Arches: a bridge symbol across the path.
        foreach (var c in map.Canopies.Where(c => ShowDetail && c.Kind == CanopyKind.Arch))
        {
            Vector2 a = P(c.EndA), b = P(c.EndB), along = (b - a).Normalized(), side = new Vector2(-along.Y, along.X) * 4f;
            DrawLine(a + side * 0.5f, b + side * 0.5f, Ink, 1.6f, true);
            DrawLine(a - side * 0.5f, b - side * 0.5f, Ink, 1.6f, true);
            foreach (var (end, dir) in new[] { (a, -along), (b, along) })
            {
                DrawLine(end + side * 0.5f, end + side * 0.5f + (dir + side.Normalized()) * 3f, Ink, 1.4f, true);
                DrawLine(end - side * 0.5f, end - side * 0.5f + (dir - side.Normalized()) * 3f, Ink, 1.4f, true);
            }
        }

        // Peaks: a cross and the spot height.
        foreach (var (at, h) in ShowDetail ? _peaks : Enumerable.Empty<(System.Numerics.Vector2, float)>())
        {
            Vector2 p = P(at);
            DrawLine(p - new Vector2(5f, 0f), p + new Vector2(5f, 0f), Ink, 1.2f, true);
            DrawLine(p - new Vector2(0f, 5f), p + new Vector2(0f, 5f), Ink, 1.2f, true);
            DrawString(font, p + new Vector2(4f, -3f), $"{h:0}", HorizontalAlignment.Left, -1, 9, Ink);
        }

        // Soundings: the depth of the deeps, as on a sea chart.
        foreach (var (at, depth) in ShowDetail ? _soundings : Enumerable.Empty<(System.Numerics.Vector2, float)>())
            DrawString(font, P(at) + new Vector2(-6f, 4f), $"{depth:0}", HorizontalAlignment.Left, -1, 10, Pebble);

        // Places: a "?" until Clementine has been there, then a dot in their landmark colour. The shop and treasure rooms
        // show their icons ($, a trophy) as soon as the fog over them is lifted.
        for (int i = 0; i < map.Pois.Count; i++)
        {
            var poi = map.Pois[i];
            Color col = LevelView.PoiColor(poi.Kind);
            Vector2 p = P(poi.Position);
            bool revealed = Visited.Contains(i) || (Seen?.At(poi.Position) ?? 0f) > 0.5f;
            if (revealed && poi.Kind is PoiKind.Shop or PoiKind.TreasureCave)
            {
                if (poi.Kind == PoiKind.Shop) DrawShopIcon(p, font);
                else DrawTrophyIcon(p);
                continue;
            }
            if (!Visited.Contains(i))
            {
                if (Spotted is not null && !Spotted.Contains(i)) continue;
                DrawCircle(p, 7f, new Color(1f, 1f, 1f, 0.75f));
                DrawArc(p, 7f, 0f, Mathf.Tau, 24, Ink, 1.2f, true);
                DrawString(font, p + new Vector2(-4f, 5f), "?", HorizontalAlignment.Left, -1, 14, Ink);
                continue;
            }
            if (poi.Kind == PoiKind.Rift && ShowDetail)
            {
                DrawCircle(p, poi.Radius * ppm, new Color(col, 0.18f));
                DrawArc(p, poi.Radius * ppm, 0f, Mathf.Tau, 48, col, 2f, true);
            }
            float r = poi.Kind is PoiKind.Rift or PoiKind.Start ? 6f : 4.5f;
            DrawCircle(p, r + 1.2f, Ink);
            DrawCircle(p, r, col);
        }

        // Clementine: an arrow along her heading.
        if (Player != default)
        {
            Vector2 p = P(Player), f = new Vector2(PlayerHeading.X, PlayerHeading.Y).Normalized(), s = new Vector2(-f.Y, f.X);
            float k = ArrowScale;
            if (k > 1f) DrawCircle(p, 11f * k, new Color(ArrowColor, 0.22f));
            var arrow = new[] { p + f * 8f * k, p - f * 5f * k + s * 5f * k, p - f * 2f * k, p - f * 5f * k - s * 5f * k };
            DrawColoredPolygon(arrow, ArrowColor);
            DrawPolyline(arrow.Append(arrow[0]).ToArray(), Colors.Black, 1.5f * MathF.Max(k * 0.9f, 1f), true);
        }

        if (ShowLegend) DrawLegend(font);
    }

    /// <summary>The shop: a green coin with a dollar sign.</summary>
    void DrawShopIcon(Vector2 p, Font font)
    {
        DrawCircle(p, 9.5f, Ink);
        DrawCircle(p, 8f, LevelView.PoiColor(PoiKind.Shop));
        DrawArc(p, 6f, 0f, Mathf.Tau, 24, new Color(1f, 1f, 1f, 0.45f), 1f, true);
        DrawString(font, p + new Vector2(-4.5f, 5.5f), "$", HorizontalAlignment.Left, -1, 15, Ink);
    }

    /// <summary>The treasure room: a gold trophy cup on a gold disc.</summary>
    void DrawTrophyIcon(Vector2 p)
    {
        DrawCircle(p, 9.5f, Ink);
        DrawCircle(p, 8f, LevelView.PoiColor(PoiKind.TreasureCave));
        var cup = new[] { p + new Vector2(-4.2f, -4.5f), p + new Vector2(4.2f, -4.5f), p + new Vector2(2.6f, 0.5f), p + new Vector2(0.9f, 1.4f), p + new Vector2(-0.9f, 1.4f), p + new Vector2(-2.6f, 0.5f) };
        DrawColoredPolygon(cup, Ink);
        DrawArc(p + new Vector2(-4.2f, -2.4f), 1.7f, Mathf.Pi * 0.5f, Mathf.Pi * 1.5f, 8, Ink, 1.1f, true);
        DrawArc(p + new Vector2(4.2f, -2.4f), 1.7f, -Mathf.Pi * 0.5f, Mathf.Pi * 0.5f, 8, Ink, 1.1f, true);
        DrawRect(new Rect2(p + new Vector2(-0.7f, 1.2f), new Vector2(1.4f, 2.2f)), Ink);
        DrawRect(new Rect2(p + new Vector2(-2.8f, 3.3f), new Vector2(5.6f, 1.5f)), Ink);
    }

    void DrawLegend(Font font)
    {
        var rows = new (PoiKind Kind, string Name)[]
        {
            (PoiKind.Start, "Start"), (PoiKind.Rift, "The Crack"), (PoiKind.ItemSpawn, "Item cache"), (PoiKind.Shop, "Shop"),
            (PoiKind.TreasureCave, "Treasure"), (PoiKind.CurseDen, "Curse den"), (PoiKind.Secret, "Secret"), (PoiKind.Ambush, "Ambush"),
        };
        var at = new Vector2(Size.X - 124f, Size.Y - rows.Length * 16f - 14f);
        DrawRect(new Rect2(at - Vector2.One * 6f, new Vector2(118f, rows.Length * 16f + 8f)), new Color(1f, 1f, 1f, 0.8f));
        foreach (var (kind, name) in rows)
        {
            DrawCircle(at + new Vector2(5f, 5f), 5.2f, Ink);
            DrawCircle(at + new Vector2(5f, 5f), 4f, LevelView.PoiColor(kind));
            DrawString(font, at + new Vector2(16f, 10f), name, HorizontalAlignment.Left, -1, 12, Ink);
            at.Y += 16f;
        }
    }

    void Dashed(List<System.Numerics.Vector2> points, Color col, float width, float dash, float gap)
    {
        float phase = 0f;
        for (int i = 1; i < points.Count; i++)
        {
            Vector2 a = P(points[i - 1]), b = P(points[i]);
            float len = a.DistanceTo(b);
            if (len < 1e-3f) continue;
            Vector2 dir = (b - a) / len;
            float t = 0f;
            while (t < len)
            {
                float period = dash + gap, into = phase % period;
                float step = into < dash ? MathF.Min(dash - into, len - t) : MathF.Min(period - into, len - t);
                if (into < dash) DrawLine(a + dir * t, a + dir * (t + step), col, width, true);
                t += step;
                phase += step;
            }
        }
    }

    void DottedCircle(Vector2 c, float r, Color col)
    {
        int dots = Mathf.Max(8, (int)(Mathf.Tau * r / 4f));
        for (int i = 0; i < dots; i++)
        {
            float a = i * Mathf.Tau / dots;
            DrawCircle(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, 0.9f, col);
        }
    }
}
