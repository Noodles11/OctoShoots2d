using System;
using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Items;
using OctoShoots.Game.Fx;
using OctoShoots.Game.Title;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The achievement banner (docs/ACHIEVEMENTS-PROPOSAL.md §4): a ticket at the bottom centre — the main card (eyebrow,
/// title, the achievement's line) joined by a perforated seam to a stub holding the pearl it unlocked (live, in its
/// socket, with its name, tagline and effects). A bubble rises and pops into it; the card springs in, its title drops
/// in letter by letter; it holds while a timer line drains; then it leaves in a puff of pastel smoke as the pearl rises
/// out in a bubble and pops. It runs on game time (the game steps it), queues, and plays down with reduced motion.
/// </summary>
public partial class AchievementBanner : Control
{
    const float W = 800f, H = 170f, Main = 470f, Seam = 20f, Bottom = 64f;

    // The timeline (seconds).
    const float PopAt = 0.26f, RingTime = 0.4f, SpringTime = 0.42f, EyebrowIn = 0.34f, EyebrowTime = 0.36f;
    const float TitleIn = 0.42f, LetterStep = 0.022f, LetterTime = 0.26f, LineIn = 0.7f, LineTime = 0.3f;
    const float StubIn = 0.82f, StubTime = 0.43f, StubTextIn = 1f, StubTextStep = 0.08f, GlintAt = 1.2f;
    const float HoldFrom = 1.45f, HoldTo = 5.2f, ShortHoldTo = HoldFrom + 3f, ExitTime = 0.75f, CardFade = 0.34f, Gap = 0.3f;

    sealed record Entry(AchievementDef Def, ItemDef? Pearl);

    readonly Queue<Entry> _queue = new();
    Entry? _current;
    float _t, _wait, _holdTo;
    readonly List<(Vector2 At, float Delay, float Life, float Size, Color Tint, float Drift)> _puffs = new();
    readonly List<(Vector2 Dir, float Speed, float Size)> _sparks = new();
    readonly List<(Vector2 At, float Size, Color Tint)> _confetti = new();
    readonly RandomNumberGenerator _rng = new();
    bool _popped, _chimed, _whooshed, _pearlPopped;

    SubViewport _view = null!;
    MeshInstance3D _pearl = null!;
    TextureRect _pearlTex = null!;

    /// <summary>The reduced-motion option: the card cross-fades in and out, with no travel, pops or particles.</summary>
    public bool ReducedMotion { get; set; }
    public Sfx? Sound { get; set; }

    /// <summary>A banner is showing (or waiting to).</summary>
    public bool Busy => _current is not null || _queue.Count > 0;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        // The pearl: a small sphere of its own material, turning in a tiny transparent viewport.
        _view = new SubViewport { Size = new Vector2I(128, 128), TransparentBg = true, OwnWorld3D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        AddChild(_view);
        _view.AddChild(new Camera3D { Position = new Vector3(0f, 0f, 2.6f), Fov = 40f, Current = true });
        _view.AddChild(new DirectionalLight3D { Rotation = new Vector3(-0.6f, 0.5f, 0f), LightEnergy = 1.4f });
        _view.AddChild(new WorldEnvironment { Environment = new Godot.Environment { AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = Colors.White, AmbientLightEnergy = 0.6f, BackgroundMode = Godot.Environment.BGMode.ClearColor } });
        _pearl = new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.78f, Height = 1.56f, RadialSegments = 32, Rings = 16 } };
        _view.AddChild(_pearl);
        _pearlTex = new TextureRect { Texture = _view.GetTexture(), Size = new Vector2(80f, 80f), PivotOffset = new Vector2(40f, 40f), MouseFilter = MouseFilterEnum.Ignore, Visible = false, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale };
        AddChild(_pearlTex);
    }

    /// <summary>Queues a banner for an achievement and the pearl it unlocked.</summary>
    public void Enqueue(AchievementDef def, ItemDef? pearl) => _queue.Enqueue(new Entry(def, pearl));

    /// <summary>Advances the banner by game time (not called while paused or under a splash, so it waits).</summary>
    public void Step(float dt)
    {
        if (_current is null)
        {
            if (_queue.Count == 0) return;
            _wait -= dt;
            if (_wait > 0f) return;
            Begin(_queue.Dequeue());
        }
        _t += dt;
        var cur = _current!;
        float end = _holdTo + ExitTime + (ReducedMotion ? 0f : 0.3f);
        if (!ReducedMotion)
        {
            if (!_popped && _t >= PopAt)
            {
                _popped = true;
                Sound?.Play("pop");
            }
            if (!_chimed && _t >= PopAt + 0.1f)
            {
                _chimed = true;
                Sound?.Play("ach_chime", -4f, 0f);
            }
            if (!_whooshed && _t >= _holdTo)
            {
                _whooshed = true;
                Sound?.Play("whoosh", -6f);
            }
            if (!_pearlPopped && _t >= _holdTo + 0.6f)
            {
                _pearlPopped = true;
                Sound?.Play("pop", -8f);
            }
        }
        if (_pearl.Mesh is not null) _pearl.RotateY(dt * 0.9f);
        if (_t >= end)
        {
            _current = null;
            _pearlTex.Visible = false;
            _wait = Gap;
        }
        PlacePearl();
        QueueRedraw();
    }

    void Begin(Entry e)
    {
        _current = e;
        _t = 0f;
        _popped = _chimed = _whooshed = _pearlPopped = false;
        // With three or more waiting, each holds a shorter while.
        _holdTo = _queue.Count >= 2 ? ShortHoldTo : HoldTo;
        _rng.Seed = (ulong)e.Def.Id.GetHashCode();
        _confetti.Clear();
        Color[] pastel = { TitleStyle.Coral, TitleStyle.Mint, TitleStyle.Butter, new(1f, 0.7f, 0.85f), new(0.7f, 0.85f, 1f) };
        for (int i = 0; i < 18; i++)
            _confetti.Add((new Vector2(_rng.RandfRange(16f, Main - 16f), _rng.RandfRange(12f, H - 12f)), _rng.RandfRange(2f, 4.5f), pastel[i % pastel.Length]));
        _sparks.Clear();
        for (int i = 0; i < 14; i++)
            _sparks.Add((Vector2.FromAngle(i * Mathf.Tau / 14f + _rng.RandfRange(-0.2f, 0.2f)), _rng.RandfRange(160f, 300f), _rng.RandfRange(3f, 6f)));
        _puffs.Clear();
        Color[] smoke = { Colors.White, new(1f, 0.82f, 0.9f), new(0.78f, 1f, 0.9f), new(1f, 0.95f, 0.75f) };
        for (int i = 0; i < 26; i++)
        {
            float x = (i + 0.5f) / 26f * W;
            float fromMiddle = Mathf.Abs(x - W * 0.5f) / (W * 0.5f);
            _puffs.Add((new Vector2(x + _rng.RandfRange(-12f, 12f), H * 0.5f + _rng.RandfRange(-45f, 45f)), fromMiddle * 0.14f,
                _rng.RandfRange(0.64f, 0.84f), _rng.RandfRange(20f, 34f), smoke[i % smoke.Length], _rng.RandfRange(30f, 70f)));
        }
        if (e.Pearl is not null) _pearl.MaterialOverride = PearlMaterials.Get(e.Pearl);
        _pearl.Visible = e.Pearl is not null;
        if (ReducedMotion) Sound?.Play("ach_chime", -6f, 0f);
    }

    /// <summary>Where the card sits on screen: bottom centre, clear of the pearls, the shells and the minimap.</summary>
    Vector2 Origin => new((Size.X - W) * 0.5f, Size.Y - H - Bottom);

    static float Clamp01(float v) => Mathf.Clamp(v, 0f, 1f);
    static float EaseOut(float k) => 1f - (1f - k) * (1f - k) * (1f - k);
    static float EaseOutBack(float k)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(k - 1f, 3f) + c1 * Mathf.Pow(k - 1f, 2f);
    }

    /// <summary>The card's spring at the pop: 0.4 → 1.06 → 1.</summary>
    float Spring(float t)
    {
        float k = Clamp01((t - PopAt) / SpringTime);
        if (k <= 0f) return 0.4f;
        return 1f + (0.4f - 1f) * Mathf.Exp(-6f * k) * Mathf.Cos(k * 9f);
    }

    /// <summary>How visible the card is (the exit fades it), and how it grows and rises as it goes.</summary>
    (float Alpha, float Scale, float Rise) Exit(float t)
    {
        if (t < _holdTo) return (1f, 1f, 0f);
        float k = Clamp01((t - _holdTo) / CardFade);
        return (1f - k, 1f + 0.04f * k, -8f * k);
    }

    void PlacePearl()
    {
        if (_current is null || _current.Pearl is null)
        {
            _pearlTex.Visible = false;
            return;
        }
        var o = Origin;
        var socket = o + new Vector2(Main + Seam + 64f, H * 0.5f);
        float t = _t;
        if (ReducedMotion)
        {
            float a = Clamp01(t / 0.3f) * (1f - Clamp01((t - _holdTo) / 0.4f));
            _pearlTex.Visible = a > 0f;
            _pearlTex.Modulate = new Color(1f, 1f, 1f, a);
            _pearlTex.Position = socket - _pearlTex.Size * 0.5f;
            _pearlTex.Scale = Vector2.One;
            return;
        }
        // It drops into the socket with a squash, sits, then rises out of the smoke in a bubble.
        float drop = Clamp01((t - (StubIn + 0.1f)) / 0.33f);
        float squash = t > StubIn + 0.43f ? 1f - 0.18f * Mathf.Exp(-12f * (t - StubIn - 0.43f)) * Mathf.Cos((t - StubIn - 0.43f) * 30f) : 1f;
        float rise = t > _holdTo ? EaseOut(Clamp01((t - _holdTo) / 0.6f)) * 200f : 0f;
        _pearlTex.Visible = drop > 0f && t < _holdTo + 0.6f;
        _pearlTex.Position = socket - _pearlTex.Size * 0.5f + new Vector2(0f, -60f * (1f - EaseOut(drop)) - rise);
        _pearlTex.Scale = new Vector2(2f - squash, squash) * (0.4f + 0.6f * EaseOut(drop));
        _pearlTex.Modulate = new Color(1f, 1f, 1f, Clamp01(drop * 3f));
    }

    public override void _Draw()
    {
        if (_current is null) return;
        var e = _current;
        float t = _t;
        var o = Origin;
        var font = TitleStyle.Body;
        var bold = TitleStyle.BodyBold;
        var display = TitleStyle.Display;

        float alpha, scale, rise;
        if (ReducedMotion)
        {
            alpha = Clamp01(t / 0.3f) * (1f - Clamp01((t - _holdTo) / 0.4f));
            scale = 1f;
            rise = 0f;
        }
        else
        {
            if (t < PopAt)
            {
                // A bubble rises to the card's centre, wobbling.
                float k = EaseOutBack(Clamp01(t / PopAt));
                var at = o + new Vector2(W * 0.5f, Mathf.Lerp(H + 140f, H * 0.5f, k));
                float wob = 1f + 0.08f * Mathf.Sin(t * 50f);
                DrawBubble(at, 26f * wob, 26f / wob, 1f);
                return;
            }
            var exit = Exit(t);
            alpha = exit.Alpha;
            scale = Spring(t) * exit.Scale;
            rise = exit.Rise;
        }

        var centre = o + new Vector2(W * 0.5f, H * 0.5f + rise);
        if (alpha > 0f)
        {
            // Everything on the card is drawn in its own frame (c is its top-left), scaled about its centre.
            var card = new Transform2D(0f, Vector2.One * scale, 0f, centre);
            DrawSetTransformMatrix(card);
            var c = -new Vector2(W * 0.5f, H * 0.5f);
            Color A(Color col, float a = 1f) => col with { A = col.A * a * alpha };
            float stub = ReducedMotion ? 1f : EaseOut(Clamp01((t - StubIn) / StubTime));

            // The main card: pearl white, a coral rim, confetti.
            DrawRoundRect(new Rect2(c + new Vector2(3f, 5f), new Vector2(Main, H)), 18f, A(new Color(0f, 0f, 0f, 0.25f)));
            DrawRoundRect(new Rect2(c, new Vector2(Main, H)), 18f, A(TitleStyle.Pearl));
            DrawRoundRectOutline(new Rect2(c, new Vector2(Main, H)), 18f, A(TitleStyle.Coral), 3f);
            foreach (var (at, size, tint) in _confetti) DrawCircle(c + at, size, A(tint, 0.55f));

            // The stub flips in on its hinge (the seam).
            if (stub > 0f)
            {
                var hinge = c + new Vector2(Main, 0f);
                float sw = (W - Main) * stub;
                DrawRoundRect(new Rect2(hinge + new Vector2(3f, 5f), new Vector2(sw, H)), 18f, A(new Color(0f, 0f, 0f, 0.25f)));
                DrawRoundRect(new Rect2(hinge, new Vector2(sw, H)), 18f, A(TitleStyle.Pearl));
                DrawRoundRectOutline(new Rect2(hinge, new Vector2(sw, H)), 18f, A(TitleStyle.Coral), 3f);
                if (stub > 0.9f) DrawStub(c, t, A, bold, font, e);
            }
            // The perforated seam.
            for (float y = 14f; y < H - 10f; y += 13f) DrawCircle(c + new Vector2(Main + Seam * 0.5f - 6f, y), 2.4f, A(TitleStyle.Coral, 0.7f));

            // The eyebrow slides in, unskewing.
            float eb = ReducedMotion ? 1f : EaseOut(Clamp01((t - EyebrowIn) / EyebrowTime));
            if (eb > 0f)
            {
                var at = c + new Vector2(28f - 60f * (1f - eb), 38f);
                // Skewed −12° as it starts, upright as it lands.
                DrawSetTransformMatrix(card * new Transform2D(0f, Vector2.One, -0.21f * (1f - eb), at));
                DrawString(bold, Vector2.Zero, "ACHIEVEMENT", HorizontalAlignment.Left, -1, 15, A(TitleStyle.Coral, eb));
                float w = bold.GetStringSize("ACHIEVEMENT", HorizontalAlignment.Left, -1, 15).X;
                var d = new Vector2(w + 14f, -6f);
                DrawColoredPolygon(new[] { d + new Vector2(0f, -6f), d + new Vector2(6f, 0f), d + new Vector2(0f, 6f), d + new Vector2(-6f, 0f) }, A(TitleStyle.Mint, eb));
                DrawSetTransformMatrix(card);
            }

            // The title drops in letter by letter, overshooting.
            {
                var at = c + new Vector2(28f, 82f);
                for (int i = 0; i < e.Def.Title.Length; i++)
                {
                    string ch = e.Def.Title[i].ToString();
                    float cw = display.GetStringSize(ch, HorizontalAlignment.Left, -1, 34).X;
                    float k = ReducedMotion ? 1f : Clamp01((t - TitleIn - i * LetterStep) / LetterTime);
                    if (k > 0f)
                    {
                        float back = EaseOutBack(k);
                        float tilt = (i % 2 == 0 ? 1f : -1f) * Mathf.DegToRad(6f) * (1f - k);
                        var pos = at + new Vector2(0f, -14f * (1f - back));
                        DrawSetTransformMatrix(card * new Transform2D(tilt, pos));
                        DrawString(display, Vector2.Zero, ch, HorizontalAlignment.Left, -1, 34, A(TitleStyle.Ink, Clamp01(k * 2f)));
                    }
                    at.X += cw;
                }
                DrawSetTransformMatrix(card);
            }

            // The line fades up.
            float ln = ReducedMotion ? 1f : EaseOut(Clamp01((t - LineIn) / LineTime));
            if (ln > 0f)
                DrawMultilineString(font, c + new Vector2(28f, 112f + 8f * (1f - ln)), "“" + e.Def.Line + "”", HorizontalAlignment.Left, Main - 56f, 16, -1,
                    A(e.Def.Tone == "dark" ? TitleStyle.Ink : TitleStyle.InkSoft, ln));

            // The hold timer drains along the bottom.
            if (t >= HoldFrom && t < _holdTo)
            {
                float k = 1f - (t - HoldFrom) / (_holdTo - HoldFrom);
                DrawRect(new Rect2(c + new Vector2(18f, H + 10f), new Vector2((W - 36f) * k, 3f)), A(TitleStyle.Coral, 0.75f));
            }
            DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
        }

        if (ReducedMotion) return;

        // The pop: a ring shockwave and sparkles.
        float ring = Clamp01((t - PopAt) / RingTime);
        if (ring > 0f && ring < 1f)
        {
            var at = o + new Vector2(W * 0.5f, H * 0.5f);
            DrawArc(at, 30f + 260f * EaseOut(ring), 0f, Mathf.Tau, 64, new Color(1f, 1f, 1f, 0.8f * (1f - ring)), 4f * (1f - ring) + 1f, true);
            foreach (var (dir, speed, size) in _sparks)
                DrawStar(at + dir * speed * EaseOut(ring) * 0.9f, size * (1f - ring * 0.6f), new Color(1f, 0.95f, 0.8f, 1f - ring));
        }

        // The smoke exit: soft pastel puffs bloom along the card, the middle first, swelling and drifting up.
        if (t >= _holdTo)
            foreach (var (at, delay, life, size, tint, drift) in _puffs)
            {
                float k = (t - _holdTo - delay) / life;
                if (k <= 0f || k >= 1f) continue;
                var p = o + at + new Vector2(0f, -drift * k);
                float r = size * (1f + 1.6f * EaseOut(k));
                float a = (k < 0.2f ? k / 0.2f : 1f - (k - 0.2f) / 0.8f) * 0.8f;
                DrawCircle(p, r * 1.25f, tint with { A = a * 0.35f });
                DrawCircle(p, r, tint with { A = a * 0.6f });
            }

        // The pearl's bubble as it rises out of the smoke, popping into sparkles.
        if (e.Pearl is not null && t >= _holdTo)
        {
            float k = Clamp01((t - _holdTo) / 0.6f);
            var at = o + new Vector2(Main + Seam + 64f, H * 0.5f - EaseOut(k) * 200f);
            if (k < 1f) DrawBubble(at, 56f, 56f, 1f);
            float pk = Clamp01((t - _holdTo - 0.6f) / 0.3f);
            if (pk > 0f && pk < 1f)
                foreach (var (dir, speed, size) in _sparks)
                    DrawStar(at + dir * speed * 0.35f * EaseOut(pk), size * (1f - pk), new Color(1f, 1f, 1f, 1f - pk));
        }
    }

    /// <summary>The stub: the socket with turning pastel rays behind it, the NEW PEARL chip, and the pearl's lines.</summary>
    void DrawStub(Vector2 c, float t, Func<Color, float, Color> A, Font bold, Font font, Entry e)
    {
        var socket = c + new Vector2(Main + Seam + 64f, H * 0.5f);
        Color[] rays = { new(1f, 0.85f, 0.9f), new(0.8f, 1f, 0.92f), new(1f, 0.96f, 0.8f), new(0.85f, 0.9f, 1f) };
        for (int i = 0; i < 12; i++)
        {
            float a0 = t * 0.4f + i * Mathf.Tau / 12f, a1 = a0 + Mathf.Tau / 24f;
            DrawColoredPolygon(new[] { socket, socket + Vector2.FromAngle(a0) * 62f, socket + Vector2.FromAngle(a1) * 62f }, A(rays[i % rays.Length], 0.8f));
        }
        DrawCircle(socket, 46f, A(Colors.White, 1f));
        DrawArc(socket, 46f, 0f, Mathf.Tau, 48, A(TitleStyle.Rule, 1f), 3f, true);
        // A glint sweeps the pearl.
        float glint = Clamp01((t - GlintAt) / 0.35f);
        if (glint > 0f && glint < 1f && !ReducedMotion)
        {
            var g = socket + new Vector2(-40f + 80f * glint, -24f + 48f * glint);
            DrawLine(g + new Vector2(-10f, 10f), g + new Vector2(10f, -10f), new Color(1f, 1f, 1f, 0.9f * (1f - Mathf.Abs(glint * 2f - 1f))), 5f, true);
        }

        float x = Main + Seam + 128f;
        float Line(int n) => ReducedMotion ? 1f : EaseOut(Clamp01((t - StubTextIn - n * StubTextStep) / 0.3f));
        // The chip.
        var chip = new Rect2(c + new Vector2(x, 20f), new Vector2(92f, 22f));
        DrawRoundRect(chip, 11f, A(TitleStyle.Mint, Line(0)));
        DrawString(bold, chip.Position + new Vector2(10f, 16f), "NEW PEARL", HorizontalAlignment.Left, -1, 12, A(Colors.White, Line(0)));
        if (e.Pearl is null) return;
        DrawString(bold, c + new Vector2(x, 66f + 6f * (1f - Line(1))), e.Pearl.Name, HorizontalAlignment.Left, W - x - 14f, 19, A(TitleStyle.Ink, Line(1)));
        DrawString(font, c + new Vector2(x, 88f + 6f * (1f - Line(2))), e.Pearl.Tagline, HorizontalAlignment.Left, W - x - 14f, 13, A(TitleStyle.InkSoft, Line(2)));
        var lines = ItemCaption.Describe(e.Pearl);
        for (int i = 0; i < Math.Min(lines.Count, 3); i++)
            DrawMultilineString(font, c + new Vector2(x, 112f + i * 17f + 6f * (1f - Line(3 + i))), "• " + lines[i], HorizontalAlignment.Left, W - x - 14f, 12, lines.Count == 1 ? 3 : 1,
                A(TitleStyle.Ink, Line(3 + i)));
    }

    void DrawBubble(Vector2 at, float rx, float ry, float a)
    {
        DrawSetTransform(at, 0f, new Vector2(rx, ry) / 26f);
        DrawCircle(Vector2.Zero, 26f, new Color(0.85f, 0.95f, 1f, 0.18f * a));
        DrawArc(Vector2.Zero, 26f, 0f, Mathf.Tau, 40, new Color(1f, 1f, 1f, 0.85f * a), 2.5f, true);
        DrawCircle(new Vector2(-9f, -9f), 5f, new Color(1f, 1f, 1f, 0.8f * a));
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }

    void DrawStar(Vector2 at, float r, Color col)
    {
        DrawColoredPolygon(new[] { at + new Vector2(0f, -r * 2f), at + new Vector2(r * 0.5f, 0f), at + new Vector2(0f, r * 2f), at + new Vector2(-r * 0.5f, 0f) }, col);
        DrawColoredPolygon(new[] { at + new Vector2(-r * 2f, 0f), at + new Vector2(0f, r * 0.5f), at + new Vector2(r * 2f, 0f), at + new Vector2(0f, -r * 0.5f) }, col);
    }

    void DrawRoundRect(Rect2 r, float radius, Color col) =>
        DrawStyleBox(new StyleBoxFlat { BgColor = col, CornerRadiusTopLeft = (int)radius, CornerRadiusTopRight = (int)radius, CornerRadiusBottomLeft = (int)radius, CornerRadiusBottomRight = (int)radius, AntiAliasing = true }, r);

    void DrawRoundRectOutline(Rect2 r, float radius, Color col, float width) =>
        DrawStyleBox(new StyleBoxFlat
        {
            DrawCenter = false, BorderColor = col,
            BorderWidthLeft = (int)width, BorderWidthRight = (int)width, BorderWidthTop = (int)width, BorderWidthBottom = (int)width,
            CornerRadiusTopLeft = (int)radius, CornerRadiusTopRight = (int)radius, CornerRadiusBottomLeft = (int)radius, CornerRadiusBottomRight = (int)radius, AntiAliasing = true,
        }, r);
}
