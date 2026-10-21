using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Run;
using Xunit;
using Xunit.Abstractions;

namespace OctoShoots.Core.Tests;

/// <summary>Unreachable water raised into mountains (DESIGN-TOPDOWN §4.1).</summary>
public class MountainsTests
{
    readonly ITestOutputHelper _out;

    public MountainsTests(ITestOutputHelper output) => _out = output;

    [Theory]
    [InlineData("KELP7Q2Z")]
    [InlineData("REEF2345")]
    [InlineData("CRABSHLL")]
    public void NoOpenFloorIsLeftThatSheCannotReach_AndWhatSheCouldReachStaysOpen(string seed)
    {
        foreach (var id in new[] { LevelId.First, LevelId.FirstRun, new LevelId(1, 2, 3) })
        {
            var map = TopDownGenerator.Generate(new RunStreams(SeedCode.Parse(seed)), id, log: s => { if (s.Contains("mountains")) _out.WriteLine(s); });
            var dist = LevelValidator.Distances(map, map.Start.Position);
            int stranded = 0, raised = 0;
            const int N = LevelMap.Samples;
            for (int y = 0; y < LevelMap.Size; y++)
            for (int x = 0; x < LevelMap.Size; x++)
            {
                var c = new Vector2(x + 0.5f, y + 0.5f);
                bool open = map.IsOpen(c);
                bool near = false;
                for (int dy = -Mountains.Keep - 1; dy <= Mountains.Keep + 1 && !near; dy++)
                for (int dx = -Mountains.Keep - 1; dx <= Mountains.Keep + 1 && !near; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    near = nx >= 0 && ny >= 0 && nx < LevelMap.Size && ny < LevelMap.Size && float.IsFinite(dist[ny * LevelMap.Size + nx]);
                }
                if (open && !near && !map.Shaft.Contains(c, 3f)) stranded++;
                if (map.Heights[y * N + x] > 4f && !open) raised++;
            }
            _out.WriteLine($"{seed} {id}: {stranded} stranded open cells");
            Assert.True(stranded <= 3, $"{seed} {id}: {stranded} open cells she cannot reach");
            // Everywhere she could reach before is still reachable (the places, the exit).
            Assert.True(float.IsFinite(LevelValidator.DistanceAt(dist, map.Exit.Position)));
            foreach (var poi in map.Pois) Assert.True(float.IsFinite(LevelValidator.DistanceAt(dist, poi.Position)) || poi.Kind == PoiKind.Secret, $"{poi.Kind}");
        }
    }
}
