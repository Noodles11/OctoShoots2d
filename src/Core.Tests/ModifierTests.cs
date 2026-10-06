using System.Linq;
using System.Numerics;
using OctoShoots.Core.Items;
using OctoShoots.Core.Sim;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Shot modifiers, statuses, triggers and actives running in the actual simulation.</summary>
public class ModifierTests
{
    static World WithItems(int enemies, params string[] items)
    {
        var world = ItemWorlds.Create(enemies);
        foreach (var id in items) world.GiveItem(id);
        return world;
    }

    static bool Saw(World w, SimEventType type) => w.Events.Any(e => e.Type == type);

    // ── volley shapes ──

    [Fact]
    public void TripleTentacleFiresAFan()
    {
        var world = WithItems(0, "triple_tentacle");
        var shots = ItemWorlds.FireOnce(world);
        Assert.Equal(3, shots.Length);
        float spread = MathUtil.AngleBetween(shots[0].Velocity, shots[2].Velocity) * MathUtil.Rad2Deg;
        Assert.InRange(spread, 14f, 18f);
    }

    [Fact]
    public void HammerheadFiresACone()
    {
        var world = WithItems(0, "hammerhead");
        var shots = ItemWorlds.FireOnce(world);
        Assert.Equal(5, shots.Length);
        var centre = shots[0].Velocity;
        foreach (var s in shots.Skip(1))
            Assert.InRange(MathUtil.AngleBetween(centre, s.Velocity) * MathUtil.Rad2Deg, 5f, 7f);
    }

    [Fact]
    public void RearFinAlsoFiresBackwards()
    {
        var world = WithItems(0, "rear_fin");
        var shots = ItemWorlds.FireOnce(world);
        Assert.Equal(2, shots.Length);
        Assert.Contains(shots, s => s.Rear && s.Velocity.X < 0f);
    }

    [Fact]
    public void KrakenFormShootsEightWays()
    {
        var world = WithItems(0, "cuttlebone", "starfish_arm", "giant_squid_eye");
        Assert.Contains("kraken_form", world.Loadout.Transformations);
        Assert.Equal(8, ItemWorlds.FireOnce(world).Length);
    }

    [Fact]
    public void DoubleHelixShotsCircleTheAimLine()
    {
        var world = WithItems(0, "double_helix");
        var shots = ItemWorlds.FireOnce(world);
        Assert.Equal(2, shots.Length);
        TestWorlds.Run(world, ItemWorlds.LookPlusX(), 10);
        var a = world.Projectiles.Single(p => p.Id == shots[0].Id);
        var b = world.Projectiles.Single(p => p.Id == shots[1].Id);
        // Same centre line, opposite sides of it.
        Assert.True(Vector3.Distance(a.AxisPosition, b.AxisPosition) < 0.01f);
        Assert.InRange(Vector3.Distance(a.Position, b.Position), 0.6f, 0.75f);
    }

    // ── flight behaviour ──

    [Fact]
    public void PierceHitsEveryCreatureInLine()
    {
        var world = WithItems(2, "swordfish_bill");
        var a = ItemWorlds.Place(world, 0, new Vector3(3.5f, 0f, 0f));
        var b = ItemWorlds.Place(world, 1, new Vector3(6.5f, 0f, 0f));
        ItemWorlds.FireOnce(world);
        ItemWorlds.RunUntil(world, w => b.Hp < 100f, 60);
        Assert.True(a.Hp < 100f && b.Hp < 100f);
    }

    [Fact]
    public void WithoutPierceOnlyTheFirstIsHit()
    {
        var world = WithItems(2);
        var a = ItemWorlds.Place(world, 0, new Vector3(3.5f, 0f, 0f));
        var b = ItemWorlds.Place(world, 1, new Vector3(6.5f, 0f, 0f));
        ItemWorlds.FireOnce(world);
        TestWorlds.Run(world, ItemWorlds.LookPlusX(), 60);
        Assert.True(a.Hp < 100f);
        Assert.Equal(100f, b.Hp);
    }

