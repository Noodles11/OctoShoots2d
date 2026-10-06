using System;
using System.Collections.Generic;
using Godot;

namespace OctoShoots.Game.Fx;

/// <summary>
/// Procedural meshes for the reef's small life, built once at startup.
/// Vertex colour channels are data, not colour: see the individual shaders.
/// </summary>
public static class ReefMeshes
{
    // ───────────────────────── anemone ─────────────────────────

    /// <summary>
    /// A sea anemone, unit footprint: a flared column, a dark mouth and three rings of soft tentacles.
    /// COLOR.r = sway weight (0 at the base → 1 at tips), COLOR.g = tip glow, COLOR.b = part (0 tentacle, .5 column, 1 mouth).
    /// </summary>
    public static ArrayMesh Anemone()
    {
        var b = new MeshBuilder();
        const float top = 0.45f;

        // Column.
        var column = new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, 0.22f, 0f), new Vector3(0f, top, 0f) };
        float[] columnR = { 0.5f, 0.4f, 0.6f };
        b.Tube(column, i => columnR[i], 14, _ => new Color(0f, 0f, 0.5f));

        // Mouth: a dark disc just above the column top.
        int centre = b.Add(new Vector3(0f, top + 0.03f, 0f), Vector3.Up, new Color(0f, 0f, 1f));
        var rim = new int[14];
        for (int k = 0; k < rim.Length; k++)
        {
            float a = k / (float)rim.Length * Mathf.Tau;
            rim[k] = b.Add(new Vector3(Mathf.Cos(a) * 0.22f, top + 0.015f, Mathf.Sin(a) * 0.22f), Vector3.Up, new Color(0f, 0f, 1f));
        }
        for (int k = 0; k < rim.Length; k++) b.Tri(centre, rim[k], rim[(k + 1) % rim.Length]);

        // Tentacles in three rings, longer and more spread toward the outside.
        var rng = new Random(7);
        (int Count, float Radius, float Length, float Lean)[] rings =
        {
            (9, 0.18f, 0.95f, 0.30f),
            (13, 0.38f, 1.1f, 0.55f),
            (17, 0.56f, 1.0f, 0.75f),
        };
        foreach (var (count, radius, length, lean) in rings)
        for (int i = 0; i < count; i++)
        {
            float a = (i + (float)rng.NextDouble() * 0.6f) / count * Mathf.Tau;
            var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            float len = length * (0.85f + 0.3f * (float)rng.NextDouble());
            float curl = (float)rng.NextDouble() * Mathf.Tau;
            var path = new Vector3[6];
            for (int s = 0; s < path.Length; s++)
            {
                float t = s / (float)(path.Length - 1);
                Vector3 p = outward * (radius + lean * len * 0.5f * t * t);
                p.Y = top + len * t * (1f - 0.3f * t);
                p += new Vector3(Mathf.Cos(curl), 0f, Mathf.Sin(curl)) * 0.05f * Mathf.Sin(t * 5f);
                path[s] = p;
            }
            b.Tube(path, s => 0.055f * (1f - 0.82f * s / (path.Length - 1f)) + 0.006f, 5, s =>
            {
                float t = s / (path.Length - 1f);
                return new Color(t, t * t * t, 0f);
            });
        }
        return b.Build();
    }

    // ───────────────────────── clownfish ─────────────────────────

    const float BodyLength = 0.6f;
    const float BodyStart = -0.3f;
    const float HalfHeight = 0.15f;
    const float HalfWidth = 0.095f;

    static readonly Color Orange = new(1f, 0.42f, 0.05f);
    static readonly Color White = new(0.97f, 0.97f, 0.94f);
    static readonly Color Black = new(0.05f, 0.04f, 0.04f);

    /// <summary>Body outline at s in 0..1 (nose to tail): 0..1 scale of the cross-section.</summary>
    static float Profile(float s)
    {
        float round = Mathf.Sqrt(Mathf.Max(0f, 1f - (2f * s - 1f) * (2f * s - 1f)));
        return Mathf.Max(Mathf.Pow(round, 0.85f) * (1f - 0.5f * s), 0.16f * s);
    }

    static float ZAt(float s) => BodyStart + BodyLength * s;

    /// <summary>Orange body with three white bands edged in black.</summary>
    static Color BodyColor(float s)
    {
        float Band(float a, float c) => Mathf.Clamp((Mathf.Min(s - a, c - s)) / 0.025f, -1f, 1f);
        float head = Band(0.24f, 0.34f), mid = Band(0.50f, 0.62f), tail = Band(0.84f, 0.90f);
        float best = Mathf.Max(head, Mathf.Max(mid, tail));
        if (best > 0.6f) return White;
        if (best > -0.2f) return Black;
        return Orange;
    }

    /// <summary>
    /// A clownfish, 0.6 m long, nose toward -Z. COLOR.rgb = colour; COLOR.a = 1 for the body and fins.
    /// </summary>
    public static ArrayMesh Clownfish()
    {
        var b = new MeshBuilder();
        const int rings = 48, sides = 12;

        // Body.
        var ids = new int[rings + 1][];
        for (int i = 0; i <= rings; i++)
        {
            float s = i / (float)rings;
            float prof = Profile(s);
            Color col = BodyColor(s);
            ids[i] = new int[sides];
            for (int k = 0; k < sides; k++)
            {
                float a = k / (float)sides * Mathf.Tau;
                var local = new Vector3(Mathf.Cos(a) * HalfWidth, Mathf.Sin(a) * HalfHeight, 0f);
                var normal = new Vector3(Mathf.Cos(a) / HalfWidth, Mathf.Sin(a) / HalfHeight, 0f).Normalized();
                ids[i][k] = b.Add(new Vector3(local.X * prof, local.Y * prof, ZAt(s)), normal, col);
            }
        }
        for (int i = 0; i < rings; i++)
        for (int k = 0; k < sides; k++)
            b.Quad(ids[i][k], ids[i][(k + 1) % sides], ids[i + 1][(k + 1) % sides], ids[i + 1][k]);

        // Eyes: white with a black pupil, set toward the front sides of the head.
        foreach (float side in new[] { -1f, 1f })
        {
            b.Sphere(new Vector3(side * 0.082f, 0.045f, -0.2f), 0.034f, White, 8);
            b.Sphere(new Vector3(side * 0.106f, 0.047f, -0.212f), 0.019f, Black, 6);
        }

        // Dorsal fin: orange with a black edge, along the back.
        Fin(b, Enumerable(0.36f, 0.86f, 9), s =>
        {
            float u = (s - 0.36f) / 0.5f;
            float height = 0.1f * Mathf.Sin(Mathf.Pi * Mathf.Pow(u, 0.7f)) + 0.012f;
            return (new Vector3(0f, HalfHeight * Profile(s) * 0.92f, ZAt(s)), Vector3.Up * height);
        });

        // Tail fin: a rounded fan with a black rim.
        int hub = b.Add(new Vector3(0f, 0f, ZAt(1f) - 0.01f), Vector3.Right, Orange);
        var fan = new List<int>();
        var fanRim = new List<int>();
        const int spokes = 9;
        for (int i = 0; i < spokes; i++)
        {
            float t = i / (spokes - 1f) * 2f - 1f;
            var mid = new Vector3(0f, t * 0.12f, ZAt(1f) + 0.09f - 0.03f * Mathf.Abs(t));
            var edge = new Vector3(0f, t * 0.17f, ZAt(1f) + 0.2f - 0.05f * Mathf.Abs(t));
            fan.Add(b.Add(mid, Vector3.Right, Orange));
            fanRim.Add(b.Add(edge, Vector3.Right, Black));
        }
        for (int i = 0; i + 1 < spokes; i++)
        {
            b.Tri(hub, fan[i], fan[i + 1]);
            b.Quad(fan[i], fanRim[i], fanRim[i + 1], fan[i + 1]);
        }

        // Pectoral fins: small paddles low on the sides.
        foreach (float side in new[] { -1f, 1f })
        {
            int root0 = b.Add(new Vector3(side * 0.085f, -0.04f, -0.07f), Vector3.Up, Orange);
            int root1 = b.Add(new Vector3(side * 0.085f, -0.05f, -0.01f), Vector3.Up, Orange);
            int tip0 = b.Add(new Vector3(side * 0.17f, -0.085f, -0.02f), Vector3.Up, Black);
            int tip1 = b.Add(new Vector3(side * 0.16f, -0.075f, 0.05f), Vector3.Up, Black);
            b.Quad(root0, tip0, tip1, root1);
        }
        return b.Build();
    }

    static IEnumerable<float> Enumerable(float from, float to, int count)
    {
        for (int i = 0; i < count; i++) yield return from + (to - from) * i / (count - 1f);
    }

    /// <summary>A fin strip: base points on the body, tips offset from them.</summary>
    static void Fin(MeshBuilder b, IEnumerable<float> stations, Func<float, (Vector3 Base, Vector3 Offset)> shape)
    {
        int prevBase = -1, prevTip = -1;
        foreach (float s in stations)
        {
            var (root, offset) = shape(s);
            int baseId = b.Add(root, Vector3.Right, Orange);
            int tipId = b.Add(root + offset, Vector3.Right, Black);
            if (prevBase >= 0) b.Quad(prevBase, prevTip, tipId, baseId);
            prevBase = baseId;
            prevTip = tipId;
        }
    }

    /// <summary>
    /// The ninja's headband, knot and two trailing ribbons, laid over the clownfish's head.
    /// COLOR.a = 0.75 for the band and knot (glows during a wind-up), 0.4 for the ribbons (flutter).
    /// </summary>
    public static ArrayMesh Headband()
    {
        var b = new MeshBuilder();
        var navy = new Color(0.02f, 0.02f, 0.09f, 0.75f);
        var red = new Color(0.78f, 0.08f, 0.08f, 0.75f);
        const float s0 = 0.255f, s1 = 0.345f;
        const int segments = 6, sides = 14;

        // The band: a ring wrapping the head, slightly proud of the body.
        var rings = new int[segments + 1][];
        for (int i = 0; i <= segments; i++)
        {
            float s = Mathf.Lerp(s0, s1, i / (float)segments);
            float prof = Profile(s) * 1.1f + 0.004f;
            rings[i] = new int[sides];
            for (int k = 0; k < sides; k++)
            {
                float a = k / (float)sides * Mathf.Tau;
                var n = new Vector3(Mathf.Cos(a) / HalfWidth, Mathf.Sin(a) / HalfHeight, 0f).Normalized();
                rings[i][k] = b.Add(new Vector3(Mathf.Cos(a) * HalfWidth * prof, Mathf.Sin(a) * HalfHeight * prof, ZAt(s)), n, i == segments / 2 ? red : navy);
            }
        }
        for (int i = 0; i < segments; i++)
        for (int k = 0; k < sides; k++)
            b.Quad(rings[i][k], rings[i][(k + 1) % sides], rings[i + 1][(k + 1) % sides], rings[i + 1][k]);

        // Knot on the back of the head.
        float sKnot = s1 + 0.01f;
        var knotAt = new Vector3(0f, HalfHeight * Profile(sKnot) * 1.12f, ZAt(sKnot));
        b.Sphere(knotAt, 0.026f, red, 8);

        // Two ribbons streaming back and slightly out, clear of the dorsal fin.
        var ribbonColor = new Color(0.02f, 0.02f, 0.09f, 0.4f);
        foreach (float side in new[] { -1f, 1f })
        {
            const int steps = 7;
            int prevA = -1, prevB = -1;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                var p = knotAt + new Vector3(side * (0.01f + 0.07f * t), -0.015f * t - 0.02f * t * t, 0.28f * t);
                float width = 0.022f * (1f - 0.4f * t);
                int a = b.Add(p + new Vector3(0f, width, 0f), Vector3.Right, ribbonColor);
                int bb = b.Add(p - new Vector3(0f, width, 0f), Vector3.Right, ribbonColor);
                if (prevA >= 0) b.Quad(prevA, a, bb, prevB);
                prevA = a;
                prevB = bb;
            }
        }
        return b.Build();
    }

    // ───────────────────────── starfish shuriken ─────────────────────────

    /// <summary>A five-armed starfish, unit radius, lying flat (normal +Y), orange with pale dots down each arm.</summary>
    public static ArrayMesh Starfish()
    {
        var b = new MeshBuilder();
        var orange = new Color(1f, 0.45f, 0.18f);
        var pale = new Color(1f, 0.85f, 0.6f);
        var deep = new Color(0.8f, 0.2f, 0.08f);

        int topCentre = b.Add(new Vector3(0f, 0.2f, 0f), Vector3.Up, pale);
        int bottomCentre = b.Add(new Vector3(0f, -0.1f, 0f), Vector3.Down, deep);
        var topRing = new int[10];
        var bottomRing = new int[10];
        for (int i = 0; i < 10; i++)
        {
            float a = i / 10f * Mathf.Tau + Mathf.Pi / 2f;
            bool tip = i % 2 == 0;
            float r = tip ? 1f : 0.38f;
            var p = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            topRing[i] = b.Add(p + new Vector3(0f, tip ? 0.04f : 0.1f, 0f), Vector3.Up, tip ? orange : pale);
            bottomRing[i] = b.Add(p + new Vector3(0f, tip ? -0.02f : -0.06f, 0f), Vector3.Down, deep);
        }
        for (int i = 0; i < 10; i++)
        {
            int j = (i + 1) % 10;
            b.Tri(topCentre, topRing[i], topRing[j]);
            b.Tri(bottomCentre, bottomRing[i], bottomRing[j]);
            b.Quad(topRing[i], bottomRing[i], bottomRing[j], topRing[j]);
        }
        return b.Build();
    }
}
