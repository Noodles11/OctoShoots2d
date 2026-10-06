using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Sim;
using OctoShoots.Core.Terrain;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Healthy sea life and freeing corrupted creatures (DEPTH1-BESTIARY §8).</summary>
public class CritterTests
{
    static readonly Lazy<CreatureCatalog> Shared = new(() =>
        CreatureCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "creatures.json"))));

    static readonly Vector3 Mid = new(24f, 14f, 22f);

    static World Den(EnemyKind kind, Vector3 at, int count = 1)
    {
        var cave = TestWorlds.Cave.Clone(new[] { new DenSpot(kind, at, Vector3.UnitY, count) });
        var tuning = new Tuning { EnemyCount = 0, NestsEnabled = false, BubbleCapacity = 12, BubbleRegrowDelay = 0f, BubbleRegrowInterval = 0.05f, AimAssist = AimAssistLevel.Off };
        var world = new World(cave, tuning, TestWorlds.Seed, ItemWorlds.Catalog, null, Shared.Value.Clone());
        world.Teleport(new Vector3(100f, 100f, 100f));
        world.Events.Clear();
        return world;
    }

    [Fact]
    public void AtDepthOneEachCorruptedCreatureHasAboutTwoHealthyTwins()
    {
        Assert.Equal(1f / 3f, World.CorruptedShare(1), 3);
        Assert.Equal(0.75f, World.CorruptedShare(6), 3);
        var world = Den(EnemyKind.MoonJelly, Mid, count: 6);
        Assert.Equal(6, world.Enemies.Count);
        Assert.Equal(12, world.Critters.Count);
        Assert.All(world.Critters, c => Assert.Equal(EnemyKind.MoonJelly, c.Kind));
        Assert.All(world.Critters, c => Assert.True(c.FreedAge < 0f));
    }

    [Fact]
    public void DefeatingACorruptedCreatureFreesIt()
    {
        var world = Den(EnemyKind.Pufferling, Mid);
        var puffer = world.Enemies.Single();
        int healthy = world.Critters.Count;
        puffer.Hp = 0.01f;
        world.Teleport(puffer.Position + new Vector3(0f, 0f, 4f));
        var shoot = TestWorlds.Look(0f);
        shoot.Fire = true;
        bool freed = false;
        for (int i = 0; i < 90 && !freed; i++)
        {
            world.Player.Hp = 100f;
            world.Step(shoot);
            freed = world.Events.Any(e => e.Type == SimEventType.CreatureFreed && e.Tag == "Pufferling");
        }
        Assert.True(freed);
        Assert.Equal(EnemyState.Dead, puffer.State);
        Assert.Equal(healthy + 1, world.Critters.Count);
        var critter = world.Critters.Last();
        Assert.Equal(EnemyKind.Pufferling, critter.Kind);
        Assert.True(critter.FreedAge >= 0f);
        Assert.Equal(1, world.Kills);
    }

    [Fact]
    public void HealthyCreaturesAreNeitherTargetsNorDangerous()
    {
        var world = Den(EnemyKind.SeaUrchin, new Vector3(Mid.X, 8.4f, Mid.Z));
        world.Enemies.Clear(); // only the healthy urchins remain
        var urchin = world.Critters.First();
        float hp = world.Player.Hp;
        for (int i = 0; i < 120; i++)
        {
            world.Teleport(urchin.Position + Vector3.UnitY * 0.2f);
            world.Step(TestWorlds.Look(0f));
        }
        Assert.Equal(hp, world.Player.Hp);

        // Bubbles pass straight through a healthy fish.
        var fish = Den(EnemyKind.Pufferling, Mid);
        fish.Enemies.Clear();
        var target = fish.Critters.First();
        var shoot = TestWorlds.Look(0f);
        shoot.Fire = true;
        bool hit = false;
        for (int i = 0; i < 60; i++)
        {
            target.Position = target.PrevPosition = Mid;
            fish.Teleport(Mid + new Vector3(0f, 0f, 3f));
            fish.Step(shoot);
            hit |= fish.Events.Any(e => e.Type is SimEventType.ShotHitEnemy or SimEventType.CreatureFreed);
        }
        Assert.False(hit);
        Assert.False(fish.ReticleOverEnemy(fish.Player.Position, -Vector3.UnitZ));
    }

    [Fact]
    public void HealthySwimmersKeepClearOfClementine()
    {
        var world = Den(EnemyKind.SpanishDancer, Mid);
        world.Enemies.Clear();
        var dancer = world.Critters.First();
        world.Teleport(dancer.Position + new Vector3(1.2f, 0f, 0f));
        float start = Vector3.Distance(dancer.Position, world.Player.Position);
        for (int i = 0; i < 90; i++)
        {
            world.Player.Velocity = Vector3.Zero;
            world.Step(TestWorlds.Look(0f));
        }
        Assert.True(Vector3.Distance(dancer.Position, world.Player.Position) > start + 0.5f);
    }
}
