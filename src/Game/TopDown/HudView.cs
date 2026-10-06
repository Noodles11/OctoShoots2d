using System;
using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The plane HUD, bottom left: Clementine's HP bar (a lagging trail shows the damage just taken) and the pearls she
/// carries, one dot each in its own colours. A pearl's name and tagline show for a moment when she takes it.
/// </summary>
public partial class HudView : Control
{
    const float BarWidth = 300f, BarHeight = 18f, Margin = 18f;

    PlaneRun? _run;
    ItemCatalog? _catalog;
    float _hp, _max = 100f, _trail;
    string _caption = "";
    int _shells;
    /// <summary>1 when a shell has just been collected, easing back to 0: the counter brightens and swells.</summary>
    float _pulse;
    float _captionTimer;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    /// <summary>The run's elapsed play time (it stands still while paused), shown at the top centre.</summary>
    public float Elapsed;

    float _bossHp, _bossTrail, _bossShown;
    bool _bossFight;

    public void Track(PlaneWorld world, ItemCatalog? catalog, float dt)
    {
        // Queen Clam's bar: shown while the fight runs, fading out once she is freed.
        var boss = world.Boss;
        _bossFight = boss is { Stage: BossStage.Fight };
        _bossShown = Mathf.MoveToward(_bossShown, _bossFight ? 1f : 0f, dt * 2f);
        if (boss is not null)
        {
            _bossHp = boss.Hp / PlaneBossTuning.Hp;
            _bossTrail = _bossTrail < _bossHp ? _bossHp : Mathf.MoveToward(_bossTrail, _bossHp, dt * 0.4f);
        }
        _run = world.Run;
        _catalog = catalog;
        _hp = world.Player.Hp;
        _max = world.Run.MaxHp;
        // The trail eases down to the HP after a short wait; healing snaps it up.
        _trail = _trail < _hp ? _hp : Mathf.MoveToward(_trail, _hp, dt * 40f);
        _captionTimer -= dt;
        _shells = world.Run.Shells;
        _pulse = Mathf.MoveToward(_pulse, 0f, dt * 2.5f);
        QueueRedraw();
    }

    /// <summary>A shell was just collected: the counter flashes and pulses.</summary>
    public void PulseShells() => _pulse = 1f;

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

    /// <summary>A short message (a new room, "Not enough shells") shown where pearl captions show.</summary>
    public void ShowMessage(string text)
    {
        _caption = text;
        _captionTimer = 2.5f;
    }

    /// <summary>Announces a pearl just taken.</summary>
    public void ShowPearl(string itemId)
    {
        if (_catalog is null || !_catalog.TryGet(itemId, out var item)) return;
        _caption = $"{item.Name} — {item.Tagline}";
        _captionTimer = 4f;
    }

