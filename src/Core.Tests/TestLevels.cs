using System;
using System.Collections.Concurrent;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Run;

namespace OctoShoots.Core.Tests;

/// <summary>Levels for the sim tests, picked by what their plan holds (plans are cheap; levels are generated once).</summary>
static class TestLevels
{
    public static readonly SeedCode Seed = SeedCode.Parse("KELP7Q2Z");
    static readonly ConcurrentDictionary<LevelId, LevelMap> Maps = new();

    public static LevelMap Get(LevelId id) => Maps.GetOrAdd(id, i => TopDownGenerator.Generate(new RunStreams(Seed), i));

    /// <summary>The first level of the seed's first loop whose plan matches.</summary>
    public static LevelId Find(Func<LevelPlan, bool> match)
    {
        var id = LevelId.First;
        for (int i = 0; i < 60; i++, id = LevelPlan.NextOf(Seed, id))
            if (match(LevelPlan.For(Seed, id))) return id;
        throw new InvalidOperationException("no such level in the seed's first 60");
    }

    /// <summary>Every level of the seed's first 60 whose plan matches, in order.</summary>
    public static System.Collections.Generic.IEnumerable<LevelId> All(Func<LevelPlan, bool> match)
    {
        var id = LevelId.First;
        for (int i = 0; i < 60; i++, id = LevelPlan.NextOf(Seed, id))
            if (match(LevelPlan.For(Seed, id))) yield return id;
    }

    /// <summary>A level with a shop.</summary>
    public static LevelMap WithShop() => Get(Find(p => p.Shops > 0 && !p.HasBoss));

    /// <summary>A level with exactly one treasure room, and the level after it.</summary>
    public static LevelMap OneTreasure() => Get(Find(p => p.Treasures == 1 && !p.HasBoss));

    public static LevelMap After(LevelMap map) => Get(LevelPlan.NextOf(Seed, map.Id));
}
