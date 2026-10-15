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
    public void HalfTheFloorStartsCorruptedGatheredTowardTheDeepEnd()
    {
        var map = TestLevels.Get(TestLevels.Find(p => !p.HasBoss && p.Menace == 0f));
        var field = CorruptionField.ForMap(map);
        int open = 0, corrupt = 0, nearC = 0, nearN = 0, farC = 0, farN = 0;
        var focus = CorruptionField.Focus(map);
        for (int i = 0; i < field.Density.Length; i++)
        {
            if (field.Guard[i] < 1f) continue;
            open++;
            bool c = field.Density[i] > 0f;
            if (c) corrupt++;
            float d = Vector2.Distance(CellAt(i), focus);
            if (d < 40f) { nearN++; if (c) nearC++; }
            else if (d > 90f) { farN++; if (c) farC++; }
        }
        Assert.InRange(corrupt / (float)open, 0.42f, 0.6f);
        // Thicker toward the deep end, but scattered everywhere.
        Assert.True(nearC / (float)nearN > farC / (float)farN);
        Assert.True(farC > farN * 0.1f, $"far {farC}/{farN} near {nearC}/{nearN}");
    }

    [Fact]
    public void BlottersCreepTowardCleanFloorAndCorruptIt()
    {
        var w = Level(p => !p.HasBoss && p.Id.Level > 1 && p.Menace == 0f);
        Assert.Equal(2, w.Blotters.Count);
        var b = w.Blotters[0];
        Assert.Equal(BlotterTuning.Meter, b.Corruption.Max);
        float before = w.Corruption!.InitialTotal;
        var start = b.Position;
        // Her far away: it creeps on and smears ink as it goes.
        for (int i = 0; i < 20 * 60; i++) w.Step(default);
        Assert.True(Vector2.Distance(b.Position, start) > 3f);
        // Clean floor it crossed is corruption now (the level's total to cleanse grew).
        Assert.True(w.Corruption.InitialTotal > before + 5f);
        Assert.True(w.Corruption.At(b.Position) > 0.5f);
        // Its guards bud round it.
        Assert.Contains(w.Murklings, m => m.Guarding == b);
    }

    [Fact]
    public void ABlotterTakesTenShotsAndBurstsCleansingAround()
    {
        var w = Level(p => !p.HasBoss && p.Id.Level > 1 && p.Menace == 0f);
        var b = w.Blotters[0];
        w.Blotters.RemoveAt(1);
        Quiet(w);
        w.Blotters.Add(b);
        int shots = 0;
        while (b.Alive && shots < 40)
        {
            w.Shots.Add(Light(b.Position));
            w.Step(default);
            shots++;
        }
        Assert.False(b.Alive);
        Assert.InRange(shots, 10, 11);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.BlotterBurst);
        Assert.True(w.Corruption!.At(b.Position) < CorruptionTuning.Clean);
    }

    [Fact]
    public void LootLiesBuriedUnderTheInkUntilItsGroundIsCleansed()
    {
        var w = Level(p => !p.HasBoss && p.Id.Level > 1);
        Quiet(w);
        Assert.Empty(w.Vases);
        Assert.InRange(w.Caches.Count, 4, CacheTuning.Max);
        var cache = w.Caches[0];
        Assert.True(w.Corruption!.At(cache.Position) >= CacheTuning.Buried);
        int shells = w.Shells.Count + w.Run.Shells, orbs = w.Hearts.Count;
        for (int i = 0; i < 30; i++) w.Step(default);
        Assert.False(cache.Revealed);
        // She swims over it: her light cleanses the ground, and the loot floats up.
        for (int i = 0; i < 4 * 60 && !cache.Revealed; i++)
        {
            w.Player.Position = w.Player.PrevPosition = cache.Position;
            w.Step(default);
        }
        Assert.True(cache.Revealed);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.CacheRevealed);
        // Shells (some already gathered as she sits on them) or a light orb (she is not corrupted, so it waits).
        Assert.True(w.Shells.Count + w.Run.Shells > shells || w.Hearts.Count > orbs);
    }

    [Fact]
    public void TheSurgeBackFromAHitCleansesAThinLineOfGround()
    {
        var w = Level(p => !p.HasBoss && p.Id.Level > 1);
        Quiet(w);
        var field = w.Corruption!;
        // A murkling 6 m off over thick ink; she stands on clean ground.
        int cell = Enumerable.Range(0, field.Density.Length).First(i => field.Density[i] > 0.8f && w.Clear(CellAt(i), 1f)
            && w.Clear(CellAt(i) + new Vector2(6f, 0f), 0.6f) && w.LineOfSight(CellAt(i), CellAt(i) + new Vector2(6f, 0f)));
        var at = CellAt(cell);
        w.Player.Position = w.Player.PrevPosition = at + new Vector2(6f, 0f);
        var mid = at + new Vector2(3f, 0f);
        float before = field.At(mid);
        var m = new Murkling { Position = at, Bud = 0f };
        w.Murklings.Add(m);
        w.Shots.Add(Light(at));
        w.Step(default);
        Assert.True(field.At(mid) < before || before <= 0f);
    }

    [Fact]
    public void ABlotterTakesHitsAlongItsWholeBody()
    {
        var w = Level(p => !p.HasBoss && p.Id.Level > 1 && p.Menace == 0f);
        var b = w.Blotters[0];
        w.Blotters.RemoveAt(1);
        Quiet(w);
        w.Blotters.Add(b);
        // A shot at its tail (the last segment), well behind its head.
        var (tail, _) = b.Segment(BlotterTuning.Segments - 1);
        Assert.True(Vector2.Distance(tail, b.Position) > 3f);
        w.Shots.Add(Light(tail));
        w.Step(default);
        Assert.True(b.Corruption.Value < b.Corruption.Max);
    }

    [Fact]
    public void AnAttackedBlotterBombsHerAndTheSplashCorruptsAndThrowsHer()
    {
        var w = Level(p => !p.HasBoss && p.Id.Level > 1 && p.Menace == 0f);
        var b = w.Blotters[0];
        w.Blotters.RemoveAt(1);
        Quiet(w);
        w.Blotters.Add(b);
        // She stands 6 m off on clean ground and has just struck it; it spits a bomb at her.
        var spot = Enumerable.Range(0, 32).Select(i => b.Position + new Vector2(MathF.Cos(i * 0.2f), MathF.Sin(i * 0.2f)) * 6f).First(q => w.Clear(q, 0.6f));
        w.Player.Position = w.Player.PrevPosition = spot;
        w.Corruption!.Cleanse(spot, 3f, 1f);
        b.SinceStruck = 0f;
        b.BombTimer = 0.01f;
        b.Seeking = 99f;
        b.Target = b.Position;
        float hp = w.Player.Hp;
        w.Step(default);
        var bomb = Assert.Single(w.Bombs);
        Assert.Equal(spot, bomb.To);
        bool splashed = false;
        for (int i = 0; i < 90 && !splashed; i++)
        {
            w.Player.Position = w.Player.PrevPosition = spot;
            w.Player.Velocity = Vector2.Zero;
            w.Step(default);
            splashed = w.Events.Any(e => e.Type == PlaneEventType.BombSplash);
        }
        Assert.True(splashed);
        Assert.True(w.Player.Hp < hp);
        Assert.True(w.Player.Velocity.Length() > 1f);
        Assert.True(w.Corruption.At(spot + new Vector2(1.5f, 0f)) > 0.3f || w.Corruption.At(spot - new Vector2(1.5f, 0f)) > 0.3f);
    }

    [Fact]
    public void AStarburstShotBurstsIntoLightThatCleansesAndStrikesTheCorrupted()
    {
        var w = Level(p => !p.HasBoss && p.Id.Level > 1);
        Quiet(w);
        var field = w.Corruption!;
        int cell = Enumerable.Range(0, field.Density.Length).First(i => field.Density[i] > 0.8f && w.Clear(CellAt(i), 1.5f)
            && Vector2.Distance(CellAt(i), w.Player.Position) > 6f);
        var at = CellAt(cell);
        var near = new Murkling { Position = at + new Vector2(1.2f, 0f), Bud = 0f };
        w.Murklings.Add(near);
        // A burst where the shot goes out: no direct hit, but its light floods round it.
        w.Shots.Add(new PlaneShot { Position = at, Line = at, Velocity = Vector2.UnitX * 0.01f, Life = 1f, FromPlayer = true, Damage = 10f, Explosive = true, Range = 1f, Traveled = 2f });
        w.Step(default);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.LightBurst);
        Assert.True(near.Corruption.Value < near.Corruption.Max);
        Assert.True(field.At(at) < 0.3f);
    }

    [Fact]
    public void NonUniquePearlsStackAndStayOfferedUniqueOnesDoNot()
    {
        var run = new PlaneRun(Catalog.Value, new Tuning());
        run.Add("mirror_scale");
        run.Add("swordfish_bill");
        // Mirror Scale may come again, less likely; Swordfish Bill (unique) may not.
        Assert.True(run.CanOffer("mirror_scale"));
        Assert.False(run.CanOffer("swordfish_bill"));
        Assert.True(run.OfferWeight("mirror_scale") < run.OfferWeight("anglerfish_lure"));
        // A second Mirror Scale adds its stack: one more bounce, not two.
        run.Add("mirror_scale");
        Assert.Equal(3, run.Loadout.Shot.Bounces);
        Assert.Equal(2, run.Loadout.Count("mirror_scale"));
        // A whole extra Shark Tooth stacks its damage.
        float one = Loadout.Build(Catalog.Value, new[] { "shark_tooth" }, new Tuning()).Stats.Damage;
        float two = Loadout.Build(Catalog.Value, new[] { "shark_tooth", "shark_tooth" }, new Tuning()).Stats.Damage;
        Assert.Equal(one + 0.5f, two, 3);
    }

    [Fact]
    public void TheClamIsHurtOnlyThroughHerOpenPearlAndItsBurstWashesTheArenaClean()
    {
        var w = World(TestLevels.Get(new LevelId(1, 2, LevelPlan.BossLevel(TestLevels.Seed, 1, 2))));
        var boss = w.Boss!;
        // In she goes: the arena seals, and no creature but the clam is inside it from then on.
        w.Player.Position = w.Player.PrevPosition = w.ArenaCenter + new Vector2(0f, 7f);
        if (!w.Clear(w.Player.Position, 0.6f)) w.Player.Position = w.Player.PrevPosition = w.ArenaCenter + new Vector2(7f, 0f);
        bool open = false;
        for (int i = 0; i < 60 * 14 && !open; i++)
        {
            w.Step(default);
            Assert.DoesNotContain(w.Murklings, m => m.Alive && Vector2.Distance(m.Position, w.ArenaCenter) < w.ArenaRadius);
            Assert.DoesNotContain(w.Blotters, b => Vector2.Distance(b.Position, w.ArenaCenter) < w.ArenaRadius);
            open = boss.Open;
        }
        Assert.True(open);
        // Her opening faces the screen (south), whichever side Clementine came in from, and she never turns.
        var facing = boss.Facing;
        Assert.Equal(Vector2.UnitY, facing);
        // Open, a shot on her shell's rim does nothing; one on her black pearl hurts her.
        float hp = boss.Hp;
        var rim = boss.Position + facing * (PlaneBossTuning.BodyRadius - 0.3f);
        w.Shots.Add(new PlaneShot { Position = rim, Line = rim, Velocity = -facing * 0.01f, Life = 1f, FromPlayer = true, Damage = 10f });
        w.Step(default);
        Assert.Equal(hp, boss.Hp);
        boss.Hp = 1f;
        boss.Crust = 0;
        w.Shots.Add(new PlaneShot { Position = boss.Position, Line = boss.Position, Velocity = Vector2.UnitX * 0.01f, Life = 1f, FromPlayer = true, Damage = 10f });
        w.Step(default);
        Assert.True(boss.Freed);
        // The pearl's burst: the whole arena is clean.
        Assert.True(w.Corruption!.CleansedOf(w.ArenaArea!.Cells) > 0.99f);
        // She leaps aside and stays there.
        for (int i = 0; i < 90; i++) w.Step(default);
        Assert.Equal(1f, boss.Leap);
        Assert.Equal(boss.RestAt, boss.Position);
        Assert.True(Vector2.Distance(boss.Position, w.ArenaCenter) > PlaneBossTuning.ShuffleAside - 0.1f);
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
        var w = World(TestLevels.Get(new LevelId(1, 2, LevelPlan.BossLevel(code, 1, 2))));
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

    [Fact]
    public void ASpringHealsOnceALevel_ItIsSpentWhenSheLeavesAfterUsingIt()
    {
        var map = TestLevels.Get(TestLevels.All(_ => true).First(id => LightSprings.Of(TestLevels.Get(id)).Count > 0));
        var w = World(map);
        Quiet(w);
        var spring = Assert.Single(w.Springs);
        var outside = spring.Position + new Vector2(SpringTuning.Radius + 2f, 0f);
        void Go(Vector2 at, int steps = 1)
        {
            w.Player.Position = w.Player.PrevPosition = at;
            for (int i = 0; i < steps; i++) w.Step(default);
        }
        // Clean, she swims through: it is not used.
        w.Player.Hp = w.Run.MaxHp;
        Go(spring.Position, 10);
        Go(outside);
        Assert.False(spring.Used || spring.Spent);
        // Hurt, she bathes a moment and leaves: used, then spent.
        w.Player.Hp = w.Run.MaxHp * 0.3f;
        Go(spring.Position, 20);
        Assert.True(spring.Used && !spring.Spent);
        Go(outside);
        Assert.True(spring.Spent);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.SpringSpent);
        // Back in it, hurt: it heals no more.
        float hp = w.Player.Hp = w.Run.MaxHp * 0.3f;
        Go(spring.Position, 60);
        Assert.Equal(hp, w.Player.Hp, 2);
        Assert.False(spring.Bathing);
    }

    /// <summary>No budding or murklings, so a test sees only what it sets up.</summary>
    [Fact]
    public void HerLightFliesToWhereSheAimsAndClearsALittleWhereItGoesOut()
    {
        var w = Level(p => !p.HasBoss);
        Quiet(w);
        // The longest open line from her start; she aims 6 m along it.
        var from = w.Player.Position;
        var aim = Enumerable.Range(0, 32).Select(i => new Vector2(MathF.Cos(i * MathF.Tau / 32f), MathF.Sin(i * MathF.Tau / 32f)))
            .OrderByDescending(d => Enumerable.Range(0, 40).TakeWhile(k => w.Map.IsOpen(from + d * (k * 0.5f))).Count()).First();
        const float reach = 6f;
        var end = from + aim * (reach + 0.5f);
        var field = w.Corruption!;
        for (int i = 0; i < CorruptionField.N * CorruptionField.N; i++)
            if (Vector2.Distance(CellAt(i), end) < 2.5f) field.Density[i] = 1f;
        float before = field.At(end);
        w.Step(new PlaneInput { Aim = aim, AimReach = reach, Fire = true });
        var shot = w.Shots.Single(s => s.FromPlayer);
        bool gone = false;
        for (int i = 0; i < 200 && !gone; i++)
        {
            w.Step(new PlaneInput { Aim = aim, AimReach = reach });
            gone = !w.Shots.Contains(shot);
        }
        Assert.True(gone);
        Assert.InRange(shot.Traveled, reach - 0.1f, reach + 0.6f);
        Assert.True(field.At(shot.Position) < before - 0.3f, "its last light clears a little of the ground");
    }

    [Fact]
    public void AThrowFallsAtItsRangeAndStainsTheSand_ButStruckInTheAirItSplattersAndStainsNothing()
    {
        var w = Level(p => !p.HasBoss);
        Quiet(w);
        var field = w.Corruption!;
        // A throw along an open stretch of the level, its landing well clear of the safe places; she watches from
        // just behind it.
        var safe = CorruptionField.SafeOf(w.Map).ToList();
        bool Clear(Vector2 q) => safe.All(c => Vector2.Distance(q, c.Centre) > c.Radius + 8f);
        var (from, dir) = Enumerable.Range(0, CorruptionField.N * CorruptionField.N).Where(i => i % 7 == 0).Select(CellAt)
            .SelectMany(q => Enumerable.Range(0, 8).Select(k => (q, new Vector2(MathF.Cos(k * MathF.Tau / 8f), MathF.Sin(k * MathF.Tau / 8f)))))
            .First(t => Enumerable.Range(-4, 30).All(k => w.Map.IsOpen(t.Item1 + t.Item2 * (k * 0.5f))) && Clear(t.Item1 + t.Item2 * BlightTuning.ThrowFallAt));
        w.Player.Position = w.Player.PrevPosition = from - dir * 1.5f;
        w.MurkShots.Add(new MurkShot { Position = from, Velocity = dir * BlightTuning.ThrowSpeed });
        float total = field.Total;
        bool landed = false;
        for (int i = 0; i < 120 && !landed; i++)
        {
            w.Step(default);
            landed = w.Events.Any(e => e.Type == PlaneEventType.MurkShotLanded);
        }
        Assert.True(landed);
        var at = w.Events.First(e => e.Type == PlaneEventType.MurkShotLanded).Position;
        Assert.InRange(Vector2.Distance(at, from), BlightTuning.ThrowFallAt, BlightTuning.ThrowFallAt + 3f);
        Assert.True(field.Total > total, "it stains the sand");

        // Struck in the air by her light: a splatter, the ground untouched.
        var shot = new MurkShot { Position = w.Player.Position + dir * 4f, Velocity = -dir * BlightTuning.ThrowSpeed };
        w.MurkShots.Add(shot);
        float before = field.Total;
        w.Step(new PlaneInput { Aim = dir, Fire = true });
        bool burst = false;
        for (int i = 0; i < 30 && !burst; i++)
        {
            w.Step(new PlaneInput { Aim = dir, Fire = true });
            burst = w.Events.Any(e => e.Type == PlaneEventType.MurkShotBurst);
        }
        Assert.True(burst);
        Assert.DoesNotContain(w.Events, e => e.Type == PlaneEventType.MurkShotLanded);
        Assert.Equal(before, field.Total, 1);
    }

    static void Quiet(PlaneWorld w)
    {
        foreach (var area in w.Areas) area.Spent = true;
        w.Murklings.Clear();
        w.Blotters.Clear();
        w.Bombs.Clear();
        foreach (var vine in w.Vines) vine.Points.RemoveRange(1, vine.Points.Count - 1);
    }
}
