using System;

namespace OctoShoots.Core.Items;

/// <summary>
/// What her luck does (docs/PEARLS.md, "Luck"), per point, each capped: an extra clam room on a level,
/// a bonus shell on a pickup, a light spring and more healing in it. Luck rolls never draw from the level's own streams, so a run without luck
/// gets the very same levels.
/// </summary>
public static class LuckTuning
{
    /// <summary>A level with no clam room gets one with this chance per point (at most ClamMax).</summary>
    public const float Clam = 0.1f, ClamMax = 0.6f;
    /// <summary>Each shell she picks up counts twice with this chance per point (at most ShellMax).</summary>
    public const float Shell = 0.08f, ShellMax = 0.6f;
    /// <summary>A light spring: this much likelier per point (at most SpringMax); on an adventure level, only by luck.</summary>
    public const float Spring = 0.1f, SpringMax = 0.6f;
    /// <summary>A light spring holds this much more healing per point (a share of SpringTuning.Pool, at most PoolMax).</summary>
    public const float Pool = 0.15f, PoolMax = 1f;

    public static float ClamChance(float luck) => Math.Clamp(Clam * luck, 0f, ClamMax);
    public static float ShellChance(float luck) => Math.Clamp(Shell * luck, 0f, ShellMax);
    public static float SpringBonus(float luck) => Math.Clamp(Spring * luck, 0f, SpringMax);
    public static float SpringPool(float luck) => Math.Clamp(Pool * luck, 0f, PoolMax);
}
