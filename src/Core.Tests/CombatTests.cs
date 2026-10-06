using System;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Sim;
using Xunit;

namespace OctoShoots.Core.Tests;

public class CombatTests
{
    /// <summary>Yaw -90° looks along +X from the chamber centre: open water for well over 10 m.</summary>
    const float LookPlusX = -90f;

    [Fact]
    public void ShotDissolvesAtRange()
    {
        var world = TestWorlds.Create();
        TestWorlds.PlaceInChamber(world);
        var input = TestWorlds.Look(LookPlusX);
        input.Fire = true;
        world.Step(input);
        var fired = world.Events.Single(e => e.Type == SimEventType.ShotFired);

        input.Fire = false;
        for (int i = 0; i < 120; i++)
        {
            world.Step(input);
            var expired = world.Events.FirstOrDefault(e => e.Type == SimEventType.ShotExpired);
            if (expired.Type != SimEventType.ShotExpired) continue;
            float flown = Vector3.Distance(fired.Position, expired.Position);
            Assert.InRange(flown, world.Tuning.RangeMetres - 0.3f, world.Tuning.RangeMetres + 0.3f);
            return;
        }
        Assert.Fail("shot never expired");
    }

    [Fact]
    public void HoldingFireMatchesFireRate()
    {
        var world = TestWorlds.Create();
        TestWorlds.PlaceInChamber(world);
        var input = TestWorlds.Look(LookPlusX);
        input.Fire = true;
        int shots = 0;
        for (int i = 0; i < 60 * 10; i++)
        {
            world.Step(input);
            shots += world.Events.Count(e => e.Type == SimEventType.ShotFired);
        }
        Assert.InRange(shots, 26, 28);
    }

    [Theory]
    [InlineData(AimAssistLevel.Medium, 3f, true)]
    [InlineData(AimAssistLevel.Medium, 9f, false)]
    [InlineData(AimAssistLevel.Off, 3f, false)]
    [InlineData(AimAssistLevel.High, 6f, true)]
    [InlineData(AimAssistLevel.High, 8f, false)]
    public void MagnetismSnapsOnlyInsideTheCone(AimAssistLevel level, float offsetDeg, bool expectSnap)
    {
        var world = TestWorlds.Create(t =>
        {
            t.EnemyCount = 1;
            t.AimAssist = level;
        });
        TestWorlds.PlaceInChamber(world);
        var enemy = world.Enemies[0];
        // 6 m ahead, offset horizontally; the cone is measured to the body centre.
        float a = offsetDeg * MathUtil.Deg2Rad;
        enemy.Position = world.Player.Position + new Vector3(MathF.Cos(a), 0f, MathF.Sin(a)) * 6f;

        var input = TestWorlds.Look(LookPlusX);
        input.Fire = true;
        world.Step(input);
        var fired = world.Events.Single(e => e.Type == SimEventType.ShotFired);

        float toEnemy = MathUtil.AngleBetween(fired.Direction, enemy.PrevPosition - fired.Position) * MathUtil.Rad2Deg;
        if (expectSnap) Assert.True(toEnemy < 0.5f, $"expected snap, angle {toEnemy}");
        else Assert.True(toEnemy > 2f, $"expected no snap, angle {toEnemy}");
    }

    [Fact]
    public void ShotsDamageAndKillEnemies()
    {
        var world = TestWorlds.Create(t =>
        {
            t.EnemyCount = 1;
            t.EnemySpeed = 0.01f;
            t.EnemyAttackRange = 0f;
        });
        TestWorlds.PlaceInChamber(world);
        world.Enemies[0].Position = world.Player.Position + new Vector3(5f, 0f, 0f);

        var input = TestWorlds.Look(LookPlusX);
        input.Fire = true;
        bool died = false;
        for (int i = 0; i < 60 * 5 && !died; i++)
        {
            world.Step(input);
            died = world.Events.Any(e => e.Type == SimEventType.EnemyDied);
        }
        Assert.True(died);
        Assert.Equal(EnemyState.Dead, world.Enemies[0].State);
    }

