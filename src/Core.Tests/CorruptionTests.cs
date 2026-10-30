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

/// <summary>The corruption war (docs/CORRUPTION.md): the field, Blightroots, murklings and the arena.</summary>
public class CorruptionTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static readonly PlaneOptions War = new() { Corruption = true, Pufferlings = false, Boss = OctoShoots.Core.Bosses.BossGenerator.QueenClam(1UL, 1, 1) };

    static PlaneWorld World(LevelMap map) => new(map, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), War);

    static PlaneWorld Level(Func<LevelPlan, bool> match) => World(TestLevels.Get(TestLevels.Find(match)));

    static Vector2 CellAt(int i) => CorruptionField.CellCentre(i % CorruptionField.N, i / CorruptionField.N);

    [Fact]
    public void ThePufferlingsAreRetracted()
    {
        var w = Level(p => TestLevels.General(p) && p.Ambushes > 0 && !p.HasBoss);
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
        var w = Level(p => p.Template?.Id != "hoard" && p.Shops > 0 && p.Treasures > 0 && !p.HasBoss);
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
        var w = Level(p => TestLevels.General(p) && !p.HasBoss && p.Id.Level > 1);
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
    public void HerLightShovesWhatItHitsALittleTheSmallerTheFurther()
    {
        var w = Level(p => TestLevels.General(p) && !p.HasBoss && p.Id.Level > 1);
        Quiet(w);
        var m = new Murkling { Position = w.Player.Position + new Vector2(0f, -3f), Bud = 0f };
        w.Murklings.Add(m);
        w.Shots.Add(Light(m.Position));
        w.Step(default);
        // Shoved along the shot (its velocity points +Y), easing off within half a second.
        Assert.True(m.Alive);
        Assert.True(m.Knock.Y > 0.5f && MathF.Abs(m.Knock.X) < 0.01f, $"knock {m.Knock}");
        for (int i = 0; i < 30; i++) w.Step(default);
        Assert.True(m.Knock.Length() < 0.1f);
        // A bigger body is shoved less.
        Assert.True(w.KnockFor(BlotterTuning.ShellRadius) < w.KnockFor(m.Radius));
        Assert.True(w.KnockFor(0.05f) <= KnockTuning.Speed * KnockTuning.Most + 1e-4f);
    }

    [Fact]
    public void ABlightrootTakesAboutThirteenShotsAndReCorruptsQuickly()
    {
        var w = Level(p => TestLevels.General(p) && !p.HasBoss && p.Id.Level > 1);
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
        var w = Level(p => TestLevels.General(p) && !p.HasBoss && p.Id.Level > 1);
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
        var w = Level(p => TestLevels.General(p) && !p.HasBoss && p.Id.Level > 1);
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
    public void ABlightrootHammersHerWhenSheComesTooCloseUnlessSheDashesOrKeepsAway()
    {
        var w = Level(p => TestLevels.General(p) && !p.HasBoss && p.Id.Level > 1);
        Quiet(w);
        var root = w.Blightroots[0];
        // Only its hammer: no pulse (of any root) and no murkling budding meanwhile.
        foreach (var r in w.Blightroots) r.PulseTimer = 1000f;
        // Out of its reach: it never swings.
        var far = Enumerable.Range(0, 64).Select(i => root.Position + new Vector2(MathF.Cos(i * 0.4f), MathF.Sin(i * 0.4f)) * (BlightTuning.HammerReach + 1.5f)).First(q => w.Clear(q, 0.6f));
        for (int i = 0; i < 120; i++)
        {
            w.Player.Position = w.Player.PrevPosition = far;
            w.Murklings.Clear();
            w.Step(default);
        }
        Assert.True(root.Swing < 0f);
        Assert.Equal(w.Run.MaxHp, w.Player.Hp);
        // Too close: it rears back, then its head comes down on her and knocks her away.
        var near = Enumerable.Range(0, 64).Select(i => root.Position + new Vector2(MathF.Cos(i * 0.4f), MathF.Sin(i * 0.4f)) * 1.8f).First(q => w.Clear(q, 0.6f));
        w.Player.Position = w.Player.PrevPosition = near;
        w.Step(default);
        Assert.True(root.Swing >= 0f);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.BlightrootSwing);
        bool slammed = false, knocked = false;
        for (int i = 0; i < 60 && !slammed; i++)
        {
            w.Player.Position = w.Player.PrevPosition = near;
            w.Murklings.Clear();
            w.Step(default);
            slammed = w.Events.Any(e => e.Type == PlaneEventType.BlightrootSlam);
            knocked = System.Numerics.Vector2.Dot(w.Player.Velocity, root.SwingDir) > 3f;
        }
        Assert.True(slammed);
        Assert.True(w.Player.Hp < w.Run.MaxHp);
        Assert.True(knocked, "knocked away from it");
        // It waits before it swings again; then, dashing as its head comes down, she takes nothing.
        for (int i = 0; i < 200 && !(root.Swing >= 0f); i++)
        {
            w.Player.Position = w.Player.PrevPosition = near;
            w.Player.Velocity = System.Numerics.Vector2.Zero;
            w.Murklings.Clear();
            w.Step(default);
        }
        Assert.True(root.Swing >= 0f);
        float hp = w.Player.Hp = w.Run.MaxHp;
        w.Player.HurtTimer = 0f;
        bool dashed = false;
        for (int i = 0; i < 60; i++)
        {
            w.Player.Position = w.Player.PrevPosition = near;
            bool dash = !dashed && root.Swing > BlightTuning.HammerWindup - 0.1f;
            dashed |= dash;
            w.Murklings.Clear();
            w.Step(new PlaneInput { Dash = dash });
            if (w.Events.Any(e => e.Type == PlaneEventType.BlightrootSlam)) break;
        }
        Assert.Equal(hp, w.Player.Hp);
    }

    [Fact]
    public void ABlightrootNeverSwingsWhileItGathersAPulse()
    {
        var w = Level(p => TestLevels.General(p) && !p.HasBoss && p.Id.Level > 1);
        Quiet(w);
        var root = w.Blightroots[0];
        root.PulseTimer = BlightTuning.Telegraph * 0.5f;
        var near = Enumerable.Range(0, 64).Select(i => root.Position + new Vector2(MathF.Cos(i * 0.4f), MathF.Sin(i * 0.4f)) * 1.8f).First(q => w.Clear(q, 0.6f));
        w.Player.Position = w.Player.PrevPosition = near;
        w.Step(default);
        Assert.True(root.Inflate > 0f);
        Assert.True(root.Swing < 0f);
    }

    [Fact]
    public void ABlightrootPulseCorruptsHerUnlessSheDashesThroughIt()
    {
        var w = Level(p => TestLevels.General(p) && !p.HasBoss && p.Id.Level > 1);
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
            // Only the ring: no murkling the den buds meanwhile clings to her.
            w.Murklings.Clear();
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
            // (The cave lies in about the middle 60 m of the square, round its route.)
            if (d < 30f) { nearN++; if (c) nearC++; }
            else if (d > 55f) { farN++; if (c) farC++; }
        }
        Assert.InRange(corrupt / (float)open, 0.42f, 0.6f);
        // Thicker toward the deep end, but scattered everywhere.
        Assert.True(nearC / (float)nearN > farC / (float)farN);
        Assert.True(farC > farN * 0.1f, $"far {farC}/{farN} near {nearC}/{nearN}");
    }

    [Fact]
    public void BlottersCreepTowardCleanFloorAndCorruptIt()
    {
        var w = Level(p => TestLevels.General(p) && !p.HasBoss && p.Id.Level > 1 && p.Menace == 0f);
        Assert.Equal(2, w.Blotters.Count);
        var b = w.Blotters[0];
        Assert.Equal(BlotterTuning.Meter, b.Corruption.Max);
        float before = w.Corruption!.InitialTotal;
        var start = b.Position;
        // Clean floor in its sight, 9 m off, for it to creep toward.
        for (int k = 0; k < 16; k++)
        {
            var dir = new Vector2(MathF.Cos(k * MathF.Tau / 16f), MathF.Sin(k * MathF.Tau / 16f));
            var spot = start + dir * 9f;
            if (!w.Clear(spot, 1.5f) || !w.LineOfSight(start, spot)) continue;
            w.Corruption.Cleanse(spot, 3f, 2f);
            before = w.Corruption.InitialTotal;
            break;
        }
        // Her far away: it creeps on (it may turn back for new clean floor) and smears ink as it goes.
        float farthest = 0f;
        for (int i = 0; i < 20 * 60; i++)
        {
            w.Step(default);
            farthest = MathF.Max(farthest, Vector2.Distance(b.Position, start));
        }
        Assert.True(farthest > 3f);
        // Clean floor it crossed is corruption now (the level's total to cleanse grew).
        Assert.True(w.Corruption.InitialTotal > before + 5f);
        Assert.True(w.Corruption.At(b.Position) > 0.5f);
        // Its guards bud round it.
        Assert.Contains(w.Murklings, m => m.Guarding == b);
    }

    [Fact]
    public void ABlotterTakesTenShotsAndBurstsCleansingAround()
    {
        var w = Level(p => TestLevels.General(p) && !p.HasBoss && p.Id.Level > 1 && p.Menace == 0f);
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
    public void NoPotsWithTheCorruptionOn()
    {
        var w = Level(p => TestLevels.General(p) && !p.HasBoss && p.Id.Level > 1);
        Assert.Empty(w.Vases);
    }

    [Fact]
    public void ABlotterTakesHitsAnywhereOnItsShell()
    {
        var w = Level(p => TestLevels.General(p) && !p.HasBoss && p.Id.Level > 1 && p.Menace == 0f);
        var b = w.Blotters[0];
        w.Blotters.RemoveAt(1);
        Quiet(w);
        w.Blotters.Add(b);
        // A shot at the rim of its shell, beside it, far from the crab at its front.
        var rim = b.Position + new Vector2(-b.Heading.Y, b.Heading.X) * (BlotterTuning.ShellRadius - 0.1f);
        w.Shots.Add(Light(rim));
        w.Step(default);
        Assert.True(b.Corruption.Value < b.Corruption.Max);
    }

    [Fact]
    public void AnAttackedBlotterBombsHerAndTheSplashCorruptsAndThrowsHer()
    {
        var w = Level(p => TestLevels.General(p) && !p.HasBoss && p.Id.Level > 1 && p.Menace == 0f);
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
        var w = Level(p => TestLevels.General(p) && !p.HasBoss && p.Id.Level > 1);
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
    public void ASpringHealsFromItsStoreAcrossVisits_ItIsSpentWhenTheStoreRunsDry()
    {
        var map = TestLevels.Get(TestLevels.All(_ => true).First(id => LightSprings.Of(TestLevels.Get(id)).Count > 0));
        var w = World(map);
        Quiet(w);
        var spring = Assert.Single(w.Springs);
        Assert.Equal(SpringTuning.Pool, spring.PoolMax);
        var outside = spring.Position + new Vector2(SpringTuning.Radius + 2f, 0f);
        bool spentRaised = false;
        void Go(Vector2 at, int steps = 1)
        {
            w.Player.Position = w.Player.PrevPosition = at;
            for (int i = 0; i < steps; i++)
            {
                w.Step(default);
                spentRaised |= w.Events.Any(e => e.Type == PlaneEventType.SpringSpent);
            }
        }
        // Whole, she swims through: it gives nothing.
        w.Player.Hp = w.Run.MaxHp;
        Go(spring.Position, 10);
        Go(outside);
        Assert.Equal(spring.PoolMax, spring.Pool);
        // Hurt, she bathes a moment and leaves: what it gave her is gone from its store, and it still shines.
        float hp = w.Player.Hp = w.Run.MaxHp * 0.3f;
        Go(spring.Position, 20);
        Go(outside);
        float given = w.Player.Hp - hp;
        Assert.True(given > 0f);
        Assert.Equal(spring.PoolMax - given, spring.Pool, 2);
        Assert.False(spring.Spent);
        // She comes back as often as she likes, until the store runs dry: then it is spent.
        for (int visit = 0; visit < 10 && !spring.Spent; visit++)
        {
            w.Player.Hp = w.Run.MaxHp * 0.1f;
            Go(spring.Position, 4 * 60);
            Go(outside);
        }
        Assert.True(spring.Spent);
        Assert.Equal(0f, spring.Pool);
        Assert.True(spentRaised);
        // Back in it, hurt: it heals no more.
        hp = w.Player.Hp = w.Run.MaxHp * 0.3f;
        Go(spring.Position, 60);
        Assert.Equal(hp, w.Player.Hp, 2);
        Assert.False(spring.Bathing);
    }

    [Fact]
    public void NoCreatureOfInkMayEnterAShiningSpring_OnceSpentItMay()
    {
        var map = TestLevels.Get(TestLevels.All(_ => true).First(id => LightSprings.Of(TestLevels.Get(id)).Count > 0));
        var w = World(map);
        Quiet(w);
        var spring = Assert.Single(w.Springs);
        w.Player.Position = w.Player.PrevPosition = spring.Position;
        // One swims at her from outside, another is clinging to her as she arrives.
        var swimmer = new Murkling { Position = spring.Position + new Vector2(SpringTuning.Radius + 1f, 0f), Bud = 0f };
        var clinger = new Murkling { Position = spring.Position + new Vector2(0.3f, 0f), Bud = 0f, Clinging = true, ClingOffset = new Vector2(0.3f, 0f) };
        w.Murklings.Add(swimmer);
        w.Murklings.Add(clinger);
        for (int i = 0; i < 120; i++)
        {
            w.Step(default);
            foreach (var m in w.Murklings.Where(m => m.Alive))
                Assert.True(Vector2.Distance(m.Position, spring.Position) >= SpringTuning.Radius + m.Body - 1e-3f, $"a murkling in the light at {m.Position}");
        }
        Assert.False(clinger.Clinging);
        // Spent, it wards nothing.
        spring.Spent = true;
        var after = new Murkling { Position = spring.Position + new Vector2(1f, 0f), Bud = 0f };
        w.Murklings.Add(after);
        w.Step(default);
        Assert.True(Vector2.Distance(after.Position, spring.Position) < SpringTuning.Radius);
    }

    /// <summary>No budding or murklings, so a test sees only what it sets up.</summary>
    [Fact]
    public void HerLightFliesHerFullRangeAndClearsALittleWhereItGoesOut()
    {
        var w = Level(p => TestLevels.General(p) && !p.HasBoss);
        Quiet(w);
        // The longest open line from her start; she aims along it.
        var from = w.Player.Position;
        var aim = Enumerable.Range(0, 32).Select(i => new Vector2(MathF.Cos(i * MathF.Tau / 32f), MathF.Sin(i * MathF.Tau / 32f)))
            .OrderByDescending(d => Enumerable.Range(0, 40).TakeWhile(k => w.Map.IsOpen(from + d * (k * 0.5f))).Count()).First();
        Assert.True(Enumerable.Range(0, 30).All(k => w.Map.IsOpen(from + aim * (k * 0.5f))), "an open line her range long");
        const float reach = PlaneCombatTuning.BubbleRange;
        var end = from + aim * (reach + 0.5f);
        var field = w.Corruption!;
        for (int i = 0; i < CorruptionField.N * CorruptionField.N; i++)
            if (Vector2.Distance(CellAt(i), end) < 2.5f) field.Density[i] = 1f;
        float before = field.At(end);
        w.Step(new PlaneInput { Aim = aim, Fire = true });
        var shot = w.Shots.Single(s => s.FromPlayer);
        bool gone = false;
        for (int i = 0; i < 200 && !gone; i++)
        {
            w.Step(new PlaneInput { Aim = aim });
            gone = !w.Shots.Contains(shot);
        }
        Assert.True(gone);
        Assert.InRange(shot.Traveled, reach - 0.1f, reach + 0.6f);
        Assert.True(field.At(shot.Position) < before - 0.3f, "its last light clears a little of the ground");
    }

    [Fact]
    public void HerLightMeetsASmallCreatureItWouldPassBetweenTwoSteps()
    {
        var w = Level(p => TestLevels.General(p) && !p.HasBoss);
        Quiet(w);
        var from = w.Player.Position;
        var aim = Enumerable.Range(0, 32).Select(i => new Vector2(MathF.Cos(i * MathF.Tau / 32f), MathF.Sin(i * MathF.Tau / 32f)))
            .OrderByDescending(d => Enumerable.Range(0, 40).TakeWhile(k => w.Map.IsOpen(from + d * (k * 0.5f))).Count()).First();
        // She throws it 1.5 m a step: a small murkling half a step past one of them is never where the light is at a
        // step's end.
        float step = PlaneCombatTuning.ShotSpeed * PlaneWorld.Dt;
        Assert.True(step >= 1.4f);
        var m = BudAt(w, from + aim * (0.25f + step * 2.5f), prime: false, drink: false)!;
        m.Bud = 0f;
        m.Radius = 0.08f;
        m.Corruption.Max = m.Corruption.Value = 1000f;
        w.Step(new PlaneInput { Aim = aim, Fire = true });
        for (int i = 0; i < 20; i++) w.Step(new PlaneInput { Aim = aim });
        Assert.True(m.Corruption.Value < 1000f, "her light struck it");
    }

    [Fact]
    public void AThrowFallsAtItsRangeAndStainsTheSand_ButStruckInTheAirItSplattersAndStainsNothing()
    {
        var w = Level(p => TestLevels.General(p) && !p.HasBoss);
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
        // Clean sand where it will land, so its stain shows.
        field.Cleanse(from + dir * BlightTuning.ThrowFallAt, 3f, 1f);
        w.MurkShots.Add(new MurkShot { Position = from, Velocity = dir * BlightTuning.ThrowSpeed });
        bool landed = false;
        for (int i = 0; i < 120 && !landed; i++)
        {
            w.Step(default);
            landed = w.Events.Any(e => e.Type == PlaneEventType.MurkShotLanded);
        }
        Assert.True(landed);
        var at = w.Events.First(e => e.Type == PlaneEventType.MurkShotLanded).Position;
        Assert.InRange(Vector2.Distance(at, from), BlightTuning.ThrowFallAt, BlightTuning.ThrowFallAt + 3f);
        // The sand where it fell (cleaned before) is stained now (her own glow, 9 m off, does not reach it).
        Assert.True(field.At(at) > 0.2f, "it stains the sand");

        // Struck in the air by her light: a splatter, the ground untouched.
        var shot = new MurkShot { Position = w.Player.Position + dir * 4f, Velocity = -dir * BlightTuning.ThrowSpeed };
        w.MurkShots.Add(shot);
        // The ground along its flight, measured before and after (the rest of the level lives on meanwhile).
        float Along() => Enumerable.Range(0, CorruptionField.N * CorruptionField.N)
            .Where(i => Geo.SegmentDistance(CellAt(i), w.Player.Position, w.Player.Position + dir * 6f) < 3f).Sum(i => field.Density[i]);
        float before = Along();
        w.Step(new PlaneInput { Aim = dir, Fire = true });
        bool burst = false;
        for (int i = 0; i < 30 && !burst; i++)
        {
            w.Step(new PlaneInput { Aim = dir, Fire = true });
            burst = w.Events.Any(e => e.Type == PlaneEventType.MurkShotBurst);
        }
        Assert.True(burst);
        Assert.DoesNotContain(w.Events, e => e.Type == PlaneEventType.MurkShotLanded);
        // Nothing stained there (a landed throw's stain adds several; the level's slow regrowth a few hundredths).
        Assert.True(Along() <= before + 0.5f, $"stained {Along() - before:0.00}");
    }

    [Fact]
    public void AMurklingBendsBackBeforeItThrowsAndLocksItsAimForTheSnap()
    {
        var w = Level(p => TestLevels.General(p) && !p.HasBoss && p.Id.Level > 1);
        Quiet(w);
        var her = w.Player.Position;
        var m = new Murkling { Position = her + new Vector2(0f, -5f), Bud = 0f, ThrowTimer = 0f };
        w.Murklings.Add(m);
        int windup = -1, thrown = -1;
        for (int i = 0; i < 120 && thrown < 0; i++)
        {
            w.Player.Position = w.Player.PrevPosition = her;
            w.Player.Hp = 100f;
            w.Step(default);
            if (windup < 0 && w.Events.Any(e => e.Type == PlaneEventType.MurklingWindup)) windup = i;
            if (w.Events.Any(e => e.Type == PlaneEventType.MurklingThrow)) thrown = i;
            // No throw leaves it while it bends back.
            if (thrown < 0) Assert.Empty(w.MurkShots);
        }
        Assert.True(windup >= 0 && thrown > windup);
        // The telegraph lasts its wind-up (Menace 0 here).
        Assert.InRange((thrown - windup) / 60f, BlightTuning.WindupSeconds - 0.03f, BlightTuning.WindupSeconds + 0.03f);
        var shot = Assert.Single(w.MurkShots);
        Assert.True(Vector2.Dot(Vector2.Normalize(shot.Velocity), Vector2.Normalize(her - m.Position)) > 0.95f);
        Assert.True(m.ThrowAge < 0.05f);
    }

    static Murkling? BudAt(PlaneWorld w, Vector2 at, bool prime, bool drink = true) =>
        (Murkling?)typeof(PlaneWorld).GetMethod("Bud", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(w, new object[] { at, -1, prime, drink, false });

    /// <summary>The densest open cell of the level at least this far from her.</summary>
    /// <summary>A dense spot with a prime's ink within 7 m.</summary>
    static Vector2 PrimeSpot(PlaneWorld w, float away)
    {
        var field = w.Corruption!;
        for (int i = 0; i < field.Density.Length; i++)
        {
            var c = CellAt(i);
            if (field.Density[i] < 0.95f || Vector2.Distance(c, w.Player.Position) < away || !w.Clear(c, 2f)) continue;
            if (field.Plan(c, BlightTuning.PrimeInk) is { Radius: < 7f }) return c;
        }
        throw new InvalidOperationException("no spot for a prime");
    }

    static Vector2 DenseSpot(PlaneWorld w, float away)
    {
        var field = w.Corruption!;
        int best = -1;
        for (int i = 0; i < field.Density.Length; i++)
        {
            var c = CellAt(i);
            if (field.Density[i] < 0.95f || Vector2.Distance(c, w.Player.Position) < away || !w.Clear(c, 2f)) continue;
            best = i;
            break;
        }
        Assert.True(best >= 0);
        return CellAt(best);
    }

    [Fact]
    public void ABlightrootUnderAttackWakesItsGuardians()
    {
        var w = Level(p => TestLevels.General(p) && !p.HasBoss && p.Id.Level > 1);
        Quiet(w);
        var root = w.Blightroots[0];
        // She stands well off; her light strikes it (and keeps striking it).
        w.Player.Position = w.Player.PrevPosition = root.Position + new Vector2(0f, 14f);
        root.Corruption.Hit(1f);
        w.Step(default);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.GuardiansWoke);
        Assert.True(root.Guarded);
        float interval = BlightTuning.GuardInterval / (1f + BlightTuning.GuardPerMenace * w.Menace);
        for (int i = 0; i < (int)((interval + 1f) * 60f); i++)
        {
            if (i % 60 == 0) root.Corruption.Hit(1f);
            w.Step(default);
        }
        // A guardian budded in its ring, even though its area had been spent.
        Assert.Contains(w.Murklings, m => m.Alive && m.Area == root.Area
            && Vector2.Distance(m.Position, root.Position) is >= BlightTuning.GuardNear - 0.01f and <= BlightTuning.GuardFar + 0.01f);
        // Left alone for the window, it calms down.
        for (int i = 0; i < (int)((BlightTuning.GuardWindow + 0.2f) * 60f); i++) w.Step(default);
        Assert.False(root.Guarded);
    }

    [Fact]
    public void ABudDrinksAFixedAmountOfInkAndHoldsItUntilFreed()
    {
        var w = Level(p => TestLevels.General(p) && !p.HasBoss && p.Id.Level > 1);
        Quiet(w);
        // No regrowth meanwhile: the ink moves only between the ground and the murkling.
        foreach (var root in w.Blightroots) root.Alive = false;
        var field = w.Corruption!;
        var at = DenseSpot(w, 20f);
        float before = field.Total, cleansed = field.Cleansed, cost = BlightTuning.Ink / CorruptionTuning.CellArea;
        var m = BudAt(w, at, prime: false)!;
        var bud = w.Events.Single(e => e.Type == PlaneEventType.MurklingBud);
        Assert.InRange(bud.Direction.X, 0.5f, 3.5f);
        Assert.True(bud.Direction.Y > 0.5f, "the ink it will gather");
        for (int i = 0; i < (int)(BlightTuning.BudSeconds * 60f) + 2; i++) w.Step(default);
        Assert.False(m.Budding);
        Assert.True(field.At(at) < 0.05f, "the ground under it is drunk clean");
        // Exactly its cost left the ground, and it holds it: the level is no cleaner for its birth.
        Assert.InRange(field.Total, before - cost - 0.05f, before - cost + 0.05f);
        Assert.Equal(cost, m.Matter, 2);
        Assert.Equal(cost, field.Held, 2);
        Assert.Equal(cleansed, field.Cleansed, 4);
        // Freed: the ink it was made of is cleansed.
        w.Shots.Add(Light(m.Position, m.Corruption.Max + 1f));
        w.Step(default);
        Assert.False(m.Alive);
        Assert.Equal(0f, field.Held, 3);
        Assert.True(field.Cleansed >= cleansed + cost / field.InitialTotal - 1e-4f);
    }

    [Fact]
    public void ABudReachesAsFarAsItMustNothingBudsOnADrainedLevelAndAnAmbushBudsOfItsOwn()
    {
        var w = Level(p => TestLevels.General(p) && !p.HasBoss && p.Id.Level > 1);
        Quiet(w);
        // From the clean start it reaches out to the nearest ink, however far.
        var far = w.Corruption!.Plan(w.Player.Position, BlightTuning.Ink);
        Assert.NotNull(far);
        Assert.True(far!.Radius > 3f, "the start is clean: its ink comes from further off");
        // With the level drained (and nothing to regrow it), nothing buds.
        foreach (var root in w.Blightroots) root.Alive = false;
        System.Array.Clear(w.Corruption.Density);
        Assert.Null(BudAt(w, w.Player.Position, prime: false));
        Assert.Null(BudAt(w, w.Player.Position, prime: true));
        // An ambush's bud is the trap's own ink: it drinks nothing and holds nothing.
        float total = w.Corruption.Total;
        var m = BudAt(w, w.Player.Position + new Vector2(0f, 4f), prime: false, drink: false)!;
        for (int i = 0; i < (int)(BlightTuning.BudSeconds * 60f) + 2; i++) w.Step(default);
        Assert.Equal(0f, m.Matter);
        Assert.Equal(0f, w.Corruption.Held);
        Assert.True(w.Corruption.Total <= total + 1e-3f);
    }

    [Fact]
    public void ALightBurstFreeingAPrimeLetsItBreakApartWithoutUpsettingTheStep()
    {
        var w = Level(p => TestLevels.General(p) && !p.HasBoss && p.Id.Level > 1);
        Quiet(w);
        var m = BudAt(w, PrimeSpot(w, 12f), prime: true)!;
        for (int i = 0; i < (int)(BlightTuning.PrimeBud * 60f) + 2; i++) w.Step(default);
        // A Starburst bubble popping on it with more than its whole meter of light.
        var shot = Light(m.Position, m.Corruption.Max * 4f);
        shot.Explosive = true;
        w.Shots.Add(shot);
        w.Step(default);
        Assert.False(m.Alive);
        Assert.Equal(BlightTuning.PrimeSplit, w.Murklings.Count(x => x.Alive && !x.Prime && !x.Budding));
    }

    [Fact]
    public void AMurklingPrimeIsFiveTimesTheSizeTwiceAsToughLeavesShellsAndBreaksIntoMurklings()
    {
        var w = Level(p => TestLevels.General(p) && !p.HasBoss && p.Id.Level > 1);
        Quiet(w);
        var at = PrimeSpot(w, 12f);
        var m = BudAt(w, at, prime: true)!;
        Assert.True(m.Prime);
        Assert.Equal(BlightTuning.MurkRadius * 5f, m.Radius, 3);
        Assert.True(m.Body < m.Radius, "it squeezes through the passes");
        Assert.Equal(BlightTuning.MurkCorruption * 10f, m.Corruption.Max, 2);
        Assert.Equal(BlightTuning.Sense * 1.5f, m.Sense, 3);
        // It drinks twelve murklings' ink, from much farther round it.
        Assert.InRange(w.Events.Single(e => e.Type == PlaneEventType.MurklingBud).Direction.X, 2.5f, 7f);
        for (int i = 0; i < (int)(BlightTuning.PrimeBud * 60f) + 2; i++) w.Step(default);
        Assert.False(m.Budding);
        Assert.Equal(BlightTuning.PrimeInk / CorruptionTuning.CellArea, m.Matter, 1);

        // Its throw: a bigger drop, hitting harder, after a longer bend.
        var her = w.Player.Position;
        // 11 m off her, wherever there is open water in her sight.
        m.Position = Enumerable.Range(0, 16).Select(k => her + new Vector2(MathF.Cos(k * MathF.Tau / 16f), MathF.Sin(k * MathF.Tau / 16f)) * 11f)
            .First(q => w.Clear(q, m.Body) && w.LineOfSight(q, her));
        m.ThrowTimer = 0f;
        MurkShot? shot = null;
        for (int i = 0; i < 120 && shot is null; i++)
        {
            w.Player.Position = w.Player.PrevPosition = her;
            w.Player.Hp = 100f;
            w.Step(default);
            shot = w.MurkShots.FirstOrDefault();
        }
        Assert.NotNull(shot);
        Assert.Equal(BlightTuning.ThrowDamage * 2.5f, shot!.Damage, 2);
        Assert.Equal(BlightTuning.ThrowRadius * 2.5f, shot.Radius, 3);

        // Freed: it always leaves shells.
        int shells = w.Shells.Count;
        w.Shots.Add(Light(m.Position, m.Corruption.Max + 1f));
        w.Step(default);
        Assert.False(m.Alive);
        Assert.InRange(w.Shells.Count - shells, BlightTuning.PrimeShellsMin, BlightTuning.PrimeShellsMax + 3);
        // And it breaks apart into five murklings, already out, each made of a murkling's ink of its own; the rest is cleansed.
        var pieces = w.Murklings.Where(x => x.Alive && !x.Prime && !x.Budding).ToList();
        Assert.Equal(BlightTuning.PrimeSplit, pieces.Count);
        Assert.All(pieces, x => Assert.Equal(BlightTuning.Ink / CorruptionTuning.CellArea, x.Matter, 2));
        Assert.Equal(BlightTuning.PrimeSplit * BlightTuning.Ink / CorruptionTuning.CellArea, w.Corruption!.Held, 1);
    }

    [Fact]
    public void ThumpsquirtsLiveFromDepthTwoOn()
    {
        Assert.Empty(Level(p => p.Id.Depth == 2 && !p.HasBoss).Thumpsquirts);
        var deeper = TestLevels.All(p => TestLevels.General(p) && p.Id.Depth >= ThumpTuning.FirstDepth && !p.HasBoss).FirstOrDefault();
        if (deeper == default) return;
        Assert.InRange(World(TestLevels.Get(deeper)).Thumpsquirts.Count, 1, ThumpTuning.Max);
    }

    [Fact]
    public void AThumpsquirtLeapsAtHerUnharmedInTheAirLandsWithARingThenRollsAndRests()
    {
        var w = Level(p => TestLevels.General(p) && !p.HasBoss && p.Id.Level > 1);
        Quiet(w);
        var her = w.Player.Position;
        var at = her + new Vector2(0f, -7f);
        Assert.True(w.Clear(at, 1f) && w.Clear(her, 1f));
        var t = new Thumpsquirt { Position = at, Seed = 1f };
        w.Thumpsquirts.Add(t);
        var seen = new System.Collections.Generic.List<PlaneEventType>();
        bool hitInAir = false, rolled = false, rested = false;
        for (int i = 0; i < 60 * 14; i++)
        {
            w.Player.Position = w.Player.PrevPosition = her;
            w.Player.Hp = 100f;
            if (t.Airborne && !hitInAir)
            {
                // Her light passes it by in the air.
                float before = t.Corruption.Value;
                w.Shots.Add(Light(t.Position));
                w.Step(default);
                Assert.Equal(before, t.Corruption.Value);
                hitInAir = true;
            }
            else w.Step(default);
            seen.AddRange(w.Events.Select(e => e.Type));
            rolled |= t.State == ThumpState.Rolling;
            rested |= t.State == ThumpState.Rest && rolled;
        }
        Assert.True(hitInAir);
        Assert.Equal(ThumpTuning.Leaps, seen.Count(e => e == PlaneEventType.ThumpLand));
        Assert.Contains(PlaneEventType.ThumpRoll, seen);
        Assert.True(rolled && rested, "after its leaps it rests, rolls after her, and rests again");
        // Landing on her: she was hurt by it.
        Assert.Contains(PlaneEventType.PlayerHit, seen);
    }

    [Fact]
    public void AThumpsquirtLandingOnCleanGroundStainsItAndIsFreedByHerLight()
    {
        var w = Level(p => TestLevels.General(p) && !p.HasBoss && p.Id.Level > 1);
        Quiet(w);
        // Out on ground that was corrupted (not a safe place), with room to land.
        var her = DenseSpot(w, 25f);
        var at = her + new Vector2(0f, -6f);
        for (int k = 0; k < 16 && !w.Clear(at, 1f); k++) at = her + new Vector2(MathF.Cos(k * 0.4f), MathF.Sin(k * 0.4f)) * 6f;
        w.Player.Position = w.Player.PrevPosition = her;
        var t = new Thumpsquirt { Position = at, Seed = 2f };
        w.Thumpsquirts.Add(t);
        w.Corruption!.Cleanse(her, 4f, 5f);
        for (int i = 0; i < 60 * 3 && !w.Events.Any(e => e.Type == PlaneEventType.ThumpLand); i++)
        {
            w.Player.Position = w.Player.PrevPosition = her;
            w.Player.Hp = 100f;
            w.Step(default);
        }
        // (Measured off to the side of where it landed: her own light cleanses under her.)
        var side = t.Position + new Vector2(1.6f, 0f);
        Assert.True(w.Corruption.At(t.Position) > 0.3f || w.Corruption.At(side) > 0.2f, "it stains clean ground it lands on");
        // Resting or rolling, her light frees it: shells and a cleansed patch.
        int shells = w.Shells.Count + w.Run.Shells;
        w.Shots.Add(Light(t.Position, t.Corruption.Max + 1f));
        w.Step(default);
        Assert.False(t.Alive);
        // (She is right there: some may be in her purse already.)
        Assert.True(w.Shells.Count + w.Run.Shells - shells >= ThumpTuning.ShellsMin);
        Assert.True(w.Corruption.At(t.Position) < 0.05f);
    }

    [Fact]
    public void InkwardensGuardTheBlightrootsFromDepthTwo()
    {
        Assert.Empty(Level(p => p.Id.Depth == 2 && !p.HasBoss).Inkwardens);
        var plan = TestLevels.All(p => TestLevels.General(p) && p.Id.Depth == WardenTuning.FirstDepth && !p.HasBoss).FirstOrDefault();
        if (plan == default) return;
        var w = World(TestLevels.Get(plan));
        Assert.NotEmpty(w.Inkwardens);
        Assert.All(w.Inkwardens, g => Assert.Contains(w.Blightroots, r => Vector2.Distance(r.Position, g.Position) <= WardenTuning.NearMax + 0.01f));
    }

    [Fact]
    public void AnInkwardenTurnsAsideAllHarmToItsKinUntilItIsFreed()
    {
        var w = Level(p => TestLevels.General(p) && !p.HasBoss && p.Id.Level > 1);
        Quiet(w);
        var at = w.Player.Position + new Vector2(0f, -4f);
        var m = new Murkling { Position = at, Bud = 0f };
        w.Murklings.Add(m);
        var warden = new Inkwarden { Position = at + new Vector2(3f, 0f), Seed = 1f };
        var other = new Inkwarden { Position = at + new Vector2(-3f, 0f), Seed = 2f };
        w.Inkwardens.Add(warden);
        w.Inkwardens.Add(other);
        // A strong shot at the murkling does nothing; both wardens show their tethers.
        float full = m.Corruption.Value;
        m.Position = at;
        w.Shots.Add(Light(at, 500f));
        w.Step(default);
        Assert.True(m.Alive);
        Assert.Equal(full, m.Corruption.Value);
        Assert.Equal(2, w.Events.Count(e => e.Type == PlaneEventType.Warded));
        // Wardens do not guard each other: one is struck down plainly.
        w.Shots.Add(Light(warden.Position, warden.Corruption.Max + 1f));
        w.Step(default);
        Assert.False(warden.Alive);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.WardenFreed);
        w.Shots.Add(Light(other.Position, other.Corruption.Max + 1f));
        w.Step(default);
        Assert.False(other.Alive);
        // Unguarded, the murkling takes her light.
        m.Position = at;
        w.Shots.Add(Light(at, 500f));
        w.Step(default);
        Assert.False(m.Alive);
    }

    [Fact]
    public void AnInkwardenIsLinkedToEveryCreatureItGuards()
    {
        var w = Level(p => TestLevels.General(p) && !p.HasBoss && p.Id.Level > 1);
        Quiet(w);
        var at = w.Player.Position + new Vector2(0f, -4f);
        var warden = new Inkwarden { Position = at, Seed = 1f };
        var other = new Inkwarden { Position = at + new Vector2(2f, 0f), Seed = 2f };
        w.Inkwardens.Add(warden);
        w.Inkwardens.Add(other);
        var near = new Murkling { Position = at + new Vector2(0f, WardenTuning.Reach - 1f), Bud = 0f };
        var far = new Murkling { Position = at + new Vector2(0f, -(WardenTuning.Reach + 3f)), Bud = 0f };
        var gone = new Murkling { Position = at + new Vector2(1f, 0f), Bud = 0f, Alive = false };
        w.Murklings.AddRange(new[] { near, far, gone });
        // The creature within its reach only: never one out of reach, a dead one, or another warden.
        Assert.Equal(new[] { near.Position }, w.GuardedBy(warden).ToArray());
        warden.Alive = false;
        Assert.Empty(w.GuardedBy(warden));
    }

    [Fact]
    public void LashjawsWaitInTheWallsFromDepthThree()
    {
        Assert.Empty(Level(p => p.Id.Depth == LashTuning.FirstDepth - 1 && !p.HasBoss).Lashjaws);
        // The first level on the general rules, past their depth, whose walls give them a place.
        var w = TestLevels.All(p => TestLevels.General(p) && p.Id.Depth >= LashTuning.FirstDepth && !p.HasBoss)
            .Select(id => World(TestLevels.Get(id))).FirstOrDefault(x => x.Lashjaws.Count > 0);
        Assert.NotNull(w);
        foreach (var l in w.Lashjaws)
        {
            Assert.False(w.Map.IsOpen(l.Position - l.Facing * 1.1f), "rock behind it");
            Assert.True(w.Map.IsOpen(l.Position + l.Facing * 4f), "open water before it");
        }
    }

    /// <summary>A Lashjaw on a wall with her in front of it (out on ground that is not a safe place).</summary>
    static (PlaneWorld W, Lashjaw L, Vector2 Her) Lashing()
    {
        var w = Level(p => TestLevels.General(p) && !p.HasBoss && p.Id.Level > 1);
        Quiet(w);
        for (int c = 0; c < w.Corruption!.Density.Length; c += 13)
        {
            var at = CellAt(c);
            if (!w.Clear(at, 0.5f) || Vector2.Distance(at, w.Map.Start.Position) < 20f) continue;
            for (int k = 0; k < 16; k++)
            {
                var d = new Vector2(MathF.Cos(k * MathF.Tau / 16f), MathF.Sin(k * MathF.Tau / 16f));
                if (w.Map.IsOpen(at - d * 1.1f)) continue;
                bool open = true;
                for (float r = 1f; r <= 8f && open; r += 0.5f) open = w.Clear(at + d * r, 0.6f);
                if (!open) continue;
                var l = new Lashjaw { Position = at, Facing = d, Seed = 1f, Timer = LashTuning.Pause };
                l.Tip = l.Mouth;
                w.Lashjaws.Add(l);
                var her = at + d * 7f;
                w.Player.Position = w.Player.PrevPosition = her;
                return (w, l, her);
            }
        }
        throw new InvalidOperationException("no wall to lodge it in");
    }

    [Fact]
    public void ALashjawCatchesHerAndReelsHerInAgainstHerSwimmingThenBitesAndLetsGo()
    {
        var (w, l, her) = Lashing();
        bool caught = false;
        for (int i = 0; i < 120 && !caught; i++)
        {
            w.Player.Hp = 100f;
            w.Step(default);
            caught = l.State == LashState.Hold;
        }
        Assert.True(caught);
        // Swimming straight away from it only slows the pull: she is still drawn in.
        float before = Vector2.Distance(w.Player.Position, l.Mouth);
        for (int i = 0; i < 30; i++)
        {
            w.Player.Hp = 100f;
            w.Step(new PlaneInput { Move = l.Facing });
        }
        float after = Vector2.Distance(w.Player.Position, l.Mouth);
        Assert.True(after < before - 0.3f, "she cannot swim free");
        Assert.True(before - after < LashTuning.Pull * 0.5f, "but swimming against it slows it");
        // Reeled all the way in: bitten and let go; it chews.
        bool bitten = false;
        for (int i = 0; i < 60 * 8 && !bitten; i++)
        {
            w.Player.Hp = 100f;
            w.Step(default);
            bitten = w.Events.Any(e => e.Type == PlaneEventType.LashBite);
        }
        Assert.True(bitten);
        Assert.Equal(LashState.Chew, l.State);
        Assert.Null(w.HeldBy);
    }

    [Fact]
    public void BreakingItsMeterOnceFreesHerAndStunsItTwiceFreesIt()
    {
        var (w, l, her) = Lashing();
        for (int i = 0; i < 120 && l.State != LashState.Hold; i++) w.Step(default);
        Assert.Equal(LashState.Hold, l.State);
        w.Shots.Add(Light(l.Position, l.Corruption.Max + 1f));
        w.Step(default);
        Assert.True(l.Alive);
        Assert.Equal(LashState.Stunned, l.State);
        Assert.Null(w.HeldBy);
        Assert.Equal(l.Corruption.Max, l.Corruption.Value, 1);
        w.Shots.Add(Light(l.Position, l.Corruption.Max + 1f));
        w.Step(default);
        Assert.False(l.Alive);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.LashFreed);
    }

    [Fact]
    public void ALashjawDevoursAFishAndLetsItOutWhenFreed()
    {
        var (w, l, her) = Lashing();
        // She is away; a fish swims before it.
        w.Player.Position = w.Player.PrevPosition = l.Position + l.Facing * 30f;
        var fish = w.ReefFish.FirstOrDefault() ?? new PlaneReefFish { Kind = ReefFishKind.BlueTang, Rng = new Rng(1UL) };
        if (!w.ReefFish.Contains(fish)) w.ReefFish.Add(fish);
        bool swallowed = false;
        for (int i = 0; i < 60 * 4 && !swallowed; i++)
        {
            if (l.State is LashState.Watch or LashState.Windup or LashState.Lash) fish.Position = l.Position + l.Facing * 5f;
            fish.Velocity = System.Numerics.Vector2.Zero;
            w.Player.Hp = 100f;
            w.Step(default);
            swallowed = l.Swallowed.Contains(fish);
        }
        Assert.True(swallowed);
        Assert.True(fish.Corrupted, "out of the reef while swallowed");
        Assert.Equal(LashState.Chew, l.State);
        for (int k = 0; k < 2; k++)
        {
            w.Shots.Add(Light(l.Position, l.Corruption.Max + 1f));
            w.Step(default);
        }
        Assert.False(l.Alive);
        Assert.False(fish.Corrupted);
        Assert.True(fish.Freed);
        // It swims off away from her and is gone.
        Assert.NotEqual(System.Numerics.Vector2.Zero, fish.Leaving);
        for (int i = 0; i < 60 * (int)ReefFishTuning.LeaveMax + 60; i++) w.Step(default);
        Assert.True(fish.Gone);
    }

    static void Quiet(PlaneWorld w)
    {
        foreach (var area in w.Areas) area.Spent = true;
        w.Murklings.Clear();
        w.Blotters.Clear();
        w.Bombs.Clear();
    }
}
