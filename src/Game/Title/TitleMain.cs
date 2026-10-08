using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using OctoShoots.Core;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using OctoShoots.Core.Run;
using OctoShoots.Core.Saves;
using OctoShoots.Game.Fx;
using OctoShoots.Game.Settings;
using OctoShoots.Game.TopDown;

namespace OctoShoots.Game.Title;

/// <summary>
/// The title screen (docs/TITLE-MENU-PROPOSAL.md): a menu over the live sea. Clementine swims slowly along a canyon of a
/// fixed seed while the camera follows; the menu offers Continue, New run, Seeded run, Sea-pedia, Statistics, Save &amp;
/// load, Settings and Quit. Run with game flags (--seed=, --at=, …) it skips straight to a run.
/// Review flags: --title-sample (an example profile, never saved), --title-card=stats|pedia|creatures|save|settings|seed,
/// --title-launch=new|continue (starts that run after a moment; the game then takes --capture), --capture=dir --frames=a,b.
/// </summary>
public partial class TitleMain : Node3D
{
    const string BackdropSeed = "KELP7Q2Z";
    /// <summary>Clementine's idle swim along the canyon: a fraction of her cruise (about 0.7 m/s).</summary>
    const float SwimInput = 0.12f;

    ItemCatalog? _catalog;
    Tuning _tuning = null!;
    ViewOptions _view = null!;
    bool Reduced => _view.ReducedMotion;

    // The backdrop.
    LevelView _level = null!;
    BellView _bell = null!;
    CameraRig _camera = null!;
    MarineSnow _snow = null!;
    SunLight _sun = null!;
    Task<LevelMap>? _backdrop;
    PlaneWorld? _world;
    List<System.Numerics.Vector2> _route = new();
    int _routeAt, _routeDir = 1, _routeEnd;
    double _accumulator;

    // The menu.
    Control _ui = null!;
    VBoxContainer _column = null!;
    Control _logo = null!;
    readonly List<(Button Button, Label Detail, Control Pip, string Id)> _items = new();
    readonly Dictionary<Button, Control> _glide = new();
    Control? _inline;
    Control? _card;
    ColorRect _fade = null!;
    bool _launching;

    // Review.
    string? _captureDir;
    readonly Queue<int> _captureFrames = new();
    int _frame;
    string? _openCard;
    string? _autoLaunch;

    static readonly string[] TitleOnlyFlags = { "--capture=", "--frames=", "--title", "--no-focus", "--dbg-" };

    public override void _Ready()
    {
        // Game flags mean a verification run: straight in, as before the title existed.
        var args = OS.GetCmdlineUserArgs();
        if (args.Any(a => !TitleOnlyFlags.Any(a.StartsWith)))
        {
            CallDeferred(MethodName.SkipToGame);
            return;
        }
        foreach (string arg in args)
        {
            string Value(string prefix) => arg[prefix.Length..];
            if (arg == "--title-sample") GameSave.UseInMemory(SampleSave());
            else if (arg.StartsWith("--title-card=")) _openCard = Value("--title-card=");
            else if (arg.StartsWith("--title-launch=")) _autoLaunch = Value("--title-launch=");
            else if (arg.StartsWith("--capture=")) _captureDir = Value("--capture=");
            else if (arg.StartsWith("--frames="))
                foreach (string f in Value("--frames=").Split(','))
                    if (int.TryParse(f, out int n)) _captureFrames.Enqueue(n);
        }

        _tuning = SettingsStore.LoadTuning();
        _view = SettingsStore.LoadView();
        _catalog = LoadCatalog();
        Engine.MaxFps = _captureDir is null ? _view.MaxFps : 0;

        AddChild(TopDownMain.MakeEnvironment(ReefLook.Shallows));
        _sun = new SunLight();
        AddChild(_sun);
        _sun.Configure(ReefLook.Shallows.Sun, ReefLook.Shallows.SunEnergy);
        _level = new LevelView();
        AddChild(_level);
        _bell = new BellView();
        AddChild(_bell);
        _camera = new CameraRig();
        AddChild(_camera);
        _snow = new MarineSnow(900, new Vector3(22f, 7f, 17f), 0.11f);
        AddChild(_snow);
        _sun.SetStrength(_view.SunLight);
        _camera.SetReducedMotion(_view.ReducedMotion);

        var streams = new RunStreams(SeedCode.Parse(BackdropSeed));
        _backdrop = Task.Run(() => TopDownGenerator.Generate(streams, 1, 1));

        BuildUi();
    }

