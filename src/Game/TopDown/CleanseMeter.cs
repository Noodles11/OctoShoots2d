using Godot;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The war at a glance (docs/CORRUPTION.md), under the minimap: how much of the level is cleansed, a gold bar over ink
/// with its tipping points marked (50%: budding slows; 75%: regrowth stops). On a boss level the arena too, with the
/// Crack's 60% mark and the boss's crust layers as pips.
/// </summary>
public partial class CleanseMeter : Control
{
    const float Width = 260f, Margin = 16f;
    static readonly Color Gold = new(1f, 0.78f, 0.36f);
    static readonly Color InkBar = new(0.07f, 0.04f, 0.1f, 0.85f);
    static readonly Color Text = new(0.95f, 0.92f, 0.85f);

    float _level, _arena, _shown, _shownArena, _flash;
    int _crust = -1;
    bool _hasArena;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.TopRight);
        OffsetLeft = -Width - Margin;
        OffsetRight = -Margin;
        OffsetTop = Margin + 260f + 46f;
        OffsetBottom = OffsetTop + 74f;
    }

    public void Track(PlaneWorld world, float dt)
    {
        Visible = world.Corruption is not null;
        if (!Visible) return;
        _level = world.Cleansed;
        _hasArena = world.ArenaArea is not null;
        _arena = world.ArenaCleansed;
        int crust = world.Boss?.Crust ?? 0;
        if (_crust >= 0 && crust < _crust) _flash = 1f;
        _crust = crust;
        // Eased, so the bar fills like light spreading.
        _shown = Mathf.Lerp(_shown, _level, 1f - Mathf.Exp(-6f * dt));
        _shownArena = Mathf.Lerp(_shownArena, _arena, 1f - Mathf.Exp(-6f * dt));
        _flash = Mathf.MoveToward(_flash, 0f, dt * 2f);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var font = ThemeDB.FallbackFont;
        Row(font, 0f, "REEF CLEANSED", _shown, new[] { CorruptionTuning.SlowBudding, CorruptionTuning.StopRegrowth });
        if (!_hasArena) return;
        Row(font, 36f, "ARENA", _shownArena, new[] { CorruptionTuning.ArenaToOpen });
        // The crust: one pip per layer still on the boss.
        for (int i = 0; i < CorruptionTuning.CrustLayers; i++)
        {
            var c = new Vector2(Width - 8f - i * 13f, 42f);
            bool on = i < _crust;
            DrawCircle(c, 5.2f, new Color(0f, 0f, 0f, 0.6f));
            DrawCircle(c, 4f, on ? new Color(0.16f, 0.06f, 0.26f) : new Color(Gold, 0.35f + 0.65f * _flash));
            if (on) DrawArc(c, 4f, 0f, Mathf.Tau, 16, new Color(0.55f, 0.25f, 0.9f, 0.9f), 1.2f, true);
        }
    }

    void Row(Font font, float y, string name, float value, float[] marks)
    {
        string pct = $"{Mathf.RoundToInt(value * 100f)}%";
        DrawString(font, new Vector2(0f, y + 13f), name, HorizontalAlignment.Left, -1, 13, Text);
        DrawString(font, new Vector2(name == "ARENA" ? 56f : Width - 40f, y + 13f), pct, HorizontalAlignment.Left, -1, 13, Gold);
        var bar = new Rect2(0f, y + 19f, Width, 7f);
        DrawRect(bar.Grow(1.5f), new Color(0f, 0f, 0f, 0.55f));
        DrawRect(bar, InkBar);
        // Gold over the ink, with a soft glow at its leading edge.
        var fill = new Rect2(bar.Position, new Vector2(bar.Size.X * Mathf.Clamp(value, 0f, 1f), bar.Size.Y));
        DrawRect(fill, Gold);
        if (value > 0.005f) DrawCircle(new Vector2(fill.End.X, bar.GetCenter().Y), 6f, new Color(1f, 0.85f, 0.5f, 0.35f));
        foreach (float m in marks)
        {
            float x = bar.Position.X + bar.Size.X * m;
            DrawLine(new Vector2(x, bar.Position.Y - 3f), new Vector2(x, bar.End.Y + 3f), value >= m ? new Color(1f, 0.95f, 0.8f) : new Color(0.7f, 0.5f, 0.95f), 1.5f);
        }
    }
}
