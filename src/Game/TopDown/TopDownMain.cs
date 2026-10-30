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
/// Verification flags: --seed=, --cycle=, --depth=, --level=, --at=start|arch|arch-near|cave|exit|hole|kelp|shop|shop-out|cache|cache-near|secret|secret-in|treasure|treasure-out|clam-pearl|ambush|mob|boss|spring|spring-in,
/// --autopilot, --dive (dives whenever it can), --dbg-pad (on the controller from the start), --calm (no creatures), --fire, --pearls=, --hp=, --luck=, --boss-hp=, --paused, --map, --f3, --no-focus, --dbg-nofog (no floor fog),
/// --capture=dir --frames=a,b, --dbg-say=text (a message banner, a second in), --at=gate (swims into the boss arena through its
/// opened way in), --dbg-free=seconds (the boss freed that long after it lands), --dbg-steer=x,y (she swims that way;
/// --dbg-steer-fight: once the boss fights), --look=N (an art direction under study for the reef, docs/ART-LOOKS.md).
/// </summary>
public partial class TopDownMain : Node3D
{
    /// <summary>Every game starts on a random seed (--seed= pins one, for verification).</summary>
    SeedCode _seed = SeedCode.NewRandom();
    /// <summary>The level she is on: one hole leads down to the next (LevelPlan.NextOf).</summary>
    LevelId _id = LevelId.First;
    CombatView _combat = null!;
    CurrentView _currents = null!;
    KelpView _kelp = null!;
    AbyssView _abyss = null!;
    VaseView _vases = null!;
    /// <summary>Achievements: their data, what earns them on this run, and the banner that announces them (§8).</summary>
    AchievementCatalog? _achievementData;
    PlaneAchievements _achievements = null!;
    PearlBanner _pearlBanner = null!;
    AchievementBanner _achievementBanner = null!;
    string? _debugAward;
    string? _debugSay;
    /// <summary>Verification (`--dbg-steer=x,y`): she swims that way (map axes) the whole time (with `--dbg-steer-fight`, once the boss fights).</summary>
    Vector2? _dbgSteer;
    bool _dbgSteerFight;
    bool _dbgGate;
    /// <summary>How far round her the rock between the camera and her is carved away.</summary>
    const float SeeThroughRadius = 4.5f;
    /// <summary>How far the walls round her are carved away (0 whole → 1 open while they hide her).</summary>
    float _seeThrough;
    /// <summary>Verification (--dbg-nocarve): the walls are not carved away round her.</summary>
    readonly bool _dbgNoCarve = OS.GetCmdlineUserArgs().Contains("--dbg-nocarve");
    float _dbgFree;
    BossView _boss = null!;
    PufferlingView _puffers = null!;
    ReefFishView _reefFish = null!;
    CurseDenView _curseDen = null!;
    ClamRoomView _clamRooms = null!;
    SealView _seals = null!;
    SurgeView _surgeView = null!;
    GeyserView _geysers = null!;
    CurseDarkness _dark = null!;
    /// <summary>
    /// The level-up (DESIGN-TOPDOWN §2.6): once a skill point waits and she is out of the fight, the game stops (she
    /// still moves in place) while the camera closes in on her at the screen's left; the drawer slides in; a point
    /// chosen, it slides away and the camera eases back; any key plays on.
    /// </summary>
    /// <summary>How far the pause menu's close framing has come (0 the usual framing → 1 close on her).</summary>
    float _menuBlend;
    /// <summary>How close the level-up brings the camera (a share of its usual distance), and where she sits across the screen.</summary>
    const float MenuZoom = 0.33f, MenuAt = 0.3f, MenuIn = 0.5f, MenuOut = 0.35f;
    LowHealthVeil _lowHealth = null!;
    bool _inCurse;
    /// <summary>A boss level's Crack: 0 sealed under sand → 1 open (the sand drains over CrackOpenSeconds once it opens).</summary>
    float _crackOpen;
    const float CrackOpenSeconds = 3f;
    /// <summary>Verification: `--curse=nothing|fountain|pearl|teleport` sets what the curse den's end holds.</summary>
    CurseReward? _dbgCurse;
    /// <summary>Verification: `--dbg-goto=level` takes the debug menu's "Go to level" (this depth) a second into the run.</summary>
    int _dbgGoTo;
    /// <summary>The boss's anime cut-in, played once as its battle begins (the game held meanwhile).</summary>
    BossIntro _intro = null!;
    readonly BellView[] _prismBells = { new() { Part = 0 }, new() { Part = 1 } };
    bool _introShown;
    float? _bossHp;
    /// <summary>Verification: fire at the nearest mob; start the run with these pearls.</summary>
    /// <summary>Verification (the adventure): a blue tang, a clownfish and a school brought round her, full of ink, so all three murklings show.</summary>
    bool _dbgTurn;
    /// <summary>Verification (the adventure): the vent nearest her seals two seconds in.</summary>
    bool _dbgSealVent;
    /// <summary>Verification (the adventure): every Inkwarden is struck two seconds in (it rouses and fires its spikes).</summary>
    bool _dbgRouse;
    bool _autoFire, _autoActive, _dbgPop, _dbgSurge, _startPaused, _dbgBossLevel, _dbgGeyser, _dbgShells, _dbgMurkFish, _dbgThrow;
    /// <summary>Verification: `--boss=<Concept>` (or `queen`, or `drippus`) names the boss to meet; `--boss-level` starts on the depth's boss level.</summary>
    string? _dbgBoss;
    float? _startHp, _dbgLuck;
    /// <summary>Verification (`--xp=n`): experience she starts the run with.</summary>
    int _startXp;
    /// <summary>Verification (`--dbg-levelup`): the drawer's first choice made for her, a moment after it opens.</summary>
    readonly bool _dbgLevelUp = OS.GetCmdlineUserArgs().Contains("--dbg-levelup");
    float _spendIn;
    string[] _startPearls = Array.Empty<string>();
    HudView _hud = null!;
    ItemCatalog? _catalog;
    /// <summary>What Clementine carries through the rift (HP, pearls); a new seed or R starts a fresh run.</summary>
    PlaneRun? _run;
    Tuning _tuning = null!;
    LevelMap _map = null!;
    PlaneWorld _world = null!;

    LevelView _level = null!;
    BellView _bell = null!;
    /// <summary>Where her light reaches over the reef (light_field.gdshaderinc reads it): redone as she moves.</summary>
    readonly OctoShoots.Game.Fx.HorizonMap _lantern = new();
    PearlOrbitView _orbit = null!;
    CameraRig _camera = null!;
    DebugMapOverlay _debugMap = null!;
    MinimapView _minimap = null!;
    /// <summary>The way down, once open: an arrow at the screen's edge while the shaft is out of sight.</summary>
    readonly ExitPointer _exitPointer = new();
    TopoMap _fullMap = null!;
    readonly HashSet<int> _visited = new();
    readonly FogOfWar _fog = new();
    LineEdit _seedField = null!;
    Label _info = null!;
    double _accumulator;
    bool _dashLatched, _rLatched, _f3Latched, _escLatched, _backLatched;
    PauseMenu _pause = null!;
    /// <summary>When she was last told why the boss's wall will not open.</summary>
    float _sealToldAt = -10f;
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
    bool Paused => _pause.Visible || _debug.Visible || _menuBlend > 0f;
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
    // The corruption (docs/CORRUPTION.md): its shards and creatures, the live texture, and the shards being laid out.
    CorruptionView _corruption = null!;
    BlightView _blight = null!;
    AdventureView _adventure = null!;
    readonly OctoShoots.Game.Fx.WaterSwells _swells = new();
    SpringView _springs = null!;
    InkVapour _vapour = null!;
    /// <summary>Her health last frame: a rise is corruption cleared, which steams off her.</summary>
    float _lastHp = -1f;
    CleanseMeter _cleanse = null!;
    /// <summary>The HUD's root: its parts laid out on a 1080-line screen, scaled to the window.</summary>
    Control _hudRoot = null!;
    /// <summary>
    /// The HUD's scale: with the window's height against 1080 lines, but gently (its 0.6 power), so a small window keeps
    /// it readable and a large one does not blow it up (0.9 at 900 lines, 1.5 at 2160).
    /// </summary>
    float HudScale => Mathf.Clamp(Mathf.Pow(GetViewport().GetVisibleRect().Size.Y / 1080f, 0.6f), 0.7f, 2f);

    /// <summary>
    /// The way down, once it is open, pointed out from the screen's edge while the shaft is out of sight (not while the
    /// game is held: the pause menu, the boss's intro, a dive, between levels).
    /// </summary>
    void SyncExitPointer(float dt)
    {
        _exitPointer.Size = _hudRoot.Size;
        bool open = _world.ExitOpen && _transition == Transition.None && !_deathSplash && !_diving && !Paused && !_intro.Playing;
        var cam = _camera.Camera;
        var view = GetViewport().GetVisibleRect().Size;
        Vector2 Shown(Vector3 at) => OctoShoots.Game.Fx.Fisheye.Shown(cam.UnprojectPosition(at), view) / HudScale;
        var shaft = _map.Shaft.Center;
        var her = _bell.GlobalPosition;
        Vector2? target = open ? Shown(new Vector3(shaft.X, LevelMap.SwimBand, shaft.Y)) : null;
        _exitPointer.Step(dt, target, Shown(new Vector3(her.X, LevelMap.SwimBand, her.Z)));
    }

    /// <summary>Lays the HUD out for the window's size this frame.</summary>
    void FitHud()
    {
        float s = HudScale;
        _hudRoot.Scale = new Vector2(s, s);
        _hudRoot.Size = GetViewport().GetVisibleRect().Size / s;
    }
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
    /// <summary>Verification: bud a murkling ("murk") or a prime ("prime") on the densest ink near her at the start.</summary>
    string? _dbgBud;
    /// <summary>Verification (--dbg-exit): the way down is open from the start.</summary>
    bool _dbgExit;
    /// <summary>Verification: murklings bud round an Inkwarden (the first, or a turned pufferfish set 6 m ahead of her), so its tethers show.</summary>
    bool _dbgWard;
    /// <summary>Verification: the first seal starts with this many hits less one, and takes the last a second in (0: off).</summary>
    int _dbgSeal;
    bool _autopilot, _autoDive, _calm;
    List<System.Numerics.Vector2> _route = new();
    int _routeProgress;
    string? _captureDir;
    readonly Queue<int> _captureFrames = new();
    int _frame;

