using System;
using Godot;
using OctoShoots.Game.Settings;

namespace OctoShoots.Game.Title;

/// <summary>Settings (proposal §4.6): the view options that exist today, reduced motion, and the controls (read-only).</summary>
public partial class SettingsCard : PanelContainer
{
    readonly ViewOptions _view;
    readonly Action<ViewOptions> _applied;
    readonly Action _close;

    public SettingsCard(ViewOptions view, Action<ViewOptions> applied, Action close)
    {
        _view = view;
        _applied = applied;
        _close = close;
    }

    public override void _Ready()
    {
        var style = TitleStyle.Box(TitleStyle.Pearl, TitleStyle.Coral, 3, 18, 28);
        style.ShadowColor = new Color(0.04f, 0.1f, 0.12f, 0.45f);
        style.ShadowSize = 24;
        style.ShadowOffset = new Vector2(0f, 10f);
        AddThemeStyleboxOverride("panel", style);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 16);
        AddChild(box);

        var head = new HBoxContainer();
        box.AddChild(head);
        var title = TitleStyle.Text("Settings", 40, TitleStyle.Ink, TitleStyle.Display);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        head.AddChild(title);
        var close = TitleStyle.Pill("Close  (Esc)", primary: false, size: 15);
        close.Pressed += _close;
        head.AddChild(close);

        box.AddChild(Heading("View"));
        var fps = new OptionButton();
        StyleControl(fps);
        int[] caps = { 30, 60, 120, 144, 0 };
        foreach (int cap in caps) fps.AddItem(cap == 0 ? "Unlimited" : $"{cap} fps");
        fps.Selected = Math.Max(0, Array.IndexOf(caps, _view.MaxFps));
        fps.ItemSelected += i =>
        {
            _view.MaxFps = caps[i];
            Save();
        };
        box.AddChild(Row("Frame-rate cap", "Keeps the GPU cool; vsync may cap it further.", fps));

        var sun = new HSlider { MinValue = 0, MaxValue = 2, Step = 0.05, Value = _view.SunLight, CustomMinimumSize = new Vector2(260f, 24f) };
        sun.ValueChanged += v =>
        {
            _view.SunLight = (float)v;
            Save();
        };
        box.AddChild(Row("Sunlight", "God rays, caustics and dappled light under the water (0 turns them off).", sun));

        var shake = Toggle(_view.CameraShake, on =>
        {
            _view.CameraShake = on;
            Save();
        });
        box.AddChild(Row("Camera shake", "The jolt when a boss lands or slams shut.", shake));

        var reduced = Toggle(_view.ReducedMotion, on =>
        {
            _view.ReducedMotion = on;
            Save();
        });
        box.AddChild(Row("Reduced motion", "Menus and banners cross-fade instead of rising, diving and splashing.", reduced));

        box.AddChild(Heading("Controls"));
        var keys = new GridContainer { Columns = 2 };
        keys.AddThemeConstantOverride("h_separation", 40);
        keys.AddThemeConstantOverride("v_separation", 6);
        foreach (var (what, key) in new[]
                 {
                     ("Swim", "W A S D"), ("Dash", "Space"), ("Aim and shoot", "Mouse (hold) or arrow keys"),
                     ("Level map", "Tab"), ("Pause", "Esc"),
                 })
        {
            keys.AddChild(TitleStyle.Text(what, 16, TitleStyle.InkSoft));
            keys.AddChild(TitleStyle.Text(key, 16, TitleStyle.Ink, TitleStyle.Mono));
        }
        box.AddChild(keys);
        box.AddChild(TitleStyle.Text("Rebinding comes later.", 14, TitleStyle.InkSoft));
    }

    void Save()
    {
        SettingsStore.Save(SettingsStore.LoadTuning(), _view);
        _applied(_view);
    }

    static Label Heading(string text) => TitleStyle.Text(text.ToUpperInvariant(), 13, TitleStyle.Coral, TitleStyle.BodyBold);

    static Control Row(string name, string help, Control control)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 24);
        var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        text.AddThemeConstantOverride("separation", 0);
        text.AddChild(TitleStyle.Text(name, 18, TitleStyle.Ink, TitleStyle.BodyBold));
        var h = TitleStyle.Text(help, 14, TitleStyle.InkSoft);
        h.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        h.CustomMinimumSize = new Vector2(520f, 0f);
        text.AddChild(h);
        row.AddChild(text);
        control.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        row.AddChild(control);
        return row;
    }

    static CheckButton Toggle(bool on, Action<bool> changed)
    {
        var t = new CheckButton { ButtonPressed = on, Text = on ? "On" : "Off" };
        StyleControl(t);
        t.Toggled += v =>
        {
            t.Text = v ? "On" : "Off";
            changed(v);
        };
        return t;
    }

    static void StyleControl(Control c)
    {
        if (c is OptionButton)
        {
            foreach (string state in new[] { "normal", "hover", "pressed", "focus" })
            {
                var box = TitleStyle.Box(state == "hover" ? new Color(1f, 0.93f, 0.9f) : Colors.White, state == "focus" ? TitleStyle.Mint : TitleStyle.Coral, 2, 12);
                box.ContentMarginLeft = 14;
                box.ContentMarginRight = 30;
                box.ContentMarginTop = box.ContentMarginBottom = 6;
                c.AddThemeStyleboxOverride(state, box);
            }
        }
        c.AddThemeFontOverride("font", TitleStyle.BodyBold);
        c.AddThemeFontSizeOverride("font_size", 16);
        c.AddThemeColorOverride("font_color", TitleStyle.Ink);
        c.AddThemeColorOverride("font_hover_color", TitleStyle.Ink);
        c.AddThemeColorOverride("font_pressed_color", TitleStyle.Ink);
        c.AddThemeColorOverride("font_focus_color", TitleStyle.Ink);
    }
}
