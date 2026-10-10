using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OctoShoots.Game.Fx;

/// <summary>
/// The pufferling's body (docs/PUFFERLING-PROPOSAL.md §2), baked once: a plump fish with a blunt head, a small beak,
/// domed eyes, fluttering pectoral fins, a dorsal and an anal fin set far back, a rounded tail fan, and about 60 spines
/// lying flat along the skin. Every vertex also carries where it goes when the fish blows up into a ball
/// (pufferling.gdshader blends the two), so it inflates as a ball with its fins, eyes and beak where they belong.
/// Local frame: +X is the nose, +Y up, Z across.
///   CUSTOM0: blown-up position (xyz), part (w: 0 body, 1 eye, 2 beak, 3 pectoral, 4 dorsal, 5 anal, 6 tail, 7 spine)
///   CUSTOM1: calm attachment point (xyz) and how far out along the fin or spine (w, 0 root → 1 tip); eyes: disc uv
///   CUSTOM2: blown-up attachment point (xyz) and side (w, −1 or +1)
///   CUSTOM3: blown-up normal (xyz) and how far along the body (w, 0 nose → 1 tail)
/// </summary>
public static class PufferlingMesh
{
    const float Nose = 0.46f, StemEnd = -0.46f, Girth = 0.3f, StemRadius = 0.055f, Ball = 1.05f;
    static readonly Vector3 BallCentre = new(0.02f, 0f, 0f);
    const int Rings = 34, Around = 40, SpineCount = 62;

