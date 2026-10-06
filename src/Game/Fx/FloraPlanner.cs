using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using OctoShoots.Core.Gen;
using OctoShoots.Core.Terrain;
using OctoShoots.Game.Util;

namespace OctoShoots.Game.Fx;

/// <summary>One plant or sessile animal on the reef.</summary>
public readonly record struct FloraItem(FloraKind Kind, Transform3D Transform, Color Tint);

/// <summary>
/// Decides where the reef's flora grows, following how a real Caribbean fore-reef is zoned:
/// turtle-grass meadows on shallow sandy flats; Halimeda where sand meets rock; elkhorn and staghorn coral
/// on sunlit rock in the shallows; brain corals and sea rods down the slope; sea fans on walls, broadside to
/// the current; tube and barrel sponges deeper and in the shade of caves.
/// Pure decoration: deterministic from the level seed, computed on a worker thread at load.
/// </summary>
public static class FloraPlanner
{
    const int Blocks = 64;

    const float RealDepthScale = 0.45f;

    /// <summary>The prevailing current; sea fans grow broadside to it.</summary>
    static readonly Vector3 Current = new Vector3(1f, 0f, 0.35f).Normalized();

    public static List<FloraItem> Plan(Cave cave, ReefLayout? layout, ulong seed)
    {
        var sdf = cave.Sdf;
        Vector3 size = sdf.Size.G();
        float surface = float.IsFinite(cave.SurfaceY) ? cave.SurfaceY : size.Y;
        // Samples per cubic metre of water: points near rock are projected onto it, so this sets density per m² of reef.
        long samples = (long)(size.X * size.Z * Mathf.Min(surface, size.Y) * 0.5f);
        long perBlock = samples / Blocks;

        var keepOut = cave.Loot.Shells.Select(s => (s.Position.G(), 3f))
            .Concat(cave.Loot.Chests.Select(c => (c.Position.G(), 2f)))
            .Concat(cave.Anemones.Select(a => (a.Position.G(), a.Radius + 0.6f)))
            .Append((cave.PlayerSpawn.G(), 4f))
            .ToArray();

        var results = new List<FloraItem>[Blocks];
        Parallel.For(0, Blocks, block =>
        {
            var rng = new Random(unchecked((int)(seed * 31 + (ulong)block * 7919)));
            var list = results[block] = new List<FloraItem>();
            for (long i = 0; i < perBlock; i++)
            {
                var p = new Vector3((float)rng.NextDouble() * size.X, (float)rng.NextDouble() * surface, (float)rng.NextDouble() * size.Z);
                float d = sdf.Sample(p.N());
                if (d < 0.25f || d > 2.2f) continue;
                Vector3 g = sdf.Gradient(p.N()).G();
                Vector3 at = p - g * d;
                Vector3 n = sdf.Gradient((at + g * 0.15f).N()).G();
                if (keepOut.Any(k => k.Item1.DistanceSquaredTo(at) < k.Item2 * k.Item2)) continue;
                Decide(list, rng, sdf, layout, at, n, surface);
            }
        });
        return results.SelectMany(r => r).ToList();
    }

