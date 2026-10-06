using Xunit;

namespace OctoShoots.Core.Tests;

public class RngTests
{
    [Fact]
    public void SameSeedGivesSameSequence()
    {
        var a = new Rng(42);
        var b = new Rng(42);
        for (int i = 0; i < 1000; i++) Assert.Equal(a.NextU64(), b.NextU64());
    }

    [Fact]
    public void NamedStreamsAreIndependentAndStable()
    {
        var root = new Rng(42);
        Assert.NotEqual(root.Stream("enemies").NextU64(), root.Stream("cave").NextU64());
        Assert.Equal(new Rng(42).Stream("cave").NextU64(), new Rng(42).Stream("cave").NextU64());
    }

    [Fact]
    public void FloatsStayInUnitRange()
    {
        var rng = new Rng(7);
        for (int i = 0; i < 10000; i++)
        {
            float f = rng.NextFloat();
            Assert.InRange(f, 0f, 0.99999994f);
        }
    }
}
