using System;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Sim;
using Xunit;

namespace OctoShoots.Core.Tests;

public class MovementTests
{
    [Fact]
    public void ReachesSwimSpeedInAboutAccelTime()
    {
        var world = TestWorlds.Create(t => t.JetDuration = 0f);
        TestWorlds.PlaceInChamber(world);
        var input = TestWorlds.Look(-90f);
        input.Forward = 1f;

        int ticks = (int)MathF.Ceiling(world.Tuning.AccelTime / World.Dt);
        TestWorlds.Run(world, input, ticks - 1);
        Assert.True(world.Player.Velocity.Length() < world.Tuning.SwimSpeed);
        TestWorlds.Run(world, input, 2);
        Assert.Equal(world.Tuning.SwimSpeed, world.Player.Velocity.Length(), 2);
    }

    [Fact]
    public void JetStartBurstsThenSettles()
    {
        var world = TestWorlds.Create();
        TestWorlds.PlaceInChamber(world);
        var input = TestWorlds.Look(-90f);
        input.Forward = 1f;

        world.Step(input);
        Assert.Contains(world.Events, e => e.Type == SimEventType.JetStarted);

        float peak = 0f;
        for (int i = 0; i < 20; i++)
        {
            world.Step(input);
            peak = MathF.Max(peak, world.Player.Velocity.Length());
        }
        Assert.True(peak > world.Tuning.SwimSpeed * 1.4f, $"peak {peak}");

        TestWorlds.Run(world, input, 30);
        Assert.Equal(world.Tuning.SwimSpeed, world.Player.Velocity.Length(), 1);
    }

    [Fact]
    public void ReleasingMidJetStopsQuickly()
    {
        var world = TestWorlds.Create();
        TestWorlds.PlaceInChamber(world);
        var input = TestWorlds.Look(-90f);
        input.Forward = 1f;
        TestWorlds.Run(world, input, 8); // inside the jet burst
        Assert.True(world.Player.JetTimer > 0f);

        input.Forward = 0f;
        var start = world.Player.Position;
        TestWorlds.Run(world, input, 30);
        Assert.True(world.Player.Velocity.Length() < 0.2f, $"still moving at {world.Player.Velocity.Length()}");
        Assert.True(Vector3.Distance(start, world.Player.Position) < 0.8f, $"slid {Vector3.Distance(start, world.Player.Position)} m");
    }

    [Fact]
    public void SharpTurnTriggersAnotherJet()
    {
        var world = TestWorlds.Create();
        TestWorlds.PlaceInChamber(world);
        var input = TestWorlds.Look(-90f);
        input.Forward = 1f;
        TestWorlds.Run(world, input, 40);

        input.Forward = -1f;
        world.Step(input);
        Assert.Contains(world.Events, e => e.Type == SimEventType.JetStarted);
    }

    [Fact]
    public void IdleSinksSlowly()
    {
        var world = TestWorlds.Create();
        TestWorlds.PlaceInChamber(world);
        TestWorlds.Run(world, TestWorlds.Look(0f), 120);
        Assert.Equal(-world.Tuning.SinkSpeed, world.Player.Velocity.Y, 3);
        Assert.Equal(0f, world.Player.Velocity.X, 3);
    }

    [Fact]
    public void SinkingSettlesOnTheFloor()
    {
        var world = TestWorlds.Create(t => t.SinkSpeed = 1f);
        TestWorlds.PlaceInChamber(world);
        TestWorlds.Run(world, TestWorlds.Look(0f), 60 * 12);
        Assert.True(world.Player.OnFloor);
        Assert.True(world.Player.Position.Y < 10f);
    }

    [Fact]
    public void DashIsFastInvulnerableAndHasACooldown()
    {
        var world = TestWorlds.Create();
        TestWorlds.PlaceInChamber(world);
        var input = TestWorlds.Look(-90f);
        input.Dash = true;
        world.Step(input);

        var t = world.Tuning;
        Assert.Contains(world.Events, e => e.Type == SimEventType.DashStarted);
        Assert.Equal(t.SwimSpeed * t.DashMultiplier, world.Player.Velocity.Length(), 1);
        Assert.True(world.Player.DashInvulnerableTimer > 0f);
        Assert.Single(world.Clouds);

        world.Step(input);
        Assert.DoesNotContain(world.Events, e => e.Type == SimEventType.DashStarted);

        input.Dash = false;
        TestWorlds.Run(world, input, (int)(t.DashCooldown / World.Dt));
        input.Dash = true;
        world.Step(input);
        Assert.Contains(world.Events, e => e.Type == SimEventType.DashStarted);
    }

    [Fact]
    public void NeverPassesThroughRock()
    {
        var world = TestWorlds.Create();
        float r = world.Tuning.PlayerRadius;
        var input = TestWorlds.Look(30f, -20f);
        input.Forward = 1f;
        for (int i = 0; i < 60 * 10; i++)
        {
            input.Dash = i % 50 == 0;
            input.Yaw += 0.01f;
            world.Step(input);
            Assert.True(world.Sdf.Sample(world.Player.Position) > r - 0.05f, $"tick {i}: inside rock at {world.Player.Position}");
        }
    }

    [Fact]
    public void PitchIsClamped()
    {
        var world = TestWorlds.Create();
        world.Step(TestWorlds.Look(0f, 120f));
        Assert.Equal(world.Tuning.PitchLimit * MathUtil.Deg2Rad, world.Player.Pitch, 4);
    }
}
