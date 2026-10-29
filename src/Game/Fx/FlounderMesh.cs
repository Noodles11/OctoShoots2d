using System;
using System.Collections.Generic;
using Godot;

namespace OctoShoots.Game.Fx;

/// <summary>
/// The flounder's body (docs/ADVENTURE.md §4), baked once: a flat oval, domed a little on top and flat beneath, its fin
/// fringe running round both edges from behind the head to the tail stem, a short stem and a rounded tail fan, and both
/// eyes raised on its top near the head. flounder.gdshader paints and moves it. Local frame: +X is the nose, +Y up, Z
/// across.
///   UV:    part (x: 0 back, 1 belly, 2 fringe, 3 tail, 4 eye), how far out (y: body 0 middle → 1 rim; fringe and tail
///          0 root → 1 edge; eye 0 middle → 1 rim)
///   UV2:   where it lies at rest (x, z), for its patterns
///   COLOR: r how far round the fringe (0 behind the head on one side → 1 on the other, through the tail), g how far
///          along it (0 nose → 1 the tail's edge)
/// </summary>
public static class FlounderMesh
{
    /// <summary>Its body's half-length and half-width, its middle (along X), and the dome's height.</summary>
    public const float HalfLength = 0.44f, HalfWidth = 0.3f, Middle = 0.03f, Dome = 0.07f;
    /// <summary>The fringe's widest, and where the stem leaves the body and the tail ends.</summary>
    public const float Fringe = 0.12f, StemFrom = -0.4f, TailEnd = -0.74f;

    static ArrayMesh? _mesh;
    public static ArrayMesh Mesh => _mesh ??= Build();

