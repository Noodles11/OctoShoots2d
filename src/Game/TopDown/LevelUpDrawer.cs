using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The level-up (DESIGN-TOPDOWN §2.6): a drawer that slides in from the right, over the reef, while the camera holds
/// close on Clementine at the left. It lists the modifiers a skill point can go to, each with her value now (as the
/// pause menu's stat sheet shows it), what a point adds and how many points it holds; she picks one (a click, or a
/// controller's press). Once her points are spent it slides away. Then a prompt waits for any key to play on.
/// </summary>
public partial class LevelUpDrawer : Control
{
    /// <summary>A modifier was picked.</summary>
    public event Action<Stat>? Chosen;

    const float Width = 460f, SlideSeconds = 0.35f;

    static readonly Color Gold = new(1f, 0.82f, 0.45f);
    static readonly Color Soft = new(0.75f, 0.85f, 0.88f);

    /// <summary>The stat sheet's name for each modifier (its values are read from there).</summary>
    static readonly Dictionary<Stat, string> SheetName = new()
    {
        [Stat.MaxHp] = "Max HP", [Stat.Damage] = "Damage per shot", [Stat.FireRate] = "Fire rate", [Stat.ShotSpeed] = "Shot speed",
        [Stat.Range] = "Range", [Stat.ShotSize] = "Shot size", [Stat.Speed] = "Swim speed", [Stat.DashCooldown] = "Dash cooldown", [Stat.Glow] = "Glow",
    };

    PanelContainer _panel = null!;
    VBoxContainer _rows = null!;
    Label _title = null!, _hint = null!, _prompt = null!;
    float _open, _promptAlpha;
    bool _opening, _waiting;

    /// <summary>Fully slid away (and not waiting on a key).</summary>
    public bool Closed => !_opening && _open <= 0f;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;

        _panel = new PanelContainer { CustomMinimumSize = new Vector2(Width, 0f), MouseFilter = MouseFilterEnum.Stop };
        _panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.03f, 0.07f, 0.09f, 0.94f),
            BorderColor = new Color(Gold, 0.6f),
            BorderWidthLeft = 3,
            CornerRadiusTopLeft = 14, CornerRadiusBottomLeft = 14,
            ContentMarginLeft = 30, ContentMarginRight = 26, ContentMarginTop = 40, ContentMarginBottom = 30,
            ShadowColor = new Color(0f, 0f, 0f, 0.5f), ShadowSize = 18,
        });
        AddChild(_panel);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 10);
        _panel.AddChild(box);
        _title = new Label();
        _title.AddThemeFontSizeOverride("font_size", 36);
        _title.AddThemeColorOverride("font_color", Gold);
        box.AddChild(_title);
        _hint = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _hint.AddThemeFontSizeOverride("font_size", 17);
        _hint.AddThemeColorOverride("font_color", Soft);
        box.AddChild(_hint);
        box.AddChild(new HSeparator());
        _rows = new VBoxContainer();
        _rows.AddThemeConstantOverride("separation", 6);
        box.AddChild(_rows);

        // After the drawer: any key plays on.
        _prompt = new Label
        {
            Text = "Press any key to play on",
            HorizontalAlignment = HorizontalAlignment.Center,
            AnchorLeft = 0f, AnchorRight = 1f, AnchorTop = 0.78f, AnchorBottom = 0.78f,
            OffsetTop = -20f, OffsetBottom = 20f,
            Modulate = new Color(1f, 1f, 1f, 0f),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _prompt.AddThemeFontSizeOverride("font_size", 26);
        _prompt.AddThemeColorOverride("font_color", Gold);
        _prompt.AddThemeConstantOverride("outline_size", 8);
        _prompt.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.85f));
        AddChild(_prompt);
        Place();
    }

    /// <summary>Slides in, listing her modifiers (focus on the first for a controller).</summary>
    public void Open(PlaneRun run, bool pad)
    {
        _opening = true;
        Fill(run);
        if (pad) CallDeferred(MethodName.FocusFirst);
    }

    /// <summary>Lists her modifiers again (after a point, while more are left).</summary>
    public void Fill(PlaneRun run)
    {
        _title.Text = $"Level {run.XpLevel}!";
        _hint.Text = run.Points == 1 ? "Choose a modifier to strengthen: 1 skill point." : $"Choose a modifier to strengthen: {run.Points} skill points.";
        foreach (var child in _rows.GetChildren()) child.QueueFree();
        var sheet = PlaneStatSheet.Of(run).ToDictionary(l => l.Name, l => l.Value);
        foreach (var skill in Skills.All(run.Tuning))
        {
            int held = run.Skills.GetValueOrDefault(skill.Stat);
            var button = new Button { CustomMinimumSize = new Vector2(0f, 52f), FocusMode = FocusModeEnum.All };
            var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            row.OffsetLeft = 14f;
            row.OffsetRight = -14f;
            row.AddThemeConstantOverride("separation", 10);
            button.AddChild(row);
            var name = new Label { Text = skill.Name, SizeFlagsHorizontal = SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
            name.AddThemeFontSizeOverride("font_size", 19);
            row.AddChild(name);
            if (held > 0)
            {
                var dots = new Label { Text = new string('●', Math.Min(held, 8)) + (held > 8 ? $"+{held - 8}" : ""), VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
                dots.AddThemeFontSizeOverride("font_size", 12);
                dots.AddThemeColorOverride("font_color", Gold);
                row.AddChild(dots);
            }
            var value = new Label { Text = sheet.GetValueOrDefault(SheetName[skill.Stat], ""), VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
            value.AddThemeFontSizeOverride("font_size", 17);
            value.AddThemeColorOverride("font_color", Soft);
            row.AddChild(value);
            var gain = new Label { Text = skill.Gain, CustomMinimumSize = new Vector2(52f, 0f), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
            gain.AddThemeFontSizeOverride("font_size", 17);
            gain.AddThemeColorOverride("font_color", Gold);
            row.AddChild(gain);
            var stat = skill.Stat;
            button.Pressed += () => Chosen?.Invoke(stat);
            _rows.AddChild(button);
        }
    }

    public void FocusFirst()
    {
        if (_rows.GetChildCount() > 0 && _rows.GetChild(0) is Control first) first.GrabFocus();
    }

    /// <summary>Slides away.</summary>
    public void Close() => _opening = false;

    /// <summary>The prompt to play on, shown or hidden.</summary>
    public bool Waiting
    {
        get => _waiting;
        set => _waiting = value;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _open = Mathf.MoveToward(_open, _opening ? 1f : 0f, dt / SlideSeconds);
        _promptAlpha = Mathf.MoveToward(_promptAlpha, _waiting ? 1f : 0f, dt * 4f);
        // The prompt breathes while it waits.
        float breathe = 0.75f + 0.25f * Mathf.Sin((float)Time.GetTicksMsec() * 0.004f);
        _prompt.Modulate = new Color(1f, 1f, 1f, _promptAlpha * breathe);
        _panel.Visible = _open > 0f;
        Visible = _open > 0f || _promptAlpha > 0f;
        Place();
    }

    void Place()
    {
        var size = GetViewportRect().Size;
        float e = 1f - Mathf.Pow(1f - _open, 3f);
        _panel.Position = new Vector2(size.X - Width * e, 0f);
        _panel.Size = new Vector2(Width, size.Y);
    }
}
