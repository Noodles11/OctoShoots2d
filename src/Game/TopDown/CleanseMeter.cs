using Godot;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The war at a glance (docs/CORRUPTION.md), under the minimap and as wide as it: how much of the level is cleansed, a
/// slim gold bar over ink with its tipping points notched (50%: budding slows; 75%: regrowth stops). No caption at rest:
/// while it moves, its name and percentage show beside it and fade a moment after it settles. Inside a boss's arena a
/// second bar under it, the arena's, notched at the Crack's 60% (the boss's crust is on the boss's own bar).
/// </summary>
public partial class CleanseMeter : Control
{
    const float Width = MinimapView.Diameter, BarHeight = 5f, Gap = 14f;
    static readonly Color Gold = new(1f, 0.78f, 0.36f);
    static readonly Color InkBar = new(0.07f, 0.04f, 0.1f, 0.85f);
    static readonly Color Text = new(0.95f, 0.92f, 0.85f);
    /// <summary>How long a bar's caption stays after it last moved, and how fast it fades (seconds).</summary>
    const float CaptionHold = 1.6f, CaptionFade = 0.6f;

    float _level, _arena, _shown, _shownArena, _arenaIn;
    float _levelSince = 99f, _arenaSince = 99f;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.TopRight);
        OffsetLeft = -Width - MinimapView.Margin;
        OffsetRight = -MinimapView.Margin;
        // Clear of the experience ring round the map.
        OffsetTop = MinimapView.Margin + MinimapView.Diameter + 22f;
        OffsetBottom = OffsetTop + 50f;
    }

    public void Track(PlaneWorld world, float dt)
    {
        Visible = world.Corruption is not null && !world.Options.Adventure;
        if (!Visible) return;
        _level = world.Cleansed;
        _arena = world.ArenaCleansed;
        // The arena's bar only while she is in it.
        bool inArena = world.ArenaArea is not null && System.Numerics.Vector2.Distance(world.Player.Position, world.ArenaCenter) < world.ArenaRadius;
        _arenaIn = Mathf.MoveToward(_arenaIn, inArena ? 1f : 0f, dt * 3f);
        // Eased, so the bar fills like light spreading; a caption while it moves.
        float was = _shown, wasArena = _shownArena;
        _shown = Mathf.Lerp(_shown, _level, 1f - Mathf.Exp(-6f * dt));
        _shownArena = Mathf.Lerp(_shownArena, _arena, 1f - Mathf.Exp(-6f * dt));
        _levelSince = Mathf.Abs(_shown - was) > 1e-4f ? 0f : _levelSince + dt;
        _arenaSince = Mathf.Abs(_shownArena - wasArena) > 1e-4f ? 0f : _arenaSince + dt;
        QueueRedraw();
    }

    static float Caption(float since) => 1f - Mathf.Clamp((since - CaptionHold) / CaptionFade, 0f, 1f);

    public override void _Draw()
    {
        var font = ThemeDB.FallbackFont;
        Row(font, 0f, "Reef", _shown, Caption(_levelSince), 1f, new[] { CorruptionTuning.SlowBudding, CorruptionTuning.StopRegrowth });
        if (_arenaIn > 0.01f) Row(font, Gap, "Arena", _shownArena, Caption(_arenaSince), _arenaIn, new[] { CorruptionTuning.ArenaToOpen });
    }

    void Row(Font font, float y, string name, float value, float caption, float alpha, float[] marks)
    {
        var bar = new Rect2(0f, y, Width, BarHeight);
        DrawRect(bar.Grow(1.5f), new Color(0f, 0f, 0f, 0.55f * alpha));
        DrawRect(bar, InkBar with { A = InkBar.A * alpha });
        // Gold over the ink, with a soft glow at its leading edge.
        var fill = new Rect2(bar.Position, new Vector2(bar.Size.X * Mathf.Clamp(value, 0f, 1f), bar.Size.Y));
        DrawRect(fill, Gold with { A = alpha });
        if (value > 0.005f) DrawCircle(new Vector2(fill.End.X, bar.GetCenter().Y), 5f, new Color(1f, 0.85f, 0.5f, 0.35f * alpha));
        foreach (float m in marks)
        {
            float x = bar.Position.X + bar.Size.X * m;
            var notch = value >= m ? new Color(1f, 0.95f, 0.8f, alpha) : new Color(0.7f, 0.5f, 0.95f, alpha);
            DrawLine(new Vector2(x, bar.Position.Y - 2f), new Vector2(x, bar.End.Y + 2f), notch, 1.5f);
        }
        // While it moves: its name and how far it has come, just left of the bar.
        float a = caption * alpha;
        if (a <= 0.01f) return;
        string text = $"{name} {Mathf.RoundToInt(value * 100f)}%";
        var ts = font.GetStringSize(text, HorizontalAlignment.Left, -1, 15);
        var at = new Vector2(-ts.X - 8f, y + BarHeight * 0.5f + 5f);
        DrawStringOutline(font, at, text, HorizontalAlignment.Left, -1, 15, 4, new Color(0f, 0f, 0f, 0.7f * a));
        DrawString(font, at, text, HorizontalAlignment.Left, -1, 15, Text with { A = a });
    }
}
