using System.Collections.Generic;
using System.Linq;
using Godot;
using OctoShoots.Core;
using OctoShoots.Core.Items;
using OctoShoots.Core.Run;
using OctoShoots.Core.Saves;
using OctoShoots.Core.Sim;
using OctoShoots.Core.Terrain;
using OctoShoots.Game.Controls;
using OctoShoots.Game.Enemies;
using OctoShoots.Game.Fx;
using OctoShoots.Game.Player;
using OctoShoots.Game.Settings;
using OctoShoots.Game.Terrain;
using OctoShoots.Game.UI;
using OctoShoots.Game.Util;

namespace OctoShoots.Game.Scenes;

/// <summary>
/// The grey-box cave scene (M0 feel prototype + step 2 items). Runs the Core simulation at a fixed
/// 60 Hz, draws it, and turns simulation events into camera, viewmodel, sound, FX and HUD feedback.
/// Events live in Main.Events.cs; the debug panel, saves and verification mode in Main.Debug.cs.
/// </summary>
public partial class Main : Node3D
{
    const int MaxStepsPerFrame = 5;
    const float RestartHoldTime = 0.6f;
    const float BaseGlowRange = 11f;

    Tuning _tuning = null!;
    ViewOptions _view = null!;
    ItemCatalog _catalog = null!;
    SaveFile _save = null!;
    SeedCode _seed = SeedCode.FromValue(20261002);
    Cave _cave = null!;
    World _world = null!;

    TerrainView _terrain = null!;
    LevelFx _levelFx = null!;
    AnemoneViews _anemones = null!;
    FloraViews _flora = null!;
    SunLight _sun = null!;
    ClownfishViews _fish = null!;
    CreatureViews _creatureViews = null!;
    CreatureCatalog _creatures = null!;
    PlayerCamera _camera = null!;
    ProjectileViews _shots = null!;
    FxParticles _ink = null!;
    FxParticles _sparks = null!;
    MarineSnow _snow = null!;
    InkStains _stains = null!;
    LineFx _lines = null!;
    Sfx _sfx = null!;
    Hud _hud = null!;
    DebugPanel _panel = null!;
    ShaderMaterial _screenFx = null!;
    Label _pauseLabel = null!;
    readonly Dictionary<int, LanternfishView> _enemyViews = new();

    double _accumulator;
    float _yaw, _pitch;
    bool _dashLatched;
    bool _activeLatched;
    bool _bombLatched;
    bool _debugModeStarted;
    float _time;
    bool _paused;
    bool _panelOpen;
    bool _reticleOnEnemy;
    float _restartHeld;
    float _hurtPulse;
    float _trailTimer;

    public override void _Ready()
    {
        ParseArgs();
        InputSetup.Register();
        _tuning = SettingsStore.LoadTuning();
        if (_lanternOverride is { } lanterns) _tuning.EnemyCount = lanterns;
        _view = SettingsStore.LoadView();
        RenderingServer.ViewportSetMeasureRenderTime(GetViewport().GetViewportRid(), true);
        _catalog = LoadCatalog();
        _creatures = LoadCreatures();
        _save = SaveStore.Load();

        BuildEnvironment();
        _terrain = new TerrainView();
        AddChild(_terrain);
        _levelFx = new LevelFx();
        AddChild(_levelFx);
        _anemones = new AnemoneViews();
        AddChild(_anemones);
        _flora = new FloraViews();
        AddChild(_flora);
        _fish = new ClownfishViews();
        AddChild(_fish);
        _creatureViews = new CreatureViews();
        AddChild(_creatureViews);

        _camera = new PlayerCamera();
        AddChild(_camera);
        _shots = new ProjectileViews();
        AddChild(_shots);
        _ink = new FxParticles(1024, false);
        AddChild(_ink);
        _sparks = new FxParticles(768, true);
        AddChild(_sparks);
        _snow = new MarineSnow();
        AddChild(_snow);
        _stains = new InkStains();
        AddChild(_stains);
        _lines = new LineFx();
        AddChild(_lines);
        _sfx = new Sfx();
        AddChild(_sfx);
        BuildSea();
        BuildUi();

        CaptureMouse(true);
        BeginLoad();
    }

