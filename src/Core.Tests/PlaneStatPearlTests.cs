using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Stat pearls on the plane (stacking, trade-offs), Sardine Run, Pistol Shrimp Claw, Pearl Necklace, Depth Charge.</summary>
public class PlaneStatPearlTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static readonly Lazy<LevelMap> Room = new(TestLevels.OneTreasure);

    static PlaneWorld World(params string[] pearls)
    {
        var run = new PlaneRun(Catalog.Value, new Tuning());
        foreach (string id in pearls) run.Add(id);
        var w = new PlaneWorld(Room.Value, new Tuning(), run, new PlaneOptions { Corruption = false });
        w.Mobs.Clear();
        w.Pearls.Clear();
        return w;
    }

    /// <summary>An aim from her start into open water (so shots fly their full range).</summary>
    static Vector2 OpenAim(PlaneWorld w)
    {
        for (int i = 0; i < 16; i++)
        {
            float a = i * MathF.Tau / 16f;
            var d = new Vector2(MathF.Cos(a), MathF.Sin(a));
            if (Enumerable.Range(1, 14).All(k => w.Map.IsOpen(w.Player.Position + d * k))) return d;
        }
        return Vector2.UnitX;
    }

    static int ShotsIn(PlaneWorld w, float seconds)
    {
        var aim = OpenAim(w);
        int shots = 0;
        for (int i = 0; i < (int)(seconds * PlaneWorld.TickRate); i++)
        {
            w.Player.Hp = 100f;
            w.Step(new PlaneInput { Aim = aim, Fire = true });
            shots += w.Events.Count(e => e.Type == PlaneEventType.Shot);
        }
        return shots;
    }

    [Fact]
    public void FireRateShotSpeedRangeSizeAndSwimSpeedAllCount()
    {
        Assert.True(ShotsIn(World("squid_ink_espresso"), 3f) > ShotsIn(World(), 3f));
        var plain = World();
        var cuttle = World("cuttlebone");
        plain.Step(new PlaneInput { Aim = Vector2.UnitX, Fire = true });
        cuttle.Step(new PlaneInput { Aim = Vector2.UnitX, Fire = true });
        var a = plain.Shots.Single(s => s.FromPlayer);
        var b = cuttle.Shots.Single(s => s.FromPlayer);
        Assert.True(b.Velocity.Length() > a.Velocity.Length() * 1.2f);
        Assert.True(b.Range > a.Range * 1.1f);
        var lung = World("whale_lung");
        lung.Step(new PlaneInput { Aim = Vector2.UnitX, Fire = true });
        Assert.Equal(a.Radius * 1.3f, lung.Shots.Single(s => s.FromPlayer).Radius, 3);
        Assert.True(World("dolphin_fin").CruiseSpeed > World().CruiseSpeed);
        Assert.True(World("sea_cucumber").CruiseSpeed < World().CruiseSpeed);
    }

    [Fact]
    public void StatPearlsStackAndTradeOffs()
    {
        Assert.True(World("glass_squid", "glass_squid").Run.Loadout.Stats.Damage > World("glass_squid").Run.Loadout.Stats.Damage);
        Assert.Equal(60f, World("glass_squid", "glass_squid").Run.MaxHp);
        Assert.Equal(130f, World("sea_cucumber").Run.MaxHp);
        Assert.True(Catalog.Value.Items.Where(i => PlaneRun.PortedPearls.Contains(i.Id) && i.Kind == ItemKind.Passive && i.Shot is null && i.Flags.Count == 0 && i.Effects.All(e => e.Action is EffectAction.Heal or EffectAction.Coins))
            .All(i => !i.Unique), "pure stat pearls can be absorbed again");
    }

    [Fact]
    public void CoralPolypMendsHerSlowly()
    {
        var w = World("coral_polyp");
        w.Player.Hp = 50f;
        for (int i = 0; i < 4 * PlaneWorld.TickRate; i++) w.Step(default);
        Assert.InRange(w.Player.Hp, 50.9f, 51.1f);
    }

    [Fact]
    public void SardineRunSpinsUpIntoAStream()
    {
        var w = World("sardine_run");
        var aim = OpenAim(w);
        int first = 0, later = 0;
        for (int i = 0; i < 3 * PlaneWorld.TickRate; i++)
        {
            w.Step(new PlaneInput { Aim = aim, Fire = true });
            int n = w.Events.Count(e => e.Type == PlaneEventType.Shot);
            if (i < PlaneWorld.TickRate / 2) first += n;
            else if (i >= 2 * PlaneWorld.TickRate && i < 2.5f * PlaneWorld.TickRate) later += n;
        }
        Assert.Equal(1f, w.Player.Spin);
        Assert.True(later >= first * 2, $"{first} in the first half second, {later} spun up");
        // Small, weak shots.
        var shot = w.Shots.First(s => s.FromPlayer);
        Assert.True(shot.Damage < PlaneCombatTuning.ShotDamage * 0.4f);
        // Let go, it spins down.
        for (int i = 0; i < PlaneWorld.TickRate; i++) w.Step(new PlaneInput { Aim = aim });
        Assert.Equal(0f, w.Player.Spin);
    }

    [Fact]
    public void PistolShrimpShotsBangAtTheEndOfTheirRange()
    {
        var w = World("pistol_shrimp");
        var aim = OpenAim(w);
        w.Step(new PlaneInput { Aim = aim, Fire = true });
        var shot = w.Shots.Single(s => s.FromPlayer);
        // A pufferling waiting just past where the light goes out.
        var at = w.Player.Position + aim * (shot.Range + 1f);
        var mob = new PlaneMob { Position = at, Home = at, Rng = new Rng(9) };
        w.Mobs.Add(mob);
        bool bang = false;
        for (int i = 0; i < 2 * PlaneWorld.TickRate && !bang; i++)
        {
            w.Step(default);
            bang = w.Events.Any(e => e.Type == PlaneEventType.Cavitation);
        }
        Assert.True(bang);
        Assert.True(mob.Hp < PlaneCombatTuning.MobHp, "the blast hits what is near");
    }

    [Fact]
    public void PearlNecklaceAndDepthCharge()
    {
        var w = World("pearl_diver", "mitosis");
        Assert.Contains("pearl_necklace", w.Run.Loadout.Synergies);
        var pearl = new PlaneShot { Position = w.Player.Position + new Vector2(3f, 0f), Velocity = Vector2.UnitX, FromPlayer = true, Split = true, Charged = 1f, Life = 1f, Damage = 30f };
        w.Shots.Add(pearl);
        w.Mobs.Add(new PlaneMob { Position = pearl.Position + new Vector2(0.2f, 0f), Home = pearl.Position, Rng = new Rng(4) });
        w.Step(default);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.Necklace);
        Assert.True(w.Shots.Count(s => s.FromPlayer && s != pearl) >= PlaneCombatTuning.NecklaceCount - 1);

        float Reach(params string[] pearls)
        {
            var d = World(pearls);
            var at = d.Player.Position + new Vector2(3f, 0f);
            d.Shots.Add(new PlaneShot { Position = at, Line = at, Velocity = Vector2.UnitX, FromPlayer = true, Cavitation = true, Charged = 1f, Life = 0.001f, Damage = 30f, Radius = 0.3f });
            d.Step(default);
            return d.Events.Single(e => e.Type == PlaneEventType.Cavitation).Size;
        }
        Assert.Equal(Reach("pistol_shrimp") * PlaneCombatTuning.DepthChargeReach, Reach("pistol_shrimp", "pearl_diver"), 3);
    }

    [Fact]
    public void TheStatSheetShowsWhatPlays()
    {
        var bare = PlaneStatSheet.Of(World().Run);
        Assert.All(bare, l => Assert.Equal(0f, l.Change, 3));
        var w = World("shark_tooth", "squid_ink_espresso", "dolphin_fin");
        var sheet = PlaneStatSheet.Of(w.Run).ToDictionary(l => l.Name);
        Assert.True(sheet["Damage per shot"].Change > 0.1f);
        Assert.True(sheet["Fire rate"].Change > 0.1f);
        Assert.True(sheet["Swim speed"].Change > 0.5f);
        Assert.True(sheet["Dash cooldown"].Change < 0f && sheet["Dash cooldown"].LowerIsBetter);
        Assert.Equal($"{w.CruiseSpeed:0.#} m/s", sheet["Swim speed"].Value);
    }
}
