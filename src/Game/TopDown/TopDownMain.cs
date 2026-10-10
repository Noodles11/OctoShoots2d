using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using OctoShoots.Game.Controls;
using OctoShoots.Core;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using OctoShoots.Core.Run;
using OctoShoots.Core.Saves;
using OctoShoots.Game.Fx;
using OctoShoots.Game.Settings;
using OctoShoots.Game.Title;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The top-down game (DESIGN-TOPDOWN), current pass: generate a level from a seed, show it, and swim it; over the level's
/// shaft, Shift dives down into the next one, which has been readied underneath meanwhile (§4.6). Opened from the title
/// screen (RunLaunch); run directly with verification flags, it starts a run on its own and saves nothing.
/// WASD swims (north is up), Space dashes, Shift dives (InputSetup), the mouse aims, F1 opens the debug menu (any
/// level, any pearls), F3 toggles the debug map, R regenerates from the seed field.
/// Verification flags: --seed=, --cycle=, --depth=, --level=, --at=start|arch|cave|exit|hole|shop|cache|treasure|ambush|mob|boss,
/// --autopilot, --dive (dives whenever it can), --calm (no creatures), --fire, --pearls=, --hp=, --boss-hp=, --paused, --map, --f3, --no-focus,
/// --capture=dir --frames=a,b.
/// </summary>
public partial class TopDownMain : Node3D
{
    /// <summary>Every game starts on a random seed (--seed= pins one, for verification).</summary>
    SeedCode _seed = SeedCode.NewRandom();
    /// <summary>The level she is on: one hole leads down to the next (LevelPlan.NextOf).</summary>
    LevelId _id = LevelId.First;
    CombatView _combat = null!;
    CurrentView _currents = null!;
    VaseView _vases = null!;
    /// <summary>Achievements: their data, what earns them on this run, and the banner that announces them (§8).</summary>
    AchievementCatalog? _achievementData;
    PlaneAchievements _achievements = null!;
    AchievementBanner _achievementBanner = null!;
    string? _debugAward;
    BossView _boss = null!;
    PufferlingView _puffers = null!;
    BannerView _banner = null!;
    float? _bossHp;
    /// <summary>Verification: fire at the nearest mob; start the run with these pearls.</summary>
    bool _autoFire, _autoActive, _dbgSurge, _startPaused;
    float? _startHp;
    string[] _startPearls = Array.Empty<string>();
    HudView _hud = null!;
    ItemCatalog? _catalog;
    /// <summary>What Clementine carries through the rift (HP, pearls); a new seed or R starts a fresh run.</summary>
    PlaneRun? _run;
    string _status = "";
    float _statusTimer;
    Tuning _tuning = null!;
    LevelMap _map = null!;
    PlaneWorld _world = null!;

    LevelView _level = null!;
    BellView _bell = null!;
    CameraRig _camera = null!;
    DebugMapOverlay _debugMap = null!;
    MinimapView _minimap = null!;
    TopoMap _fullMap = null!;
    readonly HashSet<int> _visited = new();
    /// <summary>Unvisited places she has come near enough to see on the minimap.</summary>
    readonly HashSet<int> _spotted = new();
    readonly FogOfWar _fog = new();
    LineEdit _seedField = null!;
    Label _info = null!;
    double _accumulator;
    bool _dashLatched, _rLatched, _f3Latched, _escLatched, _backLatched;
    PauseMenu _pause = null!;
    ViewOptions _view = null!;
    DamageNumbers _damage = null!;
    Sfx _sfx = null!;
    float _lastChime = -1f;
    PanelContainer _debugPanel = null!;

    // On death: fade to the death splash, wait for the player, generate a new run's first level, fade back in.
    // (Between levels there is no splash: she dives.)
    enum Transition { None, FadingIn, Loading, Waiting, FadingOut }
    const float FadeSeconds = 0.35f;
    /// <summary>The first splash waits for Enter or a click once the level is ready; the death splash
    /// waits first, then builds the new run.</summary>
    bool _deathSplash;
    bool _confirmLatched;
    /// <summary>The run so far, for the death splash.</summary>
    int _runMobs, _runShells;
    float _runTime;
    LoadingSplash _splash = null!;
    Transition _transition;
    float _transitionTime;
    Task<LevelShape>? _nextMap;
    /// <summary>Seconds spent on the current level (sim time).</summary>
    float _levelTime;
    /// <summary>The run's play time so far (sim time: still while paused or diving), for the HUD clock.</summary>
    float _elapsed;
    bool _fireBlocked;
    bool Paused => _pause.Visible || _debug.Visible;
    /// <summary>F1: the debug menu (level jump, pearls). Once used, the run is neither saved nor recorded.</summary>
    DebugMenu _debug = null!;
    bool _f1Latched, _debugRun;

    /// <summary>Opened from the title: the run is recorded in the profile and saved at the start of every level.</summary>
    bool _persist;
    /// <summary>A seed she chose (or a verification seed): such runs never earn achievements.</summary>
    bool _customSeed;
    /// <summary>A saved run to put her back into, at the start of its level.</summary>
    SuspendedRun? _resume;
    PlaneProfileRecorder _recorder = null!;

    // The level below (DESIGN-TOPDOWN §4.6): made off the main thread as soon as she arrives on a level, then shown
    // under the hole, drawn only through the shaft until she dives.
    Task<LevelShape>? _belowTask;
    LevelView? _below;
    LevelMap? _belowMap;
    // The corruption (docs/CORRUPTION.md): its blades and creatures, the live texture, and the blades being laid out.
    CorruptionView _corruption = null!;
    BlightView _blight = null!;
    CleanseMeter _cleanse = null!;
    CorruptionTexture? _corruptTex;
    Task<CorruptionView.Layout>? _bladeTask;
    /// <summary>The level below's corruption, laid out off the main thread while she is still up here.</summary>
    Task<(CorruptionField Field, CorruptionView.Layout Layout)>? _belowCorruption;
    LevelMap? _belowCorruptionMap, _belowLayoutMap;
    CorruptionView.Layout? _belowLayout;
    /// <summary>Where the level below sits under this one: its start right under the hole, a level's drop down.</summary>
    Vector3 _belowOffset;
    /// <summary>The absolute position (xz) of the world's origin: each dive moves the world back by the hole's offset.</summary>
    Vector2 _origin;
    bool _diving, _diveLatched, _activeLatched;
    float _diveTime;
    System.Numerics.Vector2 _diveFrom;
    ReefLook _lookFrom = ReefLook.Shallows, _lookTo = ReefLook.Shallows;
    /// <summary>The dive, from the gathering stroke to settling on the level below.</summary>
    const float DiveSeconds = 1.6f;

    // Verification mode.
    string? _startAt;
    bool _autopilot, _autoDive, _calm;
    List<System.Numerics.Vector2> _route = new();
    int _routeProgress;
    string? _captureDir;
    readonly Queue<int> _captureFrames = new();
    int _frame;

    public override void _Ready()
    {
        ParseArgs();
        TakeLaunch();
        InputSetup.Register();
        AddChild(new PadMenus());
        _tuning = SettingsStore.LoadTuning();
        _catalog = LoadCatalog();
        _achievementData = LoadAchievements(_catalog);
        // Keep the GPU cool: the frame-rate cap from the view options (60 by default; captures run uncapped).
        var view = _view = SettingsStore.LoadView();
        Engine.MaxFps = _captureDir is null ? view.MaxFps : 0;
        if (_captureDir is not null) RenderingServer.ViewportSetMeasureRenderTime(GetViewport().GetViewportRid(), true);
        BuildEnvironment();
        _level = new LevelView();
        AddChild(_level);
        _bell = new BellView();
        AddChild(_bell);
        _combat = new CombatView();
        AddChild(_combat);
        _currents = new CurrentView();
        AddChild(_currents);
        _vases = new VaseView();
        AddChild(_vases);
        _boss = new BossView();
        AddChild(_boss);
        _puffers = new PufferlingView();
        AddChild(_puffers);
        _corruption = new CorruptionView();
        AddChild(_corruption);
        _blight = new BlightView();
        AddChild(_blight);
        _camera = new CameraRig();
        AddChild(_camera);
        _snow = new MarineSnow(900, new Vector3(22f, 7f, 17f), 0.11f);
        AddChild(_snow);
        _camera.ShakeEnabled = view.CameraShake;
        _camera.ZoomSetting = view.CameraZoom;
        _damage = new DamageNumbers();
        AddChild(_damage);
        _sfx = new Sfx();
        AddChild(_sfx);
        if (_achievementBanner is not null) _achievementBanner.Sound = _sfx;
        // Light bubbles: a merge chimes, a semitone higher for every bubble it holds (at most one chime per 60 ms).
        _combat.Merged += (_, bubbles) =>
        {
            if (_elapsed - _lastChime < 0.06f) return;
            _lastChime = _elapsed;
            _sfx.PlayPitched("merge", Mathf.Pow(2f, (bubbles - 2) / 12f), -8f);
        };
        _camera.SetReducedMotion(view.ReducedMotion);
        var sun = _sunLight = new SunLight();
        AddChild(sun);
        sun.SetStrength(view.SunLight);
        if (OS.GetCmdlineUserArgs().Contains("--dbg-noshadow"))
            foreach (var l in sun.FindChildren("*", "DirectionalLight3D", true, false)) ((DirectionalLight3D)l).ShadowEnabled = false;
        BuildUi();
        // From the title: the first level is shaped off the main thread behind the splash.
        if (_persist) StartFirstLevel();
        else Regenerate();
        if (_startPaused) CallDeferred(MethodName.TogglePause);
    }

