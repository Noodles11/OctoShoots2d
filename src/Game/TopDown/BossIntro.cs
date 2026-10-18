using System;
using Godot;
using OctoShoots.Core.Bosses;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The boss intro (DESIGN-TOPDOWN §6.4): an anime cut-in over the whole screen while the game holds, drawn live. Five
/// beats in about 5.4 s: speed lines wipe in in the depth's tint; Clementine's panel slides in from the left, she spots
/// something, does a double-take with a sweat-drop and gulps; the boss's panel slams in from the right in a white impact
/// frame and it does its body form's gag; a glowing VS punches in on the seam, she sets her face and it taunts; the
/// panels burst into bubbles and pop away into the game. "CLEMENTINE" and the boss's name ride on ribbons. Any key
/// skips to the bubbles. Reduced motion keeps the beats but drops the shake, the flash and the overshoot.
/// </summary>
public partial class BossIntro : Control
{
    /// <summary>The beats (seconds): wipe, her panel, the slam, the face-off, the bubbles, the end.</summary>
    const float HerIn = 0.5f, Slam = 1.8f, Face = 3.2f, Out = 4.6f, End = 5.4f;

    /// <summary>A beat for the game to sound or shake: "wipe", "slam", "vs", "pop".</summary>
    public event Action<string>? Cue;

    BossSpec _spec = null!;
    float _t = -1f, _menace;
    int _depth;
    bool _reduced;
    string _name = "";
    Panel _her = null!, _foe = null!;
    readonly Random _random = new(5);
    readonly (Vector2 At, float Delay, float Size)[] _bubbles = new (Vector2, float, float)[56];
    int _cues;

