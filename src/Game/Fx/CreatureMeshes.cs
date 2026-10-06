using System;
using System.Collections.Generic;
using Godot;

namespace OctoShoots.Game.Fx;

/// <summary>
/// Procedural meshes of the Depth 1 den creatures, nose toward -Z, built once at startup.
/// The vertex alpha picks the behaviour in creature.gdshader: 1 body, 0.85 glowing part, 0.7 bell, 0.5 soft part, 0.3 spine.
/// </summary>
public static class CreatureMeshes
{
    const float Solid = 1f, Glowing = 0.85f, Bell = 0.7f, Soft = 0.5f, Spine = 0.3f;

    static Color C(float r, float g, float b, float a = Solid) => new(r, g, b, a);

    static Color WithAlpha(Color c, float a) => new(c.R, c.G, c.B, a);

    /// <summary>Points spread evenly over a sphere (Fibonacci spiral).</summary>
    static IEnumerable<Vector3> SpherePoints(int count)
    {
        for (int i = 0; i < count; i++)
        {
            float y = 1f - (i + 0.5f) / count * 2f;
            float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float a = i * 2.3999632f;
            yield return new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
        }
    }

    // ───────────────────────── Spanish Dancer ─────────────────────────

    /// <summary>A red-orange sea slug with a white-edged ruffled mantle, two feathery gills and two horns. 1.6 m long.</summary>
    public static ArrayMesh Dancer()
    {
        var b = new MeshBuilder();
        var red = C(0.93f, 0.24f, 0.12f);
        var deep = C(0.75f, 0.12f, 0.1f);
        b.Ellipsoid(Vector3.Zero, new Vector3(0.3f, 0.21f, 0.76f), red, 12, p => Color.FromHsv(0.02f, 0.85f, 0.85f + 0.1f * Mathf.Sin(p.Z * 9f)));

        // The mantle: a wavy skirt all the way round, fading to a white edge.
        const int n = 44;
        var inner = new int[n];
        var outer = new int[n];
        for (int k = 0; k < n; k++)
        {
            float a = k / (float)n * Mathf.Tau;
            float wave = Mathf.Sin(a * 9f);
            inner[k] = b.Add(new Vector3(Mathf.Cos(a) * 0.3f, -0.04f, Mathf.Sin(a) * 0.74f), Vector3.Up, WithAlpha(deep, Soft));
            outer[k] = b.Add(new Vector3(Mathf.Cos(a) * (0.62f + 0.07f * wave), -0.13f + 0.07f * Mathf.Sin(a * 13f), Mathf.Sin(a) * (1.02f + 0.08f * wave)), Vector3.Up, C(1f, 0.93f, 0.88f, Soft));
        }
        for (int k = 0; k < n; k++) b.Quad(inner[k], inner[(k + 1) % n], outer[(k + 1) % n], outer[k]);

        // Two feathery gills on the back.
        var rng = new Random(11);
        foreach (float side in new[] { -1f, 1f })
        for (int i = 0; i < 8; i++)
        {
            float a = i / 8f * Mathf.Tau;
            var root = new Vector3(side * 0.12f, 0.2f, 0.4f) + new Vector3(Mathf.Cos(a) * 0.05f, 0f, Mathf.Sin(a) * 0.05f);
            var path = new Vector3[5];
            for (int s = 0; s < path.Length; s++)
            {
                float t = s / 4f;
                path[s] = root + new Vector3(Mathf.Cos(a) * 0.12f * t + side * 0.1f * t, 0.38f * t - 0.12f * t * t, Mathf.Sin(a) * 0.1f * t);
            }
            b.Tube(path, s => 0.03f * (1f - s / 5f), 4, s => C(1f, 0.55f + 0.1f * s, 0.45f, Soft));
        }

        // Horns and a pair of dots for eyes.
        foreach (float side in new[] { -1f, 1f })
        {
            b.Cone(new Vector3(side * 0.09f, 0.16f, -0.62f), new Vector3(side * 0.15f, 0.42f, -0.78f), 0.04f, red, C(1f, 0.8f, 0.6f));
            b.Sphere(new Vector3(side * 0.13f, 0.08f, -0.7f), 0.03f, C(0.05f, 0.03f, 0.04f), 6);
        }
        _ = rng;
        return b.Build();
    }