    [Fact]
    public void MitosisSplitsInTwoOnHit()
    {
        var world = WithItems(1, "mitosis");
        ItemWorlds.Place(world, 0, new Vector3(4f, 0f, 0f));
        ItemWorlds.FireOnce(world);
        Assert.True(ItemWorlds.RunUntil(world, w => w.Events.Any(e => e.Type == SimEventType.ShotHitEnemy), 60));
        world.Step(ItemWorlds.LookPlusX());
        Assert.Equal(2, world.Projectiles.Count(p => p.FromPlayer && !p.CanSplit));
    }

    [Fact]
    public void MirrorScaleBouncesOffRock()
    {
        var world = WithItems(0, "mirror_scale");
        ItemWorlds.FireOnce(world, pitchDeg: -85f);
        Assert.True(ItemWorlds.RunUntil(world, w => Saw(w, SimEventType.ShotBounced), 60, -85f));
        Assert.Contains(world.Projectiles, p => p.FromPlayer && p.Velocity.Y > 0f);
    }

    [Fact]
    public void GhostJellyPassesThroughRock()
    {
        var world = WithItems(0, "ghost_jelly");
        ItemWorlds.FireOnce(world, pitchDeg: -85f);
        bool splat = false, expired = false;
        for (int i = 0; i < 60 && !expired; i++)
        {
            world.Step(ItemWorlds.LookPlusX(false, -85f));
            splat |= Saw(world, SimEventType.ShotHitTerrain);
            expired |= Saw(world, SimEventType.ShotExpired);
        }
        Assert.False(splat);
        Assert.True(expired);
    }

    [Fact]
    public void HomingFindsAnOffAxisTarget()
    {
        var plain = WithItems(1);
        var e1 = ItemWorlds.Place(plain, 0, new Vector3(5f, 0f, 3f));
        ItemWorlds.FireOnce(plain);
        TestWorlds.Run(plain, ItemWorlds.LookPlusX(), 60);
        Assert.Equal(100f, e1.Hp);

        var homing = WithItems(1, "anglerfish_lure");
        var e2 = ItemWorlds.Place(homing, 0, new Vector3(5f, 0f, 3f));
        ItemWorlds.FireOnce(homing);
        Assert.True(ItemWorlds.RunUntil(homing, w => e2.Hp < 100f, 60));
    }

    [Fact]
    public void BoomerangComesBack()
    {
        var world = WithItems(0, "boomerang_shrimp");
        ItemWorlds.FireOnce(world);
        Assert.True(ItemWorlds.RunUntil(world, w => Saw(w, SimEventType.ShotReturned), 180));
    }

    [Fact]
    public void StarfishArmGrowsShots()
    {
        var world = WithItems(0, "starfish_arm");
        var shot = ItemWorlds.FireOnce(world)[0];
        float r0 = shot.Radius, d0 = shot.Damage;
        TestWorlds.Run(world, ItemWorlds.LookPlusX(), 20);
        Assert.True(shot.Radius > r0 * 1.4f && shot.Damage > d0 * 1.4f);
    }

    // ── on-hit effects ──

    [Fact]
    public void InkSacExplosionHurtsNeighbours()
    {
        var world = WithItems(2, "ink_sac");
        ItemWorlds.Place(world, 0, new Vector3(4f, 0f, 0f));
        var neighbour = ItemWorlds.Place(world, 1, new Vector3(4f, 1.2f, 0f));
        ItemWorlds.FireOnce(world);
        Assert.True(ItemWorlds.RunUntil(world, w => Saw(w, SimEventType.Explosion), 60));
        Assert.True(neighbour.Hp < 100f);
        Assert.Equal(world.Tuning.MaxHp, world.Player.Hp);
    }

    [Fact]
    public void EelTailChainsLightning()
    {
        var world = WithItems(2, "electric_eel_tail");
        ItemWorlds.Place(world, 0, new Vector3(4f, 0f, 0f));
        var other = ItemWorlds.Place(world, 1, new Vector3(4f, 0f, 3f));
        ItemWorlds.FireOnce(world);
        Assert.True(ItemWorlds.RunUntil(world, w => Saw(w, SimEventType.ChainArc), 60));
        Assert.True(other.Hp < 100f);
    }

