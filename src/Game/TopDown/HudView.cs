using System;
using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The plane HUD, bottom left: her shells (the pearls she carries circle her bell: PearlOrbitView). Each shell she
/// picks up flies from where she took it to the counter as a wisp of light on a slight arc, and counts in there with a
/// pulse, so a pickup is never missed. A pearl's name
/// and tagline show for a moment when she takes it. Over the shaft, a prompt: Shift to dive, or why she cannot yet.
/// Her health and her active pearl's charge are not here: they are on her bell (BellView, DESIGN-TOPDOWN §2.5).
/// </summary>
public partial class HudView : Control
{
    const float Margin = 18f;

    PlaneRun? _run;
    int _shells;
    /// <summary>1 when a shell has just reached the counter, easing back to 0: the counter brightens and swells.</summary>
    float _pulse;
    /// <summary>1 as a shell lands, easing to 0: a ring of light spreading from the counter.</summary>
    float _ring;
    /// <summary>
    /// Shells on their way to the counter: where each set off (on screen), which way its arc bows, and how far along it is
    /// (0 → 1). The counter shows them only once they land.
    /// </summary>
    readonly List<(Vector2 From, float Side, float T)> _flights = new();
    /// <summary>A flight's length (s): brief, so it does not get in the way, quick enough to read as a flick.</summary>
    const float FlightTime = 0.55f;
    int _side = 1;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    /// <summary>The run's elapsed play time (it stands still while paused), shown at the top centre.</summary>
    public float Elapsed;

    /// <summary>Where the run stands ("Depth 1 · Level 2"), always shown beside the clock.</summary>
    public string LevelTitle = "";

    float _bossHp, _bossTrail, _bossShown, _bossFlash, _bossHold;
    string _bossName = "";
    int _bossCrust;
    bool _bossFight;
    string _prompt = "";
    bool _promptReady;
    float _promptShown;

    public void Track(PlaneWorld world, ItemCatalog? catalog, float dt)
    {
        // Queen Clam's bar: shown while the fight runs, fading out once she is freed.
        var boss = world.Boss;
        if (boss is not null) _bossName = boss.Spec.Name.ToUpperInvariant();
        _bossFight = boss is { Stage: BossStage.Fight };
        _bossShown = Mathf.MoveToward(_bossShown, _bossFight ? 1f : 0f, dt * 2f);
        if (boss is not null)
        {
            float hp = boss.Hp / Mathf.Max(boss.MaxHp, 1f);
            // A hit flashes the bar; what it took stays pale for a moment (chip damage adds up where it can be seen),
            // then drains away.
            if (hp < _bossHp - 1e-5f)
            {
                _bossFlash = 1f;
                _bossHold = 0.7f;
            }
            _bossHp = hp;
            _bossHold -= dt;
            _bossTrail = _bossTrail < _bossHp ? _bossHp : _bossHold > 0f ? _bossTrail : Mathf.MoveToward(_bossTrail, _bossHp, dt * 0.25f);
            _bossFlash = Mathf.MoveToward(_bossFlash, 0f, dt * 5f);
            _bossCrust = boss.Crust;
        }
        _run = world.Run;
        _shells = world.Run.Shells;
        _pulse = Mathf.MoveToward(_pulse, 0f, dt * 2.5f);
        _ring = Mathf.MoveToward(_ring, 0f, dt * 3f);
        for (int i = _flights.Count - 1; i >= 0; i--)
        {
            var f = _flights[i];
            f.T += dt / FlightTime;
            if (f.T >= 1f)
            {
                // Landed: it counts in, with a pulse.
                _flights.RemoveAt(i);
                _pulse = 1f;
                _ring = 1f;
            }
            else _flights[i] = f;
        }
        // Over the shaft: how to dive, or why not yet.
        bool over = world.OverShaft && !world.Defeated && !Diving;
        if (over)
        {
            _promptReady = world.CanDive;
            _prompt = world.CanDive ? "dive" : !world.ExitOpen ? "The Crack is sealed" : "Not while fighting";
        }
        _promptShown = Mathf.MoveToward(_promptShown, over ? 1f : 0f, dt * 4f);
        QueueRedraw();
    }