    void SkipToGame() => GetTree().ChangeSceneToFile(RunLaunch.GameScene);

    /// <summary>The menu is laid out on a 1600×900 page, scaled to fit the window.</summary>
    static readonly Vector2 Page = new(1600f, 900f);

    Vector2 PageSize => GetViewport().GetVisibleRect().Size / UiScale;
    float UiScale
    {
        get
        {
            var size = GetViewport().GetVisibleRect().Size;
            return Mathf.Clamp(Mathf.Min(size.X / Page.X, size.Y / Page.Y), 0.5f, 2f);
        }
    }

    void FitUi()
    {
        float s = UiScale;
        _ui.Scale = new Vector2(s, s);
        _ui.Size = GetViewport().GetVisibleRect().Size / s;
    }

    static ItemCatalog? LoadCatalog()
    {
        try
        {
            using var file = FileAccess.Open("res://data/items.json", FileAccess.ModeFlags.Read);
            return ItemCatalog.FromJson(file.GetAsText());
        }
        catch (Exception e)
        {
            GD.PushError($"No items: {e.Message}");
            return null;
        }
    }

    // ───────────────────────── the backdrop ─────────────────────────

    /// <summary>The level is ready: show it, empty it of foes, and set Clementine swimming along a canyon.</summary>
    void ShowBackdrop(LevelMap map)
    {
        _level.Show(map);
        _world = new PlaneWorld(map, _tuning, new PlaneRun(_catalog, _tuning));
        _world.Mobs.Clear();
        _world.Ambushes.Clear();
        _route = LevelValidator.ShortestPath(map, map.Start.Position, map.Rift.Position, clearance: 1.5f);
        // Back and forth along the first part of the way, well clear of the rift's arena.
        _routeEnd = Math.Max(2, (int)(_route.Count * 0.55f));
        _camera.Track(Focus(1f), 0f, snap: true);
    }

    System.Numerics.Vector2 SwimInputNow()
    {
        if (_world is null || _route.Count < 3) return default;
        var pos = _world.Player.Position;
        // Advance (or back up) the target once she is near it.
        var target = _route[Math.Clamp(_routeAt, 0, _route.Count - 1)];
        if (System.Numerics.Vector2.Distance(target, pos) < 1.2f)
        {
            _routeAt += _routeDir;
            if (_routeAt >= _routeEnd || _routeAt <= 0) _routeDir = -_routeDir;
            _routeAt = Math.Clamp(_routeAt, 0, _routeEnd);
            target = _route[_routeAt];
        }
        var to = target - pos;
        return to.LengthSquared() > 0.01f ? System.Numerics.Vector2.Normalize(to) * SwimInput : default;
    }

    /// <summary>The camera looks a little left of her, so she swims in the right third, clear of the menu.</summary>
    Vector3 Focus(float alpha)
    {
        var p = System.Numerics.Vector2.Lerp(_world!.Player.PrevPosition, _world.Player.Position, alpha);
        return new Vector3(p.X - 6f, LevelMap.SwimBand, p.Y + 1f);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        FitUi();
        if (_world is null && _backdrop is { IsCompleted: true } task)
        {
            _backdrop = null;
            if (task.IsFaulted) GD.PushError(task.Exception?.ToString());
            else ShowBackdrop(task.Result);
            // The sea fades up once there is something to see.
            var t = CreateTween();
            t.TweenProperty(_fade, "color:a", 0f, Reduced ? 0.3f : 0.8f);
        }
        if (_world is not null)
        {
            _accumulator += delta;
            int steps = 0;
            while (_accumulator >= PlaneWorld.Dt && steps < 4)
            {
                _world.Step(new PlaneInput { Move = SwimInputNow(), Aim = -System.Numerics.Vector2.UnitY });
                _accumulator -= PlaneWorld.Dt;
                steps++;
            }
            if (steps == 4) _accumulator = 0;
            float alpha = (float)(_accumulator / PlaneWorld.Dt);
            _bell.Sync(_world, alpha, dt);
            _camera.Track(Focus(alpha), dt);
            _snow.Tick(dt, Focus(alpha) + Vector3.Up * 6f, _camera.Camera.GlobalBasis);
            _level.UpdateCanopy(_world.Player.Position, dt);
        }
        Capture();
    }

