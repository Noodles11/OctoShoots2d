using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// A topographic map of the level, drawn once the level is generated: grayscale contours from the heightfield (the
/// terrain shader), dashed routes, arches, cave chambers, bommies, peaks with spot heights, and the places by their
/// emblems. Shows any window of the level: <see cref="Center"/> and <see cref="MetresAcross"/>.
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
    /// <summary>
    /// The Tab map: cut out of its square along the level's outline (its water and the rock just round it, seen or not;
    /// soft edges, over a dimming of the game), framed to fit that outline (<see cref="Frame"/>), its title (Title,
    /// Subtitle) over it.
    /// </summary>
    public bool Cutout;
    public string Title = "", Subtitle = "";
    /// <summary>With the legend (the Tab map): where the run stands, and its clock, at the top left.</summary>
    public string Caption = "", Clock = "";
    /// <summary>Dashed routes, arch symbols, area outlines (the rift's arena, cave chambers), peak crosses and spot heights — the minimap leaves them out.</summary>
    public bool ShowDetail = true;
    /// <summary>Places Clementine has been to (indices into the level's POIs): only these show.</summary>
    public IReadOnlySet<int> Visited = new HashSet<int>();
    /// <summary>Brass Compass: every place shows, visited or not, except secret rooms (found, never advertised).</summary>
    public bool Landmarks;
    /// <summary>Metres between contour lines (every fifth is an index contour).</summary>
    public float ContourInterval = 2f;
    /// <summary>Only the wall edges (the swim-level coastline), no contours or shading — the minimap.</summary>
    public bool EdgesOnly;
    /// <summary>Fog of war over everything not yet seen (null: no fog).</summary>
    public FogOfWar? Fog;
    /// <summary>What she has seen (the minimap's own fog).</summary>
    public FogOfWar? Seen;
    /// <summary>Light springs (docs/CORRUPTION.md), and whether she is bathing in one.</summary>
    public readonly List<(System.Numerics.Vector2 At, bool Bathing)> Springs = new();
    /// <summary>The places' emblems' size (the Tab map draws them larger than the minimap).</summary>
    public float EmblemScale = 1f;
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

    /// <summary>
    /// The level's chart. Secret rooms still sealed (<paramref name="sealedCaves"/>) are drawn as wall, their way in
    /// with them, so the map gives them away no more than the reef does.
    /// </summary>
    public void SetMap(LevelMap map, IEnumerable<int>? sealedCaves = null)
    {
        _map = map;
        const int N = LevelMap.Samples;
        // The outline's box: all its open water.
        var lo = new System.Numerics.Vector2(float.MaxValue);
        var hi = new System.Numerics.Vector2(float.MinValue);
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
            if (map[x, y] <= LevelMap.BlockHeight)
            {
                lo = System.Numerics.Vector2.Min(lo, new System.Numerics.Vector2(x, y));
                hi = System.Numerics.Vector2.Max(hi, new System.Numerics.Vector2(x, y));
            }
        _outline = lo.X <= hi.X ? (lo, hi) : (System.Numerics.Vector2.Zero, new System.Numerics.Vector2(LevelMap.Size));
        var shown = new float[N * N];
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
            shown[y * N + x] = float.IsNaN(map.LidAt(x, y)) ? map[x, y] : map.LidAt(x, y);
        if (sealedCaves is not null)
            foreach (int c in sealedCaves)
            {
                if (c < 0 || c >= map.Caves.Count) continue;
                var cave = map.Caves[c];
                var plug = map.WeakRocks.Find(r => r.Cave == c);
                for (int y = 0; y < LevelMap.Samples; y++)
                for (int x = 0; x < LevelMap.Samples; x++)
                {
                    var p = new System.Numerics.Vector2(x, y);
                    bool hidden = plug.Radius > 0f && System.Numerics.Vector2.Distance(p, plug.Center) < plug.Radius + 1.5f
                        || System.Numerics.Vector2.Distance(p, cave.Mouth) < 3.5f;
                    foreach (var (centre, radius) in cave.Chambers)
                        hidden |= System.Numerics.Vector2.Distance(p, centre) < radius + 2.5f;
                    if (cave.Chambers.Count > 0) hidden |= Geo.SegmentDistance(p, cave.Mouth, cave.Chambers[0].Center) < 3.5f;
                    for (int i = 1; i < cave.Chambers.Count; i++) hidden |= Geo.SegmentDistance(p, cave.Chambers[i - 1].Center, cave.Chambers[i].Center) < 3.5f;
                    if (hidden) shown[y * N + x] = Mathf.Max(shown[y * N + x], LevelMap.BlockHeight + 3f);
                }
            }
        // The chart's channels: the height, and how far into the rock and into the water each point lies (the hatching
        // band and the walls' shadow).
        var water = shown.Select(h => h <= LevelMap.BlockHeight).ToArray();
        var intoRock = CaveLayout.Chamfer(water, true);
        var intoWater = CaveLayout.Chamfer(water, false);
        var image = Image.CreateEmpty(N, N, false, Image.Format.Rgbf);
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            int i = y * N + x;
            image.SetPixel(x, y, new Color(shown[i], Mathf.Min(intoRock[i], 64f), Mathf.Min(intoWater[i], 64f)));
        }
        // The depth's own water, and its reef rock in a stone tone (never paper white).
        var look = ReefLook.For(map.Depth);
        _terrain.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("water_col"), look.WaterShallow);
        _terrain.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("deep_col"), look.Murk);
        _terrain.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("rock_col"), RockTone(look));
        _terrain.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("heights"), ImageTexture.CreateFromImage(image));
        _terrain.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("samples"), (float)LevelMap.Samples);
        _terrain.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("shore"), LevelMap.BlockHeight);
        Extremes(map, _peaks, 8f, 14f, 1f);
        Extremes(map, _soundings, 6f, 18f, -1f);
        QueueRedraw();
    }

    /// <summary>The chart's rock: the depth's reef rock, greyed toward stone and a shade darker, so the walls read as rock.</summary>
    public static Color RockTone(ReefLook look) => look.Rock.Lerp(new Color(0.5f, 0.5f, 0.48f), 0.45f) * new Color(0.9f, 0.9f, 0.9f, 1f);

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

    float PixelsPerMetre => Size.X / MetresAcross;

    System.Numerics.Vector2 Origin => Center - new System.Numerics.Vector2(Size.X, Size.Y) * 0.5f / PixelsPerMetre;

    Vector2 P(System.Numerics.Vector2 p) => new Vector2(p.X - Origin.X, p.Y - Origin.Y) * PixelsPerMetre;

    public override void _Process(double delta)
    {
        if (_map is null || !IsVisibleInTree() || Size.X < 1f) return;
        var o = Origin;
        _terrain.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("view_origin"), new Vector2(o.X, o.Y));
        _terrain.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("view_size"), Size / PixelsPerMetre);
        _terrain.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("interval"), ContourInterval);
        _terrain.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("edges_only"), EdgesOnly);
        // The fineliner and the hatching band in screen pixels, a little bolder on the larger Tab map.
        float ppm = PixelsPerMetre;
        _terrain.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("ppm"), ppm);
        _terrain.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("line_px"), ppm > 3f ? 2.8f : 2.2f);
        _terrain.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("band_px"), ppm > 4f ? Mathf.Clamp(ppm * 3.2f, 14f, 26f) : 16f);
        _terrain.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("use_fog"), Fog is not null);
        _terrain.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("cutout"), Cutout);
        if (Fog is not null) _terrain.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("fog"), Fog.Texture);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_map is null || Size.X < 1f) return;
        var map = _map;
        float ppm = PixelsPerMetre;
        var font = ThemeDB.FallbackFont;

        // Bommies (the only rocks on the seabed) as the little squares of a survey map; under fog of war only once seen.
        foreach (var d in map.Decor)
            if (d.Kind == DecorKind.Bommie && (Fog is null || Fog.At(d.Position) > 0.5f))
            {
                const float s = 4f;
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

        // Places: nothing until Clementine has been there (no hints), then its emblem — never a bare coloured dot. With
        // the Brass Compass every place shows from the start, except secret rooms. The map tells nothing of enemies (nor
        // of the corruption).
        for (int i = 0; i < map.Pois.Count; i++)
        {
            var poi = map.Pois[i];
            bool shown = Visited.Contains(i) || Landmarks && poi.Kind != PoiKind.Secret;
            if (!shown) continue;
            Vector2 p = P(poi.Position);
            if (poi.Kind == PoiKind.Exit && ShowDetail)
            {
                Color col = LevelView.PoiColor(poi.Kind);
                DrawCircle(p, poi.Radius * ppm, new Color(col, 0.18f));
                DrawArc(p, poi.Radius * ppm, 0f, Mathf.Tau, 48, col, 2f, true);
            }
            DrawEmblem(poi.Kind, p, font, EmblemScale);
        }

        // A light spring, once seen: a pool of clear aqua with a gold heart (brighter while she bathes).
        foreach (var (at, bathing) in Springs)
        {
            if (Fog is not null && Fog.At(at) < 0.5f) continue;
            Vector2 q = P(at);
            DrawCircle(q, 8f, new Color(0f, 0f, 0f, 0.55f));
            DrawCircle(q, 6.5f, new Color(0.3f, 0.85f, 0.95f, bathing ? 1f : 0.85f));
            DrawArc(q, 6.5f, 0f, Mathf.Tau, 24, new Color(0.85f, 1f, 1f), 1.4f, true);
            DrawCircle(q, bathing ? 3f : 2.2f, new Color(1f, 0.85f, 0.45f));
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

        if (ShowLegend)
        {
            DrawLegend(font);
            DrawCaption(font);
        }
        if (Cutout) DrawTitle(font);
    }

    /// <summary>The Tab map's title, top centre: the template's name large, the depth and level small over it, the clock under it.</summary>
    void DrawTitle(Font font)
    {
        void Line(string text, float y, int size, Color color)
        {
            if (text.Length == 0) return;
            var ts = font.GetStringSize(text, HorizontalAlignment.Left, -1, size);
            var at = new Vector2((Size.X - ts.X) * 0.5f, y);
            DrawStringOutline(font, at, text, HorizontalAlignment.Left, -1, size, Mathf.Max(4, size / 4), new Color(0f, 0f, 0f, 0.7f));
            DrawString(font, at, text, HorizontalAlignment.Left, -1, size, color);
        }
        Line(Subtitle, 60f, 18, new Color(1f, 0.9f, 0.75f, 0.9f));
        Line(Title, 100f, 34, new Color(1f, 0.96f, 0.9f));
        Line(Clock, 128f, 15, new Color(1f, 1f, 1f, 0.6f));
    }

    /// <summary>The Tab map's room for its title at the top (pixels).</summary>
    public const float TitleRoom = 145f;
    (System.Numerics.Vector2 Min, System.Numerics.Vector2 Max) _outline = (System.Numerics.Vector2.Zero, new System.Numerics.Vector2(LevelMap.Size));

    /// <summary>
    /// Fits the chart to the screen: this rect, framing the level's outline (its open water, a margin round it) as large
    /// as it will go under the title.
    /// </summary>
    public void Frame(Rect2 screen)
    {
        Position = screen.Position;
        Size = screen.Size;
        var (min, max) = _outline;
        const float margin = 8f;
        min -= new System.Numerics.Vector2(margin);
        max += new System.Numerics.Vector2(margin);
        var span = max - min;
        float room = Mathf.Max(Size.Y - TitleRoom - 30f, 50f);
        float ppm = Mathf.Min((Size.X - 60f) / span.X, room / span.Y);
        MetresAcross = Size.X / ppm;
        // Its middle under the title's room.
        var mid = (min + max) * 0.5f;
        Center = mid - new System.Numerics.Vector2(0f, (TitleRoom - 30f) * 0.5f / ppm);
    }

    /// <summary>
    /// A place's emblem: a badge in its landmark colour, ink-rimmed, with its sign — the start a pennant, the way down
    /// the evacuation sign (a figure running out of a door, on a green plate), the shop a dollar, a clam room a pearl in
    /// an open clam, a shell cache a shell, a curse den a spider, a secret a question mark.
    /// </summary>
    void DrawEmblem(PoiKind kind, Vector2 p, Font font, float k)
    {
        if (kind == PoiKind.Exit)
        {
            DrawExitSign(p, k);
            return;
        }
        DrawCircle(p, 9.5f * k, Ink);
        DrawCircle(p, 8f * k, LevelView.PoiColor(kind));
        switch (kind)
        {
            case PoiKind.Start:
                // A pennant on its pole.
                DrawLine(p + new Vector2(-3f, 5f) * k, p + new Vector2(-3f, -5f) * k, Ink, 1.5f * k, true);
                DrawColoredPolygon(new[] { p + new Vector2(-3f, -5f) * k, p + new Vector2(4.5f, -2.5f) * k, p + new Vector2(-3f, 0f) * k }, Ink);
                break;
            case PoiKind.Shop:
                DrawArc(p, 6f * k, 0f, Mathf.Tau, 24, new Color(1f, 1f, 1f, 0.45f), 1f * k, true);
                Glyph(font, p, "$", 15, k);
                break;
            case PoiKind.TreasureCave:
                DrawClam(p, k);
                break;
            case PoiKind.ShellCache:
                DrawScallop(p + new Vector2(0f, -1f) * k, k);
                break;
            case PoiKind.CurseDen:
                DrawSpider(p, k);
                break;
            case PoiKind.Secret:
                Glyph(font, p, "?", 15, k);
                break;
            default:
                Glyph(font, p, "!", 15, k);
                break;
        }
    }

    /// <summary>A glyph in ink, centred on the badge.</summary>
    void Glyph(Font font, Vector2 p, string text, int size, float k)
    {
        int px = Mathf.RoundToInt(size * k);
        var extent = font.GetStringSize(text, HorizontalAlignment.Left, -1, px);
        DrawString(font, p + new Vector2(-extent.X * 0.5f, px * 0.36f), text, HorizontalAlignment.Left, -1, px, Ink);
    }

    /// <summary>The way down: the evacuation sign — a white figure running out through a door, on a green plate.</summary>
    void DrawExitSign(Vector2 p, float k)
    {
        var plate = new Color(0.05f, 0.62f, 0.32f);
        DrawRect(new Rect2(p - new Vector2(10f, 8.5f) * k, new Vector2(20f, 17f) * k), Ink);
        DrawRect(new Rect2(p - new Vector2(8.6f, 7.1f) * k, new Vector2(17.2f, 14.2f) * k), plate);
        var white = new Color(0.97f, 0.97f, 0.95f);
        Vector2 V(float x, float y) => p + new Vector2(x, y) * k;
        // The door, on the right: its frame, open.
        DrawRect(new Rect2(V(3.2f, -5.6f), new Vector2(4.2f, 11.2f) * k), white, false, 1.2f * k);
        // The figure, running toward it.
        DrawCircle(V(-1.2f, -4.6f), 1.5f * k, white);
        DrawLine(V(-1.8f, -2.6f), V(-3f, 1.2f), white, 1.9f * k, true);
        DrawLine(V(-3f, 1.2f), V(-0.4f, 2.6f), white, 1.5f * k, true);
        DrawLine(V(-0.4f, 2.6f), V(0.2f, 5.4f), white, 1.5f * k, true);
        DrawLine(V(-3f, 1.2f), V(-5.2f, 3.4f), white, 1.5f * k, true);
        DrawLine(V(-5.2f, 3.4f), V(-7f, 3f), white, 1.5f * k, true);
        DrawLine(V(-2f, -1.8f), V(0.8f, -0.4f), white, 1.4f * k, true);
        DrawLine(V(-2f, -1.8f), V(-4.6f, -1f), white, 1.4f * k, true);
    }

    /// <summary>A clam room: a pearl lying in an open clam — the lower valve a bowl, the upper raised clear above it.</summary>
    void DrawClam(Vector2 p, float k)
    {
        Vector2 V(float x, float y) => p + new Vector2(x, y) * k;
        var gold = LevelView.PoiColor(PoiKind.TreasureCave);
        // The upper valve, raised: a scalloped, ribbed dome over the gap.
        var upper = new List<Vector2>();
        for (int i = 0; i <= 12; i++)
        {
            float a = Mathf.Pi * (1f + i / 12f), bump = 1f + 0.07f * Mathf.Cos(i * Mathf.Pi);
            upper.Add(V(Mathf.Cos(a) * 6f * bump, -2f + Mathf.Sin(a) * 4.6f * bump));
        }
        Fan(V(0f, -2f), upper, Ink);
        // Three crisp ribs (antialiased ones feather over the whole valve at map size).
        for (int i = 1; i <= 5; i += 2)
        {
            float a = Mathf.Pi * (1f + i / 6f);
            DrawLine(V(Mathf.Cos(a) * 1.6f, -2.4f + Mathf.Sin(a) * 1.2f), V(Mathf.Cos(a) * 4.8f, -2.4f + Mathf.Sin(a) * 3.6f), gold, Mathf.Max(1f, 0.5f * k));
        }
        // The lower valve: a shallow bowl.
        var lower = new List<Vector2>();
        for (int i = 0; i <= 12; i++)
        {
            float a = Mathf.Pi * i / 12f;
            lower.Add(V(Mathf.Cos(a) * 6.4f, 2f + Mathf.Sin(a) * 3.6f));
        }
        Fan(V(0f, 2f), lower, Ink);
        // The pearl, sitting in it, in the open gap.
        DrawCircle(V(0f, 0.6f), 2.5f * k, new Color(0.98f, 0.96f, 0.92f));
        DrawArc(V(0f, 0.6f), 2.5f * k, 0f, Mathf.Tau, 16, Ink, 0.7f * k, true);
        DrawCircle(V(-0.8f, -0.2f), 0.75f * k, Colors.White);
    }

    /// <summary>A shell cache: the shell the HUD counts (HudView.DrawShell), cream on its badge.</summary>
    void DrawScallop(Vector2 p, float k)
    {
        float size = 6.4f * k;
        var hinge = p + new Vector2(0f, size * 0.55f);
        var rim = new List<Vector2>();
        for (int i = 0; i <= 12; i++)
        {
            float a = Mathf.Pi * (1.15f + 0.7f * i / 12f);
            float bump = 1f + 0.06f * Mathf.Cos(i * Mathf.Pi);
            rim.Add(hinge + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * size * 1.05f * bump);
        }
        var cream = new Color(1f, 0.95f, 0.85f);
        Fan(hinge, rim, cream);
        var poly = new List<Vector2> { hinge };
        poly.AddRange(rim);
        // Its ribs only where there is room for them (the legend's small badges keep the plain fan).
        if (k >= 1.2f)
            for (int i = 3; i <= 9; i += 3) DrawLine(hinge, hinge + (rim[i] - hinge) * 0.9f, Ink, Mathf.Max(1f, 0.5f * k));
        poly.Add(hinge);
        DrawPolyline(poly.ToArray(), Ink, 1f, true);
        DrawRect(new Rect2(hinge - new Vector2(size * 0.28f, size * 0.12f), new Vector2(size * 0.56f, size * 0.22f)), cream);
    }

    /// <summary>
    /// A filled fan: triangles from its centre to each step of its rim, handed straight to the canvas (small scalloped
    /// outlines defeat the polygon triangulator).
    /// </summary>
    void Fan(Vector2 centre, List<Vector2> rim, Color col)
    {
        var points = new Vector2[rim.Count + 1];
        var colors = new Color[rim.Count + 1];
        points[0] = centre;
        for (int i = 0; i < rim.Count; i++) points[i + 1] = rim[i];
        System.Array.Fill(colors, col);
        var indices = new int[(rim.Count - 1) * 3];
        for (int i = 0; i < rim.Count - 1; i++)
        {
            indices[i * 3] = 0;
            indices[i * 3 + 1] = i + 1;
            indices[i * 3 + 2] = i + 2;
        }
        RenderingServer.CanvasItemAddTriangleArray(GetCanvasItem(), indices, points, colors);
    }

    /// <summary>A curse den: a spider — a small body on eight long, jointed legs.</summary>
    void DrawSpider(Vector2 p, float k)
    {
        Vector2 V(float x, float y) => p + new Vector2(x, y) * k;
        for (int side = -1; side <= 1; side += 2)
            for (int leg = 0; leg < 4; leg++)
            {
                float y = -1.4f + leg * 1f;
                var knee = V(side * 3.8f, -4.2f + leg * 2.4f);
                var foot = V(side * 6.4f, -2.2f + leg * 2.6f);
                DrawLine(V(side * 0.8f, y), knee, Ink, 1.1f * k, true);
                DrawLine(knee, foot, Ink, 1.1f * k, true);
            }
        DrawCircle(V(0f, 1.4f), 2.3f * k, Ink);
        DrawCircle(V(0f, -1.6f), 1.5f * k, Ink);
        DrawCircle(V(-0.6f, -2f), 0.45f * k, new Color(1f, 0.85f, 0.85f));
        DrawCircle(V(0.6f, -2f), 0.45f * k, new Color(1f, 0.85f, 0.85f));
    }

    void DrawCaption(Font font)
    {
        if (Caption.Length == 0 && Clock.Length == 0) return;
        var at = new Vector2(14f, 26f);
        DrawString(font, at, Caption, HorizontalAlignment.Left, -1, 15, Ink);
        if (Clock.Length > 0) DrawString(font, at + new Vector2(0f, 20f), Clock, HorizontalAlignment.Left, -1, 14, Ink with { A = 0.75f });
    }

    void DrawLegend(Font font)
    {
        var rows = new (PoiKind Kind, string Name)[]
        {
            (PoiKind.Start, "Start"), (PoiKind.Exit, "Way down"), (PoiKind.ShellCache, "Shell cache"), (PoiKind.Shop, "Shop"),
            (PoiKind.TreasureCave, "Clam room"), (PoiKind.CurseDen, "Curse den"), (PoiKind.Secret, "Secret"),
        };
        const float row = 22f;
        var at = new Vector2(Size.X - 132f, Size.Y - rows.Length * row - 14f);
        DrawRect(new Rect2(at - Vector2.One * 6f, new Vector2(126f, rows.Length * row + 8f)), new Color(1f, 1f, 1f, 0.8f));
        foreach (var (kind, name) in rows)
        {
            DrawEmblem(kind, at + new Vector2(9f, 9f), font, 0.85f);
            DrawString(font, at + new Vector2(24f, 13f), name, HorizontalAlignment.Left, -1, 12, Ink);
            at.Y += row;
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
