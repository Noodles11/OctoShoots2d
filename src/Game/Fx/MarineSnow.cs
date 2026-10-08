using System;
using Godot;

namespace OctoShoots.Game.Fx;

/// <summary>
/// Drifting specks in a box that wraps around a point (the camera, or what it looks at): gives the water parallax so
/// movement reads, and the specks glitter where they cross a sunbeam (THEME-BIBLE §6.9: marine snow between camera
/// and level).
/// </summary>
public partial class MarineSnow : Node3D
{
    readonly int Count;
    readonly Vector3 Half;
    readonly float _size;

    readonly Vector3[] _positions;
    readonly float[] _phase;

    public MarineSnow() : this(700, new Vector3(9f, 9f, 9f), 0.035f) { }

    /// <param name="half">Half the box's size on each axis.</param>
    /// <param name="size">A speck's size (metres).</param>
    public MarineSnow(int count, Vector3 half, float size)
    {
        Count = count;
        Half = half;
        _size = size;
        _positions = new Vector3[count];
        _phase = new float[count];
    }
    MultiMesh _multimesh = null!;
    float _time;

    public override void _Ready()
    {
        var random = new Random(77);
        for (int i = 0; i < Count; i++)
        {
            _positions[i] = new Vector3(
                ((float)random.NextDouble() * 2f - 1f) * Half.X,
                ((float)random.NextDouble() * 2f - 1f) * Half.Y,
                ((float)random.NextDouble() * 2f - 1f) * Half.Z);
            _phase[i] = (float)random.NextDouble() * 10f;
        }

        // Faint in the shade, glittering where a sunbeam catches them (marine_snow.gdshader).
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/marine_snow.gdshader") };
        material.SetShaderParameter("speck", FxParticles.SoftCircle());
        material.SetShaderParameter("fade_far", Mathf.Max(Half.X, Mathf.Max(Half.Y, Half.Z)) * 6f);
        _multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = new QuadMesh { Size = new Vector2(_size, _size), Material = material },
            InstanceCount = Count,
        };
        AddChild(new MultiMeshInstance3D
        {
            Multimesh = _multimesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            CustomAabb = new Aabb(new Vector3(-500, -500, -500), new Vector3(1000, 1000, 1000)),
        });
    }

    public void Tick(float dt, Transform3D camera) => Tick(dt, camera.Origin, camera.Basis);

    /// <summary>Drifts the specks and wraps them around <paramref name="centre"/>, facing the camera.</summary>
    public void Tick(float dt, Vector3 centre, Basis facing)
    {
        _time += dt;
        Vector3 c = centre;
        for (int i = 0; i < Count; i++)
        {
            Vector3 p = _positions[i];
            p.Y -= 0.04f * dt;
            p.X += Mathf.Sin(_time * 0.3f + _phase[i]) * 0.03f * dt;
            p = c + Wrap(p - c);
            _positions[i] = p;
            _multimesh.SetInstanceTransform(i, new Transform3D(facing.Scaled(Vector3.One * (0.6f + 0.8f * Mathf.PosMod(_phase[i] * 0.37f, 1f))), p));
        }
    }

    Vector3 Wrap(Vector3 d) => new(WrapAxis(d.X, Half.X), WrapAxis(d.Y, Half.Y), WrapAxis(d.Z, Half.Z));

    static float WrapAxis(float v, float half) => Mathf.PosMod(v + half, 2f * half) - half;
}
