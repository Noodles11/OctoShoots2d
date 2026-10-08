using OctoShoots.Core.Run;

namespace OctoShoots.Game.Title;

public enum LaunchMode { New, Seeded, Continue }

/// <summary>What the title screen asks the game scene to start: a new run, a seeded one, or the saved run.</summary>
public static class RunLaunch
{
    public const string GameScene = "res://src/Game/TopDown/TopDownMain.tscn";
    public const string TitleScene = "res://src/Game/Title/TitleMain.tscn";

    /// <summary>Set by the title before it opens the game scene; the game reads and clears it.</summary>
    public static LaunchMode? Pending { get; set; }
    public static SeedCode Seed { get; set; }
}
