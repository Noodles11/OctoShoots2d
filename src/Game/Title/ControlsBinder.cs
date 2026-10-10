using System.Collections.Generic;
using Godot;
using OctoShoots.Game.Controls;

namespace OctoShoots.Game.Title;

/// <summary>
/// Settings → Controls: every rebindable action with its keyboard/mouse binding and its controller binding. Press a
/// binding (click, or A on a controller) and then the new key, button, stick or trigger; Esc cancels and Delete clears
/// it. Saved at once (InputSetup, user://bindings.json).
/// </summary>
public partial class ControlsBinder : VBoxContainer
{
    readonly Dictionary<(string Action, bool Pad), Button> _slots = new();
    (string Action, bool Pad)? _capturing;
    /// <summary>When the capture began: the press that started it is not taken as the new binding.</summary>
    ulong _capturedAt;

    public override void _Ready()
    {
        AddThemeConstantOverride("separation", 6);
        // What is connected and what it really sends: a pad Godot does not recognise numbers its buttons its own way.
        _pads = TitleStyle.Text("", 14, TitleStyle.Ink);
        _pads.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        AddChild(_pads);
        _last = TitleStyle.Text("", 14, TitleStyle.InkSoft, TitleStyle.Mono);
        AddChild(_last);
        var grid = new GridContainer { Columns = 3 };
        grid.AddThemeConstantOverride("h_separation", 18);
        grid.AddThemeConstantOverride("v_separation", 6);
        AddChild(grid);
        grid.AddChild(TitleStyle.Text("Action", 14, TitleStyle.InkSoft, TitleStyle.BodyBold));
        grid.AddChild(TitleStyle.Text("Keyboard & mouse", 14, TitleStyle.InkSoft, TitleStyle.BodyBold));
        grid.AddChild(TitleStyle.Text("Controller", 14, TitleStyle.InkSoft, TitleStyle.BodyBold));
        foreach (var spec in InputSetup.Bindable)
        {
            var name = TitleStyle.Text(spec.Name, 16, TitleStyle.Ink);
            name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            grid.AddChild(name);
            grid.AddChild(Slot(spec.Action, pad: false, locked: spec.KeyLocked));
            grid.AddChild(Slot(spec.Action, pad: true, locked: false));
        }

        var foot = new HBoxContainer();
        foot.AddThemeConstantOverride("separation", 16);
        AddChild(foot);
        var reset = TitleStyle.Pill("Reset to defaults", primary: false, size: 14);
        reset.Pressed += () =>
        {
            _capturing = null;
            PadMenus.Capturing = false;
            InputSetup.ResetAll();
            Refresh();
        };
        foot.AddChild(reset);
        var help = TitleStyle.Text("Menus: D-pad moves, A confirms, B goes back. Keyboard only: F1 debug menu, F3 debug map, R regenerate, typing a seed.", 13, TitleStyle.InkSoft);
        help.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        help.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        foot.AddChild(help);
        Refresh();
    }

    Label _pads = null!, _last = null!;

    public override void _Process(double delta)
    {
        var pads = string.Join("\n", InputSetup.PadReport());
        _pads.Text = pads.Length > 0 ? "Controller: " + pads : "No controller connected.";
        _last.Text = InputSetup.LastPad is { } code ? $"Last controller input: {InputSetup.Describe(code)}   ({code})" : "Press any controller button to see what it sends.";
    }

    Button Slot(string action, bool pad, bool locked)
    {
        var button = new Button { CustomMinimumSize = new Vector2(190f, 32f), Disabled = locked, TooltipText = locked ? "Fixed on the keyboard" : "Press, then the new binding (Esc cancels, Delete clears)" };
        foreach (string state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
        {
            var box = TitleStyle.Box(state == "hover" ? new Color(1f, 0.93f, 0.9f) : Colors.White, state == "focus" ? TitleStyle.Mint : TitleStyle.Coral, 2, 10);
            box.ContentMarginLeft = box.ContentMarginRight = 10;
            box.ContentMarginTop = box.ContentMarginBottom = 4;
            button.AddThemeStyleboxOverride(state, box);
        }
        button.AddThemeFontOverride("font", TitleStyle.Mono);
        button.AddThemeFontSizeOverride("font_size", 14);
        foreach (string c in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" }) button.AddThemeColorOverride(c, TitleStyle.Ink);
        button.AddThemeColorOverride("font_disabled_color", TitleStyle.InkSoft);
        button.Pressed += () =>
        {
            _capturing = (action, pad);
            PadMenus.Capturing = true;
            _capturedAt = Time.GetTicksMsec();
            Refresh();
        };
        _slots[(action, pad)] = button;
        return button;
    }

    void Refresh()
    {
        foreach (var ((action, pad), button) in _slots)
            button.Text = _capturing == (action, pad)
                ? pad ? "Press a button or stick…" : "Press a key or click…"
                : InputSetup.Describe(InputSetup.Get(action, pad));
    }

    public override void _Input(InputEvent e)
    {
        if (_capturing is not { } slot) return;
        // Esc cancels, Delete clears; the press that opened the capture (a click, A) is not the new binding.
        if (e is InputEventKey { Pressed: true, Echo: false } k && k.PhysicalKeycode is Key.Escape or Key.Delete)
        {
            if (k.PhysicalKeycode == Key.Delete) InputSetup.Set(slot.Action, slot.Pad, null);
            End();
            return;
        }
        if (Time.GetTicksMsec() - _capturedAt < 250) return;
        if (InputSetup.FromEvent(e, slot.Pad) is not { } code) return;
        InputSetup.Set(slot.Action, slot.Pad, code);
        End();
    }

    void End()
    {
        var slot = _capturing;
        _capturing = null;
        PadMenus.Capturing = false;
        GetViewport().SetInputAsHandled();
        Refresh();
        if (slot is { } s && _slots.TryGetValue(s, out var button)) button.CallDeferred(Control.MethodName.GrabFocus);
    }

    /// <summary>A capture still open when the card closes is dropped.</summary>
    public override void _ExitTree()
    {
        _capturing = null;
        PadMenus.Capturing = false;
    }
}
