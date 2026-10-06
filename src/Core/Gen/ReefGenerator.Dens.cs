using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Sim;
using OctoShoots.Core.Terrain;

namespace OctoShoots.Core.Gen;

public static partial class ReefGenerator
{
    /// <summary>Closest any two dens may be.</summary>
    const float DenGap = 9f;

    /// <summary>No den this close to where Clementine starts.</summary>
    const float DenStartSafe = 28f;

    /// <summary>
    /// Creature dens (DEPTH1-BESTIARY §4). Creatures live where their body suits: urchins on any surface, morays in
    /// walls, crabs on the seabed, jellies in open water. The shallow corner around the start gets only jellies,
    /// urchins and dancers; the middle adds pufferlings and crabs; the deep half adds barracuda and morays, and
    /// the boss reef is ringed by urchins and morays.
    /// </summary>
    static List<DenSpot> PlaceDens(Rng rng, VoxelSdf sdf, Func<float, float, float> floor, Vector2 start, Vector2 boss2,
        List<Formation> formations, List<Chamber> chambers, float W, float D, float surface, ReefSpec spec)
    {
        var dens = new List<DenSpot>();
        float scale = W * D / (224f * 224f);
        float span = MathF.Max(1f, Vector2.Distance(start, boss2));
        float mid = 62f / span; // the shallow corner ends 60 m from the start
        float Progress(Vector3 p) => Vector2.Distance(new Vector2(p.X, p.Z), start) / span;

        bool Free(Vector3 p, float gap = DenGap)
        {
            if (Vector2.Distance(new Vector2(p.X, p.Z), start) < DenStartSafe) return false;
            foreach (var d in dens)
                if (Vector3.DistanceSquared(d.Position, p) < gap * gap) return false;
            return true;
        }
        Vector3 Border(out float x, out float z)
        {
            x = rng.Range(24f, W - 24f);
            z = rng.Range(24f, D - 24f);
            return default;
        }

        // A point in open water, between the seabed and just below the surface.
        bool WaterPoint(float minAbove, float maxAbove, float zoneMin, float zoneMax, out Vector3 at)
        {
            for (int i = 0; i < 40; i++)
            {
                Border(out float x, out float z);
                float y = MathF.Min(floor(x, z) + rng.Range(minAbove, maxAbove), surface - 4f);
                at = new Vector3(x, y, z);
                float prog = Progress(at);
                if (prog < zoneMin || prog > zoneMax || sdf.Sample(at) < 3f || !Free(at)) continue;
                return true;
            }
            at = default;
            return false;
        }

        // A point on any rock surface (wall, ceiling or seabed), found by casting a ray from open water.
        bool SurfacePoint(float zoneMin, float zoneMax, float minUpY, float maxUpY, out Vector3 pos, out Vector3 up, Vector3? around = null, float aroundR = 0f)
        {
            for (int i = 0; i < 60; i++)
            {
                Vector3 from;
                if (around is { } c)
                {
                    from = c + rng.InsideUnitSphere() * aroundR;
                }
                else
                {
                    Border(out float x, out float z);
                    from = new Vector3(x, MathF.Min(floor(x, z) + rng.Range(2f, 18f), surface - 4f), z);
                }
                float prog = Progress(from);
                if (prog < zoneMin || prog > zoneMax || sdf.Sample(from) < 1.5f) continue;
                Vector3 dir = Vector3.Normalize(rng.InsideUnitSphere() + new Vector3(0f, -0.1f, 0f));
                if (!sdf.Raycast(from, dir, 14f, out float hit)) continue;
                pos = from + dir * hit;
                up = sdf.Gradient(pos);
                if (up.Y < minUpY || up.Y > maxUpY || sdf.Sample(pos + up * 1.2f) < 0.8f || !Free(pos, 5f)) continue;
                return true;
            }
            pos = default;
            up = Vector3.UnitY;
            return false;
        }

        bool Seabed(float zoneMin, float zoneMax, out Vector3 pos, out Vector3 up)
        {
            for (int i = 0; i < 60; i++)
            {
                Border(out float x, out float z);
                if (!Snap(sdf, x, z, floor(x, z), out pos, out up) || up.Y < 0.8f) continue;
                float prog = Progress(pos);
                if (prog < zoneMin || prog > zoneMax || sdf.Sample(pos + up * 1.5f) < 1f || !Free(pos, 5f)) continue;
                return true;
            }
            pos = default;
            up = Vector3.UnitY;
            return false;
        }

        int Scaled(int n) => Math.Max(1, (int)MathF.Round(n * scale));

        // Spanish Dancers: seabed and reef walls, everywhere.
        for (int i = 0, n = Scaled(10); i < n; i++)
            if (SurfacePoint(0f, 2f, -0.2f, 1f, out var pos, out var up)) dens.Add(new DenSpot(EnemyKind.SpanishDancer, pos + up * 1.5f, up, 1));

        // Jelly swarms: open water, mid and upper.
        for (int i = 0, n = Scaled(5); i < n; i++)
            if (WaterPoint(10f, 24f, 0f, 2f, out var at)) dens.Add(new DenSpot(EnemyKind.MoonJelly, at, Vector3.UnitY, 6 + rng.Int(4)));

        // Sea urchins: on any surface; beds of small ones on the seabed.
        for (int i = 0, n = Scaled(25); i < n; i++)
            if (SurfacePoint(0f, 2f, -1f, 1f, out var pos, out var up)) dens.Add(new DenSpot(EnemyKind.SeaUrchin, pos, up, 1));
        for (int i = 0, n = Scaled(10); i < n; i++)
            if (Seabed(0f, 2f, out var pos, out var up)) dens.Add(new DenSpot(EnemyKind.SeaUrchin, pos, up, 3 + rng.Int(3), Bed: true));

        // The middle adds pufferlings (near formations), crabs and the ninjas that already live in anemones.
        for (int i = 0, n = Scaled(8); i < n && formations.Count > 0; i++)
        {
            for (int tries = 0; tries < 20; tries++)
            {
                var f = formations[rng.Int(formations.Count)];
                float a = rng.Range(0f, MathF.Tau);
                var at = new Vector3(f.Center.X + MathF.Cos(a) * (f.Radius + 4f), f.Center.Y + rng.Range(-2f, 8f), f.Center.Z + MathF.Sin(a) * (f.Radius + 4f));
                if (Progress(at) < mid || at.X < 24f || at.Z < 24f || at.X > W - 24f || at.Z > D - 24f || at.Y > surface - 5f) continue;
                if (sdf.Sample(at) < 2.5f || !Free(at)) continue;
                dens.Add(new DenSpot(EnemyKind.Pufferling, at, Vector3.UnitY, 1 + rng.Int(3)));
                break;
            }
        }
        for (int i = 0, n = Scaled(12); i < n; i++)
            if (Seabed(mid, 2f, out var pos, out var up)) dens.Add(new DenSpot(EnemyKind.Crabby, pos, up, 1));

        // The deep half: barracuda in open water, morays in rock walls and at cave mouths.
        for (int i = 0, n = 3; i < n; i++)
            if (WaterPoint(8f, 20f, MathF.Max(mid, 0.5f), 2f, out var at)) dens.Add(new DenSpot(EnemyKind.Barracuda, at, Vector3.UnitY, 1 + rng.Int(2)));
        int morays = Scaled(8);
        foreach (var c in chambers.Where(c => c.Role is ChamberRole.Normal or ChamberRole.Treasure or ChamberRole.Curse).Take(morays / 2))
            if (SurfacePoint(MathF.Max(mid, 0.4f), 2f, -0.4f, 0.55f, out var pos, out var up, c.Mouth, 6f)) dens.Add(new DenSpot(EnemyKind.Moray, pos, up, 1));
        for (int i = dens.Count(d => d.Kind == EnemyKind.Moray); i < morays; i++)
            if (SurfacePoint(MathF.Max(mid, 0.45f), 2f, -0.4f, 0.55f, out var pos, out var up)) dens.Add(new DenSpot(EnemyKind.Moray, pos, up, 1));

        // The boss reef is ringed by urchins and morays.
        var bossChamber = chambers.FirstOrDefault(c => c.Role == ChamberRole.Boss);
        if (bossChamber is not null)
        {
            for (int i = 0; i < 8; i++)
                if (SurfacePoint(0f, 3f, -1f, 1f, out var pos, out var up, bossChamber.Mouth, 14f))
                    dens.Add(new DenSpot(i % 2 == 0 ? EnemyKind.SeaUrchin : EnemyKind.Moray, pos, up, 1));
        }
        return dens;
    }
}
