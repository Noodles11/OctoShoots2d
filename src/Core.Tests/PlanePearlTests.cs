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

/// <summary>Pearls in treasure rooms, their effect on her shots, and what she carries through the rift.</summary>
public class PlanePearlTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static readonly Lazy<LevelMap> Room1 = new(() => TopDownGenerator.Generate(new RunStreams(SeedCode.Parse("KELP7Q2Z")), 1, 1));
    static readonly Lazy<LevelMap> Room2 = new(() => TopDownGenerator.Generate(new RunStreams(SeedCode.Parse("KELP7Q2Z")), 1, 2));

    static PlaneRun NewRun() => new(Catalog.Value, new Tuning());

    [Fact]
    public void EveryPortedPearlIsInTheCatalogAndShapesShots()
    {
        foreach (string id in PlaneRun.ShotPearls)
        {
            Assert.True(Catalog.Value.Contains(id), id);
            Assert.NotNull(Catalog.Value[id].Shot);
        }
    }

    [Fact]
    public void TheTreasureRoomHoldsAPearlSheCanTake()
    {
        var w = new PlaneWorld(Room1.Value, new Tuning(), NewRun());
        var pearl = Assert.Single(w.Pearls);
        Assert.Contains(pearl.ItemId, PlaneRun.ShotPearls);
        var treasure = w.Map.Pois.Single(p => p.Kind == PoiKind.TreasureCave);
        Assert.Equal(treasure.Position, pearl.Position);

        w.Player.Position = w.Player.PrevPosition = pearl.Position;
        w.Step(default);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.PearlCollected);
        Assert.True(pearl.Taken);
        Assert.Contains(pearl.ItemId, w.Run.Items);
    }

    [Fact]
    public void TripleTentacleFiresThreeShotsAVolley()
    {
        var run = NewRun();
        run.Add("triple_tentacle");
        var w = new PlaneWorld(Room1.Value, new Tuning(), run);
        w.Step(new PlaneInput { Aim = Vector2.UnitX, Fire = true });
        var shots = w.Shots.Where(s => s.FromPlayer).ToList();
        Assert.Equal(3, shots.Count);
        Assert.All(shots, s => Assert.Equal(PlaneCombatTuning.ShotDamage * 0.8f, s.Damage, 3));
        // A fan: three different directions.
        Assert.Equal(3, shots.Select(s => MathF.Round(MathF.Atan2(s.Velocity.Y, s.Velocity.X), 3)).Distinct().Count());
    }

    [Fact]
    public void PiercingShotsGoThroughAMob()
    {
        var run = NewRun();
        run.Add("swordfish_bill");
        var w = new PlaneWorld(Room1.Value, new Tuning(), run);
        var mob = w.Mobs[0];
        var shot = new PlaneShot { Position = mob.Position, Line = mob.Position, Velocity = Vector2.UnitX * 16f, Life = 1f, FromPlayer = true, Damage = 5f, Pierce = true, Hit = new() };
        w.Shots.Add(shot);
        w.Step(default);
        Assert.True(mob.Hp < PlaneCombatTuning.MobHp);
        Assert.True(shot.Life > 0f || !w.Map.IsOpen(shot.Line), "a piercing shot flies on");
    }

    [Fact]
    public void ThroughTheRiftSheKeepsHerPearlsAndHp()
    {
        var run = NewRun();
        var w1 = new PlaneWorld(Room1.Value, new Tuning(), run);
        var pearl = w1.Pearls.Single();
        w1.Player.Position = w1.Player.PrevPosition = pearl.Position;
        w1.Step(default);
        w1.Player.Hp = 55f;
        w1.Step(default);

        var w2 = new PlaneWorld(Room2.Value, new Tuning(), run);
        Assert.Equal(55f, w2.Player.Hp);
        Assert.Contains(pearl.ItemId, w2.Run.Items);
        // The next treasure room never offers what she already has.
        Assert.All(w2.Pearls, p => Assert.NotEqual(pearl.ItemId, p.ItemId));
    }

    [Fact]
    public void LevelsHoldManyMobs()
    {
        var w = new PlaneWorld(Room1.Value, new Tuning(), NewRun());
        Assert.True(w.Mobs.Count >= 15, $"{w.Mobs.Count} mobs");
        var reach = LevelValidator.Distances(w.Map, w.Map.Start.Position);
        Assert.All(w.Mobs, m => Assert.False(float.IsPositiveInfinity(LevelValidator.DistanceAt(reach, m.Position))));
    }
}
