using System.Linq;
using OctoShoots.Core.Gen.TopDown;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>The terrain port reproduces reef_generator.html exactly (reference values taken from the prototype's own code).</summary>
public class ReefTerrainTests
{
    public static readonly TheoryData<string, float[]> Reference = new()
    {
        // Heights at cells (0,0), (17,42), (75,75), (149,149), (120,33), (3,140).
        { "KELP 7Q2Z", new[] { 1.13622f, -2.93646f, 1.39094f, -2.05808f, -2.53865f, -1.34509f } },
        { "AAAA AAAA", new[] { -0.01258f, 3.21236f, -1.12055f, -0.97041f, -0.97511f, 2.07199f } },
        { "DEEP SEA9", new[] { 3.78235f, -1.09002f, -0.16422f, -0.44325f, -1.92007f, 1.17457f } },
    };

    static readonly (int X, int Y)[] Cells = { (0, 0), (17, 42), (75, 75), (149, 149), (120, 33), (3, 140) };

    [Theory]
    [MemberData(nameof(Reference))]
    public void TheSameSeedTextGivesThePrototypesHeights(string seed, float[] expected)
    {
        // The prototype's own grid (a level is smaller now; the terrain is sampled the same way).
        const int n = ReefTerrain.Cells + 1;
        var h = new float[n * n];
        ReefTerrain.Generate(h, n, seed);
        for (int i = 0; i < Cells.Length; i++)
            Assert.True(System.Math.Abs(expected[i] - h[Cells[i].Y * n + Cells[i].X]) < 1e-4f, $"cell {Cells[i]}: {h[Cells[i].Y * n + Cells[i].X]} vs {expected[i]}");
        // Exactly 40% of the prototype's 150×150 cells stand above level 0.
        int above = 0;
        for (int y = 0; y < ReefTerrain.Cells; y++)
        for (int x = 0; x < ReefTerrain.Cells; x++)
            if (h[y * n + x] > 0f) above++;
        Assert.Equal(0.4, above / (double)(ReefTerrain.Cells * ReefTerrain.Cells), 3);
        Assert.All(h, v => Assert.InRange(v, ReefTerrain.HMin, ReefTerrain.HMax));
    }

    [Fact]
    public void TheBrushFallsOffSmoothlyToZero()
    {
        Assert.Equal(1f, ReefTerrain.Falloff(0f), 5);
        Assert.Equal(0.5f, ReefTerrain.Falloff(0.5f), 5);
        Assert.Equal(0f, ReefTerrain.Falloff(1f));
        Assert.Equal(0f, ReefTerrain.Falloff(2f));
        float[] steps = Enumerable.Range(0, 11).Select(i => ReefTerrain.Falloff(i / 10f)).ToArray();
        for (int i = 1; i < steps.Length; i++) Assert.True(steps[i] <= steps[i - 1]);
    }
}
