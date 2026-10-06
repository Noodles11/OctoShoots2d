using System;
using System.Numerics;
using OctoShoots.Core.Sim;
using OctoShoots.Core.Terrain;

namespace OctoShoots.Core.Tests;

static class TestWorlds
{
    public const ulong Seed = 12345;

    static readonly Lazy<Cave> SharedCave = new(() => GreyboxCave.Build(Seed));

    public static Cave Cave => SharedCave.Value;

    /// <summary>A world with no enemies unless the tweak adds some.</summary>
    public static World Create(Action<Tuning>? tweak = null)
    {
        var tuning = new Tuning { EnemyCount = 0, NestsEnabled = false, BubbleCapacity = 12, BubbleRegrowDelay = 0f, BubbleRegrowInterval = 0.05f };
        tweak?.Invoke(tuning);
        return new World(Cave.Clone(), tuning, Seed); // own copy: worlds dig craters
    }

    public static PlayerInput Look(float yawDeg, float pitchDeg = 0f) => new()
    {
        Yaw = yawDeg * MathUtil.Deg2Rad,
        Pitch = pitchDeg * MathUtil.Deg2Rad,
        ViewHalfAngleDeg = 45f,
    };

    /// <summary>Puts the player in the middle of the main chamber, at rest.</summary>
    public static void PlaceInChamber(World world)
    {
        world.Player.Position = world.Player.PrevPosition = new Vector3(24f, 14f, 22f);
        world.Player.Velocity = Vector3.Zero;
    }

    public static void Run(World world, PlayerInput input, int ticks)
    {
        for (int i = 0; i < ticks; i++) world.Step(input);
    }
}
