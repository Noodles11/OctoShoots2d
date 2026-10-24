using System;
using Godot;

namespace OctoShoots.Game.Fx;

/// <summary>
/// Magic particles: tiny fine-tip star drawings (magic_particle.gdshader), a "+" or a "*" in white or ecru, that pops in with a little
/// overshoot, twirls (spinning down), drifts (or, given gravity, is thrown and falls like dust), and at the end pops — a
/// quick swell as it goes. Drawn as
/// camera-facing quads in one MultiMesh, turned about the view for the twirl. Cosmetic only.
/// </summary>
public partial class MagicParticles : Node3D
{
    struct Particle
    {
        public Vector3 Position, Velocity;
        public float Life, MaxLife, Size, Angle, Spin, Gravity, Drag;
        public Color Color;
        public bool Star;
    }

    readonly Particle[] _particles;
    int _count;
    MultiMesh _multimesh = null!;
    readonly Random _random = new(4242);

    /// <summary>The inks: white and a warm ecru.</summary>
    public static readonly Color White = new(1f, 0.99f, 0.96f), Ecru = new(0.94f, 0.88f, 0.74f);

    public MagicParticles(int capacity) => _particles = new Particle[capacity];

    public float RandRange(float a, float b) => a + (b - a) * (float)_random.NextDouble();

    public override void _Ready()
    {
        _multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            UseCustomData = true,
            Mesh = new QuadMesh { Size = Vector2.One, Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/magic_particle.gdshader") } },
            InstanceCount = _particles.Length,
            VisibleInstanceCount = 0,
        };
        AddChild(new MultiMeshInstance3D
        {
            Multimesh = _multimesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            CustomAabb = new Aabb(new Vector3(-500, -500, -500), new Vector3(1000, 1000, 1000)),
        });
    }

    /// <summary>
    /// A particle here, drifting so, for this long, this big (metres across); a star or a cross, white or ecru, at random.
    /// With gravity it falls (dust thrown up by a blast), its water drag slowing it as it goes.
    /// </summary>
    public void Emit(Vector3 at, Vector3 velocity, float life, float size, float gravity = 0f, float drag = 2.5f)
    {
        if (_count >= _particles.Length) return;
        _particles[_count++] = new Particle
        {
            Position = at,
            Velocity = velocity,
            Life = life,
            MaxLife = life,
            Size = size,
            Gravity = gravity,
            Drag = drag,
            Angle = RandRange(0f, Mathf.Tau),
            Spin = RandRange(4f, 9f) * (_random.NextDouble() < 0.5 ? -1f : 1f),
            Color = _random.NextDouble() < 0.5 ? White : Ecru,
            Star = _random.NextDouble() < 0.55,
        };
    }

    public void Clear() => _count = 0;

    public void Tick(float dt, Basis cameraBasis)
    {
        int i = 0;
        while (i < _count)
        {
            ref var s = ref _particles[i];
            s.Life -= dt;
            if (s.Life <= 0f)
            {
                _particles[i] = _particles[--_count];
                continue;
            }
            s.Velocity *= Mathf.Max(0f, 1f - s.Drag * dt);
            s.Velocity.Y -= s.Gravity * dt;
            s.Position += s.Velocity * dt;
            float t = 1f - s.Life / s.MaxLife;
            // The twirl, spinning down.
            s.Angle += s.Spin * dt * (1f - 0.6f * t);
            // Popping in with an overshoot; holding; then the pop — a quick swell as it fades out.
            float scale, alpha = 1f;
            if (t < 0.18f)
            {
                float u = t / 0.18f;
                scale = 1f + 0.35f * Mathf.Sin(u * Mathf.Pi) - (1f - u) * (1f - u);
            }
            else if (t < 0.8f) scale = 1f;
            else
            {
                float u = (t - 0.8f) / 0.2f;
                scale = 1f + 0.7f * u;
                alpha = 1f - u * u;
            }
            float size = s.Size * Mathf.Max(scale, 0.01f);
            var basis = cameraBasis * new Basis(Vector3.Back, s.Angle);
            _multimesh.SetInstanceTransform(i, new Transform3D(basis.Scaled(new Vector3(size, size, size)), s.Position));
            _multimesh.SetInstanceColor(i, s.Color with { A = alpha });
            _multimesh.SetInstanceCustomData(i, new Color(s.Star ? 1f : 0f, 0f, 0f, 0f));
            i++;
        }
        _multimesh.VisibleInstanceCount = _count;
    }
}