    // ───────────────────────── the menu ─────────────────────────

    void BuildUi()
    {
        var layer = new CanvasLayer { Layer = 3 };
        AddChild(layer);
        _ui = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        layer.AddChild(_ui);
        FitUi();

        // Dim the water under the menu column for legibility.
        var shade = new Gradient();
        shade.SetColor(0, new Color(0.01f, 0.05f, 0.07f, 0.62f));
        shade.SetColor(1, new Color(0.01f, 0.05f, 0.07f, 0f));
        var dim = new TextureRect
        {
            Texture = new GradientTexture2D { Gradient = shade, Width = 256, Height = 4, FillFrom = new Vector2(0.3f, 0f), FillTo = new Vector2(1f, 0f) },
            StretchMode = TextureRect.StretchModeEnum.Scale,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorBottom = 1f,
            OffsetRight = 900f,
        };
        _ui.AddChild(dim);

        _column = new VBoxContainer { Position = new Vector2(72f, 70f), CustomMinimumSize = new Vector2(760f, 0f) };
        _column.AddThemeConstantOverride("separation", 4);
        _ui.AddChild(_column);

        var logo = new VBoxContainer();
        logo.AddThemeConstantOverride("separation", -8);
        var name = TitleStyle.Text("INK DEEP", 104, TitleStyle.Pearl, TitleStyle.Display);
        name.AddThemeColorOverride("font_shadow_color", new Color(1f, 0.478f, 0.42f, 0.55f));
        name.AddThemeConstantOverride("shadow_offset_x", 0);
        name.AddThemeConstantOverride("shadow_offset_y", 6);
        logo.AddChild(name);
        var sub = TitleStyle.Text("the absorbent jellyfish", 24, TitleStyle.WaterSoft, TitleStyle.Body);
        logo.AddChild(sub);
        _logo = logo;
        _column.AddChild(logo);
        _column.AddChild(new Control { CustomMinimumSize = new Vector2(0f, 26f) });

        AddItem("continue", "Continue", () => Launch(LaunchMode.Continue, default));
        AddItem("new", "New run", NewRun);
        AddItem("seeded", "Seeded run", ToggleSeedBox);
        AddItem("pedia", "Sea-pedia", () => OpenCard("pedia"));
        AddItem("stats", "Statistics", () => OpenCard("stats"));
        AddItem("save", "Save & load", () => OpenCard("save"));
        AddItem("settings", "Settings", () => OpenCard("settings"));
        AddItem("quit", "Quit", () => GetTree().Quit());
        Refresh();

        var footer = TitleStyle.Text("Ink Deep · prototype · one profile", 14, new Color(0.78f, 0.88f, 0.88f, 0.7f), TitleStyle.Body, HorizontalAlignment.Right);
        footer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomRight);
        footer.Position -= new Vector2(24f, 30f);
        footer.GrowHorizontal = Control.GrowDirection.Begin;
        _ui.AddChild(footer);

        _fade = new ColorRect { Color = new Color(0f, 0f, 0f, 1f), MouseFilter = Control.MouseFilterEnum.Ignore };
        _fade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _ui.AddChild(_fade);

