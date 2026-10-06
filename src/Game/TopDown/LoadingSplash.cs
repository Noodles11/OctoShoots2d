using System.Collections.Generic;
using Godot;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// A dark screen between rooms: after the rift (what Clementine did in the room she left, and where she goes next) and
/// on her death (how far the run got). It shows "working…" while a room is generated, then a pulsing prompt to go on.
/// Its opacity is set by the caller (brief fades in and out).
/// </summary>
public partial class LoadingSplash : Control
{
    Label _title = null!, _next = null!, _working = null!;
    GridContainer _stats = null!;
    float _time;
    string _status = "";
    bool _prompt;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        AddChild(new ColorRect { Color = new Color(0.01f, 0.025f, 0.035f), AnchorRight = 1f, AnchorBottom = 1f, MouseFilter = MouseFilterEnum.Ignore });

        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(460f, 0f) };
        box.AddThemeConstantOverride("separation", 14);
        center.AddChild(box);

        _title = Text(36, new Color(1f, 0.82f, 0.62f));
        box.AddChild(_title);
        box.AddChild(new HSeparator());
        _stats = new GridContainer { Columns = 2 };
        _stats.AddThemeConstantOverride("h_separation", 40);
        _stats.AddThemeConstantOverride("v_separation", 6);
        box.AddChild(_stats);
        box.AddChild(new HSeparator());
        _next = Text(24, new Color(0.75f, 0.9f, 0.95f));
        box.AddChild(_next);
        _working = Text(18, new Color(0.5f, 0.62f, 0.66f));
        box.AddChild(_working);
    }

    static Label Text(int size, Color color)
    {
        var l = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", color);
        return l;
    }

    /// <summary>Fills the splash: the room just cleared, its numbers (label, value), and the next room.</summary>
    public void Fill(string title, IEnumerable<(string Label, string Value)> stats, string next)
    {
        _title.Text = title;
        foreach (var child in _stats.GetChildren()) child.QueueFree();
        foreach (var (label, value) in stats)
        {
            var l = new Label { Text = label };
            l.AddThemeColorOverride("font_color", new Color(0.62f, 0.72f, 0.75f));
            _stats.AddChild(l);
            var v = new Label { Text = value, HorizontalAlignment = HorizontalAlignment.Right, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            v.AddThemeColorOverride("font_color", new Color(1f, 0.95f, 0.88f));
            _stats.AddChild(v);
        }
        _next.Text = next;
        _time = 0f;
        Working("Shaping the reef");
    }

    /// <summary>Something is under way: the status line with animated dots.</summary>
    public void Working(string text)
    {
        _status = text;
        _prompt = false;
    }

    /// <summary>Ready: the status line becomes a pulsing prompt.</summary>
    public void Prompt(string text)
    {
        _status = text;
        _prompt = true;
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;
        _time += (float)delta;
        if (_prompt)
        {
            _working.Text = _status;
            float pulse = 0.55f + 0.45f * Mathf.Sin(_time * 3.5f);
            _working.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.75f, pulse));
        }
        else
        {
            _working.Text = _status + new string('.', 1 + (int)(_time * 2.5f) % 3);
            _working.AddThemeColorOverride("font_color", new Color(0.5f, 0.62f, 0.66f));
        }
    }
}
