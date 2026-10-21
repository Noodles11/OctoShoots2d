using System;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>The cave round its route (docs/CAVEGEN.md): side chambers close to it, quick detours, and shortcuts: tunnels through the rock.</summary>
public class RouteTests
{
    [Fact]
    public void TheCaveKeepsCloseToItsRouteWithShortcutTunnelsThroughTheRock()
    {
        int levels = 0, shortcuts = 0;
        foreach (var id in TestLevels.All(p => p.Id.Depth <= 5).Take(12))
        {
            var map = TestLevels.Get(id);
            levels++;
            var route = map.Corridors.Where(c => c.Kind == CorridorKind.Main).ToList();
            float FromRoute(Vector2 p) => route.Min(c => Enumerable.Range(1, c.Points.Count - 1).Min(i => Geo.SegmentDistance(p, c.Points[i - 1], c.Points[i])));
            // Every side chamber lies near the route (its middle within 24 m of it, give or take the swell).
            foreach (var nave in map.Naves) Assert.True(FromRoute(nave.At) <= 26f, $"{id}: a nave {FromRoute(nave.At):0} m from the route");
            // Shortcuts: passes bored through the rock, every one roofed by it (its open mouths aside).
            foreach (var (pass, index) in map.Corridors.Select((c, i) => (c, i)).Where(t => t.c.Kind == CorridorKind.Pass))
            {
                shortcuts++;
                Assert.True(pass.Roofed);
                Assert.Contains(map.Canopies, c => c.Kind == CanopyKind.TunnelRoof && c.Corridor == index);
                var mid = pass.Points[pass.Points.Count / 2];
                Assert.True(map.IsOpen(mid), $"{id}: a pass blocked");
                Assert.False(float.IsNaN(map.RoofAt((int)MathF.Round(mid.X), (int)MathF.Round(mid.Y))), $"{id}: a pass open to the sky in its middle");
            }
            // The way down stays far enough along the water, shortcuts and all.
            float way = LevelValidator.DistanceAt(LevelValidator.Distances(map, map.Start.Position), map.Exit.Position);
            Assert.True(way >= LevelValidator.MinExitGeodesic, $"{id}: the way down {way:0} m");
        }
        // Most levels have one (where the rock is thick enough to bore one through).
        Assert.True(shortcuts * 4 >= levels * 3, $"{shortcuts} shortcuts over {levels} levels");
    }
}
