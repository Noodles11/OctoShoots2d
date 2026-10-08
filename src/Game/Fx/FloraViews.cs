using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OctoShoots.Game.Fx;

/// <summary>One plant or sessile animal on the reef.</summary>
public readonly record struct FloraItem(FloraKind Kind, Transform3D Transform, Color Tint);

/// <summary>
/// Draws the reef's flora: one MultiMesh per species per 32 m tile, each fading out beyond the mist,
/// so only the nearby reef costs anything. Sway, lattice, grooves and polyps are done in flora.gdshader.
/// </summary>
public partial class FloraViews : Node3D
{
    const float Tile = 32f;

    sealed record Look(float Sway, float SwaySpeed, float Lattice, float Meander, float Polyps, float Rough, float Glow, float Backlight, float Range);

    static readonly Dictionary<FloraKind, Look> Looks = new()
    {
        [FloraKind.TurtleGrass] = new(0.09f, 1.3f, 0f, 0f, 0f, 0.6f, 0.2f, 0.6f, 30f),
        [FloraKind.Halimeda] = new(0.03f, 1.1f, 0f, 0f, 0f, 0.6f, 0.05f, 0.2f, 28f),
        [FloraKind.Staghorn] = new(0f, 0f, 0f, 0f, 0.8f, 0.75f, 0.06f, 0f, 55f),
        [FloraKind.Elkhorn] = new(0f, 0f, 0f, 0f, 0.6f, 0.7f, 0.06f, 0f, 55f),
        [FloraKind.BrainCoral] = new(0f, 0f, 0f, 1f, 0f, 0.8f, 0.05f, 0f, 55f),
        [FloraKind.SeaFan] = new(0.06f, 0.9f, 1f, 0f, 0f, 0.65f, 0.08f, 0.45f, 50f),
        [FloraKind.SeaRod] = new(0.07f, 1.0f, 0f, 0f, 0.5f, 0.85f, 0.06f, 0.15f, 45f),
        [FloraKind.TubeSponge] = new(0f, 0f, 0f, 0f, 0f, 0.8f, 0.08f, 0f, 50f),
        [FloraKind.BarrelSponge] = new(0f, 0f, 0f, 0f, 0f, 0.9f, 0.05f, 0f, 60f),
    };

    readonly Dictionary<FloraKind, (Mesh Mesh, ShaderMaterial Material)> _species = new();

    /// <summary>Draw at any distance (the top-down camera sees the whole view at once), and cast shadows.</summary>
    public bool Unlimited { get; set; }

    /// <summary>Repaints a species in two variants (low and high ends each), keeping its mesh's light and dark structure.</summary>
    public void SetPalette(FloraKind kind, Color lowA, Color highA, Color lowB, Color highB, float sway = -1f)
    {
        if (!_species.TryGetValue(kind, out var s)) return;
        var m = s.Material;
        m.SetShaderParameter("recolor", 1f);
        m.SetShaderParameter("low_a", lowA);
        m.SetShaderParameter("high_a", highA);
        m.SetShaderParameter("low_b", lowB);
        m.SetShaderParameter("high_b", highB);
        if (sway >= 0f) m.SetShaderParameter("sway", sway);
    }

    public override void _Ready()
    {
        var shader = GD.Load<Shader>("res://assets/shaders/flora.gdshader");
        foreach (var (kind, look) in Looks)
        {
            var material = new ShaderMaterial { Shader = shader };
            material.SetShaderParameter("sway", look.Sway);
            material.SetShaderParameter("sway_speed", look.SwaySpeed);
            material.SetShaderParameter("lattice", look.Lattice);
            material.SetShaderParameter("meander", look.Meander);
            material.SetShaderParameter("polyps", look.Polyps);
            material.SetShaderParameter("rough", look.Rough);
            material.SetShaderParameter("glow", look.Glow);
            material.SetShaderParameter("backlight", look.Backlight);
            _species[kind] = (FloraMeshes.Build(kind), material);
        }
    }

    public void Clear()
    {
        foreach (var child in GetChildren()) child.QueueFree();
    }

    public void Show(IReadOnlyList<FloraItem> items)
    {
        Clear();
        var groups = items.GroupBy(i => (i.Kind, X: Mathf.FloorToInt(i.Transform.Origin.X / Tile), Z: Mathf.FloorToInt(i.Transform.Origin.Z / Tile)));
        foreach (var group in groups)
        {
            var (mesh, material) = _species[group.Key.Kind];
            var look = Looks[group.Key.Kind];
            var list = group.ToList();
            var multimesh = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                UseCustomData = true,
                Mesh = mesh,
                InstanceCount = list.Count,
            };
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < list.Count; i++)
            {
                multimesh.SetInstanceTransform(i, list[i].Transform);
                multimesh.SetInstanceCustomData(i, list[i].Tint);
                minY = Mathf.Min(minY, list[i].Transform.Origin.Y);
                maxY = Mathf.Max(maxY, list[i].Transform.Origin.Y);
            }
            // The node sits at the tile's middle (visibility ranges are measured from it); instances are relative to it.
            var centre = new Vector3((group.Key.X + 0.5f) * Tile, (minY + maxY) * 0.5f, (group.Key.Z + 0.5f) * Tile);
            for (int i = 0; i < list.Count; i++) multimesh.SetInstanceTransform(i, list[i].Transform.Translated(-centre));
            float half = (maxY - minY) * 0.5f + 3f;
            AddChild(new MultiMeshInstance3D
            {
                Position = centre,
                Multimesh = multimesh,
                MaterialOverride = material,
                CastShadow = Unlimited ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off,
                CustomAabb = new Aabb(new Vector3(-Tile * 0.5f - 3f, -half, -Tile * 0.5f - 3f), new Vector3(Tile + 6f, half * 2f, Tile + 6f)),
                VisibilityRangeEnd = Unlimited ? 0f : look.Range + Tile * 0.5f,
                VisibilityRangeEndMargin = 8f,
                VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Self,
            });
        }
        GD.Print($"Flora: {items.Count} plants and animals ({string.Join(", ", items.GroupBy(i => i.Kind).Select(g => $"{g.Count()} {g.Key}"))})");
    }
}