    [Fact]
    public void AtMostThreeOffscreenAttackersAtOnce()
    {
        var world = TestWorlds.Create(t =>
        {
            t.EnemyCount = 6;
            t.EnemySpeed = 0.01f;
        });
        TestWorlds.PlaceInChamber(world);
        var behind = new[]
        {
            new Vector3(19f, 13f, 18f), new Vector3(19f, 13f, 20f), new Vector3(19f, 13f, 22f),
            new Vector3(19f, 15.5f, 19f), new Vector3(19f, 15.5f, 21f), new Vector3(19f, 15.5f, 23f),
        };
        for (int i = 0; i < 6; i++)
        {
            var e = world.Enemies[i];
            e.Position = behind[i];
            e.State = EnemyState.Hunt;
            e.AttackCooldown = 0f;
        }

        var input = TestWorlds.Look(LookPlusX);
        int maxTelegraphing = 0;
        for (int i = 0; i < 60 * 4; i++)
        {
            world.Step(input);
            maxTelegraphing = Math.Max(maxTelegraphing, world.Enemies.Count(e => e.State == EnemyState.Telegraph));
        }
        Assert.Equal(world.Tuning.MaxOffscreenAttackers, maxTelegraphing);
    }

    [Fact]
    public void TelegraphLastsBeforeTheShot()
    {
        var world = TestWorlds.Create(t =>
        {
            t.EnemyCount = 1;
            t.EnemySpeed = 0.01f;
        });
        TestWorlds.PlaceInChamber(world);
        var e = world.Enemies[0];
        e.Position = world.Player.Position + new Vector3(7f, 0f, 0f);
        e.State = EnemyState.Hunt;
        e.AttackCooldown = 0f;

        var input = TestWorlds.Look(LookPlusX);
        long telegraphTick = -1, shotTick = -1;
        for (int i = 0; i < 120 && shotTick < 0; i++)
        {
            world.Step(input);
            if (world.Events.Any(ev => ev.Type == SimEventType.EnemyTelegraph)) telegraphTick = world.Tick;
            if (world.Events.Any(ev => ev.Type == SimEventType.EnemyShotFired)) shotTick = world.Tick;
        }
        Assert.True(telegraphTick > 0 && shotTick > 0);
        Assert.True((shotTick - telegraphTick) * World.Dt >= 0.45f - World.Dt);
    }

    [Fact]
    public void DashPassesThroughEnemyShots()
    {
        var world = TestWorlds.Create();
        TestWorlds.PlaceInChamber(world);
        world.Player.DashInvulnerableTimer = 1f;
        world.Projectiles.Add(new Projectile
        {
            Id = 999,
            Position = world.Player.Position + new Vector3(0.5f, 0f, 0f),
            Velocity = new Vector3(-6f, 0f, 0f),
            Radius = 0.3f,
            Damage = 10f,
            MaxRange = 20f,
        });
        TestWorlds.Run(world, TestWorlds.Look(LookPlusX), 10);
        Assert.Equal(world.Tuning.MaxHp, world.Player.Hp);
    }

    [Fact]
    public void SimulationIsDeterministic()
    {
        ulong RunScripted()
        {
            var world = TestWorlds.Create(t => t.EnemyCount = 3);
            for (int i = 0; i < 60 * 15; i++)
            {
                var input = TestWorlds.Look(MathF.Sin(i * 0.02f) * 120f, MathF.Sin(i * 0.013f) * 30f);
                input.Forward = MathF.Sin(i * 0.05f);
                input.Strafe = MathF.Cos(i * 0.031f);
                input.Vertical = i % 200 < 100 ? 0.5f : -0.5f;
                input.Fire = i % 90 < 60;
                input.Dash = i % 140 == 0;
                world.Step(input);
            }
            return world.StateHash();
        }

        Assert.Equal(RunScripted(), RunScripted());
    }

    [Fact]
    public void TuningRoundTripsThroughJson()
    {
        var t = new Tuning { SwimSpeed = 6.25f, AimAssist = AimAssistLevel.High, EnemyCount = 5, Invincible = true };
        var back = Tuning.FromJson(t.ToJson());
        Assert.Equal(6.25f, back.SwimSpeed);
        Assert.Equal(AimAssistLevel.High, back.AimAssist);
        Assert.Equal(5, back.EnemyCount);
        Assert.True(back.Invincible);
    }
}
