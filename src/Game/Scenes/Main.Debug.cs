using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using OctoShoots.Core;
using OctoShoots.Core.Gen;
using OctoShoots.Core.Items;
using OctoShoots.Core.Run;
using OctoShoots.Core.Saves;
using OctoShoots.Core.Sim;
using OctoShoots.Game.Settings;
using OctoShoots.Game.Util;

namespace OctoShoots.Game.Scenes;

/// <summary>F1 debug panel wiring, run saves, and the verification mode used for screenshots.</summary>
public partial class Main
{
    // Verification mode: `-- --autopilot --capture=<dir> --frames=60,240 --items=a,b --panel`
    // swims and shoots by itself, saves screenshots at those frames, then quits.
    bool _autopilot;
    bool _openPanel;
    ChamberRole? _startAt;
    bool _startNest;
    bool _uncapped;
    Vector2? _look;
    EnemyKind? _startDen;
    int _denIndex;
    bool _denBed;
    bool _ninjaCam;
    int? _lanternOverride;
    string? _captureDir;
    readonly List<string> _startItems = new();
    readonly Queue<int> _captureFrames = new();
    int _frame;
    int _benchFrames;

    void BuildPanel()
    {
        _panel.Build("Debug  (F1 to close)");
        _panel.Changed += () => _world.RefreshLoadout();

        _panel.AddButtons(
            ("Save tuning", () =>
            {
                SettingsStore.Save(_tuning, _view);
                Notice("Tuning saved", SettingsStore.GlobalTuningPath);
            }),
            ("Defaults", () =>
            {
                CopyFields(new Tuning(), _tuning);
                CopyFields(new ViewOptions(), _view);
                _world.RefreshLoadout();
                _panel.Refresh();
            }),
            ("Restart", Restart));

        _panel.AddSection("Level");
        _panel.AddOptions("Level", new[] { "Reef", "Grey-box cave" }, () => (int)_levelKind, i => _levelKind = (LevelKind)i);
        _panel.AddOptions("Depth", new[] { "1", "2", "3", "4", "5", "6" }, () => _depth - 1, i => _depth = i + 1);
        _panel.AddOptions("Reef", new[] { "1 of 3", "2 of 3", "3 of 3" }, () => _reefIndex - 1, i => _reefIndex = i + 1);
        _panel.AddButtons(("Generate / reset", Restart), ("+5 ink bombs", () => _world?.AddBombs(5)), ("+10 sand dollars", () => _world?.AddCoins(10)));
        _panel.AddButtons(
            ("Start", () => TeleportTo(ChamberRole.Start)),
            ("Treasure", () => TeleportTo(ChamberRole.Treasure)),
            ("Shop", () => TeleportTo(ChamberRole.Shop)));
        _panel.AddButtons(
            ("Curse", () => TeleportTo(ChamberRole.Curse)),
            ("Secret", () => TeleportTo(ChamberRole.Secret)),
            ("Boss arena", () => TeleportTo(ChamberRole.Boss)));
        _panel.AddButtons(("Nearest nest", TeleportToNest));
        _panel.AddButtons(
            ("Dancer", () => TeleportToDen(EnemyKind.SpanishDancer)),
            ("Pufferling", () => TeleportToDen(EnemyKind.Pufferling)),
            ("Barracuda", () => TeleportToDen(EnemyKind.Barracuda)),
            ("Jellies", () => TeleportToDen(EnemyKind.MoonJelly)));
        _panel.AddButtons(
            ("Urchin", () => TeleportToDen(EnemyKind.SeaUrchin)),
            ("Crabby", () => TeleportToDen(EnemyKind.Crabby)),
            ("Moray", () => TeleportToDen(EnemyKind.Moray)),
            ("Urchin bed", () => TeleportToDen(EnemyKind.SeaUrchin, bed: true)));

        _panel.AddSection("Run");
        _panel.AddTextField("Seed", () => _seed.ToString(), text =>
        {
            if (!SeedCode.TryParse(text, out var seed))
            {
                Notice("Not a seed", "8 characters from A–Z and 2–9 (no 0, O, 1 or I), or DEBUG");
                return;
            }
            _seed = seed;
            Restart();
        });
        _panel.AddButtons(
            ("Random seed", () =>
            {
                _seed = SeedCode.NewRandom();
                Restart();
            }),
            ("Save run", SaveRun),
            ("Load run", LoadRun));
        _panel.AddButtons(
            ("Copy save code", () =>
            {
                _save.Run = Snapshot();
                DisplayServer.ClipboardSet(SaveCodec.Export(_save));
                Notice("Save code copied", "Paste it anywhere to back up this profile and run");
            }),
            ("Import from clipboard", ImportSave));

        _panel.AddSection("Items  (hover for effects; ◆ = active, F to use)");
        _panel.AddItemPicker(_catalog.Items, () => _world?.Items.ToHashSet() ?? new HashSet<string>(), ids =>
        {
            _world.SetItems(ids);
            HandleEvents();
            _world.Events.Clear();
            _panel.Refresh();
        }, GiveRandomTreasure);

        _panel.AddTunables(_tuning, _view);
        foreach (var (kind, def) in _creatures.All) _panel.AddTunables("Creature: " + kind, def);
        _panel.Refresh();
    }