    public override void _Ready()
    {
        // The bundled fonts: Nunito is every screen's default (THEME-BIBLE §6.8).
        TitleStyle.InstallDefaultFont();
        // Her colour (data/clementine_color.cfg), for every shader and view that wears it.
        OctoShoots.Game.Fx.HerColor.Load();
        ParseArgs();
        TakeLaunch();
        if (_dbgBossLevel) _id = _id with { Level = LevelPlan.BossLevel(_seed, _id.Cycle, _id.Depth) };
        InputSetup.Register();
        AddChild(new PadMenus());
        _tuning = SettingsStore.LoadTuning();
        _catalog = LoadCatalog();
        _achievementData = LoadAchievements(_catalog);
        // Keep the GPU cool: the frame-rate cap from the view options (60 by default; captures run uncapped).
        var view = _view = SettingsStore.LoadView();
        ViewOptions.Shadows = view.LightShadows && System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--dbg-noshadow") < 0;
        _perf = OS.GetCmdlineUserArgs().Contains("--dbg-perf");
        Engine.MaxFps = _captureDir is null && !_perf ? view.MaxFps : 0;
        if (_captureDir is not null || _perf) RenderingServer.ViewportSetMeasureRenderTime(GetViewport().GetViewportRid(), true);
        BuildEnvironment();
        _level = new LevelView();
        AddChild(_level);
        _orbit = new PearlOrbitView();
        AddChild(_orbit);
        _bell = new BellView();
        AddChild(_bell);
        // Prism Pearl: the red and the blue beside her while her light is split.
        foreach (var part in _prismBells) AddChild(part);
        RenderingServer.GlobalShaderParameterSet(OctoShoots.Game.Fx.SN.Of("her_horizon"), _lantern.Texture);
        _combat = new CombatView();
        AddChild(_combat);
        _currents = new CurrentView();
        AddChild(_currents);
        _kelp = new KelpView();
        AddChild(_kelp);
        _abyss = new AbyssView();
        AddChild(_abyss);
        _vases = new VaseView();
        AddChild(_vases);
        _boss = new BossView();
        AddChild(_boss);
        _puffers = new PufferlingView();
        AddChild(_puffers);
        _reefFish = new ReefFishView { Visible = !OS.GetCmdlineUserArgs().Contains("--dbg-nofish") };
        AddChild(_reefFish);
        _curseDen = new CurseDenView();
        AddChild(_curseDen);
        _clamRooms = new ClamRoomView();
        AddChild(_clamRooms);
        _seals = new SealView();
        AddChild(_seals);
        _surgeView = new SurgeView();
        AddChild(_surgeView);
        _geysers = new GeyserView();
        AddChild(_geysers);
        _corruption = new CorruptionView();
        AddChild(_corruption);
        _blight = new BlightView { Swells = _swells };
        AddChild(_blight);
        _adventure = new AdventureView();
        AddChild(_adventure);
        _springs = new SpringView();
        AddChild(_springs);
        _vapour = new InkVapour();
        AddChild(_vapour);
        _blight.Vapour = _vapour;
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
            else if (arg == "--dbg-pop") _dbgPop = true;
            else if (arg == "--dbg-geyser") _dbgGeyser = true;
            else if (arg == "--dbg-shells") _dbgShells = true;
            else if (arg == "--dbg-murkfish") _dbgMurkFish = true;
            else if (arg == "--dbg-turn") _dbgTurn = true;
            else if (arg == "--dbg-sealvent") _dbgSealVent = true;
            else if (arg == "--dbg-rouse") _dbgRouse = true;
            else if (arg == "--dbg-throw") _dbgThrow = true;
            else if (arg.StartsWith("--dbg-seal=") && int.TryParse(Value("--dbg-seal="), out int sealHits)) _dbgSeal = sealHits;
            else if (arg.StartsWith("--dbg-bud=")) _dbgBud = Value("--dbg-bud=");
            else if (arg == "--dbg-ward") _dbgWard = true;
            else if (arg == "--dbg-exit") _dbgExit = true;
            else if (arg.StartsWith("--dbg-moment=")) _dbgMoment = float.Parse(Value("--dbg-moment="), CultureInfo.InvariantCulture);
            else if (arg.StartsWith("--boss=")) _dbgBoss = Value("--boss=");
            else if (arg == "--boss-level") _dbgBossLevel = true;
            else if (arg.StartsWith("--dbg-goto=") && int.TryParse(Value("--dbg-goto="), out int gotoLevel)) _dbgGoTo = gotoLevel;
            else if (arg.StartsWith("--curse=") && Enum.TryParse<CurseReward>(Value("--curse="), true, out var reward)) _dbgCurse = reward;
            else if (arg.StartsWith("--award=")) _debugAward = Value("--award=");
            else if (arg.StartsWith("--dbg-say=")) _debugSay = Value("--dbg-say=");
            else if (arg.StartsWith("--dbg-free=")) _dbgFree = float.Parse(Value("--dbg-free="), System.Globalization.CultureInfo.InvariantCulture);
            else if (arg.StartsWith("--dbg-steer=") && Value("--dbg-steer=").Split(',') is { Length: 2 } steer
                && float.TryParse(steer[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float sx) && float.TryParse(steer[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float sy))
                _dbgSteer = new Vector2(sx, sy).Normalized();
            else if (arg == "--dbg-steer-fight") _dbgSteerFight = true;
            // Verification: an art direction under study for the reef (docs/ART-LOOKS.md: 0 the game's own; 1–5).
            else if (arg.StartsWith("--look=") && int.TryParse(Value("--look="), out int look))
                RenderingServer.GlobalShaderParameterSet(OctoShoots.Game.Fx.SN.Of("reef_art"), look);
            else if (arg == "--reset-profile")
            {
                // Verification: the saved profile's achievements (and so its unlocked pearls) are cleared.
                GameSave.Current.Profile.Achievements.Clear();
                GameSave.Write();
            }
            else if (arg.StartsWith("--dbg-slowmo=") && float.TryParse(Value("--dbg-slowmo="), NumberStyles.Float, CultureInfo.InvariantCulture, out float slow))
                Engine.TimeScale = Math.Clamp(slow, 0.02f, 1f);
            else if (arg == "--paused") _startPaused = true;
            // Verification: as on a controller from the start (its key hints).
            else if (arg == "--dbg-pad") InputSetup.UsingPad = _pad = true;
            else if (arg.StartsWith("--xp=") && int.TryParse(Value("--xp="), out int xp)) _startXp = xp;
            else if (arg.StartsWith("--hp=") && float.TryParse(Value("--hp="), NumberStyles.Float, CultureInfo.InvariantCulture, out float hp)) _startHp = hp;
            else if (arg.StartsWith("--luck=") && float.TryParse(Value("--luck="), NumberStyles.Float, CultureInfo.InvariantCulture, out float luck)) _dbgLuck = luck;
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
        _id = RunStart();
    }

    /// <summary>
    /// Where a new run starts: in the tutorial (Depth 0) until she has passed it once, then in Depth 1 — unless the
    /// settings ask to replay the tutorial.
    /// </summary>
    LevelId RunStart() =>
        _recorder.Profile.TutorialPassed && !(_view ?? SettingsStore.LoadView()).ReplayTutorial ? LevelId.FirstRun : LevelId.First;

    /// <summary>Verification: the options with the boss `--boss=` names (null: the game's own).</summary>
    PlaneOptions? DebugBoss()
    {
        if (_dbgBoss is null || !_map.HasBoss) return null;
        int depth = Math.Max(1, _map.Depth - LevelPlan.Tutorial);
        var spec = _dbgBoss == "queen" ? OctoShoots.Core.Bosses.BossGenerator.QueenClam(_map.Seed, _map.Cycle, depth)
            : _dbgBoss is "drippus" || _dbgBoss.Equals(nameof(OctoShoots.Core.Bosses.BossConcept.TheBrood), StringComparison.OrdinalIgnoreCase)
                ? OctoShoots.Core.Bosses.BossGenerator.DrippusPrime(_map.Seed, _map.Cycle, depth)
            : Enum.TryParse<OctoShoots.Core.Bosses.BossConcept>(_dbgBoss, true, out var concept)
                ? OctoShoots.Core.Bosses.BossGenerator.ForConcept(_map.Seed, _map.Cycle, depth, concept)
                : null;
        if (spec is null) return null;
        var d = PlaneOptions.Default;
        GD.Print($"Debug boss: {spec.Name} ({spec.Tier} {spec.Form}, {spec.Concept}, {spec.Theme}, {spec.Exposure}, {spec.Signature})");
        return new PlaneOptions { Corruption = d.Corruption, Pufferlings = d.Pufferlings, Vases = d.Vases, Adventure = d.Adventure, Boss = spec };
    }

    /// <summary>Saves the run as it stands at the start of this level (continuing puts her back here).</summary>
    void SaveRun()
    {
        if (!_persist || _debugRun || _run is null) return;
        GameSave.Current.Run = RunRecord();
        GameSave.Write();
    }

    /// <summary>The run as it stands at the start of this level.</summary>
    SuspendedRun RunRecord() =>
        new()
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
            Xp = _run.Xp,
            XpLevel = _run.XpLevel,
            Points = _run.Points,
            Skills = _run.Skills.ToDictionary(k => k.Key.ToString(), k => k.Value),
            Luck = _map.Luck,
            Elapsed = _elapsed,
            Foes = _runMobs,
            ShellsCollected = _runShells,
            SavedAt = DateTime.Now.ToString("s", CultureInfo.InvariantCulture),
        };

    Task<LevelShape>? _firstMap;

    void StartFirstLevel()
    {
        var stats = new List<(string, string)> { ("Seed", _seed.ToString() + (_customSeed ? "  (seeded)" : "")) };
        if (_resume is { } saved) stats.Add(("Carrying", $"{saved.Items.Count} pearls · {saved.Shells} shells · {CorruptionText(saved.Hp, saved.Items)}"));
        _splash.Fill(_resume is not null ? "Back into the sea" : "A new dive", stats, _id.ToString());
        _splash.Modulate = Colors.White;
        _splash.Visible = true;
        _firstMap = Shape(_id);
    }

    /// <summary>
    /// A level made and readied for showing, off the main thread (both steps are pure work on data). The level below is
    /// made in the <paramref name="background"/>, on one thread, so the game keeps its frame rate.
    /// </summary>
    /// <summary>The maps (the minimap and the whole level's on Tab / Select) show: always, but in the adventure only once she has absorbed Sea Chart Nautilus.</summary>
    bool HasMap => !_world.Options.Adventure || _world.Run.Loadout.Flags.Contains("minimap");

    /// <summary>The adventure's levels are abyss reefs (AbyssReef): no seabed outside the caves and tunnels.</summary>
    static LevelMap Abyss(LevelMap map) => PlaneOptions.Default.Adventure ? AbyssReef.Apply(map) : map;

    Task<LevelShape> Shape(LevelId id, bool background = false)
    {
        var streams = new RunStreams(_seed);
        float luck = Luck;
        return Task.Run(() => LevelShape.Prepare(Abyss(TopDownGenerator.Generate(streams, id, background: background, luck: luck))));
    }

    /// <summary>
    /// Her luck as levels are made (--luck= overrides it, for verification). A resumed run's level is made again with the
    /// luck it was made with the first time (else it would not be the same level).
    /// </summary>
    float Luck => _dbgLuck ?? _run?.Loadout.Stats.Luck ?? _resume?.Luck ?? 0f;

    void GoToTitle()
    {
        _haptics.Stop();
        Input.MouseMode = Input.MouseModeEnum.Visible;
        if (_persist)
        {
            SaveMoment();
            GameSave.Write();
        }
        GetTree().ChangeSceneToFile(RunLaunch.TitleScene);
    }

    /// <summary>Closing the game mid-level saves the moment too.</summary>
    public override void _Notification(int what)
    {
        if (what != NotificationWMCloseRequest || !_persist) return;
        SaveMoment();
        GameSave.Write();
    }

    /// <summary>The objects a level made again from its seed shares with this one (Snapshot), found as it was made.</summary>
    OctoShoots.Core.Saves.Snapshot.Anchors? _anchors;

    /// <summary>
    /// Saves the level as it stands this very moment, so continuing resumes it exactly (the save's level-start record
    /// stays, to fall back on). Not while she is dying, diving or between levels: then the level's start is kept.
    /// </summary>
    void SaveMoment()
    {
        if (!_persist || _debugRun || GameSave.Current.Run is not { } saved) return;
        if (saved.Cycle != _id.Cycle || saved.Depth != _id.Depth || saved.Level != _id.Level) return;
        saved.State = Moment();
        if (saved.State is null) return;
        saved.Elapsed = _elapsed;
        saved.Foes = _runMobs;
        saved.ShellsCollected = _runShells;
        saved.SavedAt = DateTime.Now.ToString("s", CultureInfo.InvariantCulture);
    }

    /// <summary>This very moment of the level (null while she is dying, diving or between levels, or if it cannot be saved).</summary>
    LevelState? Moment()
    {
        if (_run is null || _anchors is null) return null;
        if (_transition != Transition.None || _deathSplash || _diving || _world.Player.Hp <= 0f) return null;
        try
        {
            return new LevelState
            {
                World = Convert.ToBase64String(_world.SaveState(_anchors)),
                Fog = _fog.Save(),
                Visited = _visited.ToList(),
                LevelTime = _levelTime,
                Items = _run.Items.ToList(),
                Hp = _world.Player.Hp,
                Shells = _run.Shells,
            };
        }
        catch (Exception e)
        {
            GD.PushWarning($"The moment could not be saved ({e.Message}); continuing will start the level again.");
            return null;
        }
    }

    /// <summary>
    /// Verification (--dbg-moment=seconds): that long into the first level, its moment is saved (in memory only) and the
    /// level is made again from its seed and put back at it, as quitting and continuing would.
    /// </summary>
    float _dbgMoment;
    SuspendedRun? _dbgMomentStart;

    void DebugMoment()
    {
        if (_dbgMoment <= 0f || _levelTime < _dbgMoment || _dbgMomentStart is not { } start || Moment() is not { } moment) return;
        _dbgMoment = 0f;
        start.State = moment;
        start.Elapsed = _elapsed;
        GD.Print($"Moment saved: tick {_world.Tick}, her at {_world.Player.Position}, HP {_world.Player.Hp:F1}, {_world.Murklings.Count} murklings, {_world.Shots.Count} shots, {moment.World.Length / 1024} KB");
        _resume = start;
        _run = null;
        Regenerate(keepRun: false);
        GD.Print($"Moment restored: tick {_world.Tick}, her at {_world.Player.Position}, HP {_world.Player.Hp:F1}, {_world.Murklings.Count} murklings, {_world.Shots.Count} shots");
    }

    /// <summary>Puts the level back at the moment it was left; false (and the level as it starts) if that cannot be done.</summary>
    bool RestoreMoment(LevelState moment)
    {
        try
        {
            _world.LoadState(_anchors!, Convert.FromBase64String(moment.World));
            // Synergies she had by then are not news either.
            foreach (string id in _run!.Loadout.Synergies) _announced.Add(id);
            return true;
        }
        catch (Exception e)
        {
            GD.PushWarning($"The moment she left could not be restored ({e.Message}); back at the level's start.");
            return false;
        }
    }

    WorldEnvironment _environment = null!;
    MarineSnow _snow = null!;
    SunLight _sunLight = null!;

    /// <summary>Debug (F1, --dbg-nofog): the floor's fog — the depth focus pass's murk and blur, and the water's fog.</summary>
    bool _floorFog = !OS.GetCmdlineUserArgs().Contains("--dbg-nofog");

    void SetFloorFog(bool on)
    {
        _floorFog = on;
        _camera.SetFloorFog(on);
        _environment.Environment.FogEnabled = on;
    }

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
        SetFloorFog(_floorFog);
        _sunLight.Configure(look.Sun, look.SunEnergy);
        _bell.Lantern = look.Lantern;
        BlightView.Lantern = look.Lantern;
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
        // The view bent slightly toward its edges, over the whole 3D picture, under everything drawn on the screen.
        AddChild(new OctoShoots.Game.Fx.Fisheye());
        // The curse den's dark: over the reef, under the HUD.
        var darkLayer = new CanvasLayer { Layer = 1 };
        AddChild(darkLayer);
        _dark = new CurseDarkness();
        darkLayer.AddChild(_dark);
        // Her low health: a violet veil closing in, over the reef (and the curse's dark), under the HUD.
        _lowHealth = new LowHealthVeil { ReducedMotion = _view.ReducedMotion };
        darkLayer.AddChild(_lowHealth);
        var layer = new CanvasLayer { Layer = 2 };
        AddChild(layer);
        _debugMap = new DebugMapOverlay { Visible = OS.GetCmdlineUserArgs().Contains("--f3") };
        layer.AddChild(_debugMap);
        // The controller's crosshair: her own, the way she aims (SyncCrosshair). Under the HUD.
        _padCross = new TextureRect
        {
            Texture = OctoShoots.Game.Fx.GonadCursor.Texture(),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
        };
        layer.AddChild(_padCross);
        _hud = new HudView();
        // The HUD is laid out on a 1080-line screen and scaled with the window (HudScale), so it keeps its share of it.
        _hudRoot = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        layer.AddChild(_hudRoot);
        // (Under the rest of the HUD.)
        _hudRoot.AddChild(_exitPointer);
        _hudRoot.AddChild(_hud);
        _minimap = new MinimapView();
        _hudRoot.AddChild(_minimap);
        _cleanse = new CleanseMeter();
        _hudRoot.AddChild(_cleanse);
        // Tab: the whole level on one sheet, drawn by the minimap's rules (wall edges, fog, places only once spotted).
        _fullMap = new TopoMap
        {
            Visible = OS.GetCmdlineUserArgs().Contains("--map"), Cutout = true, EmblemScale = 1.5f, MetresAcross = LevelMap.Size + 4f,
            ShowDetail = false, EdgesOnly = true, ArrowScale = 1.3f, ArrowColor = new Color(1f, 0.72f, 0.35f),
        };
        layer.AddChild(_fullMap);
        // Her arrow on both maps follows her colour.
        void Arrows()
        {
            var arrow = OctoShoots.Game.Fx.HerColor.Light.Lerp(OctoShoots.Game.Fx.HerColor.Body, 0.35f);
            _minimap.ArrowColor = arrow;
            _fullMap.ArrowColor = arrow;
            _exitPointer.Tint = OctoShoots.Game.Fx.HerColor.Light;
        }
        Arrows();
        OctoShoots.Game.Fx.HerColor.Changed += Arrows;
        TreeExiting += () => OctoShoots.Game.Fx.HerColor.Changed -= Arrows;
        // Achievement banners: above the HUD, below the pause menu.
        var bannerLayer = new CanvasLayer { Layer = 3 };
        AddChild(bannerLayer);
        // The boss intro: over everything but the pointer and the pause menu.
        _intro = new BossIntro();
        bannerLayer.AddChild(_intro);
        _intro.Cue += cue =>
        {
            switch (cue)
            {
                case "wipe": _sfx.PlayPitched("spout", 1.4f, -8f); break;
                case "slam":
                    _sfx.PlayPitched("snap", 0.6f, -2f);
                    if (_view.CameraShake && !_view.ReducedMotion) _haptics.Pulse(0.7f, 0.5f, 0.25f);
                    break;
                case "vs": _sfx.PlayPitched("bell_ring", 0.8f, -4f); break;
                case "pop": _sfx.PlayPitched("pop", 1.1f, -4f); break;
            }
        };
        _achievementBanner = new AchievementBanner { ReducedMotion = _view.ReducedMotion };
        bannerLayer.AddChild(_achievementBanner);
        // The pearl she is absorbing, as the pause menu lists it.
        _pearlBanner = new PearlBanner();
        bannerLayer.AddChild(_pearlBanner);
        // Its sounds come from the game's Sfx (made in _Ready, before or after this).
        if (_sfx is not null) _achievementBanner.Sound = _sfx;
        // ESC: pause, restart the run, see the pearls she has absorbed. Drawn above the rest of the HUD.
        var pauseLayer = new CanvasLayer { Layer = 5 };
        AddChild(pauseLayer);
        _pause = new PauseMenu { Zoom = _view.CameraZoom };
        pauseLayer.AddChild(_pause);
        _pause.ResumePressed += Resume;
        _pause.SkillChosen += ChooseSkill;
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
            _id = RunStart();
            Regenerate();
            Say("New run");
        };
        _debug = new DebugMenu();
        pauseLayer.AddChild(_debug);
        _debug.Closed += CloseDebug;
        _debug.GoTo += DebugGoTo;
        _debug.FloorFogToggled += SetFloorFog;
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
        // The level is saved as it stands (SaveMoment): she resumes right there.
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
            _id = RunStart();
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
            if (!parsed.Equals(_seed)) _id = RunStart();
            _seed = parsed;
        }
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var shape = LevelShape.Prepare(Abyss(TopDownGenerator.Generate(new RunStreams(_seed), _id, luck: Luck)));
        ShowLevel(shape, keepRun, watch.ElapsedMilliseconds);
    }

    /// <summary>
    /// A level shown afresh (a new run, a resumed one, a regenerated one): the world back at the origin, no level below
    /// until it is made.
    /// </summary>
    void ShowLevel(LevelShape shape, bool keepRun, long ms)
    {
        ResetMenu();
        _intro.Stop();
        _introShown = false;
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
        if (_dbgShells)
        {
            // Verification: a few shells round her, 2–3 m out, so the pickup's flight to the counter shows.
            var her = _world.Player.Position;
            for (int i = 0; i < 4; i++)
            {
                float a = i * 1.7f;
                _world.Shells.Add(new PlaneShell { Position = her + new System.Numerics.Vector2(MathF.Cos(a), MathF.Sin(a)) * (2f + 0.3f * i) });
            }
            _dbgShells = false;
        }
        if (_dbgTurn)
        {
            var her = _world.Player.Position;
            var spots = new[] { new System.Numerics.Vector2(-5f, -3f), new System.Numerics.Vector2(5f, -3f), new System.Numerics.Vector2(0f, 5f) };
            var kinds = new[] { ReefFishKind.BlueTang, ReefFishKind.Clownfish, ReefFishKind.Chromis };
            for (int k = 0; k < kinds.Length; k++)
            {
                var spot = her + spots[k];
                if (!_world.Clear(spot, 0.6f)) spot = her + spots[k] * 0.5f;
                var fish = _world.ReefFish.Where(f => f.Kind == kinds[k] && !f.Corrupted).OrderBy(f => System.Numerics.Vector2.Distance(f.Position, her)).FirstOrDefault();
                if (fish is null) continue;
                var school = _world.ReefFish.Where(f => kinds[k] == ReefFishKind.Chromis && f.Kind == ReefFishKind.Chromis && f.Host == fish.Host).ToList();
                if (school.Count == 0) school.Add(fish);
                foreach (var f in school)
                {
                    f.Position = spot + (f.Position - fish.Position) * 0.3f;
                    if (!_world.Clear(f.Position, 0.3f)) f.Position = spot;
                }
                fish.Ink = 1f;
            }
            // ...and a pufferfish, north of her, to be the spiky Inkwarden.
            var puffer = _world.Fish.Where(f => !f.Freed).OrderBy(f => System.Numerics.Vector2.Distance(f.Position, her)).FirstOrDefault();
            if (puffer is not null)
            {
                var at = her + new System.Numerics.Vector2(0f, -4.5f);
                puffer.Position = _world.Clear(at, 0.8f) ? at : her + new System.Numerics.Vector2(0f, -2.5f);
                puffer.Ink = 1f;
            }
            _dbgTurn = false;
        }
        if (_dbgMurkFish && _world.Schools.Count > 0)
        {
            // Verification: half the first school starts turning (a swarm of lesser murklings in a moment).
            int k = 0;
            foreach (var f in _world.ReefFish.Where(f => f.Kind == ReefFishKind.Chromis && f.Host == 0))
                if (k++ % 2 == 0) f.Turning = MurkFishTuning.TurnSeconds;
            _dbgMurkFish = false;
        }
        if (_dbgBud is not null && _world.Corruption is { } ink)
        {
            // A spot of dense ink with room round it, and her 7 m off it in sight, so its gathering shows.
            bool prime = _dbgBud == "prime";
            for (int c = 0; c < ink.Density.Length; c += 7)
            {
                var at = CorruptionField.CellCentre(c % CorruptionField.N, c / CorruptionField.N);
                if (ink.Density[c] < 0.95f || !_world.Clear(at, prime ? 2.5f : 1f) || ink.At(at + new System.Numerics.Vector2(1.5f, 0f)) < 0.9f) continue;
                var her = at + new System.Numerics.Vector2(0f, 7f);
                if (!_world.Clear(her, 0.8f) || !_world.LineOfSight(at, her)) continue;
                _world.Player.Position = _world.Player.PrevPosition = her;
                _camera.Track(Focus(1f), 0f, snap: true);
                _world.DebugBud(at, prime);
                // Its bud's event now (the next step clears the events), so its gathering shows.
                foreach (var e in _world.Events)
                    if (e.Type == PlaneEventType.MurklingBud) _blight.OnEvent(e);
                break;
            }
            _dbgBud = null;
        }
        if (_dbgWard && _world.Corruption is not null)
        {
            if (_world.Inkwardens.Count == 0)
                _world.Inkwardens.Add(new Inkwarden { Position = _world.Player.Position + new System.Numerics.Vector2(0f, -6f), Puffer = _world.Options.Adventure, Seed = 1f });
            var warden = _world.Inkwardens[0];
            for (int k = 0; k < 8; k++)
            {
                float a = Mathf.Tau * k / 8f;
                var at = warden.Position + new System.Numerics.Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (3f + k % 3 * 1.5f);
                if (_world.Clear(at, 1f)) _world.DebugBud(at, prime: false);
            }
            _dbgWard = false;
        }
        if (_dbgExit) _world.ExitOpen = true;
        if (_dbgSeal > 0 && _world.Seals.Count > 0) _world.Seals[0].Hits = Math.Max(0, _dbgSeal - 1);
        if (_dbgThrow)
        {
            // Verification: a murkling 5 m off her (up the screen first), its throw due at once (the slingshot telegraph from the start).
            var her = _world.Player.Position;
            for (int i = 0; i < 16; i++)
            {
                var at = her + new System.Numerics.Vector2(Mathf.Cos(i * 0.4f - 1.9f), Mathf.Sin(i * 0.4f - 1.9f)) * 5f;
                if (!_world.Clear(at, 0.6f) || !_world.LineOfSight(at, her)) continue;
                _world.Murklings.Add(new Murkling { Position = at, Bud = 0f, ThrowTimer = 0f });
                break;
            }
            _dbgThrow = false;
        }
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
        // Continuing a run left mid-level: the moment it was left (restored once the level is made again).
        var moment = keepRun ? null : _resume?.State;
        ApplyLook();
        // Where the sun reaches into this level's water (its god rays and caustics end at the rock that shades them).
        OctoShoots.Game.Fx.SunMap.Build(map, OctoShoots.Game.Fx.SunLight.Direction);
        if (!keepRun || _run is null)
        {
            ResetRunTotals();
            _announced.Clear();
            _run = new PlaneRun(_catalog, _tuning);
            // Her earned achievements are the run's unlocks (the profile's own set: one earned mid-run applies at once).
            _run.Unlocked = _recorder.Profile.Achievements;
            _achievements = new PlaneAchievements(_recorder.Profile, _customSeed);
            // Every run starts with these (a resumed one brings back what it carried).
            if (_resume is null)
                foreach (string id in PlaneRun.StartingPearls)
                    if (_catalog?.Contains(id) == true) _run.Add(id);
            foreach (string id in _startPearls)
                if (_catalog?.Contains(id) == true) _run.Add(id);
            if (_startHp is { } hp) _run.Hp = hp;
            _startHp = null;
            if (_startXp > 0) _run.Gain(_startXp);
            _startXp = 0;
            bool continuing = _resume is not null;
            if (_resume is { } saved)
            {
                // Back where the save left her: what she carried onto this level, and the run's totals so far.
                foreach (string id in saved.Items)
                    if (_catalog?.Contains(id) == true) _run.Add(id);
                _run.Xp = saved.Xp;
                _run.XpLevel = Math.Max(saved.XpLevel, 1);
                _run.Points = saved.Points;
                foreach (var (name, n) in saved.Skills)
                    if (Enum.TryParse<OctoShoots.Core.Items.Stat>(name, out var stat) && n > 0) _run.Skills[stat] = n;
                _run.Refresh();
                _run.Hp = Mathf.Min(saved.Hp, _run.MaxHp);
                _run.Shells = saved.Shells;
                _run.ActiveCharge = saved.ActiveCharge;
                _elapsed = _runTime = (float)saved.Elapsed;
                _runMobs = saved.Foes;
                _runShells = saved.ShellsCollected;
                _resume = null;
            }
            // The tutorial is not a run of its own: the run is counted when she dives out of it (TutorialPassed).
            _recorder.StartRun(_seed.ToString(), _customSeed, _run.Items, continuing || _id.IsTutorial);
        }
        _run.Level = _id;
        _world = new PlaneWorld(_map, _tuning, _run, DebugBoss());
        // A flounder's patch is open sand: no coral head of the view's scatter on it.
        _level.KeepClear(_world.Flounders.Select(f => f.Home), 2.2f);
        _anchors = _world.SaveAnchors(_recorder.Profile, _achievements, _recorder);
        // Synergies she came in with (a resumed run, debug pearls) are not news.
        foreach (string id in _run.Loadout.Synergies) _announced.Add(id);
        if (arriveAt is { } at) _world.Player.Position = _world.Player.PrevPosition = at;
        if (_calm)
        {
            _world.Mobs.Clear();
            _world.Ambushes.Clear();
        }
        if (_dbgSurge) InjectSurge();
        // Verification: the level's first geyser sprays a second from now (8 s).
        if (_dbgGeyser && _world.Director.Geysers.Count > 0)
            _world.Director.Inject(new ReefEvent { Kind = ReefEventKind.GeyserSpray, Start = _world.Time + 1f, Duration = GeyserTuning.Warn + 8f, Geyser = 0 });
        if (_dbgCurse is { } forced && _world.Curse is { } den && den.Reward != forced)
        {
            _world.Pearls.RemoveAll(q => q.Position == den.End);
            den.Reward = forced;
            if (forced == CurseReward.Pearl) _world.Pearls.Add(new PlanePearl { ItemId = PlaneRun.PortedPearls[0], Position = den.End, Placed = true });
        }
        _achievements.EnterLevel();
        // A pearl unlocked by an achievement, offered here for the first time, wears a NEW chip.
        var fresh = new HashSet<string>();
        if (_catalog is not null)
            foreach (string pid in _world.Pearls.Select(q => q.ItemId).Concat(_world.Stands.Where(st => st.Kind == StandKind.Pearl).Select(st => st.ItemId)))
                if (_catalog.TryGet(pid, out var offered) && offered.Unlock is not null && !_recorder.Profile.SeenItems.Contains(pid)) fresh.Add(pid);
        _recorder.EnterLevel(_world, _id);
        // Back at the moment she left (the save keeps it, should she leave again before saving anew); else the level's
        // start is saved.
        if (moment is not null && !RestoreMoment(moment)) moment = null;
        if (moment is null) SaveRun();
        if (_dbgMoment > 0f && _dbgMomentStart is null) _dbgMomentStart = RunRecord();
        _combat.Show(_world, _catalog, fresh);
        _currents.Clear();
        _swells.Clear();
        _kelp.Build(map, ReefLook.For(map.Id.Depth));
        _abyss.Build(map, ReefLook.For(map.Id.Depth));
        _vases.Show(_world);
        _puffers.Show(_world);
        _reefFish.Show(_world);
        _curseDen.Show(_world);
        // Verification: `--clam-pearl=<id>` lays that pearl in the level's clam rooms.
        if (OS.GetCmdlineUserArgs().FirstOrDefault(x => x.StartsWith("--clam-pearl=")) is { } clamPearl)
            foreach (var pearl in _world.Pearls)
                if (_map.Pois.Exists(q => q.Kind == PoiKind.TreasureCave && q.Position == pearl.Position)) pearl.ItemId = clamPearl["--clam-pearl=".Length..];
        _clamRooms.Show(_world);
        _seals.Show(_world, _level);
        _geysers.Show(_world);
        _crackOpen = _world.ExitOpen ? 1f : 0f;
        _level.SetCrackOpen(_crackOpen);
        _dark.Clear();
        _inCurse = false;
        _combat.Visible = _boss.Visible = _puffers.Visible = true;
        _damage.Clear();
        _springHealed = 0f;
        _camera.SetBubbleLights(_combat.Lights, 0);
        _camera.SetLenses(_combat.Lenses, 0);
        SetupCorruption(map);
        _boss.Show(_world);
        if (_bossHp is { } bossHp && _world.Boss is { } queen) queen.Hp = bossHp;
        _debugMap.SetMap(_map);
        _minimap.SetMap(_map, _world.SealedCaves);
        _fullMap.SetMap(_map, _world.SealedCaves);
        // Places are unknown ("?") until visited; she starts at the start.
        _visited.Clear();
        _visited.Add(_map.Pois.IndexOf(_map.Start));
        _minimap.SetVisited(_visited);
        _fog.Reset();
        if (moment is not null)
        {
            foreach (int place in moment.Visited) _visited.Add(place);
            _minimap.SetVisited(_visited);
            _fog.Load(moment.Fog);
            _levelTime = (float)moment.LevelTime;
        }
        _minimap.SetFog(_fog);
        _fullMap.Fog = _fog;
        _fullMap.Seen = _fog;
        _fullMap.Visited = _visited;
        _seedField.Text = _seed.ToString();
        // The level's title, for a moment as she arrives: what the level is about (its template's name, DESIGN-TOPDOWN
        // §4.1; the default template has none), else the depth's name on its first level, else the level; where it is
        // over it (a new depth's first level names the depth too).
        string where = _id.Level == 1 ? $"{ReefLook.For(_id.Depth).Name} · {_id}" : _id.ToString();
        string title = _map.Template is { Name.Length: > 0 } template ? template.Name : _id.Level == 1 ? ReefLook.For(_id.Depth).Name : $"Level {_id.Level}";
        _hud.ShowTitle(title, where);
        // Heard through deeper water: more muffled.
        _sfx.SetDepth(_id.ShownDepth);
        // The Tab map's title: the level's template (else what the arrival card calls it), and where it is.
        _fullMap.Title = title;
        _fullMap.Subtitle = _id.ToString();
        GD.Print($"Top-down level {_seed} {_id}: attempt {_map.Attempt + 1}, {ms} ms, {_map.Pois.Count} POIs{(_map.HasBoss ? ", boss" : "")}, {_map.Canopies.Count} canopy pieces, {_map.Decor.Count} decor{(_world.Options.Adventure ? $", {_world.Vents.Count} vents, {_world.ReefFish.Count} fish" : "")}");
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
        // Its corruption, laid out off the main thread: the floor's ink for the dive, the shards for when she lands.
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

    /// <summary>A level's corruption on screen: the live texture on its floor and maps, its creatures, and its shards.</summary>
    void SetupCorruption(LevelMap map)
    {
        _bladeTask = null;
        _vapour.Clear();
        _lastHp = -1f;
        if (_world.Corruption is not { } field)
        {
            _corruptTex = null;
            _level.SetCorruption(null);
            _corruption.Show(null, null);
            _blight.Show(_world);
            _adventure.Show(_world);
            _springs.Show(_world);
            return;
        }
        _corruptTex = new CorruptionTexture(field);
        _level.SetCorruption(_corruptTex.Texture);
        _blight.Show(_world);
        _adventure.Show(_world);
        _springs.Show(_world);
        // The shards were laid out already if she dived here; otherwise they are laid out now, off the main thread.
        if (_belowLayout is { } ready && ReferenceEquals(_belowLayoutMap, map)) _corruption.Show(ready, _corruptTex.Texture);
        else
        {
            _corruption.Show(null, null);
            _bladeTask = CorruptionView.Plan(map, field);
        }
        _belowLayout = null;
        _belowLayoutMap = null;
    }

    /// <summary>The corruption's per-frame work: its texture, shards arriving, the creatures.</summary>
    void SyncCorruption(float dt, float alpha)
    {
        if (_bladeTask is { IsCompleted: true } blades)
        {
            _bladeTask = null;
            if (blades.IsFaulted) GD.PushError(blades.Exception?.ToString());
            else if (_corruptTex is not null) _corruption.Show(blades.Result, _corruptTex.Texture);
        }
        if (_world.Corruption is { } field && _corruptTex is not null)
        {
            _corruptTex.Update(field, dt);
            _vapour.FromField(_corruptTex, _world.Map);
        }
        // Her own corruption clearing (a spring, or any healing) steams off her.
        float hp = _world.Player.Hp, max = _world.Run.MaxHp;
        if (_lastHp >= 0f && hp > _lastHp && max > 0f) _vapour.FromHer(Focus(alpha), (hp - _lastHp) / max);
        _lastHp = hp;
        _vapour.Tick(dt, _camera.Camera);
        _blight.Sync(_world, dt, _camera.Camera);
        _adventure.Sync(_world, dt, _camera.Camera);
        _springs.Sync(dt, _camera.Camera, Focus(alpha) with { Y = LevelMap.SwimBand });
        _cleanse.Track(_world, dt);
        MinimapView.FillSprings(_fullMap, _world);
        _minimap.SetSprings(_world);
    }

    /// <summary>Verification: start somewhere interesting.</summary>
    void PlaceAt(string? where)
    {
        System.Numerics.Vector2? at = where switch
        {
            // Under the level's first shortcut tunnel's roof, and at the chamber at its one end ("tunnel-mouth").
            "tunnel" => _map.Canopies.Where(c => c.Kind == CanopyKind.TunnelRoof).Select(c => (System.Numerics.Vector2?)c.Center).FirstOrDefault(),
            "tunnel-mouth" => _map.Canopies.Where(c => c.Kind == CanopyKind.TunnelRoof).Select(c => (System.Numerics.Vector2?)_map.Corridors[c.Corridor].Points[0]).FirstOrDefault(),
            "arch" => _map.Canopies.Where(c => c.Kind == CanopyKind.Arch).Select(c => (System.Numerics.Vector2?)c.Center).FirstOrDefault(),
            // Down the corridor from the level's first arch, clear of it (captures of the arch itself).
            "arch-near" => _map.Canopies.Where(c => c.Kind == CanopyKind.Arch)
                .Select(c => (System.Numerics.Vector2?)(c.Center + new System.Numerics.Vector2(-c.Axis.Y, c.Axis.X) * (c.HalfWidth + 6f))).FirstOrDefault(),
            "cave" => _map.Caves.Select(c => (System.Numerics.Vector2?)(c.Mouth + c.Facing * 5f)).FirstOrDefault(),
            "exit" => _map.Exit.Position + new System.Numerics.Vector2(0f, _map.Exit.Radius + 3f),
            "hole" => _map.Exit.Position,
            // Just west of the biggest kelp bed (with --dbg-steer=1,0 she swims straight through it).
            "kelp-west" => _map.Kelp.OrderByDescending(k => k.Roots.Count).Select(k => (System.Numerics.Vector2?)(k.Center - new System.Numerics.Vector2(k.Radius + 2f, 0f))).FirstOrDefault(q => q is { } v && _world.Clear(v, 0.6f)),
            // In the biggest kelp bed, a little in from its edge.
            "kelp" => _map.Kelp.OrderByDescending(k => k.Roots.Count).Select(k => (System.Numerics.Vector2?)(k.Center + new System.Numerics.Vector2(k.Radius * 0.5f, 0f))).FirstOrDefault(q => q is { } v && _world.Clear(v, 0.6f)),
            // Just outside Queen Clam's arena, in water that leads in.
            "boss" => Enumerable.Range(0, 24).Select(i => i * Mathf.Tau / 24f)
                .Select(a => (System.Numerics.Vector2?)(_map.Exit.Position + new System.Numerics.Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (_map.Exit.Radius + 2.5f)))
                .FirstOrDefault(q => q is { } v && _world.Clear(v, 0.8f) && _world.LineOfSight(v, _map.Exit.Position)),
            // Inside Queen Clam's arena: the fight starts at once.
            "arena" => _map.HasBoss ? _world.ArenaCenter + new System.Numerics.Vector2(0f, _world.ArenaRadius * 0.55f) : null,
            // Close in front of the boss (captures of its body).
            "boss-near" => _map.HasBoss ? _world.ArenaCenter + new System.Numerics.Vector2(0f, 6.5f) : null,
            "shop" => _map.Caves.Where(c => _map.Pois[c.Poi].Kind == PoiKind.Shop).Select(c => (System.Numerics.Vector2?)(c.Mouth - c.Facing * 1.5f)).FirstOrDefault(),
            // Out along the shop's passage, facing its gateway.
            "shop-out" => _map.Caves.Where(c => _map.Pois[c.Poi].Kind == PoiKind.Shop).Select(c => (System.Numerics.Vector2?)(c.Mouth + c.Facing * 6f)).FirstOrDefault(),
            // Beside the level's light spring, if it has one.
            "spring" => _world.Springs.Select(sp => (System.Numerics.Vector2?)(sp.Position + new System.Numerics.Vector2(0f, SpringTuning.Radius + 1.5f))).FirstOrDefault(),
            "spring-in" => _world.Springs.Select(sp => (System.Numerics.Vector2?)sp.Position).FirstOrDefault(),
            // A few metres south of a tall wall facing her (captures of its face).
            "wall" => Enumerable.Range(0, 70 * 70).Select(i => new System.Numerics.Vector2(i % 70 * 2f, i / 70 * 2f))
                .Where(v => _world.Clear(v, 1f) && _map.HeightAt(v - new System.Numerics.Vector2(0f, 5f)) > 4f && _map.HeightAt(v - new System.Numerics.Vector2(0f, 3f)) < 0f)
                .Select(v => (System.Numerics.Vector2?)v).FirstOrDefault(),
            // Beside the level's first bubble geyser.
            "geyser" => _world.Director.Geysers.Count > 0 ? _world.Director.Geysers[0] + new System.Numerics.Vector2(0f, 3f) : null,
            // Out in front of a secret room's sealed way in.
            "secret" => _map.WeakRocks.Where(r => r.Kind == SealKind.Secret).Select(r => (System.Numerics.Vector2?)(r.Center + LevelView.SealFacing(_map, r) * (r.Thick + 5f))).FirstOrDefault(q => q is { } v && _world.Clear(v, 0.6f)),
            // Outside the boss arena's first way in, that wall already down; she swims in through it (so a barred arena shuts
            // it behind her).
            "gate" => _world.Seals.Where(x => x.Kind == SealKind.Boss).Select(x =>
            {
                while (_world.VentsOpen > 0) _world.DebugSealVent();
                while (!x.Open) _world.DebugStrikeSeal(x);
                _level.Slide(x.Rock.Center);
                foreach (var e in _world.Events) _seals.OnEvent(_world, e);
                return (System.Numerics.Vector2?)(x.Rock.Center + x.Facing * (x.Rock.Thick + 4f));
            }).FirstOrDefault(),
            // Inside the level's secret room, its wall already down.
            "secret-in" => _world.Seals.Where(x => x.Kind == SealKind.Secret).Select(x =>
            {
                while (!x.Open) _world.DebugStrikeSeal(x);
                _level.Slide(x.Rock.Center);
                foreach (var e in _world.Events) _seals.OnEvent(_world, e);
                return (System.Numerics.Vector2?)_map.Caves[x.Rock.Cave].Chambers[0].Center;
            }).FirstOrDefault(),
            // In front of the first sealed wall of any kind (the boss's, on a boss level).
            "seal" => _map.WeakRocks.OrderByDescending(r => r.Kind).Select(r => (System.Numerics.Vector2?)(r.Center + LevelView.SealFacing(_map, r) * (r.Thick + 5f))).FirstOrDefault(q => q is { } v && _world.Clear(v, 0.6f)),
            // Beside the level's first flounder (south of it, in sight); "hearth": it is a Hearthguard, and she in its reach;
            // "hearth-far": just out of its reach, where it shimmies.
            "flounder" or "hearth" or "hearth-far" => _world.Flounders.SelectMany(f =>
            {
                if (where != "flounder" && !f.Corrupted) _world.DebugTurnFlounder(f);
                float off = where switch { "hearth" => 2.4f, "hearth-far" => 4.5f, _ => 4f };
                return Enumerable.Range(0, 16).Select(k => (System.Numerics.Vector2?)(f.Home + new System.Numerics.Vector2(MathF.Sin(k * 0.39f), MathF.Cos(k * 0.39f)) * off));
            }).FirstOrDefault(q => q is { } v && _world.Clear(v, 0.6f)),
            // A few metres off the level's first Thumpsquirt.
            // A few metres off the level's first Inkwarden.
            // In front of the level's first Lashjaw, in its reach.
            // A few metres south of the first Blightroot, in sight of it.
            // In a Blightroot's hammer reach (its swing comes at her).
            "root-near" => _world.Blightroots.SelectMany(r => Enumerable.Range(0, 16).Select(k => (System.Numerics.Vector2?)(r.Position + new System.Numerics.Vector2(MathF.Cos(k * 0.39f), MathF.Sin(k * 0.39f)) * 2.2f))).FirstOrDefault(q => q is { } v && _world.Clear(v, 0.6f)),
            "root" => _world.Blightroots.Select(r => (System.Numerics.Vector2?)(r.Position + new System.Numerics.Vector2(0f, 6f))).FirstOrDefault(q => q is { } v && _world.Clear(v, 0.6f)),
            "lashjaw" => _world.Lashjaws.Select(l => (System.Numerics.Vector2?)(l.Position + l.Facing * 6f)).FirstOrDefault(q => q is { } v && _world.Clear(v, 0.6f)),
            "warden" => _world.Inkwardens.Select(w => (System.Numerics.Vector2?)(w.Position + new System.Numerics.Vector2(0f, 7f))).FirstOrDefault(q => q is { } v && _world.Clear(v, 0.6f)),
            "thump" => _world.Thumpsquirts.Select(t => (System.Numerics.Vector2?)(t.Position + new System.Numerics.Vector2(0f, 8f))).FirstOrDefault(q => q is { } v && _world.Clear(v, 0.6f)),
            // Just outside the level's first den with a heart (else any den), in its nave's mouth.
            "den" => _world.Dens.OrderByDescending(d => d.Heart != DenHeart.None).Select(d => (System.Numerics.Vector2?)(d.At + new System.Numerics.Vector2(0f, d.Radius + 3f))).FirstOrDefault(q => q is { } v && _world.Clear(v, 0.6f)),
            // A few metres south of the level's first Blotter.
            "blotter" => _world.Blotters.Select(b => (System.Numerics.Vector2?)(b.Position + new System.Numerics.Vector2(0f, 3.4f))).FirstOrDefault(q => q is { } v && _world.Clear(v, 0.6f)),
            // A few metres out before the level's first vent, facing it.
            "vent" => _world.Vents.Select(v => (System.Numerics.Vector2?)(v.Position + v.Facing * 4f)).FirstOrDefault(q => q is { } v && _world.Clear(v, 0.6f)),
            "cache" => _map.Pois.Where(p => p.Kind == PoiKind.ShellCache).Select(p => (System.Numerics.Vector2?)p.Position).FirstOrDefault(),
            // A few metres from the first shell cache, its shells still lying there (captures of them).
            "cache-near" => _map.Pois.Where(p => p.Kind == PoiKind.ShellCache)
                .SelectMany(p => Enumerable.Range(0, 12).Select(k => p.Position + new System.Numerics.Vector2(Mathf.Cos(k * Mathf.Tau / 12f), Mathf.Sin(k * Mathf.Tau / 12f)) * 6f))
                .Select(q => (System.Numerics.Vector2?)q).FirstOrDefault(q => q is { } v && _world.Clear(v, 0.6f)),
            // A few metres south of the first pot, in water that sees it.
            "vase" => _world.Vases.Select(v => (System.Numerics.Vector2?)(v.Position + new System.Numerics.Vector2(0f, 4f))).FirstOrDefault(q => q is { } v && _world.Clear(v, 0.6f)),
            "ambush" => _map.Pois.Where(p => p.Kind == PoiKind.Ambush).Select(p => (System.Numerics.Vector2?)p.Position).FirstOrDefault(),
            "treasure" => _map.Caves.Where(c => _map.Pois[c.Poi].Kind == PoiKind.TreasureCave).Select(c => (System.Numerics.Vector2?)(c.Mouth - c.Facing * 2.5f)).FirstOrDefault(),
            // On a clam room's pearl: it is touched at once.
            "clam-pearl" => _map.Pois.Where(p => p.Kind == PoiKind.TreasureCave).Select(p => (System.Numerics.Vector2?)p.Position).FirstOrDefault(),
            // Out in the canyon before a clam room's mouth, to see its light spill.
            "treasure-out" => _map.Caves.Where(c => _map.Pois[c.Poi].Kind == PoiKind.TreasureCave).Select(c => (System.Numerics.Vector2?)(c.Mouth + c.Facing * 7f)).FirstOrDefault(q => q is { } v && _world.Clear(v, 0.6f)),
            "mob" => _world.Mobs.Select(m => (System.Numerics.Vector2?)(m.Position + new System.Numerics.Vector2(0f, 7f))).FirstOrDefault(q => q is { } v && _world.Clear(v, 0.6f)),
            // Outside the curse den's mouth, halfway down its tunnel, and at its end.
            "curse" => _world.Curse is { } c ? _map.Caves[_map.Pois[c.Poi].Cave].Mouth + _map.Caves[_map.Pois[c.Poi].Cave].Facing * 2.5f : null,
            "curse-deep" => _world.Curse is { } cd ? cd.Line[cd.Line.Count / 2] : null,
            "curse-end" => _world.Curse is { } ce ? ce.End : null,
            // The open water nearest the west rim, to look past the square's edge.
            // Beside the first anemone, its clownfish in view.
            "fish" => _world.Anemones.Select(a => (System.Numerics.Vector2?)(a.Position + new System.Numerics.Vector2(0f, 3f))).FirstOrDefault(q => q is { } v && _world.Clear(v, 0.6f)),
            "tang" => _world.ReefFish.Where(f => f.Kind == ReefFishKind.BlueTang).Select(f => (System.Numerics.Vector2?)(f.Position + new System.Numerics.Vector2(0f, 2.5f))).FirstOrDefault(q => q is { } v && _world.Clear(v, 0.6f)),
            "school" => _world.Schools.Select(sc => (System.Numerics.Vector2?)(sc.Position + new System.Numerics.Vector2(0f, 2.5f))).FirstOrDefault(q => q is { } v && _world.Clear(v, 0.6f)),
            "edge" => Enumerable.Range(0, LevelMap.Size).SelectMany(y => Enumerable.Range(0, LevelMap.Size).Select(x => new System.Numerics.Vector2(x, y)))
                .Where(q => _world.Clear(q, 0.8f)).OrderBy(q => q.X).Select(q => (System.Numerics.Vector2?)q).FirstOrDefault(),
            // A point of the level's grid, "x,y" (metres).
            _ when where?.Split(',') is [var sx, var sy] && float.TryParse(sx, NumberStyles.Float, CultureInfo.InvariantCulture, out float ax)
                && float.TryParse(sy, NumberStyles.Float, CultureInfo.InvariantCulture, out float ay) => new System.Numerics.Vector2(ax, ay),
            _ => null,
        };
        if (at is not { } p) return;
        _world.Player.Position = _world.Player.PrevPosition = p;
        if (where == "gate")
        {
            var toward = _world.ArenaCenter - p;
            _dbgSteer = new Vector2(toward.X, toward.Y).Normalized();
            _dbgGate = true;
        }
    }

    Vector3 Focus(float alpha)
    {
        var p = System.Numerics.Vector2.Lerp(_world.Player.PrevPosition, _world.Player.Position, alpha);
        // Verification: `--dbg-follow=mob|fish|blotter|murkling` keeps the camera on the nearest pufferling (or Blotter, or
        // murkling) instead, and
        // `--dbg-follow=clownfish|bluetang|chromis` on the nearest reef fish of that kind.
        if (_follow is not null)
        {
            var from = p;
            System.Numerics.Vector2? at = _follow switch
            {
                "fish" => _world.Fish.OrderBy(f => System.Numerics.Vector2.Distance(f.Position, from)).Select(f => (System.Numerics.Vector2?)f.Position).FirstOrDefault(),
                "boss" => _world.Boss is { } b ? b.Position : null,
                "tunnel" => _map.Canopies.Where(c => c.Kind == CanopyKind.TunnelRoof).Select(c => (System.Numerics.Vector2?)c.Center).FirstOrDefault(),
                "root" => _world.Blightroots.OrderBy(r => System.Numerics.Vector2.Distance(r.Position, from)).Select(r => (System.Numerics.Vector2?)r.Position).FirstOrDefault(),
                "murkling" => _world.Murklings.Where(m => m.Alive && !m.Budding).OrderBy(m => System.Numerics.Vector2.Distance(m.Position, from)).Select(m => (System.Numerics.Vector2?)m.Position).FirstOrDefault(),
                "blotter" => _world.Blotters.OrderBy(b => System.Numerics.Vector2.Distance(b.Position, from)).Select(b => (System.Numerics.Vector2?)b.Position).FirstOrDefault(),
                "mob" => _world.Mobs.Where(m => m.Alive).OrderBy(m => System.Numerics.Vector2.Distance(m.Position, from)).Select(m => (System.Numerics.Vector2?)m.Position).FirstOrDefault(),
                _ => _world.ReefFish.Where(f => f.Kind.ToString().Equals(_follow, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(f => System.Numerics.Vector2.Distance(f.Position, from)).Select(f => (System.Numerics.Vector2?)f.Position).FirstOrDefault(),
            };
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

    /// <summary>
    /// Her light in the water (light_field.gdshaderinc): the horizon map round her light (the level sits at the origin
    /// while she plays it), where the light is, and how strongly the water scatters it — more in the murkier, darker
    /// depths, none as she turns head-down into a dive.
    /// </summary>
    void SyncLantern()
    {
        var at = _bell.LightPosition;
        float range = _bell.LightRange;
        _lantern.Update(_map, at, range);
        RenderingServer.GlobalShaderParameterSet(OctoShoots.Game.Fx.SN.Of("her_light_at"), new Vector4(at.X, at.Y, at.Z, range));
        // Her searchlight's beam: which way, how wide.
        var beam = _bell.BeamDirection;
        RenderingServer.GlobalShaderParameterSet(OctoShoots.Game.Fx.SN.Of("her_spot"), new Vector4(beam.X, beam.Y, beam.Z, _bell.BeamCos));
        float murk = 0.5f + ReefLook.For(_id.Depth).Fog;
        RenderingServer.GlobalShaderParameterSet(OctoShoots.Game.Fx.SN.Of("her_scatter"), OS.GetCmdlineUserArgs().Contains("--dbg-noscatter") ? 0f : 0.016f * _bell.LightEnergy / Mathf.Pow(_bell.Lantern, 1.3f) * murk * (1f - _bell.DiveTurn));
    }

    /// <summary>Verification (--dbg-perf): uncapped, and once a second the frame's cost printed (CPU and GPU, ms).</summary>
    bool _perf;
    double _perfClock, _perfProcess, _perfWorst;
    int _perfFrames, _perfHitches;

    readonly Dictionary<string, (double Total, double Worst, long Bytes)> _perfCosts = new();
    int _gc0, _gc1, _gc2;
    long _allocated;
    TimeSpan _paused;

    void PerfAdd(string what, long start, long bytes)
    {
        double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        long allocated = GC.GetAllocatedBytesForCurrentThread() - bytes;
        var (total, worst, sum) = _perfCosts.TryGetValue(what, out var c) ? c : (0.0, 0.0, 0L);
        _perfCosts[what] = (total + ms, Math.Max(worst, ms), sum + allocated);
    }

    bool PerfReport(double delta, bool skip)
    {
        if (skip) return false;
        _perfClock += delta;
        _perfFrames++;
        _perfWorst = Math.Max(_perfWorst, delta);
        if (delta > 1.0 / 30.0) _perfHitches++;
        _perfProcess += Performance.GetMonitor(Performance.Monitor.TimeProcess);
        if (_perfClock < 3.0) return false;
        var vp = GetViewport().GetViewportRid();
        int gc0 = GC.CollectionCount(0), gc1 = GC.CollectionCount(1), gc2 = GC.CollectionCount(2);
        long allocated = GC.GetTotalAllocatedBytes();
        var paused = GC.GetTotalPauseDuration();
        GD.Print($"PERF gc {gc0 - _gc0}/{gc1 - _gc1}/{gc2 - _gc2} paused {(paused - _paused).TotalMilliseconds:0.0} ms alloc {(allocated - _allocated) / 1024} KB/s");
        (_gc0, _gc1, _gc2, _allocated, _paused) = (gc0, gc1, gc2, allocated, paused);
        GD.Print($"PERF fps {_perfFrames / _perfClock:0} | worst {1000.0 * _perfWorst:0.0} ms, {_perfHitches} over 33 ms | render cpu {RenderingServer.ViewportGetMeasuredRenderTimeCpu(vp):0.00} ms gpu {RenderingServer.ViewportGetMeasuredRenderTimeGpu(vp):0.00} ms"
            + $" | draws {Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame)} prims {Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame) / 1000:0}k objs {Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame)}");
        // The costliest parts of a frame: average per frame, and the worst single one.
        GD.Print("  " + string.Join("  ", _perfCosts.OrderByDescending(kv => kv.Value.Total).Take(8).Select(kv => $"{kv.Key} {kv.Value.Total / _perfFrames:0.00}/{kv.Value.Worst:0.0}")));
        GD.Print("  alloc KB/s: " + string.Join("  ", _perfCosts.OrderByDescending(kv => kv.Value.Bytes).Take(10).Select(kv => $"{kv.Key} {kv.Value.Bytes / 1024}")));
        _perfCosts.Clear();
        _perfClock = _perfProcess = _perfWorst = 0;
        _perfFrames = _perfHitches = 0;
        return true;
    }

    public override void _Process(double delta)
    {
        if (!_perf)
        {
            ProcessFrame(delta);
            return;
        }
        // Verification: a frame that took long, with what the game's own code spent of it.
        // (The frame after a report carries the report's own printing, tens of milliseconds on Windows: not counted.)
        bool afterReport = _reported;
        if (delta > 1.0 / 30.0 && !afterReport) GD.Print($"HITCH {1000.0 * delta:0.0} ms; the frame before spent {_lastFrameMs:0.0} ms in the game's code");
        _reported = PerfReport(afterReport ? 0.0 : delta, afterReport);
        long start = System.Diagnostics.Stopwatch.GetTimestamp();
        ProcessFrame(delta);
        _lastFrameMs = (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
    }

    double _lastFrameMs;
    float _lastWardSound = -1f;
    bool _reported;

    void ProcessFrame(double delta)
    {
        FitHud();
        _hud.ShowClock = _view.ShowClock;
        CapRenderScale();
        // F1, any time in a run: the debug menu.
        bool f1 = Input.IsPhysicalKeyPressed(Key.F1);
        if (f1 && !_f1Latched && _world is not null && _run is not null)
        {
            if (_debug.Visible) CloseDebug();
            else
            {
                if (_pause.Visible) _pause.Close();
                _debug.Open(_run, _catalog, _seed, _id, _floorFog);
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
        SyncPointer();
        SyncHaptics(dt);
        // Banners run on game time: they wait while paused and under a splash.
        if (!Paused && _transition == Transition.None && !_deathSplash)
        {
            _achievementBanner.Step(dt);
            ItemDef? absorbing = null;
            if (_world?.Pearls.Find(q => q.Absorbing) is { } ritual && _catalog is not null && _catalog.TryGet(ritual.ItemId, out var item)) absorbing = item;
            _pearlBanner.Step(dt, absorbing, _achievementBanner.Busy);
        }
        if (_debugSay is { } said && _levelTime > 1f)
        {
            _debugSay = null;
            Say(said);
        }
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
        if (_dbgGoTo > 0 && _frame == 60)
        {
            DebugGoTo(_id with { Level = _dbgGoTo }, true);
            _dbgGoTo = 0;
        }
        // The debug menu: everything stands still under it.
        if (_debug.Visible)
        {
            _accumulator = 0;
            _hud.Elapsed = _elapsed;
            _hud.Track(_world, _catalog, 0f);
            Capture();
            return;
        }

        DebugMoment();
        // The pause menu holds the sea still (and the clock): only the views go on, so she still moves in place.
        StepMenu(dt);
        bool held = Paused || _intro.Playing;
        _accumulator += delta;
        if (held) _accumulator = 0;
        int steps = 0;
        bool dived = false;
        while (_accumulator >= PlaneWorld.Dt && steps < 8 && !dived)
        {
            float hpBefore = _world.Player.Hp;
            long simStart = _perf ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
            long simBytes = _perf ? GC.GetAllocatedBytesForCurrentThread() : 0;
            _world.Step(ReadInput());
            if (_perf) PerfAdd("sim", simStart, simBytes);
            _recorder.Observe(_world);
            Earned(_achievements.Observe(_world));
            // The curse den's dark closing round her: a low drone.
            bool cursed = _world.CurseDarkDepth > CurseTuning.Inside;
            if (cursed && !_inCurse) _sfx.Play("curse_enter", -4f, 0f);
            _inCurse = cursed;
            // Healing from any source shows as a green number: the HP she gained beyond the hits she took this step.
            float taken = 0f;
            foreach (var e in _world.Events)
                if (e.Type == PlaneEventType.PlayerHit) taken += e.Size;
            float healed = _world.Player.Hp - hpBefore + taken;
            // A light spring's washing shows as one number: the whole of it, once she is clean or has left the water.
            bool bathing = _world.Player.Hp < _world.Run.MaxHp && _world.Springs.Exists(sp => sp.Bathing);
            if (bathing || _springHealed > 0f)
            {
                if (healed > 0f) _springHealed += healed;
                if (!bathing && _springHealed > 0.05f)
                {
                    _damage.ShowHeal(_world.Player.Position, _springHealed);
                    _springHealed = 0f;
                }
            }
            else if (healed > 0.05f) _damage.ShowHeal(_world.Player.Position, healed);
            _levelTime += PlaneWorld.Dt;
            _elapsed += PlaneWorld.Dt;
            _accumulator -= PlaneWorld.Dt;
            steps++;
            foreach (var e in _world.Events)
            {
                _blight.OnEvent(e);
                _adventure.OnEvent(_world, e);
                _seals.OnEvent(_world, e);
                // A level reached: her gonads pulse, on her and on the HUD, until the point is spent in the pause menu.
                if (e.Type == PlaneEventType.LevelUp)
                {
                    Say($"Level {(int)e.Size}! A skill point waits in the pause menu");
                    _sfx.PlayPitched("synergy", 1.25f, -6f);
                }
                // Any explosion (hers, a creature's, the reef's): the screen shakes and the controller kicks, by its size and
                // how near her it went off.
                if (Blast(e) is float power) Explode(e.Position, power);
                // Her light struck something: flares burst round its outline (hits by a shot carry its direction).
                if (e.Type is PlaneEventType.MobHit or PlaneEventType.MobDefeated or PlaneEventType.BossHit or PlaneEventType.VaseHit && e.Direction != default)
                {
                    _combat.Flares(e.Position, Outline(e), e.Direction, e.Type == PlaneEventType.VaseHit ? 1f : e.Size / PlaneCombatTuning.ShotDamage);
                    // Struck: the screen shakes a little, more for a harder hit.
                    if (e.Type is PlaneEventType.MobHit or PlaneEventType.MobDefeated) _camera.Shake(Mathf.Clamp(0.08f + 0.006f * e.Size, 0.08f, 0.22f));
                    // A tap for a hit landed, a firmer one for a creature freed.
                    if (e.Type == PlaneEventType.MobDefeated) _haptics.Pulse(0.4f, 0.25f, 0.16f);
                    else ShotKick(e.Size / PlaneCombatTuning.ShotDamage);
                }
                // Struck: her light bounces back off it toward her, and its corruption is knocked out through its far side
                // (the more of it the hit took, the more of both); a liquid body dents where it struck.
                if (e.Type is PlaneEventType.MobHit or PlaneEventType.MobDefeated or PlaneEventType.BossHit && Through(e) is { } through)
                {
                    _combat.HitBurst(e.Position, Outline(e), through, HitShare(e));
                    if (e.Type == PlaneEventType.MobHit) _blight.Struck(_world, e.Position, through);
                }
                CorruptionEvent(e);
                CurseEvent(e);
                if (e.Type == PlaneEventType.Dived) dived = true;
                else if (e.Type == PlaneEventType.PlayerDefeated) dived = false;
                else if (e.Type == PlaneEventType.PearlTouched)
                {
                    // The ritual begins: a soft rising chime.
                    _sfx.PlayPitched("merge", 0.8f, -6f);
                }
                else if (e.Type == PlaneEventType.PearlShattered)
                {
                    _sfx.PlayPitched("merge", 1.6f, -4f);
                    _combat.ShatterPearl(BellView.RitualAt(_bell.GlobalPosition, PearlRitual.ShatterAt));
                    // Its last: a burst of magic particles out from her.
                    _combat.MagicBurst(_bell.GlobalPosition);
                    _haptics.Pulse(0.45f, 0.2f, 0.12f);
                }
                else if (e.Type == PlaneEventType.Cavitation)
                {
                    // Pistol Shrimp Claw: the bang. Bigger blasts (Depth Charge) shake and kick harder.
                    float big = Mathf.Clamp((e.Size - 2.4f) / 2.6f, 0f, 1f);
                    _combat.Shockwave(e.Position, e.Size, new Color(0.75f, 0.92f, 1f, 0.85f), 0.38f + 0.2f * big);
                    _sfx.PlayAt("snap", new Vector3(e.Position.X, LevelMap.SwimBand, e.Position.Y), -4f + 5f * big);
                    _camera.Shake(0.04f + 0.2f * big);
                    _haptics.Pulse(0.05f + 0.6f * big, 0.5f * big, 0.04f + 0.12f * big);
                }
                else if (e.Type == PlaneEventType.SchoolPoints)
                {
                    // A school gathers into an arrow: a soft rising chime. What it means she finds out herself (no hints).
                    _sfx.PlayPitched("merge", 1.35f, -9f);
                }
                else if (e.Type == PlaneEventType.Necklace)
                {
                    // Pearl Necklace: the pearl bursts into its ring.
                    _combat.Shockwave(e.Position, 2.2f, HerColor.Light with { A = 0.9f }, 0.45f);
                    _sfx.PlayPitched("merge", 1.9f, -4f);
                }
                else if (e.Type == PlaneEventType.PearlCollected && _world.LastPearl is { } pearl)
                {
                    // The climax: her light bursts out and the pearl is hers.
                    _bell.Burst();
                    _combat.RitualBurst(_bell.GlobalPosition);
                    _sfx.PlayPitched("bell_ring", 1.1f, -3f);
                    _camera.Shake(0.25f);
                    _haptics.Pulse(1f, 1f, 0.45f);
                    AnnounceSynergies();
                }
                else if (e.Type == PlaneEventType.ShellCollected)
                {
                    // The very shell flies from where she took it to the counter: from where it is shown, its size there,
                    // turned as it lay (as the camera sees it).
                    var cam = _camera.Camera;
                    var view = GetViewport().GetVisibleRect().Size;
                    var at3 = new Vector3(e.Position.X, LevelMap.SwimBand, e.Position.Y);
                    var shownAt = OctoShoots.Game.Fx.Fisheye.Shown(cam.UnprojectPosition(at3), view);
                    float across = (OctoShoots.Game.Fx.Fisheye.Shown(cam.UnprojectPosition(at3 + cam.GlobalBasis.X * CombatView.ShellSize), view) - shownAt).Length();
                    Quaternion? lie = _combat.ShellLie(e.Position) is { } basis ? (cam.GlobalBasis.Orthonormalized().Inverse() * basis).GetRotationQuaternion() : null;
                    // (In the HUD's own units: it is scaled with the window.)
                    _hud.CollectShell(shownAt / HudScale, across / HudScale, lie);
                }
                else if (e.Type == PlaneEventType.CannotAfford) Say("Not enough shells");
                else if (e.Type == PlaneEventType.AmbushSprung) Say("Ambush!");
                else if (e.Type == PlaneEventType.PlayerDrained)
                {
                    _damage.Show(e);
                    _haptics.Pulse(0.25f, 0.2f, 0.25f);
                }
                else if (e.Type == PlaneEventType.DashStarted)
                {
                    _combat.DashBurst(e.Position);
                    _haptics.Pulse(0.35f, 0.3f, 0.14f);
                }
                else if (e.Type == PlaneEventType.PlayerHit)
                {
                    _damage.Show(e);
                    // Being hurt thuds: harder hits, harder thuds.
                    _haptics.Pulse(0.5f, Mathf.Clamp(0.45f + e.Size * 0.02f, 0.45f, 0.9f), 0.28f);
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
                else if (e.Type == PlaneEventType.Shot)
                {
                    _bell.Spend(e.Size);
                    // Lightning under water: a muffled crackle and a sizzle of bubbles, a touch louder for a stronger volley.
                    _sfx.PlayZap(Mathf.Clamp(-10f + 4f * Mathf.Log(Mathf.Max(_world.LastVolleyDamage / PlaneCombatTuning.ShotDamage, 0.5f)), -14f, -3f));
                    ShotKick(_world.LastVolleyDamage / PlaneCombatTuning.ShotDamage);
                }
                else if (e.Type is PlaneEventType.MobHit or PlaneEventType.BossHit) _damage.Show(e);
                else if (e.Type == PlaneEventType.ShotPopped) _combat.Pop(e.Position, e.Size * 1.7f, e.Direction);
                else if (e.Type == PlaneEventType.ShotOut) _combat.Extinguish(e.Position, e.Direction);
                else if (e.Type == PlaneEventType.LightBurst)
                {
                    _combat.LightBurst(e.Position, e.Size);
                    // Its mark: a small round smear of the picture where it went off.
                    _camera.AddBlur(e.Position, Mathf.Clamp(e.Size * 0.75f, 1f, 2.4f));
                }
                else if (e.Type == PlaneEventType.ActiveUsed) _combat.ActiveUsed(_world);
                else if (e.Type == PlaneEventType.SealRefused)
                {
                    // The boss's wall takes no light yet: a dull knock, and now and then why.
                    _sfx.PlayPitched("pop", 0.55f, -10f);
                    if (_levelTime - _sealToldAt > 4f)
                    {
                        _sealToldAt = _levelTime;
                        Say(e.Size == 1f ? "It will not open: one vent still pours" : $"It will not open: {(int)e.Size} vents still pour");
                    }
                }
                else if (e.Type == PlaneEventType.PushWave)
                {
                    // Tide Pulse: a wave of moving water from her bell (her swells' shimmer, as the currents'), running out
                    // to its reach in step with the sim's front, then fading; each harder and wider than the last.
                    int k = Math.Clamp((int)e.Size, 0, PushTuning.At.Length - 1);
                    var pl = _world.Player;
                    float grow = PushTuning.FrontShare * pl.PushLength;
                    _swells.AddRing(new Vector2(_bell.GlobalPosition.X, _bell.GlobalPosition.Z), 1.1f + 0.3f * k,
                        PushTuning.Reach[k] * pl.PushRadius / 2.6f, grow + 0.35f, snap: 6f);
                    _sfx.PlayPitched("spout", 0.75f + 0.12f * k, -6f + 2f * k);
                    if (_view.CameraShake) _camera.Shake(0.06f + 0.04f * k);
                    _haptics.Pulse(0.3f + 0.15f * k, 0.15f + 0.1f * k, 0.1f);
                }
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
                else if (e.Type == PlaneEventType.BossDefeated)
                {
                    // Her black pearl bursts into light: a white flash to the screen's edges (not with reduced motion),
                    // a bell, a long rumble; the arena's corruption burns gold as it goes.
                    _camera.Shake(0.6f);
                    if (!_view.ReducedMotion) _camera.EdgeFlash();
                    _sfx.PlayPitched("bell_ring", 0.8f, -4f);
                    _haptics.Pulse(0.8f, 0.9f, 0.6f);
                    _combat.LightBurst(e.Position, Mathf.Min(e.Size, 8f));
                }
                else if (e.Type == PlaneEventType.BossLeap) _sfx.PlayPitched("merge", 0.4f, -6f);
                else if (e.Type == PlaneEventType.BossFreed)
                {
                    _camera.Shake(0.4f);
                    Say($"{_world.Boss?.Spec.Name ?? PlaneBossTuning.Name} is freed!");
                }
            }
        }
        // The soundtrack follows the reef: darker the more of it the corruption holds, the combat layer while a fight is on.
        if (OctoShoots.Game.Audio.MusicManager.Instance is { } music)
        {
            music.SetCorruption(-(1f - _world.Cleansed));
            if (_world.InBattle && !_world.Defeated) music.EnterCombat();
            else music.ExitCombat();
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
        if (steps == 8) _accumulator = 0;
        float alpha = (float)(_accumulator / PlaneWorld.Dt);

        // The readouts first (her meter, the boss's bar, the clock), so they follow the world whatever the views do.
        _hud.Elapsed = _elapsed;
        _hud.Track(_world, _catalog, dt);
        float health = _world.Run.MaxHp > 0f ? _world.Player.Hp / _world.Run.MaxHp : 0f;
        _minimap.SetHealth(health);
        _minimap.SetExperience(_world.Run.Xp / (float)Math.Max(_world.Run.XpNeeded, 1), _world.Run.XpLevel, _world.Run.Points > 0);
        _lowHealth.Sync(health, dt);
        SyncExitPointer(dt);
        // Each view on its own: one that fails is reported (once) and the rest carry on.
        Guard("bell", () => _bell.Sync(_world, alpha, dt));
        Guard("prism", () =>
        {
            foreach (var part in _prismBells)
            {
                part.Lantern = _bell.Lantern;
                part.Dim = _bell.Dim;
                part.Sync(_world, alpha, dt);
            }
        });
        Guard("lantern", SyncLantern);
        OctoShoots.Game.Fx.SunMap.Poll(_map);
        Guard("orbit", () => _orbit.Sync(_world.Run.Items, _catalog, _bell.GlobalPosition, dt, _bell.Visible));
        _combat.HerAt = _bell.GlobalPosition;
        Guard("combat", () => _combat.Sync(_world, dt));
        Guard("surge", () => _surgeView.Sync(_world, dt));
        Guard("geysers", () => _geysers.Sync(_world, dt, _camera.Camera));
        _camera.SetBubbleLights(_combat.Lights, _combat.LightCount);
        _camera.SetLenses(_combat.Lenses, System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--dbg-nolens") >= 0 ? 0 : _combat.LensCount);
        // The water she pushes against as she sets off, turns or dashes.
        {
            var pl = _world.Player;
            // Where she is in the world (the level is drawn off its map's origin), so the post pass finds it.
            _swells.Watch(new Vector2(_bell.GlobalPosition.X, _bell.GlobalPosition.Z), new Vector2(pl.Velocity.X, pl.Velocity.Y), pl.DashTimer > 0f, dt);
            _camera.SetSwells(_swells);
        }
        Guard("kelp", () => _kelp.Sync(_world, dt, _bell.GlobalPosition, _swells, _camera.Camera));
        // Behind a blade, she wears a thin outline.
        _bell.Covered = _kelp.Covers(_bell.GlobalPosition, _camera.Camera) || _blight.Covers(_bell.GlobalPosition, _camera.Camera);
        Guard("currents", () => _currents.Sync(_world, dt));
        Guard("abyss", () => _abyss.Sync(_world, dt));
        Guard("vases", () => _vases.Sync(dt));
        Guard("pufferlings", () => _puffers.Sync(_world, dt));
        Guard("reef fish", () => _reefFish.Sync(_world, dt));
        Guard("corruption", () => SyncCorruption(dt, alpha));
        _damage.Tick(dt);
        Guard("boss", () => _boss.Sync(_world, dt));
        Guard("clam rooms", () => _clamRooms.Sync(_world, dt));
        Guard("seals", () => _seals.Sync(_world, dt, _camera.Camera));
        // A sealed wall grinding down into the sand (or back up out of it): the ground shakes as long as it goes.
        if (_seals.Sliding) _camera.Shake(0.22f);
        // A light spring healing her: a soft steady tremor.
        if (_world.Springs.Exists(sp => sp.Healing)) _camera.Shake(0.1f);
        if (_dbgRouse && _world.Time >= 2f)
        {
            _dbgRouse = false;
            foreach (var w in _world.Inkwardens) w.Corruption.Hit(0.5f);
        }
        if (_dbgSealVent && _world.Time >= 2f)
        {
            _dbgSealVent = false;
            int from = _world.Events.Count;
            var her = _world.Player.Position;
            _world.DebugSealVent(_world.Vents.Where(v => !v.Sealed).OrderBy(v => System.Numerics.Vector2.Distance(v.Position, her)).FirstOrDefault());
            for (int i = from; i < _world.Events.Count; i++)
            {
                _adventure.OnEvent(_world, _world.Events[i]);
                CorruptionEvent(_world.Events[i]);
            }
        }
        if (_dbgSeal > 0 && _world.Time >= 1f)
        {
            int from = _world.Events.Count;
            _world.DebugStrikeSeal();
            for (int i = from; i < _world.Events.Count; i++)
            {
                _seals.OnEvent(_world, _world.Events[i]);
                CorruptionEvent(_world.Events[i]);
            }
            _dbgSeal = 0;
        }
        _camera.SetClamGlows(_clamRooms.Glows, _clamRooms.GlowColors);
        // The battle begun: the boss's cut-in plays (once), the game held under it.
        if (_world.Boss is { Stage: BossStage.Banner } introBoss && !_introShown)
        {
            _introShown = true;
            if (!OS.GetCmdlineUserArgs().Contains("--dbg-nobanner")) _intro.Play(introBoss.Spec, _id.ShownDepth, _view.ReducedMotion);
        }
        // The pause menu's close framing: nearer, and looking past her so she sits at the screen's left.
        float framing = _menuBlend * _menuBlend * (3f - 2f * _menuBlend);
        _camera.Zoom = Mathf.Lerp(1f, MenuZoom, framing);
        _camera.Track(Focus(alpha), dt, look: new Vector3(MenuShift() * framing, 0f, 0f));
        _dark.Sync(_world, _camera.Camera, _bell.GlobalPosition, dt);
        // A wall between the camera and her (a tunnel's roof, a wall in front) is carved away round her while it hides
        // her, quick to open and gentle to close; one she only swims beside stays whole.
        bool hidden = _level.Hides(_bell.GlobalPosition, _camera.Camera.GlobalPosition);
        _seeThrough = Mathf.MoveToward(_seeThrough, hidden ? 1f : 0f, dt * (hidden ? 6f : 2f));
        _level.SeeThrough(_bell.GlobalPosition, SeeThroughRadius, _dbgNoCarve ? 0f : _seeThrough);
        // In the curse den's dark her own light is dimmed too, to about half.
        _bell.Dim = 1f - 0.475f * _dark.Amount;
        _minimap.Blackout = _dark.Amount;
        _snow.Tick(dt, Focus(alpha) + Vector3.Up * 6f, _camera.Camera.GlobalBasis);
        _level.UpdateCanopy(_world.Player.Position, dt, _camera.Camera.GlobalPosition);
        // The Crack opens: the sand over it sags, turns and drains away into the dark, and its glow comes up.
        if (_map.HasBoss && _world.ExitOpen && _crackOpen < 1f)
        {
            if (_crackOpen <= 0f)
            {
                _camera.Shake(0.3f);
                _sfx.Play("sand_sink", -2f, 0f);
                _haptics.Pulse(0.3f, 0.6f, 1.2f);
            }
            _crackOpen = Mathf.Min(1f, _crackOpen + dt / CrackOpenSeconds);
            _level.SetCrackOpen(_crackOpen);
        }
        var p = _world.Player;
        _debugMap.SetPlayer(_world.Player.Position);
        _minimap.Track(p.Position, p.Velocity);
        // Lantern Pearl: a brighter glow shows more of the level around her.
        float glow = _world.Run.Loadout.Stats[Stat.Glow];
        // In the curse den's dark she learns nothing of the map.
        if (_dark.Amount < 0.5f) _fog.Reveal(p.Position, MinimapView.RevealRadius * (1f + 0.5f * (glow - 1f)), MinimapView.RevealSoft);
        // Brass Compass: every place on both maps (secret rooms stay hidden).
        _minimap.Landmarks = _fullMap.Landmarks = _world.Run.Loadout.Flags.Contains("landmarks");
        // The adventure: the minimap only with Sea Chart Nautilus, and never a ring round it (the gonad gauge says those).
        bool adventure = _world.Options.Adventure;
        _minimap.Visible = HasMap;
        _minimap.Plain = adventure;
        _cleanse.Visible = !adventure;
        // Entering a place names it under the minimap.
        var inside = _map.Pois.Where(poi => System.Numerics.Vector2.Distance(poi.Position, p.Position) <= poi.Radius + 1.5f)
            .OrderBy(poi => System.Numerics.Vector2.Distance(poi.Position, p.Position)).FirstOrDefault();
        // The start needs no name under the map.
        bool named = inside is not null && inside.Kind != PoiKind.Start;
        _minimap.ShowPlace(named ? LevelView.PlaceName(_map, inside!.Kind) : null, named ? LevelView.PoiColor(inside!.Kind) : Colors.White);
        if (inside is not null) _visited.Add(_map.Pois.IndexOf(inside));
        // The Tab map: the whole screen, framing the level's outline.
        _fullMap.Frame(new Rect2(Vector2.Zero, GetViewport().GetVisibleRect().Size));
        _fullMap.Player = p.Position;
        _fullMap.Clock = HudView.Clock(_elapsed);
        if (p.Velocity.LengthSquared() > 0.04f) _fullMap.PlayerHeading = p.Velocity;
        int left = _world.Mobs.Count(m => m.Alive);
        _info.Text = $"{_id} · mobs {left}/{_world.Mobs.Count} · pearls {_run!.Items.Count}   attempt {_map.Attempt + 1}   pos {p.Position.X:0.0}, {p.Position.Y:0.0}   " +
                     $"{Engine.GetFramesPerSecond():0} FPS" +
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
        // The sea surface sinks with her: the sun's shade is this level's, so it is off until the next one is entered.
        OctoShoots.Game.Fx.SunMap.Enable(false);
        _diveTime = 0f;
        _diveFrom = _world.Player.Position;
        var next = LevelPlan.NextOf(_seed, _id);
        _lookFrom = ReefLook.For(_id.Depth);
        _lookTo = ReefLook.For(next.Depth);
        _bell.Kick();
        _hud.Diving = true;
        if (_captureDir is not null) GD.Print($"Dive begins at frame {_frame}");
        // The level's life stays behind (only she goes down): it fades out with the iris (FadeLevelLife).
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
        SyncLantern();

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
        _corruption.Fade(stay);
        // The minimap dives too: it zooms in on her and fades away (the new level's fades in once she is down).
        _minimap.DiveOut(Ease(t / 0.9f));
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
        _hud.Diving = false;
        _diving = false;
        bool leftTutorial = _id.IsTutorial && !LevelPlan.NextOf(_seed, _id).IsTutorial;
        _id = LevelPlan.NextOf(_seed, _id);
        if (leftTutorial) TutorialPassed();
        EnterLevel(map, keepRun: true, 0, arrived);
        _minimap.Arrive();
        foreach (var view in LevelLife) ResetFade(view);
        Route();
        if (_captureDir is not null) GD.Print($"Dive finishes at frame {_frame}");
    }

    /// <summary>The views of a level's life: its creatures, pickups and shots, Queen Clam, damage numbers, currents.</summary>
    IEnumerable<Node> LevelLife => new Node[] { _combat, _puffers, _reefFish, _curseDen, _clamRooms, _geysers, _boss, _damage, _currents, _vases, _abyss };

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

    /// <summary>
    /// She dived out of the tutorial: it is passed (runs start below it from now on), and the run proper begins here —
    /// its clock and totals from zero, with everything she gathered in the tutorial still hers.
    /// </summary>
    void TutorialPassed()
    {
        _recorder.Profile.TutorialPassed = true;
        ResetRunTotals();
        if (_run is not null) _recorder.StartRun(_seed.ToString(), _customSeed, _run.Items, continuing: false);
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
        ResetMenu();
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
        _splash.Fill("The corruption took Clementine", stats, "Her meter filled with ink. Everything she carried is lost.");
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
                    _splash.Prompt(WaitPrompt());
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
                    _splash.Prompt(WaitPrompt());
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
                    _id = RunStart();
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

    /// <summary>A status line for the F3 debug panel (nothing on the HUD: the game shows no log of messages).</summary>
    /// <summary>A message for her: the HUD's banner (HudView.Say).</summary>
    void Say(string text) => _hud.Say(text);

    void TogglePause()
    {
        if (_pause.IsOpen) Resume();
        else if (!_debug.Visible)
        {
            _pause.Open(_world.Run, _catalog, _seed.ToString(), $"{_id} · {HudView.Clock(_elapsed)}", _world.Player.Hp, _achievementData);
            if (_pad) _pause.FocusFirst();
        }
    }

    /// <summary>A saved run's corruption, from its health and the pearls it carries (they set her meter's size).</summary>
    string CorruptionText(float hp, System.Collections.Generic.IEnumerable<string> items)
    {
        float max = Loadout.Build(_catalog, items.ToList(), _tuning).Stats.MaxHp;
        return $"{Mathf.RoundToInt(Mathf.Clamp(1f - hp / Mathf.Max(max, 1f), 0f, 1f) * 100f)}% corrupted";
    }

    /// <summary>Synergies her pearls have made that she has not been told of yet (each once a run).</summary>
    readonly HashSet<string> _announced = new();

    /// <summary>A pearl just made a synergy: its name and what it does, a chime, a flare of her light.</summary>
    void AnnounceSynergies()
    {
        if (_catalog is null) return;
        foreach (string id in _world.Run.Loadout.Synergies)
        {
            if (!_announced.Add(id)) continue;
            var syn = _catalog.Synergies.FirstOrDefault(s => s.Id == id);
            if (syn is null) continue;
            _sfx.Play("synergy", -2f, 0f);
            _combat.Shockwave(_world.Player.Position, 3f, HerColor.Light with { A = 0.8f }, 0.6f);
        }
    }

    /// <summary>The curse den's moments (DESIGN-TOPDOWN §4.2): the laugh as its curse is sprung, the fountain, getting out.</summary>
    void CurseEvent(in PlaneEvent e)
    {
        switch (e.Type)
        {
            case PlaneEventType.CurseLaugh:
                _sfx.Play("curse_laugh", -1f, 0f);
                break;
            case PlaneEventType.FountainSpout:
                _sfx.PlayAt("spout", new Vector3(e.Position.X, LevelMap.SwimBand, e.Position.Y), -2f);
                break;
            case PlaneEventType.CurseCleared:
                // Out of its dark: the den is done.
                _sfx.PlayPitched("bell_ring", 1.3f, -10f);
                Say("Out of the curse");
                break;
        }
    }

    /// <summary>The corruption war's moments: shakes, sounds and words (the sights are BlightView's).</summary>
    /// <summary>How lit the vent at this mouth is (0..1).</summary>
    float VentGlowAt(System.Numerics.Vector2 mouth)
    {
        foreach (var v in _world.Vents)
            if (v.Position == mouth) return v.Glow;
        return 0f;
    }

    void CorruptionEvent(in PlaneEvent e)
    {
        switch (e.Type)
        {
            case PlaneEventType.VentLit:
                // The vent takes her light: a chime rising as it fills.
                _sfx.PlayPitched("bell_ring", 1.1f + 0.7f * VentGlowAt(e.Position), -13f);
                break;
            case PlaneEventType.VentSealed:
                // It grinds shut: a shudder, felt; the count of those left.
                _camera.Shake(0.4f);
                _haptics.Pulse(0.7f, 0.5f, 0.4f);
                _sfx.PlayPitched("pop", 0.35f, -2f);
                _sfx.PlayPitched("bell_ring", 0.85f, -6f);
                Say(e.Size > 0 ? $"Vent sealed · {(int)e.Size} left" : "Every vent sealed");
                break;
            case PlaneEventType.ExitOpened:
                _sfx.PlayPitched("bell_ring", 0.6f, -4f);
                Say("The way down is open");
                break;
            case PlaneEventType.AnemoneTurned:
                _sfx.PlayPitched("curse_enter", 1.6f, -12f);
                break;
            case PlaneEventType.GuardiansWoke:
                // Its guardians wake: a low call.
                _sfx.PlayPitched("curse_enter", 1.5f, -12f);
                break;
            case PlaneEventType.DenCleared:
                // A den's battle is over: a bell, a felt lift, its payout out, and its cleansing wave running out over the nave
                // (unseen but in the corruption going up in magic particles as it passes).
                _sfx.PlayPitched("bell_ring", 1.15f, -6f);
                _haptics.Pulse(0.4f, 0.3f, 0.25f);
                _camera.Shake(0.2f);
                Say("Den cleared");
                break;
            case PlaneEventType.BlightrootBurst:
                _camera.Shake(0.35f);
                _sfx.PlayPitched("bell_ring", 0.75f, -8f);
                Say("A Blightroot starves and bursts");
                break;
            case PlaneEventType.MurklingBurst:
                if (e.Direction.X > 0.5f)
                {
                    // A prime freed: a deep burst, felt.
                    _sfx.PlayPitched("bell_ring", 0.6f, -4f);
                    _camera.Shake(0.35f);
                    _haptics.Pulse(0.6f, 0.4f, 0.3f);
                }
                else _sfx.PlayPitched("pop", 0.6f, -6f);
                break;
            case PlaneEventType.BlightrootSwing:
                // A Blightroot rears back to swing at her: a creak of its pole.
                _sfx.PlayPitched("pop", 0.55f, -10f);
                break;
            case PlaneEventType.BlightrootSlam:
                // Its head comes down like a hammer: a deep thud and a jolt (the water it shoves: BlightView.Octopus).
                _sfx.PlayPitched("pop", 0.35f, -4f);
                _camera.Shake(e.Size > 0f ? 0.4f : 0.25f);
                break;
            case PlaneEventType.CrustBroken:
                _camera.Shake(0.3f);
                _sfx.PlayPitched("bell_ring", 1.2f, -10f);
                Say(e.Size > 0f ? $"Crust broken: {Mathf.RoundToInt(e.Size)} left" : "The last of her crust falls away");
                break;
            case PlaneEventType.SpringEntered:
                _sfx.PlayPitched("merge", 1.2f, -8f);
                break;
            case PlaneEventType.SpringCleansed:
                _sfx.PlayPitched("bell_ring", 1.4f, -10f);
                Say("Wholly cleansed");
                break;
            case PlaneEventType.PrismSplit:
            case PlaneEventType.PrismJoined:
                // Her light splits into its colours (or they flow back together): a jolt, the picture's colours pulled
                // apart for a moment, and a burst of light from her.
                _camera.Shake(0.35f);
                if (!_view.ReducedMotion)
                {
                    _camera.Aberrate();
                    _camera.EdgeFlash();
                }
                _combat.LightBurst(e.Position, 3.5f);
                _sfx.PlayPitched("bell_ring", e.Type == PlaneEventType.PrismSplit ? 1.5f : 1.25f, -6f);
                _sfx.PlayPitched("synergy", 1.2f, -10f);
                _haptics.Pulse(0.5f, 0.4f, 0.25f);
                break;
            case PlaneEventType.BossBrood:
                // Drippus Prime births a murkling: a wet pop as the piece snaps off it.
                _sfx.PlayPitched("pop", 0.5f, -7f);
                _sfx.PlayPitched("merge", 0.7f, -12f);
                break;
            case PlaneEventType.BossBounceWarn:
                // Its side dimples toward her: a low, rubbery creak.
                _sfx.PlayPitched("merge", 0.35f, -10f);
                break;
            case PlaneEventType.BossBounce:
                // The bounce: a deep rubbery boing, a thump in the controller and a small shake.
                _sfx.PlayPitched("pop", 0.3f, -3f);
                _sfx.PlayPitched("spout", 0.55f, -9f);
                _haptics.Pulse(0.7f, 0.5f, 0.2f);
                _camera.Shake(0.3f);
                break;
            case PlaneEventType.BombSplash:
                _sfx.PlayPitched("merge", 0.45f, -8f);
                if (System.Numerics.Vector2.Distance(e.Position, _world.Player.Position) < 8f) _camera.Shake(0.15f);
                break;
            case PlaneEventType.GeyserSpray:
                // A geyser bursts: a deep bubbling pop, and a jolt when she is close.
                _sfx.PlayPitched("pop", 0.45f, -8f);
                if (System.Numerics.Vector2.Distance(e.Position, _world.Player.Position) < 10f) _camera.Shake(0.12f);
                break;
            case PlaneEventType.MurkShotBurst:
                // A wet little pop, higher than a bomb's splash.
                _sfx.PlayPitched("merge", 1.6f, -12f);
                break;
            case PlaneEventType.FishFreed:
                // A fish again: a bright little chime.
                _sfx.PlayPitched("bell_ring", 1.8f, -14f);
                break;
            case PlaneEventType.ThumpLand:
                // A Thumpsquirt comes down: a deep thud, felt when it is near.
                _sfx.PlayPitched("pop", 0.4f, -6f);
                if (System.Numerics.Vector2.Distance(e.Position, _world.Player.Position) < 10f) _camera.Shake(0.22f);
                break;
            case PlaneEventType.LashOut:
                _sfx.PlayPitched("merge", 1.9f, -10f);
                break;
            case PlaneEventType.LashCatch:
                // Caught (her, not a fish): a jolt.
                if (e.Size == 0f)
                {
                    _camera.Shake(0.2f);
                    _haptics.Pulse(0.5f, 0.3f, 0.2f);
                }
                _sfx.PlayPitched("pop", 1.3f, -8f);
                break;
            case PlaneEventType.FlounderShimmy:
                // A Hearthguard shimmies into the sand: a soft scuff, only near.
                if (System.Numerics.Vector2.Distance(e.Position, _world.Player.Position) < 12f) _sfx.PlayPitched("merge", 0.7f, -16f);
                break;
            case PlaneEventType.FlounderWindup:
                // Its needle draws out: a rising creak.
                _sfx.PlayPitched("merge", 1.5f, -9f);
                break;
            case PlaneEventType.FlounderStrike:
                _sfx.PlayPitched("pop", 1.6f, -9f);
                break;
            case PlaneEventType.FlounderBite:
                _camera.Shake(0.3f);
                _haptics.Pulse(0.6f, 0.4f, 0.2f);
                _sfx.PlayPitched("pop", 0.6f, -4f);
                break;
            case PlaneEventType.LashBite:
                _camera.Shake(0.45f);
                _haptics.Pulse(0.9f, 0.7f, 0.3f);
                _sfx.PlayPitched("pop", 0.35f, -2f);
                break;
            case PlaneEventType.LashStun:
                _sfx.PlayPitched("bell_ring", 0.8f, -8f);
                break;
            case PlaneEventType.LashFreed:
                _sfx.PlayPitched("bell_ring", 1.2f, -5f);
                break;
            case PlaneEventType.Warded:
                // Her light turned aside: a dull, muffled knock (not more than a few a second).
                if (_levelTime - _lastWardSound > 0.15f)
                {
                    _lastWardSound = _levelTime;
                    _sfx.PlayPitched("merge", 0.55f, -14f);
                }
                break;
            case PlaneEventType.WardenFreed:
                _sfx.PlayPitched("bell_ring", 1.3f, -6f);
                break;
            case PlaneEventType.ThumpBurst:
                _sfx.PlayPitched("bell_ring", 1.1f, -6f);
                break;
            case PlaneEventType.SealLit:
                // The emblem takes her light: a chime rising as it fills.
                _sfx.PlayPitched("bell_ring", 1.3f + 0.7f * e.Size, -12f);
                break;
            case PlaneEventType.SealOpened:
                // The wall slides down into the sand; the charts show the way in.
                _level.Slide(e.Position);
                _camera.Shake(0.45f);
                _haptics.Pulse(0.7f, 0.5f, 0.45f);
                _sfx.PlayPitched("pop", 0.35f, -2f);
                _sfx.PlayPitched("bell_ring", 0.9f, -6f);
                _minimap.SetMap(_map, _world.SealedCaves);
                _fullMap.SetMap(_map, _world.SealedCaves);
                break;
            case PlaneEventType.SealClosed:
                // A barred arena's way in rises back up out of the sand behind her.
                _level.Raise(e.Position);
                _camera.Shake(0.45f);
                _haptics.Pulse(0.7f, 0.5f, 0.45f);
                _sfx.PlayPitched("pop", 0.3f, -2f);
                _sfx.PlayPitched("bell_ring", 0.7f, -6f);
                break;
            case PlaneEventType.MurkShotLanded:
                _sfx.PlayPitched("merge", 0.7f, -16f);
                break;
            case PlaneEventType.BlotterBorn:
                _sfx.PlayPitched("merge", 0.5f, -6f);
                break;
            case PlaneEventType.BlotterBurst:
                _camera.Shake(0.3f);
                _sfx.PlayPitched("bell_ring", 0.9f, -8f);
                _haptics.Pulse(0.5f, 0.5f, 0.3f);
                break;
        }
    }

    /// <summary>Corruption a light spring has washed off her since she entered it (shown as one number when she is done).</summary>
    float _springHealed;


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

    /// <summary>The pause menu's close framing: the camera closes in as the drawer slides in, and eases back as it slides away.</summary>
    void StepMenu(float dt)
    {
        _menuBlend = _pause.IsOpen ? Mathf.Min(1f, _menuBlend + dt / MenuIn) : Mathf.Max(0f, _menuBlend - dt / MenuOut);
        if (!_dbgLevelUp || _world.Run.Points <= 0) return;
        // Verification (--dbg-levelup): the menu opens and spends her points, one every 1.2 s.
        if (!_pause.IsOpen) TogglePause();
        _spendIn += dt;
        if (_spendIn <= 1.2f) return;
        _spendIn = 0f;
        ChooseSkill(OctoShoots.Core.Items.Stat.MaxHp);
    }

    /// <summary>A modifier chosen on the level-up tab: the point is spent and the tab shows it.</summary>
    void ChooseSkill(OctoShoots.Core.Items.Stat stat)
    {
        if (!_world.SpendSkill(stat)) return;
        _sfx.PlayPitched("bell_ring", 1.3f, -6f);
        _pause.Refill(_world.Run);
    }

    /// <summary>A new level, a restart or her end: the menu is gone and the camera back at once.</summary>
    void ResetMenu()
    {
        _menuBlend = 0f;
        _pause.Shut();
        _camera.Zoom = 1f;
    }

    /// <summary>How far east of her the menu's camera looks so she sits at MenuAt across the screen (metres).</summary>
    float MenuShift()
    {
        var cam = _camera.Camera;
        float distance = (cam.GlobalPosition - Focus(1f)).Length();
        var size = GetViewport().GetVisibleRect().Size;
        float half = distance * Mathf.Tan(Mathf.DegToRad(cam.Fov) * 0.5f) * size.X / Mathf.Max(size.Y, 1f);
        return (0.5f - MenuAt) * 2f * half;
    }

    void HandleKeys()
    {
        // During the boss intro, it takes the keys.
        if (_intro.Playing) return;
        // Esc or Start pauses and resumes; B also resumes (and closes the debug menu).
        bool esc = Input.IsActionPressed(InputSetup.Pause);
        bool back = Input.IsActionPressed(InputSetup.MenuBack);
        if (esc && !_escLatched)
        {
            if (_debug.Visible) CloseDebug();
            else if (_seedField.HasFocus()) _seedField.ReleaseFocus();
            else TogglePause();
        }
        else if (back && !_backLatched && (_pause.IsOpen || _debug.Visible))
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
        // The whole level's map (Tab / Select): in the adventure only with Sea Chart Nautilus, as the minimap.
        if (!OS.GetCmdlineUserArgs().Contains("--map")) _fullMap.Visible = Input.IsActionPressed(InputSetup.Review) && !typing && HasMap;
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

    /// <summary>
    /// How far out from its middle the outline of the body a hit struck runs: the body nearest the hit's place (the event
    /// carries its middle as it was struck; it may have moved on a little within the step).
    /// </summary>
    float Outline(PlaneEvent e)
    {
        var at = e.Position;
        // A Blightroot's head, a Lashjaw's skull and anything else: about a metre across.
        float best = 1.2f, radius = 0.6f;
        void Near(System.Numerics.Vector2 p, float r)
        {
            float d = System.Numerics.Vector2.Distance(p, at);
            if (d < best) (best, radius) = (d, r);
        }
        if (e.Type == PlaneEventType.VaseHit)
        {
            foreach (var v in _world.Vases) Near(v.Position, v.Radius);
            return radius;
        }
        if (e.Type == PlaneEventType.BossHit)
            return _world.Boss is { } boss && System.Numerics.Vector2.Distance(boss.Position, at) < 0.5f ? boss.Radius : PlaneBossTuning.SacRadius;
        // (A murkling's drawn body, its lobes swelling round it, reaches past its radius.)
        foreach (var m in _world.Murklings) Near(m.Position, m.Radius * 1.3f);
        foreach (var m in _world.Mobs) Near(m.Position, m.Radius);
        foreach (var b in _world.Blotters) Near(b.Position, BlotterTuning.ShellRadius);
        foreach (var w in _world.Inkwardens) Near(w.Position, WardenTuning.Radius);
        foreach (var t in _world.Thumpsquirts) Near(t.Position, ThumpTuning.Radius);
        return radius;
    }

    /// <summary>The way a hit went through what it struck: the shot's own way, else from her to it (null: none).</summary>
    System.Numerics.Vector2? Through(PlaneEvent e)
    {
        if (e.Direction != default) return e.Direction;
        var to = e.Position - _world.Player.Position;
        return to.LengthSquared() > 1e-6f ? System.Numerics.Vector2.Normalize(to) : null;
    }

    /// <summary>The share of the struck creature's whole health a hit took (0..1; the creature nearest the hit's place).</summary>
    float HitShare(PlaneEvent e)
    {
        if (e.Type == PlaneEventType.BossHit) return _world.Boss is { } boss ? Mathf.Clamp(e.Size / Mathf.Max(boss.MaxHp, 1f), 0f, 1f) : 0f;
        var at = e.Position;
        float best = 1.5f, whole = 0f;
        void Near(System.Numerics.Vector2 p, float max)
        {
            float d = System.Numerics.Vector2.Distance(p, at);
            if (d < best) (best, whole) = (d, max);
        }
        foreach (var m in _world.Mobs) Near(m.Position, PlaneCombatTuning.MobHp);
        foreach (var m in _world.Murklings) Near(m.Position, m.Corruption.Max);
        foreach (var r in _world.Blightroots) Near(r.Position, r.Corruption.Max);
        foreach (var b in _world.Blotters) Near(b.Position, b.Corruption.Max);
        foreach (var w in _world.Inkwardens) Near(w.Position, w.Corruption.Max);
        foreach (var t in _world.Thumpsquirts) Near(t.Position, t.Corruption.Max);
        foreach (var l in _world.Lashjaws) Near(l.Position, l.Corruption.Max);
        foreach (var f in _world.Flounders) Near(f.Position, f.Corruption.Max);
        return whole > 0f ? Mathf.Clamp(e.Size / whole, 0f, 1f) : 0f;
    }

    /// <summary>How big an explosion an event is (0..1), or null when it is none.</summary>
    static float? Blast(PlaneEvent e) => e.Type switch
    {
        // Hers: the Starburst Gland's burst (it comes with nearly every shot, so only a subtle tremor) and the Pistol
        // Shrimp's bang.
        PlaneEventType.LightBurst => 0.06f,
        PlaneEventType.Cavitation => 0.55f,
        // The creatures': a Blotter's bomb, a creature of ink bursting (a prime far more than a murkling), a Blightroot
        // bursting or slamming down, a Thumpsquirt bursting, a boss's ink sac.
        PlaneEventType.BombSplash => 0.5f,
        PlaneEventType.BlotterBurst => 0.7f,
        PlaneEventType.MurklingBurst => e.Direction.X > 0.5f ? 0.8f : 0.25f,
        PlaneEventType.BlightrootBurst => 0.8f,
        PlaneEventType.BlightrootSlam => 0.6f,
        PlaneEventType.ThumpBurst => 0.6f,
        PlaneEventType.BossSacBurst => 0.6f,
        PlaneEventType.MurkShotBurst => 0.2f,
        // The reef's: a geyser bursting.
        PlaneEventType.GeyserSpray => 0.45f,
        _ => null,
    };

    /// <summary>
    /// An explosion here, this big: the screen shakes and the controller kicks — fully within 5 m of her, fading out by
    /// 24 m (it is felt through the water, less the farther off).
    /// </summary>
    void Explode(System.Numerics.Vector2 at, float power)
    {
        float near = 1f - Mathf.SmoothStep(5f, 24f, System.Numerics.Vector2.Distance(at, _world.Player.Position));
        if (near <= 0.02f) return;
        _camera.Shake(Mathf.Min((0.03f + 0.42f * power) * near, 0.5f));
        _haptics.Pulse((0.1f + 0.7f * power) * near, (0.05f + 0.8f * power) * near, 0.08f + 0.22f * power);
    }

    PlaneInput ReadInput()
    {
        var input = new PlaneInput();
        // Verification (--at=gate): she stops once the battle has begun, just inside, the way in behind her.
        if (_dbgGate && _world.BossBattle) _dbgSteer = null;
        // Verification (--dbg-free=seconds): the boss is freed that long after it lands.
        if (_dbgFree > 0f && _world.Boss is { Stage: BossStage.Fight, StageTime: var fought } && fought >= _dbgFree)
        {
            _world.DebugFreeBoss();
            _dbgFree = 0f;
        }
        if (_dbgSteer is { } steerTo && !(_dbgSteerFight && _world.Boss is not { Stage: BossStage.Fight }))
        {
            input.Move = new System.Numerics.Vector2(steerTo.X, steerTo.Y);
            return input;
        }
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
            // Controller: the right stick aims, the trigger shoots (her light always flies her full range). Left alone, she
            // aims the way she swims (standing still, the way she last aimed).
            if (aim.LengthSquared() > 0.04f) _padAim = new System.Numerics.Vector2(aim.X, aim.Y);
            else if (stick.LengthSquared() > 0.04f) _padAim = System.Numerics.Vector2.Normalize(new System.Numerics.Vector2(stick.X, stick.Y));
            else if (_padAim.LengthSquared() > 1e-6f) _padAim = System.Numerics.Vector2.Normalize(_padAim);
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
            // The mouse: shoot toward the pointer while the button is held (only its way counts: her light flies her full range).
            var to = MouseOnBand() - _world.Player.Position;
            input.Aim = to.LengthSquared() > 1e-4f ? System.Numerics.Vector2.Normalize(to) : default;
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
        // The boss intro: any key, click or button skips to its end (and does nothing else).
        if (_intro.Playing && _intro.Elapsed > 0.3f && e.IsPressed() && !e.IsEcho() && e is InputEventKey or InputEventMouseButton or InputEventJoypadButton)
        {
            _intro.Skip();
            _fireBlocked = true;
            GetViewport().SetInputAsHandled();
            return;
        }
        if (e is InputEventJoypadButton or InputEventJoypadMotion) _haptics.Device = e.Device;
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
        InputSetup.UsingPad = pad;
        SyncPointer();
        if (pad && _pause.Visible) _pause.FocusFirst();
        // The key hints on screen follow the device in use.
        _pause.ShowHints();
        if (_transition == Transition.Waiting) _splash.Prompt(WaitPrompt());
    }

    /// <summary>On the controller (from the last input; carried over from the title).</summary>
    bool _pad = InputSetup.UsingPad;
    System.Numerics.Vector2 _padAim;

    /// <summary>The splash's prompt while it waits for her: in the device's own buttons (the menu bindings on a controller).</summary>
    string WaitPrompt()
    {
        string go = _pad ? InputSetup.Hint(InputSetup.MenuAccept) : "Enter or click", back = _pad ? InputSetup.Hint(InputSetup.MenuBack) : "Esc";
        if (!_deathSplash) return $"Press {go} to continue";
        return _persist ? $"{go}: a new run  ·  {back}: back to the title" : $"Press {go} to start a new run";
    }
    readonly Haptics _haptics = new();

    readonly HashSet<string> _failedViews = new();

    /// <summary>Runs one view's update; if it throws, the error is reported once and the frame carries on without it.</summary>
    void Guard(string view, Action sync)
    {
        long start = _perf ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
        long bytes = _perf ? GC.GetAllocatedBytesForCurrentThread() : 0;
        try
        {
            sync();
            if (_perf) PerfAdd(view, start, bytes);
        }
        catch (Exception ex)
        {
            if (_failedViews.Add(view)) GD.PushError($"The {view} view failed: {ex}");
        }
    }

    /// <summary>
    /// Her light leaving her or landing, by its power (in plain shots' damage): an ordinary shot is a barely-there
    /// flicker; only a powerful one (a big volley, a merged, charged or empowered bubble) is a real kick.
    /// </summary>
    void ShotKick(float power)
    {
        float k = Mathf.SmoothStep(2.5f, 8f, power);
        _haptics.Pulse(Mathf.Lerp(0.05f, 0.8f, k), Mathf.Lerp(0f, 0.65f, k), Mathf.Lerp(0.03f, 0.14f, k));
    }

    /// <summary>
    /// The controller's buzz this frame: the pulses the sim's moments asked for, and a hum while a current carries her
    /// (stronger the faster it runs). Only while she plays with a controller and vibration is on.
    /// </summary>
    void SyncHaptics(float dt)
    {
        bool playing = _world is not null && !Paused && !_deathSplash && !_diving && _transition == Transition.None;
        if (playing)
        {
            var flow = _world!.Director.FlowAt(_world.Player.Position, _world.Time);
            float k = Mathf.Clamp(flow.Length() / ReefDirectorTuning.SurgeSpeedMax, 0f, 1f);
            if (k > 0.05f) _haptics.Hold(0.1f + 0.12f * k, 0.18f * k);
            // Firefly Squid's surge: a faint steady hum while it pours out, firmer as it grows stronger.
            if (_world.LightSurge is { } surge) _haptics.Hold(0.05f + 0.05f * surge.Power, 0.02f * surge.Power);
            // A light spring healing her: a warm hum, swelling and easing like a slow breath.
            if (_world.Springs.Exists(sp => sp.Healing)) _haptics.Hold(0.16f + 0.08f * Mathf.Sin(_world.Time * 5f), 0.06f);
            // The absorbing ritual: a hum that rises as she takes the pearl's look, to a trembling peak while she holds
            // it (the shatter and the burst add their own pulses).
            foreach (var q in _world.Pearls)
            {
                if (!q.Absorbing) continue;
                float r = Mathf.Clamp(q.Ritual / PearlRitual.ShatterAt, 0f, 1f);
                float tremble = q.Ritual > PearlRitual.Mimic ? 0.08f * (0.5f + 0.5f * Mathf.Sin(q.Ritual * 60f)) : 0f;
                _haptics.Hold(0.08f + 0.5f * r * r + tremble, 0.55f * r * r * r + tremble);
            }
        }
        _haptics.Tick(dt, playing && _pad && _view.Vibration);
    }

    public override void _ExitTree()
    {
        _haptics.Stop();
        // Back to the system's arrow off the reef.
        OctoShoots.Game.Fx.GonadCursor.Release();
    }
    /// <summary>
    /// The pointer: playing with the mouse, a small orange crosshair of her four gonads (GonadCursor) — she also turns
    /// to face it (BellView), her first gonad toward it; under a menu, a splash, a dive or a level change, the system's
    /// own arrow; with the controller, the system's pointer hidden and her crosshair drawn the way she aims (SyncCrosshair).
    /// </summary>
    void SyncPointer()
    {
        bool playing = _world is not null && !Paused && !_deathSplash && !_diving && _transition == Transition.None && !_autopilot && !_intro.Playing;
        SyncCrosshair(playing);
        var mode = _pad ? Input.MouseModeEnum.Hidden : Input.MouseModeEnum.Visible;
        if (Input.MouseMode != mode) Input.MouseMode = mode;
        bool cross = playing && !_pad;
        if (cross == _crosshairOn) return;
        _crosshairOn = cross;
        if (cross) OctoShoots.Game.Fx.GonadCursor.Use();
        else OctoShoots.Game.Fx.GonadCursor.Release();
    }
    bool _crosshairOn;

    /// <summary>
    /// The crosshair stands no farther from her than CrossReach of the screen's height, the mouse's or the controller's
    /// (only its way from her sets her aim). The mouse's pointer, strayed past that circle, is drawn back onto it. With the
    /// controller her crosshair is drawn the way she aims, out as far as the right stick is tilted (at least PadCrossLeast
    /// of the circle); let go, it holds there as her aim follows the way she swims.
    /// </summary>
    void SyncCrosshair(bool playing)
    {
        _padCross.Visible = playing && _pad;
        if (!playing || _world is null) return;
        float reach = CrossReach * GetViewport().GetVisibleRect().Size.Y;
        var her = OnScreen(_world.Player.Position);
        if (!_pad)
        {
            var off = GetViewport().GetMousePosition() - her;
            if (GetWindow().HasFocus() && off.Length() > reach + 1f) GetViewport().WarpMouse(her + off.Normalized() * reach);
            return;
        }
        float tilt = Input.GetVector(InputSetup.AimLeft, InputSetup.AimRight, InputSetup.AimUp, InputSetup.AimDown).Length();
        if (tilt > 0.2f) _padCrossOut = Mathf.Clamp(tilt, PadCrossLeast, 1f);
        var way = OnScreen(_world.Player.Position + _world.Player.Aim) - her;
        if (way.LengthSquared() < 1e-6f) return;
        float side = 32f * Mathf.Max(1f, GetViewport().GetVisibleRect().Size.Y / 1080f);
        _padCross.Size = Vector2.One * side;
        _padCross.Position = her + way.Normalized() * reach * _padCrossOut - _padCross.Size * 0.5f;
    }

    /// <summary>The farthest the crosshair stands from her: this share of the screen's height.</summary>
    const float CrossReach = 0.45f;
    /// <summary>The controller's crosshair at the stick's lightest tilt: this share of CrossReach out from her.</summary>
    const float PadCrossLeast = 0.3f;
    /// <summary>The controller's crosshair (her own, GonadCursor's) and how far out it stands now (a share of CrossReach).</summary>
    TextureRect _padCross = null!;
    float _padCrossOut = 0.6f;

    /// <summary>Verification: fire at the boss in her fight, else at the nearest mob.</summary>
    bool AutoFire(ref PlaneInput input)
    {
        if (!_autoFire) return false;
        var from = _world.Player.Position;
        System.Numerics.Vector2? target = _world.Boss is { Stage: BossStage.Fight } queen ? queen.Position
            : _world.Mobs.Where(m => m.Alive).OrderBy(m => System.Numerics.Vector2.Distance(m.Position, from)).Select(m => (System.Numerics.Vector2?)m.Position).FirstOrDefault()
              // Else the nearest corrupted creature (not the wardens: what they guard).
              ?? _world.Murklings.Where(m => m.Alive && !m.Budding).Select(m => m.Position)
                  .Concat(_world.Blightroots.Where(r => r.Alive).Select(r => r.Position))
                  .Concat(_world.Blotters.Where(b => b.Alive && b.Rise <= 0f).Select(b => b.Position))
                  .Concat(_world.Thumpsquirts.Where(t => t.Alive).Select(t => t.Position))
                  .Concat(_world.Lashjaws.Where(l => l.Alive).Select(l => l.Position))
                  .Where(q => System.Numerics.Vector2.Distance(q, from) < 14f)
                  .OrderBy(q => System.Numerics.Vector2.Distance(q, from)).Select(q => (System.Numerics.Vector2?)q).FirstOrDefault()
              // With no creature to shoot, the nearest open vent near her (the adventure's), else the nearest standing pot.
              ?? _world.Vents.Where(v => !v.Sealed && System.Numerics.Vector2.Distance(v.Wall, from) < 14f)
                  .OrderBy(v => System.Numerics.Vector2.Distance(v.Wall, from)).Select(v => (System.Numerics.Vector2?)v.Wall).FirstOrDefault()
              ?? _world.Vases.Where(v => !v.Broken).OrderBy(v => System.Numerics.Vector2.Distance(v.Position, from)).Select(v => (System.Numerics.Vector2?)v.Position).FirstOrDefault();
        // Nothing to shoot at: straight ahead, the way she faces.
        input.Aim = target is { } at && System.Numerics.Vector2.DistanceSquared(at, from) > 1e-4f ? System.Numerics.Vector2.Normalize(at - from) : _world.Player.Aim;
        // With Pearl Diver, let go once the pearl is fully charged.
        input.Fire = !(_world.Run.Loadout.Shot.Charge && _world.Player.Charge >= PlaneCombatTuning.ChargeSeconds);
        return true;
    }

    /// <summary>The point under the mouse on the swim band (her own position if the ray misses it).</summary>
    System.Numerics.Vector2 MouseOnBand()
    {
        var cam = _camera.Camera;
        // What is shown under the pointer lies, undistorted, at the fisheye's source of it.
        Vector2 mouse = OctoShoots.Game.Fx.Fisheye.Source(GetViewport().GetMousePosition(), GetViewport().GetVisibleRect().Size);
        Vector3 origin = cam.ProjectRayOrigin(mouse), dir = cam.ProjectRayNormal(mouse);
        if (Mathf.Abs(dir.Y) < 1e-4f) return _world.Player.Position;
        Vector3 hit = origin + dir * ((LevelMap.SwimBand - origin.Y) / dir.Y);
        return new System.Numerics.Vector2(hit.X, hit.Z);
    }

    /// <summary>Where a point on the swim band is shown on the screen (through the fisheye).</summary>
    Vector2 OnScreen(System.Numerics.Vector2 p) =>
        OctoShoots.Game.Fx.Fisheye.Shown(_camera.Camera.UnprojectPosition(new Vector3(p.X, LevelMap.SwimBand, p.Y)), GetViewport().GetVisibleRect().Size);

    void Capture()
    {
        _frame++;
        if (_dbgPop && _frame == 30)
        {
            // Verification (`--dbg-pop`): a murkling, a prime, a Thumpsquirt and a Lashjaw freed round her at once.
            var her = _world.Player.Position;
            _blight.OnEvent(new PlaneEvent(PlaneEventType.MurklingBurst, her + new System.Numerics.Vector2(-4f, -2f), System.Numerics.Vector2.Zero, 0f));
            _blight.OnEvent(new PlaneEvent(PlaneEventType.MurklingBurst, her + new System.Numerics.Vector2(0f, -6f), System.Numerics.Vector2.UnitX, 0f));
            _blight.OnEvent(new PlaneEvent(PlaneEventType.ThumpBurst, her + new System.Numerics.Vector2(4.5f, -1f), System.Numerics.Vector2.Zero, 0f));
            _blight.OnEvent(new PlaneEvent(PlaneEventType.LashFreed, her + new System.Numerics.Vector2(-5f, 3f), System.Numerics.Vector2.Zero, 0f));
            _dbgPop = false;
        }
        if (_captureDir is null || _captureFrames.Count == 0 || _frame < _captureFrames.Peek()) return;
        int frame = _captureFrames.Dequeue();
        string path = $"{_captureDir}/td_{frame:0000}.png";
        GetViewport().GetTexture().GetImage().SavePng(path);
        var p = _world.Player.Position;
        var vp = GetViewport().GetViewportRid();
        GD.Print(string.Format(CultureInfo.InvariantCulture, "Captured {0}  pos {1:0.0},{2:0.0}  canopy {3}  {4:0} FPS  gpu {6:0.0} ms  {7}  sim {5:0.00} s", path, p.X, p.Y, _map.CanopyAt(p), Engine.GetFramesPerSecond(), _elapsed,
            RenderingServer.ViewportGetMeasuredRenderTimeGpu(vp), GetViewport().GetVisibleRect().Size));
        if (_world.Boss is { } boss)
            GD.Print(string.Format(CultureInfo.InvariantCulture, "  boss {0} {1} {2} ({8}) at {3:0.0},{4:0.0}  hp {5:0}/{6:0}  phase {7}  mass {9}  buds {10}", boss.Spec.Name, boss.Stage, boss.Act, boss.Position.X, boss.Position.Y, boss.Hp, boss.MaxHp, boss.Phase, boss.Next, boss.Mass, boss.BudDirs.Count));
        if (_captureFrames.Count == 0) GetTree().Quit();
    }
}
