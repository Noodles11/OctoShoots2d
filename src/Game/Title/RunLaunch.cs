using System;
using System.Threading.Tasks;
using OctoShoots.Core;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Run;
using OctoShoots.Core.Saves;
using OctoShoots.Game.Settings;
using OctoShoots.Game.TopDown;

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
    /// <summary>The first level, shaped behind the title's own loading page; the game takes it instead of making it again.</summary>
    public static (SeedCode Seed, LevelId Id, float Luck, Task<LevelShape> Shape)? Shaping { get; set; }
    /// <summary>The title's last frame of its loading screen, which the game keeps up until it is ready to show.</summary>
    public static Godot.Texture2D? Frame { get; set; }

    /// <summary>
    /// Where a new run starts: in the tutorial (Depth 0) until she has passed it once, then in Depth 1 — unless the
    /// settings ask to replay the tutorial.
    /// </summary>
    public static LevelId Start(Profile profile, ViewOptions view) =>
        profile.TutorialPassed && !view.ReplayTutorial ? LevelId.FirstRun : LevelId.First;

    /// <summary>Where a saved run takes up again.</summary>
    public static LevelId Start(SuspendedRun run) =>
        new(Math.Max(1, run.Cycle), Math.Clamp(run.Depth, 1, LevelPlan.Depths), Math.Clamp(run.Level, 1, 5));
}
