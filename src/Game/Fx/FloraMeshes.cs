using System;
using System.Collections.Generic;
using Godot;

namespace OctoShoots.Game.Fx;

/// <summary>The reef's plants and sessile animals, as in a Caribbean fore-reef. Each species has one look.</summary>
public enum FloraKind
{
    /// <summary>Turtle grass (Thalassia testudinum): strap-like blades in meadows on sandy flats.</summary>
    TurtleGrass,
    /// <summary>Calcareous green algae (Halimeda): chains of small green discs, on sand at the foot of rock.</summary>
    Halimeda,
    /// <summary>Staghorn coral (Acropora cervicornis): branching golden-brown antlers with pale tips, in the sunlit shallows.</summary>
    Staghorn,
    /// <summary>Elkhorn coral (Acropora palmata): broad flattened ochre branches, on the shallowest tops.</summary>
    Elkhorn,
    /// <summary>Brain coral (Diploria): rounded boulders grooved with meandering valleys.</summary>
    BrainCoral,
    /// <summary>Common sea fan (Gorgonia ventalina): a purple lattice fan, broadside to the current.</summary>
    SeaFan,
    /// <summary>Sea rods (Plexaura): bushy soft-coral candelabras that sway in the surge.</summary>
    SeaRod,
    /// <summary>Tube sponges (Aplysina, Callyspongia): clusters of hollow tubes, yellow, purple or lavender.</summary>
    TubeSponge,
    /// <summary>Giant barrel sponge (Xestospongia muta): a ridged red-brown barrel on the deeper slope.</summary>
    BarrelSponge,
}

/// <summary>
/// Procedural meshes for <see cref="FloraKind"/>, unit-ish real sizes in metres, growing up +Y from the origin
/// (the sea fan's lattice spans the XY plane). Vertex alpha = how freely a part sways (see flora.gdshader).
/// </summary>
public static class FloraMeshes
{
    public static ArrayMesh Build(FloraKind kind) => kind switch
    {
        FloraKind.TurtleGrass => TurtleGrass(),
        FloraKind.Halimeda => Halimeda(),
        FloraKind.Staghorn => Staghorn(),
        FloraKind.Elkhorn => Elkhorn(),
        FloraKind.BrainCoral => BrainCoral(),
        FloraKind.SeaFan => SeaFan(),
        FloraKind.SeaRod => SeaRod(),
        FloraKind.TubeSponge => TubeSponge(),
        _ => BarrelSponge(),
    };

    static Color C(float r, float g, float b, float a = 0f) => new(r, g, b, a);

    /// <summary>A flat strip (blade) along a path, facing <paramref name="facing"/>, both sides visible.</summary>
    static void Blade(MeshBuilder b, IReadOnlyList<Vector3> path, Func<int, float> width, Vector3 facing, Func<int, Color> color)
    {
        var left = new int[path.Count];
        var right = new int[path.Count];
        for (int i = 0; i < path.Count; i++)
        {
            Vector3 tangent = (path[Math.Min(i + 1, path.Count - 1)] - path[Math.Max(i - 1, 0)]).Normalized();
            Vector3 side = tangent.Cross(facing).Normalized();
            Vector3 n = side.Cross(tangent).Normalized();
            left[i] = b.Add(path[i] - side * width(i) * 0.5f, n, color(i));
            right[i] = b.Add(path[i] + side * width(i) * 0.5f, n, color(i));
        }
        for (int i = 0; i + 1 < path.Count; i++) b.Quad(left[i], right[i], right[i + 1], left[i + 1]);
    }

    static ArrayMesh TurtleGrass()
    {
        var b = new MeshBuilder();
        var rng = new Random(3);
        for (int k = 0; k < 9; k++)
        {
            float a = (float)rng.NextDouble() * Mathf.Tau;
            var root = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.07f * (float)rng.NextDouble();
            float length = 0.25f + 0.32f * (float)rng.NextDouble();
            var lean = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (0.15f + 0.25f * (float)rng.NextDouble());
            var path = new Vector3[5];
            for (int s = 0; s < path.Length; s++)
            {
                float t = s / 4f;
                path[s] = root + (Vector3.Up + lean * t) * length * t;
            }
            bool epiphytes = rng.NextDouble() < 0.5;
            Blade(b, path, s => 0.011f * (s == 4 ? 0.6f : 1f), new Vector3(Mathf.Sin(a), 0f, -Mathf.Cos(a)), s =>
            {
                float t = s / 4f;
                Color green = new Color(0.2f, 0.45f, 0.13f).Lerp(new Color(0.42f, 0.56f, 0.18f), t);
                if (epiphytes && t > 0.7f) green = green.Lerp(new Color(0.55f, 0.48f, 0.3f), (t - 0.7f) / 0.3f * 0.8f);
                green.A = t * t;
                return green;
            });
        }
        return b.Build();
    }

