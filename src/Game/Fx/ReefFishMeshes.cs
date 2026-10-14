using System;
using Godot;

namespace OctoShoots.Game.Fx;

/// <summary>
/// The reef fish's bodies (docs/REEF-FISH-PROPOSAL.md §4), baked once per kind: a body of revolution flattened to the
/// species' cross-section, domed eyes, fluttering pectoral and pelvic fins, dorsal and anal fins that ripple, and the
/// species' tail (a clownfish's rounded fan, a tang's broad truncate tail, a chromis's deep fork). reef_fish.gdshader
/// paints the patterns and swims them. Local frame: +X is the nose, +Y up, Z across.
///   CUSTOM0: attachment point (xyz: a fin's root, a body vertex itself), part (w: 0 body, 1 eye, 3 pectoral, 4 dorsal,
///            5 anal, 6 tail, 7 pelvic)
///   CUSTOM1: how far out along the fin (x, 0 root → 1 edge), side (y, −1 or +1), how far along the body (z, 0 nose → 1
///            the end of the tail stem), and the fin's own position along its root (w, 0 front → 1 back)
///   CUSTOM2: the eye's disc coordinates (xy); on the body, where round it the vertex lies (z: sin θ, −1 belly → +1
///            back; w: cos θ, the side)
/// </summary>
public static class ReefFishMeshes
{
    public enum Tail { Rounded, Truncate, Forked }

    /// <summary>One species' shape. Lengths in metres; the fins' sizes as fractions of the body length.</summary>
    public sealed record Spec(
        float Length, float Height, float Width, float Peak, float Stem, float NoseK, float Belly,
        float DorsalFrom, float DorsalTo, float DorsalHeight, float DorsalNotch,
        float AnalFrom, float AnalTo, float AnalHeight,
        Tail TailKind, float TailLength, float TailSpan, float Pectoral, float Pelvic,
        float EyeU, float EyeAngle, float EyeRadius);

    /// <summary>Clownfish (Amphiprion ocellaris): stout and rounded, a notched dorsal, a rounded tail fan.</summary>
    public static readonly Spec Clownfish = new(
        Length: 0.5f, Height: 0.115f, Width: 0.066f, Peak: 0.36f, Stem: 0.3f, NoseK: 2.1f, Belly: 1.05f,
        DorsalFrom: 0.22f, DorsalTo: 0.84f, DorsalHeight: 0.15f, DorsalNotch: 0.5f,
        AnalFrom: 0.58f, AnalTo: 0.84f, AnalHeight: 0.13f,
        Tail.Rounded, TailLength: 0.3f, TailSpan: 0.22f, Pectoral: 0.2f, Pelvic: 0.11f,
        EyeU: 0.15f, EyeAngle: 0.42f, EyeRadius: 0.029f);

    /// <summary>Blue tang (Paracanthurus hepatus): a tall oval, flattened side to side, long fins, a broad tail.</summary>
    public static readonly Spec BlueTang = new(
        Length: 0.7f, Height: 0.2f, Width: 0.06f, Peak: 0.4f, Stem: 0.16f, NoseK: 1.7f, Belly: 1.0f,
        DorsalFrom: 0.2f, DorsalTo: 0.86f, DorsalHeight: 0.1f, DorsalNotch: 0f,
        AnalFrom: 0.45f, AnalTo: 0.86f, AnalHeight: 0.09f,
        Tail.Truncate, TailLength: 0.22f, TailSpan: 0.3f, Pectoral: 0.2f, Pelvic: 0.08f,
        EyeU: 0.14f, EyeAngle: 0.5f, EyeRadius: 0.031f);

    /// <summary>Blue-green chromis (Chromis viridis): small and slim, a deeply forked tail.</summary>
    public static readonly Spec Chromis = new(
        Length: 0.26f, Height: 0.065f, Width: 0.028f, Peak: 0.38f, Stem: 0.22f, NoseK: 1.9f, Belly: 1.0f,
        DorsalFrom: 0.28f, DorsalTo: 0.8f, DorsalHeight: 0.18f, DorsalNotch: 0f,
        AnalFrom: 0.58f, AnalTo: 0.8f, AnalHeight: 0.16f,
        Tail.Forked, TailLength: 0.36f, TailSpan: 0.42f, Pectoral: 0.2f, Pelvic: 0.1f,
        EyeU: 0.15f, EyeAngle: 0.35f, EyeRadius: 0.017f);

