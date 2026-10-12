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

    /// <summary>A default shot of her light (damage 10) striking a point.</summary>
    static PlaneShot Light(Vector2 at, float damage = 10f) =>
        new() { Position = at, Line = at, Velocity = Vector2.UnitY * 0.01f, Life = 1f, FromPlayer = true, Damage = damage };

    [Fact]
    public void ABlightrootTakesAboutThirteenShotsAndReCorruptsQuickly()
    {
        var w = Level(p => !p.HasBoss && p.Id.Level > 1);
        Quiet(w);
        var root = w.Blightroots[0];
        // A few shots: its meter drops by each one's light, shown as partly emptied.
        for (int i = 0; i < 4; i++)
        {
            w.Shots.Add(Light(root.Position));
            w.Step(default);
        }
        Assert.True(root.Alive);
        Assert.True(root.Corruption.Shown);
        Assert.Equal(root.Corruption.Max - 40f, root.Corruption.Value, 0);
        // Left alone, it re-corrupts in a few seconds, and the meter hides again.
        for (int i = 0; i < 4 * 60; i++) w.Step(default);
        Assert.False(root.Corruption.Shown);
        // Its meter's worth of default shots in a row (13 in the Shallows) cleanse it: it bursts, cleansing its reach.
        int shots = (int)MathF.Ceiling(root.Corruption.Max / 10f);
        for (int i = 0; i < shots && root.Alive; i++)
        {
            w.Shots.Add(Light(root.Position));
            w.Step(default);
        }
        Assert.False(root.Alive);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.BlightrootBurst);
        for (int i = 0; i < 60; i++) w.Step(default);
        Assert.True(w.Corruption!.CleansedOf(w.Areas[root.Area].Cells) > 0.9f);
    }

    [Fact]
    public void AMurklingTakesFourShotsInARowOrOneStrongOne()
    {
        var w = Level(p => !p.HasBoss && p.Id.Level > 1);
        Quiet(w);
        var at = w.Player.Position + new Vector2(0f, -3f);
        var m = new Murkling { Position = at, Bud = 0f };
        w.Murklings.Add(m);
        // Shots at her firing rate: three leave it standing, the fourth bursts it.
        int shots = (int)MathF.Ceiling(m.Corruption.Max / 10f);
        Assert.Equal(4, shots);
        for (int shot = 0; shot < shots; shot++)
        {
            m.Position = at;
            w.Shots.Add(Light(m.Position));
            w.Step(default);
            if (shot < shots - 1)
            {
                Assert.True(m.Alive);
                for (int i = 0; i < (int)(PlaneCombatTuning.ShotInterval * 60f); i++) w.Step(default);
            }
        }
        Assert.False(m.Alive);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.MurklingBurst);

        // One strong shot (a merged one) empties a full meter at once.
        var big = new Murkling { Position = at, Bud = 0f };
        w.Murklings.Add(big);
        w.Shots.Add(Light(at, BlightTuning.MurkCorruption + 1f));
        w.Step(default);
        Assert.False(big.Alive);
    }

    [Fact]
    public void ADashThrowsAClingingMurklingOffAndItComesBack()
    {
        var w = Level(p => !p.HasBoss && p.Id.Level > 1);
        Quiet(w);
        var m = new Murkling { Position = w.Player.Position + new Vector2(0.5f, 0f), Bud = 0f, Clinging = true, ClingOffset = new Vector2(0.5f, 0f) };
        w.Murklings.Add(m);
        // While it clings, it drinks: one number a second, the whole second's worth.
        float hp = w.Player.Hp;
        int reports = 0;
        for (int i = 0; i < 60; i++)
        {
            w.Step(default);
            reports += w.Events.Count(e => e.Type == PlaneEventType.PlayerDrained);
        }
        Assert.Equal(1, reports);
        Assert.True(w.Player.Hp < hp);
        // A dash throws it off with one shot's worth of light; it lives on.
        w.Step(new PlaneInput { Dash = true, Move = Vector2.UnitY });
        Assert.True(m.Alive);
        Assert.False(m.Clinging);
        Assert.Equal(m.Corruption.Max - BlightTuning.ShakeLight, m.Corruption.Value, 0);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.MobHit);
        // Once it recovers, it chases her again.
        for (int i = 0; i < (int)(BlightTuning.ShakeSeconds * 60f) + 2; i++) w.Step(default);
        Assert.True(m.Alive);
        Assert.True(m.Shaken <= 0f);
        float apart = Vector2.Distance(m.Position, w.Player.Position);
        for (int i = 0; i < 90 && !m.Clinging; i++) w.Step(default);
        Assert.True(m.Clinging || Vector2.Distance(m.Position, w.Player.Position) < apart);
    }

    [Fact]
    public void ABlightrootPulseCorruptsHerUnlessSheDashesThroughIt()
    {
        var w = Level(p => !p.HasBoss && p.Id.Level > 1);
        Quiet(w);
        var root = w.Blightroots[0];
        // She waits in its reach, where the ring will pass.
        var spot = Enumerable.Range(0, 64).Select(i => root.Position + new Vector2(MathF.Cos(i * 0.4f), MathF.Sin(i * 0.4f)) * 4f).First(q => w.Clear(q, 0.6f));
        w.Player.Position = w.Player.PrevPosition = spot;
        root.PulseTimer = 0.05f;
        float hp = w.Player.Hp;
        for (int i = 0; i < 90; i++)
        {
            w.Player.Position = w.Player.PrevPosition = spot;
            w.Step(default);
        }
        Assert.True(w.Player.Hp < hp);

        // Next ring: dashing as it arrives, she slips through untouched.
        hp = w.Player.Hp = w.Run.MaxHp;
        w.Player.HurtTimer = 0f;
        root.PulseTimer = 0.05f;
        for (int i = 0; i < 90; i++)
        {
            w.Player.Position = w.Player.PrevPosition = spot;
            if (root.PulseRadius >= 0f && root.PulseRadius > 4f - 2.5f && w.Player.DashInvulnerableTimer <= 0f && w.Player.DashCooldownTimer <= 0f)
                w.Step(new PlaneInput { Dash = true, Move = Vector2.Zero });
            else w.Step(default);
        }
        Assert.Equal(hp, w.Player.Hp);
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

    [Fact]
    public void AboutHalfTheLevelsHaveALightSpringThatWashesHerCorruptionAway()
    {
        var ids = TestLevels.All(_ => true).Take(16).ToList();
        int with = ids.Count(id => LightSprings.Of(TestLevels.Get(id)).Count > 0);
        Assert.InRange(with, 3, 13);
        var map = TestLevels.Get(ids.First(id => LightSprings.Of(TestLevels.Get(id)).Count > 0));
        var w = World(map);
        Quiet(w);
        var spring = Assert.Single(w.Springs);
        // Kept clean of the field, and far from the start.
        Assert.Equal(0f, w.Corruption!.At(spring.Position));
        Assert.True(System.Numerics.Vector2.Distance(spring.Position, map.Start.Position) >= SpringTuning.FromStart);
        // Corrupted to three quarters, she bathes: it drains steadily until she is wholly clean.
        w.Player.Hp = w.Run.MaxHp * 0.25f;
        w.Player.Position = w.Player.PrevPosition = spring.Position;
        w.Step(default);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.SpringEntered);
        float after1 = w.Player.Hp;
        Assert.True(after1 > w.Run.MaxHp * 0.25f && after1 < w.Run.MaxHp);
        bool cleansed = false;
        for (int i = 0; i < 4 * 60 && !cleansed; i++)
        {
            w.Step(default);
            cleansed = w.Events.Any(e => e.Type == PlaneEventType.SpringCleansed);
        }
        Assert.True(cleansed);
        Assert.Equal(w.Run.MaxHp, w.Player.Hp);
    }

    /// <summary>No budding or murklings, so a test sees only what it sets up.</summary>
    static void Quiet(PlaneWorld w)
    {
        foreach (var area in w.Areas) area.Spent = true;
        w.Murklings.Clear();
        foreach (var vine in w.Vines) vine.Points.RemoveRange(1, vine.Points.Count - 1);
    }
}
