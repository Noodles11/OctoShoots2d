using System;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using OctoShoots.Core.Gen;
using OctoShoots.Core.Run;
using OctoShoots.Core.Saves;
using OctoShoots.Core.Sim;
using OctoShoots.Core.Terrain;
using OctoShoots.Game.Fx;
using OctoShoots.Game.Terrain;
using OctoShoots.Game.Util;

namespace OctoShoots.Game.Scenes;

/// <summary>
/// Level lifecycle: generating a reef (or the grey-box cave) on a worker thread, showing it, and
/// starting a fresh world on a copy of the pristine terrain so craters never leak between runs.
/// </summary>
public partial class Main
{
    enum LevelKind { Reef, Greybox }

    sealed record LoadedLevel(ReefLayout? Layout, Cave Pristine, Cave Playable, MeshData[] Meshes, List<FloraItem>? Flora, float[]? SunMap, string Key, long Millis);

    /// <summary>The grey-box cave stays the same whatever the run seed; seeds drive items, AI and combat rolls.</summary>
    const ulong GreyboxSeed = 20261002;

    LevelKind _levelKind = LevelKind.Reef;
    int _depth = 1;
    int _reefIndex = 1;
    ReefLayout? _layout;
    Cave? _pristine;
    string _pristineKey = "";
    Task<LoadedLevel>? _loading;
    SuspendedRun? _pendingRun;
    List<string> _carryItems = new();
    Label _loadingLabel = null!;
    float _nestDistance = 14f;
    float _nestAngle;
    bool _hideAnemones;

    string LevelKey => _levelKind == LevelKind.Greybox ? "greybox" : $"{_seed}/{_depth}/{_reefIndex}";

    string LevelName => _levelKind == LevelKind.Greybox ? "Grey-box cave" : new ReefSpec(_depth, _reefIndex).ToString();

