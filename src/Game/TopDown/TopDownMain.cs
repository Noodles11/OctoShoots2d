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
/// The top-down game (DESIGN-TOPDOWN), current pass: generate a level from a seed, show it, and swim it. Opened from the
/// title screen (RunLaunch); run directly with verification flags, it starts a run on its own and saves nothing.
/// WASD swims (north is up), Space dashes (InputSetup), the mouse aims, F3 toggles the debug map, R regenerates from the
/// seed field.
/// Verification flags: --seed=, --depth=, --room=, --at=start|arch|cave|rift|gate|shop|cache|treasure|ambush|mob|boss,
/// --autopilot, --fire, --pearls=, --hp=, --boss-hp=, --paused, --map, --f3, --no-focus, --capture=dir --frames=a,b.
/// </summary>
public partial class TopDownMain : Node3D
{
    /// <summary>Every game starts on a random seed (--seed= pins one, for verification).</summary>
    SeedCode _seed = SeedCode.NewRandom();
    /// <summary>Rooms follow one another through the rift's gateway: room N is level N of this seed (a fresh layout).</summary>
    int _depth = 1, _room = 1;
    CombatView _combat = null!;
    BossView _boss = null!;
    BannerView _banner = null!;
    float? _bossHp;
    /// <summary>Verification: fire at the nearest mob; start the run with these pearls.</summary>
    bool _autoFire, _startPaused;
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
    bool _dashLatched, _rLatched, _f3Latched, _escLatched;
    PauseMenu _pause = null!;
    PanelContainer _debugPanel = null!;

    // The rift: fade to the splash, generate the next room off the main thread, fade back in.
    // On death: fade to the death splash, wait for the player, generate a new run's first room, fade back in.
    enum Transition { None, FadingIn, Loading, Waiting, FadingOut }
    const float FadeSeconds = 0.35f;
    /// <summary>The splash after the rift waits for Enter or a click once the next room is ready; the death splash
    /// waits first, then builds the new run.</summary>
    bool _deathSplash;
    bool _confirmLatched;
    /// <summary>The run so far, for the death splash.</summary>
    int _runMobs, _runShells;
    float _runTime;
    LoadingSplash _splash = null!;
    Transition _transition;
    float _transitionTime;
    Task<LevelMap>? _nextMap;
    /// <summary>Seconds spent in the current room (sim time).</summary>
    float _roomTime;
    /// <summary>The run's play time so far (sim time: still while paused or between rooms), for the HUD clock.</summary>
    float _elapsed;
    bool _fireBlocked;
    bool Paused => _pause.Visible;

    /// <summary>Opened from the title: the run is recorded in the profile and saved at the start of every room.</summary>
    bool _persist;
    /// <summary>A seed she chose (or a verification seed): such runs never earn achievements.</summary>
    bool _customSeed;
    /// <summary>A saved run to put her back into, at the start of its room.</summary>
    SuspendedRun? _resume;
    PlaneProfileRecorder _recorder = null!;

