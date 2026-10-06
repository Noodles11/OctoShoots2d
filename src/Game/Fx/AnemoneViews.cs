using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Terrain;
using OctoShoots.Game.Util;

namespace OctoShoots.Game.Fx;

/// <summary>
/// Every anemone of the reef in one MultiMesh: a single draw call, with the sway done in the shader.
/// Nest anemones are larger, tinted violet-pink and a little brighter, so a school's home reads from afar.
/// </summary>
public partial class AnemoneViews : Node3D
{
    static readonly Color[] Palette =
    {
        new(0.62f, 0.2f, 0.55f),  // violet-magenta (the classic clownfish host)
        new(0.78f, 0.25f, 0.45f), // rose
        new(0.2f, 0.6f, 0.45f),   // sea green
        new(0.9f, 0.5f, 0.25f),   // orange
        new(0.25f, 0.55f, 0.75f), // azure
        new(0.75f, 0.7f, 0.25f),  // yellow-green
        new(0.55f, 0.35f, 0.75f), // lavender
        new(0.85f, 0.35f, 0.3f),  // coral red
    };

    MultiMeshInstance3D? _instance;

    public void Show(IReadOnlyList<AnemoneSpot> spots)
    {
        Clear();
        if (spots.Count == 0) return;

        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/anemone.gdshader") };
        var multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseCustomData = true,
            Mesh = ReefMeshes.Anemone(),
            InstanceCount = spots.Count,
        };
        multimesh.Mesh.SurfaceSetMaterial(0, material);

        var rng = new System.Random(spots.Count * 7919);
        for (int i = 0; i < spots.Count; i++)
        {
            var spot = spots[i];
            var basis = Conv.AlignUp(spot.Up.G(), (float)rng.NextDouble() * Mathf.Tau).Scaled(Vector3.One * spot.Radius);
            multimesh.SetInstanceTransform(i, new Transform3D(basis, spot.Position.G()));
            Color body = Palette[spot.Hue % Palette.Length];
            multimesh.SetInstanceCustomData(i, new Color(body.R, body.G, body.B, spot.Nest ? 1f : 0f));
        }

        _instance = new MultiMeshInstance3D
        {
            Multimesh = multimesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            CustomAabb = new Aabb(new Vector3(-1000f, -1000f, -1000f), new Vector3(3000f, 3000f, 3000f)),
        };
        AddChild(_instance);
    }

    public void Clear()
    {
        _instance?.QueueFree();
        _instance = null;
    }
}
