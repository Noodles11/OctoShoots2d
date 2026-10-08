using System.Collections.Generic;

namespace OctoShoots.Core.Saves;

/// <summary>The whole save: one profile plus at most one suspended run (2D §13.1).</summary>
public sealed class SaveFile
{
    public int Version { get; set; } = SaveCodec.CurrentVersion;
    public Profile Profile { get; set; } = new();
    public SuspendedRun? Run { get; set; }
}

/// <summary>Meta progress: achievements (which unlock pearls), the Sea-pedia's records and the statistics.</summary>
public sealed class Profile
{
    public HashSet<string> Achievements { get; set; } = new();
    /// <summary>Pearls she has seen offered (in a room or a shop) or taken.</summary>
    public HashSet<string> SeenItems { get; set; } = new();
    /// <summary>Creatures and bosses she has met.</summary>
    public HashSet<string> SeenCreatures { get; set; } = new();
    public HashSet<string> SeenBosses { get; set; } = new();
    public ProfileStats Stats { get; set; } = new();
    /// <summary>Per pearl id: how it has done.</summary>
    public Dictionary<string, PearlRecord> Pearls { get; set; } = new();
    /// <summary>Per creature or boss id: how it has done against her.</summary>
    public Dictionary<string, CreatureRecord> Creatures { get; set; } = new();
    /// <summary>The last few seeds she played, newest first.</summary>
    public List<string> RecentSeeds { get; set; } = new();

    /// <summary>Grants an achievement unless the run used a custom seed (Isaac rule). Returns true when new.</summary>
    public bool Award(string achievement, bool customSeed) => !customSeed && Achievements.Add(achievement);

    public PearlRecord Pearl(string id) => Pearls.TryGetValue(id, out var r) ? r : Pearls[id] = new PearlRecord();

    public CreatureRecord Creature(string id) => Creatures.TryGetValue(id, out var r) ? r : Creatures[id] = new CreatureRecord();
}

/// <summary>Totals and records over every run (seeded runs included; SeededRuns counts them).</summary>
public sealed class ProfileStats
{
    public int Runs { get; set; }
    public int SeededRuns { get; set; }
    public int Wins { get; set; }
    public int Deaths { get; set; }
    public int Abandoned { get; set; }
    public int Kills { get; set; }
    public int RoomsCleared { get; set; }
    public int BossesFreed { get; set; }
    public long BubblesThrown { get; set; }
    public int FullBubbles { get; set; }
    public int BiggestVolley { get; set; }
    public double DamageDealt { get; set; }
    public double DamageTaken { get; set; }
    public long ShellsCollected { get; set; }
    public long ShellsSpent { get; set; }
    public int PearlsAbsorbed { get; set; }
    public int UntouchableRooms { get; set; }
    public double PlaySeconds { get; set; }
    public double LongestRunSeconds { get; set; }
    /// <summary>0 until a room has been cleared.</summary>
    public double FastestRoomSeconds { get; set; }
    public int MostShellsInRoom { get; set; }
    /// <summary>The furthest she has got: depth, then room within it.</summary>
    public int BestDepth { get; set; }
    public int BestRoom { get; set; }
    /// <summary>What ended each run, by source id (see the sim's damage sources).</summary>
    public Dictionary<string, int> DeathsByCause { get; set; } = new();
}

public sealed class PearlRecord
{
    public int Absorbed { get; set; }
    /// <summary>Runs in which she held it at some point.</summary>
    public int Runs { get; set; }
    /// <summary>Runs that ended in her death while she held it.</summary>
    public int RunsLost { get; set; }
    public int RoomsCleared { get; set; }
}

public sealed class CreatureRecord
{
    /// <summary>Times one noticed her (bosses: times she entered the arena).</summary>
    public int Encounters { get; set; }
    /// <summary>Times she beat one (bosses: times freed).</summary>
    public int Defeated { get; set; }
    /// <summary>Runs one of them ended.</summary>
    public int DefeatedYou { get; set; }
    /// <summary>Bosses: the fastest time from landing to freed (0 until freed once).</summary>
    public double BestSeconds { get; set; }
}

/// <summary>
/// A run saved at the start of a room (2D §13.1): continuing regenerates that room from the seed and puts her back in
/// it with what she carried in.
/// </summary>
public sealed class SuspendedRun
{
    public string Seed { get; set; } = "";
    public bool CustomSeed { get; set; }
    public int Depth { get; set; } = 1;
    public int Room { get; set; } = 1;
    public List<string> Items { get; set; } = new();
    public float Hp { get; set; }
    public int Shells { get; set; }
    /// <summary>Play time of the run so far (the HUD clock).</summary>
    public double Elapsed { get; set; }
    /// <summary>The run's totals so far, for the death splash.</summary>
    public int Foes { get; set; }
    public int ShellsCollected { get; set; }
    /// <summary>When it was saved, as an ISO 8601 local time.</summary>
    public string SavedAt { get; set; } = "";
}

/// <summary>Achievement ids referenced by item unlocks.</summary>
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
