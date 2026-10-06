using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Sim;
using OctoShoots.Core.Terrain;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>The Depth 1 den creatures (docs/DEPTH1-BESTIARY.md): noticing, leash, and each attack.</summary>
public class CreatureTests
{
    static readonly Lazy<CreatureCatalog> Shared = new(() =>
        CreatureCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "creatures.json"))));

    static readonly Vector3 Mid = new(24f, 14f, 22f);

    /// <summary>One den in the grey-box chamber; the player is moved by the test.</summary>
    static World Den(EnemyKind kind, Vector3 at, Vector3? up = null, int count = 1, bool bed = false, Action<CreatureDef>? tweak = null)
    {
        var catalog = Shared.Value.Clone();
        tweak?.Invoke(catalog[kind]);
        var cave = TestWorlds.Cave.Clone(new[] { new DenSpot(kind, at, up ?? Vector3.UnitY, count, bed) });
        var tuning = new Tuning { EnemyCount = 0, NestsEnabled = false, BubbleCapacity = 12, BubbleRegrowDelay = 0f, BubbleRegrowInterval = 0.05f, AimAssist = AimAssistLevel.Off };
        var world = new World(cave, tuning, TestWorlds.Seed, ItemWorlds.Catalog, null, catalog);
        world.Teleport(new Vector3(100f, 100f, 100f));
        world.Events.Clear();
        return world;
    }

    static void Run(World w, int ticks, PlayerInput? input = null) => TestWorlds.Run(w, input ?? TestWorlds.Look(0f), ticks);

    static Enemy Only(World w) => w.Enemies.First(e => World.IsDenCreature(e.Kind));

    static int Seconds(float s) => (int)(s * World.TickRate);

    [Fact]
    public void TheCatalogCoversEveryCreatureAndRejectsShortTelegraphs()
    {
        var catalog = Shared.Value;
        foreach (var kind in CreatureCatalog.Kinds) Assert.True(catalog[kind].Telegraph >= 0.45f, kind.ToString());
        Assert.Equal(30f, catalog[EnemyKind.Barracuda].NoticeRange);
        var json = catalog.ToJson().Replace("\"telegraph\": 0.8,", "\"telegraph\": 0.2,");
        Assert.Throws<InvalidOperationException>(() => CreatureCatalog.FromJson(json));
    }

    [Fact]
    public void ACreatureNoticesClementineBeyondItsBodyAndStartlesBeforeActing()
    {
        var world = Den(EnemyKind.Barracuda, Mid);
        var barracuda = Only(world);
        world.Teleport(Mid + new Vector3(0f, 0f, 14f));
        world.Player.LoudTimer = 2f;
        bool noticed = false;
        for (int i = 0; i < 12; i++)
        {
            world.Step(TestWorlds.Look(0f));
            noticed |= world.Events.Any(e => e.Type == SimEventType.CreatureNoticed && e.EntityId == barracuda.Id);
        }
        Assert.True(noticed);
        Assert.Equal(EnemyState.Alert, barracuda.State);
        Run(world, Seconds(0.7f));
        Assert.NotEqual(EnemyState.Alert, barracuda.State);
        Assert.NotEqual(EnemyState.Idle, barracuda.State);
    }

    [Fact]
    public void NoticeRangeDependsOnTheCreature()
    {
        var moray = Den(EnemyKind.Moray, Mid);
        moray.Teleport(Mid + new Vector3(0f, 0f, 6f));
        moray.Player.LoudTimer = 0f;
        Run(moray, 30);
        Assert.Equal(EnemyState.Idle, Only(moray).State); // 5 m notice, halved while drifting
        var dancer = Den(EnemyKind.SpanishDancer, Mid);
        dancer.Teleport(Mid + new Vector3(0f, 0f, 6f));
        Run(dancer, 12);
        Assert.NotEqual(EnemyState.Idle, Only(dancer).State);
    }

    [Fact]
    public void DriftingHalvesAndNoisyActionsDoubleTheRange()
    {
        var world = Den(EnemyKind.Barracuda, Mid, tweak: d => d.NoticeRange = 10f);
        world.Teleport(Mid + new Vector3(0f, 0f, 7f));
        Run(world, 30);
        Assert.Equal(EnemyState.Idle, Only(world).State); // drifting: 10 m becomes 5 m
        world.Player.LoudTimer = 2f;
        Run(world, 12);
        Assert.NotEqual(EnemyState.Idle, Only(world).State); // just made a noise: 20 m
    }

    [Fact]
    public void InkCloudHidesHer()
    {
        var world = Den(EnemyKind.Barracuda, Mid);
        world.Teleport(Mid + new Vector3(0f, 0f, 8f));
        for (int i = 0; i < 60; i++)
        {
            world.Player.HiddenTimer = 1f;
            world.Step(TestWorlds.Look(0f));
        }
        Assert.Equal(EnemyState.Idle, Only(world).State);
    }

    [Fact]
    public void ACreatureLosesHerBeyondItsGiveUpRangeAndGoesHome()
    {
        var world = Den(EnemyKind.Pufferling, Mid, tweak: d => { d.NoticeRange = 20f; d.GiveUpRange = 25f; });
        world.Teleport(Mid + new Vector3(0f, 0f, 8f));
        world.Player.LoudTimer = 2f;
        Run(world, Seconds(1f));
        var puffer = Only(world);
        Assert.NotEqual(EnemyState.Idle, puffer.State);
        world.Teleport(new Vector3(300f, 10f, 300f)); // far away (the sea beyond the chamber is empty rock here)
        world.Events.Clear();
        world.Step(TestWorlds.Look(0f));
        Assert.Equal(EnemyState.Idle, puffer.State);
        Assert.Contains(world.Events, e => e.Type == SimEventType.CreatureLost);
    }

    [Fact]
    public void DensFarAwayStaySound()
    {
        var world = Den(EnemyKind.MoonJelly, Mid, count: 6);
        var before = world.Enemies.Select(e => e.Position).ToArray();
        world.Teleport(new Vector3(300f, 10f, 300f));
        Run(world, 120);
        Assert.Equal(before, world.Enemies.Select(e => e.Position).ToArray());
    }

    [Fact]
    public void ADancerFlaresARingOfSixSporesAfterATelegraph()
    {
        var world = Den(EnemyKind.SpanishDancer, Mid);
        var dancer = Only(world);
        world.Teleport(Mid + new Vector3(0f, 0f, 4f));
        bool telegraphed = false, fired = false;
        int spores = 0;
        for (int i = 0; i < Seconds(8f) && !fired; i++)
        {
            world.Player.Hp = 100f;
            world.Step(TestWorlds.Look(0f));
            telegraphed |= world.Events.Any(e => e.Type == SimEventType.EnemyTelegraph && e.Tag == "SpanishDancer");
            if (world.Events.Any(e => e.Type == SimEventType.EnemyShotFired && e.Tag == "spore"))
            {
                fired = true;
                world.Step(TestWorlds.Look(0f));
                spores = world.Projectiles.Count(p => p.Kind == ProjectileKind.Spore);
            }
        }
        Assert.True(telegraphed, "it winds up first");
        Assert.True(fired);
        Assert.Equal(6, spores);
        Assert.All(world.Projectiles.Where(p => p.Kind == ProjectileKind.Spore), p => Assert.True(MathF.Abs(p.Velocity.Y) < 1e-3f));
        Assert.Equal(EnemyState.Recover, dancer.State);
    }

    [Fact]
    public void ABarracudaChargesInAStraightLineAndOvershoots()
    {
        var world = Den(EnemyKind.Barracuda, Mid);
        var fish = Only(world);
        world.Teleport(Mid + new Vector3(0f, 0f, 9f));
        bool charging = false;
        Vector3 start = default, aim = default;
        for (int i = 0; i < Seconds(12f); i++)
        {
            world.Player.LoudTimer = 2f;
            world.Step(TestWorlds.Look(0f));
            if (!charging && fish.State == EnemyState.Attack)
            {
                charging = true;
                start = fish.Position;
                aim = fish.Aim;
                world.Teleport(fish.Position + Vector3.UnitY * 6f); // she steps aside: it must not turn to follow
            }
            if (charging && fish.State == EnemyState.Recover) break;
        }
        Assert.True(charging);
        Assert.Equal(EnemyState.Recover, fish.State);
        Vector3 travelled = fish.Position - start;
        Assert.True(travelled.Length() < 1f || Vector3.Dot(Vector3.Normalize(travelled), aim) > 0.98f);
    }

    [Fact]
    public void ABarracudaChargeHurtsForSixteen()
    {
        var world = Den(EnemyKind.Barracuda, Mid);
        Vector3 spot = Mid + new Vector3(0f, 0f, 9f);
        world.Teleport(spot);
        float hp = world.Player.Hp;
        for (int i = 0; i < Seconds(12f) && world.Player.Hp >= hp; i++)
        {
            // She holds still, so the charge runs straight into her.
            world.Player.LoudTimer = 2f;
            world.Teleport(spot);
            world.Step(TestWorlds.Look(0f));
        }
        Assert.Equal(hp - 16f, world.Player.Hp, 0.01f);
    }

    [Fact]
    public void APufferlingSwellsThenBurstsIntoTwelveSpines()
    {
        var world = Den(EnemyKind.Pufferling, Mid);
        var puffer = Only(world);
        world.Teleport(Mid + new Vector3(0f, 0f, 3f));
        bool swelled = false;
        int spines = 0;
        float baseRadius = world.RadiusOf(puffer);
        for (int i = 0; i < Seconds(8f); i++)
        {
            world.Player.Hp = 100f;
            world.Step(TestWorlds.Look(0f));
            swelled |= puffer.State == EnemyState.Attack && world.RadiusOf(puffer) > baseRadius * 2f;
            if (world.Events.Any(e => e.Type == SimEventType.EnemyShotFired && e.Tag == "burst"))
            {
                world.Step(TestWorlds.Look(0f));
                spines = world.Projectiles.Count(p => p.Kind == ProjectileKind.Spine);
                break;
            }
        }
        Assert.True(swelled);
        Assert.Equal(12, spines);
    }

    [Fact]
    public void AHitSwollenPufferlingBurstsAtOnce()
    {
        var world = Den(EnemyKind.Pufferling, Mid);
        var puffer = Only(world);
        world.Teleport(Mid + new Vector3(0f, 0f, 3f));
        for (int i = 0; i < Seconds(8f) && puffer.State != EnemyState.Attack; i++)
        {
            world.Player.Hp = 100f;
            world.Step(TestWorlds.Look(0f));
        }
        Assert.Equal(EnemyState.Attack, puffer.State);
        puffer.StateTimer = 1f;
        puffer.Hp = 100f;
        world.Teleport(puffer.Position + new Vector3(0f, 0f, 5f));
        var shoot = TestWorlds.Look(0f);
        shoot.Fire = true;
        for (int i = 0; i < 90 && puffer.State == EnemyState.Attack; i++)
        {
            world.Player.Hp = 100f;
            world.Step(shoot);
        }
        Assert.NotEqual(EnemyState.Attack, puffer.State);
    }

    [Fact]
    public void JellySwarmsPulseAtHerAndHurtOnTouch()
    {
        var world = Den(EnemyKind.MoonJelly, Mid, count: 6);
        Assert.Equal(6, world.Enemies.Count);
        world.Teleport(Mid + new Vector3(0f, 0f, 5f));
        float hp = world.Player.Hp;
        for (int i = 0; i < Seconds(12f) && world.Player.Hp >= hp; i++) world.Step(TestWorlds.Look(0f));
        Assert.Equal(hp - 6f, world.Player.Hp, 0.01f);
    }

    [Fact]
    public void ASeaUrchinBloomsTwelveSpinesAwayFromItsSurface()
    {
        var world = Den(EnemyKind.SeaUrchin, new Vector3(Mid.X, 8.4f, Mid.Z), Vector3.UnitY);
        var urchin = Only(world);
        world.Teleport(Mid + new Vector3(0f, -1f, 6f));
        int spines = 0;
        for (int i = 0; i < Seconds(6f); i++)
        {
            world.Player.Hp = 100f;
            world.Player.LoudTimer = 2f;
            world.Step(TestWorlds.Look(0f));
            if (world.Events.Any(e => e.Type == SimEventType.EnemyShotFired && e.Tag == "bloom"))
            {
                world.Step(TestWorlds.Look(0f));
                spines = world.Projectiles.Count(p => p.Kind == ProjectileKind.Spine);
                break;
            }
        }
        Assert.Equal(12, spines);
        Assert.All(world.Projectiles.Where(p => p.Kind == ProjectileKind.Spine), p => Assert.True(Vector3.Dot(Vector3.Normalize(p.Velocity), Vector3.UnitY) > -0.45f));
        Assert.Equal(EnemyState.Recover, urchin.State);
    }

    [Fact]
    public void UrchinBedsNeverShootButPrickForEight()
    {
        var world = Den(EnemyKind.SeaUrchin, new Vector3(Mid.X, 8.4f, Mid.Z), Vector3.UnitY, count: 4, bed: true);
        Assert.Equal(4, world.Enemies.Count);
        Assert.All(world.Enemies, e => Assert.True(e.Small));
        world.Teleport(Mid + new Vector3(0f, -1f, 5f));
        Run(world, Seconds(5f));
        Assert.Empty(world.Projectiles);
        var bed = world.Enemies[0];
        float hp = world.Player.Hp;
        world.Teleport(bed.Position + Vector3.UnitY * 0.3f);
        world.Step(TestWorlds.Look(0f));
        Assert.Equal(hp - 8f, world.Player.Hp, 0.01f);
        Assert.Equal(CreatureDef.BedHp, bed.Hp);
    }

    [Fact]
    public void AMorayLungesSixMetresAndBitesForEighteen()
    {
        var world = Den(EnemyKind.Moray, new Vector3(Mid.X, 8.4f, Mid.Z), Vector3.UnitY);
        var moray = Only(world);
        Vector3 anchor = moray.Anchor;
        Vector3 spot = anchor + new Vector3(0f, 4f, 0f);
        world.Teleport(spot);
        float maxExtension = 0f;
        float hp = world.Player.Hp;
        for (int i = 0; i < Seconds(4f); i++)
        {
            world.Player.LoudTimer = 2f;
            world.Step(TestWorlds.Look(0f));
            maxExtension = MathF.Max(maxExtension, moray.Aux);
            world.Teleport(spot);
        }
        Assert.InRange(maxExtension, 3.5f, 6.05f);
        Assert.True(world.Player.Hp < hp, "the bite landed");
    }

    [Fact]
    public void ACrabbyLeapsAtSomeoneHoveringAboveIt()
    {
        var world = Den(EnemyKind.Crabby, new Vector3(Mid.X, 8.4f, Mid.Z));
        var crab = Only(world);
        world.Teleport(new Vector3(Mid.X + 2f, 12f, Mid.Z));
        bool leapt = false;
        for (int i = 0; i < Seconds(6f) && !leapt; i++)
        {
            world.Player.Hp = 100f;
            world.Player.LoudTimer = 2f;
            world.Step(TestWorlds.Look(0f));
            leapt = crab.State == EnemyState.Attack;
        }
        Assert.True(leapt);
        float top = crab.Position.Y;
        for (int i = 0; i < 60; i++)
        {
            world.Player.Hp = 100f;
            world.Teleport(new Vector3(Mid.X + 2f, 16f, Mid.Z));
            world.Step(TestWorlds.Look(0f));
            top = MathF.Max(top, crab.Position.Y);
        }
        Assert.True(top > 11f, $"leapt to {top}");
    }

    [Fact]
    public void SlainCreaturesStayDeadUntilClementineDies()
    {
        var world = Den(EnemyKind.Pufferling, Mid);
        var puffer = Only(world);
        puffer.Hp = 0.001f;
        world.Teleport(puffer.Position + new Vector3(0f, 0f, 4f));
        var shoot = TestWorlds.Look(0f);
        shoot.Fire = true;
        for (int i = 0; i < 90 && puffer.Alive; i++)
        {
            world.Player.Hp = 100f;
            world.Step(shoot);
        }
        Assert.Equal(EnemyState.Dead, puffer.State);
        Run(world, Seconds(120f));
        Assert.Equal(EnemyState.Dead, puffer.State);
    }

    [Fact]
    public void DenCreaturesAreDeterministic()
    {
        ulong Hash()
        {
            var world = Den(EnemyKind.Barracuda, Mid, count: 2);
            world.Teleport(Mid + new Vector3(0f, 0f, 10f));
            Run(world, Seconds(8f));
            return world.StateHash();
        }
        Assert.Equal(Hash(), Hash());
    }
}