    // ───────────────────────── Pufferling ─────────────────────────

    /// <summary>A round yellow puffer with big eyes and tiny fins. The spines (alpha 0.3) hide inside the body until it swells.</summary>
    public static ArrayMesh Puffer()
    {
        var b = new MeshBuilder();
        var yellow = C(1f, 0.82f, 0.18f);
        b.Ellipsoid(Vector3.Zero, new Vector3(0.4f, 0.38f, 0.44f), yellow, 14, p =>
            p.Y < -0.12f ? C(1f, 0.95f, 0.7f) : Color.FromHsv(0.13f, 0.85f - 0.1f * Mathf.Sin(p.X * 14f + p.Z * 9f), 1f));
        // Tail and fins.
        int hub = b.Add(new Vector3(0f, 0f, 0.42f), Vector3.Right, C(1f, 0.7f, 0.15f, Soft));
        int up = b.Add(new Vector3(0f, 0.16f, 0.68f), Vector3.Right, C(1f, 0.7f, 0.15f, Soft));
        int down = b.Add(new Vector3(0f, -0.16f, 0.68f), Vector3.Right, C(1f, 0.7f, 0.15f, Soft));
        b.Tri(hub, up, down);
        foreach (float side in new[] { -1f, 1f })
        {
            int r0 = b.Add(new Vector3(side * 0.36f, -0.04f, -0.06f), Vector3.Up, C(1f, 0.75f, 0.2f, Soft));
            int r1 = b.Add(new Vector3(side * 0.36f, -0.06f, 0.1f), Vector3.Up, C(1f, 0.75f, 0.2f, Soft));
            int t0 = b.Add(new Vector3(side * 0.56f, -0.1f, 0.0f), Vector3.Up, C(1f, 0.9f, 0.5f, Soft));
            int t1 = b.Add(new Vector3(side * 0.54f, -0.08f, 0.14f), Vector3.Up, C(1f, 0.9f, 0.5f, Soft));
            b.Quad(r0, t0, t1, r1);
            // Big eyes.
            b.Sphere(new Vector3(side * 0.22f, 0.13f, -0.3f), 0.13f, C(1f, 1f, 0.97f), 10);
            b.Sphere(new Vector3(side * 0.25f, 0.14f, -0.41f), 0.07f, C(0.04f, 0.04f, 0.05f), 8);
            b.Sphere(new Vector3(side * 0.27f, 0.17f, -0.46f), 0.025f, C(1f, 1f, 1f, Glowing), 5);
        }
        b.Ellipsoid(new Vector3(0f, -0.08f, -0.44f), new Vector3(0.07f, 0.04f, 0.04f), C(0.85f, 0.4f, 0.25f), 6);

        // Spines: tucked away at rest.
        foreach (var d in SpherePoints(44))
        {
            if (d.Z < -0.55f && Mathf.Abs(d.X) < 0.85f && d.Y > -0.4f) continue; // keep the face clear
            b.Cone(d * 0.4f, d * 0.62f, 0.028f, C(0.95f, 0.8f, 0.4f, Spine), C(1f, 0.97f, 0.85f, Spine), 4);
        }
        return b.Build();
    }

    // ───────────────────────── Barracuda ─────────────────────────

