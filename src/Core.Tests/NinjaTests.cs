using System;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen;
using OctoShoots.Core.Sim;
using OctoShoots.Core.Terrain;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Anemones, neutral clownfish and the clownfish ninjas hiding among them (§7.5).</summary>
public class NinjaTests
{
    /// <summary>The grey-box cave has one nest at (24.5, 8.35, 17); lanternfish are off.</summary>
    static World Nest(System.Action<Tuning>? tweak = null)
    {
        var tuning = new Tuning { EnemyCount = 0, NestsEnabled = true, BubbleCapacity = 12, BubbleRegrowDelay = 0f, BubbleRegrowInterval = 0.05f };
        tweak?.Invoke(tuning);
        var world = new World(TestWorlds.Cave.Clone(), tuning, TestWorlds.Seed, ItemWorlds.Catalog);
        world.Events.Clear();
        return world;
    }

    static Enemy Ninja(World w) => w.Enemies.Single(e => e.Kind == EnemyKind.ClownNinja);

    static AnemoneSpot Home(World w) => w.Anemones.Single(a => a.Nest);

    /// <summary>Hovers the player at a distance from the anemone, facing it, in open water.</summary>
    static void PlaceAt(World w, float distance, float height = 3.5f)
    {
        var a = Home(w);
        w.Teleport(new Vector3(a.Position.X + distance, a.Position.Y + height, a.Position.Z));
        w.Player.Yaw = MathUtil.Deg2Rad * 90f; // looks along -X, toward the anemone
    }

    static PlayerInput Face(World w)
    {
        var input = new PlayerInput { Yaw = MathUtil.Deg2Rad * 90f, ViewHalfAngleDeg = 45f };
        return input;
    }

    [Fact]
    public void ANestHoldsASchoolWithOneHiddenNinja()
    {
        var world = Nest();
        Assert.InRange(world.Clownfish.Count, 3, 5);
        var ninja = Ninja(world);
        Assert.Equal(EnemyState.Nested, ninja.State);
        Assert.False(ninja.Alive);
        Assert.Equal(Home(world).Mouth, ninja.Position);
    }

    [Fact]
    public void NestsCanBeSwitchedOff()
    {
        var world = Nest(t => t.NestsEnabled = false);
        Assert.Empty(world.Enemies);
        Assert.Empty(world.Clownfish);
    }

    [Fact]
    public void LanternfishCountIgnoresNinjas()
    {
        var world = Nest(t => t.EnemyCount = 2);
        Assert.Equal(2, world.Enemies.Count(e => e.Kind == EnemyKind.Lanternfish));
        Assert.Single(world.Enemies, e => e.Kind == EnemyKind.ClownNinja);
        world.Tuning.EnemyCount = 0;
        world.Step(Face(world));
        Assert.DoesNotContain(world.Enemies, e => e.Kind == EnemyKind.Lanternfish);
        Assert.Single(world.Enemies);
    }

    [Fact]
    public void TheNinjaSleepsUntilClementineComesClose()
    {
        var world = Nest();
        PlaceAt(world, 40f);
        TestWorlds.Run(world, Face(world), 30);
        Assert.Equal(EnemyState.Nested, Ninja(world).State);

        PlaceAt(world, 9f);
        world.Step(Face(world));
        Assert.Equal(EnemyState.Idle, Ninja(world).State);
        Assert.Contains(world.Events, e => e.Type == SimEventType.NinjaEmerged);
        Assert.True(Ninja(world).Alive);
    }

    [Fact]
    public void FarFromTheNestItGoesBackToSleep()
    {
        var world = Nest();
        PlaceAt(world, 9f);
        world.Step(Face(world));
        Assert.Equal(EnemyState.Idle, Ninja(world).State);

        // Out of attack range but inside the emerge range: it stays out. Past the return range: back in.
        PlaceAt(world, 40f);
        TestWorlds.Run(world, Face(world), 5);
        Assert.Equal(EnemyState.Idle, Ninja(world).State);
        PlaceAt(world, 47f);
        world.Step(Face(world));
        Assert.Equal(EnemyState.Nested, Ninja(world).State);
        Assert.Contains(world.Events, e => e.Type == SimEventType.NinjaHid);
    }