    /// <summary>The body's rings along it and segments round it; a small fish (seen only small) needs fewer.</summary>
    static (int Rings, int Around) Detail(Spec s) => s.Length < 0.3f ? (18, 14) : (30, 28);

    /// <summary>The body's surface at u (0 nose → 1 the end of the tail stem) and θ round the body.</summary>
    public static Vector3 Body(Spec s, float u, float a)
    {
        float r;
        if (u <= s.Peak)
        {
            float k = (s.Peak - u) / s.Peak;
            r = Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow(k, s.NoseK)));
        }
        else
        {
            float k = (u - s.Peak) / (1f - s.Peak);
            r = s.Stem + (1f - s.Stem) * Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Pow(k, 1.8f)), 1.2f);
        }
        float x = s.Length * (0.5f - u);
        float sn = Mathf.Sin(a), cs = Mathf.Cos(a);
        // The width narrows faster than the height toward the tail: a fish is a blade at its stem.
        float wr = r * (1f - 0.35f * Mathf.Clamp((u - s.Peak) / (1f - s.Peak), 0f, 1f));
        return new Vector3(x, s.Height * r * sn * (sn < 0f ? s.Belly : 1f), s.Width * wr * cs);
    }

    static Vector3 BodyNormal(Spec s, float u, float a)
    {
        const float e = 0.004f;
        Vector3 du = Body(s, Mathf.Min(u + e, 1f), a) - Body(s, Mathf.Max(u - e, 0f), a);
        Vector3 da = Body(s, u, a + e) - Body(s, u, a - e);
        var n = da.Cross(du);
        return n.LengthSquared() < 1e-14f ? (u < 0.5f ? Vector3.Right : Vector3.Left) : n.Normalized();
    }

    sealed class Builder
    {
        readonly SurfaceTool _st = new();
        int _count;

        public Builder()
        {
            _st.Begin(Mesh.PrimitiveType.Triangles);
            for (int i = 0; i < 3; i++) _st.SetCustomFormat(i, SurfaceTool.CustomFormat.RgbaFloat);
        }

        public int Add(Vector3 p, Vector3 normal, int part, Vector3 attach, float f, float side, float bodyU, float finT, Vector2 eye = default, Vector2 round = default)
        {
            _st.SetNormal(normal);
            _st.SetCustom(0, new Color(attach.X, attach.Y, attach.Z, part));
            _st.SetCustom(1, new Color(f, side, bodyU, finT));
            _st.SetCustom(2, new Color(eye.X, eye.Y, round.X, round.Y));
            _st.AddVertex(p);
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

    public static ArrayMesh Build(Spec s)
    {
        var b = new Builder();
        BuildBody(b, s);
        foreach (float side in new[] { -1f, 1f })
        {
            BuildEye(b, s, side);
            BuildPectoral(b, s, side);
            BuildPelvic(b, s, side);
        }
        BuildDorsal(b, s);
        BuildAnal(b, s);
        BuildTail(b, s);
        return b.Commit();
    }

    static void BuildBody(Builder b, Spec s)
    {
        var (rings, around) = Detail(s);
        var index = new int[(rings + 1) * (around + 1)];
        for (int i = 0; i <= rings; i++)
        for (int j = 0; j <= around; j++)
        {
            // rings bunched toward the nose, where the head rounds off.
            float t = (float)i / rings, u = t * t * (3f - 2f * t) * 0.35f + t * 0.65f;
            float a = (float)j / around * Mathf.Tau;
            var p = Body(s, u, a);
            index[i * (around + 1) + j] = b.Add(p, BodyNormal(s, u, a), 0, p, 0f, 0f, u, 0f, default, new Vector2(Mathf.Sin(a), Mathf.Cos(a)));
        }
        for (int i = 0; i < rings; i++)
        for (int j = 0; j < around; j++)
        {
            int p = index[i * (around + 1) + j], q = index[(i + 1) * (around + 1) + j], r = index[(i + 1) * (around + 1) + j + 1], t = index[i * (around + 1) + j + 1];
            b.Tri(p, r, q);
            b.Tri(p, t, r);
        }
    }

    /// <summary>A dome on the head's flank, looking out and a little forward; its disc coordinates draw the iris.</summary>
    static void BuildEye(Builder b, Spec s, float side)
    {
        float a = side > 0f ? s.EyeAngle : Mathf.Pi - s.EyeAngle;
        var at = Body(s, s.EyeU, a);
        var n = (BodyNormal(s, s.EyeU, a) + Vector3.Right * 0.2f).Normalized();
        Vector3 t1 = n.Cross(Vector3.Up).Normalized(), t2 = t1.Cross(n);
        const int rings = 5, seg = 16;
        var idx = new int[(rings + 1) * (seg + 1)];
        for (int i = 0; i <= rings; i++)
        for (int j = 0; j <= seg; j++)
        {
            float rr = (float)i / rings, ang = (float)j / seg * Mathf.Tau;
            var disc = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rr;
            float h = Mathf.Sqrt(Mathf.Max(0f, 1f - rr * rr)) * 0.55f;
            var p = at + (t1 * disc.X + t2 * disc.Y) * s.EyeRadius + n * (h - 0.4f) * s.EyeRadius;
            var normal = (n * h + (t1 * disc.X + t2 * disc.Y) * 0.6f).Normalized();
            idx[i * (seg + 1) + j] = b.Add(p, normal, 1, at, 0f, side, s.EyeU, 0f, disc);
        }
        for (int i = 0; i < rings; i++)
        for (int j = 0; j < seg; j++)
        {
            int p = idx[i * (seg + 1) + j], q = idx[(i + 1) * (seg + 1) + j], r = idx[(i + 1) * (seg + 1) + j + 1], t = idx[i * (seg + 1) + j + 1];
            if (side > 0f)
            {
                b.Tri(p, q, r);
                b.Tri(p, r, t);
            }
            else
            {
                b.Tri(p, r, q);
                b.Tri(p, t, r);
            }
        }
    }

    /// <summary>
    /// A fin (drawn from both sides): rays from root(t) out to tip(t), t running front to back along the root. Each ray
    /// hinges at its own root (<paramref name="attachAtRoot"/>) for a ripple, or the whole fin at one point.
    /// </summary>
    static void Fin(Builder b, int part, float side, Func<float, float> bodyU, Func<float, Vector3> root, Func<float, Vector3> tip, Vector3 normal, Vector3? attach = null, int rays = 12, int steps = 4)
    {
        var idx = new int[(rays + 1) * (steps + 1)];
        for (int i = 0; i <= rays; i++)
        {
            float t = (float)i / rays;
            Vector3 r = root(t), e = tip(t);
            for (int k = 0; k <= steps; k++)
            {
                float f = (float)k / steps;
                idx[i * (steps + 1) + k] = b.Add(r.Lerp(e, f), normal, part, attach ?? r, f, side, bodyU(t), t);
            }
        }
        for (int i = 0; i < rays; i++)
        for (int k = 0; k < steps; k++)
        {
            int p = idx[i * (steps + 1) + k], q = idx[(i + 1) * (steps + 1) + k], r = idx[(i + 1) * (steps + 1) + k + 1], t = idx[i * (steps + 1) + k + 1];
            b.Tri(p, q, r);
            b.Tri(p, r, t);
        }
    }

    /// <summary>The dorsal fin along the back: low and spiny in front, a notch, then the taller soft rays; swept back.</summary>
    static void BuildDorsal(Builder b, Spec s)
    {
        float U(float t) => Mathf.Lerp(s.DorsalFrom, s.DorsalTo, t);
        Vector3 Root(float t) => Body(s, U(t), Mathf.Pi * 0.5f) - new Vector3(0f, 0.004f, 0f);
        Vector3 Tip(float t)
        {
            float rise = Mathf.Sin(Mathf.Pi * Mathf.Pow(t, 0.8f));
            // The clownfish's notch between its spiny and soft dorsal.
            if (s.DorsalNotch > 0f) rise *= t < 0.42f ? 0.75f * (1f - s.DorsalNotch * Mathf.SmoothStep(0.25f, 0.42f, t)) + 0.1f : 0.35f + 0.75f * Mathf.Sin(Mathf.Pi * (t - 0.42f) / 0.58f);
            float h = s.Length * s.DorsalHeight * (0.25f + 0.75f * rise);
            return Root(t) + new Vector3(-h * 0.55f, h, 0f);
        }
        Fin(b, 4, 0f, U, Root, Tip, Vector3.Back, rays: 18);
    }

    static void BuildAnal(Builder b, Spec s)
    {
        float U(float t) => Mathf.Lerp(s.AnalFrom, s.AnalTo, t);
        Vector3 Root(float t) => Body(s, U(t), -Mathf.Pi * 0.5f) + new Vector3(0f, 0.004f, 0f);
        Vector3 Tip(float t)
        {
            float h = s.Length * s.AnalHeight * (0.3f + 0.7f * Mathf.Sin(Mathf.Pi * Mathf.Pow(t, 0.7f)));
            return Root(t) + new Vector3(-h * 0.6f, -h, 0f);
        }
        Fin(b, 5, 0f, U, Root, Tip, Vector3.Back, rays: 12);
    }

    /// <summary>The tail on the end of the stem, upright, its shape the species' own.</summary>
    static void BuildTail(Builder b, Spec s)
    {
        var end = Body(s, 1f, Mathf.Pi * 0.5f);
        var attach = new Vector3(end.X + 0.004f, 0f, 0f);
        float stem = Mathf.Max(end.Y, s.Length * 0.02f);
        float len = s.Length * s.TailLength, span = s.Length * s.TailSpan * 0.5f;
        Vector3 Root(float t) => attach + new Vector3(0f, Mathf.Lerp(stem, -stem, t), 0f);
        Vector3 Tip(float t)
        {
            float c = t * 2f - 1f, ac = Mathf.Abs(c);
            return s.TailKind switch
            {
                // A rounded fan.
                Tail.Rounded => attach + new Vector3(-len * (0.45f + 0.55f * Mathf.Cos(c)), -Mathf.Sin(c * 1.2f) * span, 0f),
                // Broad and almost square, the corners drawn out a little.
                Tail.Truncate => attach + new Vector3(-len * (0.88f + 0.18f * ac * ac), -c * span, 0f),
                // Deeply forked: long pointed lobes, the middle short.
                _ => attach + new Vector3(-len * (0.38f + 0.62f * Mathf.Pow(ac, 1.4f)), -c * span * (0.55f + 0.45f * ac), 0f),
            };
        }
        Fin(b, 6, 0f, _ => 1f, Root, Tip, Vector3.Back, attach, rays: 16, steps: 5);
    }

    /// <summary>A pectoral fin just behind the gill, a little below the midline: the paddle they hover and row with.</summary>
    static void BuildPectoral(Builder b, Spec s, float side)
    {
        const float u = 0.3f;
        float a = side > 0f ? -0.25f : Mathf.Pi + 0.25f;
        var attach = Body(s, u, a) - new Vector3(0f, 0f, side * 0.002f);
        float len = s.Length * s.Pectoral, root = s.Length * 0.035f;
        Vector3 Root(float t) => attach + new Vector3(0f, Mathf.Lerp(root, -root, t), 0f);
        Vector3 Tip(float t)
        {
            float ang = Mathf.Lerp(0.55f, -0.8f, t);
            return attach + new Vector3(-len * (0.55f + 0.45f * Mathf.Cos(ang)), Mathf.Sin(ang) * len * 0.5f, side * len * 0.32f);
        }
        Fin(b, 3, side, _ => u, Root, Tip, new Vector3(0f, 0f, side), attach, rays: 8);
    }

    /// <summary>A small pelvic fin under the belly, angled back and out.</summary>
    static void BuildPelvic(Builder b, Spec s, float side)
    {
        const float u = 0.34f;
        float a = -Mathf.Pi * 0.5f + side * 0.35f;
        var attach = Body(s, u, a);
        float len = s.Length * s.Pelvic, root = s.Length * 0.02f;
        Vector3 Root(float t) => attach + new Vector3(Mathf.Lerp(root, -root, t), 0f, 0f);
        Vector3 Tip(float t) => attach + new Vector3(-len * (0.5f + 0.5f * Mathf.Sin(Mathf.Pi * t * 0.8f + 0.3f)), -len * 0.55f, side * len * 0.35f);
        Fin(b, 7, side, _ => u, Root, Tip, new Vector3(0f, -1f, side).Normalized(), attach, rays: 5, steps: 3);
    }
}