    public bool Playing => _t >= 0f && _t < End;
    /// <summary>How long it has been playing.</summary>
    public float Elapsed => _t;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
        // Each panel is a mask: its own shape clips the scene drawn inside it.
        AddChild(_her = new Panel(this, true) { ClipChildren = ClipChildrenMode.AndDraw });
        AddChild(_foe = new Panel(this, false) { ClipChildren = ClipChildrenMode.AndDraw });
        AddChild(new Overlay(this));
    }

    /// <summary>Plays for this boss, at this depth (shown: 1 the Kelp Jungle … 6), gentler with reduced motion.</summary>
    public void Play(BossSpec spec, int depth, bool reduced)
    {
        _spec = spec;
        _depth = depth;
        _menace = Mathf.Clamp((depth - 1) / 5f, 0f, 1f);
        _reduced = reduced;
        _name = spec.Name.ToUpperInvariant();
        _t = 0f;
        _cues = 0;
        for (int i = 0; i < _bubbles.Length; i++)
            _bubbles[i] = (new Vector2((float)_random.NextDouble(), (float)_random.NextDouble()), (float)_random.NextDouble() * 0.45f, 0.03f + 0.07f * (float)_random.NextDouble());
        Visible = true;
    }

    /// <summary>Skips ahead to the bubbles (already there: nothing).</summary>
    public void Skip()
    {
        if (Playing && _t < Out) _t = Out;
    }

    public void Stop()
    {
        _t = -1f;
        Visible = false;
    }

    public override void _Process(double delta)
    {
        if (_t < 0f) return;
        _t += (float)delta;
        // The cues, once each as their beat comes.
        string[] cues = { "wipe", "slam", "vs", "pop" };
        float[] at = { 0f, Slam, Face, Out };
        while (_cues < cues.Length && _t >= at[_cues]) Cue?.Invoke(cues[_cues++]);
        if (_t >= End)
        {
            Stop();
            return;
        }
        // The bubbles take the panels away.
        _her.Modulate = _foe.Modulate = new Color(1f, 1f, 1f, Fading);
        foreach (var child in GetChildren())
            if (child is CanvasItem item) item.QueueRedraw();
        foreach (var panel in new[] { _her, _foe })
            foreach (var child in panel.GetChildren())
                if (child is CanvasItem item) item.QueueRedraw();
        QueueRedraw();
    }

    static float Ease(float x) => 1f - Mathf.Pow(1f - Mathf.Clamp(x, 0f, 1f), 3f);

    /// <summary>Overshooting ease (a slam that bounces back), plain with reduced motion.</summary>
    float Back(float x)
    {
        x = Mathf.Clamp(x, 0f, 1f);
        if (_reduced) return Ease(x);
        const float c = 2.2f;
        return 1f + (c + 1f) * Mathf.Pow(x - 1f, 3f) + c * Mathf.Pow(x - 1f, 2f);
    }

    /// <summary>The fade of everything as the bubbles take over (1 → 0).</summary>
    float Fading => 1f - Mathf.Clamp((_t - Out - 0.25f) / (End - Out - 0.25f), 0f, 1f);

    /// <summary>The impact frame's jolt (screen pixels), none with reduced motion.</summary>
    Vector2 Shake
    {
        get
        {
            if (_reduced) return Vector2.Zero;
            float k = Mathf.Clamp(1f - (_t - Slam - 0.15f) / 0.35f, 0f, 1f);
            if (_t < Slam + 0.15f || k <= 0f) return Vector2.Zero;
            return new Vector2(Mathf.Sin(_t * 90f), Mathf.Cos(_t * 77f)) * 14f * k;
        }
    }

    /// <summary>The panels' corners: hers on the left, the boss's on the right, a slanted seam between.</summary>
    Vector2[] Shape(bool her)
    {
        var s = Size;
        var shake = Shake;
        if (her)
        {
            float slide = (1f - Back((_t - HerIn) / 0.45f)) * -s.X * 0.7f;
            return new[] { new Vector2(slide, -10f) + shake, new Vector2(s.X * 0.6f + slide, -10f) + shake, new Vector2(s.X * 0.46f + slide, s.Y + 10f) + shake, new Vector2(slide, s.Y + 10f) + shake };
        }
        float slam = (1f - Back((_t - Slam) / 0.3f)) * s.X * 0.75f;
        return new[] { new Vector2(s.X * 0.63f + slam, -10f) + shake, new Vector2(s.X + 10f + slam, -10f) + shake, new Vector2(s.X + 10f + slam, s.Y + 10f) + shake, new Vector2(s.X * 0.49f + slam, s.Y + 10f) + shake };
    }

    public override void _Draw()
    {
        if (_t < 0f) return;
        var s = Size;
        float a = Fading;
        // The wipe: dark water tinted by the depth (warm in the Kelp Jungle, cold below), speed lines sweeping across.
        float wipe = Ease(_t / 0.45f);
        var deep = new Color(0.06f, 0.1f, 0.16f).Lerp(new Color(0.03f, 0.03f, 0.07f), _menace);
        var tint = new Color(0.12f, 0.3f, 0.22f).Lerp(new Color(0.12f, 0.06f, 0.2f), _menace);
        float edge = s.X * 1.3f * wipe;
        DrawPolygon(new[] { new Vector2(-s.X * 0.3f, 0f), new Vector2(edge, 0f), new Vector2(edge - s.X * 0.3f, s.Y), new Vector2(-s.X * 0.3f, s.Y) },
            new[] { deep with { A = a }, tint with { A = a }, tint with { A = a }, deep with { A = a } });
        for (int i = 0; i < 40; i++)
        {
            float y = (i * 0.618f % 1f) * s.Y;
            float speed = 900f + 600f * (i * 0.37f % 1f);
            float x = (_t * speed + i * 211f) % (s.X * 1.6f) - s.X * 0.3f;
            if (x > edge) continue;
            DrawLine(new Vector2(x, y), new Vector2(x + 120f + 160f * (i * 0.29f % 1f), y - 30f), new Color(1f, 1f, 1f, 0.12f * a), 2f + (i % 3), true);
        }
    }

    /// <summary>A panel: its shape is the mask, filled with its backdrop; its character and lines drawn inside it.</summary>
    partial class Panel : Control
    {
        readonly BossIntro _o;
        readonly bool _her;

        public Panel(BossIntro owner, bool her)
        {
            _o = owner;
            _her = her;
            MouseFilter = MouseFilterEnum.Ignore;
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(new Scene(owner, her));
        }

        public override void _Draw()
        {
            var o = _o;
            if (o._t < 0f || (_her ? o._t < HerIn : o._t < Slam)) return;
            var pts = o.Shape(_her);
            float a = o.Fading;
            Color c0, c1;
            if (_her)
            {
                c0 = new Color(1f, 0.62f, 0.32f);
                c1 = new Color(1f, 0.86f, 0.55f);
            }
            else
            {
                var accent = new Color(o._spec.Accent.X, o._spec.Accent.Y, o._spec.Accent.Z);
                c0 = accent.Darkened(0.45f + 0.3f * o._menace);
                c1 = accent.Lerp(new Color(0.8f, 0.9f, 1f), 0.25f).Darkened(0.15f + 0.4f * o._menace);
            }
            DrawPolygon(pts, new[] { c1 with { A = a }, c0 with { A = a }, c0 with { A = a }, c1 with { A = a } });
        }
    }

    /// <summary>What a panel holds: radiating speed lines, halftone dots and its character.</summary>
    partial class Scene : Control
    {
        readonly BossIntro _o;
        readonly bool _her;

        public Scene(BossIntro owner, bool her)
        {
            _o = owner;
            _her = her;
            MouseFilter = MouseFilterEnum.Ignore;
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }

        public override void _Draw()
        {
            var o = _o;
            float t = o._t;
            if (t < 0f) return;
            var size = o.Size;
            var pts = o.Shape(_her);
            // The panel's own centre moves with it.
            var shift = pts[0] - (_her ? new Vector2(0f, -10f) : new Vector2(size.X * 0.63f, -10f));
            var c = (_her ? new Vector2(size.X * 0.22f, size.Y * 0.56f) : new Vector2(size.X * 0.8f, size.Y * 0.6f)) + shift;
            float a = o.Fading;
            // Speed lines radiating from the character, turning slowly.
            for (int i = 0; i < 36; i++)
            {
                float ang = i * Mathf.Tau / 36f + t * (_her ? 0.25f : -0.35f);
                var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                float r0 = size.Y * (0.32f + 0.08f * (i % 3));
                DrawLine(c + dir * r0, c + dir * size.X, new Color(1f, 1f, 1f, (i % 2 == 0 ? 0.14f : 0.07f) * a), 3f + 4f * (i % 2), true);
            }
            // Halftone dots in the far corner of the panel.
            for (int y = 0; y < 9; y++)
            for (int x = 0; x < 9; x++)
            {
                var at = (_her ? new Vector2(18f, size.Y - 20f) + new Vector2(x * 22f, -y * 22f) : new Vector2(size.X - 18f, 20f) + new Vector2(-x * 22f, y * 22f)) + shift;
                float r = 7f * (1f - (x + y) / 16f);
                if (r > 0.5f) DrawCircle(at, r, new Color(0f, 0f, 0f, 0.08f * a));
            }
            float scale = size.Y * (_her ? 0.2f : 0.19f);
            // The face-off: a slow push in.
            float push = 1f + 0.08f * Ease((t - Face) / 1.2f);
            if (_her) Her(c, scale * push, t, a);
            else Foe(c, scale * push, t, a);
        }

        void Her(Vector2 c, float s, float t, float a)
        {
            var o = _o;
            // She bobs in, spots it (her eyes snap right), double-takes with a sweat-drop and gulps, then sets her face.
            float bob = Mathf.Sin(t * 3f) * s * 0.08f;
            BossIntroArt.Mood mood = t < 1.15f ? BossIntroArt.Mood.Calm : t < 1.45f ? BossIntroArt.Mood.Shock : t < Face ? BossIntroArt.Mood.Gulp : BossIntroArt.Mood.Determined;
            float look = t < 1.0f ? 0f : 1f;
            // The double-take: a quick squash and a hop.
            float take = t > 1.15f && t < 1.45f ? Mathf.Sin((t - 1.15f) / 0.3f * Mathf.Pi) : 0f;
            DrawSetTransform(c + new Vector2(0f, bob - take * s * 0.35f), Mathf.Sin(t * 2f) * 0.06f, new Vector2(1f + take * 0.12f, 1f - take * 0.12f));
            BossIntroArt.Clementine(this, s, t, mood, look);
            if (t > 1.2f && t < Face + 0.2f)
            {
                float k = Mathf.Clamp((t - 1.2f) / 1.4f, 0f, 1f);
                BossIntroArt.SweatDrop(this, new Vector2(s * 0.95f, -s * 0.85f + k * s * 0.3f), s * 0.13f);
            }
            if (t > Face)
                for (int i = 0; i < 3; i++)
                    BossIntroArt.Sparkle(this, new Vector2(-s * 1.2f + s * 0.25f * i, -s * 1.1f + s * 0.3f * i), s * 0.14f * Mathf.Abs(Mathf.Sin(t * 4f + i)), Colors.White);
            DrawSetTransformMatrix(Transform2D.Identity);
        }

        void Foe(Vector2 c, float s, float t, float a)
        {
            var o = _o;
            float g = t - Slam;
            if (g < 0f) return;
            // It arrives big and settles (the slam), then plays its gag; at the face-off it taunts.
            float settle = o._reduced ? 1f : 1f + 0.25f * Mathf.Exp(-g * 6f) * Mathf.Cos(g * 18f);
            var mood = t < Face ? BossIntroArt.Mood.Calm : BossIntroArt.Mood.Taunt;
            float bob = Mathf.Sin(t * 2.4f + 1f) * s * 0.06f;
            // Its form's own entrance: the snail slides in, the sea star spins in place.
            float slide = o._spec.Form == BodyForm.Snail ? (1f - Ease(g / 1.4f)) * s * 2.2f : 0f;
            DrawSetTransform(c + new Vector2(slide, bob), 0f, Vector2.One * settle);
            BossIntroArt.Boss(this, o._spec, s, g, o._menace, mood, t);
            DrawSetTransformMatrix(Transform2D.Identity);
        }
    }

    /// <summary>Over the panels: their borders, the impact frame, the ribbons, the VS and the bubbles.</summary>
    partial class Overlay : Control
    {
        readonly BossIntro _o;

        public Overlay(BossIntro owner)
        {
            _o = owner;
            MouseFilter = MouseFilterEnum.Ignore;
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }

        public override void _Draw()
        {
            var o = _o;
            float t = o._t;
            if (t < 0f) return;
            var size = o.Size;
            float a = o.Fading;
            var font = OctoShoots.Game.Title.TitleStyle.Display;
            // Thick white borders with an ink edge.
            foreach (bool her in new[] { true, false })
            {
                if (her ? t < HerIn : t < Slam) continue;
                var p = o.Shape(her);
                var loop = new[] { p[0], p[1], p[2], p[3], p[0] };
                DrawPolyline(loop, new Color(0.05f, 0.03f, 0.08f, a), 16f, true);
                DrawPolyline(loop, new Color(1f, 1f, 1f, a), 9f, true);
            }
            // The impact frame: a white flash and a burst of rays from the boss (a soft glow with reduced motion).
            float hit = t - Slam - 0.2f;
            if (hit > 0f && hit < 0.3f)
            {
                float k = 1f - hit / 0.3f;
                DrawRect(new Rect2(Vector2.Zero, size), new Color(1f, 1f, 1f, (o._reduced ? 0.25f : 0.85f) * k * k));
                if (!o._reduced)
                {
                    var c = new Vector2(size.X * 0.8f, size.Y * 0.6f);
                    for (int i = 0; i < 18; i++)
                    {
                        float ang = i * Mathf.Tau / 18f;
                        var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                        var side = new Vector2(-dir.Y, dir.X) * 18f;
                        var from = c + dir * size.Y * 0.2f;
                        DrawColoredPolygon(new[] { from + side, from + dir * size.Y * (0.7f + 0.3f * (i % 2)), from - side }, new Color(1f, 0.97f, 0.85f, 0.7f * k));
                    }
                }
            }
            // The ribbons: her name, then the boss's.
            Ribbon(font, "CLEMENTINE", new Vector2(size.X * 0.05f, size.Y * 0.83f), t - 0.9f, new Color(1f, 0.55f, 0.26f), false, a);
            Ribbon(font, o._name, new Vector2(size.X * 0.97f, size.Y * 0.83f), t - Slam - 0.35f, new Color(o._spec.Accent.X, o._spec.Accent.Y, o._spec.Accent.Z).Darkened(0.2f + 0.3f * o._menace), true, a);
            // VS: it punches in on the seam and glows; warm and big at Depth 1, thin and cold by Depth 6.
            float vs = t - Face;
            if (vs > 0f)
            {
                float pop = o.Back(vs / 0.3f);
                int px = Mathf.RoundToInt(size.Y * (0.26f - 0.06f * o._menace) * Mathf.Max(pop, 0.05f));
                var col = new Color(1f, 0.82f, 0.4f).Lerp(new Color(0.75f, 0.9f, 1f), o._menace);
                var at = new Vector2(size.X * 0.545f, size.Y * 0.5f);
                var w = font.GetStringSize("VS", HorizontalAlignment.Left, -1, px);
                var pos = at + new Vector2(-w.X * 0.5f, px * 0.32f);
                float glow = 0.6f + 0.4f * Mathf.Sin(vs * 7f);
                for (int i = 4; i >= 1; i--)
                    DrawStringOutline(font, pos, "VS", HorizontalAlignment.Left, -1, px, 10 * i, col with { A = 0.12f * glow * a });
                DrawStringOutline(font, pos, "VS", HorizontalAlignment.Left, -1, px, 14, new Color(0.08f, 0.03f, 0.1f, a));
                DrawString(font, pos, "VS", HorizontalAlignment.Left, -1, px, col with { A = a });
                for (int i = 0; i < 6; i++)
                {
                    float ang = vs * 1.6f + i * Mathf.Tau / 6f;
                    BossIntroArt.Sparkle(this, at + new Vector2(Mathf.Cos(ang) * w.X * 0.75f, Mathf.Sin(ang) * px * 0.55f), px * 0.09f * (0.5f + 0.5f * Mathf.Sin(vs * 9f + i)), Colors.White with { A = a });
                }
            }
            // The way out: the panels burst into bubbles that swell and pop.
            float out_ = t - Out;
            if (out_ > 0f)
                foreach (var (at, delay, s) in o._bubbles)
                {
                    float k = (out_ - delay) / 0.4f;
                    if (k <= 0f || k >= 1f) continue;
                    var c = at * size;
                    float r = size.Y * s * (0.4f + 0.8f * k);
                    if (k < 0.8f)
                    {
                        DrawCircle(c, r, new Color(0.85f, 0.95f, 1f, 0.18f));
                        DrawArc(c, r, 0f, Mathf.Tau, 24, new Color(1f, 1f, 1f, 0.75f), 3f, true);
                        DrawCircle(c + new Vector2(-r * 0.35f, -r * 0.35f), r * 0.18f, new Color(1f, 1f, 1f, 0.8f));
                    }
                    else
                    {
                        float q = (k - 0.8f) / 0.2f;
                        for (int i = 0; i < 6; i++)
                        {
                            float ang = i * Mathf.Tau / 6f;
                            var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                            DrawLine(c + dir * r, c + dir * r * (1.2f + 0.3f * q), new Color(1f, 1f, 1f, 1f - q), 3f, true);
                        }
                    }
                }
        }

        /// <summary>A name on a slanted ribbon that sweeps in (from the left, or from the right ending at the anchor).</summary>
        void Ribbon(Font font, string text, Vector2 anchor, float since, Color col, bool fromRight, float a)
        {
            if (since <= 0f) return;
            var o = _o;
            float k = Ease(since / 0.35f);
            int px = Mathf.RoundToInt(o.Size.Y * (0.07f - 0.012f * (fromRight ? o._menace : 0f)));
            var w = font.GetStringSize(text, HorizontalAlignment.Left, -1, px);
            float width = w.X + px * 1.4f, height = px * 1.35f;
            var at = fromRight ? anchor - new Vector2(width * k, 0f) : anchor - new Vector2(width * (1f - k), 0f);
            float skew = height * 0.35f;
            var band = new[] { at + new Vector2(skew, 0f), at + new Vector2(width + skew, 0f), at + new Vector2(width, height), at + new Vector2(0f, height) };
            DrawColoredPolygon(band, new Color(0.05f, 0.03f, 0.08f, 0.85f * a));
            var inner = new[] { band[0] + new Vector2(4f, 4f), band[1] + new Vector2(-4f, 4f), band[2] + new Vector2(-4f, -4f), band[3] + new Vector2(4f, -4f) };
            DrawColoredPolygon(inner, col with { A = a });
            var pos = at + new Vector2(px * 0.7f + skew * 0.5f, height * 0.5f + px * 0.36f);
            DrawStringOutline(font, pos, text, HorizontalAlignment.Left, -1, px, 8, new Color(0.05f, 0.03f, 0.08f, a));
            DrawString(font, pos, text, HorizontalAlignment.Left, -1, px, new Color(1f, 1f, 1f, a));
        }
    }
}