    void ParseArgs()
    {
        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            string Value(string prefix) => arg[prefix.Length..];
            if (arg.StartsWith("--seed=") && SeedCode.TryParse(Value("--seed="), out var seed)) _seed = seed;
            else if (arg.StartsWith("--cycle=") && int.TryParse(Value("--cycle="), out int c)) _id = _id with { Cycle = Math.Max(1, c) };
            else if (arg.StartsWith("--depth=") && int.TryParse(Value("--depth="), out int d)) _id = _id with { Depth = Math.Clamp(d, 1, LevelPlan.Depths) };
            else if (arg.StartsWith("--level=") && int.TryParse(Value("--level="), out int l)) _id = _id with { Level = Math.Clamp(l, 1, 5) };
            else if (arg == "--dive") _autoDive = true;
            else if (arg == "--calm") _calm = true;
            else if (arg.StartsWith("--at=")) _startAt = Value("--at=");
            else if (arg == "--autopilot") _autopilot = true;
            else if (arg.StartsWith("--boss-hp=") && float.TryParse(Value("--boss-hp="), NumberStyles.Float, CultureInfo.InvariantCulture, out float bhp)) _bossHp = bhp;
            else if (arg == "--fire") _autoFire = true;
            else if (arg == "--use-active") _autoActive = true;
            else if (arg == "--dbg-surge") _dbgSurge = true;
            else if (arg.StartsWith("--award=")) _debugAward = Value("--award=");
            else if (arg == "--reset-profile")
            {
                // Verification: the saved profile's achievements (and so its unlocked pearls) are cleared.
                GameSave.Current.Profile.Achievements.Clear();
                GameSave.Write();
            }
            else if (arg.StartsWith("--dbg-slowmo=") && float.TryParse(Value("--dbg-slowmo="), NumberStyles.Float, CultureInfo.InvariantCulture, out float slow))
                Engine.TimeScale = Math.Clamp(slow, 0.02f, 1f);
            else if (arg == "--paused") _startPaused = true;
            else if (arg.StartsWith("--hp=") && float.TryParse(Value("--hp="), NumberStyles.Float, CultureInfo.InvariantCulture, out float hp)) _startHp = hp;
            else if (arg.StartsWith("--pearls=")) _startPearls = Value("--pearls=").Split(',', StringSplitOptions.RemoveEmptyEntries);
            else if (arg.StartsWith("--capture=")) _captureDir = Value("--capture=");
            else if (arg.StartsWith("--frames="))
                foreach (string f in Value("--frames=").Split(','))
                    if (int.TryParse(f, out int n)) _captureFrames.Enqueue(n);
        }
    }

    /// <summary>What the title screen asked for: a new run, a seeded run, or the saved one.</summary>
    void TakeLaunch()
    {
        if (RunLaunch.Pending is not { } mode)
        {
            _customSeed = OS.GetCmdlineUserArgs().Any(a => a.StartsWith("--seed="));
            _recorder = new PlaneProfileRecorder(new Profile());
            return;
        }
        RunLaunch.Pending = null;
        _persist = true;
        _recorder = new PlaneProfileRecorder(GameSave.Current.Profile);
        if (mode == LaunchMode.Continue && GameSave.Current.Run is { } saved && SeedCode.TryParse(saved.Seed, out var seed))
        {
            _seed = seed;
            _customSeed = saved.CustomSeed;
            _id = new LevelId(Math.Max(1, saved.Cycle), Math.Clamp(saved.Depth, 1, LevelPlan.Depths), Math.Clamp(saved.Level, 1, 5));
            _resume = saved;
            return;
        }
        _seed = RunLaunch.Seed;
        _customSeed = mode == LaunchMode.Seeded;
    }

    /// <summary>Saves the run as it stands at the start of this level (continuing puts her back here).</summary>
    void SaveRun()
    {
        if (!_persist || _debugRun || _run is null) return;
        GameSave.Current.Run = new SuspendedRun
        {
            Seed = _seed.ToString(),
            CustomSeed = _customSeed,
            Cycle = _id.Cycle,
            Depth = _id.Depth,
            Level = _id.Level,
            Items = _run.Items.ToList(),
            Hp = _run.Hp,
            Shells = _run.Shells,
            ActiveCharge = _run.ActiveCharge,
            Elapsed = _elapsed,
            Foes = _runMobs,
            ShellsCollected = _runShells,
            SavedAt = DateTime.Now.ToString("s", CultureInfo.InvariantCulture),
        };
        GameSave.Write();
    }

    Task<LevelShape>? _firstMap;

    void StartFirstLevel()
    {
        var stats = new List<(string, string)> { ("Seed", _seed.ToString() + (_customSeed ? "  (seeded)" : "")) };
        if (_resume is { } saved) stats.Add(("Carrying", $"{saved.Items.Count} pearls · {saved.Shells} shells · {Mathf.CeilToInt(saved.Hp)} HP"));
        _splash.Fill(_resume is not null ? "Back into the sea" : "A new dive", stats, _id.ToString());
        _splash.Modulate = Colors.White;
        _splash.Visible = true;
        _firstMap = Shape(_id);
    }

    /// <summary>
    /// A level made and readied for showing, off the main thread (both steps are pure work on data). The level below is
    /// made in the <paramref name="background"/>, on one thread, so the game keeps its frame rate.
    /// </summary>
    Task<LevelShape> Shape(LevelId id, bool background = false)
    {
        var streams = new RunStreams(_seed);
        return Task.Run(() => LevelShape.Prepare(TopDownGenerator.Generate(streams, id, background: background)));
    }

    void GoToTitle()
    {
        Input.MouseMode = Input.MouseModeEnum.Visible;
        if (_persist) GameSave.Write();
        GetTree().ChangeSceneToFile(RunLaunch.TitleScene);
    }

    WorldEnvironment _environment = null!;
    MarineSnow _snow = null!;
    SunLight _sunLight = null!;

    void BuildEnvironment()
    {
        _environment = MakeEnvironment(ReefLook.For(_id.Depth));
        AddChild(_environment);
    }

    /// <summary>The depth's look on the water, the sun and every reef shader.</summary>
    void ApplyLook() => ApplyLook(ReefLook.For(_id.Depth));

    void ApplyLook(ReefLook look)
    {
        look.Apply();
        look.ApplyTo(_environment.Environment);
        _sunLight.Configure(look.Sun, look.SunEnergy);
    }

    /// <summary>The water: shared with the title screen's backdrop.</summary>
    /// <summary>
    /// The water a depth is seen through (THEME-BIBLE §6.2): its ambient light and colour from the depth's look; the
    /// depth focus pass (topdown_post.gdshader) adds the murk, the rays and the grade on top. Shared with the title.
    /// </summary>
    public static WorldEnvironment MakeEnvironment(ReefLook look)
    {
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            TonemapMode = Godot.Environment.ToneMapper.Aces,
            TonemapExposure = 0.84f,
            FogEnabled = true,
            GlowEnabled = true,
            GlowIntensity = 0.6f,
            GlowBloom = 0.05f,
            GlowHdrThreshold = 1.0f,
            SsaoEnabled = true,
            SsaoRadius = 2.5f,
            SsaoIntensity = 2.2f,
        };
        look.ApplyTo(env);
        look.Apply();
        if (OS.GetCmdlineUserArgs().Contains("--dbg-nossao")) env.SsaoEnabled = false;
        return new WorldEnvironment { Environment = env };
    }

    void BuildUi()
    {
        var layer = new CanvasLayer { Layer = 2 };
        AddChild(layer);
        _debugMap = new DebugMapOverlay { Visible = OS.GetCmdlineUserArgs().Contains("--f3") };
        layer.AddChild(_debugMap);
        _hud = new HudView();
        layer.AddChild(_hud);
        _banner = new BannerView();
        layer.AddChild(_banner);
        _minimap = new MinimapView();
        layer.AddChild(_minimap);
        _cleanse = new CleanseMeter();
        layer.AddChild(_cleanse);
        // Tab: the whole level on one sheet, drawn by the minimap's rules (wall edges, fog, places only once spotted).
        _fullMap = new TopoMap
        {
            Visible = OS.GetCmdlineUserArgs().Contains("--map"), ShowLegend = true, MetresAcross = LevelMap.Size + 4f,
            ShowDetail = false, EdgesOnly = true, ArrowScale = 1.3f, ArrowColor = new Color(1f, 0.72f, 0.35f),
        };
        layer.AddChild(_fullMap);
        // Achievement banners: above the HUD, below the pause menu.
        var bannerLayer = new CanvasLayer { Layer = 3 };
        AddChild(bannerLayer);
        _achievementBanner = new AchievementBanner { ReducedMotion = _view.ReducedMotion };
        bannerLayer.AddChild(_achievementBanner);
        // Its sounds come from the game's Sfx (made in _Ready, before or after this).
        if (_sfx is not null) _achievementBanner.Sound = _sfx;
        // ESC: pause, restart the run, see the pearls she has absorbed. Drawn above the rest of the HUD.
        var pauseLayer = new CanvasLayer { Layer = 5 };
        AddChild(pauseLayer);
        _pause = new PauseMenu { Zoom = _view.CameraZoom };
        pauseLayer.AddChild(_pause);
        _pause.ResumePressed += Resume;
        _pause.ZoomChanged += zoom =>
        {
            _view.CameraZoom = zoom;
            _camera.ZoomSetting = zoom;
            SettingsStore.Save(SettingsStore.LoadTuning(), _view);
        };
        _splash = new LoadingSplash();
        pauseLayer.AddChild(_splash);
        _pause.RestartPressed += () =>
        {
            // A new run is a new reef: a fresh random seed. The one she leaves counts as abandoned.
            Resume();
            _recorder.RunAbandoned(_elapsed);
            _seed = SeedCode.NewRandom();
            _customSeed = false;
            _id = LevelId.First;
            Regenerate();
            Say("New run");
        };
        _debug = new DebugMenu();
        pauseLayer.AddChild(_debug);
        _debug.Closed += CloseDebug;
        _debug.GoTo += DebugGoTo;
        _debug.PearlToggled += (id, on) =>
        {
            MarkDebugRun();
            if (on) _run!.Add(id);
            else _run!.Remove(id);
            DebugPearlsChanged();
        };
        _debug.AllPearls += on =>
        {
            MarkDebugRun();
            foreach (string id in _run!.Items.ToList()) _run.Remove(id);
            if (on)
                foreach (string id in PlaneRun.PortedPearls)
                    if (_catalog?.Contains(id) == true) _run.Add(id);
            DebugPearlsChanged();
        };
        // The run was saved at the start of this level: she resumes there.
        _pause.QuitPressed += GoToTitle;

        // The debug panel (seed field, numbers, controls): hidden unless F3 is on.
        var panel = _debugPanel = new PanelContainer { Position = new Vector2(12f, 12f), Visible = OS.GetCmdlineUserArgs().Contains("--f3") };
        layer.AddChild(panel);
        var box = new VBoxContainer();
        panel.AddChild(box);
        var row = new HBoxContainer();
        box.AddChild(row);
        row.AddChild(new Label { Text = "Seed" });
        _seedField = new LineEdit { Text = _seed.ToString(), CustomMinimumSize = new Vector2(130f, 0f), FocusMode = Control.FocusModeEnum.Click };
        _seedField.TextSubmitted += _ =>
        {
            _seedField.ReleaseFocus();
            Regenerate(fromField: true);
        };
        row.AddChild(_seedField);
        var random = new Button { Text = "Random", FocusMode = Control.FocusModeEnum.None };
        random.Pressed += () =>
        {
            _seed = SeedCode.NewRandom();
            _id = LevelId.First;
            _seedField.Text = _seed.ToString();
            Regenerate();
        };
        row.AddChild(random);
        _info = new Label { Text = "" };
        box.AddChild(_info);
    }

    static AchievementCatalog? LoadAchievements(ItemCatalog? items)
    {
        if (items is null) return null;
        try
        {
            using var file = FileAccess.Open("res://data/achievements.json", FileAccess.ModeFlags.Read);
            return AchievementCatalog.FromJson(file.GetAsText(), items);
        }
        catch (Exception e)
        {
            GD.PushError($"No achievements: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Newly earned achievements: each is announced by its banner (with the pearl it unlocked, which the offers now
    /// draw from), and the profile is saved at once.
    /// </summary>
    void Earned(IReadOnlyList<string> ids)
    {
        if (ids.Count == 0) return;
        foreach (string id in ids)
            if (_achievementData is not null && _achievementData.TryGet(id, out var def))
                _achievementBanner.Enqueue(def, _catalog is not null && _catalog.TryGet(def.Pearl, out var pearl) ? pearl : null);
        if (_persist) GameSave.Write();
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

    void Regenerate(bool fromField = false, bool keepRun = false)
    {
        if (fromField)
        {
            if (!SeedCode.TryParse(_seedField.Text, out var parsed))
            {
                _info.Text = "Not a seed: 8 characters from A–Z and 2–9 (no 0, O, 1 or I)";
                return;
            }
            if (!parsed.Equals(_seed)) _id = LevelId.First;
            _seed = parsed;
        }
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var shape = LevelShape.Prepare(TopDownGenerator.Generate(new RunStreams(_seed), _id));
        ShowLevel(shape, keepRun, watch.ElapsedMilliseconds);
    }

    /// <summary>
    /// A level shown afresh (a new run, a resumed one, a regenerated one): the world back at the origin, no level below
    /// until it is made.
    /// </summary>
    void ShowLevel(LevelShape shape, bool keepRun, long ms)
    {
        DropBelow();
        _origin = Vector2.Zero;
        _camera.SetWorldOrigin(_origin);
        _snow.SetWorldOrigin(_origin);
        _level.Show(shape);
        _level.Place(Vector3.Zero, _origin);
        EnterLevel(shape.Map, keepRun, ms);
        // A verification start applies to the first level only (later levels begin at their start).
        PlaceAt(_startAt);
        _startAt = null;
        _camera.Track(Focus(1f), 0f, snap: true);
        Route();
    }

    /// <summary>
    /// She is on a level (shown already): its world, its places on the maps, the HUD, the save; and the level below starts
    /// being made.
    /// </summary>
    void EnterLevel(LevelMap map, bool keepRun, long ms, System.Numerics.Vector2? arriveAt = null)
    {
        _map = map;
        _levelTime = 0f;
        ApplyLook();
        if (!keepRun || _run is null)
        {
            ResetRunTotals();
            _run = new PlaneRun(_catalog, _tuning);
            // Her earned achievements are the run's unlocks (the profile's own set: one earned mid-run applies at once).
            _run.Unlocked = _recorder.Profile.Achievements;
            _achievements = new PlaneAchievements(_recorder.Profile, _customSeed);
            foreach (string id in _startPearls)
                if (_catalog?.Contains(id) == true) _run.Add(id);
            if (_startHp is { } hp) _run.Hp = hp;
            _startHp = null;
            bool continuing = _resume is not null;
            if (_resume is { } saved)
            {
                // Back where the save left her: what she carried onto this level, and the run's totals so far.
                foreach (string id in saved.Items)
                    if (_catalog?.Contains(id) == true) _run.Add(id);
                _run.Hp = Mathf.Min(saved.Hp, _run.MaxHp);
                _run.Shells = saved.Shells;
                _run.ActiveCharge = saved.ActiveCharge;
                _elapsed = _runTime = (float)saved.Elapsed;
                _runMobs = saved.Foes;
                _runShells = saved.ShellsCollected;
                _resume = null;
            }
            _recorder.StartRun(_seed.ToString(), _customSeed, _run.Items, continuing);
        }
        _run.Level = _id;
        _world = new PlaneWorld(_map, _tuning, _run);
        if (arriveAt is { } at) _world.Player.Position = _world.Player.PrevPosition = at;
        if (_calm)
        {
            _world.Mobs.Clear();
            _world.Ambushes.Clear();
        }
        if (_dbgSurge) InjectSurge();
        _achievements.EnterLevel();
        // A pearl unlocked by an achievement, offered here for the first time, wears a NEW chip.
        var fresh = new HashSet<string>();
        if (_catalog is not null)
            foreach (string pid in _world.Pearls.Select(q => q.ItemId).Concat(_world.Stands.Where(st => st.Kind == StandKind.Pearl).Select(st => st.ItemId)))
                if (_catalog.TryGet(pid, out var offered) && offered.Unlock is not null && !_recorder.Profile.SeenItems.Contains(pid)) fresh.Add(pid);
        _recorder.EnterLevel(_world, _id);
        SaveRun();
        _combat.Show(_world, _catalog, fresh);
        _currents.Clear();
        _vases.Show(_world);
        _puffers.Show(_world);
        _combat.Visible = _boss.Visible = _puffers.Visible = true;
        _damage.Clear();
        _camera.SetBubbleLights(_combat.Lights, 0);
        SetupCorruption(map);
        _boss.Show(_world);
        if (_bossHp is { } bossHp && _world.Boss is { } queen) queen.Hp = bossHp;
        _debugMap.SetMap(_map);
        _minimap.SetMap(_map);
        _fullMap.SetMap(_map);
        // Places are unknown ("?") until visited; she starts at the start.
        _visited.Clear();
        _visited.Add(_map.Pois.IndexOf(_map.Start));
        _minimap.SetVisited(_visited);
        _fog.Reset();
        _spotted.Clear();
        _minimap.SetFog(_fog, _spotted);
        // No mud cloud until this level's boss raises one.
        _minimap.SetMud(default, 0f, 0f);
        _fullMap.Mud = default;
        _fullMap.Fog = _fog;
        _fullMap.Seen = _fog;
        _fullMap.Spotted = _spotted;
        _fullMap.Visited = _visited;
        _seedField.Text = _seed.ToString();
        // The level's title, always on beside the clock (a new depth's first level names the depth too).
        _hud.LevelTitle = _id.Level == 1 ? $"{ReefLook.For(_id.Depth).Name} · {_id}" : _id.ToString();
        GD.Print($"Top-down level {_seed} {_id}: attempt {_map.Attempt + 1}, {ms} ms, {_map.Pois.Count} POIs{(_map.HasBoss ? ", boss" : "")}, {_map.Canopies.Count} canopy pieces, {_map.Decor.Count} decor");
        _belowTask = Shape(LevelPlan.NextOf(_seed, _id), background: true);
    }

    /// <summary>Verification: the autopilot's way to the exit.</summary>
    void Route()
    {
        _route = _autopilot ? LevelValidator.ShortestPath(_map, _world.Player.Position, _map.Exit.Position, clearance: 1f) : new();
        _routeProgress = 0;
    }

    /// <summary>Forgets the level below (a new run, a regenerated level).</summary>
    void DropBelow()
    {
        _belowTask = null;
        _below?.QueueFree();
        _below = null;
        _belowMap = null;
        _belowCorruption = null;
        _belowCorruptionMap = _belowLayoutMap = null;
        _belowLayout = null;
    }

    /// <summary>
    /// The level below is ready: shown under this one, its start right under the hole and a level's drop down, drawn only
    /// through the shaft (under a boss level's Crack, the Crack's glow fills it until the boss is freed).
    /// </summary>
    void ShowBelow(LevelShape shape)
    {
        _belowMap = shape.Map;
        _below = new LevelView();
        AddChild(_below);
        _below.Show(shape, below: true);
        var hole = _map.Exit.Position;
        var start = shape.Map.Start.Position;
        _belowOffset = new Vector3(hole.X - start.X, -LevelMap.LevelDrop, hole.Y - start.Y);
        _below.Place(_belowOffset, _origin + new Vector2(_belowOffset.X, _belowOffset.Z));
        _below.SetPortal(new Vector2(hole.X, hole.Y), Stamp.Pin, 2);
        // Its corruption, laid out off the main thread: the floor's ink for the dive, the blades for when she lands.
        if (PlaneOptions.Default.Corruption)
        {
            var map = _belowCorruptionMap = shape.Map;
            _belowCorruption = Task.Run(() =>
            {
                var field = CorruptionField.ForMap(map);
                return (field, CorruptionView.Build(map, field));
            });
        }
    }

    /// <summary>A level's corruption on screen: the live texture on its floor and maps, its creatures, and its blades.</summary>
    void SetupCorruption(LevelMap map)
    {
        _bladeTask = null;
        if (_world.Corruption is not { } field)
        {
            _corruptTex = null;
            _level.SetCorruption(null);
            _corruption.Show(null, null);
            _minimap.SetCorruption(null);
            _fullMap.Corruption = null;
            _blight.Show(_world);
            return;
        }
        _corruptTex = new CorruptionTexture(field);
        _level.SetCorruption(_corruptTex.Texture);
        _minimap.SetCorruption(_corruptTex.Texture);
        _fullMap.Corruption = _corruptTex.Texture;
        _blight.Show(_world);
        // The blades were laid out already if she dived here; otherwise they are laid out now, off the main thread.
        if (_belowLayout is { } ready && ReferenceEquals(_belowLayoutMap, map)) _corruption.Show(ready, _corruptTex.Texture);
        else
        {
            _corruption.Show(null, null);
            _bladeTask = CorruptionView.Plan(map, field);
        }
        _belowLayout = null;
        _belowLayoutMap = null;
    }

    /// <summary>The corruption's per-frame work: its texture, blades arriving, her light pushing the blades, the creatures.</summary>
    void SyncCorruption(float dt, float alpha)
    {
        if (_bladeTask is { IsCompleted: true } blades)
        {
            _bladeTask = null;
            if (blades.IsFaulted) GD.PushError(blades.Exception?.ToString());
            else if (_corruptTex is not null) _corruption.Show(blades.Result, _corruptTex.Texture);
        }
        if (_world.Corruption is { } field && _corruptTex is not null) _corruptTex.Update(field, dt);
        _corruption.SetHer(Focus(alpha));
        _blight.Sync(_world, dt, _camera.Camera);
        _cleanse.Track(_world, dt);
        MinimapView.FillBlight(_fullMap, _world);
        _minimap.SetBlight(_world);
    }

    /// <summary>Verification: start somewhere interesting.</summary>
    void PlaceAt(string? where)
    {
        System.Numerics.Vector2? at = where switch
        {
            "arch" => _map.Canopies.Where(c => c.Kind == CanopyKind.Arch).Select(c => (System.Numerics.Vector2?)c.Center).FirstOrDefault(),
            "cave" => _map.Caves.Select(c => (System.Numerics.Vector2?)(c.Mouth + c.Facing * 5f)).FirstOrDefault(),
            "exit" => _map.Exit.Position + new System.Numerics.Vector2(0f, _map.Exit.Radius + 3f),
            "hole" => _map.Exit.Position,
            // Just outside Queen Clam's arena, in water that leads in.
            "boss" => Enumerable.Range(0, 24).Select(i => i * Mathf.Tau / 24f)
                .Select(a => (System.Numerics.Vector2?)(_map.Exit.Position + new System.Numerics.Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (_map.Exit.Radius + 2.5f)))
                .FirstOrDefault(q => q is { } v && _world.Clear(v, 0.8f) && _world.LineOfSight(v, _map.Exit.Position)),
            "shop" => _map.Caves.Where(c => _map.Pois[c.Poi].Kind == PoiKind.Shop).Select(c => (System.Numerics.Vector2?)(c.Mouth - c.Facing * 1.5f)).FirstOrDefault(),
            "cache" => _map.Pois.Where(p => p.Kind == PoiKind.ShellCache).Select(p => (System.Numerics.Vector2?)p.Position).FirstOrDefault(),
            // A few metres south of the first pot, in water that sees it.
            "vase" => _world.Vases.Select(v => (System.Numerics.Vector2?)(v.Position + new System.Numerics.Vector2(0f, 4f))).FirstOrDefault(q => q is { } v && _world.Clear(v, 0.6f)),
            "ambush" => _map.Pois.Where(p => p.Kind == PoiKind.Ambush).Select(p => (System.Numerics.Vector2?)p.Position).FirstOrDefault(),
            "treasure" => _map.Caves.Where(c => _map.Pois[c.Poi].Kind == PoiKind.TreasureCave).Select(c => (System.Numerics.Vector2?)(c.Mouth - c.Facing * 2.5f)).FirstOrDefault(),
            "mob" => _world.Mobs.Select(m => (System.Numerics.Vector2?)(m.Position + new System.Numerics.Vector2(0f, 7f))).FirstOrDefault(q => q is { } v && _world.Clear(v, 0.6f)),
            _ => null,
        };
        if (at is not { } p) return;
        _world.Player.Position = _world.Player.PrevPosition = p;
    }

    Vector3 Focus(float alpha)
    {
        var p = System.Numerics.Vector2.Lerp(_world.Player.PrevPosition, _world.Player.Position, alpha);
        // Verification: `--dbg-follow=mob|fish` keeps the camera on the nearest pufferling instead.
        if (_follow is not null)
        {
            var from = p;
            System.Numerics.Vector2? at = _follow == "fish"
                ? _world.Fish.OrderBy(f => System.Numerics.Vector2.Distance(f.Position, from)).Select(f => (System.Numerics.Vector2?)f.Position).FirstOrDefault()
                : _world.Mobs.Where(m => m.Alive).OrderBy(m => System.Numerics.Vector2.Distance(m.Position, from)).Select(m => (System.Numerics.Vector2?)m.Position).FirstOrDefault();
            if (at is { } q) p = q;
        }
        return new Vector3(p.X, LevelMap.SwimBand, p.Y);
    }

    string? _follow = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--dbg-follow="))?["--dbg-follow=".Length..];

    Vector2 _renderSizeFor;

    /// <summary>
    /// On a big window the 3D scene renders at about the pixel count of a 16:9 screen MaxRenderHeight tall and FSR
    /// upscales it (the reef's water blur hides the difference); the HUD draws at full size. Rechecked when the window
    /// changes size.
    /// </summary>
    void CapRenderScale()
    {
        var vp = GetViewport();
        var size = vp.GetVisibleRect().Size;
        if (size == _renderSizeFor) return;
        _renderSizeFor = size;
        float budget = _view.MaxRenderHeight * _view.MaxRenderHeight * 16f / 9f;
        float scale = _view.MaxRenderHeight <= 0 || size.X * size.Y <= budget ? 1f : Mathf.Sqrt(budget / (size.X * size.Y));
        vp.Scaling3DMode = scale < 1f ? Viewport.Scaling3DModeEnum.Fsr : Viewport.Scaling3DModeEnum.Bilinear;
        vp.Scaling3DScale = scale;
        vp.FsrSharpness = 0.4f;
    }

    public override void _Process(double delta)
    {
        CapRenderScale();
        // F1, any time in a run: the debug menu.
        bool f1 = Input.IsPhysicalKeyPressed(Key.F1);
        if (f1 && !_f1Latched && _world is not null && _run is not null)
        {
            if (_debug.Visible) CloseDebug();
            else
            {
                if (_pause.Visible) _pause.Close();
                _debug.Open(_run, _catalog, _seed, _id);
            }
        }
        _f1Latched = f1;
        if (_world is null)
        {
            if (_firstMap is { IsCompleted: true } first)
            {
                _firstMap = null;
                if (first.IsFaulted) GD.PushError(first.Exception?.ToString());
                else
                {
                    ShowLevel(first.Result, keepRun: false, 0);
                    _transition = Transition.FadingOut;
                    _transitionTime = 0f;
                }
            }
            return;
        }
        float dt = (float)delta;
        // Banners run on game time: they wait while paused and under a splash.
        if (!Paused && _transition == Transition.None && !_deathSplash) _achievementBanner.Step(dt);
        if (_debugAward is { } award && _achievementData is not null && _achievementData.TryGet(award, out var shown))
        {
            // Verification: shows the banner (it awards nothing).
            _debugAward = null;
            _achievementBanner.Enqueue(shown, _catalog is not null && _catalog.TryGet(shown.Pearl, out var p0) ? p0 : null);
        }
        // The level below, once made, goes under the hole.
        if (_belowCorruption is { IsCompleted: true } preview)
        {
            _belowCorruption = null;
            if (preview.IsFaulted) GD.PushError(preview.Exception?.ToString());
            else if (_below is not null && ReferenceEquals(_belowMap, _belowCorruptionMap))
            {
                _below.SetCorruption(new CorruptionTexture(preview.Result.Field).Texture);
                _belowLayout = preview.Result.Layout;
                _belowLayoutMap = _belowMap;
            }
        }
        if (_belowTask is { IsCompleted: true } below && _transition == Transition.None)
        {
            _belowTask = null;
            if (below.IsFaulted) GD.PushError(below.Exception?.ToString());
            else ShowBelow(below.Result);
        }
        if (_diving)
        {
            StepDive(dt);
            Capture();
            return;
        }
        if (_transition != Transition.None)
        {
            StepTransition(dt);
            _hud.Elapsed = _elapsed;
            _hud.Track(_world, _catalog, dt);
            Capture();
            return;
        }
        HandleKeys();
        // Paused: the sea stands still under the menu.
        if (Paused)
        {
            _accumulator = 0;
            _hud.Elapsed = _elapsed;
            _hud.Track(_world, _catalog, 0f);
            Capture();
            return;
        }

        _accumulator += delta;
        int steps = 0;
        bool dived = false;
        while (_accumulator >= PlaneWorld.Dt && steps < 8 && !dived)
        {
            float hpBefore = _world.Player.Hp;
            _world.Step(ReadInput());
            _recorder.Observe(_world);
            Earned(_achievements.Observe(_world));
            // Healing from any source shows as a green number: the HP she gained beyond the hits she took this step.
            float taken = 0f;
            foreach (var e in _world.Events)
                if (e.Type == PlaneEventType.PlayerHit) taken += e.Size;
            float healed = _world.Player.Hp - hpBefore + taken;
            if (healed > 0.05f) _damage.ShowHeal(_world.Player.Position, healed);
            _levelTime += PlaneWorld.Dt;
            _elapsed += PlaneWorld.Dt;
            _accumulator -= PlaneWorld.Dt;
            steps++;
            foreach (var e in _world.Events)
            {
                _blight.OnEvent(e);
                CorruptionEvent(e);
                if (e.Type == PlaneEventType.Dived) dived = true;
                else if (e.Type == PlaneEventType.PlayerDefeated) dived = false;
                else if (e.Type == PlaneEventType.PearlCollected && _world.LastPearl is { } pearl) _hud.ShowPearl(pearl);
                else if (e.Type == PlaneEventType.ShellCollected) _hud.PulseShells();
                else if (e.Type == PlaneEventType.CannotAfford) Say("Not enough shells");
                else if (e.Type == PlaneEventType.AmbushSprung) Say("Ambush!");
                else if (e.Type == PlaneEventType.PlayerHit)
                {
                    _damage.Show(e);
                    // Harder hits jolt more: a mob shot (10) ~0.42, the boss's snap (18) ~0.5.
                    _camera.Shake(Mathf.Clamp(0.3f + e.Size * 0.011f, 0.3f, 0.55f));
                }
                else if (e.Type == PlaneEventType.MobDefeated)
                {
                    _damage.Show(e);
                    // Freed by light: the flash grows with the bubble that did it; a big one rings her bell and lifts
                    // the screen's edges (not with reduced motion).
                    if (_combat.Kill(e.Position) >= CombatView.BigKill)
                    {
                        if (!_view.ReducedMotion) _camera.EdgeFlash();
                        _sfx.PlayPitched("bell_ring", 1f, -10f);
                    }
                }
                else if (e.Type == PlaneEventType.Shot) _bell.Spend(e.Size);
                else if (e.Type is PlaneEventType.MobHit or PlaneEventType.BossHit) _damage.Show(e);
                else if (e.Type == PlaneEventType.ShotPopped) _combat.Pop(e.Position, e.Size * 1.7f, e.Direction);
                else if (e.Type == PlaneEventType.InkBlast)
                {
                    _combat.InkBlast(e.Position, e.Size);
                    _camera.Shake(0.12f);
                }
                else if (e.Type == PlaneEventType.ActiveUsed) _combat.ActiveUsed(_world);
                else if (e.Type == PlaneEventType.ShieldBlocked) _bell.ShieldRipple();
                else if (e.Type == PlaneEventType.VaseHit) _vases.Hit(_world, e.Position, e.Direction);
                else if (e.Type == PlaneEventType.VaseBroken)
                {
                    _vases.Break(_world, e.Position, e.Direction);
                    _camera.Shake(0.1f);
                }
                else if (e.Type == PlaneEventType.ActiveDenied) Say("Already at full health");
                else if (e.Type == PlaneEventType.BossLanded) _camera.Shake(0.9f);
                else if (e.Type == PlaneEventType.BossStagger) _camera.Shake(0.45f);
                else if (e.Type == PlaneEventType.BossSnap) _camera.Shake(0.35f);
                else if (e.Type == PlaneEventType.BossFreed)
                {
                    _camera.Shake(0.4f);
                    Say(_world.ExitOpen ? $"{PlaneBossTuning.Name} is freed! The Crack is open"
                        : $"{PlaneBossTuning.Name} is freed! Cleanse the arena to open the Crack ({Mathf.RoundToInt(_world.ArenaCleansed * 100f)}% of {Mathf.RoundToInt(CorruptionTuning.ArenaToOpen * 100f)}%)");
                }
            }
        }
        // Down the shaft: the dive takes over until she is on the level below.
        if (dived)
        {
            BeginDive();
            return;
        }
        // Out of HP: the run is over.
        if (_world.Defeated)
        {
            BeginDeath();
            return;
        }
        _statusTimer -= dt;
        if (steps == 8) _accumulator = 0;
        float alpha = (float)(_accumulator / PlaneWorld.Dt);

        _bell.Sync(_world, alpha, dt);
        _combat.Sync(_world, dt);
        _camera.SetBubbleLights(_combat.Lights, _combat.LightCount);
        _currents.Sync(_world, dt);
        _vases.Sync(dt);
        _puffers.Sync(_world, dt);
        SyncCorruption(dt, alpha);
        _damage.Tick(dt);
        _boss.Sync(_world, dt);
        if (!OS.GetCmdlineUserArgs().Contains("--dbg-nobanner")) _banner.Sync(_world.Boss);
        if (_world.Boss is { } b)
        {
            _minimap.SetMud(_world.ArenaCenter, _world.ArenaRadius, b.Cloud);
            _fullMap.Mud = new Vector4(_world.ArenaCenter.X, _world.ArenaCenter.Y, _world.ArenaRadius, b.Cloud);
        }
        _hud.Elapsed = _elapsed;
        _hud.Track(_world, _catalog, dt);
        _camera.Track(Focus(alpha), dt);
        _snow.Tick(dt, Focus(alpha) + Vector3.Up * 6f, _camera.Camera.GlobalBasis);
        _level.UpdateCanopy(_world.Player.Position, dt);
        var p = _world.Player;
        _debugMap.SetPlayer(_world.Player.Position);
        _minimap.Track(p.Position, p.Velocity);
        _minimap.SetHealth(_world.Run.MaxHp > 0f ? p.Hp / _world.Run.MaxHp : 0f);
        // Lantern Pearl: a brighter glow shows more of the level around her.
        float glow = _world.Run.Loadout.Stats[Stat.Glow];
        _fog.Reveal(p.Position, MinimapView.RevealRadius * (1f + 0.5f * (glow - 1f)), MinimapView.RevealSoft);
        for (int i = 0; i < _map.Pois.Count; i++)
            if (System.Numerics.Vector2.Distance(_map.Pois[i].Position, p.Position) <= MinimapView.SpotRadius) _spotted.Add(i);
        // Entering a place names it under the minimap.
        var inside = _map.Pois.Where(poi => System.Numerics.Vector2.Distance(poi.Position, p.Position) <= poi.Radius + 1.5f)
            .OrderBy(poi => System.Numerics.Vector2.Distance(poi.Position, p.Position)).FirstOrDefault();
        // The start needs no name under the map.
        bool named = inside is not null && inside.Kind != PoiKind.Start;
        _minimap.ShowPlace(named ? LevelView.PlaceName(_map, inside!.Kind) : null, named ? LevelView.PoiColor(inside!.Kind) : Colors.White);
        if (inside is not null) _visited.Add(_map.Pois.IndexOf(inside));
        float side = Mathf.Min(GetViewport().GetVisibleRect().Size.X, GetViewport().GetVisibleRect().Size.Y) - 80f;
        _fullMap.Size = new Vector2(side, side);
        _fullMap.Position = (GetViewport().GetVisibleRect().Size - _fullMap.Size) * 0.5f;
        _fullMap.Player = p.Position;
        if (p.Velocity.LengthSquared() > 0.04f) _fullMap.PlayerHeading = p.Velocity;
        int left = _world.Mobs.Count(m => m.Alive);
        _info.Text = $"{_id} · mobs {left}/{_world.Mobs.Count} · pearls {_run!.Items.Count}   attempt {_map.Attempt + 1}   pos {p.Position.X:0.0}, {p.Position.Y:0.0}   " +
                     $"{Engine.GetFramesPerSecond():0} FPS" + (_statusTimer > 0f ? $"   {_status}" : "") +
                     "\nWASD swim · Space dash · hold the mouse or arrows to shoot · Shift over the shaft dives · Tab map · Esc pause · F1 debug menu · F3 debug map · R regenerate";
        Capture();
    }

    // ───────────────────────── the dive (DESIGN-TOPDOWN §4.6) ─────────────────────────

    /// <summary>She dives: the level is cleared, the sim stops, and the dive plays out over the level below.</summary>
    void BeginDive()
    {
        _accumulator = 0;
        _recorder.LevelCleared(_world, _levelTime);
        Earned(_achievements.LevelCleared(_world));
        AddToRun();
        _diving = true;
        _diveTime = 0f;
        _diveFrom = _world.Player.Position;
        var next = LevelPlan.NextOf(_seed, _id);
        _lookFrom = ReefLook.For(_id.Depth);
        _lookTo = ReefLook.For(next.Depth);
        _bell.Kick();
        _hud.Diving = true;
        if (_captureDir is not null) GD.Print($"Dive begins at frame {_frame}");
        // The level's life stays behind (only she goes down): it fades out with the iris (FadeLevelLife).
        _banner.Visible = false;
    }

    static float Ease(float t) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp(t, 0f, 1f));

    /// <summary>
    /// One frame of the dive. She gathers (a flare), then a hard stroke turns her head-first and she sinks a level's drop
    /// down the shaft in pulses, then rights herself and settles. The camera falls with her and pulls in; the level above
    /// opens like an iris from the hole outward, revealing the level below; the focus, the sea surface and the water
    /// sink with her; through the Crack, the next depth's look blends in. If the level below is not made yet, she holds
    /// at the lip of the shaft until it is.
    /// </summary>
    void StepDive(float dt)
    {
        _diveTime += dt;
        if (_below is null && _diveTime > 0.25f) _diveTime = 0.25f;
        float t = _diveTime, k = Mathf.Clamp(t / DiveSeconds, 0f, 1f);
        // The descent: in three pulses, each a quick lurch then a glide.
        float sink = Ease((t - 0.25f) / 0.95f);
        sink = Mathf.Clamp(sink + 0.04f * Mathf.Sin(Mathf.Clamp((t - 0.25f) / 0.95f, 0f, 1f) * Mathf.Pi * 3f), 0f, 1f);
        float drop = sink * LevelMap.LevelDrop;
        // A small rise as she gathers, before the stroke.
        float gather = Mathf.Sin(Mathf.Clamp(t / 0.25f, 0f, 1f) * Mathf.Pi) * 0.35f;
        _bell.DiveDepth = drop - gather;
        _bell.DiveTurn = Ease((t - 0.2f) / 0.3f) * (1f - Ease((t - 1.1f) / 0.4f));
        _bell.Sync(_world, 1f, dt);

        var focus = new Vector3(_diveFrom.X, LevelMap.SwimBand - drop, _diveFrom.Y);
        // In and back out, easing to rest at both ends (sin²): the view is still when the world is moved back.
        float pull = Mathf.Sin(Mathf.Pi * k);
        _camera.Zoom = 1f - 0.35f * pull * pull;
        _camera.Track(focus, dt);
        _camera.SetSwimLevel(-drop);
        var look = _lookFrom == _lookTo ? _lookFrom : ReefLook.Blend(_lookFrom, _lookTo, sink);
        if (_lookFrom != _lookTo) ApplyLook(look);
        look.SetDrop(drop);
        _snow.Tick(dt, focus + Vector3.Up * 6f, _camera.Camera.GlobalBasis);

        // The iris: the level above is cut away from the hole outward; the level below shows inside the same ring.
        var hole = new Vector2(_map.Exit.Position.X, _map.Exit.Position.Y);
        // It opens from nothing (its dissolving edge reaches 3 m past the radius) to the shaft's width as she gathers,
        // then sweeps outward.
        float radius = t < 0.35f ? Mathf.Lerp(-3f, 6f, Ease(t / 0.35f)) : Mathf.Lerp(6f, 150f, Mathf.Pow(Ease((t - 0.35f) / 0.9f), 2f));
        _level.SetPortal(hole, radius, 1);
        _below?.SetPortal(hole, Mathf.Max(radius, Stamp.Pin), 2);
        // What lived on the level above goes with it: each piece as the iris's edge passes, the rest with the descent;
        // the Crack's glow, light and bubbles fade as she sinks through it. All gone before the level is let go.
        float stay = 1f - Ease((t - 0.3f) / 1.0f);
        // The fading edge grows out from the shaft over the first half second (what sits over it does not just vanish),
        // then follows the iris.
        FadeLevelLife(hole, Mathf.Lerp(-5f, radius, Ease(t / 0.45f)), stay);
        _level.FadeCrack(stay);
        _damage.Tick(dt);
        _hud.Elapsed = _elapsed;
        _hud.Track(_world, _catalog, dt);
        if (_diveTime >= DiveSeconds && _below is not null) FinishDive();
    }

    /// <summary>
    /// She is down: the level below becomes the level, and the whole world (camera, specks, her strands) moves back by the
    /// level's offset in the same frame, so nothing on screen moves. Its patterns are drawn from its absolute origin, so
    /// they do not move either.
    /// </summary>
    void FinishDive()
    {
        var offset = _belowOffset;
        var shift = -offset;
        var arrived = _diveFrom - new System.Numerics.Vector2(offset.X, offset.Z);
        _level.QueueFree();
        _level = _below!;
        _below = null;
        var map = _belowMap!;
        _belowMap = null;
        _origin += new Vector2(offset.X, offset.Z);
        _level.Promote(_origin);
        _camera.Shift(shift);
        _camera.SetWorldOrigin(_origin);
        _snow.SetWorldOrigin(_origin);
        _camera.SetSwimLevel(0f);
        _camera.Zoom = 1f;
        _snow.Shift(shift);
        _bell.Shift(shift);
        _bell.DiveDepth = 0f;
        _bell.DiveTurn = 0f;
        _banner.Visible = true;
        _hud.Diving = false;
        _diving = false;
        _id = LevelPlan.NextOf(_seed, _id);
        EnterLevel(map, keepRun: true, 0, arrived);
        foreach (var view in LevelLife) ResetFade(view);
        Route();
        if (_captureDir is not null) GD.Print($"Dive finishes at frame {_frame}");
    }

    /// <summary>The views of a level's life: its creatures, pickups and shots, Queen Clam, damage numbers, currents.</summary>
    IEnumerable<Node> LevelLife => new Node[] { _combat, _puffers, _boss, _damage, _currents, _vases };

    /// <summary>
    /// Fades the old level's life during the dive: each piece fades out over a few metres as the iris's dissolving edge
    /// passes over it (as the reef under it does), and whatever is left fades with <paramref name="stay"/> (1 → 0), so
    /// nothing is switched off in one frame. It only ever fades further.
    /// </summary>
    void FadeLevelLife(Vector2 hole, float radius, float stay)
    {
        foreach (var view in LevelLife) FadeTree(view, hole, radius, stay);
    }

    static void FadeTree(Node node, Vector2 hole, float radius, float stay)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is GeometryInstance3D g && g.IsInsideTree())
            {
                var p = g.GlobalPosition;
                float keep = Mathf.Clamp((new Vector2(p.X, p.Z).DistanceTo(hole) - (radius - 3f)) / 5f, 0f, 1f) * stay;
                g.Transparency = Mathf.Max(g.Transparency, 1f - keep);
                // Fading, it casts no shadow on the level below (a faded clam must not leave hers behind).
                if (g.Transparency > 0f && g.CastShadow != GeometryInstance3D.ShadowCastingSetting.Off)
                {
                    g.SetMeta("dive_shadow", (int)g.CastShadow);
                    g.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
                }
            }
            FadeTree(child, hole, radius, stay);
        }
    }

    /// <summary>A new level's life is drawn whole again.</summary>
    static void ResetFade(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is GeometryInstance3D g)
            {
                g.Transparency = 0f;
                if (g.HasMeta("dive_shadow"))
                {
                    g.CastShadow = (GeometryInstance3D.ShadowCastingSetting)(int)g.GetMeta("dive_shadow");
                    g.RemoveMeta("dive_shadow");
                }
            }
            ResetFade(child);
        }
    }

    /// <summary>Adds the level she is leaving to the run's totals.</summary>
    void AddToRun()
    {
        _runMobs += _world.Stats.MobsDefeated;
        _runShells += _world.Stats.ShellsCollected;
        _runTime += _levelTime;
    }

    void ResetRunTotals()
    {
        _runMobs = _runShells = 0;
        _runTime = 0f;
        _elapsed = 0f;
    }

    /// <summary>The death splash: how far the run got, then a new run with a new seed.</summary>
    void BeginDeath()
    {
        _accumulator = 0;
        AddToRun();
        _recorder.RunDied(_world, _runTime);
        // The run is over: nothing to continue (a debug run leaves the save as it was).
        if (_persist && !_debugRun)
        {
            GameSave.Current.Run = null;
            GameSave.Write();
        }
        var time = TimeSpan.FromSeconds(_runTime);
        var stats = new List<(string, string)>
        {
            ("Reached", _id.ToString()),
            ("Time", $"{(int)time.TotalMinutes}:{time.Seconds:00}"),
            ("Foes defeated", $"{_runMobs}"),
            ("Shells collected", $"{_runShells}"),
            ("Pearls absorbed", $"{_run!.Items.Count}"),
            ("Seed", _seed.ToString()),
        };
        _deathSplash = true;
        _splash.Fill("Clementine's light went out", stats, "Everything she carried is lost.");
        OpenSplash();
    }

    void OpenSplash()
    {
        _splash.Modulate = new Color(1f, 1f, 1f, 0f);
        _splash.Visible = true;
        _transition = Transition.FadingIn;
        _transitionTime = 0f;
    }

    /// <summary>Enter, a click or A, as a press (a key or button already held when the prompt appears does not count).</summary>
    bool Confirmed()
    {
        bool down = Input.IsPhysicalKeyPressed(Key.Enter) || Input.IsPhysicalKeyPressed(Key.KpEnter) || Input.IsMouseButtonPressed(MouseButton.Left)
                    || Input.IsActionPressed(InputSetup.MenuAccept)
                    // Captures go on by themselves after a moment.
                    || _captureDir is not null && _transitionTime > 2f;
        bool pressed = down && !_confirmLatched;
        _confirmLatched = down;
        return pressed;
    }

    void StartLoading()
    {
        DropBelow();
        _nextMap = Shape(_id);
        _transition = Transition.Loading;
        _transitionTime = 0f;
    }

    void StepTransition(float dt)
    {
        _transitionTime += dt;
        switch (_transition)
        {
            case Transition.FadingIn:
                _splash.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(_transitionTime / FadeSeconds, 0f, 1f));
                if (_transitionTime < FadeSeconds) return;
                if (_deathSplash)
                {
                    // Dead: read the page, then start over.
                    _splash.Prompt(_persist ? "Enter, click or A: a new run  ·  Esc or B: back to the title" : "Press Enter, click or A to start a new run");
                    _confirmLatched = true;
                    _transition = Transition.Waiting;
                    _transitionTime = 0f;
                }
                // The generator is pure and deterministic: it runs off the main thread while the splash shows.
                else StartLoading();
                break;
            case Transition.Loading:
                if (_nextMap is null || !_nextMap.IsCompleted) return;
                var task = _nextMap;
                _nextMap = null;
                if (task.IsFaulted) GD.PushError(task.Exception?.ToString());
                else ShowLevel(task.Result, keepRun: !_deathSplash, (long)(_transitionTime * 1000f));
                if (_deathSplash)
                {
                    // A new run: straight in.
                    Say($"New run · seed {_seed}");
                    _transition = Transition.FadingOut;
                }
                else
                {
                    // The level is ready: wait until the player has read the page.
                    _splash.Prompt("Press Enter, click or A to continue");
                    _confirmLatched = true;
                    _transition = Transition.Waiting;
                }
                _transitionTime = 0f;
                break;
            case Transition.Waiting:
                if (_deathSplash && _persist && (Input.IsPhysicalKeyPressed(Key.Escape) || Input.IsActionPressed(InputSetup.MenuBack)))
                {
                    GoToTitle();
                    return;
                }
                if (!Confirmed()) return;
                // The click that closes the splash must not fire a shot.
                _fireBlocked = true;
                if (_deathSplash)
                {
                    // A clean new game: new seed, the first level, no pearls or shells.
                    _seed = SeedCode.NewRandom();
                    _customSeed = false;
                    _id = LevelId.First;
                    ResetRunTotals();
                    _splash.Working("Shaping a new reef");
                    StartLoading();
                }
                else
                {
                    _transition = Transition.FadingOut;
                    _transitionTime = 0f;
                }
                break;
            case Transition.FadingOut:
                _splash.Modulate = new Color(1f, 1f, 1f, 1f - Mathf.Clamp(_transitionTime / FadeSeconds, 0f, 1f));
                if (_transitionTime < FadeSeconds) return;
                _splash.Visible = false;
                _transition = Transition.None;
                break;
        }
    }

    void Say(string text)
    {
        _status = text;
        _statusTimer = 3f;
        _hud.ShowMessage(text);
    }

    void TogglePause()
    {
        if (Paused) Resume();
        else
        {
            _pause.Open(_world.Run, _catalog, _seed.ToString(), _id.ToString(), _world.Player.Hp, _achievementData);
            if (_pad) _pause.FocusFirst();
        }
    }

    /// <summary>The corruption war's moments: shakes, sounds and words (the sights are BlightView's).</summary>
    void CorruptionEvent(in PlaneEvent e)
    {
        switch (e.Type)
        {
            case PlaneEventType.BlightrootBurst:
                _camera.Shake(0.35f);
                _sfx.PlayPitched("bell_ring", 0.75f, -8f);
                Say("A Blightroot starves and bursts");
                break;
            case PlaneEventType.MurklingBurst:
                _sfx.PlayPitched("pop", 0.6f, -6f);
                break;
            case PlaneEventType.VineCoiled:
                _camera.Shake(0.22f);
                if (!_saidCoil)
                {
                    _saidCoil = true;
                    Say("A gloomvine has you: dash to break free");
                }
                break;
            case PlaneEventType.ValveSeen:
                if (!_saidValve)
                {
                    _saidValve = true;
                    Say("A valve of darkness: dash through it free, or cleanse its edges");
                }
                break;
            case PlaneEventType.ValveCleared:
                _sfx.PlayPitched("merge", 0.8f, -6f);
                break;
            case PlaneEventType.CrustBroken:
                _camera.Shake(0.3f);
                _sfx.PlayPitched("bell_ring", 1.2f, -10f);
                Say(e.Size > 0f ? $"Crust broken: {Mathf.RoundToInt(e.Size)} left" : "The last of her crust falls away");
                break;
            case PlaneEventType.CrackOpened:
                Say("The arena is clean: the Crack opens");
                break;
        }
    }

    bool _saidCoil, _saidValve;

    void CloseDebug()
    {
        _debug.Close();
        // The click that closed the menu must not fire a shot.
        _fireBlocked = true;
    }

    /// <summary>
    /// The first debug change makes this a debug run: it is not saved, and from here on it is recorded into a throwaway
    /// profile (the statistics keep what came before).
    /// </summary>
    void MarkDebugRun()
    {
        if (_debugRun || _run is null) return;
        _debugRun = true;
        _recorder = new PlaneProfileRecorder(new Profile());
        _recorder.StartRun(_seed.ToString(), _customSeed, _run.Items, continuing: false);
        _recorder.EnterLevel(_world, _id);
        // Achievements too: a debug run never earns one into the saved profile (its banners still show).
        _achievements = new PlaneAchievements(_recorder.Profile, _customSeed);
        Say("Debug run: not saved");
    }

    /// <summary>Debug: on to any level of the seed, keeping what she carries (at full HP if asked).</summary>
    void DebugGoTo(LevelId id, bool fullHp)
    {
        if (_diving || _transition != Transition.None)
        {
            Say("Wait for the level to finish loading");
            return;
        }
        MarkDebugRun();
        _run!.Hp = fullHp ? _run.MaxHp : Mathf.Min(_world.Player.Hp, _run.MaxHp);
        _id = id;
        CloseDebug();
        Regenerate(keepRun: true);
        Say($"Debug: {_id}");
    }

    /// <summary>Debug: pearls switched; her HP stays within the new maximum, and the menu's boxes follow.</summary>
    void DebugPearlsChanged()
    {
        _world.Player.Hp = Mathf.Min(_world.Player.Hp, _run!.MaxHp);
        _run.Hp = _world.Player.Hp;
        _debug.Refresh(_run);
    }

    void Resume()
    {
        _pause.Close();
        // The click that closed the menu must not fire a shot.
        _fireBlocked = true;
    }

    void HandleKeys()
    {
        // Esc or Start pauses and resumes; B also resumes (and closes the debug menu).
        bool esc = Input.IsActionPressed(InputSetup.Pause);
        bool back = Input.IsActionPressed(InputSetup.MenuBack);
        if (esc && !_escLatched)
        {
            if (_debug.Visible) CloseDebug();
            else if (_seedField.HasFocus()) _seedField.ReleaseFocus();
            else TogglePause();
        }
        else if (back && !_backLatched && (Paused || _debug.Visible))
        {
            if (_debug.Visible) CloseDebug();
            else Resume();
        }
        _escLatched = esc;
        _backLatched = back;
        if (Paused) return;
        bool typing = _seedField.HasFocus();
        bool r = Input.IsPhysicalKeyPressed(Key.R) && !typing;
        if (r && !_rLatched) Regenerate(fromField: true);
        _rLatched = r;
        bool f3 = Input.IsPhysicalKeyPressed(Key.F3);
        if (f3 && !_f3Latched)
        {
            _debugMap.Visible = !_debugMap.Visible;
            _debugPanel.Visible = _debugMap.Visible;
        }
        _f3Latched = f3;
        if (!OS.GetCmdlineUserArgs().Contains("--map")) _fullMap.Visible = Input.IsActionPressed(InputSetup.Review) && !typing;
    }

    /// <summary>Verification (`--dbg-surge`): a full-strength surge through the canyon she is in, a second into the level.</summary>
    void InjectSurge()
    {
        int best = -1;
        float bestDist = float.MaxValue;
        for (int i = 0; i < _map.Corridors.Count; i++)
        {
            if (_map.Corridors[i].Kind is not (CorridorKind.Main or CorridorKind.Side)) continue;
            ReefDirector.Nearest(_map.Corridors[i], _world.Player.Position, out float d, out _);
            if (d < bestDist) (best, bestDist) = (i, d);
        }
        if (best >= 0)
            _world.Director.Inject(new ReefEvent { Kind = ReefEventKind.CurrentSurge, Start = 1f, Duration = ReefDirectorTuning.SurgeSeconds, Corridor = best, Speed = ReefDirectorTuning.SurgeSpeedMax });
    }

    PlaneInput ReadInput()
    {
        var input = new PlaneInput();
        if (_autopilot && _route.Count > 0)
        {
            // Follow the route a couple of cells ahead of the nearest point on it.
            var pos = _world.Player.Position;
            for (int i = _routeProgress; i < Math.Min(_route.Count, _routeProgress + 12); i++)
                if (System.Numerics.Vector2.Distance(_route[i], pos) < System.Numerics.Vector2.Distance(_route[_routeProgress], pos)) _routeProgress = i;
            var to = _route[Math.Min(_route.Count - 1, _routeProgress + 2)] - pos;
            if (to.Length() > 0.3f) input.Move = System.Numerics.Vector2.Normalize(to);
            input.Dive = _autoDive && _world.CanDive;
            AutoFire(ref input);
            return input;
        }
        if (_seedField.HasFocus()) return input;
        // Everything comes from the bindings (InputSetup): keyboard and mouse, or a controller.
        Vector2 stick = Input.GetVector(InputSetup.Left, InputSetup.Right, InputSetup.Forward, InputSetup.Back);
        input.Move = new System.Numerics.Vector2(stick.X, stick.Y);
        bool dash = Input.IsActionPressed(InputSetup.Dash);
        input.Dash = dash && !_dashLatched;
        _dashLatched = dash;
        bool dive = Input.IsActionPressed(InputSetup.Dive);
        input.Dive = dive && !_diveLatched || _autoDive && _world.CanDive;
        _diveLatched = dive;
        bool use = Input.IsActionPressed(InputSetup.Active);
        input.UseActive = use && !_activeLatched || _autoActive && _world.Run.ActiveCharge >= 1f;
        _activeLatched = use;
        if (AutoFire(ref input)) return input;
        bool held = Input.IsActionPressed(InputSetup.Fire);
        if (!held) _fireBlocked = false;
        Vector2 aim = Input.GetVector(InputSetup.AimLeft, InputSetup.AimRight, InputSetup.AimUp, InputSetup.AimDown);
        if (_pad)
        {
            // Controller: the right stick aims (she keeps the last aim when it is let go), the trigger shoots.
            if (aim.LengthSquared() > 0.04f) _padAim = new System.Numerics.Vector2(aim.X, aim.Y);
            input.Aim = _padAim.LengthSquared() > 1e-6f ? System.Numerics.Vector2.Normalize(_padAim) : _world.Player.Aim;
            input.Fire = held && !_fireBlocked;
        }
        else if (aim.LengthSquared() > 0f)
        {
            // The aim keys (the arrows) aim and shoot at once.
            input.Aim = System.Numerics.Vector2.Normalize(new System.Numerics.Vector2(aim.X, aim.Y));
            input.Fire = true;
        }
        else
        {
            // The mouse: shoot toward the pointer while the button is held.
            input.Aim = MouseAim();
            input.Fire = held && !_fireBlocked;
        }
        return input;
    }

    /// <summary>
    /// Which device she is played with: a controller once a pad button or stick is used, keyboard and mouse again on a key,
    /// a click or a real mouse move. The pointer hides while the controller plays.
    /// </summary>
    public override void _Input(InputEvent e)
    {
        bool pad = e switch
        {
            InputEventJoypadButton => true,
            InputEventJoypadMotion m => Mathf.Abs(m.AxisValue) > 0.4f || _pad,
            InputEventKey or InputEventMouseButton => false,
            InputEventMouseMotion mm => mm.Relative.LengthSquared() < 9f && _pad,
            _ => _pad,
        };
        if (pad == _pad) return;
        _pad = pad;
        Input.MouseMode = pad ? Input.MouseModeEnum.Hidden : Input.MouseModeEnum.Visible;
        if (pad && _pause.Visible) _pause.FocusFirst();
    }

    bool _pad;
    System.Numerics.Vector2 _padAim;

    /// <summary>Verification: fire at the boss in her fight, else at the nearest mob.</summary>
    bool AutoFire(ref PlaneInput input)
    {
        if (!_autoFire) return false;
        var from = _world.Player.Position;
        System.Numerics.Vector2? target = _world.Boss is { Stage: BossStage.Fight } queen ? queen.Position
            : _world.Mobs.Where(m => m.Alive).OrderBy(m => System.Numerics.Vector2.Distance(m.Position, from)).Select(m => (System.Numerics.Vector2?)m.Position).FirstOrDefault()
              // With no creature to shoot, the nearest standing pot.
              ?? _world.Vases.Where(v => !v.Broken).OrderBy(v => System.Numerics.Vector2.Distance(v.Position, from)).Select(v => (System.Numerics.Vector2?)v.Position).FirstOrDefault();
        if (target is not { } at || System.Numerics.Vector2.DistanceSquared(at, from) < 1e-4f) return false;
        input.Aim = System.Numerics.Vector2.Normalize(at - from);
        // With Pearl Diver, let go once the pearl is fully charged.
        input.Fire = !(_world.Run.Loadout.Shot.Charge && _world.Player.Charge >= PlaneCombatTuning.ChargeSeconds);
        return true;
    }

    /// <summary>Mouse aim on the plane: from Clementine toward the point under the cursor on the swim band.</summary>
    System.Numerics.Vector2 MouseAim()
    {
        var cam = _camera.Camera;
        Vector2 mouse = GetViewport().GetMousePosition();
        Vector3 origin = cam.ProjectRayOrigin(mouse), dir = cam.ProjectRayNormal(mouse);
        if (Mathf.Abs(dir.Y) < 1e-4f) return default;
        float t = (LevelMap.SwimBand - origin.Y) / dir.Y;
        Vector3 hit = origin + dir * t;
        var aim = new System.Numerics.Vector2(hit.X, hit.Z) - _world.Player.Position;
        return aim.LengthSquared() > 1e-4f ? System.Numerics.Vector2.Normalize(aim) : default;
    }

    void Capture()
    {
        _frame++;
        if (_captureDir is null || _captureFrames.Count == 0 || _frame < _captureFrames.Peek()) return;
        int frame = _captureFrames.Dequeue();
        string path = $"{_captureDir}/td_{frame:0000}.png";
        GetViewport().GetTexture().GetImage().SavePng(path);
        var p = _world.Player.Position;
        var vp = GetViewport().GetViewportRid();
        GD.Print(string.Format(CultureInfo.InvariantCulture, "Captured {0}  pos {1:0.0},{2:0.0}  canopy {3}  {4:0} FPS  gpu {6:0.0} ms  {7}  sim {5:0.00} s", path, p.X, p.Y, _map.CanopyAt(p), Engine.GetFramesPerSecond(), _elapsed,
            RenderingServer.ViewportGetMeasuredRenderTimeGpu(vp), GetViewport().GetVisibleRect().Size));
        if (_captureFrames.Count == 0) GetTree().Quit();
    }
}
