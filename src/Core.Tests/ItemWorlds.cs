using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Items;
using OctoShoots.Core.Sim;

namespace OctoShoots.Core.Tests;

/// <summary>Worlds with the real item catalog and creatures pinned in place for modifier tests.</summary>
static class ItemWorlds
{
    static readonly Lazy<ItemCatalog> SharedCatalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    public static ItemCatalog Catalog => SharedCatalog.Value;

    public static readonly Vector3 Centre = new(24f, 14f, 22f);

    /// <summary>Player in the chamber centre looking along +X; creatures stay put and never attack.</summary>
    public static World Create(int enemies, ItemCatalog? catalog = null, Action<Tuning>? tweak = null)
    {
        var tuning = new Tuning
        {
            EnemyCount = enemies,
            NestsEnabled = false,
            EnemySpeed = 0.001f,
            EnemyAttackRange = 0f,
            EnemyContactDamage = 0f,
            EnemyHp = 100f,
            AimAssist = AimAssistLevel.Off,
            EnemyRespawnDelay = 99f,
            // Bubbles regrow at once, so modifier tests can fire freely; BubbleTests cover the real limits.
            BubbleCapacity = 12,
            BubbleRegrowDelay = 0f,
            BubbleRegrowInterval = 0.05f,
        };
        tweak?.Invoke(tuning);
        var world = new World(TestWorlds.Cave.Clone(), tuning, TestWorlds.Seed, catalog ?? Catalog);
        TestWorlds.PlaceInChamber(world);
        for (int i = 0; i < world.Enemies.Count; i++) world.Enemies[i].Position = world.Enemies[i].PrevPosition = Centre + new Vector3(0f, -100f + i, 0f);
        world.Events.Clear();
        return world;
    }

    /// <summary>Puts creature i at an offset from the chamber centre.</summary>
    public static Enemy Place(World world, int index, Vector3 offset)
    {
        var e = world.Enemies[index];
        e.Position = e.PrevPosition = Centre + offset;
        e.Velocity = Vector3.Zero;
        return e;
    }

    public static PlayerInput LookPlusX(bool fire = false, float pitchDeg = 0f)
    {
        var input = TestWorlds.Look(-90f, pitchDeg);
        input.Fire = fire;
        return input;
    }

    /// <summary>Fires exactly one volley (one tick of held fire) and returns the new projectiles.</summary>
    public static Projectile[] FireOnce(World world, float pitchDeg = 0f)
    {
        var before = world.Projectiles.Select(p => p.Id).ToHashSet();
        world.Step(LookPlusX(fire: true, pitchDeg));
        return world.Projectiles.Where(p => !before.Contains(p.Id)).ToArray();
    }

    /// <summary>Steps without firing until the predicate holds or the tick budget runs out.</summary>
    public static bool RunUntil(World world, Func<World, bool> done, int maxTicks, float pitchDeg = 0f)
    {
        var input = LookPlusX(false, pitchDeg);
        for (int i = 0; i < maxTicks; i++)
        {
            world.Step(input);
            if (done(world)) return true;
        }
        return false;
    }
}