    /// <summary>The rim of the body at angle a round its middle (0 the nose): a blunt oval, a little narrower at the tail.</summary>
    static Vector2 Rim(float a)
    {
        float c = Mathf.Cos(a), s = Mathf.Sin(a);
        float sx = Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), 0.9f), sz = Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), 0.85f);
        float narrow = 1f - 0.18f * Mathf.Max(0f, -c);
        return new Vector2(Middle + HalfLength * sx, HalfWidth * sz * narrow);
    }

    /// <summary>The fringe's width at angle a: none at the head and at the stem, widest along the flanks.</summary>
    static float FringeAt(float a)
    {
        float from = Mathf.Abs(Mathf.Wrap(a, -Mathf.Pi, Mathf.Pi));
        float head = Mathf.SmoothStep(0.45f, 1.0f, from), tail = 1f - Mathf.SmoothStep(2.55f, 2.95f, from);
        return Fringe * head * tail * (0.85f + 0.15f * Mathf.Sin(from * 2.2f));
    }

    /// <summary>The dome's height at r (0 middle → 1 rim), a little higher over the head where the eyes sit.</summary>
    static float Height(float r, Vector2 at) =>
        Dome * Mathf.Pow(Mathf.Max(0f, 1f - r * r), 0.6f) * (1f + 0.25f * Mathf.Clamp((at.X - 0.1f) / 0.3f, 0f, 1f));

    static ArrayMesh Build()
    {
        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var uv = new List<Vector2>();
        var uv2 = new List<Vector2>();
        var colors = new List<Color>();
        var indices = new List<int>();
        void Add(Vector3 p, Vector3 n, float part, float outward, float round_, float along)
        {
            verts.Add(p);
            normals.Add(n);
            uv.Add(new Vector2(part, outward));
            uv2.Add(new Vector2(p.X, p.Z));
            colors.Add(new Color(round_, along, 0f));
        }
        float Along(float x) => Mathf.Clamp((HalfLength + Middle - x) / (HalfLength + Middle - TailEnd), 0f, 1f);
        // Grid of rows × cols, wound so its front faces +Y when up (or −Y when not).
        void Grid(int start, int rows, int cols, bool up, bool wrap)
        {
            int c1 = wrap ? cols : cols - 1;
            for (int i = 0; i + 1 < rows; i++)
            for (int j = 0; j < c1; j++)
            {
                int jn = (j + 1) % cols;
                int a = start + i * cols + j, b = start + i * cols + jn, c = start + (i + 1) * cols + j, d = start + (i + 1) * cols + jn;
                if (up) indices.AddRange(new[] { a, c, b, b, c, d });
                else indices.AddRange(new[] { a, b, c, b, d, c });
            }
        }

        const int around = 72, rings = 10, fins = 5;
        // The back and the belly: rings out from the middle to the rim.
        foreach (bool back in new[] { true, false })
        {
            int start = verts.Count;
            for (int i = 0; i < rings; i++)
            {
                float r = (i + 0.02f) / (rings - 1f);
                for (int j = 0; j < around; j++)
                {
                    float a = Mathf.Tau * j / around;
                    var rim = Rim(a);
                    var at = new Vector2(Middle, 0f).Lerp(rim, r);
                    float h = back ? Height(r, at) : -0.3f * Height(r, at);
                    // The normal from the dome's slope.
                    float e = 0.02f;
                    float hr = back ? Height(Mathf.Min(r + e, 1f), at) : -0.3f * Height(Mathf.Min(r + e, 1f), at);
                    var outDir = new Vector3(rim.X - Middle, 0f, rim.Y).Normalized();
                    float run = (rim - new Vector2(Middle, 0f)).Length() * e;
                    var n = (back ? new Vector3(0f, run, 0f) - outDir * (hr - h) : new Vector3(0f, -run, 0f) + outDir * (hr - h)).Normalized();
                    Add(new Vector3(at.X, h, at.Y), n, back ? 0f : 1f, r, j / (float)around, Along(at.X));
                }
            }
            Grid(start, rings, around, back, true);
        }

        // The fringe: a thin fin out from the rim, drooping a little toward its edge, both faces drawn (cull off).
        {
            int start = verts.Count;
            for (int i = 0; i < fins; i++)
            {
                float k = i / (fins - 1f);
                for (int j = 0; j < around; j++)
                {
                    float a = Mathf.Tau * j / around;
                    var rim = Rim(a);
                    var outDir = (rim - new Vector2(Middle, 0f)).Normalized();
                    var at = rim + outDir * (FringeAt(a) * k - 0.015f);
                    // Round the fringe from behind the head on one side, through the tail, to the other.
                    float round_ = Mathf.Wrap(a, 0f, Mathf.Tau) / Mathf.Tau;
                    Add(new Vector3(at.X, -0.012f * k * k, at.Y), Vector3.Up, 2f, k, round_, Along(at.X));
                }
            }
            Grid(start, fins, around, true, true);
        }

        // The stem and the tail fan: a flat blade from inside the body out to a rounded edge.
        {
            const int along = 12, across = 11;
            int start = verts.Count;
            for (int i = 0; i < along; i++)
            {
                float t = i / (along - 1f);
                float x = Mathf.Lerp(StemFrom + 0.06f, TailEnd, t);
                // Narrow at the stem, fanning out, its edge rounded.
                float stem = Mathf.Clamp((x - (StemFrom - 0.12f)) / 0.06f, 0f, 1f);
                float fan = 0.055f + 0.13f * Mathf.SmoothStep(StemFrom - 0.1f, TailEnd + 0.06f, x);
                float half = Mathf.Lerp(fan, 0.075f, stem);
                for (int j = 0; j < across; j++)
                {
                    float v = j / (across - 1f) * 2f - 1f;
                    float edge = t > 0.75f ? (t - 0.75f) / 0.25f : 0f;
                    float xr = x + 0.05f * edge * (1f - v * v);
                    Add(new Vector3(xr, -0.01f + 0.012f * (1f - t), half * v), Vector3.Up, 3f, t, 0.5f + 0.5f * v, Along(xr));
                }
            }
            // (It runs toward −X, so it winds the other way to face up.)
            Grid(start, along, across, false, false);
        }

        // The eyes: two small domes on its top behind the nose, the upper a little ahead of the lower.
        foreach (var (ex, ez) in new[] { (0.31f, -0.075f), (0.25f, 0.08f) })
        {
            const int er = 6, ea = 16;
            const float radius = 0.055f;
            int start = verts.Count;
            var centre = new Vector2(ex, ez);
            float r0 = (centre - new Vector2(Middle, 0f)).Length() / (Rim(Mathf.Atan2(ez, ex - Middle)) - new Vector2(Middle, 0f)).Length();
            float baseY = Height(r0, centre) - 0.01f;
            for (int i = 0; i < er; i++)
            {
                float k = i / (er - 1f);
                float phi = k * Mathf.Pi / 2f;
                for (int j = 0; j < ea; j++)
                {
                    float a = Mathf.Tau * j / ea;
                    var n = new Vector3(Mathf.Sin(phi) * Mathf.Cos(a), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(a));
                    var p = new Vector3(ex, baseY, ez) + new Vector3(n.X * radius, n.Y * radius * 1.1f, n.Z * radius);
                    verts.Add(p);
                    normals.Add(n);
                    uv.Add(new Vector2(4f, k));
                    // The eye's own disc coordinates (seen from above).
                    uv2.Add(new Vector2(n.X, n.Z));
                    colors.Add(new Color(0f, Along(ex), 0f));
                }
            }
            Grid(start, er, ea, true, true);
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Godot.Mesh.ArrayType.Max);
        arrays[(int)Godot.Mesh.ArrayType.Vertex] = verts.ToArray();
        arrays[(int)Godot.Mesh.ArrayType.Normal] = normals.ToArray();
        arrays[(int)Godot.Mesh.ArrayType.TexUV] = uv.ToArray();
        arrays[(int)Godot.Mesh.ArrayType.TexUV2] = uv2.ToArray();
        arrays[(int)Godot.Mesh.ArrayType.Color] = colors.ToArray();
        arrays[(int)Godot.Mesh.ArrayType.Index] = indices.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Godot.Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }
}