    /// <summary>She is diving: no prompt.</summary>
    public bool Diving { get; set; }

    /// <summary>A shell was just collected at that point of the screen: it flies to the counter.</summary>
    public void CollectShell(Vector2 from)
    {
        // Alternate the arcs' bow, so a handful taken at once fans out instead of stacking up.
        _flights.Add((from, _side * (0.7f + 0.3f * GD.Randf()), 0f));
        _side = -_side;
    }

    /// <summary>The middle of the counter's shell icon.</summary>
    Vector2 ShellIcon => new(Margin + 12f, Size.Y - Margin - 22f);

    /// <summary>Where a flight is at progress e (0 → 1): a quadratic arc from its start to the counter, bowed to one side.</summary>
    Vector2 FlightAt(Vector2 from, float side, float e)
    {
        var to = ShellIcon;
        var d = to - from;
        var bow = new Vector2(-d.Y, d.X) * 0.22f * side;
        var mid = (from + to) * 0.5f + bow;
        float u = 1f - e;
        return u * u * from + 2f * u * e * mid + e * e * to;
    }

    /// <summary>A flight's progress along its arc: it leaps off, then speeds in to the counter.</summary>
    static float FlightEase(float t) => t * t * (3f - 2f * t) * 0.6f + t * t * 0.4f;

    /// <summary>A shell icon: a scallop fan with ribs, hinge down.</summary>
    void DrawShell(Vector2 c, float size, Color fill, Color ink)
    {
        var hinge = c + new Vector2(0f, size * 0.55f);
        var rim = new List<Vector2>();
        for (int i = 0; i <= 12; i++)
        {
            float a = Mathf.Pi * (1.15f + 0.7f * i / 12f);
            float bump = 1f + 0.06f * Mathf.Cos(i * Mathf.Pi);
            rim.Add(hinge + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * size * 1.05f * bump);
        }
        var poly = new List<Vector2> { hinge };
        poly.AddRange(rim);
        DrawColoredPolygon(poly.ToArray(), fill);
        for (int i = 2; i <= 10; i += 2) DrawLine(hinge, rim[i], ink, 1.2f, true);
        poly.Add(hinge);
        DrawPolyline(poly.ToArray(), ink, 1.5f, true);
        DrawRect(new Rect2(hinge - new Vector2(size * 0.28f, size * 0.12f), new Vector2(size * 0.56f, size * 0.22f)), fill);
    }