    static ArrayMesh Halimeda()
    {
        var b = new MeshBuilder();
        var rng = new Random(5);
        for (int k = 0; k < 6; k++)
        {
            float a = k / 6f * Mathf.Tau + (float)rng.NextDouble() * 0.5f;
            var dir = (Vector3.Up + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.5f).Normalized();
            var at = Vector3.Zero;
            for (int s = 0; s < 5; s++)
            {
                at += dir * 0.035f;
                float t = s / 4f;
                b.Ellipsoid(at, new Vector3(0.022f, 0.008f, 0.02f) * (1f - 0.2f * t), C(0.36f + 0.1f * t, 0.62f, 0.22f, t * 0.5f), 6);
                dir = (dir + new Vector3((float)rng.NextDouble() - 0.5f, 0.2f, (float)rng.NextDouble() - 0.5f) * 0.6f).Normalized();
            }
        }
        return b.Build();
    }

    /// <summary>Branching antlers: each branch forks into two or three, thinner and paler toward the tips.</summary>
    static ArrayMesh Staghorn()
    {
        var b = new MeshBuilder();
        var rng = new Random(7);
        void Branch(Vector3 from, Vector3 dir, float length, float radius, int depth)
        {
            var path = new Vector3[4];
            for (int s = 0; s < 4; s++) path[s] = from + dir * length * s / 3f;
            b.Tube(path, s => radius * (1f - 0.25f * s / 3f), 5, s =>
            {
                float pale = depth == 0 ? s / 3f : 0f;
                return new Color(0.72f, 0.54f, 0.3f).Lerp(new Color(0.96f, 0.92f, 0.82f), pale * 0.85f);
            });
            if (depth == 0) return;
            int forks = 2 + (rng.NextDouble() < 0.15 ? 1 : 0);
            for (int f = 0; f < forks; f++)
            {
                var spread = new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() * 0.4f, (float)rng.NextDouble() - 0.5f) * 1.2f;
                var nd = (dir + spread).Normalized();
                if (nd.Y < 0.2f) nd = (nd + Vector3.Up * 0.5f).Normalized();
                Branch(path[3], nd, length * 0.85f, radius * 0.75f, depth - 1);
            }
        }
        for (int i = 0; i < 4; i++)
        {
            float a = i / 4f * Mathf.Tau + 0.3f;
            var dir = (Vector3.Up + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.6f).Normalized();
            Branch(Vector3.Zero, dir, 0.2f, 0.035f, 3);
        }
        return b.Build();
    }

    static ArrayMesh Elkhorn()
    {
        var b = new MeshBuilder();
        var ochre = new Color(0.78f, 0.52f, 0.25f);
        b.Tube(new[] { Vector3.Zero, new Vector3(0f, 0.15f, 0f), new Vector3(0f, 0.3f, 0f) }, s => 0.1f - 0.02f * s, 8, _ => ochre);
        var rng = new Random(9);
        for (int i = 0; i < 6; i++)
        {
            float a = i / 6f * Mathf.Tau + (float)rng.NextDouble() * 0.4f;
            var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            float rise = 0.25f + 0.35f * (float)rng.NextDouble();
            // A broad flattened lobe: a chain of flat ellipsoids, widening then forking into fingers.
            for (int s = 0; s < 5; s++)
            {
                float t = s / 4f;
                var at = new Vector3(0f, 0.28f, 0f) + outward * (0.12f + 0.42f * t) + Vector3.Up * rise * t;
                var basis = Basis.LookingAt(outward + Vector3.Up * rise, Vector3.Up);
                float w = 0.09f + 0.1f * Mathf.Sin(t * Mathf.Pi);
                var color = ochre.Lerp(new Color(0.95f, 0.85f, 0.65f), t * t * 0.7f);
                AddEllipsoid(b, at, basis, new Vector3(w, 0.03f, 0.12f), color);
            }
        }
        return b.Build();
    }

    /// <summary>An ellipsoid with a rotation (MeshBuilder's is axis-aligned).</summary>
    static void AddEllipsoid(MeshBuilder b, Vector3 center, Basis basis, Vector3 radii, Color color)
    {
        const int seg = 8, rings = 5;
        var ids = new int[rings + 1, seg];
        for (int r = 0; r <= rings; r++)
        {
            float v = r / (float)rings * Mathf.Pi;
            for (int s = 0; s < seg; s++)
            {
                float u = s / (float)seg * Mathf.Tau;
                var n = new Vector3(Mathf.Sin(v) * Mathf.Cos(u), Mathf.Cos(v), Mathf.Sin(v) * Mathf.Sin(u));
                var local = n * radii;
                var normal = new Vector3(n.X / radii.X, n.Y / radii.Y, n.Z / radii.Z).Normalized();
                ids[r, s] = b.Add(center + basis * local, (basis * normal).Normalized(), color);
            }
        }
        for (int r = 0; r < rings; r++)
        for (int s = 0; s < seg; s++)
            b.Quad(ids[r, s], ids[r, (s + 1) % seg], ids[r + 1, (s + 1) % seg], ids[r + 1, s]);
    }

    static ArrayMesh BrainCoral()
    {
        var b = new MeshBuilder();
        // A dome slightly wider than tall, sunk a little into the rock; the grooves are drawn by the shader.
        b.Ellipsoid(new Vector3(0f, 0.05f, 0f), new Vector3(0.5f, 0.42f, 0.5f), new Color(0.74f, 0.66f, 0.4f, 0f), 18,
            p => new Color(0.72f + 0.06f * Mathf.Sin(p.X * 7f), 0.64f, 0.38f + 0.05f * Mathf.Sin(p.Z * 5f), 0f));
        return b.Build();
    }

    static ArrayMesh SeaFan()
    {
        var b = new MeshBuilder();
        var purple = new Color(0.58f, 0.24f, 0.6f);
        // Stalk.
        b.Tube(new[] { Vector3.Zero, new Vector3(0f, 0.06f, 0f), new Vector3(0f, 0.12f, 0f) }, s => 0.025f, 5, s => C(0.45f, 0.2f, 0.45f, 0f));
        // The fan: a rounded blade in the XY plane, both sides drawn; the shader cuts the lattice.
        const int spokes = 18, rings = 6;
        var ids = new int[rings + 1, spokes + 1];
        for (int r = 0; r <= rings; r++)
        for (int s = 0; s <= spokes; s++)
        {
            float t = r / (float)rings;
            float a = (s / (float)spokes - 0.5f) * Mathf.Pi * 0.95f;
            float radius = 0.08f + 0.62f * t;
            var p = new Vector3(Mathf.Sin(a) * radius * 0.95f, 0.1f + Mathf.Cos(a) * radius, 0.02f * Mathf.Sin(a * 3f + t * 4f));
            var color = purple.Lerp(new Color(0.75f, 0.42f, 0.78f), t * 0.4f);
            color.A = t;
            ids[r, s] = b.Add(p, Vector3.Back, color);
        }
        for (int r = 0; r < rings; r++)
        for (int s = 0; s < spokes; s++)
            b.Quad(ids[r, s], ids[r, s + 1], ids[r + 1, s + 1], ids[r + 1, s]);
        return b.Build();
    }

    static ArrayMesh SeaRod()
    {
        var b = new MeshBuilder();
        var rng = new Random(13);
        var stem = new Color(0.42f, 0.28f, 0.3f);
        b.Tube(new[] { Vector3.Zero, new Vector3(0f, 0.1f, 0f), new Vector3(0f, 0.2f, 0f) }, s => 0.03f, 6, s => C(stem.R, stem.G, stem.B, 0f));
        for (int i = 0; i < 8; i++)
        {
            float a = i / 8f * Mathf.Tau + (float)rng.NextDouble() * 0.5f;
            var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            float height = 0.45f + 0.5f * (float)rng.NextDouble();
            var path = new Vector3[7];
            for (int s = 0; s < path.Length; s++)
            {
                float t = s / 6f;
                path[s] = new Vector3(0f, 0.18f, 0f) + outward * (0.18f * Mathf.Sqrt(t)) + Vector3.Up * height * t;
            }
            // Fuzzy with extended polyps: paler, purplish-tan toward the tips.
            b.Tube(path, s => 0.018f - 0.002f * s, 5, s =>
            {
                float t = s / 6f;
                var c = stem.Lerp(new Color(0.72f, 0.58f, 0.62f), t);
                c.A = t * t;
                return c;
            });
        }
        return b.Build();
    }

    /// <summary>A hollow tube: outside wall, dark inside, a rim; clusters of three to five.</summary>
    static void HollowTube(MeshBuilder b, Vector3 baseAt, Vector3 dir, float radius, float height, Color color)
    {
        var outer = new[] { baseAt, baseAt + dir * height * 0.5f, baseAt + dir * height };
        b.Tube(outer, s => radius * (0.9f + 0.1f * s), 8, s => color);
        b.Tube(outer, s => radius * 0.78f * (0.9f + 0.1f * s), 8, s => color * 0.3f);
        // Rim ring joining outside and inside.
        var top = baseAt + dir * height;
        Vector3 u = dir.Cross(Mathf.Abs(dir.Y) > 0.9f ? Vector3.Right : Vector3.Up).Normalized();
        Vector3 v = dir.Cross(u);
        var o = new int[10];
        var i2 = new int[10];
        for (int k = 0; k < 10; k++)
        {
            float a = k / 10f * Mathf.Tau;
            var r = u * Mathf.Cos(a) + v * Mathf.Sin(a);
            o[k] = b.Add(top + r * radius * 1.1f, dir, color * 1.1f);
            i2[k] = b.Add(top + r * radius * 0.86f, dir, color * 1.1f);
        }
        for (int k = 0; k < 10; k++) b.Quad(o[k], o[(k + 1) % 10], i2[(k + 1) % 10], i2[k]);
    }

    static ArrayMesh TubeSponge()
    {
        var b = new MeshBuilder();
        var rng = new Random(17);
        int tubes = 4;
        for (int i = 0; i < tubes; i++)
        {
            float a = i / (float)tubes * Mathf.Tau + (float)rng.NextDouble();
            var off = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.08f;
            var dir = (Vector3.Up + off * 2.5f).Normalized();
            HollowTube(b, off, dir, 0.055f + 0.025f * (float)rng.NextDouble(), 0.35f + 0.4f * (float)rng.NextDouble(), new Color(1f, 1f, 1f, 0f));
        }
        return b.Build();
    }

    static ArrayMesh BarrelSponge()
    {
        var b = new MeshBuilder();
        const int rings = 8, sides = 20;
        var outer = new int[rings + 1, sides];
        var inner = new int[rings + 1, sides];
        for (int r = 0; r <= rings; r++)
        {
            float t = r / (float)rings;
            float radius = 0.26f + 0.12f * Mathf.Sin(t * Mathf.Pi * 0.8f) + 0.08f * t;
            float y = t * 1.1f;
            for (int s = 0; s < sides; s++)
            {
                float a = s / (float)sides * Mathf.Tau;
                // Vertical ridges and stair-step bands.
                float ridge = 1f + 0.06f * Mathf.Abs(Mathf.Sin(a * 7f)) + 0.03f * Mathf.Sin(t * 30f);
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var col = new Color(0.8f, 0.4f, 0.3f).Lerp(new Color(0.95f, 0.62f, 0.48f), Mathf.Abs(Mathf.Sin(a * 7f)) * 0.6f);
                outer[r, s] = b.Add(dir * radius * ridge + Vector3.Up * y, (dir + Vector3.Down * 0.1f).Normalized(), col);
                inner[r, s] = b.Add(dir * radius * 0.8f + Vector3.Up * Mathf.Max(y, 0.15f), -dir, new Color(0.35f, 0.15f, 0.12f));
            }
        }
        for (int r = 0; r < rings; r++)
        for (int s = 0; s < sides; s++)
        {
            int s1 = (s + 1) % sides;
            b.Quad(outer[r, s], outer[r, s1], outer[r + 1, s1], outer[r + 1, s]);
            b.Quad(inner[r, s], inner[r, s1], inner[r + 1, s1], inner[r + 1, s]);
        }
        for (int s = 0; s < sides; s++)
        {
            int s1 = (s + 1) % sides;
            b.Quad(outer[rings, s], outer[rings, s1], inner[rings, s1], inner[rings, s]);
        }
        return b.Build();
    }
}