    /// <summary>A silver barracuda, 2.2 m long, with an underbite, a dark stripe and a forked tail.</summary>
    public static ArrayMesh Barracuda()
    {
        var b = new MeshBuilder();
        const int rings = 40, sides = 12;
        const float length = 2.2f, start = -1.1f;
        float Radius(float s) => 0.17f * Mathf.Pow(Mathf.Sin(Mathf.Pi * Mathf.Pow(s, 0.65f)), 0.8f) + 0.02f + (s > 0.85f ? -0.06f * (s - 0.85f) / 0.15f : 0f);
        var ids = new int[rings + 1][];
        for (int i = 0; i <= rings; i++)
        {
            float s = i / (float)rings;
            float r = Mathf.Max(0.012f, Radius(s));
            ids[i] = new int[sides];
            for (int k = 0; k < sides; k++)
            {
                float a = k / (float)sides * Mathf.Tau;
                var n = new Vector3(Mathf.Cos(a) * 0.85f, Mathf.Sin(a), 0f).Normalized();
                // Dark back, white belly, silver sides with a dark stripe.
                float up = Mathf.Sin(a);
                Color col = up > 0.55f ? C(0.2f, 0.27f, 0.36f) : up < -0.45f ? C(0.93f, 0.95f, 0.96f) : (Mathf.Abs(up - 0.05f) < 0.2f && s > 0.2f && s < 0.9f) ? C(0.12f, 0.15f, 0.2f) : C(0.68f, 0.74f, 0.8f);
                ids[i][k] = b.Add(new Vector3(Mathf.Cos(a) * r * 0.85f, Mathf.Sin(a) * r, start + length * s), n, col);
            }
        }
        for (int i = 0; i < rings; i++)
        for (int k = 0; k < sides; k++)
            b.Quad(ids[i][k], ids[i][(k + 1) % sides], ids[i + 1][(k + 1) % sides], ids[i + 1][k]);

        // The long lower jaw with a row of teeth.
        b.Cone(new Vector3(0f, -0.07f, -0.9f), new Vector3(0f, -0.06f, -1.22f), 0.06f, C(0.8f, 0.84f, 0.88f), C(0.9f, 0.9f, 0.9f), 6);
        for (int i = 0; i < 5; i++)
            b.Cone(new Vector3(0f, -0.05f, -0.96f - i * 0.06f), new Vector3(0f, 0.02f, -0.96f - i * 0.06f), 0.012f, C(1f, 1f, 0.95f), C(1f, 1f, 0.95f), 3);

        // Eyes.
        foreach (float side in new[] { -1f, 1f })
        {
            b.Sphere(new Vector3(side * 0.1f, 0.06f, -0.86f), 0.045f, C(1f, 0.85f, 0.3f, Glowing), 8);
            b.Sphere(new Vector3(side * 0.12f, 0.06f, -0.89f), 0.025f, C(0.02f, 0.02f, 0.03f), 6);
        }

        // Dorsal fins, pectorals and a forked tail.
        Fin(b, new Vector3(0f, 0.15f, 0.1f), new Vector3(0f, 0.34f, 0.28f), new Vector3(0f, 0.15f, 0.36f), C(0.15f, 0.2f, 0.28f, Soft));
        Fin(b, new Vector3(0f, 0.09f, 0.7f), new Vector3(0f, 0.24f, 0.86f), new Vector3(0f, 0.08f, 0.9f), C(0.15f, 0.2f, 0.28f, Soft));
        foreach (float side in new[] { -1f, 1f })
            Fin(b, new Vector3(side * 0.1f, -0.06f, -0.45f), new Vector3(side * 0.3f, -0.18f, -0.3f), new Vector3(side * 0.1f, -0.08f, -0.3f), C(0.8f, 0.85f, 0.9f, Soft));
        Fin(b, new Vector3(0f, 0.01f, 1.04f), new Vector3(0f, 0.34f, 1.38f), new Vector3(0f, 0.04f, 1.2f), C(0.15f, 0.2f, 0.28f, Soft));
        Fin(b, new Vector3(0f, -0.01f, 1.04f), new Vector3(0f, -0.34f, 1.38f), new Vector3(0f, -0.04f, 1.2f), C(0.15f, 0.2f, 0.28f, Soft));
        return b.Build();
    }

    static void Fin(MeshBuilder b, Vector3 a, Vector3 tip, Vector3 c, Color color)
    {
        int ia = b.Add(a, Vector3.Right, color), it = b.Add(tip, Vector3.Right, color), ic = b.Add(c, Vector3.Right, color);
        b.Tri(ia, it, ic);
    }

