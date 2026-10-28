using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using OctoShoots.Core.Run;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Her luck (LuckTuning): clam rooms, ambushes, bonus shells, light springs.</summary>
public class LuckTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static readonly LevelId[] Levels = TestLevels.All(p => !p.Rift).ToArray();

    [Fact]
    public void Without_luck_every_plan_is_the_same()
    {
        foreach (var id in Levels)
        {
            var a = LevelPlan.For(TestLevels.Seed, id);
            var b = LevelPlan.For(TestLevels.Seed, id, 0f);
            Assert.Equal((a.Treasures, a.Ambushes, a.Secrets, a.Shops, a.Caches, a.Curses), (b.Treasures, b.Ambushes, b.Secrets, b.Shops, b.Caches, b.Curses));
        }
    }

    [Fact]
    public void Luck_brings_more_clam_rooms_and_fewer_ambushes()
    {
        int Treasures(float luck) => Levels.Sum(id => LevelPlan.For(TestLevels.Seed, id, luck).Treasures);
        int Ambushes(float luck) => Levels.Sum(id => LevelPlan.For(TestLevels.Seed, id, luck).Ambushes);
        Assert.True(Treasures(4f) > Treasures(0f), $"{Treasures(4f)} clam rooms with luck against {Treasures(0f)}");
        Assert.True(Ambushes(4f) < Ambushes(0f), $"{Ambushes(4f)} ambushes with luck against {Ambushes(0f)}");
        // It never takes a clam room away, nor adds an ambush.
        foreach (var id in Levels)
        {
            var plain = LevelPlan.For(TestLevels.Seed, id);
            var lucky = LevelPlan.For(TestLevels.Seed, id, 4f);
            Assert.True(lucky.Treasures >= plain.Treasures && lucky.Ambushes <= plain.Ambushes);
        }
    }

    [Fact]
    public void Luck_sometimes_counts_a_shell_twice()
    {
        int Collected(bool lucky)
        {
            var run = new PlaneRun(Catalog.Value, new Tuning());
            if (lucky) run.Add("lucky_sea_glass");
            var map = TestLevels.Get(TestLevels.Find(p => p.Id.Depth == 2 && !p.HasBoss));
            var w = new PlaneWorld(map, new Tuning(), run, new PlaneOptions { Corruption = false });
            w.Shells.Clear();
            var her = w.Player.Position;
            for (int i = 0; i < 200; i++)
            {
                w.Shells.Add(new PlaneShell { Position = her });
                w.Step(default);
            }
            return run.Shells;
        }
        Assert.Equal(200, Collected(false));
        int lucky = Collected(true);
        // Two points of luck: about one in six counts twice.
        Assert.InRange(lucky, 215, 260);
    }

    [Fact]
    public void An_abyss_reef_has_a_light_spring_only_by_luck_and_keeps_its_seabed()
    {
        int with = 0;
        foreach (var id in Levels.Where(id => !LevelPlan.For(TestLevels.Seed, id).HasBoss).Take(12))
        {
            var plain = AbyssReef.Apply(TopDownGenerator.Generate(new RunStreams(TestLevels.Seed), id));
            Assert.Empty(LightSprings.Of(plain));
            var lucky = AbyssReef.Apply(TopDownGenerator.Generate(new RunStreams(TestLevels.Seed), id, luck: 6f));
            foreach (var at in LightSprings.Of(lucky))
            {
                with++;
                // Its seabed is kept: it wells up from the ground, not over the abyss.
                Assert.True(lucky.HeightAt(at) > -6f, $"a spring over the abyss at {at} ({lucky.HeightAt(at):0.0} m)");
            }
        }
        Assert.True(with > 0, "luck should bring some light springs");
    }

    [Fact]
    public void Luck_fills_a_light_spring_with_more_healing()
    {
        Assert.Equal(0f, LuckTuning.SpringPool(0f));
        Assert.Equal(0.3f, LuckTuning.SpringPool(2f), 4);
        Assert.Equal(LuckTuning.PoolMax, LuckTuning.SpringPool(50f));
    }
}
