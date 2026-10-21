using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// A creature born of the ink (docs/CORRUPTION.md): the ink it drinks is sucked into it as if by a vacuum. Dots of
/// corruption leave the very cells it drinks from (InkDraw) — nearest first, however far off the ink lies — creep, then
/// whoosh in, stretching along their flight and arcing over any rock in the way, into a blob of darkness swelling
/// low over the floor where it is born; once all the ink is in, the blob rises to her plane as the murkling
/// (SyncMurklings grows and lifts it on the same clock). A prime's draught is far bigger and its dots far more.
/// </summary>
public partial class BlightView
{
    /// <summary>When (a share of the birth) every dot is in; after it the blob rises to her plane.</summary>
    const float GatherIn = 0.82f;

    sealed class Gather
    {
        public MultiMeshInstance3D Node = null!;
        public System.Numerics.Vector2 At;
        public float Floor, Blob, Age, Life;
        public Vector3[] From = null!;
        public float[] Depart = null!, Arrive = null!, Arc = null!, Size = null!, Swirl = null!;
    }

    readonly List<Gather> _gathers = new();
    StandardMaterial3D? _dotMat;
    readonly SphereMesh _dot = new() { Radius = 1f, Height = 2f, RadialSegments = 8, Rings = 4 };

    /// <summary>
    /// A birth begins at <paramref name="at"/>: the cells it drinks (null: a disc of the given radius, an ambush's own
    /// ink), how inked they are (0..1), how long it takes and how big the blob it swells into is.
    /// </summary>
    void StartGather(System.Numerics.Vector2 at, float radius, float ink, float life, bool big, InkDraw? draw, float blob)
    {
        // Drawn after the water pass (as the ink is), so the stream stays sharp and dark wherever it flows.
        _dotMat ??= new StandardMaterial3D
        {
            AlbedoColor = new Color(0.02f, 0.01f, 0.04f, 0.97f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            RenderPriority = 1,
            Roughness = 0.15f,
            EmissionEnabled = true,
            Emission = new Color(0.22f, 0.07f, 0.4f),
            EmissionEnergyMultiplier = 0.35f,
        };
        // Where its dots come from: the cells it drinks, each as often as its share of the draught.
        var from = new List<System.Numerics.Vector2>();
        if (draw is { Cells.Count: > 0 })
        {
            int n = Mathf.Clamp(Mathf.RoundToInt(draw.Total * 3.5f), 24, 320);
            var sum = new float[draw.Cells.Count];
            float total = 0f;
            for (int k = 0; k < sum.Length; k++) sum[k] = total += draw.Amounts[k];
            for (int i = 0; i < n; i++)
            {
                float pick = GD.Randf() * total;
                int k = System.Array.BinarySearch(sum, pick);
                if (k < 0) k = ~k;
                k = Mathf.Min(k, sum.Length - 1);
                var c = CorruptionField.CellCentre(draw.Cells[k] % CorruptionField.N, draw.Cells[k] / CorruptionField.N);
                from.Add(c + new System.Numerics.Vector2(GD.Randf() - 0.5f, GD.Randf() - 0.5f) * CorruptionTuning.Cell);
            }
        }
        else
        {
            int n = Mathf.Max(big ? 40 : 8, Mathf.RoundToInt((big ? 240 : 54) * Mathf.Clamp(ink, 0f, 1f)));
            for (int i = 0; i < n; i++)
            {
                float r = radius * Mathf.Sqrt(GD.Randf()) * 0.95f, a = GD.Randf() * Mathf.Tau;
                from.Add(at + new System.Numerics.Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            }
        }
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = _dot, InstanceCount = from.Count };
        var node = new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = _dotMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(node);
        float floor = Floor(at);
        var g = new Gather
        {
            Node = node, At = at, Floor = floor, Blob = blob, Life = life,
            From = new Vector3[from.Count], Depart = new float[from.Count], Arrive = new float[from.Count],
            Arc = new float[from.Count], Size = new float[from.Count], Swirl = new float[from.Count],
        };
        float far = 0.5f;
        foreach (var p in from) far = Mathf.Max(far, System.Numerics.Vector2.Distance(p, at));
        var lo = new Vector3(at.X, floor, at.Y);
        var hi = new Vector3(at.X, LevelMap.SwimBand + 1f, at.Y);
        for (int i = 0; i < from.Count; i++)
        {
            var p = from[i];
            float d = System.Numerics.Vector2.Distance(p, at);
            g.From[i] = new Vector3(p.X, Floor(p) + 0.05f, p.Y);
            // All set off within the first part of the birth; the farther, the later in — all in by GatherIn.
            g.Depart[i] = GD.Randf() * 0.25f;
            g.Arrive[i] = g.Depart[i] + (GatherIn - g.Depart[i]) * Mathf.Clamp(0.4f + 0.6f * d / far, 0f, 1f) * (float)GD.RandRange(0.85, 1.0);
            // Over any rock between: the highest ground under its line, plus a clearance, above the stream's own height.
            float top = floor;
            for (int s = 1; s < 6; s++) top = Mathf.Max(top, Floor(System.Numerics.Vector2.Lerp(p, at, s / 6f)));
            g.Arc[i] = Mathf.Clamp(Mathf.Max(0.1f + 0.03f * d, top + 0.5f - Flow), 0f, 9f);
            // Coarser than the shards' drift, so the stream reads against it.
            g.Size[i] = (big ? 1.3f : 1f) * (float)GD.RandRange(0.035, 0.07);
            g.Swirl[i] = (GD.Randf() < 0.5f ? -1f : 1f) * (float)GD.RandRange(0.3, 0.9);
            lo = new Vector3(Mathf.Min(lo.X, p.X), Mathf.Min(lo.Y, g.From[i].Y), Mathf.Min(lo.Z, p.Y));
            hi = new Vector3(Mathf.Max(hi.X, p.X), Mathf.Max(hi.Y, Flow + g.Arc[i]), Mathf.Max(hi.Z, p.Y));
        }
        mm.CustomAabb = new Aabb(lo - Vector3.One, hi - lo + Vector3.One * 2f);
        _gathers.Add(g);
        // A small dark swirl where it is born.
        AddRipple(at, floor + 0.06f, Mathf.Min(radius, 2f), 0.2f, life * GatherIn, new Color(Ink.R, Ink.G, Ink.B, 0.6f));
    }

    /// <summary>The swelling blob's centre (low over the floor while the ink pours in), at a share of the birth.</summary>
    static float BlobHeight(float floor, float blob, float t) => floor + 0.1f + blob * 0.9f * BlobGrowth(t);

    /// <summary>How much of the blob has formed at a share of the birth (as the ink arrives).</summary>
    static float BlobGrowth(float t)
    {
        float k = Mathf.Clamp((t - 0.08f) / (GatherIn - 0.08f), 0f, 1f);
        return 0.06f + 0.94f * k * k * (3f - 2f * k);
    }

    /// <summary>The height the stream flows at, just under her plane, once sucked up off the floor.</summary>
    const float Flow = LevelMap.SwimBand - 0.3f;

    Vector3 DotAt(Gather g, int i, float t)
    {
        float u = Mathf.Clamp((t - g.Depart[i]) / Mathf.Max(g.Arrive[i] - g.Depart[i], 0.01f), 0f, 1f);
        // A vacuum: sucked up off the floor first, then a creep, then a whoosh into the blob.
        float lift = Mathf.SmoothStep(0f, 0.3f, u);
        float pull = Mathf.Pow(u, 2.6f);
        var to = new Vector3(g.At.X, BlobHeight(g.Floor, g.Blob, t), g.At.Y);
        var off = new Vector2(g.From[i].X - g.At.X, g.From[i].Z - g.At.Y).Rotated(g.Swirl[i] * pull * pull) * (1f - pull);
        float stream = Mathf.Lerp(Flow, to.Y, pull) + g.Arc[i] * Mathf.Sin(Mathf.Pi * pull);
        float y = Mathf.Lerp(g.From[i].Y, stream, lift);
        return new Vector3(g.At.X + off.X, y, g.At.Y + off.Y);
    }

    void StepGathers(float dt)
    {
        for (int k = _gathers.Count - 1; k >= 0; k--)
        {
            var g = _gathers[k];
            g.Age += dt;
            if (g.Age >= g.Life * GatherIn + 0.05f)
            {
                g.Node.QueueFree();
                _gathers.RemoveAt(k);
                continue;
            }
            float t = g.Age / g.Life, step = 0.01f;
            var mm = g.Node.Multimesh;
            for (int i = 0; i < g.From.Length; i++)
            {
                float u = Mathf.Clamp((t - g.Depart[i]) / Mathf.Max(g.Arrive[i] - g.Depart[i], 0.01f), 0f, 1f);
                var at = DotAt(g, i, t);
                // Stretched along its flight, the faster the longer; swallowed into the blob as it arrives.
                var vel = (DotAt(g, i, t + step) - at) / (step * g.Life);
                float speed = vel.Length();
                float size = g.Size[i] * (1f - Mathf.SmoothStep(0.88f, 1f, u));
                var basis = Basis.Identity.Scaled(Vector3.One * Mathf.Max(size, 0.001f));
                if (speed > 0.05f)
                {
                    var dir = vel / speed;
                    var up = Mathf.Abs(dir.Y) > 0.95f ? Vector3.Right : Vector3.Up;
                    basis = Basis.LookingAt(dir, up) * Basis.FromScale(new Vector3(size, size, size * (1f + Mathf.Min(speed * 0.35f, 9f))));
                }
                mm.SetInstanceTransform(i, new Transform3D(size <= 0.0005f ? Basis.Identity.Scaled(Vector3.One * 0.0001f) : basis, at));
            }
        }
    }
}