    // ───────────────────────── Moon jelly ─────────────────────────

    /// <summary>A translucent bell with four oral arms and a fringe of thin tentacles. About 0.8 m across.</summary>
    public static ArrayMesh Jelly()
    {
        var b = new MeshBuilder();
        const int rings = 8, sides = 16;
        var ids = new int[rings + 1][];
        for (int r = 0; r <= rings; r++)
        {
            float v = r / (float)rings * Mathf.Pi * 0.5f;
            ids[r] = new int[sides];
            for (int s = 0; s < sides; s++)
            {
                float u = s / (float)sides * Mathf.Tau;
                var n = new Vector3(Mathf.Sin(v) * Mathf.Cos(u), Mathf.Cos(v), Mathf.Sin(v) * Mathf.Sin(u));
                var p = new Vector3(n.X * 0.4f, n.Y * 0.28f, n.Z * 0.4f);
                float rim = Mathf.Pow(r / (float)rings, 2f);
                ids[r][s] = b.Add(p, n, C(0.62f + 0.3f * rim, 0.78f + 0.12f * rim, 1f, Bell));
            }
        }
        for (int r = 0; r < rings; r++)
        for (int s = 0; s < sides; s++)
            b.Quad(ids[r][s], ids[r][(s + 1) % sides], ids[r + 1][(s + 1) % sides], ids[r + 1][s]);

        // Four-leaf ring and a glowing core.
        b.Ellipsoid(new Vector3(0f, 0.07f, 0f), new Vector3(0.13f, 0.05f, 0.13f), C(0.85f, 0.65f, 1f, Glowing), 8);
        var rng = new Random(5);
        for (int i = 0; i < 4; i++)
        {
            float a = i / 4f * Mathf.Tau + 0.4f;
            var path = new Vector3[6];
            for (int s = 0; s < path.Length; s++)
            {
                float t = s / 5f;
                path[s] = new Vector3(Mathf.Cos(a) * 0.08f * (1f + t), -0.04f - 0.55f * t, Mathf.Sin(a) * 0.08f * (1f + t));
            }
            b.Tube(path, s => 0.035f * (1f - 0.5f * s / 5f), 5, s => C(0.9f, 0.7f, 1f, Soft));
        }
        for (int i = 0; i < 14; i++)
        {
            float a = i / 14f * Mathf.Tau;
            float len = 0.7f + 0.5f * (float)rng.NextDouble();
            var path = new Vector3[6];
            for (int s = 0; s < path.Length; s++)
            {
                float t = s / 5f;
                path[s] = new Vector3(Mathf.Cos(a) * 0.38f, -0.02f - len * t, Mathf.Sin(a) * 0.38f);
            }
            b.Tube(path, s => 0.009f, 3, s => C(0.7f, 0.85f, 1f, Soft));
        }
        return b.Build();
    }

    // ───────────────────────── Sea urchin ─────────────────────────