    [Fact]
    public void WhileDisguisedItSwimsInTheSchoolLikeTheOthers()
    {
        var world = Nest(t => t.NinjaAttackRange = 0f); // never attacks, only hides
        PlaceAt(world, 9f);
        TestWorlds.Run(world, Face(world), 60 * 8);
        var anemone = Home(world);
        var ninja = Ninja(world);
        Assert.Equal(EnemyState.Idle, ninja.State);
        float limit = anemone.Radius * 0.6f + 1.6f + 0.3f + anemone.Height + 1.4f + 1f;
        Assert.True(Vector3.Distance(ninja.Position, anemone.Position) < limit, $"ninja is {Vector3.Distance(ninja.Position, anemone.Position)} m away");
        foreach (var f in world.Clownfish)
            Assert.True(Vector3.Distance(f.Position, anemone.Position) < limit, "a neutral fish strayed from its anemone");
    }

    [Fact]
    public void ItAmbushesWithAFairTelegraphThenThrowsAStar()
    {
        var world = Nest();
        PlaceAt(world, 11f);
        long telegraphTick = -1, throwTick = -1;
        for (int i = 0; i < 60 * 8 && throwTick < 0; i++)
        {
            world.Step(Face(world));
            if (telegraphTick < 0 && world.Events.Any(e => e.Type == SimEventType.EnemyTelegraph && e.Tag == "ninja")) telegraphTick = world.Tick;
            if (world.Events.Any(e => e.Type == SimEventType.EnemyShotFired && e.Tag == "star")) throwTick = world.Tick;
        }
        Assert.True(telegraphTick > 0, "no telegraph");
        Assert.True(throwTick > telegraphTick, "no throw");
        Assert.True((throwTick - telegraphTick) * World.Dt >= 0.45f - World.Dt, "telegraph is shorter than the 0.45 s fairness minimum");
        var star = Assert.Single(world.Projectiles, p => p.Kind == ProjectileKind.Star);
        Assert.False(star.FromPlayer);
    }

    [Fact]
    public void ItDartsBackIntoTheSchoolAfterThrowing()
    {
        var world = Nest();
        PlaceAt(world, 11f);
        for (int i = 0; i < 60 * 8 && !world.Events.Any(e => e.Type == SimEventType.EnemyShotFired); i++) world.Step(Face(world));
        Assert.Equal(EnemyState.Recover, Ninja(world).State);
        TestWorlds.Run(world, Face(world), 60);
        Assert.Equal(EnemyState.Idle, Ninja(world).State);
    }

    [Fact]
    public void AStarHurtsAndADashDodgesIt()
    {
        var world = Nest();
        PlaceAt(world, 11f);
        world.Tuning.NinjaCooldown = 99f;
        // Throw one, then stand still: it lands.
        for (int i = 0; i < 60 * 10 && world.Player.Hp >= world.Tuning.MaxHp; i++) world.Step(Face(world));
        Assert.Equal(world.Tuning.MaxHp - world.Tuning.NinjaShotDamage, world.Player.Hp, 1);

        // Another world: dash through its flight.
        var dodge = Nest();
        PlaceAt(dodge, 11f);
        dodge.Tuning.NinjaCooldown = 99f;
        bool thrown = false;
        for (int i = 0; i < 60 * 10 && !thrown; i++)
        {
            dodge.Step(Face(dodge));
            thrown = dodge.Events.Any(e => e.Type == SimEventType.EnemyShotFired);
        }
        var input = Face(dodge);
        input.Dash = true;
        dodge.Step(input);
        TestWorlds.Run(dodge, Face(dodge), 40);
        Assert.Equal(dodge.Tuning.MaxHp, dodge.Player.Hp);
    }

    [Fact]
    public void NormalClownfishAreHarmlessAndBubblesPassThroughThem()
    {
        var world = Nest(t => t.NinjaAttackRange = 0f);
        var fish = world.Clownfish[0];
        // Aim a bubble straight through a neutral fish.
        world.Teleport(fish.Position - new Vector3(4f, 0f, 0f));
        world.Player.Yaw = MathUtil.Deg2Rad * -90f; // along +X
        var fire = new PlayerInput { Yaw = MathUtil.Deg2Rad * -90f, Fire = true, ViewHalfAngleDeg = 45f };
        world.Step(fire);
        var bubble = world.Projectiles.First(p => p.FromPlayer);
        bubble.Position = bubble.AxisPosition = fish.Position - new Vector3(0.1f, 0f, 0f);
        bool popped = false;
        for (int i = 0; i < 20; i++)
        {
            world.Step(new PlayerInput { Yaw = MathUtil.Deg2Rad * -90f, ViewHalfAngleDeg = 45f });
            popped |= world.Events.Any(e => e.Type == SimEventType.ShotHitEnemy);
        }
        Assert.False(popped);
        Assert.Equal(world.Tuning.MaxHp, world.Player.Hp);
        Assert.Equal(0, world.Kills);
    }

