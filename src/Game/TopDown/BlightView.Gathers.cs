using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// A murkling budding (docs/CORRUPTION.md): the ink round it gathers into it. Dots of corruption over its disc draw in
/// to the middle in a slow swirl, then rise up together to her plane, where they close into the sphere that is the
/// murkling (SyncMurklings grows it as they arrive). A prime's disc is far wider and its dots far more.
/// </summary>
public partial class BlightView
{
    /// <summary>When the dots have drawn in (as a share of the bud) and begin to rise.</summary>
    const float GatherIn = 0.6f;

    sealed class Gather
    {
        public MultiMeshInstance3D Node = null!;
        public System.Numerics.Vector2 At;
        public float Floor, Radius, Age, Life;
        public float[] R = null!, A = null!, Size = null!, Lag = null!;
    }

    readonly List<Gather> _gathers = new();
    StandardMaterial3D? _dotMat;
    readonly SphereMesh _dot = new() { Radius = 1f, Height = 2f, RadialSegments = 8, Rings = 4 };

    /// <summary>A bud begins: its disc (radius), how inked it is (0..1) and how long it takes.</summary>
    void StartGather(System.Numerics.Vector2 at, float radius, float ink, float life, bool prime)
    {
        _dotMat ??= new StandardMaterial3D
        {
            AlbedoColor = new Color(0.02f, 0.01f, 0.04f),
            Roughness = 0.15f,
            EmissionEnabled = true,
            Emission = new Color(0.22f, 0.07f, 0.4f),
            EmissionEnergyMultiplier = 0.6f,
        };
        int n = Mathf.Max(prime ? 40 : 8, Mathf.RoundToInt((prime ? 240 : 54) * Mathf.Clamp(ink, 0f, 1f)));
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = _dot, InstanceCount = n };
        var node = new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = _dotMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(node);
        var g = new Gather
        {
            Node = node, At = at, Floor = Floor(at), Radius = radius, Life = life,
            R = new float[n], A = new float[n], Size = new float[n], Lag = new float[n],
        };
        for (int i = 0; i < n; i++)
        {
            // Spread over the disc (evenly by area), each its own size and a little lag, so they arrive as a stream.
            g.R[i] = radius * Mathf.Sqrt(GD.Randf()) * 0.95f;
            g.A[i] = GD.Randf() * Mathf.Tau;
            g.Size[i] = (prime ? 1.6f : 1f) * (float)GD.RandRange(0.035, 0.08);
            g.Lag[i] = GD.Randf() * 0.15f;
        }
        _gathers.Add(g);
        // A dark ring closing in over the disc as it is drunk up.
        AddRipple(at, g.Floor + 0.06f, radius, 0.2f, life * GatherIn, new Color(Ink.R, Ink.G, Ink.B, 0.8f));
    }

    void StepGathers(float dt)
    {
        for (int k = _gathers.Count - 1; k >= 0; k--)
        {
            var g = _gathers[k];
            g.Age += dt;
            if (g.Age >= g.Life)
            {
                g.Node.QueueFree();
                _gathers.RemoveAt(k);
                continue;
            }
            float t = g.Age / g.Life;
            var mm = g.Node.Multimesh;
            for (int i = 0; i < g.R.Length; i++)
            {
                // Drawing in: from where it lay to the middle, faster as it nears, turning about it.
                float u = Mathf.Clamp((t - g.Lag[i]) / (GatherIn - g.Lag[i]), 0f, 1f);
                float pull = u * u * (3f - 2f * u);
                float r = g.R[i] * Mathf.Pow(1f - pull, 1.3f);
                float a = g.A[i] + pull * 1.6f;
                float y = g.Floor + 0.06f + 0.1f * pull;
                float size = g.Size[i];
                // Rising: up together to her plane, tightening into the forming sphere and closing into it.
                float v = Mathf.Clamp((t - GatherIn - g.Lag[i] * 0.5f) / (1f - GatherIn - 0.08f), 0f, 1f);
                if (v > 0f)
                {
                    float e = v * v * (3f - 2f * v);
                    y = Mathf.Lerp(y, LevelMap.SwimBand, e);
                    r = Mathf.Max(r, 0.15f * g.Radius / 3.5f * (1f - e) + 0.02f) * (1f - e * 0.8f);
                    a += v * 4f;
                    size *= 1f - e;
                }
                var at = new Vector3(g.At.X + Mathf.Cos(a) * r, y, g.At.Y + Mathf.Sin(a) * r);
                mm.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(Vector3.One * Mathf.Max(size, 0.001f)), at));
            }
            mm.CustomAabb = new Aabb(new Vector3(g.At.X - g.Radius - 1f, g.Floor - 1f, g.At.Y - g.Radius - 1f), new Vector3(g.Radius * 2f + 2f, LevelMap.SwimBand - g.Floor + 3f, g.Radius * 2f + 2f));
        }
    }
}
