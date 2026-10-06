using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen;
using OctoShoots.Core.Sim;
using OctoShoots.Game.Util;

namespace OctoShoots.Game.Fx;

/// <summary>
/// Landmark lights so special chambers read from a distance (§6.4), the glowing Crack in the boss
/// arena floor (§7.4), and the live ink bombs with their blinking fuses.
/// </summary>
public partial class LevelFx : Node3D
{
    public static Color RoleColor(ChamberRole role) => role switch
    {
        ChamberRole.Treasure => new Color(1f, 0.8f, 0.3f),
        ChamberRole.Shop => new Color(0.4f, 1f, 0.5f),
        ChamberRole.Curse => new Color(1f, 0.25f, 0.2f),
        ChamberRole.Secret => new Color(0.75f, 0.4f, 1f),
        ChamberRole.Boss => new Color(1f, 0.4f, 0.15f),
        ChamberRole.Start => new Color(0.6f, 0.95f, 1f),
        _ => Colors.White,
    };

    readonly Node3D _landmarks = new();
    readonly Dictionary<int, (Node3D Root, OmniLight3D Light, StandardMaterial3D Glow)> _bombs = new();
    StandardMaterial3D _bombBody = null!;
    SphereMesh _bombMesh = null!;

    public override void _Ready()
    {
        AddChild(_landmarks);
        _bombBody = new StandardMaterial3D { AlbedoColor = new Color(0.05f, 0.04f, 0.1f), Roughness = 0.3f, RimEnabled = true, Rim = 0.6f };
        _bombMesh = new SphereMesh { Radius = 0.25f, Height = 0.5f };
    }

    public void ShowLayout(ReefLayout? layout)
    {
        foreach (var child in _landmarks.GetChildren()) child.QueueFree();
        if (layout is null) return;

        foreach (var c in layout.Chambers)
        {
            if (c.Role is ChamberRole.Normal or ChamberRole.Boss) continue;
            _landmarks.AddChild(new OmniLight3D
            {
                LightColor = RoleColor(c.Role),
                LightEnergy = 1.8f,
                OmniRange = 16f,
                OmniAttenuation = 1f,
                ShadowEnabled = false,
                Position = new Vector3(c.Center.X, c.Center.Y + c.Radii.Y * 0.6f, c.Center.Z),
            });
            if (c.Role == ChamberRole.Secret) continue;
            // A beacon at the cave mouth so it reads from the open sea.
            _landmarks.AddChild(new OmniLight3D
            {
                LightColor = RoleColor(c.Role),
                LightEnergy = 2.5f,
                OmniRange = 9f,
                ShadowEnabled = false,
                Position = new Vector3(c.Mouth.X, c.Mouth.Y + 2f, c.Mouth.Z),
            });
        }

        // The Crack: an emissive seam with warm light pouring up.
        Vector3 a = layout.CrackA.G(), b = layout.CrackB.G();
        var seam = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(a.DistanceTo(b) + 1f, 0.3f, 0.7f) },
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                AlbedoColor = new Color(1f, 0.55f, 0.2f),
                EmissionEnabled = true,
                Emission = new Color(1f, 0.45f, 0.1f),
                EmissionEnergyMultiplier = 5f,
            },
            Position = (a + b) / 2f + Vector3.Down * 0.6f,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        _landmarks.AddChild(seam);
        for (int i = 0; i < 4; i++)
        {
            _landmarks.AddChild(new OmniLight3D
            {
                LightColor = RoleColor(ChamberRole.Boss),
                LightEnergy = 2f,
                OmniRange = 12f,
                Position = a.Lerp(b, (i + 0.5f) / 4f) + Vector3.Up * 1.5f,
            });
        }
    }

    public void SyncBombs(World world, float alpha, float time)
    {
        var seen = new HashSet<int>();
        foreach (var b in world.LiveBombs)
        {
            seen.Add(b.Id);
            if (!_bombs.TryGetValue(b.Id, out var view))
            {
                var root = new Node3D();
                var glow = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, EmissionEnabled = true };
                root.AddChild(new MeshInstance3D { Mesh = _bombMesh, MaterialOverride = _bombBody });
                root.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.07f, Height = 0.14f }, MaterialOverride = glow, Position = new Vector3(0f, 0.26f, 0f) });
                var light = new OmniLight3D { OmniRange = 2.5f, OmniAttenuation = 2f, ShadowEnabled = false, LightCullMask = ~Player.Viewmodel.RenderLayer };
                root.AddChild(light);
                AddChild(root);
                view = (root, light, glow);
                _bombs[b.Id] = view;
            }

            view.Root.Position = Conv.Lerp(b.PrevPosition, b.Position, alpha);
            // Blink faster as the fuse runs down.
            float rate = 3f + 12f * (1f - Mathf.Clamp(b.Fuse / world.Tuning.BombFuse, 0f, 1f));
            bool on = Mathf.Sin(time * rate * Mathf.Tau) > 0f;
            Color c = on ? new Color(1f, 0.3f, 0.6f) : new Color(0.4f, 0.1f, 0.3f);
            view.Glow.AlbedoColor = c;
            view.Glow.Emission = c;
            view.Glow.EmissionEnergyMultiplier = on ? 4f : 0.5f;
            view.Light.LightColor = c;
            view.Light.LightEnergy = on ? 0.7f : 0.1f;
        }

        var gone = new List<int>();
        foreach (var id in _bombs.Keys) if (!seen.Contains(id)) gone.Add(id);
        foreach (var id in gone)
        {
            _bombs[id].Root.QueueFree();
            _bombs.Remove(id);
        }
    }

    public void ClearBombs()
    {
        foreach (var view in _bombs.Values) view.Root.QueueFree();
        _bombs.Clear();
    }
}