    public override void _Draw()
    {
        var font = ThemeDB.FallbackFont;
        var at = new Vector2(Margin, Size.Y - Margin - BarHeight);

        // HP bar.
        float k = Mathf.Clamp(_hp / _max, 0f, 1f), trail = Mathf.Clamp(_trail / _max, 0f, 1f);
        DrawRect(new Rect2(at - Vector2.One * 3f, new Vector2(BarWidth + 6f, BarHeight + 6f)), new Color(0f, 0f, 0f, 0.55f));
        DrawRect(new Rect2(at, new Vector2(BarWidth, BarHeight)), new Color(0.12f, 0.05f, 0.07f, 0.9f));
        DrawRect(new Rect2(at, new Vector2(BarWidth * trail, BarHeight)), new Color(1f, 0.85f, 0.7f, 0.85f));
        var fill = k > 0.3f ? new Color(0.95f, 0.35f, 0.38f) : new Color(1f, 0.2f, 0.2f).Lerp(new Color(1f, 0.6f, 0.6f), 0.5f + 0.5f * Mathf.Sin((float)Time.GetTicksMsec() * 0.012f));
        DrawRect(new Rect2(at, new Vector2(BarWidth * k, BarHeight)), fill);
        DrawRect(new Rect2(at, new Vector2(BarWidth, BarHeight * 0.35f)), new Color(1f, 1f, 1f, 0.12f));
        DrawString(font, at + new Vector2(8f, BarHeight - 4f), $"{Mathf.CeilToInt(_hp)} / {Mathf.RoundToInt(_max)}", HorizontalAlignment.Left, -1, 14, Colors.White);

        // The run's clock, top centre.
        var clock = TimeSpan.FromSeconds(Elapsed);
        string text = clock.TotalHours >= 1 ? $"{(int)clock.TotalHours}:{clock.Minutes:00}:{clock.Seconds:00}" : $"{clock.Minutes}:{clock.Seconds:00}";
        var tsize = font.GetStringSize(text, HorizontalAlignment.Left, -1, 16);
        var tat = new Vector2((Size.X - tsize.X) * 0.5f, Margin + 4f);
        DrawRect(new Rect2(tat - new Vector2(10f, 4f), tsize + new Vector2(20f, 8f)), new Color(0f, 0f, 0f, 0.35f));
        DrawString(font, tat + new Vector2(0f, tsize.Y - 4f), text, HorizontalAlignment.Left, -1, 16, new Color(1f, 1f, 1f, 0.9f));

        // Queen Clam's health, under the clock.
        if (_bossShown > 0.01f)
        {
            const float bw = 460f, bh = 14f;
            var b = new Vector2((Size.X - bw) * 0.5f, Margin + 52f);
            float a = _bossShown;
            string name = PlaneBossTuning.Name.ToUpperInvariant();
            var ns = font.GetStringSize(name, HorizontalAlignment.Left, -1, 15);
            DrawStringOutline(font, new Vector2((Size.X - ns.X) * 0.5f, b.Y - 6f), name, HorizontalAlignment.Left, -1, 15, 4, new Color(0f, 0f, 0f, 0.7f * a));
            DrawString(font, new Vector2((Size.X - ns.X) * 0.5f, b.Y - 6f), name, HorizontalAlignment.Left, -1, 15, new Color(1f, 0.93f, 0.85f, a));
            DrawRect(new Rect2(b - Vector2.One * 3f, new Vector2(bw + 6f, bh + 6f)), new Color(0f, 0f, 0f, 0.55f * a));
            DrawRect(new Rect2(b, new Vector2(bw, bh)), new Color(0.08f, 0.1f, 0.14f, 0.9f * a));
            DrawRect(new Rect2(b, new Vector2(bw * Mathf.Clamp(_bossTrail, 0f, 1f), bh)), new Color(1f, 0.9f, 0.7f, 0.8f * a));
            DrawRect(new Rect2(b, new Vector2(bw * Mathf.Clamp(_bossHp, 0f, 1f), bh)), new Color(0.35f, 0.8f, 0.78f, a));
            DrawRect(new Rect2(b, new Vector2(bw, bh * 0.35f)), new Color(1f, 1f, 1f, 0.12f * a));
            // The phase mark at half.
            DrawRect(new Rect2(b + new Vector2(bw * 0.5f - 1f, -2f), new Vector2(2f, bh + 4f)), new Color(0f, 0f, 0f, 0.7f * a));
        }

        // Her pearls, above the bar.
        if (_run is not null && _catalog is not null)
        {
            var dot = at + new Vector2(10f, -18f);
            foreach (string id in _run.Items)
            {
                if (!_catalog.TryGet(id, out var item) || item.Pearl is null) continue;
                Color a = new(item.Pearl.Colors[0]), b = new(item.Pearl.Colors[^1]);
                DrawCircle(dot, 9.5f, new Color(0f, 0f, 0f, 0.6f));
                DrawCircle(dot, 8f, b);
                DrawCircle(dot - new Vector2(1.5f, 1.5f), 5.5f, a);
                DrawCircle(dot - new Vector2(3f, 3f), 2f, new Color(1f, 1f, 1f, 0.8f));
                dot.X += 24f;
            }
        }

        // Shells, above the pearls: brightens and swells with each one collected.
        {
            float k2 = _pulse * _pulse;
            var c = at + new Vector2(12f, -46f);
            float size = 14f * (1f + 0.35f * k2);
            if (k2 > 0.01f) DrawCircle(c, size * 1.5f, new Color(1f, 0.8f, 0.55f, 0.4f * k2));
            DrawShell(c, size, new Color(1f, 0.85f, 0.75f).Lerp(new Color(1f, 1f, 0.9f), k2), new Color(0.35f, 0.18f, 0.15f));
            int fontSize = (int)(22f * (1f + 0.3f * k2));
            var col = new Color(1f, 0.9f, 0.8f).Lerp(new Color(1f, 1f, 0.75f), k2);
            DrawString(font, c + new Vector2(22f, 8f + 3f * k2), _shells.ToString(), HorizontalAlignment.Left, -1, fontSize, col);
        }

        if (_captionTimer > 0f)
        {
            float alpha = Mathf.Clamp(_captionTimer, 0f, 1f);
            DrawString(font, at + new Vector2(0f, -72f), _caption, HorizontalAlignment.Left, -1, 20, new Color(1f, 0.93f, 0.8f, alpha));
        }
    }
}