    [Fact]
    public void NeutralFishScatterFromClementine()
    {
        var world = Nest(t => t.NinjaAttackRange = 0f);
        var fish = world.Clownfish[0];
        world.Teleport(fish.Position + new Vector3(0.8f, 0f, 0f));
        var start = Vector3.Distance(fish.Position, world.Player.Position);
        TestWorlds.Run(world, Face(world), 30);
        Assert.True(Vector3.Distance(fish.Position, world.Player.Position) > start);
    }

    [Fact]
    public void BubblesHitTheNinjaAndItDropsLootWhenDefeated()
    {
        var world = Nest(t => t.NinjaHp = 3f);
        PlaceAt(world, 11f);
        world.Step(Face(world)); // wakes
        var ninja = Ninja(world);
        Assert.True(ninja.Alive);
        // Put it right in front of the barrel and throw.
        world.Teleport(ninja.Position + new Vector3(-4f, 0f, 0f));
        var fire = new PlayerInput { Yaw = MathUtil.Deg2Rad * -90f, Fire = true, ViewHalfAngleDeg = 45f };
        bool died = false;
        for (int i = 0; i < 90 && !died; i++)
        {
            ninja.Position = ninja.PrevPosition = world.Player.Position + new Vector3(4f, 0f, 0f);
            ninja.Velocity = Vector3.Zero;
            ninja.AttackCooldown = 99f;
            world.Step(fire);
            died = world.Events.Any(e => e.Type == SimEventType.CreatureFreed);
        }
        Assert.True(died);
        Assert.Equal(EnemyState.Dead, ninja.State);
        Assert.Equal(1, world.Kills);
    }

    [Fact]
    public void AFreedNinjaJoinsItsSchoolAsAnOrdinaryClownfish()
    {
        var world = Nest();
        int school = world.Clownfish.Count;
        PlaceAt(world, 9f);
        world.Step(Face(world));
        var ninja = Ninja(world);
        ninja.Hp = 0.01f;
        world.Teleport(ninja.Position + new Vector3(-4f, 0f, 0f));
        var fire = new PlayerInput { Yaw = MathUtil.Deg2Rad * -90f, Fire = true, ViewHalfAngleDeg = 45f };
        for (int i = 0; i < 90 && ninja.Alive; i++)
        {
            ninja.Position = ninja.PrevPosition = world.Player.Position + new Vector3(4f, 0f, 0f);
            ninja.AttackCooldown = 99f;
            world.Step(fire);
        }
        Assert.Equal(EnemyState.Dead, ninja.State);
        Assert.Equal(school + 1, world.Clownfish.Count);
        Assert.True(world.Clownfish.Last().Freed);

        // It stays freed: the nest sends no new ninja, however long she stays away.
        PlaceAt(world, 80f);
        TestWorlds.Run(world, Face(world), 60 * 90);
        Assert.Equal(EnemyState.Dead, ninja.State);
    }

    [Fact]
    public void AtMostThreeAttackersFromOutsideTheViewIncludesNinjas()
    {
        var world = Nest(t => t.MaxOffscreenAttackers = 0);
        PlaceAt(world, 11f);
        // Looking away from the nest: every attack would be off-screen, so none may start.
        var away = new PlayerInput { Yaw = MathUtil.Deg2Rad * -90f, ViewHalfAngleDeg = 45f };
        for (int i = 0; i < 60 * 6; i++) world.Step(away);
        Assert.NotEqual(EnemyState.Telegraph, Ninja(world).State);
        Assert.DoesNotContain(world.Projectiles, p => p.Kind == ProjectileKind.Star);
    }

