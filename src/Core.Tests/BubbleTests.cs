using System.Linq;
using OctoShoots.Core.Items;
using OctoShoots.Core.Sim;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Bubbles on the throwing tentacle: capacity, spending, regrowth and the items that change them.</summary>
public class BubbleTests
{
    /// <summary>A world with the real bubble rules (defaults from Tuning).</summary>
    static World Create(params string[] items)
    {
        var world = ItemWorlds.Create(0, tweak: t =>
        {
            var defaults = new Tuning();
            t.BubbleCapacity = defaults.BubbleCapacity;
            t.BubbleRegrowDelay = defaults.BubbleRegrowDelay;
            t.BubbleRegrowInterval = defaults.BubbleRegrowInterval;
        });
        foreach (var id in items) world.GiveItem(id);
        return world;
    }

    static int Shots(World w) => w.Events.Count(e => e.Type == SimEventType.ShotFired);

    [Fact]
    public void TheTentacleStartsFull()
    {
        var world = Create();
        Assert.Equal(8, world.BubbleCapacity);
        Assert.Equal(8, world.Player.Bubbles);
    }

    [Fact]
    public void HoldingFireRunsDryThenWaits()
    {
        var world = Create();
        int shots = 0;
        bool empty = false;
        var fire = ItemWorlds.LookPlusX(fire: true);
        // 8 bubbles at 2.7 throws/s last ~2.6 s; the pause stops regrowth while she keeps throwing.
        for (int i = 0; i < 60 * 4; i++)
        {
            world.Step(fire);
            shots += Shots(world);
            empty |= world.Events.Any(e => e.Type == SimEventType.BubblesEmpty);
        }
        Assert.True(empty);
        Assert.InRange(shots, 8, 10);
    }

    [Fact]
    public void BubblesRegrowAfterADelay()
    {
        var world = Create();
        world.Step(ItemWorlds.LookPlusX(fire: true));
        Assert.Equal(7, world.Player.Bubbles);

        var t = world.Tuning;
        int delayTicks = (int)(t.BubbleRegrowDelay / World.Dt);
        TestWorlds.Run(world, ItemWorlds.LookPlusX(), delayTicks - 2);
        Assert.Equal(7, world.Player.Bubbles);
        TestWorlds.Run(world, ItemWorlds.LookPlusX(), (int)(t.BubbleRegrowInterval / World.Dt) + 4);
        Assert.Equal(8, world.Player.Bubbles);
    }

    [Fact]
    public void BubbleGlandAddsSuckers()
    {
        var world = Create("bubble_gland");
        Assert.Equal(11, world.BubbleCapacity);
        // New suckers start empty and fill over time.
        TestWorlds.Run(world, ItemWorlds.LookPlusX(), 60 * 3);
        Assert.Equal(11, world.Player.Bubbles);
    }

    [Fact]
    public void AnemonePumpRegrowsFaster()
    {
        int TicksToRefill(World w)
        {
            w.Player.Bubbles = 0;
            for (int i = 1; i < 60 * 10; i++)
            {
                w.Step(ItemWorlds.LookPlusX());
                if (w.Player.Bubbles == w.BubbleCapacity) return i;
            }
            return int.MaxValue;
        }

        int plain = TicksToRefill(Create());
        int pumped = TicksToRefill(Create("anemone_pump"));
        Assert.True(pumped < plain * 0.75f, $"{pumped} vs {plain}");
    }

    [Fact]
    public void TwinSiphonThrowsTwoAtOnce()
    {
        var world = Create("twin_siphon");
        world.Step(ItemWorlds.LookPlusX(fire: true));
        Assert.Equal(2, Shots(world));
        Assert.Equal(6, world.Player.Bubbles);
        Assert.Equal(2, world.Projectiles.Count);
    }

    [Fact]
    public void ThrowingWithOneBubbleLeftThrowsOne()
    {
        var world = Create("twin_siphon");
        world.Player.Bubbles = 1;
        world.Step(ItemWorlds.LookPlusX(fire: true));
        Assert.Equal(1, Shots(world));
        Assert.Equal(0, world.Player.Bubbles);
    }

    [Fact]
    public void PearlsCostOneBubbleAndNeedOne()
    {
        var world = Create("pearl_diver");
        world.Player.Bubbles = 0;
        world.Player.RegrowDelay = 99f;
        TestWorlds.Run(world, ItemWorlds.LookPlusX(fire: true), 30);
        Assert.Equal(0f, world.Player.Charge);

        world.Player.Bubbles = 2;
        world.Player.RegrowDelay = 99f;
        TestWorlds.Run(world, ItemWorlds.LookPlusX(fire: true), 70);
        world.Step(ItemWorlds.LookPlusX());
        Assert.Equal(1, world.Player.Bubbles);
        Assert.Single(world.Projectiles, p => p.Kind == ProjectileKind.Pearl);
    }

    [Fact]
    public void CaptionsDescribeBubbleItems()
    {
        Assert.Contains("+3 bubbles on the tentacle", ItemCaption.Describe(ItemWorlds.Catalog["bubble_gland"]));
        Assert.Contains("×1.6 bubble regrowth speed", ItemCaption.Describe(ItemWorlds.Catalog["anemone_pump"]));
        Assert.Contains("+1 bubbles per throw", ItemCaption.Describe(ItemWorlds.Catalog["twin_siphon"]));
    }
}
