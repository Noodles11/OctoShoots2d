using System.Linq;
using System.Numerics;
using OctoShoots.Core.Loot;
using OctoShoots.Core.Saves;
using OctoShoots.Core.Sim;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Pearl shells, chests, pickups, drops and buried coins (grey-box cave loot plan).</summary>
public class LootTests
{
    static World Create(int enemies = 0) => ItemWorlds.Create(enemies);

    static PearlShell Treasure(World w) => w.Shells.Single(s => s.Price == 0);
    static PearlShell Shop(World w) => w.Shells.Single(s => s.Price > 0);

    [Fact]
    public void ShellsHoldPearlsFromTheirPools()
    {
        var world = Create();
        Assert.Equal(2, world.Shells.Count);
        Assert.All(world.Shells, s => Assert.NotNull(s.ItemId));
        Assert.Contains("treasure", ItemWorlds.Catalog[Treasure(world).ItemId!].Pools);
        Assert.Contains("shop", ItemWorlds.Catalog[Shop(world).ItemId!].Pools);
        Assert.InRange(Shop(world).Price, 15, 20);
    }

    [Fact]
    public void ShellOpensNearbyAndThePearlIsTakenOnTouch()
    {
        var world = Create();
        var shell = Treasure(world);
        string item = shell.ItemId!;
        world.Teleport(shell.Position + new Vector3(3f, 1f, 0f));
        world.Step(ItemWorlds.LookPlusX());
        Assert.True(shell.Open);
        Assert.Contains(world.Events, e => e.Type == SimEventType.ShellOpened && e.Tag == item);
        Assert.DoesNotContain(item, world.Items);

        world.Teleport(shell.PearlPosition + new Vector3(0.3f, 0.2f, 0f));
        world.Step(ItemWorlds.LookPlusX());
        Assert.Contains(item, world.Items);
        Assert.Null(shell.ItemId);
    }

    [Fact]
    public void ShopPearlsCostSandDollars()
    {
        var world = Create();
        var shell = Shop(world);
        world.Teleport(shell.PearlPosition + new Vector3(0f, 0.5f, 0f));
        world.Step(ItemWorlds.LookPlusX());
        Assert.Contains(world.Events, e => e.Type == SimEventType.TooPoor);
        Assert.NotNull(shell.ItemId);

        world.AddCoins(shell.Price + 3);
        world.Teleport(shell.PearlPosition + new Vector3(0f, 0.5f, 0f));
        world.Step(ItemWorlds.LookPlusX());
        Assert.Null(shell.ItemId);
        Assert.Equal(3, world.Coins);
    }

    [Fact]
    public void ShootingAChestSpillsLoot()
    {
        var world = Create();
        var chest = world.Chests.Single();
        // Hover 4 m in front of it and fire straight at it.
        world.Teleport(chest.Position + new Vector3(-4f, 0.2f, 0f));
        int before = world.Pickups.Count;
        var input = ItemWorlds.LookPlusX(fire: true);
        bool opened = false;
        for (int i = 0; i < 60 && !opened; i++)
        {
            world.Step(input);
            opened = world.Events.Any(e => e.Type == SimEventType.ChestOpened);
        }
        Assert.True(opened);
        Assert.True(world.Pickups.Count > before + 2);
    }

    [Fact]
    public void BombBlastsOpenChests()
    {
        var world = Create();
        var chest = world.Chests.Single();
        world.Teleport(chest.Position + new Vector3(-0.8f, 0.6f, 0f));
        var drop = ItemWorlds.LookPlusX();
        drop.DropBomb = true;
        world.Step(drop);
        world.Teleport(chest.Position + new Vector3(-12f, 2f, 0f));
        TestWorlds.Run(world, ItemWorlds.LookPlusX(), 120);
        Assert.True(chest.Opened);
    }

    [Fact]
    public void PickupsAreCollectedOnTouch()
    {
        var world = Create();
        TestWorlds.Run(world, ItemWorlds.LookPlusX(), 60); // let the loot settle
        var coin = world.Pickups.First(k => k.Kind == PickupKind.Coin);
        world.Teleport(coin.Position + new Vector3(0f, 0.3f, 0f));
        world.Step(ItemWorlds.LookPlusX());
        Assert.True(world.Coins >= 1);
        Assert.DoesNotContain(coin, world.Pickups);
    }

