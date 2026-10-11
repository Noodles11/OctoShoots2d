using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The HUD minimap (DESIGN-TOPDOWN §9): circular, top-right, a <see cref="TopoMap"/> window centred on Clementine,
/// north up like the camera (§3).
/// </summary>
public partial class MinimapView : Control
{
    const float Diameter = 260f;
    const float Margin = 16f;
    /// <summary>Metres across the disc.</summary>
    public const float Span = 90f;
    /// <summary>
    /// She reveals the fog around her to a third of the minimap's radius: the gradient (RevealSoft wide) is centred
    /// where the old 5 m rim was, so the area reads the same size with a softer edge.
    /// </summary>
    public const float RevealSoft = 10f, RevealRadius = Span / 2f / 3f - 2.5f + RevealSoft / 2f;
    /// <summary>An unvisited place shows its "?" once she has been this close (half the minimap's radius).</summary>
    public const float SpotRadius = Span / 2f / 2f;

    TopoMap _map = null!;
    Frame _frame = null!;
    Label _place = null!;
    float _placeAlpha;
    bool _placeShown;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.TopRight);
        Position = new Vector2(-Diameter - Margin, Margin);
        Size = new Vector2(Diameter, Diameter);
        OffsetLeft = -Diameter - Margin;
        OffsetRight = -Margin;
        OffsetTop = Margin;
        OffsetBottom = Margin + Diameter;

        var viewport = new SubViewport { Size = new Vector2I((int)Diameter, (int)Diameter), TransparentBg = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        AddChild(viewport);
        // Wall edges, fog of war and places only: no contours, routes, area outlines, crosses or numbers.
        _map = new TopoMap
        {
            MetresAcross = Span, Size = new Vector2(Diameter, Diameter), ShowDetail = false, EdgesOnly = true,
            ArrowScale = 1.7f, ArrowColor = new Color(1f, 0.72f, 0.35f),
        };
        viewport.AddChild(_map);

        AddChild(new TextureRect
        {
            Texture = viewport.GetTexture(),
            Size = new Vector2(Diameter, Diameter),
            MouseFilter = MouseFilterEnum.Ignore,
            Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/minimap_mask.gdshader") },
        });
        AddChild(_frame = new Frame { Size = new Vector2(Diameter, Diameter) });

        // The place Clementine is in, named under the map.
        _place = new Label
        {
            Position = new Vector2(-40f, Diameter + 8f),
            Size = new Vector2(Diameter + 80f, 32f),
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
            Modulate = new Color(1f, 1f, 1f, 0f),
        };
        _place.AddThemeFontSizeOverride("font_size", 22);
        _place.AddThemeConstantOverride("outline_size", 6);
        _place.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.85f));
        AddChild(_place);
    }

    /// <summary>Names the place Clementine has entered (null when she is in none); the label fades in and out.</summary>
    public void ShowPlace(string? name, Color color)
    {
        _placeShown = name is not null;
        if (name is null) return;
        _place.Text = name;
        _place.AddThemeColorOverride("font_color", color);
    }

    public override void _Process(double delta)
    {
        _placeAlpha = Mathf.MoveToward(_placeAlpha, _placeShown ? 1f : 0f, (float)delta * 2.5f);
        _place.Modulate = new Color(1f, 1f, 1f, _placeAlpha);
    }

    public void SetMap(LevelMap map) => _map.SetMap(map);

    /// <summary>The corruption's texture and works on the minimap (docs/CORRUPTION.md).</summary>
    public void SetCorruption(Texture2D? texture) => _map.Corruption = texture;

    public void SetBlight(PlaneWorld world) => FillBlight(_map, world);

    /// <summary>Copies the world's Blightroots, valves and light springs onto a map.</summary>
    public static void FillBlight(TopoMap map, PlaneWorld world)
    {
        map.Blights.Clear();
        foreach (var b in world.Blightroots) map.Blights.Add((b.Position, b.Alive));
        map.Springs.Clear();
        foreach (var sp in world.Springs) map.Springs.Add((sp.Position, sp.Bathing));
        map.Valves.Clear();
        foreach (var v in world.Valves) map.Valves.Add((v.Center - v.Across * v.HalfWidth, v.Center + v.Across * v.HalfWidth, v.Seen, v.Cleared));
    }

    public void SetVisited(IReadOnlySet<int> visited) => _map.Visited = visited;

    public void SetFog(FogOfWar fog, IReadOnlySet<int> spotted)
    {
        _map.Fog = fog;
        _map.Seen = fog;
        _map.Spotted = spotted;
    }

    /// <summary>The mud cloud around the boss arena (0: none).</summary>
    public void SetMud(System.Numerics.Vector2 center, float rim, float amount) => _map.Mud = new Vector4(center.X, center.Y, rim, amount);

    /// <summary>Her health as a share of her max (0..1): the bezel shows the rest as her corruption meter.</summary>
    public void SetHealth(float health)
    {
        _frame.Health = Mathf.Clamp(health, 0f, 1f);
        _frame.QueueRedraw();
    }

    public void Track(System.Numerics.Vector2 player, System.Numerics.Vector2 velocity)
    {
        _map.Center = player;
        _map.Player = player;
        if (velocity.LengthSquared() > 0.04f) _map.PlayerHeading = velocity;
    }

    /// <summary>
    /// The bezel and the north mark. The bezel is her corruption meter (docs/CORRUPTION.md): it fills clockwise from 12
    /// o'clock with dark ink as corruption takes her (a full ring and she is lost), over a pale track of her light. The
    /// ink's leading edge glints violet; past three quarters it pulses. Draining (a light spring), it recedes.
    /// </summary>
    partial class Frame : Control
    {
        /// <summary>Her health as a share of her max (her corruption is what is missing).</summary>
        public float Health = 1f;
        /// <summary>The corruption shown (eased, so the ink flows rather than jumps).</summary>
        float _shown;

        public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

        public override void _Process(double delta)
        {
            float target = 1f - Health;
            float before = _shown;
            _shown = Mathf.MoveToward(_shown, target, (float)delta * (target > _shown ? 1.2f : 0.9f));
            if (_shown != before || _shown > 0.75f) QueueRedraw();
        }

        public override void _Draw()
        {
            var c = Size * 0.5f;
            float r = Size.X * 0.5f;
            // A thin outline.
            DrawArc(c, r - 0.75f, 0f, Mathf.Tau, 128, new Color(0.85f, 0.88f, 0.9f, 0.9f), 1.5f, true);
            const float width = 10f;
            float rr = r - 7.5f;
            // The track: her light, pale gold, all round.
            DrawArc(c, rr, 0f, Mathf.Tau, 96, new Color(0f, 0f, 0f, 0.6f), width + 3f, true);
            DrawArc(c, rr, 0f, Mathf.Tau, 96, new Color(1f, 0.86f, 0.6f, 0.35f), width, true);
            if (_shown <= 0.001f) return;
            // The ink, clockwise from 12 o'clock (Godot's angles run clockwise on screen from east).
            float from = -Mathf.Pi * 0.5f, to = from + Mathf.Tau * Mathf.Clamp(_shown, 0f, 1f);
            int points = Mathf.Max(4, (int)(96 * _shown));
            float critical = _shown > 0.75f ? 0.5f + 0.5f * Mathf.Sin((float)Time.GetTicksMsec() * 0.008f) : 0f;
            DrawArc(c, rr, from, to, points, new Color(0.04f, 0.015f, 0.07f, 0.97f), width, true);
            // A glossy violet sheen along its outer lip, brighter when she is close to lost.
            DrawArc(c, rr + width * 0.3f, from, to, points, new Color(0.45f, 0.18f, 0.8f, 0.45f + 0.4f * critical), width * 0.22f, true);
            // The leading edge: a bead of ink, glinting.
            var tip = c + new Vector2(Mathf.Cos(to), Mathf.Sin(to)) * rr;
            DrawCircle(tip, width * 0.62f, new Color(0.04f, 0.015f, 0.07f));
            DrawCircle(tip + new Vector2(-1.5f, -1.5f), width * 0.18f, new Color(0.75f, 0.55f, 1f, 0.8f));
            var top = new Vector2(c.X, 3f);
            DrawColoredPolygon(new[] { top + new Vector2(0f, -1f), top + new Vector2(5f, 8f), top + new Vector2(-5f, 8f) }, new Color(0.85f, 0.88f, 0.9f));
            DrawString(ThemeDB.FallbackFont, top + new Vector2(-4.5f, 22f), "N", HorizontalAlignment.Left, -1, 12, new Color(0.85f, 0.88f, 0.9f));
        }
    }
}