    static ItemCatalog LoadCatalog()
    {
        using var file = FileAccess.Open("res://data/items.json", FileAccess.ModeFlags.Read);
        var catalog = ItemCatalog.FromJson(file.GetAsText());
        GD.Print($"Items: {catalog.Items.Count} items, {catalog.Synergies.Count} synergies, {catalog.Transformations.Count} transformations");
        return catalog;
    }

    static CreatureCatalog LoadCreatures()
    {
        using var file = FileAccess.Open("res://data/creatures.json", FileAccess.ModeFlags.Read);
        var catalog = CreatureCatalog.FromJson(file.GetAsText());
        GD.Print($"Creatures: {CreatureCatalog.Kinds.Length} kinds");
        return catalog;
    }

    void BuildEnvironment()
    {
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = MistColor,
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.35f, 0.6f, 0.7f),
            AmbientLightEnergy = 0.38f,
            TonemapMode = Godot.Environment.ToneMapper.Aces,
            TonemapExposure = 1.0f,
            FogEnabled = true,
            FogLightColor = MistColor,
            FogDensity = 0.012f,
            FogSkyAffect = 1f,
            GlowEnabled = true,
            GlowIntensity = 0.9f,
            GlowBloom = 0.08f,
            GlowHdrThreshold = 1f,
            GlowBlendMode = Godot.Environment.GlowBlendModeEnum.Additive,
        };
        AddChild(new WorldEnvironment { Environment = env });

        // The sun: its light, shadows, caustics and god rays all share one direction (SunLight, sunlight.gdshaderinc).
        _sun = new SunLight();
        AddChild(_sun);
    }

    void BuildUi()
    {
        var fxLayer = new CanvasLayer { Layer = 1 };
        AddChild(fxLayer);
        _screenFx = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/screen_fx.gdshader") };
        var fxRect = new ColorRect { Material = _screenFx, MouseFilter = Control.MouseFilterEnum.Ignore };
        fxRect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        fxLayer.AddChild(fxRect);

        var hudLayer = new CanvasLayer { Layer = 2 };
        AddChild(hudLayer);
        _hud = new Hud();
        hudLayer.AddChild(_hud);
        _pauseLabel = new Label
        {
            Text = "PAUSED\n\nEsc  resume\nF1  debug panel (items, saves, tuning)\nHold R  restart",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visible = false,
        };
        _pauseLabel.AddThemeFontSizeOverride("font_size", 32);
        _pauseLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        hudLayer.AddChild(_pauseLabel);
        _loadingLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visible = false,
        };
        _loadingLabel.AddThemeFontSizeOverride("font_size", 36);
        _loadingLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        hudLayer.AddChild(_loadingLabel);

        var panelLayer = new CanvasLayer { Layer = 3 };
        AddChild(panelLayer);
        _panel = new DebugPanel { Visible = false };
        panelLayer.AddChild(_panel);
        BuildPanel();
    }

    /// <summary>A fresh world on clean terrain (regenerated if the seed or reef changed). Items are carried over.</summary>
    void Restart() => BeginLoad();

    // ───────────────────────── input ─────────────────────────

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventMouseMotion motion && Godot.Input.MouseMode == Godot.Input.MouseModeEnum.Captured && !_paused && !_panelOpen)
        {
            float sensitivity = _view.MouseSensitivity * Mathf.Pi / 180f;
            if (_reticleOnEnemy) sensitivity *= 1f - _tuning.AimProfile.Slowdown;
            _yaw -= motion.ScreenRelative.X * sensitivity;
            _pitch -= motion.ScreenRelative.Y * sensitivity * (_view.InvertY ? -1f : 1f);
            float limit = _tuning.PitchLimit * Mathf.Pi / 180f;
            _pitch = Mathf.Clamp(_pitch, -limit, limit);
            _yaw = Mathf.Wrap(_yaw, -Mathf.Pi, Mathf.Pi);
        }
        else if (e.IsActionPressed(InputSetup.Pause))
        {
            if (_panelOpen) TogglePanel();
            else TogglePause();
        }
        else if (e.IsActionPressed(InputSetup.DebugPanel))
        {
            TogglePanel();
        }
        else if (e.IsActionPressed(InputSetup.Dash))
        {
            _dashLatched = true;
        }
        else if (e.IsActionPressed(InputSetup.Active))
        {
            _activeLatched = true;
        }
        else if (e.IsActionPressed(InputSetup.Bomb))
        {
            _bombLatched = true;
        }
        else if (e.IsActionPressed(InputSetup.Review))
        {
            _reviewing = true;
        }
        else if (e.IsActionReleased(InputSetup.Review))
        {
            _reviewing = false;
        }
        else if (_reviewing && e is InputEventMouseButton { Pressed: true } wheel && wheel.ButtonIndex is MouseButton.WheelDown or MouseButton.WheelUp)
        {
            CycleReview(wheel.ButtonIndex == MouseButton.WheelDown ? 1 : -1);
        }
        else if (e is InputEventMouseButton { Pressed: true } && Godot.Input.MouseMode != Godot.Input.MouseModeEnum.Captured && !_paused && !_panelOpen)
        {
            CaptureMouse(true);
        }
    }

    void TogglePause()
    {
        _paused = !_paused;
        _pauseLabel.Visible = _paused;
        CaptureMouse(!_paused);
    }

    void TogglePanel()
    {
        _panelOpen = !_panelOpen;
        _panel.Visible = _panelOpen;
        if (_panelOpen) _panel.Refresh();
        CaptureMouse(!_panelOpen && !_paused);
    }

    static void CaptureMouse(bool capture) =>
        Godot.Input.MouseMode = capture ? Godot.Input.MouseModeEnum.Captured : Godot.Input.MouseModeEnum.Visible;

    PlayerInput BuildInput()
    {
        var input = new PlayerInput
        {
            Yaw = _yaw,
            Pitch = _pitch,
            ViewHalfAngleDeg = VerticalHalfFovDeg(),
        };
        if (_autopilot) return Autopilot(input);
        bool active = !_paused && !_panelOpen && Godot.Input.MouseMode == Godot.Input.MouseModeEnum.Captured;
        if (!active) return input;

        input.Forward = Godot.Input.GetActionStrength(InputSetup.Forward) - Godot.Input.GetActionStrength(InputSetup.Back);
        input.Strafe = Godot.Input.GetActionStrength(InputSetup.Right) - Godot.Input.GetActionStrength(InputSetup.Left);
        input.Vertical = Godot.Input.GetActionStrength(InputSetup.Rise) - Godot.Input.GetActionStrength(InputSetup.Descend);
        // The inventory tentacle is busy while she examines it.
        input.Fire = Godot.Input.IsActionPressed(InputSetup.Fire) && !_reviewing;
        input.Dash = _dashLatched;
        input.UseActive = _activeLatched;
        input.DropBomb = _bombLatched;
        return input;
    }

    float VerticalHalfFovDeg()
    {
        Vector2 size = GetViewport().GetVisibleRect().Size;
        float aspect = size.X / Mathf.Max(size.Y, 1f);
        float hHalf = _view.Fov * 0.5f * Mathf.Pi / 180f;
        return Mathf.Atan(Mathf.Tan(hHalf) / aspect) * 180f / Mathf.Pi;
    }

    // ───────────────────────── frame ─────────────────────────

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (PollLoad() || _world is null) return;
        if (!_debugModeStarted)
        {
            _debugModeStarted = true;
            StartDebugMode();
        }
        HandleRestartHold(dt);
        if (_ninjaCam) FollowNinja();
        _time += dt;

        _prof.Restart();
        if (!_paused)
        {
            _accumulator += delta;
            int steps = 0;
            while (_accumulator >= World.Dt && steps < MaxStepsPerFrame)
            {
                _world.Step(BuildInput());
                _dashLatched = false;
                _activeLatched = false;
                _bombLatched = false;
                HandleEvents();
                _accumulator -= World.Dt;
                steps++;
            }
            if (steps == MaxStepsPerFrame) _accumulator = 0;
        }

        Mark("sim");
        float alpha = _paused ? 1f : (float)(_accumulator / World.Dt);
        var p = _world.Player;
        var stats = _world.Loadout.Stats;
        float speed01 = p.Velocity.Length() / _world.SwimSpeed;
        // Extrapolate the eye from the latest tick instead of interpolating behind it: no added frame of input lag.
        Vector3 eye = (p.Position + p.Velocity * (alpha * World.Dt)).G();

        float frameDt = _paused ? 0f : dt;
        _camera.Viewmodel.SetBubbles(p.Bubbles, _world.BubbleCapacity, p.RegrowDelay > 0f ? 0f : p.RegrowProgress);
        _camera.Tick(frameDt, eye, _yaw, _pitch, speed01, _view);
        _camera.Glow.OmniRange = BaseGlowRange * stats[Stat.Glow];
        var camXform = _camera.Camera.GlobalTransform;

        _reticleOnEnemy = _tuning.AimAssist != AimAssistLevel.Off &&
            _world.ReticleOverEnemy(p.Position, MathUtil.Forward(_yaw, _pitch));

        Mark("camera+reticle");
        SyncEnemies(alpha, frameDt);
        _fish.Sync(_world, alpha, frameDt);
        _creatureViews.Sync(_world, camXform.Origin, alpha, frameDt);
        _shots.SetColorCycle(_world.Loadout.Flags.Contains("colorCycle"), frameDt);
        _shots.Sync(_world, alpha, _ink, frameDt);
        _levelFx.SyncBombs(_world, alpha, _time);
        Mark("views");
        TickSea(alpha, frameDt);
        Mark("sea");
        InkTrail(frameDt);
        _ink.Tick(frameDt, camXform.Basis);
        _sparks.Tick(frameDt, camXform.Basis);
        _sun.SetStrength(_view.SunLight);
        // Cap the frame rate (and drop it right down while the window is in the background) so the GPU never runs flat out.
        Engine.MaxFps = !GetWindow().HasFocus() && _captureDir is null ? 15 : _uncapped ? 0 : _view.MaxFps;
        _snow.Tick(frameDt, camXform);
        _lines.Tick(frameDt);
        Mark("fx");

        _hurtPulse = Mathf.MoveToward(_hurtPulse, 0f, frameDt * 2.2f);
        _screenFx.SetShaderParameter("hurt", _hurtPulse);
        _screenFx.SetShaderParameter("aberration", _hurtPulse * 1.2f);
        bool shielded = p.ShieldTimer > 0f;
        _screenFx.SetShaderParameter("tint", shielded || p.HiddenTimer > 0f ? 1f : 0f);
        _screenFx.SetShaderParameter("tint_color", shielded ? new Color(0.4f, 0.95f, 1f) : new Color(0.02f, 0.01f, 0.05f));

        var active = _world.Loadout.Active;
        _hud.SetState(new HudState
        {
            Hp = p.Hp,
            MaxHp = stats.MaxHp,
            Foam = p.Foam,
            Dash01 = 1f - Mathf.Clamp(p.DashCooldownTimer / (_tuning.DashCooldown * stats[Stat.DashCooldown]), 0f, 1f),
            OnTarget = _reticleOnEnemy,
            Charge01 = p.Charge,
            Shielded = shielded,
            ActiveName = active?.Name,
            ActiveCharge = _world.ActiveCharge,
            ActiveMax = _world.ActiveMaxCharge,
            Coins = _world.Coins,
            Bombs = _world.Bombs,
            Status = StatusLine(),
            Items = ItemLine(),
        });
        _hud.SetMarkers(DangerMarkers(camXform));
        _hud.Tick(frameDt);

        if (_panelOpen) _panel.SetReadout(Readout());
        Capture();
    }

    // Frame profile, printed every 2 s with `-- --profile`.
    readonly System.Diagnostics.Stopwatch _prof = new();
    readonly Dictionary<string, double> _profTotals = new();
    int _profFrames;
    bool _profile;

    void Mark(string section)
    {
        if (!_profile) return;
        _profTotals[section] = _profTotals.GetValueOrDefault(section) + _prof.Elapsed.TotalMilliseconds;
        _prof.Restart();
        if (section != "fx" || ++_profFrames < 120) return;
        GD.Print("frame ms: " + string.Join("  ", _profTotals.Select(kv => $"{kv.Key} {kv.Value / _profFrames:0.00}")));
        _profTotals.Clear();
        _profFrames = 0;
    }

    /// <summary>Kraken Form: Clementine leaves a trail of ink puffs while she swims.</summary>
    void InkTrail(float dt)
    {
        if (!_world.Loadout.Flags.Contains("inkTrail") || _world.Player.Velocity.Length() < 1f) return;
        _trailTimer -= dt;
        if (_trailTimer > 0f) return;
        _trailTimer = 0.06f;
        Vector3 at = _world.Player.Position.G() - _world.Player.Velocity.G().Normalized() * 0.6f + Vector3.Down * 0.25f;
        _ink.Emit(at, _ink.RandDir() * 0.2f, 1.6f, 0.15f, 0.7f, new Color(0.05f, 0.04f, 0.11f, 0.5f), new Color(0.05f, 0.04f, 0.11f, 0f), 1.5f, 0.1f);
    }

    void HandleRestartHold(float dt)
    {
        if (!_paused && !_panelOpen && Godot.Input.IsActionPressed(InputSetup.Restart))
        {
            _restartHeld += dt;
            if (_restartHeld >= RestartHoldTime)
            {
                _restartHeld = float.NegativeInfinity;
                Restart();
            }
        }
        else
        {
            _restartHeld = 0f;
        }
    }

    string StatusLine() =>
        $"{WhereAmI()} · seed {_seed} · deaths {_world.Player.Deaths}    F item · E bomb · F1 debug · Esc pause · hold R restart";

    string ItemLine()
    {
        if (_world.Items.Count == 0) return "";
        var names = _world.Items.Select(id => _catalog[id].Name);
        var combos = _world.Loadout.Synergies.Concat(_world.Loadout.Transformations).Select(ComboName);
        return string.Join(" · ", names) + (combos.Any() ? "   ★ " + string.Join(" · ", combos) : "");
    }

    string ComboName(string id) =>
        _catalog.Synergies.FirstOrDefault(s => s.Id == id)?.Name ?? _catalog.Transformations.FirstOrDefault(t => t.Id == id)?.Name ?? id;

    void SyncEnemies(float alpha, float dt)
    {
        var seen = new HashSet<int>();
        foreach (var e in _world.Enemies)
        {
            if (e.Kind != EnemyKind.Lanternfish) continue; // clownfish ninjas have their own views
            seen.Add(e.Id);
            if (!_enemyViews.TryGetValue(e.Id, out var view))
            {
                view = new LanternfishView();
                AddChild(view);
                _enemyViews[e.Id] = view;
            }
            view.Sync(e, alpha, _tuning.EnemyTelegraph, dt);
        }

        var gone = new List<int>();
        foreach (var id in _enemyViews.Keys) if (!seen.Contains(id)) gone.Add(id);
        foreach (var id in gone)
        {
            _enemyViews[id].QueueFree();
            _enemyViews.Remove(id);
        }
    }

    /// <summary>Telegraphing creatures and incoming orbs that are outside the view.</summary>
    List<Hud.DangerMarker> DangerMarkers(Transform3D cam)
    {
        var markers = new List<Hud.DangerMarker>();
        var camera = _camera.Camera;
        Rect2 screen = GetViewport().GetVisibleRect();
        Vector3 playerPos = _world.Player.Position.G();

        void Consider(Vector3 worldPos, float intensity)
        {
            bool onScreen = !camera.IsPositionBehind(worldPos) && screen.Grow(-20f).HasPoint(camera.UnprojectPosition(worldPos));
            if (onScreen) return;
            Vector3 local = cam.Basis.Inverse() * (worldPos - cam.Origin);
            var dir = new Vector2(local.X, -local.Y);
            if (dir.LengthSquared() < 1e-6f) dir = Vector2.Down;
            markers.Add(new Hud.DangerMarker(dir, intensity));
        }

        foreach (var e in _world.Enemies)
        {
            if (e.State != EnemyState.Telegraph) continue;
            float charge = 1f - Mathf.Clamp(e.StateTimer / _world.TelegraphTime(e), 0f, 1f);
            Consider(e.Position.G(), 0.5f + 0.5f * charge);
        }
        foreach (var s in _world.Projectiles)
        {
            if (s.FromPlayer) continue;
            Vector3 pos = s.Position.G();
            Vector3 toPlayer = playerPos - pos;
            if (toPlayer.Length() > 10f || toPlayer.Dot(s.Velocity.G()) <= 0f) continue;
            Consider(pos, 0.8f);
        }
        return markers;
    }
}