    static void Decide(List<FloraItem> list, Random rng, VoxelSdf sdf, ReefLayout? layout, Vector3 at, Vector3 n, float surface)
    {
        // The game's reef is a compressed model of a real one (its 60 m water column stands for about 27 m),
        // so zonation uses that real-world depth: sunlit corals in the top ~14 m, sponges below.
        float depth = (surface - at.Y) * RealDepthScale;
        bool seabed = layout is null ? n.Y > 0.85f && at.Y < surface - 6f : at.Y - layout.FloorHeight(at.X, at.Z) < 1.2f;
        bool inCave = layout?.ChamberAt(at.N()) is not null;
        bool sunlit = !inCave && !sdf.Raycast((at + n * 0.4f).N(), System.Numerics.Vector3.UnitY, 30f, out _);
        float roll = (float)rng.NextDouble();
        float Rand(float a, float b) => a + (b - a) * (float)rng.NextDouble();
        Vector3 upish = (n + Vector3.Up * 1.5f).Normalized();

        void Add(FloraKind kind, Vector3 up, float scale, Color tint, float sink = 0.05f)
        {
            var basis = Conv.AlignUp(up, Rand(0f, Mathf.Tau)).Scaled(Vector3.One * scale);
            list.Add(new FloraItem(kind, new Transform3D(basis, at - n * sink), new Color(tint.R, tint.G, tint.B, (float)rng.NextDouble())));
        }

        Color Shade(float spread = 0.12f)
        {
            float v = 1f - spread + spread * 2f * (float)rng.NextDouble() * 0.5f;
            return new Color(v, v, v);
        }

        if (inCave)
        {
            if (n.Y > -0.3f && roll < 0.14f) Add(FloraKind.TubeSponge, upish, Rand(0.7f, 1.2f), SpongeTint(rng), 0.08f);
            return;
        }

        if (seabed && n.Y > 0.82f)
        {
            // Sandy flats: turtle-grass meadows in the shallows, patchy.
            float meadow = (float)(Math.Sin(at.X * 0.045 + Math.Sin(at.Z * 0.03) * 2.0) * Math.Cos(at.Z * 0.05 + at.X * 0.02));
            if (depth < 30f && meadow > -0.1f && roll < 0.85f)
            {
                int clumps = 4 + rng.Next(6);
                for (int k = 0; k < clumps; k++)
                {
                    // Scatter around the point, then drop each clump back onto the sand.
                    var pos = at + new Vector3(Rand(-1.1f, 1.1f), 0.3f, Rand(-1.1f, 1.1f));
                    float d = sdf.Sample(pos.N());
                    if (d < 0.05f || d > 1.2f) continue;
                    pos -= sdf.Gradient(pos.N()).G() * d;
                    if (sdf.Gradient((pos + Vector3.Up * 0.2f).N()).Y < 0.75f) continue;
                    var basis = Conv.AlignUp(Vector3.Up, Rand(0f, Mathf.Tau)).Scaled(Vector3.One * Rand(0.8f, 1.3f));
                    list.Add(new FloraItem(FloraKind.TurtleGrass, new Transform3D(basis, pos - Vector3.Up * 0.04f), WithPhase(Shade(), rng)));
                }
                return;
            }
            // Between the meadows: Halimeda, and sea rods and small brain corals on patches of hard bottom.
            if (roll < 0.25f) Add(FloraKind.Halimeda, Vector3.Up, Rand(0.9f, 1.6f), Shade(), 0.02f);
            else if (roll < 0.33f) Add(FloraKind.SeaRod, Vector3.Up, Rand(0.7f, 1.2f), RodTint(rng));
            else if (roll < 0.37f) Add(FloraKind.BrainCoral, Vector3.Up, Rand(0.4f, 0.9f), Shade(), 0.12f);
            return;
        }

        if (n.Y > 0.5f)
        {
            // Up-facing rock: corals need sunlight, so they thin out with depth and shade.
            if (sunlit && depth < 14f)
            {
                if (roll < 0.1f) { Add(FloraKind.Elkhorn, upish, Rand(0.9f, 1.7f), Shade()); return; }
                if (roll < 0.3f) { Add(FloraKind.Staghorn, upish, Rand(0.9f, 1.6f), Shade()); return; }
                if (roll < 0.4f) { Add(FloraKind.BrainCoral, n, Rand(0.5f, 1.4f), Shade(), 0.12f); return; }
                if (roll < 0.5f) { Add(FloraKind.SeaRod, upish, Rand(0.8f, 1.4f), RodTint(rng)); return; }
                return;
            }
            if (sunlit && depth < 32f)
            {
                if (roll < 0.12f) { Add(FloraKind.Staghorn, upish, Rand(0.8f, 1.3f), Shade()); return; }
                if (roll < 0.26f) { Add(FloraKind.BrainCoral, n, Rand(0.5f, 1.6f), Shade(), 0.12f); return; }
                if (roll < 0.42f) { Add(FloraKind.SeaRod, upish, Rand(0.8f, 1.5f), RodTint(rng)); return; }
                if (roll < 0.45f) { Add(FloraKind.BarrelSponge, upish, Rand(0.7f, 1.3f), Shade(), 0.1f); return; }
                if (roll < 0.56f) { Add(FloraKind.TubeSponge, upish, Rand(0.8f, 1.3f), SpongeTint(rng), 0.08f); return; }
                return;
            }
            if (roll < 0.07f) { Add(FloraKind.BarrelSponge, upish, Rand(0.8f, 1.6f), Shade(), 0.1f); return; }
            if (roll < 0.26f) { Add(FloraKind.TubeSponge, upish, Rand(0.8f, 1.4f), SpongeTint(rng), 0.08f); return; }
            if (roll < 0.34f) { Add(FloraKind.SeaRod, upish, Rand(0.8f, 1.3f), RodTint(rng)); return; }
            return;
        }

        if (n.Y > -0.35f && depth > 5f)
        {
            // Walls: sea fans broadside to the current, tube sponges.
            if (roll < 0.12f)
            {
                Vector3 up = (Vector3.Up * 0.75f + n * 0.25f).Normalized();
                Vector3 facing = Current - up * Current.Dot(up);
                if (facing.LengthSquared() < 1e-3f) facing = n;
                facing = facing.Normalized();
                Vector3 x = up.Cross(facing).Normalized();
                float scale = Rand(0.7f, 1.4f);
                var basis = new Basis(x * scale, up * scale, facing * scale);
                Color tint = rng.NextDouble() < 0.75 ? Shade() : new Color(1.45f, 1.9f, 0.6f) * 0.68f;
                list.Add(new FloraItem(FloraKind.SeaFan, new Transform3D(basis, at - n * 0.05f), new Color(Mathf.Min(tint.R, 1f), Mathf.Min(tint.G, 1f), Mathf.Min(tint.B, 1f), (float)rng.NextDouble())));
                return;
            }
            if (roll < 0.19f) Add(FloraKind.TubeSponge, upish, Rand(0.7f, 1.2f), SpongeTint(rng), 0.08f);
        }
    }

    static Color WithPhase(Color c, Random rng) => new(c.R, c.G, c.B, (float)rng.NextDouble());

    /// <summary>Yellow tube sponge, purple, lavender-blue (azure vase) or orange.</summary>
    static Color SpongeTint(Random rng)
    {
        Color[] palette =
        {
            new(0.95f, 0.78f, 0.2f),
            new(0.5f, 0.22f, 0.58f),
            new(0.55f, 0.55f, 0.85f),
            new(0.92f, 0.45f, 0.15f),
        };
        return palette[rng.Next(palette.Length)];
    }

    static Color RodTint(Random rng)
    {
        float v = 0.85f + 0.3f * (float)rng.NextDouble();
        return rng.NextDouble() < 0.6 ? new Color(Mathf.Min(v, 1f), Mathf.Min(v * 0.95f, 1f), Mathf.Min(v, 1f)) : new Color(1f, 0.85f, 0.6f);
    }
}
