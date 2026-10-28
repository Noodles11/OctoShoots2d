using System.Linq;
using System;
using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// ESC: the game stops under a dimmed screen. Resume, restart the run (a new random seed, level 1, no pearls, full HP),
/// and three tabs: her stats as they stand (PlaneStatSheet: each against her bare base, better in green, worse in
/// red), every pearl Clementine has absorbed with its name, tagline and exact effects (the item caption lines), and the
/// achievements.
/// </summary>
public partial class PauseMenu : Control
{
    public event Action? ResumePressed;
    public event Action? RestartPressed;
    public event Action? QuitPressed;
    /// <summary>The camera-zoom slider moved (saved with the view options).</summary>
    public event Action<float>? ZoomChanged;

    /// <summary>The camera zoom the slider starts at; set before the menu is added.</summary>
    public float Zoom { get; init; } = CameraRig.DefaultZoom;

    VBoxContainer _pearls = null!, _achievements = null!;
    GridContainer _stats = null!;
    Button _statTab = null!, _pearlTab = null!, _achievementTab = null!;
    Label _summary = null!;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        AddChild(new ColorRect { Color = new Color(0.01f, 0.03f, 0.05f, 0.72f), MouseFilter = MouseFilterEnum.Ignore, AnchorRight = 1f, AnchorBottom = 1f });

        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(560f, 0f) };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.04f, 0.09f, 0.11f, 0.95f),
            BorderColor = new Color(1f, 0.75f, 0.55f, 0.5f),
            BorderWidthBottom = 2, BorderWidthTop = 2, BorderWidthLeft = 2, BorderWidthRight = 2,
            CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10, CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10,
            ContentMarginLeft = 24, ContentMarginRight = 24, ContentMarginTop = 20, ContentMarginBottom = 20,
        });
        center.AddChild(panel);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 12);
        panel.AddChild(box);

        var title = new Label { Text = "Paused", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 34);
        title.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.7f));
        box.AddChild(title);

        _summary = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _summary.AddThemeColorOverride("font_color", new Color(0.75f, 0.85f, 0.88f));
        box.AddChild(_summary);

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 16);
        box.AddChild(buttons);
        var resume = _resume = new Button { Text = "Resume  (Esc · Start · B)", CustomMinimumSize = new Vector2(220f, 40f) };
        resume.Pressed += () => ResumePressed?.Invoke();
        buttons.AddChild(resume);
        var restart = new Button { Text = "Restart run", CustomMinimumSize = new Vector2(170f, 40f) };
        restart.Pressed += () => RestartPressed?.Invoke();
        buttons.AddChild(restart);
        var quit = new Button { Text = "Save & quit to title", CustomMinimumSize = new Vector2(190f, 40f), TooltipText = "You'll resume right where you are." };
        quit.Pressed += () => QuitPressed?.Invoke();
        buttons.AddChild(quit);

        var zoomRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        zoomRow.AddThemeConstantOverride("separation", 16);
        var zoomName = new Label { Text = "Camera zoom" };
        zoomName.AddThemeColorOverride("font_color", new Color(0.75f, 0.85f, 0.88f));
        zoomRow.AddChild(zoomName);
        zoomRow.AddChild(OctoShoots.Game.Title.SettingsCard.ZoomSlider(Zoom, v => ZoomChanged?.Invoke(v), new Color(0.75f, 0.85f, 0.88f)));
        box.AddChild(zoomRow);

        box.AddChild(new HSeparator());
        // Three lists under tabs: her stats, the pearls she has absorbed, and the achievements (earned or not).
        var tabs = new HBoxContainer();
        tabs.AddThemeConstantOverride("separation", 10);
        box.AddChild(tabs);
        _statTab = new Button { Text = "Stats", ToggleMode = true, ButtonPressed = true, CustomMinimumSize = new Vector2(120f, 34f) };
        _pearlTab = new Button { Text = "Absorbed pearls", ToggleMode = true, CustomMinimumSize = new Vector2(170f, 34f) };
        _achievementTab = new Button { Text = "Achievements", ToggleMode = true, CustomMinimumSize = new Vector2(170f, 34f) };
        _statTab.Pressed += () => ShowList(0);
        _pearlTab.Pressed += () => ShowList(1);
        _achievementTab.Pressed += () => ShowList(2);
        tabs.AddChild(_statTab);
        tabs.AddChild(_pearlTab);
        tabs.AddChild(_achievementTab);

        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0f, 340f), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        box.AddChild(scroll);
        var lists = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(lists);
        // Her stats: two columns of name and value.
        _stats = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _stats.AddThemeConstantOverride("h_separation", 18);
        _stats.AddThemeConstantOverride("v_separation", 8);
        lists.AddChild(_stats);
        _pearls = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Visible = false };
        _pearls.AddThemeConstantOverride("separation", 14);
        lists.AddChild(_pearls);
        _achievements = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Visible = false };
        _achievements.AddThemeConstantOverride("separation", 14);
        lists.AddChild(_achievements);
    }

    /// <summary>0 her stats, 1 the absorbed pearls, 2 the achievements.</summary>
    void ShowList(int tab)
    {
        _stats.Visible = tab == 0;
        _pearls.Visible = tab == 1;
        _achievements.Visible = tab == 2;
        _statTab.ButtonPressed = tab == 0;
        _pearlTab.ButtonPressed = tab == 1;
        _achievementTab.ButtonPressed = tab == 2;
    }

    /// <summary>Her stats now: each value, and how far it is from her bare base (better green, worse red).</summary>
    void FillStats(PlaneRun run)
    {
        foreach (var child in _stats.GetChildren()) child.QueueFree();
        foreach (var line in PlaneStatSheet.Of(run))
        {
            var name = new Label { Text = line.Name, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            name.AddThemeColorOverride("font_color", new Color(0.62f, 0.72f, 0.75f));
            _stats.AddChild(name);
            bool better = line.LowerIsBetter ? line.Change < 0f : line.Change > 0f;
            string change = MathF.Abs(line.Change) < 0.005f ? "" : $"  ({(line.Change > 0f ? "+" : "−")}{MathF.Abs(line.Change) * 100f:0}%)";
            // Counts (luck, shots per throw, mending) change by a number, not a share.
            if (line.Name is "Luck" or "Shots per throw" or "Mending" or "Critical hits") change = "";
            var value = new Label { Text = line.Value + change, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            value.AddThemeColorOverride("font_color", MathF.Abs(line.Change) < 0.005f ? new Color(1f, 0.95f, 0.88f) : better ? new Color(0.55f, 0.95f, 0.65f) : new Color(1f, 0.55f, 0.5f));
            _stats.AddChild(value);
        }
    }

    /// <summary>
    /// Every achievement, earned or not: an earned one shows the pearl it unlocked and its line; an unearned one a dark
    /// pearl silhouette, its title and how to earn it.
    /// </summary>
    void FillAchievements(AchievementCatalog? data, ISet<string> earned, ItemCatalog? catalog)
    {
        foreach (var child in _achievements.GetChildren()) child.QueueFree();
        if (data is null) return;
        int got = data.All.Count(a => earned.Contains(a.Id));
        var count = new Label { Text = $"{got} of {data.All.Count} earned" };
        count.AddThemeColorOverride("font_color", new Color(0.62f, 0.72f, 0.75f));
        _achievements.AddChild(count);
        foreach (var a in data.All)
        {
            bool has = earned.Contains(a.Id);
            ItemDef? pearl = catalog is not null && catalog.TryGet(a.Pearl, out var p) ? p : null;
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 14);
            row.AddChild(new PearlDot(has ? pearl?.Pearl : null, dark: !has));
            var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            text.AddThemeConstantOverride("separation", 2);
            row.AddChild(text);
            var title = new Label { Text = a.Title };
            title.AddThemeFontSizeOverride("font_size", 19);
            title.AddThemeColorOverride("font_color", has ? new Color(1f, 0.93f, 0.85f) : new Color(0.62f, 0.68f, 0.7f));
            text.AddChild(title);
            var line = new Label { Text = has ? $"\u201c{a.Line}\u201d" : a.Hint, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            line.AddThemeColorOverride("font_color", has ? new Color(0.82f, 0.95f, 0.88f) : new Color(0.55f, 0.62f, 0.65f));
            text.AddChild(line);
            var unlock = new Label { Text = has && pearl is not null ? $"Unlocked {pearl.Name}" : "Unlocks a pearl" };
            unlock.AddThemeColorOverride("font_color", has ? new Color(0.44f, 0.89f, 0.76f) : new Color(0.45f, 0.52f, 0.55f));
            text.AddChild(unlock);
            _achievements.AddChild(row);
        }
    }

    /// <summary>Fills the menu from the run (and the profile's achievements) and shows it.</summary>
    public void Open(PlaneRun run, ItemCatalog? catalog, string seed, string where, float hp, AchievementCatalog? achievements = null)
    {
        FillAchievements(achievements, run.Unlocked, catalog);
        FillStats(run);
        _summary.Text = $"Seed {seed} · {where} · {Mathf.RoundToInt(Mathf.Clamp(1f - hp / Mathf.Max(run.MaxHp, 1f), 0f, 1f) * 100f)}% corrupted · {run.Items.Count} pearl{(run.Items.Count == 1 ? "" : "s")}";
        foreach (var child in _pearls.GetChildren()) child.QueueFree();
        if (run.Items.Count == 0 || catalog is null)
        {
            var none = new Label { Text = "None yet — clam rooms hold them.", AutowrapMode = TextServer.AutowrapMode.WordSmart };
            none.AddThemeColorOverride("font_color", new Color(0.6f, 0.7f, 0.72f));
            _pearls.AddChild(none);
        }
        else
        {
            // Newest first.
            foreach (string id in Enumerable.Reverse(run.Items))
                if (catalog.TryGet(id, out var item)) _pearls.AddChild(Row(item));
        }
        Visible = true;
    }

    public void Close()
    {
        Visible = false;
        GetViewport()?.GuiReleaseFocus();
    }

    /// <summary>A controller in hand: focus on Resume, so the D-pad and A work the menu.</summary>
    public void FocusFirst() => _resume.GrabFocus();

    Button _resume = null!;

    static Control Row(ItemDef item)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 14);
        row.AddChild(new PearlDot(item.Pearl));
        var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        text.AddThemeConstantOverride("separation", 2);
        row.AddChild(text);

        var name = new Label { Text = item.Name };
        name.AddThemeFontSizeOverride("font_size", 19);
        name.AddThemeColorOverride("font_color", new Color(1f, 0.93f, 0.85f));
        text.AddChild(name);
        if (!string.IsNullOrEmpty(item.Tagline))
        {
            var tagline = new Label { Text = item.Tagline };
            tagline.AddThemeColorOverride("font_color", new Color(0.62f, 0.72f, 0.75f));
            text.AddChild(tagline);
        }
        foreach (string line in ItemCaption.Describe(item))
        {
            var effect = new Label { Text = "• " + line, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            effect.AddThemeColorOverride("font_color", new Color(0.82f, 0.95f, 0.88f));
            text.AddChild(effect);
        }
        return row;
    }

    /// <summary>The pearl in its own colours, as on the HUD but larger.</summary>
    partial class PearlDot : Control
    {
        readonly PearlLook? _look;
        readonly bool _dark;

        /// <param name="dark">A pearl not yet unlocked: a dark silhouette.</param>
        public PearlDot(PearlLook? look, bool dark = false)
        {
            _look = look;
            _dark = dark;
            CustomMinimumSize = new Vector2(44f, 44f);
            MouseFilter = MouseFilterEnum.Ignore;
        }

        public override void _Draw()
        {
            Color a = _look is null ? Colors.White : new Color(_look.Colors[0]);
            Color b = _look is null ? Colors.LightBlue : new Color(_look.Colors[^1]);
            var c = new Vector2(22f, 22f);
            DrawCircle(c, 19f, new Color(0f, 0f, 0f, 0.6f));
            if (_dark)
            {
                DrawCircle(c, 17f, new Color(0.1f, 0.13f, 0.15f));
                DrawArc(c, 17f, 0f, Mathf.Tau, 32, new Color(0.3f, 0.36f, 0.4f), 1.5f, true);
                DrawString(ThemeDB.FallbackFont, c + new Vector2(-5f, 7f), "?", HorizontalAlignment.Left, -1, 20, new Color(0.4f, 0.46f, 0.5f));
                return;
            }
            DrawCircle(c, 17f, b);
            DrawCircle(c - new Vector2(3f, 3f), 12f, a);
            DrawCircle(c - new Vector2(7f, 7f), 4f, new Color(1f, 1f, 1f, 0.8f));
        }
    }
}
