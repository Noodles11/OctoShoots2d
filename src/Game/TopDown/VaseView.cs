using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Sunken amphorae on screen (DESIGN-TOPDOWN §12.2): terracotta pots standing on the seabed, painted with dark glaze
/// bands, algae creeping up their feet, each leaning a little. A weak hit rocks one on its foot; a strong one shatters
/// it — shards burst out and sink to the sand, a puff of silt and a few bubbles rise.
/// </summary>
public partial class VaseView : Node3D
{
    const int Rings = 26, Segments = 24;

    sealed class Pot
    {
        public PlaneVase Vase = null!;
        public Node3D Root = null!;
        public float Floor;
        /// <summary>The rock after a hit: a damped swing about an axis on the sand.</summary>
        public Vector3 Axis;
        public float Angle, Spin;
    }

    sealed class Shard
    {
        public MeshInstance3D Mesh = null!;
        public Vector3 Velocity, SpinAxis;
        public float Spin, Floor, Age;
        public bool Landed;
    }

    readonly List<Pot> _pots = new();
    readonly List<Shard> _shards = new();
    readonly Dictionary<PlaneVase, Pot> _byVase = new();
    readonly RandomNumberGenerator _rng = new();
    StandardMaterial3D _clay = null!, _shardClay = null!;
    Mesh _shardMesh = null!;

