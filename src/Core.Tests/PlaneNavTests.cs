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

/// <summary>The way to her: creatures after her go round the rock rather than pushing into it.</summary>
public class PlaneNavTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static PlaneWorld World(int which) =>
        new(TestLevels.Get(TestLevels.All(p => !p.HasBoss).Skip(which).First()), new Tuning(), new PlaneRun(Catalog.Value, new Tuning()));

    [Fact]
    public void With_open_water_between_the_way_is_straight_at_her()
    {
        var w = World(0);
        var her = w.Player.Position;
        for (int k = 0; k < 24; k++)
        {
            var from = her + new Vector2(MathF.Cos(k * 0.26f), MathF.Sin(k * 0.26f)) * 3f;
            if (!w.NavLine(from, her)) continue;
            var way = w.WayToHer(from);
            Assert.True(Vector2.Dot(way, Vector2.Normalize(her - from)) > 0.999f);
            return;
        }
        Assert.Fail("no open water round her start");
    }

    [Fact]
    public void A_creature_with_rock_between_it_and_her_swims_round_it_and_reaches_her()
    {
        int found = 0;
        for (int which = 0; which < 6 && found < 8; which++)
        {
            var w = World(which);
            var rng = new Rng(77UL + (ulong)which);
            for (int tries = 0; tries < 4000 && found < 8; tries++)
            {
                // Her and it a few metres apart as the fish swims, with rock between them.
                var her = new Vector2(rng.Range(10f, LevelMap.Size - 10f), rng.Range(10f, LevelMap.Size - 10f));
                var from = her + new Vector2(rng.Range(-9f, 9f), rng.Range(-9f, 9f));
                if (!w.Clear(her, 0.6f) || !w.Clear(from, 0.5f) || Vector2.Distance(her, from) < 5f || w.LineOfSight(from, her)) continue;
                w.Player.Position = w.Player.PrevPosition = her;
                // Where swimming straight at her only pushes it into the rock, along the way it gets there.
                if (Reaches(w, from, her, 12f, straight: true) || !w.WayExists(from)) continue;
                found++;
                Assert.True(Reaches(w, from, her, 25f), $"stuck going from {from} to {her}");
            }
        }
        Assert.True(found >= 8, $"only {found} cases of rock between them found");
    }

    /// <summary>A body swimming at 4 m/s along the way (or straight at her), sliding along the rock: whether it gets within 1.2 m of her in so many seconds.</summary>
    static bool Reaches(PlaneWorld w, Vector2 from, Vector2 her, float seconds, bool straight = false)
    {
        var p = from;
        var v = Vector2.Zero;
        for (int i = 0; i < seconds * PlaneWorld.TickRate; i++)
        {
            var dir = straight ? Vector2.Normalize(her - p) : w.WayToHer(p);
            v = dir * 4f;
            w.Move(ref p, ref v, 0.4f, report: false);
            if (Vector2.Distance(p, her) < 1.2f) return true;
        }
        return false;
    }
}
