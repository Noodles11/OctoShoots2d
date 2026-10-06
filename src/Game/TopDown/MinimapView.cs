using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;

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
        AddChild(new Frame { Size = new Vector2(Diameter, Diameter) });

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

    public void SetVisited(IReadOnlySet<int> visited) => _map.Visited = visited;

    public void SetFog(FogOfWar fog, IReadOnlySet<int> spotted)
    {
        _map.Fog = fog;
        _map.Seen = fog;
        _map.Spotted = spotted;
    }

    /// <summary>The mud cloud around the boss arena (0: none).</summary>
    public void SetMud(System.Numerics.Vector2 center, float rim, float amount) => _map.Mud = new Vector4(center.X, center.Y, rim, amount);

    public void Track(System.Numerics.Vector2 player, System.Numerics.Vector2 velocity)
    {
        _map.Center = player;
        _map.Player = player;
        if (velocity.LengthSquared() > 0.04f) _map.PlayerHeading = velocity;
    }

    /// <summary>The bezel and the north mark.</summary>
    partial class Frame : Control
    {
        public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

        public override void _Draw()
        {
            var c = Size * 0.5f;
            float r = Size.X * 0.5f;
            // A thin outline.
            DrawArc(c, r - 0.75f, 0f, Mathf.Tau, 128, new Color(0.85f, 0.88f, 0.9f, 0.9f), 1.5f, true);
            var top = new Vector2(c.X, 3f);
            DrawColoredPolygon(new[] { top + new Vector2(0f, -1f), top + new Vector2(5f, 8f), top + new Vector2(-5f, 8f) }, new Color(0.85f, 0.88f, 0.9f));
            DrawString(ThemeDB.FallbackFont, top + new Vector2(-4.5f, 22f), "N", HorizontalAlignment.Left, -1, 12, new Color(0.85f, 0.88f, 0.9f));
        }
    }
}