    public override void _Ready()
    {
        _clay = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            Roughness = 0.72f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        _shardMesh = ShardMesh();
        _shardClay = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 0.75f };
    }

    /// <summary>A new level's pots.</summary>
    public void Show(PlaneWorld world)
    {
        foreach (var pot in _pots) pot.Root.QueueFree();
        foreach (var s in _shards) s.Mesh.QueueFree();
        _pots.Clear();
        _shards.Clear();
        _byVase.Clear();
        foreach (var vase in world.Vases)
        {
            float floor = world.Map.HeightAt(vase.Position);
            // Tall enough to stand up through her swim plane.
            float height = Mathf.Max(1.4f, LevelMap.SwimBand + 0.45f - floor);
            var root = new Node3D { Position = new Vector3(vase.Position.X, floor - 0.08f, vase.Position.Y) };
            var body = new MeshInstance3D { Mesh = PotMesh(vase.Shape, vase.Radius, height, vase.Seed), MaterialOverride = _clay };
            // Its own lean and turn.
            float lean = 0.05f + 0.07f * Mathf.PosMod(vase.Seed * 0.37f, 1f);
            body.Rotation = new Vector3(lean * Mathf.Cos(vase.Seed), vase.Seed, lean * Mathf.Sin(vase.Seed));
            root.AddChild(body);
            AddChild(root);
            var pot = new Pot { Vase = vase, Root = root, Floor = floor };
            _pots.Add(pot);
            _byVase[vase] = pot;
        }
    }

    /// <summary>A weak hit: the pot rocks on its foot, away from the blow.</summary>
    public void Hit(PlaneWorld world, System.Numerics.Vector2 at, System.Numerics.Vector2 dir)
    {
        var pot = Find(world, at);
        if (pot is null) return;
        var d = new Vector3(dir.X, 0f, dir.Y);
        if (d.LengthSquared() < 1e-6f) d = Vector3.Right;
        pot.Axis = Vector3.Up.Cross(d.Normalized()).Normalized();
        pot.Spin += 2.4f;
    }

    /// <summary>A pot shatters: shards fly out (mostly along the blow) and sink to the sand; silt and bubbles rise.</summary>
    public void Break(PlaneWorld world, System.Numerics.Vector2 at, System.Numerics.Vector2 dir)
    {
        var pot = Find(world, at);
        if (pot is null) return;
        pot.Root.Visible = false;
        var push = new Vector3(dir.X, 0f, dir.Y);
        float height = Mathf.Max(1.4f, LevelMap.SwimBand + 0.45f - pot.Floor);
        for (int i = 0; i < 16; i++)
        {
            float a = _rng.RandfRange(0f, Mathf.Tau), y = _rng.RandfRange(0.15f, 0.9f) * height;
            var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            var mesh = new MeshInstance3D
            {
                Mesh = _shardMesh,
                MaterialOverride = _shardClay,
                Position = new Vector3(at.X, pot.Floor, at.Y) + outward * pot.Vase.Radius * 0.8f + Vector3.Up * y,
                Scale = Vector3.One * _rng.RandfRange(0.7f, 1.4f) * (pot.Vase.Radius / 0.45f),
                Rotation = new Vector3(_rng.RandfRange(0f, Mathf.Tau), a, _rng.RandfRange(0f, Mathf.Tau)),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(mesh);
            _shards.Add(new Shard
            {
                Mesh = mesh,
                Velocity = outward * _rng.RandfRange(1.5f, 3.5f) + push * _rng.RandfRange(0.8f, 2.4f) + Vector3.Up * _rng.RandfRange(0.4f, 1.6f),
                SpinAxis = new Vector3(_rng.RandfRange(-1f, 1f), _rng.RandfRange(-1f, 1f), _rng.RandfRange(-1f, 1f)).Normalized(),
                Spin = _rng.RandfRange(4f, 10f),
                Floor = world.Map.HeightAt(new System.Numerics.Vector2(mesh.Position.X, mesh.Position.Z)) + 0.03f,
            });
        }
        // A puff of silt off the sand, and the air that was in it.
        var silt = new CpuParticles3D
        {
            Position = new Vector3(at.X, pot.Floor + 0.3f, at.Y),
            Emitting = true,
            OneShot = true,
            Explosiveness = 0.9f,
            Amount = 22,
            Lifetime = 1.8f,
            Mesh = new SphereMesh { Radius = 0.16f, Height = 0.32f, RadialSegments = 6, Rings = 3 },
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = 0.5f,
            Direction = Vector3.Up,
            Spread = 70f,
            InitialVelocityMin = 0.6f,
            InitialVelocityMax = 1.8f,
            Gravity = new Vector3(0f, 0.15f, 0f),
            DampingMin = 1.5f,
            DampingMax = 2.5f,
            ScaleAmountMin = 0.6f,
            ScaleAmountMax = 1.6f,
            ColorRamp = new Gradient { Colors = new[] { new Color(0.62f, 0.55f, 0.4f, 0.32f), new Color(0.62f, 0.55f, 0.4f, 0f) }, Offsets = new[] { 0f, 1f } },
            MaterialOverride = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(silt);
        GetTree().CreateTimer(2f).Timeout += silt.QueueFree;
    }

    Pot? Find(PlaneWorld world, System.Numerics.Vector2 at)
    {
        Pot? best = null;
        float bestD = 0.5f;
        foreach (var pot in _pots)
        {
            float d = System.Numerics.Vector2.Distance(pot.Vase.Position, at);
            if (d < bestD) (best, bestD) = (pot, d);
        }
        return best;
    }

    public void Sync(float dt)
    {
        foreach (var pot in _pots)
        {
            if (pot.Spin == 0f && pot.Angle == 0f) continue;
            // A damped swing back to rest.
            pot.Spin += (-pot.Angle * 70f - pot.Spin * 5f) * dt;
            pot.Angle += pot.Spin * dt;
            if (Mathf.Abs(pot.Angle) < 1e-4f && Mathf.Abs(pot.Spin) < 1e-3f) pot.Angle = pot.Spin = 0f;
            pot.Root.Basis = pot.Axis.LengthSquared() > 0.5f ? new Basis(pot.Axis, pot.Angle * 0.12f) : Basis.Identity;
        }
        for (int i = _shards.Count - 1; i >= 0; i--)
        {
            var s = _shards[i];
            s.Age += dt;
            if (!s.Landed)
            {
                // Through water: drag, a slow sink, tumbling.
                s.Velocity = s.Velocity * Mathf.Exp(-2.2f * dt) + Vector3.Down * 3f * dt;
                s.Mesh.Position += s.Velocity * dt;
                s.Mesh.Rotate(s.SpinAxis, s.Spin * dt);
                if (s.Mesh.Position.Y <= s.Floor)
                {
                    s.Mesh.Position = s.Mesh.Position with { Y = s.Floor };
                    s.Landed = true;
                }
            }
            // They lie on the sand a while, then fade away.
            if (s.Age > 6f) s.Mesh.Transparency = Mathf.Clamp((s.Age - 6f) / 1.5f, 0f, 1f);
            if (s.Age > 7.5f)
            {
                s.Mesh.QueueFree();
                _shards.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// A pot, turned on a lathe from its shape's profile: a tall amphora with two handles, a round jar, or a squat pot.
    /// Painted in the vertex colours — terracotta, black glaze bands, a cream band round the shoulder, algae creeping up
    /// the foot — with the inside dark.
    /// </summary>
    static ArrayMesh PotMesh(int shape, float radius, float height, float seed)
    {
        float Profile(float t) => shape switch
        {
            0 => Smooth(t, new[] { (0f, 0.32f), (0.06f, 0.36f), (0.36f, 1f), (0.66f, 0.8f), (0.82f, 0.3f), (0.95f, 0.32f), (1f, 0.42f) }),
            1 => Smooth(t, new[] { (0f, 0.5f), (0.08f, 0.62f), (0.45f, 1f), (0.8f, 0.7f), (0.9f, 0.52f), (1f, 0.62f) }),
            _ => Smooth(t, new[] { (0f, 0.6f), (0.1f, 0.78f), (0.4f, 1f), (0.75f, 0.92f), (0.92f, 0.68f), (1f, 0.74f) }),
        };
        var terracotta = new Color(0.72f, 0.38f, 0.22f).Lerp(new Color(0.8f, 0.5f, 0.3f), Mathf.PosMod(seed * 0.21f, 1f));
        var glaze = new Color(0.13f, 0.07f, 0.05f);
        var cream = new Color(0.92f, 0.8f, 0.6f);
        var algae = new Color(0.42f, 0.55f, 0.3f);
        Color Paint(float t, float a)
        {
            var c = terracotta;
            // Glaze bands, and a cream shoulder band with a simple wave painted on it.
            if (t is > 0.18f and < 0.22f || t is > 0.3f and < 0.32f || t is > 0.88f and < 0.92f) c = glaze;
            if (t is > 0.5f and < 0.62f) c = Mathf.Sin(a * 8f + (t - 0.56f) * 40f) > 0.35f ? glaze : cream;
            // Algae and silt creeping up from the sand.
            c = c.Lerp(algae, Mathf.Clamp(1f - t / 0.16f, 0f, 1f) * 0.6f);
            return c;
        }

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        var pos = new Vector3[Rings + 1, Segments + 1];
        var nrm = new Vector3[Rings + 1, Segments + 1];
        for (int i = 0; i <= Rings; i++)
        for (int j = 0; j <= Segments; j++)
        {
            float t = (float)i / Rings, a = (float)j / Segments * Mathf.Tau;
            float r = Profile(t) * radius;
            pos[i, j] = new Vector3(Mathf.Cos(a) * r, t * height, Mathf.Sin(a) * r);
            // Smooth shading: along the profile's slope. Seen from outside these triangles are back faces of a two-sided
            // material, whose normals the renderer flips, so they are given pointing in.
            float dt = 1f / Rings;
            float slope = (Profile(Mathf.Min(t + dt, 1f)) - Profile(Mathf.Max(t - dt, 0f))) * radius / (2f * dt * height);
            nrm[i, j] = -new Vector3(Mathf.Cos(a), -slope, Mathf.Sin(a)).Normalized();
        }
        for (int i = 0; i < Rings; i++)
        for (int j = 0; j < Segments; j++)
        {
            float t = (i + 0.5f) / Rings, a = (j + 0.5f) / Segments * Mathf.Tau;
            // The top rings' inside reads dark (the mouth).
            st.SetColor(i == Rings - 1 ? Paint(t, a).Darkened(0.3f) : Paint(t, a));
            void V(int ii, int jj)
            {
                st.SetNormal(nrm[ii, jj]);
                st.AddVertex(pos[ii, jj]);
            }
            V(i, j);
            V(i + 1, j);
            V(i + 1, j + 1);
            V(i, j);
            V(i + 1, j + 1);
            V(i, j + 1);
        }
        // A dark disc down inside the mouth.
        float mouth = Profile(1f) * radius * 0.92f;
        var floorOfMouth = new Vector3(0f, height * 0.86f, 0f);
        for (int j = 0; j < Segments; j++)
        {
            float a0 = (float)j / Segments * Mathf.Tau, a1 = (float)(j + 1) / Segments * Mathf.Tau;
            st.SetColor(new Color(0.05f, 0.03f, 0.03f));
            st.SetNormal(Vector3.Up);
            st.AddVertex(floorOfMouth);
            st.AddVertex(floorOfMouth + new Vector3(Mathf.Cos(a1) * mouth, 0f, Mathf.Sin(a1) * mouth));
            st.AddVertex(floorOfMouth + new Vector3(Mathf.Cos(a0) * mouth, 0f, Mathf.Sin(a0) * mouth));
        }
        // The amphora's two handles: arcs from neck to shoulder.
        if (shape == 0)
            for (int side = 0; side < 2; side++)
            {
                float a = side * Mathf.Pi;
                var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var across = new Vector3(-outward.Z, 0f, outward.X);
                const int steps = 10;
                float w = radius * 0.09f;
                for (int k = 0; k < steps; k++)
                {
                    Vector3 At(float u)
                    {
                        float t = Mathf.Lerp(0.88f, 0.66f, u);
                        float bulge = Mathf.Sin(u * Mathf.Pi) * radius * 0.42f;
                        return outward * (Profile(t) * radius + bulge) + Vector3.Up * t * height;
                    }
                    Vector3 a0 = At((float)k / steps), a1 = At((float)(k + 1) / steps);
                    st.SetColor(terracotta.Darkened(0.1f));
                    st.SetNormal(outward);
                    st.AddVertex(a0 - across * w);
                    st.AddVertex(a1 - across * w);
                    st.AddVertex(a1 + across * w);
                    st.AddVertex(a0 - across * w);
                    st.AddVertex(a1 + across * w);
                    st.AddVertex(a0 + across * w);
                }
            }
        return st.Commit();
    }

    /// <summary>A smooth curve through (t, value) keys.</summary>
    static float Smooth(float t, (float T, float V)[] keys)
    {
        for (int i = 1; i < keys.Length; i++)
        {
            if (t > keys[i].T) continue;
            float k = (t - keys[i - 1].T) / Mathf.Max(keys[i].T - keys[i - 1].T, 1e-4f);
            return Mathf.Lerp(keys[i - 1].V, keys[i].V, k * k * (3f - 2f * k));
        }
        return keys[^1].V;
    }

    /// <summary>A shard: a small curved sliver of the pot's wall, terracotta out, dark in.</summary>
    static ArrayMesh ShardMesh()
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        var pts = new[] { new Vector3(-0.12f, 0f, -0.07f), new Vector3(0.1f, 0.02f, -0.09f), new Vector3(0.13f, 0.01f, 0.06f), new Vector3(-0.03f, 0.03f, 0.1f), new Vector3(-0.14f, 0.01f, 0.03f) };
        var centre = new Vector3(0f, 0.03f, 0f);
        for (int i = 0; i < pts.Length; i++)
        {
            // Two faces, each seen from its own side (clockwise is the front): the paler, rougher clay of the inside
            // underneath, the glazed terracotta on top.
            st.SetColor(new Color(0.8f, 0.56f, 0.4f));
            st.SetNormal(Vector3.Down);
            st.AddVertex(centre);
            st.AddVertex(pts[(i + 1) % pts.Length]);
            st.AddVertex(pts[i]);
            st.SetColor(new Color(0.74f, 0.42f, 0.25f));
            st.SetNormal(Vector3.Up);
            st.AddVertex(centre + Vector3.Up * 0.012f);
            st.AddVertex(pts[i] + Vector3.Up * 0.012f);
            st.AddVertex(pts[(i + 1) % pts.Length] + Vector3.Up * 0.012f);
        }
        return st.Commit();
    }
}