    // Verification mode.
    string? _startAt;
    bool _autopilot;
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
        _tuning = SettingsStore.LoadTuning();
        _catalog = LoadCatalog();
        // Keep the GPU cool: the frame-rate cap from the view options (60 by default; captures run uncapped).
        var view = SettingsStore.LoadView();
        Engine.MaxFps = _captureDir is null ? view.MaxFps : 0;
        BuildEnvironment();
        _level = new LevelView();
        AddChild(_level);
        _bell = new BellView();
        AddChild(_bell);
        _combat = new CombatView();
        AddChild(_combat);
        _boss = new BossView();
        AddChild(_boss);
        _camera = new CameraRig();
        AddChild(_camera);
        _snow = new MarineSnow(900, new Vector3(22f, 7f, 17f), 0.11f);
        AddChild(_snow);
        _camera.ShakeEnabled = view.CameraShake;
        _camera.SetReducedMotion(view.ReducedMotion);
        var sun = _sunLight = new SunLight();
        AddChild(sun);
        sun.SetStrength(view.SunLight);
        if (OS.GetCmdlineUserArgs().Contains("--dbg-noshadow"))
            foreach (var l in sun.FindChildren("*", "DirectionalLight3D", true, false)) ((DirectionalLight3D)l).ShadowEnabled = false;
        BuildUi();
        // From the title: the first room is shaped off the main thread behind the splash.
        if (_persist) StartFirstRoom();
        else Regenerate();
        if (_startPaused) CallDeferred(MethodName.TogglePause);
    }

    void ParseArgs()
    {
        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            string Value(string prefix) => arg[prefix.Length..];
            if (arg.StartsWith("--seed=") && SeedCode.TryParse(Value("--seed="), out var seed)) _seed = seed;
            else if (arg.StartsWith("--depth=") && int.TryParse(Value("--depth="), out int d)) _depth = Math.Clamp(d, 1, 7);
            else if (arg.StartsWith("--room=") && int.TryParse(Value("--room="), out int r)) _room = Math.Max(1, r);
            else if (arg.StartsWith("--at=")) _startAt = Value("--at=");
            else if (arg == "--autopilot") _autopilot = true;
            else if (arg.StartsWith("--boss-hp=") && float.TryParse(Value("--boss-hp="), NumberStyles.Float, CultureInfo.InvariantCulture, out float bhp)) _bossHp = bhp;
            else if (arg == "--fire") _autoFire = true;
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
            _depth = saved.Depth;
            _room = saved.Room;
            _resume = saved;
            return;
        }
        _seed = RunLaunch.Seed;
        _customSeed = mode == LaunchMode.Seeded;
    }

    /// <summary>Saves the run as it stands at the start of this room (continuing puts her back here).</summary>
    void SaveRun()
    {
        if (!_persist || _run is null) return;
        GameSave.Current.Run = new SuspendedRun
        {
            Seed = _seed.ToString(),
            CustomSeed = _customSeed,
            Depth = _depth,
            Room = _room,
            Items = _run.Items.ToList(),
            Hp = _run.Hp,
            Shells = _run.Shells,
            Elapsed = _elapsed,
            Foes = _runMobs,
            ShellsCollected = _runShells,
            SavedAt = DateTime.Now.ToString("s", CultureInfo.InvariantCulture),
        };
        GameSave.Write();
    }

    Task<LevelMap>? _firstMap;

    void StartFirstRoom()
    {
        var stats = new List<(string, string)> { ("Seed", _seed.ToString() + (_customSeed ? "  (seeded)" : "")) };
        if (_resume is { } saved) stats.Add(("Carrying", $"{saved.Items.Count} pearls · {saved.Shells} shells · {Mathf.CeilToInt(saved.Hp)} HP"));
        _splash.Fill(_resume is not null ? "Back into the sea" : "A new dive", stats, $"Depth {_depth} · Room {_room}");
        _splash.Modulate = Colors.White;
        _splash.Visible = true;
        var streams = new RunStreams(_seed);
        int depth = _depth, room = _room;
        _firstMap = Task.Run(() => TopDownGenerator.Generate(streams, depth, room));
    }

    void GoToTitle()
    {
        if (_persist) GameSave.Write();
        GetTree().ChangeSceneToFile(RunLaunch.TitleScene);
    }

    WorldEnvironment _environment = null!;
    MarineSnow _snow = null!;
    SunLight _sunLight = null!;

    void BuildEnvironment()
    {
        _environment = MakeEnvironment(ReefLook.For(_depth));
        AddChild(_environment);
    }

    /// <summary>The depth's look on the water, the sun and every reef shader.</summary>
    void ApplyLook()
    {
        var look = ReefLook.For(_depth);
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
        // Tab: the whole level on one sheet.
        _fullMap = new TopoMap { Visible = OS.GetCmdlineUserArgs().Contains("--map"), ShowLegend = true, MetresAcross = LevelMap.Size + 4f, ContourInterval = 1f };
        layer.AddChild(_fullMap);
        // ESC: pause, restart the run, see the pearls she has absorbed. Drawn above the rest of the HUD.
        var pauseLayer = new CanvasLayer { Layer = 5 };
        AddChild(pauseLayer);
        _pause = new PauseMenu();
        pauseLayer.AddChild(_pause);
        _pause.ResumePressed += Resume;
        _splash = new LoadingSplash();
        pauseLayer.AddChild(_splash);
        _pause.RestartPressed += () =>
        {
            // A new run is a new reef: a fresh random seed. The one she leaves counts as abandoned.
            Resume();
            _recorder.RunAbandoned(_elapsed);
            _seed = SeedCode.NewRandom();
            _customSeed = false;
            _room = 1;
            Regenerate();
            Say("New run");
        };
        // The run was saved at the start of this room: she resumes there.
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
            _room = 1;
            _seedField.Text = _seed.ToString();
            Regenerate();
        };
        row.AddChild(random);
        _info = new Label { Text = "" };
        box.AddChild(_info);
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
            if (!parsed.Equals(_seed)) _room = 1;
            _seed = parsed;
        }
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var map = TopDownGenerator.Generate(new RunStreams(_seed), _depth, _room);
        ApplyLevel(map, keepRun, watch.ElapsedMilliseconds);
    }

    /// <summary>Shows a generated level and puts Clementine at its start (keeping her run when she came through the rift).</summary>
    void ApplyLevel(LevelMap map, bool keepRun, long ms)
    {
        _map = map;
        _roomTime = 0f;
        ApplyLook();
        if (!keepRun || _run is null)
        {
            ResetRunTotals();
            _run = new PlaneRun(_catalog, _tuning);
            foreach (string id in _startPearls)
                if (_catalog?.Contains(id) == true) _run.Add(id);
            if (_startHp is { } hp) _run.Hp = hp;
            _startHp = null;
            bool continuing = _resume is not null;
            if (_resume is { } saved)
            {
                // Back where the save left her: what she carried into this room, and the run's totals so far.
                foreach (string id in saved.Items)
                    if (_catalog?.Contains(id) == true) _run.Add(id);
                _run.Hp = Mathf.Min(saved.Hp, _run.MaxHp);
                _run.Shells = saved.Shells;
                _elapsed = _runTime = (float)saved.Elapsed;
                _runMobs = saved.Foes;
                _runShells = saved.ShellsCollected;
                _resume = null;
            }
            _recorder.StartRun(_seed.ToString(), _customSeed, _run.Items, continuing);
        }
        _run.Room = _room;
        _world = new PlaneWorld(_map, _tuning, _run);
        _recorder.EnterRoom(_world, _depth, _room);
        SaveRun();
        _level.Show(_map);
        _combat.Show(_world, _catalog);
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
        _fullMap.Seen = _fog;
        _fullMap.Visited = _visited;
        _seedField.Text = _seed.ToString();
        PlaceAt(_startAt);
        // A verification start applies to the first room only (later rooms begin at their start).
        _startAt = null;
        _camera.Track(Focus(1f), 0f, snap: true);
        _route = _autopilot ? LevelValidator.ShortestPath(_map, _world.Player.Position, _map.Rift.Position, clearance: 1f) : new();
        _routeProgress = 0;
        GD.Print($"Top-down level {_seed} depth {_depth} room {_room}: attempt {_map.Attempt + 1}, {ms} ms, {_map.Pois.Count} POIs, {_map.Canopies.Count} canopy pieces, {_map.Decor.Count} decor");
    }

    /// <summary>Verification: start somewhere interesting.</summary>
    void PlaceAt(string? where)
    {
        System.Numerics.Vector2? at = where switch
        {
            "arch" => _map.Canopies.Where(c => c.Kind == CanopyKind.Arch).Select(c => (System.Numerics.Vector2?)c.Center).FirstOrDefault(),
            "cave" => _map.Caves.Select(c => (System.Numerics.Vector2?)(c.Mouth + c.Facing * 5f)).FirstOrDefault(),
            "rift" => _map.Rift.Position,
            "gate" => _map.Rift.Position + new System.Numerics.Vector2(0f, 6f),
            // Just outside Queen Clam's arena, in water that leads in.
            "boss" => Enumerable.Range(0, 24).Select(i => i * Mathf.Tau / 24f)
                .Select(a => (System.Numerics.Vector2?)(_map.Rift.Position + new System.Numerics.Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (_map.Rift.Radius + 2.5f)))
                .FirstOrDefault(q => q is { } v && _world.Clear(v, 0.8f) && _world.LineOfSight(v, _map.Rift.Position)),
            "shop" => _map.Caves.Where(c => _map.Pois[c.Poi].Kind == PoiKind.Shop).Select(c => (System.Numerics.Vector2?)(c.Mouth - c.Facing * 1.5f)).FirstOrDefault(),
            "cache" => _map.Pois.Where(p => p.Kind == PoiKind.ItemSpawn).Select(p => (System.Numerics.Vector2?)p.Position).FirstOrDefault(),
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
        return new Vector3(p.X, LevelMap.SwimBand, p.Y);
    }

    public override void _Process(double delta)
    {
        if (_world is null)
        {
            if (_firstMap is { IsCompleted: true } first)
            {
                _firstMap = null;
                if (first.IsFaulted) GD.PushError(first.Exception?.ToString());
                else
                {
                    ApplyLevel(first.Result, keepRun: false, 0);
                    _transition = Transition.FadingOut;
                    _transitionTime = 0f;
                }
            }
            return;
        }
        float dt = (float)delta;
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
        bool gateway = false;
        while (_accumulator >= PlaneWorld.Dt && steps < 8 && !gateway)
        {
            _world.Step(ReadInput());
            _recorder.Observe(_world);
            _roomTime += PlaneWorld.Dt;
            _elapsed += PlaneWorld.Dt;
            _accumulator -= PlaneWorld.Dt;
            steps++;
            foreach (var e in _world.Events)
            {
                if (e.Type == PlaneEventType.GatewayEntered) gateway = true;
                else if (e.Type == PlaneEventType.PlayerDefeated) gateway = false;
                else if (e.Type == PlaneEventType.PearlCollected && _world.LastPearl is { } pearl) _hud.ShowPearl(pearl);
                else if (e.Type == PlaneEventType.ShellCollected) _hud.PulseShells();
                else if (e.Type == PlaneEventType.CannotAfford) Say("Not enough shells");
                else if (e.Type == PlaneEventType.AmbushSprung) Say("Ambush!");
                else if (e.Type == PlaneEventType.ShotPopped) _combat.Pop(e.Position, e.Size * 1.7f);
                else if (e.Type == PlaneEventType.BossLanded) _camera.Shake(0.9f);
                else if (e.Type == PlaneEventType.BossStagger) _camera.Shake(0.45f);
                else if (e.Type == PlaneEventType.BossSnap) _camera.Shake(0.35f);
                else if (e.Type == PlaneEventType.BossFreed)
                {
                    _camera.Shake(0.4f);
                    Say($"{PlaneBossTuning.Name} is freed! The rift is open");
                }
            }
        }
        // Through the gateway: the splash fades in over the room she leaves; the next one is generated behind it.
        if (gateway)
        {
            BeginRift();
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
        _boss.Sync(_world, dt);
        if (!OS.GetCmdlineUserArgs().Contains("--dbg-nobanner")) _banner.Sync(_world.Boss);
        if (_world.Boss is { } b) _minimap.SetMud(_world.ArenaCenter, _world.ArenaRadius, b.Cloud);
        _hud.Elapsed = _elapsed;
        _hud.Track(_world, _catalog, dt);
        _camera.Track(Focus(alpha), dt);
        _snow.Tick(dt, Focus(alpha) + Vector3.Up * 6f, _camera.Camera.GlobalBasis);
        _level.UpdateCanopy(_world.Player.Position, dt);
        var p = _world.Player;
        _debugMap.SetPlayer(_world.Player.Position);
        _minimap.Track(p.Position, p.Velocity);
        _fog.Reveal(p.Position, MinimapView.RevealRadius, MinimapView.RevealSoft);
        for (int i = 0; i < _map.Pois.Count; i++)
            if (System.Numerics.Vector2.Distance(_map.Pois[i].Position, p.Position) <= MinimapView.SpotRadius) _spotted.Add(i);
        // Entering a place names it under the minimap.
        var inside = _map.Pois.Where(poi => System.Numerics.Vector2.Distance(poi.Position, p.Position) <= poi.Radius + 1.5f)
            .OrderBy(poi => System.Numerics.Vector2.Distance(poi.Position, p.Position)).FirstOrDefault();
        // The start needs no name under the map.
        bool named = inside is not null && inside.Kind != PoiKind.Start;
        _minimap.ShowPlace(named ? LevelView.PlaceName(inside!.Kind) : null, named ? LevelView.PoiColor(inside!.Kind) : Colors.White);
        if (inside is not null) _visited.Add(_map.Pois.IndexOf(inside));
        float side = Mathf.Min(GetViewport().GetVisibleRect().Size.X, GetViewport().GetVisibleRect().Size.Y) - 80f;
        _fullMap.Size = new Vector2(side, side);
        _fullMap.Position = (GetViewport().GetVisibleRect().Size - _fullMap.Size) * 0.5f;
        _fullMap.Player = p.Position;
        if (p.Velocity.LengthSquared() > 0.04f) _fullMap.PlayerHeading = p.Velocity;
        int left = _world.Mobs.Count(m => m.Alive);
        _info.Text = $"Room {_room} · mobs {left}/{_world.Mobs.Count} · pearls {_run!.Items.Count}   attempt {_map.Attempt + 1}   pos {p.Position.X:0.0}, {p.Position.Y:0.0}   " +
                     $"{Engine.GetFramesPerSecond():0} FPS" + (_statusTimer > 0f ? $"   {_status}" : "") +
                     "\nWASD swim · Space dash · hold the mouse or arrows to shoot · Tab map · Esc pause · F3 debug · R regenerate · the rift's gateway leads on";
        Capture();
    }

    void BeginRift()
    {
        _accumulator = 0;
        var st = _world.Stats;
        int places = _map.Pois.Count;
        var time = TimeSpan.FromSeconds(_roomTime);
        var stats = new List<(string, string)>
        {
            ("Time", $"{(int)time.TotalMinutes}:{time.Seconds:00}"),
            ("Foes defeated", $"{st.MobsDefeated} / {_world.Mobs.Count}"),
            ("Shells collected", $"{st.ShellsCollected}" + (st.ShellsSpent > 0 ? $"  (spent {st.ShellsSpent})" : "")),
            ("Pearls found", $"{st.PearlsFound}"),
            ("Places visited", $"{_visited.Count} / {places}"),
            ("Damage taken", $"{st.DamageTaken:0}"),
            ("Carrying", $"{_run!.Items.Count} pearls · {_run.Shells} shells · {Mathf.CeilToInt(_world.Player.Hp)} / {Mathf.RoundToInt(_run.MaxHp)} HP"),
        };
        _recorder.RoomCleared(_world, _roomTime);
        AddToRun();
        _room++;
        _deathSplash = false;
        _splash.Fill($"Room {_room - 1} cleared", stats, $"Into the rift — Depth {_depth} · Room {_room}");
        OpenSplash();
    }

    /// <summary>Adds the room she is leaving to the run's totals.</summary>
    void AddToRun()
    {
        _runMobs += _world.Stats.MobsDefeated;
        _runShells += _world.Stats.ShellsCollected;
        _runTime += _roomTime;
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
        // The run is over: nothing to continue.
        if (_persist)
        {
            GameSave.Current.Run = null;
            GameSave.Write();
        }
        var time = TimeSpan.FromSeconds(_runTime);
        var stats = new List<(string, string)>
        {
            ("Reached", $"Depth {_depth} · Room {_room}"),
            ("Rooms cleared", $"{_room - 1}"),
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

    /// <summary>Enter or a click, as a press (a key or button already held when the prompt appears does not count).</summary>
    bool Confirmed()
    {
        bool down = Input.IsPhysicalKeyPressed(Key.Enter) || Input.IsPhysicalKeyPressed(Key.KpEnter) || Input.IsMouseButtonPressed(MouseButton.Left)
                    // Captures go on by themselves after a moment.
                    || _captureDir is not null && _transitionTime > 2f;
        bool pressed = down && !_confirmLatched;
        _confirmLatched = down;
        return pressed;
    }

    void StartLoading(int room)
    {
        var streams = new RunStreams(_seed);
        int depth = _depth;
        _nextMap = Task.Run(() => TopDownGenerator.Generate(streams, depth, room));
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
                    _splash.Prompt(_persist ? "Enter or click: a new run  ·  Esc: back to the title" : "Press Enter or click to start a new run");
                    _confirmLatched = true;
                    _transition = Transition.Waiting;
                    _transitionTime = 0f;
                }
                // The generator is pure and deterministic: it runs off the main thread while the splash shows.
                else StartLoading(_room);
                break;
            case Transition.Loading:
                if (_nextMap is null || !_nextMap.IsCompleted) return;
                var task = _nextMap;
                _nextMap = null;
                if (task.IsFaulted) GD.PushError(task.Exception?.ToString());
                else ApplyLevel(task.Result, keepRun: !_deathSplash, (long)(_transitionTime * 1000f));
                if (_deathSplash)
                {
                    // A new run: straight in.
                    Say($"New run · seed {_seed}");
                    _transition = Transition.FadingOut;
                }
                else
                {
                    // The next room is ready: wait until the player has read the page.
                    _splash.Prompt("Press Enter or click to continue");
                    _confirmLatched = true;
                    _transition = Transition.Waiting;
                }
                _transitionTime = 0f;
                break;
            case Transition.Waiting:
                if (_deathSplash && _persist && Input.IsPhysicalKeyPressed(Key.Escape))
                {
                    GoToTitle();
                    return;
                }
                if (!Confirmed()) return;
                // The click that closes the splash must not fire a shot.
                _fireBlocked = true;
                if (_deathSplash)
                {
                    // A clean new game: new seed, room 1, no pearls or shells.
                    _seed = SeedCode.NewRandom();
                    _customSeed = false;
                    _room = 1;
                    ResetRunTotals();
                    _splash.Working("Shaping a new reef");
                    StartLoading(_room);
                }
                else
                {
                    Say($"Depth {_depth} · Room {_room}");
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
        else _pause.Open(_world.Run, _catalog, _seed.ToString(), _room, _world.Player.Hp);
    }

    void Resume()
    {
        _pause.Close();
        // The click that closed the menu must not fire a shot.
        _fireBlocked = true;
    }

    void HandleKeys()
    {
        bool esc = Input.IsPhysicalKeyPressed(Key.Escape);
        if (esc && !_escLatched)
        {
            if (_seedField.HasFocus()) _seedField.ReleaseFocus();
            else TogglePause();
        }
        _escLatched = esc;
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
        if (!OS.GetCmdlineUserArgs().Contains("--map")) _fullMap.Visible = Input.IsPhysicalKeyPressed(Key.Tab) && !typing;
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
            return input;
        }
        if (_seedField.HasFocus()) return input;
        var move = System.Numerics.Vector2.Zero;
        // Movement and dash come from the shared bindings (InputSetup).
        if (Input.IsActionPressed(InputSetup.Forward)) move.Y -= 1f;
        if (Input.IsActionPressed(InputSetup.Back)) move.Y += 1f;
        if (Input.IsActionPressed(InputSetup.Left)) move.X -= 1f;
        if (Input.IsActionPressed(InputSetup.Right)) move.X += 1f;
        input.Move = move;
        bool dash = Input.IsActionPressed(InputSetup.Dash);
        input.Dash = dash && !_dashLatched;
        _dashLatched = dash;
        if (_autoFire && _world.Boss is { Stage: BossStage.Fight } queen)
        {
            input.Aim = System.Numerics.Vector2.Normalize(queen.Position - _world.Player.Position);
            input.Fire = true;
            return input;
        }
        if (_autoFire && _world.Mobs.Where(m => m.Alive).OrderBy(m => System.Numerics.Vector2.Distance(m.Position, _world.Player.Position)).FirstOrDefault() is { } target)
        {
            input.Aim = System.Numerics.Vector2.Normalize(target.Position - _world.Player.Position);
            input.Fire = true;
            return input;
        }
        // Shooting: arrow keys aim and fire; otherwise the held left mouse button fires toward the pointer.
        var arrows = System.Numerics.Vector2.Zero;
        if (Input.IsPhysicalKeyPressed(Key.Up)) arrows.Y -= 1f;
        if (Input.IsPhysicalKeyPressed(Key.Down)) arrows.Y += 1f;
        if (Input.IsPhysicalKeyPressed(Key.Left)) arrows.X -= 1f;
        if (Input.IsPhysicalKeyPressed(Key.Right)) arrows.X += 1f;
        if (arrows.LengthSquared() > 0f)
        {
            input.Aim = System.Numerics.Vector2.Normalize(arrows);
            input.Fire = true;
        }
        else
        {
            input.Aim = MouseAim();
            bool held = Input.IsMouseButtonPressed(MouseButton.Left);
            if (!held) _fireBlocked = false;
            input.Fire = held && !_fireBlocked;
        }
        return input;
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
        GD.Print(string.Format(CultureInfo.InvariantCulture, "Captured {0}  pos {1:0.0},{2:0.0}  canopy {3}  {4:0} FPS  sim {5:0.00} s", path, p.X, p.Y, _map.CanopyAt(p), Engine.GetFramesPerSecond(), _elapsed));
        if (_captureFrames.Count == 0) GetTree().Quit();
    }
}