    /// <summary>A black urchin with long spines tipped in violet. The spines (alpha 0.3) lie close at rest and stand when it winds up.</summary>
    public static ArrayMesh Urchin()
    {
        var b = new MeshBuilder();
        b.Sphere(Vector3.Zero, 0.22f, C(0.08f, 0.05f, 0.12f), 10);
        var rng = new Random(23);
        foreach (var d in SpherePoints(46))
        {
            float length = 0.55f + 0.2f * (float)rng.NextDouble();
            Vector3 jitter = new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f) * 0.18f;
            Vector3 dir = (d + jitter).Normalized();
            b.Cone(dir * 0.18f, dir * length, 0.028f, C(0.08f, 0.05f, 0.1f, Spine), C(0.72f, 0.32f, 1f, Spine), 5);
        }
        // A few eyes' worth of shine on the shell.
        b.Sphere(new Vector3(0f, 0.2f, 0f), 0.04f, C(0.9f, 0.5f, 1f, Glowing), 5);
        return b.Build();
    }

    // ───────────────────────── Crabby ─────────────────────────

    /// <summary>A red-orange crab with one oversized claw. Origin at the shell's centre; the feet reach 0.4 m below.</summary>
    public static ArrayMesh Crab()
    {
        var b = new MeshBuilder();
        var red = C(0.9f, 0.26f, 0.1f);
        b.Ellipsoid(Vector3.Zero, new Vector3(0.48f, 0.2f, 0.36f), red, 12, p => Color.FromHsv(0.03f, 0.85f - 0.15f * Mathf.Abs(Mathf.Sin(p.X * 8f + p.Z * 6f)), 0.95f));
        b.Ellipsoid(new Vector3(0f, 0.1f, 0.02f), new Vector3(0.3f, 0.1f, 0.22f), C(1f, 0.42f, 0.18f), 8);

        // Six legs, three a side, each a bent tube.
        foreach (float side in new[] { -1f, 1f })
        for (int i = 0; i < 3; i++)
        {
            float z = -0.14f + i * 0.15f;
            var root = new Vector3(side * 0.4f, -0.02f, z);
            var knee = root + new Vector3(side * 0.28f, 0.14f, 0.04f * (i - 1));
            var foot = knee + new Vector3(side * 0.12f, -0.52f, 0.05f * (i - 1));
            var path = new[] { root, (root + knee) * 0.5f, knee, (knee + foot) * 0.5f, foot };
            b.Tube(path, s => 0.035f - 0.006f * s, 5, s => s < 2 ? red : C(0.8f, 0.2f, 0.08f, Soft));
        }

        // Eyes on stalks.
        foreach (float side in new[] { -1f, 1f })
        {
            b.Cone(new Vector3(side * 0.12f, 0.16f, -0.28f), new Vector3(side * 0.14f, 0.34f, -0.34f), 0.02f, red, red, 4);
            b.Sphere(new Vector3(side * 0.14f, 0.36f, -0.35f), 0.05f, C(1f, 0.97f, 0.9f), 8);
            b.Sphere(new Vector3(side * 0.14f, 0.37f, -0.39f), 0.03f, C(0.02f, 0.02f, 0.03f), 6);
        }

        // The big claw (right) and a small one (left).
        Claw(b, 1f, 1f, red);
        Claw(b, -1f, 0.5f, red);
        return b.Build();
    }

    static void Claw(MeshBuilder b, float side, float size, Color red)
    {
        var shoulder = new Vector3(side * 0.4f, 0.02f, -0.22f);
        var elbow = shoulder + new Vector3(side * 0.24f * size, 0.04f, -0.14f * size);
        var wrist = elbow + new Vector3(side * 0.05f, 0f, -0.2f * size);
        b.Tube(new[] { shoulder, (shoulder + elbow) * 0.5f, elbow, wrist }, s => 0.05f * (0.6f + 0.4f * size), 6, s => red);
        b.Ellipsoid(wrist + new Vector3(0f, 0f, -0.14f * size), new Vector3(0.14f, 0.1f, 0.2f) * size, C(0.96f, 0.3f, 0.12f), 10);
        var pivot = wrist + new Vector3(0f, 0f, -0.3f * size);
        b.Cone(pivot + new Vector3(0f, 0.045f * size, 0.12f * size), pivot + new Vector3(0f, 0.02f * size, -0.14f * size), 0.05f * size, red, C(1f, 0.9f, 0.8f), 5);
        b.Cone(pivot + new Vector3(0f, -0.045f * size, 0.12f * size), pivot + new Vector3(0f, -0.02f * size, -0.14f * size), 0.05f * size, red, C(1f, 0.9f, 0.8f), 5);
    }

    // ───────────────────────── Moray ─────────────────────────

    /// <summary>The moray's head, nose toward -Z (the lower jaw is a separate mesh so it can gape).</summary>
    public static ArrayMesh MorayHead()
    {
        var b = new MeshBuilder();
        Color Mottle(Vector3 p) => (Mathf.Sin(p.X * 30f + p.Z * 18f) + Mathf.Sin(p.Z * 27f - p.Y * 21f)) > 0.9f ? C(0.88f, 0.78f, 0.22f) : C(0.28f, 0.45f, 0.18f);
        b.Ellipsoid(new Vector3(0f, 0.02f, 0f), new Vector3(0.2f, 0.17f, 0.34f), C(0.3f, 0.5f, 0.2f), 12, Mottle);
        b.Ellipsoid(new Vector3(0f, 0f, -0.2f), new Vector3(0.12f, 0.09f, 0.2f), C(0.3f, 0.5f, 0.2f), 10, Mottle);
        foreach (float side in new[] { -1f, 1f })
        {
            b.Sphere(new Vector3(side * 0.13f, 0.1f, -0.22f), 0.045f, C(1f, 0.9f, 0.3f, Glowing), 8);
            b.Sphere(new Vector3(side * 0.15f, 0.1f, -0.25f), 0.025f, C(0.02f, 0.02f, 0.02f), 6);
            b.Cone(new Vector3(side * 0.05f, 0.07f, -0.38f), new Vector3(side * 0.05f, 0.14f, -0.4f), 0.015f, C(0.2f, 0.3f, 0.15f), C(0.2f, 0.3f, 0.15f), 4);
        }
        for (int i = 0; i < 7; i++)
            b.Cone(new Vector3((i - 3) * 0.028f, -0.045f, -0.3f - Mathf.Abs(i - 3) * -0.01f), new Vector3((i - 3) * 0.028f, -0.11f, -0.31f), 0.011f, C(1f, 1f, 0.92f), C(1f, 1f, 0.92f), 3);
        return b.Build();
    }

    /// <summary>The lower jaw, hinged at its back end (the origin).</summary>
    public static ArrayMesh MorayJaw()
    {
        var b = new MeshBuilder();
        b.Ellipsoid(new Vector3(0f, -0.05f, -0.22f), new Vector3(0.12f, 0.05f, 0.26f), C(0.55f, 0.65f, 0.3f), 10);
        for (int i = 0; i < 6; i++)
            b.Cone(new Vector3((i - 2.5f) * 0.03f, -0.015f, -0.28f), new Vector3((i - 2.5f) * 0.03f, 0.05f, -0.285f), 0.011f, C(1f, 1f, 0.92f), C(1f, 1f, 0.92f), 3);
        return b.Build();
    }

    /// <summary>The moray's body: a one metre tube that starts at the head (z = 0) and runs back along +Z. Scaled to the length in use.</summary>
    public static ArrayMesh MorayBody()
    {
        var b = new MeshBuilder();
        var path = new Vector3[14];
        for (int i = 0; i < path.Length; i++) path[i] = new Vector3(0f, 0f, 0.1f + i / 13f * 0.9f);
        b.Tube(path, s => 0.15f - 0.02f * s / 13f, 10, s => (s / 2) % 2 == 0 ? C(0.3f, 0.48f, 0.2f) : C(0.82f, 0.74f, 0.25f));
        return b.Build();
    }

    /// <summary>A dark hole framed by little anemone-like growths, lying flat (normal +Y).</summary>
    public static ArrayMesh MorayHole()
    {
        var b = new MeshBuilder();
        b.Disc(new Vector3(0f, 0.03f, 0f), Vector3.Up, 0.55f, C(0.01f, 0.01f, 0.02f), 20);
        var rng = new Random(31);
        for (int i = 0; i < 14; i++)
        {
            float a = i / 14f * Mathf.Tau;
            float len = 0.28f + 0.16f * (float)rng.NextDouble();
            var path = new Vector3[5];
            for (int s = 0; s < path.Length; s++)
            {
                float t = s / 4f;
                path[s] = new Vector3(Mathf.Cos(a) * (0.58f + 0.12f * t), len * t, Mathf.Sin(a) * (0.58f + 0.12f * t));
            }
            b.Tube(path, s => 0.045f * (1f - 0.7f * s / 4f), 4, s => C(0.95f, 0.45f + 0.1f * s, 0.6f, Soft));
        }
        return b.Build();
    }
}