    /// <summary>The calm body's surface at u (0 nose → 1 the end of the tail stem) and θ round the body.</summary>
    public static Vector3 Body(float u, float a)
    {
        float r;
        if (u <= 0.4f)
        {
            float k = (0.4f - u) / 0.4f;
            r = Girth * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow(k, 2.2f)));
        }
        else
        {
            float k = (u - 0.4f) / 0.6f;
            r = StemRadius + (Girth - StemRadius) * Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Pow(k, 1.8f)), 1.25f);
        }
        float x = Mathf.Lerp(Nose, StemEnd, u);
        float s = Mathf.Sin(a), c = Mathf.Cos(a);
        // A fuller belly, a slightly narrower back.
        float y = r * s * (s < 0f ? 1.1f : 0.94f) - 0.02f;
        return new Vector3(x, y, r * c * 0.95f);
    }

    /// <summary>The same point on the ball it blows up into: the nose at the front pole, the tail stem at the back.</summary>
    public static Vector3 Blown(float u, float a)
    {
        float phi = Mathf.Pi * (u <= 0.4f ? 0.5f * u / 0.4f : 0.5f + 0.5f * (u - 0.4f) / 0.6f);
        return BallCentre + new Vector3(Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(a), Mathf.Sin(phi) * Mathf.Cos(a)) * Ball;
    }

    static Vector3 BodyNormal(float u, float a)
    {
        const float e = 0.004f;
        Vector3 du = Body(Mathf.Min(u + e, 1f), a) - Body(Mathf.Max(u - e, 0f), a);
        Vector3 da = Body(u, a + e) - Body(u, a - e);
        var n = da.Cross(du);
        return n.LengthSquared() < 1e-12f ? (u < 0.5f ? Vector3.Right : Vector3.Left) : n.Normalized();
    }

    sealed class Builder
    {
        readonly SurfaceTool _st = new();
        int _count;

        public Builder()
        {
            _st.Begin(Mesh.PrimitiveType.Triangles);
            for (int i = 0; i < 4; i++) _st.SetCustomFormat(i, SurfaceTool.CustomFormat.RgbaFloat);
        }

        public int Add(Vector3 calm, Vector3 normal, Vector3 blown, Vector3 blownNormal, int part, Vector3 attach, Vector3 blownAttach, float along, float side, float bodyU)
        {
            _st.SetNormal(normal);
            _st.SetCustom(0, new Color(blown.X, blown.Y, blown.Z, part));
            _st.SetCustom(1, new Color(attach.X, attach.Y, attach.Z, along));
            _st.SetCustom(2, new Color(blownAttach.X, blownAttach.Y, blownAttach.Z, side));
            _st.SetCustom(3, new Color(blownNormal.X, blownNormal.Y, blownNormal.Z, bodyU));
            _st.AddVertex(calm);
            return _count++;
        }

        public void Tri(int a, int b, int c)
        {
            _st.AddIndex(a);
            _st.AddIndex(b);
            _st.AddIndex(c);
        }

        public ArrayMesh Commit() => _st.Commit();
    }

    public static ArrayMesh Build()
    {
        var b = new Builder();
        BuildBody(b);
        foreach (float side in new[] { -1f, 1f })
        {
            BuildEye(b, side);
            BuildPectoral(b, side);
        }
        BuildBeak(b);
        BuildVerticalFin(b, part: 4, u: 0.73f, up: 1f);
        BuildVerticalFin(b, part: 5, u: 0.73f, up: -1f);
        BuildTail(b);
        BuildSpines(b);
        return b.Commit();
    }

    static void BuildBody(Builder b)
    {
        var index = new int[(Rings + 1) * (Around + 1)];
        for (int i = 0; i <= Rings; i++)
        for (int j = 0; j <= Around; j++)
        {
            float u = (float)i / Rings, a = (float)j / Around * Mathf.Tau;
            var calm = Body(u, a);
            var blown = Blown(u, a);
            index[i * (Around + 1) + j] = b.Add(calm, BodyNormal(u, a), blown, (blown - BallCentre).Normalized(), 0, calm, blown, 0f, 0f, u);
        }
        for (int i = 0; i < Rings; i++)
        for (int j = 0; j < Around; j++)
        {
            int p = index[i * (Around + 1) + j], q = index[(i + 1) * (Around + 1) + j], r = index[(i + 1) * (Around + 1) + j + 1], s = index[i * (Around + 1) + j + 1];
            b.Tri(p, r, q);
            b.Tri(p, s, r);
        }
    }

    /// <summary>A dome on the head's flank, looking out and a little forward; its disc coordinates draw the iris.</summary>
    static void BuildEye(Builder b, float side)
    {
        const float u = 0.2f, radius = 0.085f;
        float a = side > 0f ? 0.42f : Mathf.Pi - 0.42f;
        var at = Body(u, a);
        var blownAt = Blown(u, a);
        var n = (BodyNormal(u, a) + Vector3.Right * 0.25f).Normalized();
        var bn = ((blownAt - BallCentre).Normalized() + Vector3.Right * 0.2f).Normalized();
        Vector3 t1 = n.Cross(Vector3.Up).Normalized(), t2 = t1.Cross(n);
        Vector3 b1 = bn.Cross(Vector3.Up).Normalized(), b2 = b1.Cross(bn);
        const int rings = 6, seg = 18;
        var idx = new int[(rings + 1) * (seg + 1)];
        for (int i = 0; i <= rings; i++)
        for (int j = 0; j <= seg; j++)
        {
            float rr = (float)i / rings, ang = (float)j / seg * Mathf.Tau;
            var disc = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rr;
            float h = Mathf.Sqrt(Mathf.Max(0f, 1f - rr * rr)) * 0.6f;
            Vector3 local(Vector3 nn, Vector3 a1, Vector3 a2, float scale) => (a1 * disc.X + a2 * disc.Y) * radius * scale + nn * (h - 0.45f) * radius * scale;
            var calm = at + local(n, t1, t2, 1f);
            var blown = blownAt + local(bn, b1, b2, 1.3f);
            var normal = (n * h + (t1 * disc.X + t2 * disc.Y) * 0.6f).Normalized();
            var bnormal = (bn * h + (b1 * disc.X + b2 * disc.Y) * 0.6f).Normalized();
            idx[i * (seg + 1) + j] = b.Add(calm, normal, blown, bnormal, 1, new Vector3(disc.X, disc.Y, 0f), new Vector3(disc.X, disc.Y, 0f), 0f, side, u);
        }
        for (int i = 0; i < rings; i++)
        for (int j = 0; j < seg; j++)
        {
            int p = idx[i * (seg + 1) + j], q = idx[(i + 1) * (seg + 1) + j], r = idx[(i + 1) * (seg + 1) + j + 1], s = idx[i * (seg + 1) + j + 1];
            if (side > 0f)
            {
                b.Tri(p, q, r);
                b.Tri(p, r, s);
            }
            else
            {
                b.Tri(p, r, q);
                b.Tri(p, s, r);
            }
        }
    }

    /// <summary>The beak: two pale plates at the tip of the snout.</summary>
    static void BuildBeak(Builder b)
    {
        var at = new Vector3(Nose - 0.015f, -0.035f, 0f);
        var blownAt = BallCentre + Vector3.Right * (Ball + 0.01f) + new Vector3(0f, -0.04f, 0f);
        const int rings = 5, seg = 14;
        var idx = new int[(rings + 1) * (seg + 1)];
        for (int i = 0; i <= rings; i++)
        for (int j = 0; j <= seg; j++)
        {
            float t = (float)i / rings * Mathf.Pi, ang = (float)j / seg * Mathf.Tau;
            var dir = new Vector3(Mathf.Cos(t), Mathf.Sin(t) * Mathf.Sin(ang), Mathf.Sin(t) * Mathf.Cos(ang));
            var p = new Vector3(dir.X * 0.05f, dir.Y * 0.045f, dir.Z * 0.06f);
            idx[i * (seg + 1) + j] = b.Add(at + p, dir, blownAt + p * 1.2f, dir, 2, at, blownAt, 0f, 0f, 0f);
        }
        for (int i = 0; i < rings; i++)
        for (int j = 0; j < seg; j++)
        {
            int p = idx[i * (seg + 1) + j], q = idx[(i + 1) * (seg + 1) + j], r = idx[(i + 1) * (seg + 1) + j + 1], s = idx[i * (seg + 1) + j + 1];
            b.Tri(p, r, q);
            b.Tri(p, s, r);
        }
    }

    /// <summary>A fan (drawn from both sides): rays from a root line to a rounded edge.</summary>
    static void Fan(Builder b, int part, float side, float bodyU, Vector3 rootA, Vector3 rootB, System.Func<float, Vector3> edge, Vector3 calmAttach, Vector3 blownAttach, Vector3 blownShift, Vector3 normal)
    {
        const int rays = 10, steps = 4;
        var idx = new int[(rays + 1) * (steps + 1)];
        for (int i = 0; i <= rays; i++)
        {
            float t = (float)i / rays;
            var root = rootA.Lerp(rootB, t);
            var tip = edge(t);
            for (int k = 0; k <= steps; k++)
            {
                float f = (float)k / steps;
                var p = root.Lerp(tip, f);
                idx[i * (steps + 1) + k] = b.Add(p, normal, p + blownShift, normal, part, calmAttach, blownAttach, f, side, bodyU);
            }
        }
        for (int i = 0; i < rays; i++)
        for (int k = 0; k < steps; k++)
        {
            int p = idx[i * (steps + 1) + k], q = idx[(i + 1) * (steps + 1) + k], r = idx[(i + 1) * (steps + 1) + k + 1], s = idx[i * (steps + 1) + k + 1];
            b.Tri(p, q, r);
            b.Tri(p, r, s);
        }
    }

    /// <summary>A small fan just behind the eye, fluttering fast: the pufferling hovers and steers with these.</summary>
    static void BuildPectoral(Builder b, float side)
    {
        const float u = 0.36f;
        float a = side > 0f ? -0.15f : Mathf.Pi + 0.15f;
        var attach = Body(u, a) - new Vector3(0f, 0f, side * 0.01f);
        var blownAttach = Blown(u, a);
        var rootA = attach + new Vector3(0.02f, 0.06f, 0f);
        var rootB = attach + new Vector3(-0.02f, -0.06f, 0f);
        Vector3 Edge(float t)
        {
            float ang = Mathf.Lerp(0.9f, -0.7f, t);
            return attach + new Vector3(-0.13f + 0.04f * Mathf.Cos(ang * 2f), Mathf.Sin(ang) * 0.11f, side * 0.07f);
        }
        Fan(b, 3, side, u, rootA, rootB, Edge, attach, blownAttach, blownAttach - attach, new Vector3(0f, 0f, side));
    }

    /// <summary>The dorsal (up) or anal (down) fin: rounded, far back, fanning from side to side together.</summary>
    static void BuildVerticalFin(Builder b, int part, float u, float up)
    {
        float a = up > 0f ? Mathf.Pi * 0.5f : -Mathf.Pi * 0.5f;
        var attach = Body(u, a) - new Vector3(0f, up * 0.01f, 0f);
        var blownAttach = Blown(u, a);
        var rootA = Body(u - 0.06f, a) - new Vector3(0f, up * 0.01f, 0f);
        var rootB = Body(u + 0.09f, a) - new Vector3(0f, up * 0.01f, 0f);
        Vector3 Edge(float t)
        {
            float ang = t * Mathf.Pi;
            return rootA.Lerp(rootB, t) + new Vector3(-0.07f - 0.05f * t, up * (0.04f + 0.11f * Mathf.Sin(ang * 0.9f + 0.2f)), 0f);
        }
        Fan(b, part, 0f, u, rootA, rootB, Edge, attach, blownAttach, blownAttach - attach, Vector3.Back);
    }

    /// <summary>The tail fan on the end of the stem, upright: a rounded fan, sweeping slowly; it steers.</summary>
    static void BuildTail(Builder b)
    {
        var attach = Body(1f, 0f) with { Z = 0f };
        var blownAttach = BallCentre + Vector3.Left * (Ball - 0.02f);
        var rootA = attach + new Vector3(0.01f, 0.05f, 0f);
        var rootB = attach + new Vector3(0.01f, -0.05f, 0f);
        Vector3 Edge(float t)
        {
            float ang = Mathf.Lerp(0.95f, -0.95f, t);
            return attach + new Vector3(-0.08f - 0.16f * Mathf.Cos(ang * 0.8f), Mathf.Sin(ang) * 0.17f, 0f);
        }
        Fan(b, 6, 0f, 1f, rootA, rootB, Edge, attach, blownAttach, blownAttach - attach, Vector3.Back);
    }

    /// <summary>
    /// The spines: thin cones scattered over the body. Calm, they lie flat along the skin, pointing back; blown up,
    /// they stand straight out from the ball. The shader shrinks them to their roots when they have been fired.
    /// </summary>
    static void BuildSpines(Builder b)
    {
        var rng = new RandomNumberGenerator { Seed = 7 };
        var placed = new List<Vector2>();
        for (int tries = 0; tries < 4000 && placed.Count < SpineCount; tries++)
        {
            float u = rng.RandfRange(0.1f, 0.86f), a = rng.RandfRange(0f, Mathf.Tau);
            // Keep clear of the eyes and the fins' roots.
            var p = Body(u, a);
            bool clash = false;
            foreach (var q in placed)
                if (Body(q.X, q.Y).DistanceTo(p) < 0.075f) clash = true;
            if (clash) continue;
            if (new[] { Body(0.2f, 0.42f), Body(0.2f, Mathf.Pi - 0.42f), Body(0.36f, -0.15f), Body(0.36f, Mathf.Pi + 0.15f) }.Any(e => e.DistanceTo(p) < 0.11f)) continue;
            if (u > 0.66f && Mathf.Abs(Mathf.Sin(a)) > 0.8f) continue;
            placed.Add(new Vector2(u, a));
        }
        foreach (var (u, a) in placed.Select(q => (q.X, q.Y)))
        {
            var root = Body(u, a);
            var n = BodyNormal(u, a);
            var blownRoot = Blown(u, a);
            var bn = (blownRoot - BallCentre).Normalized();
            // Calm: lying back along the skin, lifted a hair. Blown up: straight out.
            var back = (Vector3.Left - n * n.Dot(Vector3.Left)).Normalized() * 0.75f + n * 0.25f;
            Vector3 calmTip = root + back.Normalized() * 0.07f, blownTip = blownRoot + bn * 0.3f;
            Vector3 side1 = n.Cross(back).Normalized(), side2 = n;
            Vector3 bs1 = bn.Cross(Vector3.Up).LengthSquared() > 1e-4f ? bn.Cross(Vector3.Up).Normalized() : Vector3.Forward, bs2 = bs1.Cross(bn);
            const float w = 0.016f;
            var basePts = new int[3];
            for (int k = 0; k < 3; k++)
            {
                float ang = k * Mathf.Tau / 3f;
                Vector3 off = (side1 * Mathf.Cos(ang) + side2 * Mathf.Sin(ang)) * w, boff = (bs1 * Mathf.Cos(ang) + bs2 * Mathf.Sin(ang)) * w * 1.4f;
                basePts[k] = b.Add(root + off, (off + n * 0.3f).Normalized(), blownRoot + boff, (boff + bn * 0.3f).Normalized(), 7, root, blownRoot, 0f, 0f, u);
            }
            int tip = b.Add(calmTip, n, blownTip, bn, 7, root, blownRoot, 1f, 0f, u);
            for (int k = 0; k < 3; k++) b.Tri(basePts[k], basePts[(k + 1) % 3], tip);
        }
    }
}
