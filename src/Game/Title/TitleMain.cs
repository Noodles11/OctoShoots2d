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
using OctoShoots.Game.Controls;
using OctoShoots.Game.Fx;
using OctoShoots.Game.Settings;
using OctoShoots.Game.TopDown;

namespace OctoShoots.Game.Title;

/// <summary>
/// The title screen (docs/TITLE-MENU-PROPOSAL.md): a menu over the live sea. Clementine swims slowly along a canyon of a
/// fixed seed in the middle of the screen, the camera following her, and the menu's items stand in a ring round her:
/// Continue, New run, Seeded run, Sea-pedia, Statistics, Save &amp; load, Settings and Quit. She turns to face the
/// pointer (her crosshair, GonadCursor), or with the controller the item in focus, her first gonad toward it, as in the
/// game; choosing an item, she shoots her light at it, and what it does happens as the shot strikes it. Run with game
/// flags (--seed=, --at=, …) it skips straight to a run.
/// Review flags: --title-sample (an example profile, never saved), --title-card=stats|pedia|creatures|save|settings|seed,
/// --title-pick=id (that item focused, then chosen: her shot at it),
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
    PufferlingView _puffers = null!;
    CameraRig _camera = null!;
    MarineSnow _snow = null!;
    SunLight _sun = null!;
    Task<LevelShape>? _backdrop;
    PlaneWorld? _world;
    List<System.Numerics.Vector2> _route = new();
    int _routeAt, _routeDir = 1, _routeEnd;
    double _accumulator;

    // The menu.
    Control _ui = null!;
    /// <summary>The ring of items round her (and the inline cards under them).</summary>
    Control _ring = null!;
    Control _logo = null!;
    readonly List<(Button Button, Label Detail, string Id)> _items = new();
    /// <summary>The ring: its middle (on her) and its radii, on the 1600×900 page.</summary>
    static readonly Vector2 RingRadii = new(430f, 300f);
    Vector2 RingCentre => PageSize * 0.5f + new Vector2(0f, -8f);
    /// <summary>The last pointer she followed: the mouse, or (with the controller or the keys) the item in focus.</summary>
    bool _byPad;
    MenuShots _shots = null!;
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
    /// <summary>Review: --title-pick=id focuses that item and chooses it a moment in (her shot at it).</summary>
    string? _autoPick;

    static readonly string[] TitleOnlyFlags = { "--capture=", "--frames=", "--title", "--no-focus", "--dbg-" };

    public override void _Ready()
    {
        // The bundled fonts: Nunito is every screen's default (THEME-BIBLE §6.8).
        TitleStyle.InstallDefaultFont();
        OctoShoots.Game.Fx.HerColor.Load();
        // The title's music is the clean theme.
        if (OctoShoots.Game.Audio.MusicManager.Instance is { } music)
        {
            music.SetCorruption(0f);
            music.ExitCombat();
        }
        // Game flags mean a verification run: straight in, as before the title existed.
        var args = OS.GetCmdlineUserArgs();
        if (args.Any(a => !TitleOnlyFlags.Any(a.StartsWith)))
        {
            CallDeferred(MethodName.SkipToGame);
            return;
        }
        // The bindings (keyboard, mouse and controller), so menus answer the D-pad, A and B too.
        InputSetup.Register();
        AddChild(new PadMenus());
        Input.MouseMode = Input.MouseModeEnum.Visible;
        // Her crosshair, as in the game.
        OctoShoots.Game.Fx.GonadCursor.Use();
        foreach (string arg in args)
        {
            string Value(string prefix) => arg[prefix.Length..];
            if (arg == "--title-sample") GameSave.UseInMemory(SampleSave());
            else if (arg.StartsWith("--title-card=")) _openCard = Value("--title-card=");
            else if (arg.StartsWith("--title-launch=")) _autoLaunch = Value("--title-launch=");
            else if (arg.StartsWith("--title-pick=")) _autoPick = Value("--title-pick=");
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
        _puffers = new PufferlingView();
        AddChild(_puffers);
        _bell = new BellView();
        AddChild(_bell);
        _camera = new CameraRig();
        AddChild(_camera);
        _snow = new MarineSnow(900, new Vector3(22f, 7f, 17f), 0.11f);
        AddChild(_snow);
        _sun.SetStrength(_view.SunLight);
        _camera.SetReducedMotion(_view.ReducedMotion);

        var streams = new RunStreams(SeedCode.Parse(BackdropSeed));
        _backdrop = Task.Run(() =>
        {
            var map = TopDownGenerator.Generate(streams, LevelId.First);
            return LevelShape.Prepare(PlaneOptions.Default.Adventure ? AbyssReef.Apply(map) : map);
        });

        BuildUi();
    }

    void SkipToGame() => GetTree().ChangeSceneToFile(RunLaunch.GameScene);

    public override void _ExitTree() => OctoShoots.Game.Fx.GonadCursor.Release();

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
    void ShowBackdrop(LevelShape shape)
    {
        var map = shape.Map;
        _level.Show(shape);
        _world = new PlaneWorld(map, _tuning, new PlaneRun(_catalog, _tuning));
        _world.Mobs.Clear();
        _world.Ambushes.Clear();
        _puffers.Show(_world);
        _route = LevelValidator.ShortestPath(map, map.Start.Position, map.Exit.Position, clearance: 1.5f);
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

    /// <summary>The camera keeps her in the middle of the screen, inside the ring of items.</summary>
    Vector3 Focus(float alpha)
    {
        var p = System.Numerics.Vector2.Lerp(_world!.Player.PrevPosition, _world.Player.Position, alpha);
        return new Vector3(p.X, LevelMap.SwimBand, p.Y);
    }

    /// <summary>
    /// Where she aims: at the pointer, or with the controller (or the keys) at the item in focus — the point of her plane
    /// under it on the screen, from her.
    /// </summary>
    System.Numerics.Vector2 AimNow()
    {
        if (_world is null) return -System.Numerics.Vector2.UnitY;
        Vector2? screen = null;
        if (_byPad)
        {
            if (GetViewport().GuiGetFocusOwner() is Button b && _items.Any(it => it.Button == b)) screen = b.GetGlobalRect().GetCenter();
        }
        else screen = GetViewport().GetMousePosition();
        if (screen is not { } at) return _world.Player.Aim;
        var cam = _camera.Camera;
        Vector3 origin = cam.ProjectRayOrigin(at), dir = cam.ProjectRayNormal(at);
        if (Mathf.Abs(dir.Y) < 1e-4f) return _world.Player.Aim;
        var hit = origin + dir * ((LevelMap.SwimBand - origin.Y) / dir.Y);
        var to = new System.Numerics.Vector2(hit.X, hit.Z) - _world.Player.Position;
        return to.LengthSquared() > 1e-4f ? System.Numerics.Vector2.Normalize(to) : _world.Player.Aim;
    }

    /// <summary>Where she is on the screen (in the page's units).</summary>
    Vector2 HerOnPage()
    {
        if (_world is null) return RingCentre;
        var p = _world.Player.Position;
        return _camera.Camera.UnprojectPosition(new Vector3(p.X, LevelMap.SwimBand, p.Y)) / UiScale;
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
                _world.Step(new PlaneInput { Move = SwimInputNow(), Aim = AimNow() });
                _accumulator -= PlaneWorld.Dt;
                steps++;
            }
            if (steps == 4) _accumulator = 0;
            float alpha = (float)(_accumulator / PlaneWorld.Dt);
            _bell.Sync(_world, alpha, dt);
            _puffers.Sync(_world, dt);
            _camera.Track(Focus(alpha), dt);
            _snow.Tick(dt, Focus(alpha) + Vector3.Up * 6f, _camera.Camera.GlobalBasis);
            _level.UpdateCanopy(_world.Player.Position, dt, _camera.Camera.GlobalPosition);
        }
        LayoutRing();
        _shots.From = HerOnPage();
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

        // A light dimming of the whole sea, deeper toward the edges, so the ring's words read anywhere on it.
        var shade = new Gradient();
        shade.SetColor(0, new Color(0.01f, 0.05f, 0.07f, 0.12f));
        shade.SetColor(1, new Color(0.01f, 0.05f, 0.07f, 0.6f));
        var dim = new TextureRect
        {
            Texture = new GradientTexture2D { Gradient = shade, Width = 256, Height = 256, Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1.05f, 0.5f) },
            StretchMode = TextureRect.StretchModeEnum.Scale,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _ui.AddChild(dim);

        _ring = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _ring.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _ui.AddChild(_ring);

        // The logo, top left, clear of the ring.
        var logo = new VBoxContainer { Position = new Vector2(56f, 34f) };
        logo.AddThemeConstantOverride("separation", -6);
        var name = TitleStyle.Text("INK DEEP", 72, TitleStyle.Pearl, TitleStyle.Display);
        name.AddThemeColorOverride("font_shadow_color", new Color(1f, 0.478f, 0.42f, 0.55f));
        name.AddThemeConstantOverride("shadow_offset_x", 0);
        name.AddThemeConstantOverride("shadow_offset_y", 5);
        logo.AddChild(name);
        var sub = TitleStyle.Text("the absorbent jellyfish", 20, TitleStyle.WaterSoft, TitleStyle.Italic);
        logo.AddChild(sub);
        _logo = logo;
        _ui.AddChild(logo);

        // Her shots at the items, over the ring.
        _shots = new MenuShots();
        _shots.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        AddItem("continue", "Continue", () => Launch(LaunchMode.Continue, default));
        AddItem("new", "New run", NewRun);
        AddItem("seeded", "Seeded run", ToggleSeedBox);
        AddItem("pedia", "Sea-pedia", () => OpenCard("pedia"));
        AddItem("stats", "Statistics", () => OpenCard("stats"));
        AddItem("save", "Save & load", () => OpenCard("save"));
        AddItem("settings", "Settings", () => OpenCard("settings"));
        AddItem("quit", "Quit", () => GetTree().Quit());
        Refresh();

        var footer = TitleStyle.Text($"Ink Deep {GameVersion.Label} · prototype · one profile", 14, new Color(0.78f, 0.88f, 0.88f, 0.7f), TitleStyle.Body, HorizontalAlignment.Right);
        footer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomRight);
        footer.Position -= new Vector2(24f, 30f);
        footer.GrowHorizontal = Control.GrowDirection.Begin;
        _ui.AddChild(footer);

        _ui.AddChild(_shots);

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
        foreach (var (button, detail, _) in _items)
        {
            if (!button.GetParent<Control>().Visible) continue;
            TitleStyle.Bloom(button.GetParent<Control>(), 0.55f + 0.06f * i++, Reduced);
        }
        _items.First(it => it.Button.GetParent<Control>().Visible).Button.GrabFocus();
        if (_autoLaunch is { } launch)
            GetTree().CreateTimer(1.2).Timeout += () => Launch(launch == "continue" ? LaunchMode.Continue : LaunchMode.New, SeedCode.NewRandom());
        if (_autoPick is { } pick && _items.Any(it => it.Id == pick))
        {
            _byPad = true;
            Item(pick).Button.GrabFocus();
            GetTree().CreateTimer(2.0).Timeout += () => Item(pick).Button.EmitSignal(BaseButton.SignalName.Pressed);
        }
        if (_openCard is { } open)
        {
            if (open == "seed") ToggleSeedBox();
            else OpenCard(open);
        }
    }

    void AddItem(string id, string text, Action pressed)
    {
        // Each item a little stack, centred on its place in the ring: its word, and its detail line under it.
        var row = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", -2);
        var button = new Button { Text = text, Flat = true, FocusMode = Control.FocusModeEnum.All, Alignment = HorizontalAlignment.Center, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        button.AddThemeFontOverride("font", TitleStyle.BodyBold);
        button.AddThemeFontSizeOverride("font_size", 32);
        foreach (string state in new[] { "font_color", "font_pressed_color" }) button.AddThemeColorOverride(state, TitleStyle.Pearl);
        foreach (string state in new[] { "font_hover_color", "font_focus_color", "font_hover_pressed_color" }) button.AddThemeColorOverride(state, TitleStyle.Coral);
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        button.AddThemeColorOverride("font_outline_color", new Color(0.02f, 0.08f, 0.1f, 0.6f));
        button.AddThemeConstantOverride("outline_size", 4);
        button.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        row.AddChild(button);
        var detail = TitleStyle.Text("", 16, TitleStyle.WaterSoft, TitleStyle.Body, HorizontalAlignment.Center);
        detail.AddThemeColorOverride("font_outline_color", new Color(0.02f, 0.08f, 0.1f, 0.6f));
        detail.AddThemeConstantOverride("outline_size", 3);
        row.AddChild(detail);
        _ring.AddChild(row);
        // Chosen, she shoots her light at it; what it does happens as the shot strikes.
        button.Pressed += () =>
        {
            if (_launching || _shots.Busy) return;
            _shots.Fire(button.GetGlobalRect().GetCenter() / UiScale, () =>
            {
                Struck(button);
                pressed();
            });
        };
        button.MouseEntered += () => button.GrabFocus();
        button.FocusEntered += () => Focused(button, true);
        button.FocusExited += () => Focused(button, false);
        _items.Add((button, detail, id));
    }

    /// <summary>The item in focus swells a little (it is coral too: the button's focus colour).</summary>
    void Focused(Button button, bool on)
    {
        float seconds = Reduced ? 0.01f : 0.12f;
        button.PivotOffset = button.Size * 0.5f;
        button.CreateTween().TweenProperty(button, "scale", Vector2.One * (on ? 1.12f : 1f), seconds).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
    }

    /// <summary>Her shot strikes an item: it jolts, bigger for a beat, and settles.</summary>
    void Struck(Button button)
    {
        if (Reduced) return;
        button.PivotOffset = button.Size * 0.5f;
        var t = button.CreateTween();
        t.TweenProperty(button, "scale", Vector2.One * 1.3f, 0.06f).SetEase(Tween.EaseType.Out);
        t.TweenProperty(button, "scale", Vector2.One * (button.HasFocus() ? 1.12f : 1f), 0.25f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
    }

    /// <summary>
    /// The items on their ring round her: evenly spaced, the first at the top, clockwise; the inline card (the seed box,
    /// a confirmation) under its item, kept on the page.
    /// </summary>
    void LayoutRing()
    {
        var visible = _items.Where(it => it.Button.GetParent<Control>().Visible).ToList();
        var centre = RingCentre;
        for (int i = 0; i < visible.Count; i++)
        {
            var row = visible[i].Button.GetParent<Control>();
            float a = -Mathf.Pi * 0.5f + i * Mathf.Tau / visible.Count;
            var at = centre + new Vector2(Mathf.Cos(a) * RingRadii.X, Mathf.Sin(a) * RingRadii.Y);
            row.Size = row.GetCombinedMinimumSize();
            row.Position = at - row.Size * 0.5f;
        }
        if (_inline?.GetParent() is Control holder && holder.HasMeta("under") && holder.GetMeta("under").As<Button>() is { } under)
        {
            var row = under.GetParent<Control>();
            holder.Size = holder.GetCombinedMinimumSize();
            var at = new Vector2(row.Position.X + row.Size.X * 0.5f - holder.Size.X * 0.5f, row.Position.Y + row.Size.Y + 6f);
            var page = PageSize;
            holder.Position = new Vector2(Mathf.Clamp(at.X, 16f, page.X - holder.Size.X - 16f), Mathf.Clamp(at.Y, 16f, page.Y - holder.Size.Y - 16f));
        }
    }

    (Button Button, Label Detail, string Id) Item(string id) => _items.First(it => it.Id == id);

    /// <summary>Fills in the detail lines from the save.</summary>
    void Refresh()
    {
        var save = GameSave.Current;
        var p = save.Profile;
        var (cont, contDetail, _) = Item("continue");
        cont.GetParent<Control>().Visible = save.Run is not null;
        if (save.Run is { } run) contDetail.Text = $"{run.Where} · {run.Seed} · {TitleStyle.Clock(run.Elapsed)}";
        Item("seeded").Detail.Text = p.RecentSeeds.Count > 0 ? $"last: {p.RecentSeeds[0]}" : "type a seed to share a reef";
        Item("pedia").Detail.Text = $"{SeaPediaCard.PearlsFound(p)} / {PlaneRun.PortedPearls.Length} pearls · {SeaPediaCard.CreaturesMet(p)} / {SeaPediaCard.Creatures.Count} creatures";
        Item("stats").Detail.Text = p.Stats.Runs > 0 ? $"best: {StatsCard.Reach(p.Stats)} · {p.Stats.Runs} runs" : "no runs yet";
        Item("save").Detail.Text = save.Run is { } r ? $"saved run: {r.Where}" : "no saved run";
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
        box.AddChild(TitleStyle.Text($"Start a new run? Your saved run ({run.Where}) will be lost.", 17, TitleStyle.Ink, TitleStyle.BodyBold));
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
        var holder = new MarginContainer();
        holder.SetMeta("under", under);
        holder.AddChild(content);
        _ring.AddChild(holder);
        _inline = content;
        LayoutRing();
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
        _ring.CreateTween().TweenProperty(_ring, "modulate:a", 0.25f, 0.2f);
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
        _ring.CreateTween().TweenProperty(_ring, "modulate:a", 1f, 0.2f);
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

    public override void _Input(InputEvent e)
    {
        // The mouse moving: she follows it; the controller or the keys: she follows the item in focus.
        if (e is InputEventMouseMotion) _byPad = false;
        else if (e is InputEventJoypadButton or InputEventKey || e is InputEventJoypadMotion m && Mathf.Abs(m.AxisValue) > 0.3f) _byPad = true;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        // B on a controller goes back like Esc: closes a card or the seed box.
        bool padBack = e is (InputEventJoypadButton or InputEventJoypadMotion) && e.IsActionPressed(InputSetup.MenuBack);
        if (e is not InputEventKey { Pressed: true, Echo: false } key)
        {
            if (padBack) GoBack();
            return;
        }
        if (key.PhysicalKeycode == Key.Escape)
        {
            GoBack();
            return;
        }
        // W/S move through the menu like the arrow keys.
        if (_card is null && key.PhysicalKeycode is Key.W or Key.S or Key.A or Key.D && GetViewport().GuiGetFocusOwner() is not LineEdit)
        {
            // Round the ring: S and D on clockwise, W and A back.
            var visible = _items.Where(it => it.Button.GetParent<Control>().Visible).Select(it => it.Button).ToList();
            int at = visible.FindIndex(b => b.HasFocus());
            int next = ((at < 0 ? 0 : at) + (key.PhysicalKeycode is Key.S or Key.D ? 1 : -1) + visible.Count) % visible.Count;
            visible[next].GrabFocus();
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>Esc or B: close the open card, or the seed box / prompt under a menu item.</summary>
    void GoBack()
    {
        if (_card is not null) CloseCard();
        else if (_inline is not null)
        {
            CloseInline();
            _items.FirstOrDefault(it => it.Button.GetParent<Control>().Visible).Button?.GrabFocus();
        }
        GetViewport().SetInputAsHandled();
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
                Seed = "KELP 7Q2Z", Depth = 1, Level = 3, Items = new List<string> { "triple_tentacle", "mirror_scale" },
                Hp = 74, Shells = 31, Elapsed = 761, Foes = 41, ShellsCollected = 88,
                SavedAt = DateTime.Now.AddMinutes(-42).ToString("s", CultureInfo.InvariantCulture),
            },
        };
    }
}
