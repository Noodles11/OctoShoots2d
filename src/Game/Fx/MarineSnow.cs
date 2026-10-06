using System;
using Godot;

namespace OctoShoots.Game.Fx;

/// <summary>Drifting specks in a box that wraps around the camera: gives the water parallax so speed reads.</summary>
public partial class MarineSnow : Node3D
{
    const int Count = 700;
    const float Half = 9f;

    readonly Vector3[] _positions = new Vector3[Count];
    readonly float[] _phase = new float[Count];
    MultiMesh _multimesh = null!;
    float _time;

    public override void _Ready()
    {
        var random = new Random(77);
        for (int i = 0; i < Count; i++)
        {
            _positions[i] = new Vector3(
                (float)random.NextDouble() * 2f * Half - Half,
                (float)random.NextDouble() * 2f * Half - Half,
                (float)random.NextDouble() * 2f * Half - Half);
            _phase[i] = (float)random.NextDouble() * 10f;
        }

        // Faint in the shade, glittering where a sunbeam catches them (marine_snow.gdshader).
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/marine_snow.gdshader") };
        material.SetShaderParameter("speck", FxParticles.SoftCircle());
        material.SetShaderParameter("fade_far", Half);
        _multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = new QuadMesh { Size = new Vector2(0.035f, 0.035f), Material = material },
            InstanceCount = Count,
        };
        AddChild(new MultiMeshInstance3D
        {
            Multimesh = _multimesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            CustomAabb = new Aabb(new Vector3(-500, -500, -500), new Vector3(1000, 1000, 1000)),
        });
    }

    public void Tick(float dt, Transform3D camera)
    {
        _time += dt;
        Vector3 c = camera.Origin;
        for (int i = 0; i < Count; i++)
        {
            Vector3 p = _positions[i];
            p.Y -= 0.04f * dt;
            p.X += Mathf.Sin(_time * 0.3f + _phase[i]) * 0.03f * dt;
            p = c + Wrap(p - c);
            _positions[i] = p;
            _multimesh.SetInstanceTransform(i, new Transform3D(camera.Basis, p));
        }
    }

    static Vector3 Wrap(Vector3 d) => new(WrapAxis(d.X), WrapAxis(d.Y), WrapAxis(d.Z));

    static float WrapAxis(float v) => Mathf.PosMod(v + Half, 2f * Half) - Half;
}