    static ItemCatalog SureFire() => ItemCatalog.FromJson("""
        {
          "items": [
            { "id": "ice", "name": "Ice", "pools": ["treasure"], "shot": { "freezeChance": 1 }, "pearl": { "colors": ["#ffffff", "#88ccff"] } },
            { "id": "fire", "name": "Fire", "pools": ["treasure"], "shot": { "burnChance": 1 }, "pearl": { "colors": ["#ff8800", "#ffdd00"] } },
            { "id": "venom", "name": "Venom", "pools": ["treasure"], "shot": { "poisonChance": 1 }, "pearl": { "colors": ["#33ff66", "#115522"] } }
          ],
          "synergies": [ { "id": "steam_vent", "name": "Steam Vent", "requires": ["ice", "fire"] } ]
        }
        """);

    [Fact]
    public void FreezeStopsACreature()
    {
        var world = ItemWorlds.Create(1, SureFire(), t => t.EnemySpeed = 3f);
        world.GiveItem("ice");
        var e = ItemWorlds.Place(world, 0, new Vector3(4f, 0f, 0f));
        ItemWorlds.FireOnce(world);
        Assert.True(ItemWorlds.RunUntil(world, w => e.FrozenTimer > 0f, 60));
        var at = e.Position;
        TestWorlds.Run(world, ItemWorlds.LookPlusX(), 30);
        Assert.True(Vector3.Distance(at, e.Position) < 0.3f);
        Assert.NotEqual(EnemyState.Telegraph, e.State);
    }

    [Fact]
    public void PoisonDamagesOverTime()
    {
        var world = ItemWorlds.Create(1, SureFire());
        world.GiveItem("venom");
        var e = ItemWorlds.Place(world, 0, new Vector3(4f, 0f, 0f));
        ItemWorlds.FireOnce(world);
        Assert.True(ItemWorlds.RunUntil(world, w => e.PoisonTimer > 0f, 60));
        float after = e.Hp;
        TestWorlds.Run(world, ItemWorlds.LookPlusX(), 60);
        Assert.True(e.Hp < after - 0.5f);
    }

    [Fact]
    public void SteamVentBlowsUpFrozenBurningFoes()
    {
        var world = ItemWorlds.Create(1, SureFire());
        world.GiveItem("ice");
        world.GiveItem("fire");
        ItemWorlds.Place(world, 0, new Vector3(4f, 0f, 0f));
        bool steam = false;
        var input = ItemWorlds.LookPlusX(fire: true);
        for (int i = 0; i < 120 && !steam; i++)
        {
            world.Step(input);
            steam = world.Events.Any(ev => ev.Type == SimEventType.Explosion && ev.Tag == "steam");
        }
        Assert.True(steam);
    }

    [Fact]
    public void FrozenFoesShatterIntoShards()
    {
        var world = ItemWorlds.Create(1, SureFire(), t => t.EnemyHp = 3f);
        world.GiveItem("ice");
        ItemWorlds.Place(world, 0, new Vector3(4f, 0f, 0f));
        ItemWorlds.FireOnce(world);
        Assert.True(ItemWorlds.RunUntil(world, w => w.Events.Any(e => e.Type == SimEventType.EnemyDied && e.Tag == "shatter"), 60));
        world.Step(ItemWorlds.LookPlusX());
        Assert.Equal(4, world.Projectiles.Count(p => p.Kind == ProjectileKind.Shard));
    }

    // ── charge weapons ──

    [Fact]
    public void PearlDiverChargesAThreeTimesPearl()
    {
        var world = WithItems(0, "pearl_diver");
        TestWorlds.Run(world, ItemWorlds.LookPlusX(fire: true), 70);
        Assert.Empty(world.Projectiles);
        Assert.Equal(1f, world.Player.Charge);
        world.Step(ItemWorlds.LookPlusX());
        var pearl = Assert.Single(world.Projectiles);
        Assert.Equal(ProjectileKind.Pearl, pearl.Kind);
        Assert.Equal(world.Loadout.Stats.Damage * 3f, pearl.Damage, 3);
    }

    [Fact]
    public void SunbeamFiresABeamThatHurts()
    {
        var world = WithItems(1, "sunbeam");
        var e = ItemWorlds.Place(world, 0, new Vector3(6f, 0f, 0f));
        TestWorlds.Run(world, ItemWorlds.LookPlusX(fire: true), 60);
        bool beam = false;
        for (int i = 0; i < 30; i++)
        {
            world.Step(ItemWorlds.LookPlusX());
            beam |= Saw(world, SimEventType.BeamTick);
        }
        Assert.True(beam);
        Assert.True(e.Hp < 100f - world.Loadout.Stats.Damage * 2f);
    }

