using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>The pearls past the shot modifiers: stats, Pearl Diver, Starfish Arm, Ink Sac, Remora, the hook, the actives.</summary>
public class PlanePearlEffectTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static readonly Lazy<LevelMap> Room = new(TestLevels.OneTreasure);

    /// <summary>A world on a quiet level (no mobs unless a test adds them) with these pearls.</summary>
    static PlaneWorld World(params string[] pearls)
    {
        var run = new PlaneRun(Catalog.Value, new Tuning());
        foreach (string id in pearls) run.Add(id);
        var w = new PlaneWorld(Room.Value, new Tuning(), run);
        w.Mobs.Clear();
        w.Pearls.Clear();
        return w;
    }

    static readonly PlaneInput Fire = new() { Aim = Vector2.UnitX, Fire = true };

    [Fact]
    public void EveryPortedPearlIsInTheCatalogAndDoesSomethingOnThePlane()
    {
        foreach (string id in PlaneRun.PortedPearls)
        {
            Assert.True(Catalog.Value.Contains(id), id);
            var item = Catalog.Value[id];
            if (item.Kind == ItemKind.Active)
                Assert.Contains(item.Active!.Action, new[] { ActiveAction.BubbleShield, ActiveAction.WhaleSong });
            else
                Assert.True(item.Shot is not null || item.Stats.Count > 0 || item.Flags.Count > 0, id);
        }
    }

    [Fact]
    public void CoralCrownRaisesMaxHpHealsAndHitsHarder()
    {
        var w = World();
        w.Player.Hp = 50f;
        w.Pearls.Add(new PlanePearl { ItemId = "coral_crown", Position = w.Player.Position });
        w.Step(default);
        Assert.Equal(120f, w.Run.MaxHp);
        Assert.Equal(70f, w.Player.Hp);

        w.Step(Fire);
        var shot = w.Shots.Single(s => s.FromPlayer);
        Assert.Equal(PlaneCombatTuning.ShotDamage * (3.5f + 0.3f) / 3.5f, shot.Damage, 3);
    }

    [Fact]
    public void ShopPearlsHealOnPickupToo()
    {
        var w = World();
        w.Player.Hp = 90f;
        w.Pearls.Add(new PlanePearl { ItemId = "moon_jelly_heart", Position = w.Player.Position });
        w.Step(default);
        Assert.Equal(120f, w.Run.MaxHp);
        Assert.Equal(110f, w.Player.Hp);
    }

    [Fact]
    public void SharkToothHitsHarder()
    {
        var w = World("shark_tooth");
        w.Step(Fire);
        Assert.Equal(PlaneCombatTuning.ShotDamage * 4f / 3.5f, w.Shots.Single(s => s.FromPlayer).Damage, 3);
    }

    [Fact]
    public void PearlDiverChargesWhileHeldAndThrowsOnRelease()
    {
        var w = World("pearl_diver");
        int full = 0;
        for (int i = 0; i < 80; i++)
        {
            w.Step(Fire);
            full += w.Events.Count(e => e.Type == PlaneEventType.ChargeFull);
        }
        Assert.Empty(w.Shots);
        Assert.Equal(1, full);

        w.Step(new PlaneInput { Aim = Vector2.UnitX });
        var pearl = w.Shots.Single(s => s.FromPlayer);
        Assert.Equal(1f, pearl.Charged, 3);
        Assert.Equal(PlaneCombatTuning.ShotDamage * PlaneCombatTuning.ChargeDamage, pearl.Damage, 3);
        Assert.Equal(PlaneCombatTuning.ShotRadius * PlaneCombatTuning.ChargeSize, pearl.Radius, 3);
        Assert.Equal(0f, w.Player.Charge);
    }

    [Fact]
    public void PearlDiverTapThrowsAPlainBubble()
    {
        var w = World("pearl_diver");
        w.Step(Fire);
        w.Step(new PlaneInput { Aim = Vector2.UnitX });
        var shot = w.Shots.Single(s => s.FromPlayer);
        Assert.True(shot.Damage < PlaneCombatTuning.ShotDamage * 1.1f);
    }

    [Fact]
    public void StarfishArmBubblesGrowAsTheyFly()
    {
        var w = World("starfish_arm");
        w.Step(Fire);
        var shot = w.Shots.Single(s => s.FromPlayer);
        float start = shot.Radius;
        for (int i = 0; i < 20 && shot.Life > 0f; i++) w.Step(default);
        Assert.True(shot.Traveled > 2f);
        Assert.True(shot.Radius > start * 1.15f, $"{start} → {shot.Radius}");
    }

    [Fact]
    public void InkSacBubblesBurstIntoInkThatHurtsEveryoneNear()
    {
        var w = World("ink_sac");
        var at = w.Player.Position + new Vector2(4f, 0f);
        var a = new PlaneMob { Position = at, Home = at, Rng = new Rng(1) };
        var b = new PlaneMob { Position = at + new Vector2(0f, 1.2f), Home = at, Rng = new Rng(2) };
        w.Mobs.Add(a);
        w.Mobs.Add(b);
        w.Shots.Add(new PlaneShot { Position = at, Line = at, Velocity = Vector2.UnitX, Life = 1f, FromPlayer = true, Damage = 10f, Explosive = true });
        w.Step(default);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.InkBlast);
        Assert.True(a.Hp <= PlaneCombatTuning.MobHp - 10f - 6f + 0.01f, $"hit and blast: {a.Hp}");
        Assert.True(b.Hp < PlaneCombatTuning.MobHp, "the blast reaches the one beside it");
    }

    [Fact]
    public void RemoraSuckerPullsShellsFromFarther()
    {
        foreach (bool remora in new[] { false, true })
        {
            var w = remora ? World("remora_sucker") : World();
            w.Shells.Clear();
            var shell = new PlaneShell { Position = w.Player.Position + new Vector2(0f, 5f) };
            if (!w.Map.IsOpen(shell.Position)) shell.Position = w.Player.Position + new Vector2(5f, 0f);
            w.Shells.Add(shell);
            for (int i = 0; i < 120; i++) w.Step(default);
            Assert.Equal(remora, w.Run.Shells == 1);
        }
    }

    [Fact]
    public void CaptainsHookKnocksMobsBackHarder()
    {
        float Shove(params string[] pearls)
        {
            var w = World(pearls);
            var at = w.Player.Position + new Vector2(3f, 0f);
            var mob = new PlaneMob { Position = at, Home = at, Rng = new Rng(3) };
            w.Mobs.Add(mob);
            w.Shots.Add(new PlaneShot { Position = at, Line = at, Velocity = Vector2.UnitX * 10f, Life = 1f, FromPlayer = true, Damage = 1f });
            w.Step(default);
            return mob.Knock.Length();
        }
        float plain = Shove(), hooked = Shove("captains_hook");
        Assert.True(plain > 0f);
        Assert.Equal(plain * 2.5f, hooked, 3);
    }

    [Fact]
    public void LanternPearlBrightensHerGlow()
    {
        Assert.Equal(1.8f, World("lantern_pearl").Run.Loadout.Stats[Stat.Glow], 3);
    }

    [Fact]
    public void BubbleShieldTurnsHitsAwayThenRecharges()
    {
        var w = World("bubble_shield");
        Assert.Equal(1f, w.Run.ActiveCharge);
        w.Step(new PlaneInput { UseActive = true });
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.ActiveUsed);
        Assert.Equal(0f, w.Run.ActiveCharge, 3);

        var p = w.Player;
        w.Shots.Add(new PlaneShot { Position = p.Position, Line = p.Position, Velocity = Vector2.UnitX, Life = 1f, Needle = true, Damage = 8f, Radius = 0.18f });
        w.Step(default);
        Assert.Equal(100f, p.Hp);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.ShieldBlocked);

        // Not ready again until it has recharged (20 s).
        w.Step(new PlaneInput { UseActive = true });
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.ActiveNotReady);
        for (int i = 0; i < 20 * PlaneWorld.TickRate; i++) w.Step(default);
        Assert.Equal(1f, w.Run.ActiveCharge);
    }

    [Fact]
    public void WhaleSongHealsOnlyWhenHurt()
    {
        var w = World("whale_song");
        w.Step(new PlaneInput { UseActive = true });
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.ActiveDenied);
        Assert.Equal(1f, w.Run.ActiveCharge);

        w.Player.Hp = 40f;
        w.Step(new PlaneInput { UseActive = true });
        Assert.Equal(75f, w.Player.Hp);
        Assert.Equal(0f, w.Run.ActiveCharge, 3);
    }

    [Fact]
    public void ANewActivePearlReplacesTheOldOneCharged()
    {
        var w = World("bubble_shield");
        w.Step(new PlaneInput { UseActive = true });
        w.Pearls.Add(new PlanePearl { ItemId = "whale_song", Position = w.Player.Position });
        w.Step(default);
        Assert.Equal("whale_song", w.Run.Active!.Id);
        Assert.Equal(1f, w.Run.ActiveCharge);
    }
}