    public override void _Draw()
    {
        var font = ThemeDB.FallbackFont;
        // The bottom-left corner the pearls and shells stack up from.
        var at = new Vector2(Margin, Size.Y - Margin - 4f);

        // The run's clock, top centre.
        var clock = TimeSpan.FromSeconds(Elapsed);
        string text = clock.TotalHours >= 1 ? $"{(int)clock.TotalHours}:{clock.Minutes:00}:{clock.Seconds:00}" : $"{clock.Minutes}:{clock.Seconds:00}";
        var tsize = font.GetStringSize(text, HorizontalAlignment.Left, -1, 16);
        var tat = new Vector2((Size.X - tsize.X) * 0.5f, Margin + 4f);
        DrawRect(new Rect2(tat - new Vector2(10f, 4f), tsize + new Vector2(20f, 8f)), new Color(0f, 0f, 0f, 0.35f));
        DrawString(font, tat + new Vector2(0f, tsize.Y - 4f), text, HorizontalAlignment.Left, -1, 16, new Color(1f, 1f, 1f, 0.9f));
        // Where the run stands, always on, just right of the clock.
        if (LevelTitle.Length > 0)
        {
            var lsize = font.GetStringSize(LevelTitle, HorizontalAlignment.Left, -1, 16);
            var lat = new Vector2(tat.X + tsize.X + 26f, tat.Y);
            DrawRect(new Rect2(lat - new Vector2(10f, 4f), lsize + new Vector2(20f, 8f)), new Color(0f, 0f, 0f, 0.35f));
            DrawString(font, lat + new Vector2(0f, lsize.Y - 4f), LevelTitle, HorizontalAlignment.Left, -1, 16, new Color(1f, 0.93f, 0.82f, 0.9f));
        }

        // The boss's health, under the clock.
        if (_bossShown > 0.01f)
        {
            const float bw = 460f, bh = 14f;
            var b = new Vector2((Size.X - bw) * 0.5f, Margin + 52f);
            float a = _bossShown;
            string name = _bossName;
            var ns = font.GetStringSize(name, HorizontalAlignment.Left, -1, 15);
            DrawStringOutline(font, new Vector2((Size.X - ns.X) * 0.5f, b.Y - 6f), name, HorizontalAlignment.Left, -1, 15, 4, new Color(0f, 0f, 0f, 0.7f * a));
            DrawString(font, new Vector2((Size.X - ns.X) * 0.5f, b.Y - 6f), name, HorizontalAlignment.Left, -1, 15, new Color(1f, 0.93f, 0.85f, a));
            DrawRect(new Rect2(b - Vector2.One * 3f, new Vector2(bw + 6f, bh + 6f)), new Color(0f, 0f, 0f, 0.55f * a));
            DrawRect(new Rect2(b, new Vector2(bw, bh)), new Color(0.08f, 0.1f, 0.14f, 0.9f * a));
            DrawRect(new Rect2(b, new Vector2(bw * Mathf.Clamp(_bossTrail, 0f, 1f), bh)), new Color(1f, 0.9f, 0.7f, 0.8f * a));
            DrawRect(new Rect2(b, new Vector2(bw * Mathf.Clamp(_bossHp, 0f, 1f), bh)), new Color(0.35f, 0.8f, 0.78f, a));
            // Her crust (each layer soaks up a sixth of every hit until the arena is cleansed): an ink film over the bar,
            // and a pip per layer at its right end.
            if (_bossCrust > 0)
            {
                DrawRect(new Rect2(b, new Vector2(bw * Mathf.Clamp(_bossHp, 0f, 1f), bh)), new Color(0.3f, 0.12f, 0.5f, 0.09f * _bossCrust * a));
                for (int i = 0; i < _bossCrust; i++)
                {
                    var pip = b + new Vector2(bw + 14f + i * 13f, bh * 0.5f);
                    DrawCircle(pip, 5f, new Color(0.05f, 0.02f, 0.09f, 0.95f * a));
                    DrawCircle(pip, 3.2f, new Color(0.55f, 0.3f, 0.85f, a));
                }
            }
            DrawRect(new Rect2(b, new Vector2(bw * Mathf.Clamp(_bossHp, 0f, 1f), bh)), new Color(1f, 1f, 1f, 0.55f * _bossFlash * a));
            DrawRect(new Rect2(b, new Vector2(bw, bh * 0.35f)), new Color(1f, 1f, 1f, 0.12f * a));
            // The phase mark at half.
            DrawRect(new Rect2(b + new Vector2(bw * 0.5f - 1f, -2f), new Vector2(2f, bh + 4f)), new Color(0f, 0f, 0f, 0.7f * a));
        }

        // Shells, bottom left (her pearls circle her bell instead: PearlOrbitView): brightens and swells with each one
        // collected.
        {
            float k2 = _pulse * _pulse;
            var c = ShellIcon;
            float size = 14f * (1f + 0.35f * k2);
            if (k2 > 0.01f) DrawCircle(c, size * 1.5f, new Color(1f, 0.8f, 0.55f, 0.4f * k2));
            DrawShell(c, size, new Color(1f, 0.85f, 0.75f).Lerp(new Color(1f, 1f, 0.9f), k2), new Color(0.35f, 0.18f, 0.15f));
            int fontSize = (int)(22f * (1f + 0.3f * k2));
            var col = new Color(1f, 0.9f, 0.8f).Lerp(new Color(1f, 1f, 0.75f), k2);
            if (_ring > 0.01f) DrawArc(c, size * (1.3f + 2.2f * (1f - _ring)), 0f, Mathf.Tau, 40, new Color(1f, 0.92f, 0.7f, 0.8f * _ring * _ring), 2.5f * _ring + 0.5f, true);
            // Shells still in flight are not counted yet.
            int shown = Mathf.Max(_shells - _flights.Count, 0);
            DrawString(font, c + new Vector2(22f, 8f + 3f * k2), shown.ToString(), HorizontalAlignment.Left, -1, fontSize, col);
        }

        // Shells in flight: a wisp of warm light with a short tail, the shell at its head, shrinking a little as it nears
        // the counter.
        foreach (var (from, side, t) in _flights)
        {
            float e = FlightEase(t);
            const int tail = 16;
            for (int j = tail; j >= 1; j--)
            {
                float back = FlightEase(Mathf.Max(t - j * 0.02f, 0f));
                float fade = 1f - j / (float)(tail + 1);
                var pt = FlightAt(from, side, back);
                // A soft shade under the gold, so the wisp reads on bright sand as well as in the dark.
                DrawCircle(pt, 3f + 7f * fade, new Color(0.25f, 0.12f, 0f, 0.12f * fade));
                DrawCircle(pt, 2f + 5.5f * fade, new Color(1f, 0.72f, 0.25f, 0.65f * fade * fade));
            }
            var head = FlightAt(from, side, e);
            // A pop as it leaves her: swelling in over the first tenth.
            float pop = Mathf.Clamp(t / 0.1f, 0f, 1f);
            float s2 = Mathf.Lerp(18f, 11f, e) * (0.5f + 0.5f * pop);
            DrawCircle(head, s2 * 1.9f, new Color(0.25f, 0.12f, 0f, 0.14f));
            DrawCircle(head, s2 * 1.6f, new Color(1f, 0.72f, 0.25f, 0.5f));
            DrawCircle(head, s2 * 1.1f, new Color(1f, 0.95f, 0.75f, 0.8f));
            DrawShell(head, s2, new Color(1f, 0.93f, 0.85f), new Color(0.35f, 0.18f, 0.15f));
        }

        // The dive prompt, low in the middle: a key cap and a word.
        if (_promptShown > 0.01f)
        {
            float a = _promptShown;
            const int fs = 18;
            string key = "SHIFT";
            var ks = font.GetStringSize(key, HorizontalAlignment.Left, -1, 14);
            var ws = font.GetStringSize(_prompt, HorizontalAlignment.Left, -1, fs);
            float w = (_promptReady ? ks.X + 22f : 0f) + ws.X + 28f;
            var p = new Vector2((Size.X - w) * 0.5f, Size.Y * 0.7f);
            DrawRect(new Rect2(p, new Vector2(w, 34f)), new Color(0.02f, 0.05f, 0.08f, 0.6f * a));
            float x = p.X + 14f;
            if (_promptReady)
            {
                var cap = new Rect2(new Vector2(x - 4f, p.Y + 7f), new Vector2(ks.X + 8f, 20f));
                DrawRect(cap, new Color(1f, 0.93f, 0.82f, 0.9f * a));
                DrawString(font, new Vector2(x, p.Y + 22f), key, HorizontalAlignment.Left, -1, 14, new Color(0.15f, 0.1f, 0.08f, a));
                x += ks.X + 22f;
            }
            var col = _promptReady ? new Color(1f, 0.93f, 0.8f, a) : new Color(0.85f, 0.85f, 0.9f, 0.75f * a);
            DrawString(font, new Vector2(x, p.Y + 24f), _prompt, HorizontalAlignment.Left, -1, fs, col);
        }
    }
}