    // ── triggers ──

    [Fact]
    public void LampreyHealsOnKill()
    {
        var world = ItemWorlds.Create(1, tweak: t => t.EnemyHp = 1f);
        world.GiveItem("lamprey_mouth");
        world.Player.Hp = 50f;
        ItemWorlds.Place(world, 0, new Vector3(4f, 0f, 0f));
        ItemWorlds.FireOnce(world);
        Assert.True(ItemWorlds.RunUntil(world, w => Saw(w, SimEventType.EnemyDied), 60));
        Assert.Equal(53f, world.Player.Hp, 1);
    }

    [Fact]
    public void FireUrchinSpineBurstsOnKill()
    {
        var world = ItemWorlds.Create(1, tweak: t => t.EnemyHp = 1f);
        world.GiveItem("fire_urchin_spine");
        ItemWorlds.Place(world, 0, new Vector3(4f, 0f, 0f));
        ItemWorlds.FireOnce(world);
        Assert.True(ItemWorlds.RunUntil(world, w => Saw(w, SimEventType.EnemyDied), 60));
        world.Step(ItemWorlds.LookPlusX());
        Assert.Equal(6, world.Projectiles.Count(p => p.Kind == ProjectileKind.Shard));
    }

    [Fact]
    public void FoamSoaksDamageFirst()
    {
        var world = WithItems(0, "barnacle_armor");
        Assert.Equal(30f, world.Player.Foam);
        world.Projectiles.Add(new Projectile
        {
            Id = 999,
            Kind = ProjectileKind.Orb,
            Position = world.Player.Position + new Vector3(0.4f, 0f, 0f),
            Velocity = new Vector3(-6f, 0f, 0f),
            Radius = 0.3f,
            Damage = 10f,
            MaxRange = 20f,
        });
        TestWorlds.Run(world, ItemWorlds.LookPlusX(), 5);
        Assert.Equal(20f, world.Player.Foam);
        Assert.Equal(world.Loadout.Stats.MaxHp, world.Player.Hp);
    }

    [Fact]
    public void HeartItemsRaiseMaxHpAndHeal()
    {
        var world = WithItems(0);
        world.Player.Hp = 70f;
        world.GiveItem("coral_crown");
        Assert.Equal(120f, world.Loadout.Stats.MaxHp);
        Assert.Equal(90f, world.Player.Hp);
    }

    [Fact]
    public void SecondItemAnnouncesTheSynergy()
    {
        var world = WithItems(0, "electric_eel_tail");
        world.Events.Clear();
        world.GiveItem("mirror_scale");
        Assert.Contains(world.Events, e => e.Type == SimEventType.SynergyActivated && e.Tag == "pinball_storm");
    }

    // ── actives ──

    [Fact]
    public void ConchHornStunsNearbyCreatures()
    {
        var world = WithItems(1, "conch_horn");
        var e = ItemWorlds.Place(world, 0, new Vector3(5f, 0f, 0f));
        Assert.Equal(30f, world.ActiveCharge);
        var input = ItemWorlds.LookPlusX();
        input.UseActive = true;
        world.Step(input);
        Assert.True(e.StunTimer > 0f);
        Assert.Equal(0f, world.ActiveCharge);
        Assert.Contains(world.Events, ev => ev.Type == SimEventType.ActiveUsed && ev.Tag == "conch_horn");

        world.Step(input);
        Assert.Contains(world.Events, ev => ev.Type == SimEventType.ActiveNotReady);
    }

