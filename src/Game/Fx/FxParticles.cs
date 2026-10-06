using System;
using Godot;

namespace OctoShoots.Game.Fx;

/// <summary>
/// A small CPU particle system drawn as camera-facing quads in one MultiMesh.
/// Used for ink puffs, sparks and sand: cosmetic only, never read by the simulation.
/// </summary>
public partial class FxParticles : Node3D
{
    struct Particle
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float Life;
        public float MaxLife;
        public float Size0;
        public float Size1;
        public Color Color0;
        public Color Color1;
        public float Drag;
        public float Buoyancy;
    }

    readonly Particle[] _particles;
    int _count;
    MultiMesh _multimesh = null!;
    readonly bool _additive;
    readonly Random _random = new(1234);

    public FxParticles() : this(512, false) { }

    public FxParticles(int capacity, bool additive)
    {
        _particles = new Particle[capacity];
        _additive = additive;
    }

    public static Texture2D SoftCircle()
    {
        var gradient = new Gradient
        {
            Offsets = new[] { 0f, 0.55f, 1f },
            Colors = new[] { new Color(1, 1, 1, 1), new Color(1, 1, 1, 0.45f), new Color(1, 1, 1, 0) },
        };
        return new GradientTexture2D
        {
            Gradient = gradient,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(1f, 0.5f),
            Width = 64,
            Height = 64,
        };
    }

    public override void _Ready()
    {
        var material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = _additive ? BaseMaterial3D.BlendModeEnum.Add : BaseMaterial3D.BlendModeEnum.Mix,
            VertexColorUseAsAlbedo = true,
            AlbedoTexture = SoftCircle(),
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        _multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = new QuadMesh { Size = Vector2.One, Material = material },
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

    public float RandRange(float a, float b) => a + (b - a) * (float)_random.NextDouble();

    public Vector3 RandDir()
    {
        while (true)
        {
            var v = new Vector3(RandRange(-1, 1), RandRange(-1, 1), RandRange(-1, 1));
            float l = v.LengthSquared();
            if (l > 0.01f && l <= 1f) return v / Mathf.Sqrt(l);
        }
    }

    public void Emit(Vector3 position, Vector3 velocity, float life, float size0, float size1, Color color0, Color color1, float drag = 2f, float buoyancy = 0f)
    {
        if (_count >= _particles.Length) return;
        _particles[_count++] = new Particle
        {
            Position = position,
            Velocity = velocity,
            Life = life,
            MaxLife = life,
            Size0 = size0,
            Size1 = size1,
            Color0 = color0,
            Color1 = color1,
            Drag = drag,
            Buoyancy = buoyancy,
        };
    }

    /// <summary>A burst of particles flying out from a point, biased along a direction.</summary>
    public void Burst(Vector3 position, Vector3 dir, int count, float speed, float spread, float life, float size0, float size1, Color color0, Color color1, float drag = 3f, float buoyancy = 0f)
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 v = (dir * (1f - spread) + RandDir() * spread).Normalized() * speed * RandRange(0.4f, 1f);
            Emit(position, v, life * RandRange(0.7f, 1.2f), size0 * RandRange(0.7f, 1.3f), size1 * RandRange(0.7f, 1.3f), color0, color1, drag, buoyancy);
        }
    }

    public void Clear() => _count = 0;

    public void Tick(float dt, Basis cameraBasis)
    {
        int i = 0;
        while (i < _count)
        {
            ref var p = ref _particles[i];
            p.Life -= dt;
            if (p.Life <= 0f)
            {
                _particles[i] = _particles[--_count];
                continue;
            }
            p.Velocity *= Mathf.Max(0f, 1f - p.Drag * dt);
            p.Velocity.Y += p.Buoyancy * dt;
            p.Position += p.Velocity * dt;

            float t = 1f - p.Life / p.MaxLife;
            float size = Mathf.Lerp(p.Size0, p.Size1, t);
            _multimesh.SetInstanceTransform(i, new Transform3D(cameraBasis.Scaled(new Vector3(size, size, size)), p.Position));
            _multimesh.SetInstanceColor(i, p.Color0.Lerp(p.Color1, t));
            i++;
        }
        _multimesh.VisibleInstanceCount = _count;
    }
}
