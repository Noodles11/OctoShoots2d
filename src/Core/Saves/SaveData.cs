using System.Collections.Generic;

namespace OctoShoots.Core.Saves;

/// <summary>The whole save: one profile plus at most one suspended run (2D §13.1).</summary>
public sealed class SaveFile
{
    public int Version { get; set; } = SaveCodec.CurrentVersion;
    public Profile Profile { get; set; } = new();
    public SuspendedRun? Run { get; set; }
}

/// <summary>Meta progress: achievements (which unlock items and depths), Sea-pedia and stats (2D §13.2).</summary>
public sealed class Profile
{
    public HashSet<string> Achievements { get; set; } = new();
    public HashSet<string> SeenItems { get; set; } = new();
    public HashSet<string> SeenCreatures { get; set; } = new();
    public HashSet<string> SeenBosses { get; set; } = new();
    public ProfileStats Stats { get; set; } = new();

    /// <summary>Grants an achievement unless the run used a custom seed (Isaac rule). Returns true when new.</summary>
    public bool Award(string achievement, bool customSeed) => !customSeed && Achievements.Add(achievement);
}

public sealed class ProfileStats
{
    public int Runs { get; set; }
    public int Wins { get; set; }
    public int Deaths { get; set; }
    public int Kills { get; set; }
    public long BestWinTicks { get; set; }
    public Dictionary<string, int> DeathsByCause { get; set; } = new();
    public Dictionary<string, int> ItemPickups { get; set; } = new();
}

/// <summary>Everything needed to continue a run where it was left (2D §13.1, §19 "Saving").</summary>
public sealed class SuspendedRun
{
    public string Seed { get; set; } = "";
    public bool CustomSeed { get; set; }
    public int Depth { get; set; } = 1;
    public int Reef { get; set; } = 1;
    public List<string> Items { get; set; } = new();
    /// <summary>Seconds of recharge already built up on the held active item.</summary>
    public float ActiveCharge { get; set; }
    public float Hp { get; set; }
    public float Foam { get; set; }
    public int Coins { get; set; }
    public int Bombs { get; set; }
    public float[] Position { get; set; } = new float[3];
    public float Yaw { get; set; }
    public float Pitch { get; set; }
    public long Ticks { get; set; }
    public int Kills { get; set; }

    /// <summary>Craters blown in the current reef: x, y, z, radius.</summary>
    public List<float[]> Craters { get; set; } = new();

    /// <summary>Loot already taken in the current reef, by loot-plan index.</summary>
    public List<int> TakenShells { get; set; } = new();
    public List<int> OpenedChests { get; set; } = new();
    public List<int> CollectedPickups { get; set; } = new();
    public List<int> DugCoins { get; set; } = new();
}

/// <summary>Achievement ids referenced by item unlocks and depth progression.</summary>
public static class Achievements
{
    public const string ThreeSynergies = "three_synergies";
    public const string Untouchable = "untouchable";
    public const string Ringmaster = "ringmaster";
    public const string Jesters = "jesters";
    public const string MotherAngler = "mother_angler";
    public const string Siphonophore = "siphonophore";
    public const string HollowMaw = "hollow_maw";
}
