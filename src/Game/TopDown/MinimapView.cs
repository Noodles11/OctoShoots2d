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

    TopoMap _map = null!;
    TextureRect _shown = null!;
    Frame _frame = null!;
    /// <summary>The dive (DESIGN-TOPDOWN §4.6): 0 → 1 as the map zooms in and fades out; then the new level's fades in.</summary>
    float _diveOut, _arrive = 1f;
    Shroud _shroud = null!;
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

        // A dark disc behind the map, so it fades to dark (between levels) rather than to the sea behind.
        AddChild(new Backing { Size = new Vector2(Diameter, Diameter) });
        AddChild(_shown = new TextureRect
        {
            Texture = viewport.GetTexture(),
            Size = new Vector2(Diameter, Diameter),
            MouseFilter = MouseFilterEnum.Ignore,
            Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/minimap_mask.gdshader") },
        });
        AddChild(_shroud = new Shroud { Size = new Vector2(Diameter, Diameter) });
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

    /// <summary>Diving (0 → 1): the old level's map zooms in on her and fades away.</summary>
    public void DiveOut(float k)
    {
        _diveOut = Mathf.Clamp(k, 0f, 1f);
        _arrive = 0f;
        ApplyDive();
    }

    /// <summary>Down on the new level (its map set): it fades in, settling from a little closer.</summary>
    public void Arrive()
    {
        _diveOut = 0f;
        _arrive = 0f;
        ApplyDive();
    }

    void ApplyDive()
    {
        float zoom, alpha;
        if (_diveOut > 0f)
        {
            float e = Mathf.SmoothStep(0f, 1f, _diveOut);
            zoom = 1f + 1.6f * e;
            alpha = 1f - e;
        }
        else
        {
            float e = Mathf.SmoothStep(0f, 1f, _arrive);
            zoom = 1.35f - 0.35f * e;
            alpha = e;
        }
        _map.MetresAcross = Span / zoom;
        _shown.Modulate = new Color(1f, 1f, 1f, alpha);
    }

    public override void _Process(double delta)
    {
        if (_arrive < 1f && _diveOut <= 0f)
        {
            _arrive = Mathf.MoveToward(_arrive, 1f, (float)delta / 0.8f);
            ApplyDive();
        }
        _placeAlpha = Mathf.MoveToward(_placeAlpha, _placeShown ? 1f : 0f, (float)delta * 2.5f);
        _place.Modulate = new Color(1f, 1f, 1f, _placeAlpha);
    }

    public void SetMap(LevelMap map, IEnumerable<int>? sealedCaves = null) => _map.SetMap(map, sealedCaves);

    /// <summary>Her arrow on the map (her colour).</summary>
    public Color ArrowColor
    {
        get => _map.ArrowColor;
        set => _map.ArrowColor = value;
    }

    /// <summary>The map fades to black (0 none → 1 all): in the curse den's dark she cannot see where she is. The bezel stays.</summary>
    public float Blackout
    {
        get => _shroud.Amount;
        set
        {
            if (Mathf.IsEqualApprox(value, _shroud.Amount)) return;
            _shroud.Amount = value;
            _shroud.QueueRedraw();
        }
    }

    partial class Backing : Control
    {
        public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

        public override void _Draw() => DrawCircle(Size * 0.5f, Size.X * 0.5f - 3f, new Color(0.155f, 0.17f, 0.195f));
    }

    /// <summary>Black over the map's disc, under the bezel.</summary>
    partial class Shroud : Control
    {
        public float Amount;

        public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

        public override void _Draw()
        {
            if (Amount <= 0.001f) return;
            DrawCircle(Size * 0.5f, Size.X * 0.5f - 2f, new Color(0.01f, 0.005f, 0.02f, Amount));
        }
    }

    /// <summary>The corruption's texture and works on the minimap (docs/CORRUPTION.md).</summary>
    public void SetCorruption(Texture2D? texture) => _map.Corruption = texture;

    public void SetBlight(PlaneWorld world) => FillBlight(_map, world);

    /// <summary>Copies the world's Blightroots, Blotters and light springs onto a map.</summary>
    public static void FillBlight(TopoMap map, PlaneWorld world)
    {
        map.Blights.Clear();
        foreach (var b in world.Blightroots) map.Blights.Add((b.Position, b.Alive));
        map.Blotters.Clear();
        foreach (var b in world.Blotters) map.Blotters.Add((b.Position, b.Heading));
        map.Springs.Clear();
        // A spent spring is gone from the maps too.
        foreach (var sp in world.Springs)
            if (!sp.Spent) map.Springs.Add((sp.Position, sp.Bathing));
    }

    public void SetVisited(IReadOnlySet<int> visited) => _map.Visited = visited;

    /// <summary>Brass Compass: every place on the map, visited or not (secret rooms excepted).</summary>
    public bool Landmarks
    {
        get => _map.Landmarks;
        set => _map.Landmarks = value;
    }

    public void SetFog(FogOfWar fog)
    {
        _map.Fog = fog;
        _map.Seen = fog;
    }

    /// <summary>The mud cloud around the boss arena (0: none).</summary>
    public void SetMud(System.Numerics.Vector2 center, float rim, float amount) => _map.Mud = new Vector4(center.X, center.Y, rim, amount);

    /// <summary>Her health as a share of her max (0..1): the bezel shows the rest as her corruption meter.</summary>
    public void SetHealth(float health)
    {
        health = Mathf.Clamp(health, 0f, 1f);
        // A hit: the ink's edge flares, so the meter is noticed the moment it moves.
        if (health < _frame.Health - 1e-4f) _frame.Hit = 1f;
        _frame.Health = health;
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
        /// <summary>1 on a hit, fading.</summary>
        public float Hit;
        /// <summary>The corruption shown (eased, so the ink flows rather than jumps).</summary>
        float _shown;

        public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

        /// <summary>Ink evaporating off the meter as it drains: wisps that lift away from the ring, thin and turn gold.</summary>
        struct Wisp
        {
            public Vector2 At, Velocity;
            public float Age, Life, Size;
        }

        readonly System.Collections.Generic.List<Wisp> _wisps = new();
        readonly System.Random _random = new(77);
        float _owed;

        float Rand(float a, float b) => a + (b - a) * (float)_random.NextDouble();

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            Hit = Mathf.MoveToward(Hit, 0f, dt * 2.5f);
            float target = 1f - Health;
            float before = _shown;
            _shown = Mathf.MoveToward(_shown, target, dt * (target > _shown ? 1.2f : 0.9f));
            // Draining: the ink steams off the receding end of the arc, more the faster it goes.
            float drained = before - _shown;
            if (drained > 0f)
            {
                _owed += drained * 900f;
                var c = Size * 0.5f;
                float rr = Size.X * 0.5f - 7.5f;
                while (_owed >= 1f)
                {
                    _owed -= 1f;
                    // Mostly at the tip, some from the ink just behind it.
                    float back = Mathf.Min(_shown, 0.08f) * Mathf.Pow(Rand(0f, 1f), 2f);
                    float a = -Mathf.Pi * 0.5f + Mathf.Tau * (_shown - back);
                    var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    _wisps.Add(new Wisp
                    {
                        At = c + dir * (rr + Rand(-4f, 4f)),
                        Velocity = dir * Rand(6f, 18f) + new Vector2(Rand(-6f, 6f), Rand(-26f, -12f)),
                        Life = Rand(0.6f, 1.1f),
                        Size = Rand(2.5f, 4.5f),
                    });
                }
            }
            else _owed = 0f;
            for (int i = _wisps.Count - 1; i >= 0; i--)
            {
                var w = _wisps[i];
                w.Age += dt;
                if (w.Age >= w.Life)
                {
                    _wisps.RemoveAt(i);
                    continue;
                }
                // Curling as it rises, slowing.
                w.Velocity = w.Velocity * Mathf.Exp(-1.6f * dt) + new Vector2(Mathf.Sin(w.Age * 9f + w.Size * 3f) * 30f * dt, -10f * dt);
                w.At += w.Velocity * dt;
                _wisps[i] = w;
            }
            if (_shown != before || _shown > 0.75f || _wisps.Count > 0 || Hit > 0f) QueueRedraw();
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
            // Bright enough that the dark ink reads against it over any map (the boss arena's mud included).
            DrawArc(c, rr, 0f, Mathf.Tau, 96, new Color(1f, 0.88f, 0.62f, 0.78f), width, true);
            var top = new Vector2(c.X, 3f);
            DrawColoredPolygon(new[] { top + new Vector2(0f, -1f), top + new Vector2(5f, 8f), top + new Vector2(-5f, 8f) }, new Color(0.85f, 0.88f, 0.9f));
            DrawString(ThemeDB.FallbackFont, top + new Vector2(-4.5f, 22f), "N", HorizontalAlignment.Left, -1, 12, new Color(0.85f, 0.88f, 0.9f));
            DrawWisps();
            if (_shown <= 0.001f) return;
            // The ink, clockwise from 12 o'clock (Godot's angles run clockwise on screen from east).
            float from = -Mathf.Pi * 0.5f, to = from + Mathf.Tau * Mathf.Clamp(_shown, 0f, 1f);
            int points = Mathf.Max(4, (int)(96 * _shown));
            float critical = _shown > 0.75f ? 0.5f + 0.5f * Mathf.Sin((float)Time.GetTicksMsec() * 0.008f) : 0f;
            if (Hit > 0f) DrawArc(c, rr, from, to, points, new Color(0.8f, 0.55f, 1f, 0.85f * Hit), width + 6f, true);
            DrawArc(c, rr, from, to, points, new Color(0.04f, 0.015f, 0.07f, 0.97f), width, true);
            // A glossy violet sheen along its outer lip, brighter when she is close to lost.
            DrawArc(c, rr + width * 0.3f, from, to, points, new Color(0.45f, 0.18f, 0.8f, 0.45f + 0.4f * critical), width * 0.22f, true);
            // The leading edge: a bead of ink, glinting.
            var tip = c + new Vector2(Mathf.Cos(to), Mathf.Sin(to)) * rr;
            DrawCircle(tip, width * 0.62f, new Color(0.04f, 0.015f, 0.07f));
            DrawCircle(tip + new Vector2(-1.5f, -1.5f), width * 0.18f, new Color(0.75f, 0.55f, 1f, 0.8f));
        }

        /// <summary>The evaporating ink: dark puffs that swell, thin, and burn off into gold glints.</summary>
        void DrawWisps()
        {
            foreach (var w in _wisps)
            {
                float k = w.Age / w.Life;
                float size = w.Size * (1f + 1.6f * k);
                var ink = new Color(0.08f, 0.03f, 0.13f).Lerp(new Color(0.55f, 0.3f, 0.85f), k);
                DrawCircle(w.At, size, ink with { A = 0.7f * (1f - k) * (1f - k) });
                // A spark of light where the ink lets go.
                if (k > 0.3f) DrawCircle(w.At, 1.1f, new Color(1f, 0.86f, 0.5f, 0.9f * (1f - k)));
            }
        }
    }
}
