using System.Linq;
using System.Numerics;
using OctoShoots.Core.Sim;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Ink bombs and craters in the running simulation (§6.3).</summary>
public class TerrainWorldTests
{
    /// <summary>Hovering just above the grey-box floor (y 8.3), looking along +X.</summary>
    static World NearFloor(int enemies = 0)
    {
        var world = ItemWorlds.Create(enemies);
        world.Player.Position = world.Player.PrevPosition = new Vector3(24f, 9.2f, 22f);
        return world;
    }

    static PlayerInput Drop()
    {
        var input = ItemWorlds.LookPlusX();
        input.DropBomb = true;
        return input;
    }

    static int Ticks(float seconds) => (int)(seconds / World.Dt) + 2;

    [Fact]
    public void BombSinksAndDigsACrater()
    {
        var world = NearFloor();
        Assert.Equal(3, world.Bombs);
        world.Step(Drop());
        Assert.Equal(2, world.Bombs);
        var bomb = Assert.Single(world.LiveBombs);
        float y0 = bomb.Position.Y;

        // Swim away so the blast doesn't matter here.
        var input = ItemWorlds.LookPlusX();
        input.Vertical = 1f;
        bool carved = false;
        for (int i = 0; i < Ticks(world.Tuning.BombFuse); i++)
        {
            world.Step(input);
            carved |= world.Events.Any(e => e.Type == SimEventType.TerrainCarved);
        }
        Assert.True(carved);
        Assert.Empty(world.LiveBombs);
        var crater = Assert.Single(world.Craters);
        Assert.True(crater.Center.Y < y0);
        Assert.True(world.Sdf.Sample(crater.Center + new Vector3(0f, -1.5f, 0f)) > 0f, "the floor under the bomb should be blown open");
    }

    [Fact]
    public void BombsHurtCreaturesAndClementineNearby()
    {
        var world = NearFloor(1);
        var fish = ItemWorlds.Place(world, 0, new Vector3(1.5f, -12.8f + 8.3f + 1f, 0f));
        world.Step(Drop());
        TestWorlds.Run(world, ItemWorlds.LookPlusX(), Ticks(world.Tuning.BombFuse));
        Assert.True(fish.Hp < 100f);
        Assert.True(world.Player.Hp <= world.Tuning.MaxHp - world.Tuning.BombSelfDamage);
    }

    [Fact]
    public void NoBombsNoDrop()
    {
        var world = NearFloor();
        world.AddBombs(-99);
        world.Step(Drop());
        Assert.Empty(world.LiveBombs);
        Assert.Contains(world.Events, e => e.Type == SimEventType.ActiveNotReady && e.Tag == "bomb");
    }

    [Fact]
    public void InkSacShotsDigSmallCraters()
    {
        var world = WithItems("ink_sac");
        ItemWorlds.FireOnce(world, pitchDeg: -85f);
        Assert.True(ItemWorlds.RunUntil(world, w => w.Events.Any(e => e.Type == SimEventType.TerrainCarved), 60, -85f));
        Assert.Equal(world.Tuning.InkCraterRadius, Assert.Single(world.Craters).Radius);
    }

    [Fact]
    public void PlainInkNeverDigs()
    {
        var world = WithItems();
        ulong before = world.Sdf.ContentHash();
        ItemWorlds.FireOnce(world, pitchDeg: -85f);
        TestWorlds.Run(world, ItemWorlds.LookPlusX(false, -85f), 60);
        Assert.Empty(world.Craters);
        Assert.Equal(before, world.Sdf.ContentHash());
    }

    [Fact]
    public void BeamsBurnRockSlowly()
    {
        var world = WithItems("sunbeam");
        TestWorlds.Run(world, ItemWorlds.LookPlusX(fire: true, -85f), 60);
        TestWorlds.Run(world, ItemWorlds.LookPlusX(false, -85f), 30);
        Assert.NotEmpty(world.Craters);
        Assert.All(world.Craters, c => Assert.Equal(world.Tuning.BeamCraterRadius, c.Radius));
    }

    [Fact]
    public void CratersSurviveSaveAndLoad()
    {
        var world = NearFloor();
        world.Carve(new Vector3(24f, 8.3f, 22f), 2.5f);
        world.Carve(new Vector3(30f, 8.3f, 18f), 0.8f);
        var run = Saves.SaveCodec.Deserialize(Saves.SaveCodec.Serialize(new Saves.SaveFile { Run = world.Snapshot("KELP 7Q2Z", false) })).Run!;

        var fresh = ItemWorlds.Create(0);
        Assert.NotEqual(world.Sdf.ContentHash(), fresh.Sdf.ContentHash());
        fresh.Restore(run);
        Assert.Equal(world.Sdf.ContentHash(), fresh.Sdf.ContentHash());
        Assert.Equal(2, fresh.Craters.Count);
    }

    [Fact]
    public void CreaturesSpawnNearTheStartFirst()
    {
        var reef = ReefTests.Reef("KELP 7Q2Z", 1, 1);
        var world = new World(reef.Cave.Clone(), new Tuning { EnemyCount = 3 }, 1);
        float nearest = reef.Cave.EnemySpawns.Where(s => Vector3.Distance(s, reef.Cave.PlayerSpawn) >= 10f).Min(s => Vector3.Distance(s, reef.Cave.PlayerSpawn));
        Assert.Contains(world.Enemies, e => System.MathF.Abs(Vector3.Distance(e.Position, reef.Cave.PlayerSpawn) - nearest) < 0.01f);
    }

    static World WithItems(params string[] items)
    {
        var world = ItemWorlds.Create(0);
        foreach (var id in items) world.GiveItem(id);
        return world;
    }
}