    [Fact]
    public void ActivesRechargeOverTime()
    {
        var world = WithItems(0, "bubble_shield"); // 20 s
        var use = ItemWorlds.LookPlusX();
        use.UseActive = true;
        world.Step(use);
        Assert.Equal(0f, world.ActiveCharge, 1);
        Assert.False(world.ActiveReady);

        TestWorlds.Run(world, ItemWorlds.LookPlusX(), 60 * 10);
        Assert.Equal(10f, world.ActiveCharge, 1);
        Assert.False(world.ActiveReady);

        bool announced = false;
        for (int i = 0; i < 60 * 10 + 5; i++)
        {
            world.Step(ItemWorlds.LookPlusX());
            announced |= world.Events.Any(e => e.Type == SimEventType.ActiveCharged);
        }
        Assert.True(world.ActiveReady);
        Assert.True(announced);
    }

    [Fact]
    public void BrainCoralSpeedsUpRecharging()
    {
        float ChargeAfterTenSeconds(params string[] items)
        {
            var world = WithItems(0, items);
            var use = ItemWorlds.LookPlusX();
            use.UseActive = true;
            world.Step(use);
            TestWorlds.Run(world, ItemWorlds.LookPlusX(), 60 * 10);
            return world.ActiveCharge;
        }

        Assert.Equal(ChargeAfterTenSeconds("conch_horn") * 1.35f, ChargeAfterTenSeconds("conch_horn", "brain_coral"), 0);
    }

    [Fact]
    public void GlowJelliesAddAShareOfTheRecharge()
    {
        var world = WithItems(0, "conch_horn"); // 30 s
        var use = ItemWorlds.LookPlusX();
        use.UseActive = true;
        world.Step(use);
        world.Pickups.Add(new Pickup { Id = 9001, Kind = OctoShoots.Core.Loot.PickupKind.GlowJelly, Position = world.Player.Position, PrevPosition = world.Player.Position });
        world.Step(ItemWorlds.LookPlusX());
        Assert.InRange(world.ActiveCharge, 9f, 9.1f);
    }

    [Fact]
    public void BubbleShieldBlocksHits()
    {
        var world = WithItems(0, "bubble_shield");
        var use = ItemWorlds.LookPlusX();
        use.UseActive = true;
        world.Step(use);
        world.Projectiles.Add(new Projectile
        {
            Id = 999,
            Kind = ProjectileKind.Orb,
            Position = world.Player.Position + new Vector3(0.4f, 0f, 0f),
            Velocity = new Vector3(-6f, 0f, 0f),
            Radius = 0.3f,
            Damage = 10f,
            MaxRange = 20f,
        });
        bool blocked = ItemWorlds.RunUntil(world, w => Saw(w, SimEventType.ShieldBlocked), 10);
        Assert.True(blocked);
        Assert.Equal(world.Loadout.Stats.MaxHp, world.Player.Hp);
    }

    [Fact]
    public void WhaleSongHeals()
    {
        var world = WithItems(0, "whale_song");
        world.Player.Hp = 40f;
        var use = ItemWorlds.LookPlusX();
        use.UseActive = true;
        world.Step(use);
        Assert.Equal(75f, world.Player.Hp, 1);
    }

    [Fact]
    public void GlowBurstTriplesTheVolley()
    {
        var world = WithItems(0, "glow_burst");
        Assert.Single(ItemWorlds.FireOnce(world));
        TestWorlds.Run(world, ItemWorlds.LookPlusX(), 30); // let the fire cooldown pass

        var use = ItemWorlds.LookPlusX(fire: true);
        use.UseActive = true;
        int before = world.Projectiles.Count;
        world.Step(use);
        Assert.Equal(3, world.Projectiles.Count - before);
    }

    [Fact]
    public void ItemRunsAreDeterministic()
    {
        ulong Run()
        {
            var world = ItemWorlds.Create(3, tweak: t =>
            {
                t.EnemySpeed = 2.6f;
                t.EnemyAttackRange = 15f;
                t.EnemyHp = 18f;
            });
            foreach (var id in new[] { "frost_kelp", "fire_coral", "mitosis", "electric_eel_tail", "mirror_scale", "anglerfish_lure" }) world.GiveItem(id);
            for (int i = 0; i < 60 * 10; i++)
            {
                var input = TestWorlds.Look(System.MathF.Sin(i * 0.02f) * 120f, System.MathF.Sin(i * 0.013f) * 30f);
                input.Forward = System.MathF.Sin(i * 0.05f);
                input.Fire = true;
                world.Step(input);
            }
            return world.StateHash();
        }

        Assert.Equal(Run(), Run());
    }
}
