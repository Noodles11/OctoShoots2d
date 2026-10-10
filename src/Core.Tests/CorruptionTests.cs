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

/// <summary>The corruption war (docs/CORRUPTION.md): the field, Blightroots, murklings, valves and the arena.</summary>
public class CorruptionTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static readonly PlaneOptions War = new() { Corruption = true, Pufferlings = false };

    static PlaneWorld World(LevelMap map) => new(map, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), War);

    static PlaneWorld Level(Func<LevelPlan, bool> match) => World(TestLevels.Get(TestLevels.Find(match)));

    static Vector2 CellAt(int i) => CorruptionField.CellCentre(i % CorruptionField.N, i / CorruptionField.N);

    [Fact]
    public void ThePufferlingsAreRetracted()
    {
        var w = Level(p => p.Ambushes > 0 && !p.HasBoss);
        Assert.Empty(w.Mobs);
        // An ambush buds murklings instead.
        var ambush = w.Ambushes[0];
        w.Player.Position = w.Player.PrevPosition = ambush.Center;
        w.Step(default);
        Assert.True(ambush.Sprung);
        Assert.Empty(w.Mobs);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.MurklingBud);
    }

    [Fact]
    public void SafePlacesStartClean()
    {
        var w = Level(p => p.Shops > 0 && p.Treasures > 0 && !p.HasBoss);
        var field = w.Corruption!;
        foreach (var poi in w.Map.Pois.Where(p => p.Kind is PoiKind.Start or PoiKind.Shop or PoiKind.TreasureCave))
            Assert.Equal(0f, field.At(poi.Position));
        Assert.True(field.InitialTotal > 1000f, "most of the floor is corrupted");
        Assert.Equal(0f, w.Cleansed, 3);
        // Blightroots never seed near them.
        foreach (var root in w.Blightroots)
        foreach (var poi in w.Map.Pois.Where(p => p.Kind is PoiKind.Start or PoiKind.Shop or PoiKind.TreasureCave))
            Assert.True(Vector2.Distance(root.Position, poi.Position) >= poi.Radius + BlightTuning.SafeDistance);
        Assert.NotEmpty(w.Blightroots);
    }

    [Fact]
    public void HerLightCleansesTheGroundAndTheGroundNeverHurtsHer()
    {
        var w = Level(p => !p.HasBoss && p.Id.Level > 1);
        Quiet(w);
        var field = w.Corruption!;
        // A thick patch far from every Blightroot.
        int cell = Enumerable.Range(0, field.Density.Length)
            .First(i => field.Density[i] > 0.6f && w.Blightroots.All(b => Vector2.Distance(b.Position, CellAt(i)) > BlightTuning.Reach + 4f) && w.Clear(CellAt(i), 0.6f));
        var at = CellAt(cell);
        w.Player.Position = w.Player.PrevPosition = at;
        float hp = w.Player.Hp;
        for (int i = 0; i < 60; i++) w.Step(default);
        Assert.True(field.At(at) < CorruptionTuning.Clean);
        Assert.Equal(hp, w.Player.Hp);
        Assert.True(w.Cleansed > 0f);
    }

    [Fact]
    public void ABlightrootDrinksBubblesAndStarvesWhenItsRingIsCleansed()
    {
        var w = Level(p => !p.HasBoss && p.Id.Level > 1);
        Quiet(w);
        var root = w.Blightroots[0];
        // A bubble into the sac is drunk: no pop, no harm to it.
        w.Shots.Add(new PlaneShot { Position = root.Position, Line = root.Position, Velocity = Vector2.UnitX, Life = 1f, FromPlayer = true, Damage = 999f });
        w.Step(default);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.BlightrootDrank);
        Assert.True(root.Alive);
        // Ring its reach with clean ground: it starves and bursts, cleansing its whole reach.
        var field = w.Corruption!;
        for (float a = 0f; a < MathF.Tau; a += 0.05f)
            field.Cleanse(root.Position + new Vector2(MathF.Cos(a), MathF.Sin(a)) * BlightTuning.Reach * 0.8f, 2.5f, 1f);
        for (int i = 0; i < 20 && root.Alive; i++) w.Step(default);
        Assert.False(root.Alive);
        for (int i = 0; i < 60; i++) w.Step(default);
        Assert.True(field.CleansedOf(w.Areas[root.Area].Cells) > 0.9f);
    }

    [Fact]
    public void OneBubbleBurstsAMurklingAndLosesHalfItsLight()
    {
        var w = Level(p => !p.HasBoss && p.Id.Level > 1);
        Quiet(w);
        var at = w.Player.Position + new Vector2(0f, -3f);
        w.Murklings.Add(new Murkling { Position = at, Bud = 0f });
        var shot = new PlaneShot { Position = at, Line = at, Velocity = Vector2.UnitY * 0.01f, Life = 1f, FromPlayer = true, Damage = 10f, Speed0 = 0f };
        w.Shots.Add(shot);
        w.Step(default);
        Assert.Empty(w.Murklings);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.MurklingBurst);
        Assert.Equal(0.5f, shot.Dim, 3);
    }

    [Fact]
    public void PassingAValveCostsHpAndClearsItForGood()
    {
        var w = Level(p => !p.HasBoss && p.Id.Level > 1 && (p.Caches > 0 || p.Secrets > 0 || p.Curses > 0));
        Quiet(w);
        Assert.NotEmpty(w.Valves);
        var v = w.Valves[0];
        float hp = w.Player.Hp;
        // Swim straight through it.
        w.Player.Position = w.Player.PrevPosition = v.Center - v.Normal * 2.5f;
        for (int i = 0; i < 120 && !v.Cleared; i++) w.Step(new PlaneInput { Move = v.Normal });
        Assert.True(v.Cleared);
        Assert.True(w.Player.Hp < hp);
    }

    [Fact]
    public void TheArenaIsCondensedAndItsCleansingStripsTheCrust()
    {
        var code = TestLevels.Seed;
        var w = World(TestLevels.Get(new LevelId(1, 1, LevelPlan.BossLevel(code, 1, 1))));
        Assert.NotNull(w.ArenaArea);
        Assert.Equal(1f, w.Corruption!.At(w.ArenaCenter));
        var boss = w.Boss!;
        Assert.Equal(CorruptionTuning.CrustLayers, boss.Crust);
        Assert.False(w.ExitOpen);
        // Cleanse a bit over 40% of the arena: two layers fall.
        var cells = w.ArenaArea!.Cells;
        foreach (int i in cells.Take((int)(cells.Count * 0.42f))) w.Corruption.Cleanse(CellAt(i), 0.1f, 1f);
        for (int i = 0; i < 16; i++) w.Step(default);
        Assert.InRange(boss.Crust, 2, 3);
    }

    /// <summary>No budding or murklings, so a test sees only what it sets up.</summary>
    static void Quiet(PlaneWorld w)
    {
        foreach (var area in w.Areas) area.Spent = true;
        w.Murklings.Clear();
        foreach (var vine in w.Vines) vine.Points.RemoveRange(1, vine.Points.Count - 1);
    }
}
