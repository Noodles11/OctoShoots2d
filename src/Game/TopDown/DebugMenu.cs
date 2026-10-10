using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using OctoShoots.Core.Run;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// F1, any time in a run: the debug menu. The game stands still under it. Jump to any level of the seed (cycle, depth,
/// level; a depth ends at its boss level), and switch any pearl on or off. Pearls apply at once.
/// </summary>
public partial class DebugMenu : Control
{
    /// <summary>Go to this level, keeping what she carries; true: arrive at full HP.</summary>
    public event Action<LevelId, bool>? GoTo;
    /// <summary>A pearl switched on (true) or off.</summary>
    public event Action<string, bool>? PearlToggled;
    /// <summary>Every pearl on the plane on (true), or every pearl off.</summary>
    public event Action<bool>? AllPearls;
    public event Action? Closed;

    static readonly Color Ink = new(0.82f, 0.9f, 0.92f);
    static readonly Color Soft = new(0.55f, 0.65f, 0.68f);
    static readonly Color Head = new(1f, 0.85f, 0.7f);

    ItemCatalog? _catalog;
    SeedCode _seed;
    SpinBox _cycle = null!, _depth = null!, _level = null!;
    Label _bossNote = null!, _where = null!;
    CheckBox _fullHp = null!;
    VBoxContainer _pearlList = null!;
    readonly Dictionary<string, CheckBox> _pearls = new();

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        AddChild(new ColorRect { Color = new Color(0.01f, 0.03f, 0.05f, 0.6f), MouseFilter = MouseFilterEnum.Ignore, AnchorRight = 1f, AnchorBottom = 1f });

        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(640f, 0f) };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.03f, 0.07f, 0.09f, 0.96f),
            BorderColor = new Color(0.45f, 0.95f, 0.8f, 0.55f),
            BorderWidthBottom = 2, BorderWidthTop = 2, BorderWidthLeft = 2, BorderWidthRight = 2,
            CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10, CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10,
            ContentMarginLeft = 22, ContentMarginRight = 22, ContentMarginTop = 18, ContentMarginBottom = 18,
        });
        center.AddChild(panel);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 10);
        panel.AddChild(box);

        var head = new HBoxContainer();
        box.AddChild(head);
        var title = Caption("Debug", 28, Head);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        head.AddChild(title);
        var close = new Button { Text = "Close  (F1)" };
        close.Pressed += () => Closed?.Invoke();
        head.AddChild(close);
        box.AddChild(Caption("The game is paused. Debug runs are not saved or counted in your statistics.", 13, Soft));

        // Level: cycle, depth, level (up to that depth's boss level).
        box.AddChild(new HSeparator());
        box.AddChild(Caption("LEVEL", 13, Head));
        _where = Caption("", 14, Soft);
        box.AddChild(_where);
        var pick = new HBoxContainer();
        pick.AddThemeConstantOverride("separation", 12);
        box.AddChild(pick);
        _cycle = Spin(pick, "Cycle", 1, 9);
        _depth = Spin(pick, "Depth", 1, LevelPlan.Depths);
        _level = Spin(pick, "Level", 1, 5);
        _cycle.ValueChanged += _ => ClampLevel();
        _depth.ValueChanged += _ => ClampLevel();
        _level.ValueChanged += _ => ClampLevel();
        _bossNote = Caption("", 14, Soft);
        pick.AddChild(_bossNote);

        var go = new HBoxContainer();
        go.AddThemeConstantOverride("separation", 16);
        box.AddChild(go);
        var goButton = new Button { Text = "Go to level", CustomMinimumSize = new Vector2(150f, 34f) };
        goButton.Pressed += () => GoTo?.Invoke(Selected, _fullHp.ButtonPressed);
        go.AddChild(goButton);
        var first = new Button { Text = "First level", CustomMinimumSize = new Vector2(120f, 34f) };
        first.Pressed += () => GoTo?.Invoke(LevelId.First, _fullHp.ButtonPressed);
        go.AddChild(first);
        _fullHp = new CheckBox { Text = "Arrive at full HP", ButtonPressed = true };
        _fullHp.AddThemeColorOverride("font_color", Ink);
        go.AddChild(_fullHp);

        // Pearls: the ones on the plane first, then the rest of the catalog (not ported yet; they may do nothing).
        box.AddChild(new HSeparator());
        var pearlHead = new HBoxContainer();
        pearlHead.AddThemeConstantOverride("separation", 10);
        box.AddChild(pearlHead);
        var pearlTitle = Caption("PEARLS", 13, Head);
        pearlTitle.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        pearlHead.AddChild(pearlTitle);
        var all = new Button { Text = "All on the plane" };
        all.Pressed += () => AllPearls?.Invoke(true);
        pearlHead.AddChild(all);
        var none = new Button { Text = "None" };
        none.Pressed += () => AllPearls?.Invoke(false);
        pearlHead.AddChild(none);
        box.AddChild(Caption("Switched pearls apply at once. Holding several active pearls, the last one switched on is used.", 13, Soft));

        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0f, 360f), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        box.AddChild(scroll);
        _pearlList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _pearlList.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(_pearlList);
    }

    LevelId Selected => new((int)_cycle.Value, (int)_depth.Value, (int)_level.Value);

    /// <summary>Fills the menu from the run and shows it.</summary>
    public void Open(PlaneRun run, ItemCatalog? catalog, SeedCode seed, LevelId current)
    {
        _seed = seed;
        if (!ReferenceEquals(catalog, _catalog) || _pearls.Count == 0)
        {
            _catalog = catalog;
            BuildPearls();
        }
        _where.Text = $"Seed {seed} · now on {current}";
        _cycle.SetValueNoSignal(current.Cycle);
        _depth.SetValueNoSignal(current.Depth);
        _level.SetValueNoSignal(current.Level);
        ClampLevel();
        Refresh(run);
        Visible = true;
    }

    public void Close() => Visible = false;

    /// <summary>The pearl boxes follow what she carries.</summary>
    public void Refresh(PlaneRun run)
    {
        foreach (var (id, check) in _pearls) check.SetPressedNoSignal(run.Items.Contains(id));
    }

    /// <summary>A depth's levels run up to its boss level (4 or 5, from the seed).</summary>
    void ClampLevel()
    {
        int boss = LevelPlan.BossLevel(_seed, (int)_cycle.Value, (int)_depth.Value);
        _level.MaxValue = boss;
        if (_level.Value > boss) _level.SetValueNoSignal(boss);
        _bossNote.Text = (int)_level.Value == boss ? $"boss level (of {boss})" : $"boss on level {boss}";
    }

    void BuildPearls()
    {
        foreach (var child in _pearlList.GetChildren()) child.QueueFree();
        _pearls.Clear();
        if (_catalog is null)
        {
            _pearlList.AddChild(Caption("No item catalog loaded.", 14, Soft));
            return;
        }
        var ported = PlaneRun.PortedPearls.Where(_catalog.Contains).Select(id => _catalog[id]).ToList();
        var rest = _catalog.Items.Where(i => !PlaneRun.PortedPearls.Contains(i.Id)).OrderBy(i => i.Name).ToList();
        AddSection("On the plane", ported);
        if (rest.Count > 0) AddSection("Not on the plane yet (may have no effect)", rest);
    }

    void AddSection(string name, List<ItemDef> items)
    {
        _pearlList.AddChild(Caption(name, 14, Head));
        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 24);
        grid.AddThemeConstantOverride("v_separation", 2);
        _pearlList.AddChild(grid);
        foreach (var item in items)
        {
            var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(280f, 0f) };
            row.AddThemeConstantOverride("separation", 8);
            row.AddChild(new Dot(item.Pearl));
            string id = item.Id;
            var check = new CheckBox { Text = item.Name + (item.Active is not null ? "  (active)" : ""), TooltipText = item.Tagline };
            check.AddThemeColorOverride("font_color", Ink);
            check.Toggled += on => PearlToggled?.Invoke(id, on);
            row.AddChild(check);
            grid.AddChild(row);
            _pearls[id] = check;
        }
    }

    static SpinBox Spin(HBoxContainer row, string name, int min, int max)
    {
        row.AddChild(Caption(name, 15, Ink));
        var spin = new SpinBox { MinValue = min, MaxValue = max, Step = 1, Value = min, CustomMinimumSize = new Vector2(80f, 0f) };
        row.AddChild(spin);
        return spin;
    }

    static Label Caption(string text, int size, Color color)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    /// <summary>The pearl's colours, small.</summary>
    partial class Dot : Control
    {
        readonly PearlLook? _look;

        public Dot(PearlLook? look)
        {
            _look = look;
            CustomMinimumSize = new Vector2(18f, 18f);
            SizeFlagsVertical = SizeFlags.ShrinkCenter;
            MouseFilter = MouseFilterEnum.Ignore;
        }

        public override void _Draw()
        {
            Color a = _look is null ? Colors.White : new Color(_look.Colors[0]);
            Color b = _look is null ? Colors.LightBlue : new Color(_look.Colors[^1]);
            var c = new Vector2(9f, 9f);
            DrawCircle(c, 8f, new Color(0f, 0f, 0f, 0.6f));
            DrawCircle(c, 7f, b);
            DrawCircle(c - new Vector2(1.5f, 1.5f), 4.5f, a);
        }
    }
}