    [Fact]
    public void HeartsWaitWhileHealthIsFull()
    {
        var world = Create();
        TestWorlds.Run(world, ItemWorlds.LookPlusX(), 60);
        var heart = world.Pickups.Single(k => k.Kind == PickupKind.HalfHeart);
        world.Teleport(heart.Position + new Vector3(0f, 0.3f, 0f));
        world.Step(ItemWorlds.LookPlusX());
        Assert.Contains(heart, world.Pickups);

        world.Player.Hp = 50f;
        world.Teleport(heart.Position + new Vector3(0f, 0.3f, 0f));
        world.Step(ItemWorlds.LookPlusX());
        Assert.Equal(50f + LootRules.HalfHeartHeal, world.Player.Hp, 1);
    }

    [Fact]
    public void PickupsSinkToTheFloor()
    {
        var world = Create();
        var coin = world.Pickups.First();
        coin.Position = coin.PrevPosition = coin.Position + new Vector3(0f, 4f, 0f);
        TestWorlds.Run(world, ItemWorlds.LookPlusX(), 60 * 8);
        Assert.True(coin.Position.Y < 9.2f);
    }

    [Fact]
    public void DefeatedCreaturesSometimesDropLoot()
    {
        var world = ItemWorlds.Create(1, tweak: t =>
        {
            t.EnemyHp = 1f;
            t.EnemyRespawnDelay = 0.05f;
        });
        int before = world.Pickups.Count;
        var fire = ItemWorlds.LookPlusX(fire: true);
        for (int kill = 0; kill < 25; kill++)
        {
            world.Teleport(ItemWorlds.Centre);
            ItemWorlds.Place(world, 0, new Vector3(4f, 0f, 0f));
            for (int i = 0; i < 60 && world.Enemies[0].Alive; i++) world.Step(fire);
            TestWorlds.Run(world, ItemWorlds.LookPlusX(), 25);
        }
        Assert.True(world.Kills >= 20, $"only {world.Kills} kills");
        Assert.True(world.Pickups.Count + world.Coins > before, "25 kills should drop something");
    }

    [Fact]
    public void CratersDigUpBuriedCoins()
    {
        var world = Create();
        var spot = world.Cave.Loot.Buried.Single();
        world.Carve(spot.Position + new Vector3(0f, 0.5f, 0f), 2f);
        Assert.Contains(world.Events, e => e.Type == SimEventType.CoinsDugUp && (int)e.Value == spot.Amount);
        Assert.Equal(spot.Amount, world.Pickups.Count(k => k.Kind == PickupKind.Coin && k.PlanIndex < 0));
        world.Events.Clear();
        world.Carve(spot.Position + new Vector3(0.5f, 0.5f, 0f), 2f);
        Assert.DoesNotContain(world.Events, e => e.Type == SimEventType.CoinsDugUp);
    }

    [Fact]
    public void TakenLootStaysTakenAfterLoading()
    {
        var world = Create();
        var shell = Treasure(world);
        world.Teleport(shell.PearlPosition + new Vector3(0f, 0.5f, 0f));
        world.Step(ItemWorlds.LookPlusX());
        TestWorlds.Run(world, ItemWorlds.LookPlusX(), 60);
        var coin = world.Pickups.First(k => k.PlanIndex >= 0 && k.Kind == PickupKind.Coin);
        world.Teleport(coin.Position + new Vector3(0f, 0.3f, 0f));
        world.Step(ItemWorlds.LookPlusX());
        world.Carve(world.Cave.Loot.Buried[0].Position, 2f);
        var run = SaveCodec.Deserialize(SaveCodec.Serialize(new SaveFile { Run = world.Snapshot("KELP 7Q2Z", false) })).Run!;

        var fresh = Create();
        fresh.Restore(run);
        Assert.Null(Treasure(fresh).ItemId);
        Assert.DoesNotContain(fresh.Pickups, k => k.PlanIndex == coin.PlanIndex);
        Assert.Equal(world.Items, fresh.Items);
    }

    [Fact]
    public void NothingSwimsAboveTheSurface()
    {
        var reef = ReefTests.Reef("KELP 7Q2Z");
        var world = new Sim.World(reef.Cave.Clone(), new Tuning { EnemyCount = 0 }, 1);
        var input = TestWorlds.Look(0f);
        input.Vertical = 1f;
        TestWorlds.Run(world, input, 60 * 4);
        Assert.True(world.Player.Position.Y <= reef.SurfaceY - world.Tuning.PlayerRadius + 0.001f);
    }
}
