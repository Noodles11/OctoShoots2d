using System.Collections.Generic;
using System.Linq;
using Godot;
using OctoShoots.Core;
using OctoShoots.Core.Gen;
using OctoShoots.Core.Items;
using OctoShoots.Core.Sim;
using OctoShoots.Game.Fx;
using OctoShoots.Game.Player;
using OctoShoots.Game.UI;
using OctoShoots.Game.Util;

namespace OctoShoots.Game.Scenes;

/// <summary>
/// Open-sea presentation: the mist that limits vision, the sea surface, loot, the minimap, and the
/// inventory tentacle with its review pose.
/// </summary>
public partial class Main
{
    /// <summary>Sunlit shallows turquoise; the background and the mist share it so open water reads as water.</summary>
    public static readonly Color MistColor = new(0.1f, 0.32f, 0.38f);

    LootViews _loot = null!;
    ShaderMaterial _mist = null!;
    MeshInstance3D? _seaSurface;
    bool _reviewing;
    string _heldKey = "";

    void BuildSea()
    {
        _loot = new LootViews();
        AddChild(_loot);
        _loot.Init(_catalog);

        // Full-screen mist pass that rides with the camera (§9 "limited vision").
        _mist = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/mist.gdshader"), RenderPriority = -120 };
        _mist.SetShaderParameter("mist_color", MistColor);
        _camera.Camera.AddChild(new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(2f, 2f), FlipFaces = true },
            MaterialOverride = _mist,
            ExtraCullMargin = 16384f,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
    }

    /// <summary>A rippling, sunlit sea surface seen from below; only open-sea levels have one.</summary>
    void ShowSeaSurface(ReefLayout? layout)
    {
        _seaSurface?.QueueFree();
        _seaSurface = null;
        if (layout is null) return;
        _seaSurface = new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(layout.Size.X, layout.Size.Z), SubdivideWidth = 1, SubdivideDepth = 1 },
            MaterialOverride = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/sea_surface.gdshader") },
            Position = new Vector3(layout.Size.X / 2f, layout.SurfaceY, layout.Size.Z / 2f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(_seaSurface);
    }

    void TickSea(float alpha, float dt)
    {
        _mist.SetShaderParameter("mist_start", _view.MistStart);
        _mist.SetShaderParameter("mist_end", Mathf.Max(_view.MistEnd, _view.MistStart + 1f));
        _loot.Sync(_world, alpha, dt);
        UpdateHeldPearls();
        UpdateReview();
        _hud.SetMinimap(Blips(), _view.MinimapRange);
    }

    // ───────────────────────── inventory tentacle ─────────────────────────

    /// <summary>Puts one pearl per item, in pickup order, on the inventory tentacle's suckers.</summary>
    void UpdateHeldPearls()
    {
        string key = string.Join(",", _world.Items);
        if (key == _heldKey) return;
        _heldKey = key;
        var materials = _world.Items.Where(_catalog.Contains).Select(id => (Material)PearlMaterials.Get(_catalog[id], overlay: true)).ToList();
        _camera.Viewmodel.SetPearls(materials);
    }

    void UpdateReview()
    {
        var vm = _camera.Viewmodel;
        bool want = _reviewing && !_paused && !_panelOpen;
        vm.SetReviewing(want);
        if (!want || vm.PearlCount == 0)
        {
            _hud.SetReview(null, want ? ("No pearls yet", "Find item pearls in shells around the reef", new List<string>(), 0, 0) : null);
            return;
        }

        var items = _world.Items.Where(_catalog.Contains).Select(id => _catalog[id]).ToList();
        var labels = new List<PearlLabel>();
        for (int i = 0; i < vm.PearlCount; i++)
        {
            Vector3 at = vm.PearlGlobalPosition(i);
            if (_camera.Camera.IsPositionBehind(at)) continue;
            labels.Add(new PearlLabel(_camera.Camera.UnprojectPosition(at), items[i].Name, i == vm.Selected));
        }
        var selected = items[Mathf.Clamp(vm.Selected, 0, items.Count - 1)];
        _hud.SetReview(labels, (selected.Name, selected.Tagline, ItemCaption.Describe(selected), items.Count, vm.PearlCount));
    }

    void CycleReview(int step)
    {
        var vm = _camera.Viewmodel;
        if (vm.PearlCount == 0) return;
        vm.Selected = (vm.Selected + step + vm.PearlCount) % vm.PearlCount;
        _sfx.Play("bounce", -10f, 0f);
    }

    // ───────────────────────── minimap ─────────────────────────

    /// <summary>Everything within minimap range, as offsets (right, forward) from Clementine's facing.</summary>
    List<Blip> Blips()
    {
        var p = _world.Player.Position;
        var right = MathUtil.Right(_yaw);
        var forward = new System.Numerics.Vector3(-Mathf.Sin(_yaw), 0f, -Mathf.Cos(_yaw));
        float range = _view.MinimapRange * 1.15f;
        var blips = new List<Blip>();

        void Add(System.Numerics.Vector3 at, BlipKind kind, Color? tint = null, bool always = false)
        {
            var rel = at - p;
            if (!always && rel.X * rel.X + rel.Z * rel.Z > range * range) return;
            blips.Add(new Blip(new Vector2(System.Numerics.Vector3.Dot(rel, right), System.Numerics.Vector3.Dot(rel, forward)), rel.Y, kind, tint));
        }

        if (_layout is not null)
            foreach (var c in _layout.Chambers.Where(c => c.Role is not ChamberRole.Normal and not ChamberRole.Secret))
                Add(c.Mouth, BlipKind.Landmark, LevelFx.RoleColor(c.Role));
        foreach (var k in _world.Pickups) Add(k.Position, BlipKind.Pickup);
        foreach (var c in _world.Chests.Where(c => !c.Opened)) Add(c.Position, BlipKind.Chest);
        foreach (var s in _world.Shells.Where(s => s.ItemId is not null))
            Add(s.Position, BlipKind.Pearl, _catalog.TryGet(s.ItemId!, out var item) ? PearlMaterials.GlowColor(item) : null);
        foreach (var e in _world.Enemies.Where(e => e.Alive))
        {
            // A disguised ninja stays off the map until it gives itself away.
            if (e.Kind == EnemyKind.ClownNinja && e.State == EnemyState.Idle) continue;
            // Sleeping creatures stay off the map; hunters show even far beyond it, as arrows on its rim.
            if (World.IsDenCreature(e.Kind))
            {
                if (e.State == EnemyState.Idle) continue;
                Add(e.Position, e.State is EnemyState.Telegraph or EnemyState.Attack ? BlipKind.EnemyAlert : BlipKind.Enemy, always: true);
                continue;
            }
            Add(e.Position, e.State == EnemyState.Telegraph ? BlipKind.EnemyAlert : BlipKind.Enemy);
        }
        return blips;
    }
}