    /// <summary>
    /// Starts building the level on a worker thread. The same seed and reef reuse the pristine terrain
    /// (a restart only copies and remeshes it); a saved run carves its craters before meshing.
    /// </summary>
    void BeginLoad(SuspendedRun? resume = null)
    {
        if (_loading is not null) return;
        _carryItems = _world?.Items.ToList() ?? _carryItems;
        _pendingRun = resume;

        string key = LevelKey;
        Cave? pristine = key == _pristineKey ? _pristine : null;
        ReefLayout? layout = pristine is null ? null : _layout;
        var kind = _levelKind;
        var seed = _seed;
        var spec = new ReefSpec(_depth, _reefIndex);
        var craters = resume?.Craters.ToList() ?? new List<float[]>();

        _loadingLabel.Text = pristine is null ? $"Generating {LevelName}…" : "Resetting…";
        _loadingLabel.Visible = true;
        _loading = Task.Run(() =>
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            List<FloraItem>? flora = null;
            if (pristine is null)
            {
                if (kind == LevelKind.Greybox)
                {
                    pristine = GreyboxCave.Build(GreyboxSeed);
                }
                else
                {
                    layout = ReefGenerator.Generate(new RunStreams(seed), spec, line => GD.Print(line));
                    pristine = layout.Cave;
                }
                flora = FloraPlanner.Plan(pristine, layout, kind == LevelKind.Greybox ? GreyboxSeed : seed.Value);
            }
            var playable = pristine.Clone();
            foreach (var c in craters.Where(c => c.Length == 4))
                playable.Sdf.CarveSphere(new System.Numerics.Vector3(c[0], c[1], c[2]), c[3], playable.ShellCells);
            var meshes = TerrainView.MeshAll(playable.Sdf);
            float[]? sunMap = layout is null ? null : SunLight.Build(playable.Sdf, layout.SurfaceY);
            return new LoadedLevel(layout, pristine, playable, meshes, flora, sunMap, key, watch.ElapsedMilliseconds);
        });
    }

    /// <summary>Called every frame; swaps the finished level in. Returns true while still loading.</summary>
    bool PollLoad()
    {
        if (_loading is null) return false;
        if (!_loading.IsCompleted) return true;

        var task = _loading;
        _loading = null;
        _loadingLabel.Visible = false;
        if (task.IsFaulted)
        {
            GD.PushError($"Level failed to build: {task.Exception?.GetBaseException()}");
            Notice("Level failed to build", task.Exception?.GetBaseException().Message ?? "unknown error");
            return _world is null;
        }

        var level = task.Result;
        _layout = level.Layout;
        _pristine = level.Pristine;
        _pristineKey = level.Key;
        _cave = level.Playable;
        GD.Print($"{LevelName} ready in {level.Millis} ms ({_cave.Sdf.Nx}x{_cave.Sdf.Ny}x{_cave.Sdf.Nz} grid)");

        _world = new World(_cave, _tuning, _seed.Value, _catalog, _save.Profile.Achievements, _creatures);
        if (_pendingRun is { } run)
        {
            _world.Restore(run);
            _yaw = run.Yaw;
            _pitch = run.Pitch;
        }
        else
        {
            _world.SetItems(_carryItems, runPickupEffects: false);
            FaceStart();
        }
        _pendingRun = null;
        _world.Events.Clear();

        _terrain.Show(_cave.Sdf, level.Meshes);
        if (_layout is not null) _sun.Show(_cave.Sdf, _layout.SurfaceY, level.SunMap);
        else _sun.ShowCave(_cave.Sdf.Size.Y + 12f);
        _terrain.SetSeabed(_layout?.Heights, (int)(_layout?.Size.X ?? 0), (int)(_layout?.Size.Z ?? 0), _layout?.SurfaceY ?? _cave.SurfaceY);
        _levelFx.ShowLayout(_layout);
        ShowSeaSurface(_layout);
        _loot.Clear();
        if (_hideAnemones) _anemones.Clear();
        else _anemones.Show(_cave.Anemones);
        _fish.Clear();
        if (level.Flora is not null) _flora.Show(level.Flora);
        _creatureViews.Clear();
        _heldKey = "";
        foreach (var v in _enemyViews.Values) v.QueueFree();
        _enemyViews.Clear();
        _shots.Clear();
        _ink.Clear();
        _sparks.Clear();
        _stains.Clear();
        _lines.Clear();
        _levelFx.ClearBombs();
        _hud.ClearCards();
        _accumulator = 0;
        _panel.Refresh();
        if (_layout is not null) _hud.ShowBanner(LevelName.ToUpperInvariant(), $"Seed {_seed} · {_layout.Chambers.Count - 1} caves and the boss reef", new Color(0.6f, 0.95f, 1f), 3f);
        return false;
    }

    /// <summary>Look from the spawn toward the way out of the start chamber.</summary>
    void FaceStart()
    {
        Vector3 from = _cave.PlayerSpawn.G();
        Vector3 to = new(24f, 15f, 22f);
        if (_layout is not null)
        {
            // Out over the open sea, toward the middle of the basin.
            to = new Vector3(_layout.Size.X / 2f, from.Y, _layout.Size.Z / 2f);
        }
        var d = to - from;
        _yaw = Mathf.Atan2(-d.X, -d.Z);
        _pitch = 0f;
    }

    /// <summary>Debug: jump into a chamber of the given role.</summary>
    void TeleportTo(ChamberRole role)
    {
        if (role == ChamberRole.Start && _layout is not null && _world is not null)
        {
            _world.Teleport(_layout.StartPosition);
            FaceStart();
            return;
        }
        var chamber = _layout?.ByRole(role);
        if (chamber is null || _world is null)
        {
            Notice("No such chamber", _layout is null ? "The grey-box cave has no chambers" : $"This reef has no {role} chamber");
            return;
        }
        // Hover over the middle of the floor, a few metres back, looking at what lies there.
        var at = chamber.Center + new System.Numerics.Vector3(0f, 0.5f, 3.2f);
        if (_cave.Sdf.Sample(at) < 1f) at = chamber.Center;
        _world.Teleport(at);
        _yaw = 0f;
        _pitch = role == ChamberRole.Boss ? -0.7f : -0.45f;
        _hud.ShowBanner(role.ToString(), $"Chamber {chamber.Index}", Fx.LevelFx.RoleColor(role), 2f);
    }

    /// <summary>Debug: swim to open water near the nearest nest anemone (up to 14 m away), looking at it.</summary>
    void TeleportToNest()
    {
        if (_world is null) return;
        var player = _world.Player.Position;
        var nest = _cave.Anemones.Where(a => a.Nest).OrderBy(a => System.Numerics.Vector3.DistanceSquared(a.Position, player)).Cast<AnemoneSpot?>().FirstOrDefault();
        if (nest is not { } anemone)
        {
            Notice("No nests here", "This level has no clownfish nests");
            return;
        }
        foreach (float distance in new[] { _nestDistance, 14f, 10f, 7f, 5f })
        for (int i = 0; i < 16; i++)
        {
            float angle = _nestAngle + i * Mathf.Tau / 16f;
            var at = anemone.Position + new System.Numerics.Vector3(Mathf.Cos(angle) * distance, MathF.Min(4f, distance * 0.4f), Mathf.Sin(angle) * distance);
            if (_cave.Sdf.Sample(at) < 1.2f) continue;
            _world.Teleport(at);
            var to = (anemone.Position + anemone.Up * (anemone.Height * 0.7f) - at).G();
            _yaw = Mathf.Atan2(-to.X, -to.Z);
            _pitch = Mathf.Atan2(to.Y, new Vector2(to.X, to.Z).Length());
            Notice("Clownfish nest", "Find the one wearing a headband");
            return;
        }
        Notice("Nest is boxed in", "No open water around it");
    }

    readonly Dictionary<(EnemyKind, bool), int> _denCycle = new();

    /// <summary>Debug: swim to open water near a den of this kind (the next one each time), looking at it.</summary>
    void TeleportToDen(EnemyKind kind, bool bed = false, int? index = null)
    {
        if (_world is null) return;
        var dens = _cave.Dens.Where(d => d.Kind == kind && d.Bed == bed)
            .OrderBy(d => System.Numerics.Vector3.DistanceSquared(d.Position, _cave.PlayerSpawn)).ToList();
        if (dens.Count == 0)
        {
            Notice("No dens here", "This level has no " + kind + " dens");
            return;
        }
        int next = index ?? _denCycle.GetValueOrDefault((kind, bed));
        _denCycle[(kind, bed)] = next + 1;
        var den = dens[next % dens.Count];
        var target = den.Position + den.Up * 0.6f;
        foreach (float distance in new[] { 9f, 7f, 5f, 12f })
        for (int i = 0; i < 16; i++)
        {
            float angle = i * Mathf.Tau / 16f;
            var at = den.Position + den.Up * 2f + new System.Numerics.Vector3(Mathf.Cos(angle) * distance, 1f, Mathf.Sin(angle) * distance);
            if (_cave.Sdf.Sample(at) < 1.2f || !_cave.Sdf.LineOfSight(at, target)) continue;
            _world.Teleport(at);
            var to = (target - at).G();
            _yaw = Mathf.Atan2(-to.X, -to.Z);
            _pitch = Mathf.Atan2(to.Y, new Vector2(to.X, to.Z).Length());
            Notice(kind.ToString(), "den " + (next % dens.Count + 1) + " of " + dens.Count);
            return;
        }
        Notice("Den is boxed in", "No open water with a view of it");
    }

    string WhereAmI()
    {
        if (_layout is null || _world is null) return LevelName;
        var c = _layout.ChamberAt(_world.Player.Position);
        string place = c is null ? "open sea" : c.Role == ChamberRole.Normal ? "a cave" : $"{c.Role.ToString().ToLowerInvariant()} cave";
        return $"{LevelName} · {place}";
    }
}