    static void CopyFields<T>(T from, T to)
    {
        foreach (var f in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance)) f.SetValue(to, f.GetValue(from));
    }

    void Notice(string title, string text) => _hud.ShowBanner(title, text, new Color(0.7f, 0.9f, 1f), 3f);

    /// <summary>Draws from the treasure pool exactly like a treasure chamber would (seeded per draw).</summary>
    void GiveRandomTreasure()
    {
        var streams = new RunStreams(_seed);
        var rng = streams.Items(1, 1, _world.Items.Count + _treasureDraws++);
        var item = ItemPools.Draw(_catalog, "treasure", rng, _save.Profile.Achievements, _world.Items.ToHashSet());
        if (item is null)
        {
            Notice("Treasure pool is empty", "You own every unlocked treasure item");
            return;
        }
        _world.GiveItem(item.Id);
        HandleEvents();
        _world.Events.Clear();
        _panel.Refresh();
    }

    int _treasureDraws;

    /// <summary>The run plus which level it is in (depth 0 = the grey-box cave).</summary>
    SuspendedRun Snapshot()
    {
        var run = _world.Snapshot(_seed.ToString(), customSeed: false);
        run.Depth = _levelKind == LevelKind.Greybox ? 0 : _depth;
        run.Reef = _reefIndex;
        return run;
    }

    void SaveRun()
    {
        _save.Run = Snapshot();
        SaveStore.Write(_save);
        Notice("Run saved", SaveStore.GlobalPath);
    }

    void LoadRun()
    {
        _save = SaveStore.Load();
        if (_save.Run is null)
        {
            Notice("No saved run", "Use Save run first");
            return;
        }
        ResumeRun(_save.Run);
        Notice("Run loaded", $"Seed {_save.Run.Seed} · {_save.Run.Items.Count} items");
    }

    void ImportSave()
    {
        try
        {
            _save = SaveCodec.Import(DisplayServer.ClipboardGet());
            SaveStore.Write(_save);
            if (_save.Run is not null) ResumeRun(_save.Run);
            Notice("Save imported", $"{_save.Profile.SeenItems.Count} items seen · {_save.Profile.Achievements.Count} achievements");
        }
        catch (SaveException e)
        {
            Notice("Import failed", e.Message);
        }
    }

    void ResumeRun(SuspendedRun run)
    {
        if (SeedCode.TryParse(run.Seed, out var seed)) _seed = seed;
        _levelKind = run.Depth == 0 ? LevelKind.Greybox : LevelKind.Reef;
        if (run.Depth > 0) _depth = run.Depth;
        _reefIndex = Math.Clamp(run.Reef, 1, 3);
        BeginLoad(run);
    }

    // ───────────────────────── verification mode ─────────────────────────

    void ParseArgs()
    {
        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (arg == "--autopilot") _autopilot = true;
            else if (arg == "--panel") _openPanel = true;
            else if (arg == "--greybox") _levelKind = LevelKind.Greybox;
            else if (arg == "--nest") _startNest = true;
            else if (arg == "--uncapped") _uncapped = true;
            else if (arg.StartsWith("--look=") && arg["--look=".Length..].Split(',') is [var ly, var lp]
                && float.TryParse(ly, System.Globalization.CultureInfo.InvariantCulture, out float lookYaw)
                && float.TryParse(lp, System.Globalization.CultureInfo.InvariantCulture, out float lookPitch))
                _look = new Vector2(lookYaw, lookPitch);
            else if (arg.StartsWith("--den=") && arg["--den=".Length..].Split(',') is var denArg && Enum.TryParse<EnemyKind>(denArg[0], true, out var denKind))
            {
                _startDen = denKind;
                if (denArg.Length > 1 && int.TryParse(denArg[1], out int denIndex)) _denIndex = denIndex;
                if (denArg.Length > 2 && denArg[2] == "bed") _denBed = true;
            }
            else if (arg == "--ninjacam") _ninjaCam = true;
            else if (arg == "--hideanemones") _hideAnemones = true;
            else if (arg.StartsWith("--lanterns=") && int.TryParse(arg["--lanterns=".Length..], out int lanterns)) _lanternOverride = lanterns;
            else if (arg.StartsWith("--nestangle=") && float.TryParse(arg["--nestangle=".Length..], System.Globalization.CultureInfo.InvariantCulture, out float na)) _nestAngle = na * Mathf.Pi / 180f;
            else if (arg.StartsWith("--nestdist=") && float.TryParse(arg["--nestdist=".Length..], System.Globalization.CultureInfo.InvariantCulture, out float nd)) _nestDistance = nd;
            else if (arg == "--review") _reviewing = true;
            else if (arg == "--profile") _profile = true;
            else if (arg.StartsWith("--bench=") && int.TryParse(arg["--bench=".Length..], out int bench)) _benchFrames = bench;
            else if (arg.StartsWith("--at=") && Enum.TryParse<ChamberRole>(arg["--at=".Length..], true, out var role)) _startAt = role;
            else if (arg.StartsWith("--reef=") && arg["--reef=".Length..].Split(',') is [var d, var r] && int.TryParse(d, out int depth) && int.TryParse(r, out int reef))
            {
                _depth = Math.Clamp(depth, 1, 6);
                _reefIndex = Math.Clamp(reef, 1, 3);
            }
            else if (arg.StartsWith("--seed=") && SeedCode.TryParse(arg["--seed=".Length..], out var seed)) _seed = seed;
            else if (arg.StartsWith("--capture=")) _captureDir = arg["--capture=".Length..];
            else if (arg.StartsWith("--items=")) _startItems.AddRange(arg["--items=".Length..].Split(','));
            else if (arg.StartsWith("--frames="))
                foreach (string f in arg["--frames=".Length..].Split(','))
                    if (int.TryParse(f, out int n)) _captureFrames.Enqueue(n);
        }
    }

    void StartDebugMode()
    {
        if (_startItems.Count > 0)
        {
            foreach (var id in _startItems.Where(_catalog.Contains)) _world.GiveItem(id);
            HandleEvents();
            _world.Events.Clear();
        }
        if (_startAt is { } role) TeleportTo(role);
        if (_startNest) TeleportToNest();
        if (_startDen is { } den) TeleportToDen(den, _denBed, _denIndex);
        if (_look is { } look)
        {
            _yaw = Mathf.DegToRad(look.X);
            _pitch = Mathf.DegToRad(look.Y);
        }
        if (_openPanel) TogglePanel();
    }

    void Capture()
    {
        _frame++;
        if (_benchFrames > 0 && _frame == _benchFrames)
        {
            GD.Print($"Bench after {_frame} frames: {Readout().Replace('\n', ' ')}");
            GetTree().Quit();
        }
        if (_captureDir is null || _captureFrames.Count == 0) return;
        if (_frame < _captureFrames.Peek()) return;
        int frame = _captureFrames.Dequeue();
        string path = $"{_captureDir}/m0_{frame:0000}.png";
        GetViewport().GetTexture().GetImage().SavePng(path);
        GD.Print($"Captured {path}  {Readout().Replace('\n', ' ')}");
        if (_captureFrames.Count == 0) GetTree().Quit();
    }

    /// <summary>Swims toward the nearest creature, keeps aiming at it, fires, dashes and uses the active.</summary>
    PlayerInput Autopilot(PlayerInput input)
    {
        long tick = _world.Tick;
        if (tick < 60) return input; // sink for a second first

        Enemy? target = null;
        float best = float.MaxValue;
        foreach (var e in _world.Enemies)
        {
            if (!e.Alive) continue;
            float d = System.Numerics.Vector3.Distance(e.Position, _world.Player.Position);
            if (d < best)
            {
                best = d;
                target = e;
            }
        }

        bool charging = _world.Loadout.Shot.Charge || _world.Loadout.Shot.Laser;
        if (target is not null)
        {
            Vector3 to = (target.Position - _world.Player.Position).G();
            float yaw = Mathf.Atan2(-to.X, -to.Z);
            float pitch = Mathf.Atan2(to.Y, new Vector2(to.X, to.Z).Length());
            _yaw = Mathf.LerpAngle(_yaw, yaw, 0.08f);
            _pitch = Mathf.Lerp(_pitch, pitch, 0.08f);
            input.Forward = best > 6f ? 1f : 0f;
            input.Strafe = Mathf.Sin(tick * 0.02f) * 0.6f;
            // Charge weapons: hold for a second, then release.
            input.Fire = best < 12f && (!charging || tick % 80 < 65);
        }
        input.Yaw = _yaw;
        input.Pitch = _pitch;
        input.Dash = tick % 150 == 0;
        input.UseActive = tick % 400 == 200;
        input.DropBomb = tick % 300 == 100;
        return input;
    }

    /// <summary>Capture aid: hover half a metre beside the first awake ninja, looking at it.</summary>
    void FollowNinja()
    {
        var ninja = _world.Enemies.FirstOrDefault(e => e.Kind == EnemyKind.ClownNinja && e.Alive);
        if (ninja is null) return;
        var side = new System.Numerics.Vector3(-ninja.Facing.Z, 0f, ninja.Facing.X);
        side = System.Numerics.Vector3.Normalize(side) * 1.3f + new System.Numerics.Vector3(0f, 0.12f, 0f);
        _world.Teleport(ninja.Position + side);
        var to = (ninja.Position - _world.Player.Position).G();
        _yaw = Mathf.Atan2(-to.X, -to.Z);
        _pitch = Mathf.Atan2(to.Y, new Vector2(to.X, to.Z).Length());
    }

    string NinjaSummary() =>
        string.Join("/", _world.Enemies.Where(e => e.Kind == EnemyKind.ClownNinja).Select(e => e.State.ToString().ToLowerInvariant()));

    string Readout()
    {
        var p = _world.Player;
        var st = _world.Loadout.Stats;
        int alive = _world.Enemies.Count(e => e.Alive);
        double process = Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000.0;
        double tris = Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame);
        double draws = Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame);
        double gpu = RenderingServer.ViewportGetMeasuredRenderTimeGpu(GetViewport().GetViewportRid());
        return $"FPS {Engine.GetFramesPerSecond():0}   GPU {gpu:0.0} ms   process {process:0.0} ms   {tris / 1000:0}k tris   {draws:0} draws   tick {_world.Tick}\n{WhereAmI()}\n" +
               $"seed {_seed}   bombs {_world.Bombs}   craters {_world.Craters.Count}   ninjas {NinjaSummary()}   fish {_world.Clownfish.Count}\n" +
               $"speed {p.Velocity.Length():0.00} m/s   pos {p.Position.X:0.0}, {p.Position.Y:0.0}, {p.Position.Z:0.0}\n" +
               $"jet {(p.JetTimer > 0 ? "ON" : "-")}   dash {(p.IsDashing ? "ON" : "-")}   floor {(p.OnFloor ? "yes" : "no")}\n" +
               $"enemies {alive}/{_world.Enemies.Count}   projectiles {_world.Projectiles.Count}   clouds {_world.Clouds.Count}   kills {_world.Kills}\n" +
               $"dmg {st.Damage:0.##}  rate {_world.EffectiveFireRate:0.##}/s  shot spd {st.ShotSpeed:0.##}  range {st.Range:0.##}  speed {st.Speed:0.##}  luck {st.Luck:0.#}";
    }
}