        // After the first layout pass, so the motions start from where the containers put things.
        GetTree().CreateTimer(0.05).Timeout += Intro;
    }

    /// <summary>The logo surfaces, then the items bob up one by one; Continue (or New run) takes the focus.</summary>
    void Intro()
    {
        TitleStyle.Bloom(_logo, 0.35f, Reduced);
        int i = 0;
        foreach (var (button, detail, _, _) in _items)
        {
            if (!button.GetParent<Control>().Visible) continue;
            TitleStyle.Bloom(button.GetParent<Control>(), 0.55f + 0.06f * i++, Reduced);
        }
        _items.First(it => it.Button.GetParent<Control>().Visible).Button.GrabFocus();
        if (_autoLaunch is { } launch)
            GetTree().CreateTimer(1.2).Timeout += () => Launch(launch == "continue" ? LaunchMode.Continue : LaunchMode.New, SeedCode.NewRandom());
        if (_openCard is { } open)
        {
            if (open == "seed") ToggleSeedBox();
            else OpenCard(open);
        }
    }

    void AddItem(string id, string text, Action pressed)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 0);
        var pip = new Pip { CustomMinimumSize = new Vector2(26f, 44f), Visible = true };
        pip.Modulate = new Color(1f, 1f, 1f, 0f);
        row.AddChild(pip);
        // The focus glide: this spacer widens, pushing the item right.
        var glide = new Control { CustomMinimumSize = Vector2.Zero, MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddChild(glide);
        var button = new Button { Text = text, Flat = true, FocusMode = Control.FocusModeEnum.All, Alignment = HorizontalAlignment.Left, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        button.AddThemeFontOverride("font", TitleStyle.BodyBold);
        button.AddThemeFontSizeOverride("font_size", 32);
        foreach (string state in new[] { "font_color", "font_pressed_color" }) button.AddThemeColorOverride(state, TitleStyle.Pearl);
        foreach (string state in new[] { "font_hover_color", "font_focus_color", "font_hover_pressed_color" }) button.AddThemeColorOverride(state, TitleStyle.Coral);
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        button.AddThemeColorOverride("font_outline_color", new Color(0.02f, 0.08f, 0.1f, 0.6f));
        button.AddThemeConstantOverride("outline_size", 4);
        row.AddChild(button);
        row.AddChild(new Control { CustomMinimumSize = new Vector2(16f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore });
        var detail = TitleStyle.Text("", 17, TitleStyle.WaterSoft, TitleStyle.Body);
        detail.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        detail.AddThemeColorOverride("font_outline_color", new Color(0.02f, 0.08f, 0.1f, 0.6f));
        detail.AddThemeConstantOverride("outline_size", 3);
        row.AddChild(detail);
        _column.AddChild(row);
        button.Pressed += () =>
        {
            if (_launching) return;
            pressed();
        };
        button.MouseEntered += () => button.GrabFocus();
        button.FocusEntered += () => Focused(glide, pip, true);
        button.FocusExited += () => Focused(glide, pip, false);
        _glide[button] = glide;
        _items.Add((button, detail, pip, id));
    }

    /// <summary>The focused item glides right and its coral pip pops in.</summary>
    void Focused(Control glide, Control pip, bool on)
    {
        float seconds = Reduced ? 0.01f : 0.12f;
        var t = glide.CreateTween().SetParallel();
        t.TweenProperty(glide, "custom_minimum_size:x", on ? 12f : 0f, seconds).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        t.TweenProperty(pip, "modulate:a", on ? 1f : 0f, seconds);
        if (on && !Reduced)
        {
            pip.PivotOffset = pip.Size * 0.5f;
            pip.Scale = new Vector2(0.4f, 0.4f);
            t.TweenProperty(pip, "scale", Vector2.One, 0.25f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        }
    }

    (Button Button, Label Detail, Control Pip, string Id) Item(string id) => _items.First(it => it.Id == id);

    /// <summary>Fills in the detail lines from the save.</summary>
    void Refresh()
    {
        var save = GameSave.Current;
        var p = save.Profile;
        var (cont, contDetail, _, _) = Item("continue");
        cont.GetParent<Control>().Visible = save.Run is not null;
        if (save.Run is { } run) contDetail.Text = $"Depth {run.Depth} · Room {run.Room} · {run.Seed} · {TitleStyle.Clock(run.Elapsed)}";
        Item("seeded").Detail.Text = p.RecentSeeds.Count > 0 ? $"last: {p.RecentSeeds[0]}" : "type a seed to share a reef";
        Item("pedia").Detail.Text = $"{SeaPediaCard.PearlsFound(p)} / {PlaneRun.ShotPearls.Length} pearls · {SeaPediaCard.CreaturesMet(p)} / {SeaPediaCard.Creatures.Count} creatures";
        Item("stats").Detail.Text = p.Stats.Runs > 0 ? $"best: {StatsCard.Reach(p.Stats)} · {p.Stats.Runs} runs" : "no runs yet";
        Item("save").Detail.Text = save.Run is { } r ? $"saved run: Room {r.Room}" : "no saved run";
    }

    // ───────────────────────── runs ─────────────────────────

    void NewRun()
    {
        if (GameSave.Current.Run is { } run)
        {
            ConfirmReplace(Item("new").Button, run, () => Launch(LaunchMode.New, SeedCode.NewRandom()));
            return;
        }
        Launch(LaunchMode.New, SeedCode.NewRandom());
    }

    /// <summary>Asks before a new run replaces the saved one, right under the item.</summary>
    void ConfirmReplace(Button item, SuspendedRun run, Action start)
    {
        CloseInline();
        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", TitleStyle.Box(TitleStyle.Pearl, TitleStyle.Coral, 3, 16, 16));
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 10);
        card.AddChild(box);
        box.AddChild(TitleStyle.Text($"Start a new run? Your saved run (Depth {run.Depth} · Room {run.Room}) will be lost.", 17, TitleStyle.Ink, TitleStyle.BodyBold));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        box.AddChild(row);
        var yes = TitleStyle.Pill("Start new", size: 16);
        var no = TitleStyle.Pill("Keep it", primary: false, size: 16);
        yes.Pressed += () =>
        {
            AbandonSavedRun();
            start();
        };
        no.Pressed += () =>
        {
            CloseInline();
            item.GrabFocus();
        };
        row.AddChild(yes);
        row.AddChild(no);
        ShowInline(card, item);
        no.GrabFocus();
    }

    /// <summary>The saved run ends here: it counts as abandoned.</summary>
    static void AbandonSavedRun()
    {
        var save = GameSave.Current;
        if (save.Run is not { } run) return;
        new PlaneProfileRecorder(save.Profile).RunAbandoned(run.Elapsed);
        save.Run = null;
        GameSave.Write();
    }

    /// <summary>Clementine jets down and the sea fades to the game, which starts the run.</summary>
    void Launch(LaunchMode mode, SeedCode seed)
    {
        if (_launching) return;
        _launching = true;
        RunLaunch.Pending = mode;
        RunLaunch.Seed = seed;
        var t = CreateTween();
        if (!Reduced && _world is not null)
        {
            // A jet downward (south), then the dive into the dark.
            _world.Player.Velocity += new System.Numerics.Vector2(0f, 9f);
            t.TweenInterval(0.25f);
        }
        t.TweenProperty(_fade, "color:a", 1f, Reduced ? 0.2f : 0.55f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        t.TweenCallback(Callable.From(() => GetTree().ChangeSceneToFile(RunLaunch.GameScene)));
    }

    // ───────────────────────── the seed box ─────────────────────────

    void ToggleSeedBox()
    {
        if (_inline is SeedBox)
        {
            CloseInline();
            Item("seeded").Button.GrabFocus();
            return;
        }
        var box = new SeedBox(GameSave.Current.Profile.RecentSeeds, seed =>
        {
            if (GameSave.Current.Run is { } run) ConfirmReplace(Item("seeded").Button, run, () => Launch(LaunchMode.Seeded, seed));
            else Launch(LaunchMode.Seeded, seed);
        });
        ShowInline(box, Item("seeded").Button);
        box.FocusField();
    }

    /// <summary>Shows a small card right under a menu item (the seed box, a confirmation).</summary>
    void ShowInline(Control content, Button under)
    {
        CloseInline();
        var row = under.GetParent<Control>();
        var holder = new MarginContainer();
        holder.AddThemeConstantOverride("margin_left", 36);
        holder.AddChild(content);
        _column.AddChild(holder);
        _column.MoveChild(holder, row.GetIndex() + 1);
        _inline = content;
        TitleStyle.Bloom(holder, 0f, Reduced);
    }

    void CloseInline()
    {
        if (_inline is null) return;
        _inline.GetParent().QueueFree();
        _inline = null;
    }

    // ───────────────────────── cards ─────────────────────────

    void OpenCard(string which)
    {
        CloseInline();
        CloseCard(instant: true);
        Action close = () => CloseCard();
        Control card = which switch
        {
            "stats" => new StatsCard(GameSave.Current.Profile, id => _catalog is not null && _catalog.TryGet(id, out var it) ? it.Name : id, close),
            "pedia" or "creatures" => new SeaPediaCard(GameSave.Current.Profile, _catalog, close),
            "save" => new SaveCard(_catalog, () => Launch(LaunchMode.Continue, default), () => AbandonSavedRun(), Refresh, close),
            _ => new SettingsCard(_view, ApplyView, close),
        };
        var size = PageSize;
        var cardSize = new Vector2(Mathf.Min(1060f, size.X - 120f), Mathf.Min(780f, size.Y - 70f));
        card.CustomMinimumSize = cardSize;
        card.Size = cardSize;
        card.Position = new Vector2(size.X - cardSize.X - 56f, (size.Y - cardSize.Y) * 0.5f);
        _ui.AddChild(card);
        _ui.MoveChild(card, _fade.GetIndex());
        _card = card;
        TitleStyle.Surface(card, 80f, 0.42f, 0f, Reduced);
        _column.CreateTween().TweenProperty(_column, "modulate:a", 0.4f, 0.2f);
        if (which == "creatures" && card is SeaPediaCard pedia) pedia.CallDeferred(SeaPediaCard.MethodName.ShowCreatures);
        // Focus the card's first control, so the keyboard works there.
        CallDeferred(MethodName.FocusCard);
    }

    void FocusCard()
    {
        if (_card is null) return;
        var first = FindFocusable(_card);
        first?.GrabFocus();
    }

    static Control? FindFocusable(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Control { FocusMode: Control.FocusModeEnum.All } c && c.IsVisibleInTree() && c is not ScrollContainer) return c;
            if (FindFocusable(child) is { } found) return found;
        }
        return null;
    }

    void CloseCard(bool instant = false)
    {
        if (_card is null) return;
        var card = _card;
        _card = null;
        if (instant) card.QueueFree();
        else TitleStyle.Dive(card, card.QueueFree, 0.38f, Reduced);
        _column.CreateTween().TweenProperty(_column, "modulate:a", 1f, 0.2f);
        Refresh();
        _items.FirstOrDefault(it => it.Button.GetParent<Control>().Visible).Button?.GrabFocus();
    }

    void ApplyView(ViewOptions view)
    {
        _view = view;
        Engine.MaxFps = _captureDir is null ? view.MaxFps : 0;
        _sun.SetStrength(view.SunLight);
        _camera.SetReducedMotion(view.ReducedMotion);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.PhysicalKeycode == Key.Escape)
        {
            if (_card is not null) CloseCard();
            else if (_inline is not null)
            {
                CloseInline();
                _items.FirstOrDefault(it => it.Button.GetParent<Control>().Visible).Button?.GrabFocus();
            }
            GetViewport().SetInputAsHandled();
            return;
        }
        // W/S move through the menu like the arrow keys.
        if (_card is null && key.PhysicalKeycode is Key.W or Key.S && GetViewport().GuiGetFocusOwner() is not LineEdit)
        {
            var visible = _items.Where(it => it.Button.GetParent<Control>().Visible).Select(it => it.Button).ToList();
            int at = visible.FindIndex(b => b.HasFocus());
            int next = Math.Clamp((at < 0 ? 0 : at) + (key.PhysicalKeycode == Key.S ? 1 : -1), 0, visible.Count - 1);
            visible[next].GrabFocus();
            GetViewport().SetInputAsHandled();
        }
    }

    // ───────────────────────── review ─────────────────────────

    void Capture()
    {
        _frame++;
        // When it launches a run, the game scene takes the captures.
        if (_autoLaunch is not null || _captureDir is null || _captureFrames.Count == 0 || _frame < _captureFrames.Peek()) return;
        int frame = _captureFrames.Dequeue();
        string path = $"{_captureDir}/title_{frame:0000}.png";
        GetViewport().GetTexture().GetImage().SavePng(path);
        GD.Print($"Captured {path}");
        if (_captureFrames.Count == 0) GetTree().Quit();
    }

    /// <summary>An example profile for reviewing the screens (never written to disk).</summary>
    static SaveFile SampleSave()
    {
        var p = new Profile
        {
            Achievements = new HashSet<string> { "untouchable" },
            SeenItems = new HashSet<string> { "triple_tentacle", "hammerhead", "mirror_scale", "swordfish_bill", "double_helix" },
            SeenCreatures = new HashSet<string> { PlaneProfileRecorder.MobId },
            SeenBosses = new HashSet<string> { PlaneProfileRecorder.QueenClamId },
            RecentSeeds = new List<string> { "KELP 7Q2Z", "REEF 2SEA", "MANT 4RAY" },
            Stats = new ProfileStats
            {
                Runs = 37, SeededRuns = 6, Deaths = 33, Abandoned = 3, Kills = 1284, RoomsCleared = 112, BossesFreed = 18,
                BubblesThrown = 48210, FullBubbles = 31, BiggestVolley = 8, DamageDealt = 412880, DamageTaken = 9940,
                ShellsCollected = 3904, ShellsSpent = 2610, PearlsAbsorbed = 166, UntouchableRooms = 4, PlaySeconds = 33120,
                LongestRunSeconds = 2467, FastestRoomSeconds = 112, MostShellsInRoom = 74, BestDepth = 1, BestRoom = 6,
                DeathsByCause = new Dictionary<string, int> { ["mob_shot"] = 21, ["boss_pearl"] = 7, ["boss_snap"] = 3, ["royal_pearl"] = 2 },
            },
            Pearls = new Dictionary<string, PearlRecord>
            {
                ["triple_tentacle"] = new() { Absorbed = 29, Runs = 24, RunsLost = 21, RoomsCleared = 71 },
                ["hammerhead"] = new() { Absorbed = 12, Runs = 11, RunsLost = 9, RoomsCleared = 33 },
                ["mirror_scale"] = new() { Absorbed = 9, Runs = 9, RunsLost = 8, RoomsCleared = 20 },
                ["swordfish_bill"] = new() { Absorbed = 0, Runs = 0, RunsLost = 0, RoomsCleared = 0 },
            },
            Creatures = new Dictionary<string, CreatureRecord>
            {
                [PlaneProfileRecorder.MobId] = new() { Encounters = 1402, Defeated = 1240, DefeatedYou = 21 },
                [PlaneProfileRecorder.QueenClamId] = new() { Encounters = 24, Defeated = 18, DefeatedYou = 12, BestSeconds = 21.4 },
            },
        };
        return new SaveFile
        {
            Profile = p,
            Run = new SuspendedRun
            {
                Seed = "KELP 7Q2Z", Depth = 1, Room = 3, Items = new List<string> { "triple_tentacle", "mirror_scale" },
                Hp = 74, Shells = 31, Elapsed = 761, Foes = 41, ShellsCollected = 88,
                SavedAt = DateTime.Now.AddMinutes(-42).ToString("s", CultureInfo.InvariantCulture),
            },
        };
    }

    /// <summary>The focused item's coral pip: a small pulsing bubble.</summary>
    partial class Pip : Control
    {
        float _t;

        public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

        public override void _Process(double delta)
        {
            _t += (float)delta;
            QueueRedraw();
        }

        public override void _Draw()
        {
            var c = new Vector2(Size.X * 0.5f, Size.Y * 0.5f);
            float r = 6.5f + 1f * Mathf.Sin(_t * 4f);
            DrawCircle(c, r, TitleStyle.Coral);
            DrawCircle(c + new Vector2(-2f, -2.2f), r * 0.32f, new Color(1f, 1f, 1f, 0.85f));
            DrawArc(c, r + 3.5f, 0f, Mathf.Tau, 32, new Color(1f, 0.478f, 0.42f, 0.35f), 1.5f, true);
        }
    }
}