    [Fact]
    public void StarsStickInRockWithoutScorching()
    {
        var world = Nest();
        world.Projectiles.Add(new Projectile
        {
            Id = 777,
            Kind = ProjectileKind.Star,
            Position = new Vector3(24f, 9f, 22f),
            Velocity = new Vector3(0f, -9f, 0f),
            Radius = 0.15f,
            Damage = 8f,
            MaxRange = 20f,
        });
        bool hit = false;
        for (int i = 0; i < 60 && !hit; i++)
        {
            world.Step(Face(world));
            hit = world.Events.Any(e => e.Type == SimEventType.ShotHitTerrain && e.Tag == "star");
        }
        Assert.True(hit);
    }

    [Fact]
    public void NestsAreDeterministic()
    {
        ulong Run()
        {
            var world = Nest();
            for (int i = 0; i < 60 * 12; i++)
            {
                var input = new PlayerInput { Yaw = MathUtil.Deg2Rad * (90f + 25f * MathF.Sin(i * 0.02f)), ViewHalfAngleDeg = 45f, Fire = i % 30 < 10 };
                if (i == 0) world.Teleport(new Vector3(35f, 12f, 17f));
                world.Step(input);
            }
            return world.StateHash();
        }

        Assert.Equal(Run(), Run());
    }
}

/// <summary>Anemone placement across a generated reef.</summary>
public class AnemoneGenerationTests
{
    [Fact]
    public void ReefsAreFullOfAnemonesWithAFewNests()
    {
        var layout = ReefTests.Reef("KELP 7Q2Z");
        var anemones = layout.Cave.Anemones;
        Assert.True(anemones.Count(a => !a.Nest) >= 40, $"{anemones.Count(a => !a.Nest)} decorations");
        var nests = anemones.Where(a => a.Nest).ToList();
        Assert.InRange(nests.Count, 4, 12);
        Assert.All(nests, n => Assert.InRange(n.SchoolSize, 3, 5));
        Assert.All(anemones.Where(a => !a.Nest), a => Assert.Equal(0, a.SchoolSize));
    }

    [Fact]
    public void AnemonesSitOnTheSeabedInOpenWater()
    {
        var layout = ReefTests.Reef("KELP 7Q2Z");
        var sdf = layout.Cave.Sdf;
        foreach (var a in layout.Cave.Anemones)
        {
            Assert.True(a.Up.Y >= 0.8f, "an anemone clings to a cliff");
            Assert.True(sdf.Sample(a.Position + a.Up * 0.4f) > 0.2f, $"{a.Position} is in rock");
            Assert.True(sdf.Sample(a.Position - a.Up * 0.6f) < 0.3f, $"{a.Position} floats above the seabed");
            Assert.True(sdf.Sample(a.Position + a.Up * (a.Radius * 0.6f)) > 0.1f);
        }
    }

    [Fact]
    public void NestsAreAwayFromTheStartAndApart()
    {
        var layout = ReefTests.Reef("KELP 7Q2Z");
        var nests = layout.Cave.Anemones.Where(a => a.Nest).ToList();
        var start = new Vector2(layout.StartPosition.X, layout.StartPosition.Z);
        Assert.All(nests, n => Assert.True(Vector2.Distance(new Vector2(n.Position.X, n.Position.Z), start) >= 50f));
        for (int i = 0; i < nests.Count; i++)
        for (int j = i + 1; j < nests.Count; j++)
            Assert.True(Vector3.Distance(nests[i].Position, nests[j].Position) > 6f);
    }

    [Fact]
    public void ARealReefSpawnsOneNinjaPerNest()
    {
        var layout = ReefTests.Reef("KELP 7Q2Z");
        var world = new World(layout.Cave.Clone(), new Tuning { EnemyCount = 0 }, 1);
        int nests = layout.Cave.Anemones.Count(a => a.Nest);
        Assert.Equal(nests, world.Enemies.Count(e => e.Kind == EnemyKind.ClownNinja));
        Assert.Equal(layout.Cave.Anemones.Where(a => a.Nest).Sum(a => a.SchoolSize), world.Clownfish.Count);
    }

    [Fact]
    public void AnemonesSurviveCloneAndDifferByReef()
    {
        var layout = ReefTests.Reef("KELP 7Q2Z");
        Assert.Equal(layout.Cave.Anemones.Count, layout.Cave.Clone().Anemones.Count);
        Assert.NotEqual(layout.Cave.Anemones[0].Position, ReefTests.Reef("KELP 7Q2Z", 1, 2).Cave.Anemones[0].Position);
    }
}
