using System;
using System.Collections.Generic;
using System.Linq;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Saves;

namespace OctoShoots.Core.Plane;

/// <summary>
/// Writes a run into the profile: the statistics, each pearl's and creature's record, and what ended the run. It reads
/// the world's events after every step and is told when rooms and runs begin and end. Deterministic: the same events
/// give the same profile.
/// </summary>
public sealed class PlaneProfileRecorder
{
    /// <summary>Sea-pedia ids of the creatures on the plane today.</summary>
    public const string MobId = "pufferling", QueenClamId = "queen_clam";

    public static readonly IReadOnlyList<string> CreatureIds = new[] { MobId, QueenClamId };

    public PlaneProfileRecorder(Profile profile) => Profile = profile;

    public Profile Profile { get; }

    readonly HashSet<string> _runPearls = new();
    long _landedTick = -1;

    /// <summary>What a damage source is called in the statistics' "how runs ended".</summary>
    public static string SourceId(DamageSource source) => source switch
    {
        DamageSource.PufferNeedle => "puffer_needle",
        DamageSource.PufferSpines => "puffer_spines",
        DamageSource.BossPearl => "boss_pearl",
        DamageSource.RoyalPearl => "royal_pearl",
        DamageSource.BossSnap => "boss_snap",
        DamageSource.BossContact => "boss_contact",
        _ => "unknown",
    };

    /// <summary>The creature behind a damage source.</summary>
    public static string? CreatureOf(DamageSource source) => source switch
    {
        DamageSource.PufferNeedle or DamageSource.PufferSpines => MobId,
        DamageSource.BossPearl or DamageSource.RoyalPearl or DamageSource.BossSnap or DamageSource.BossContact => QueenClamId,
        _ => null,
    };

    /// <summary>A run begins (or a saved one continues, which is not a new run).</summary>
    public void StartRun(string seed, bool customSeed, IEnumerable<string> carried, bool continuing)
    {
        _runPearls.Clear();
        foreach (string id in carried) _runPearls.Add(id);
        if (continuing) return;
        foreach (string id in _runPearls) Profile.Pearl(id).Runs++;
        Profile.Stats.Runs++;
        if (customSeed) Profile.Stats.SeededRuns++;
        Profile.RecentSeeds.Remove(seed);
        Profile.RecentSeeds.Insert(0, seed);
        if (Profile.RecentSeeds.Count > 5) Profile.RecentSeeds.RemoveRange(5, Profile.RecentSeeds.Count - 5);
    }

    /// <summary>
    /// She is on a new level: the furthest reach (depths count on through the loops: the second pass's first depth is
    /// depth 8), and the pearls the level offers are now seen.
    /// </summary>
    public void EnterLevel(PlaneWorld world, LevelId level)
    {
        var s = Profile.Stats;
        int depth = (level.Cycle - 1) * LevelPlan.Depths + level.Depth;
        if (depth > s.BestDepth || depth == s.BestDepth && level.Level > s.BestRoom)
        {
            s.BestDepth = depth;
            s.BestRoom = level.Level;
        }
        foreach (var pearl in world.Pearls) Profile.SeenItems.Add(pearl.ItemId);
        foreach (var stand in world.Stands)
            if (stand.Kind == StandKind.Pearl) Profile.SeenItems.Add(stand.ItemId);
        _landedTick = -1;
    }

    /// <summary>Reads one step's events.</summary>
    public void Observe(PlaneWorld world)
    {
        var s = Profile.Stats;
        if (!world.Defeated) s.PlaySeconds += PlaneWorld.Dt;
        foreach (var e in world.Events)
        {
            switch (e.Type)
            {
                case PlaneEventType.Shot:
                    s.BubblesThrown += (long)e.Size;
                    s.BiggestVolley = Math.Max(s.BiggestVolley, (int)e.Size);
                    break;
                case PlaneEventType.BubbleFull:
                    s.FullBubbles++;
                    break;
                case PlaneEventType.MobNoticed:
                    Profile.SeenCreatures.Add(MobId);
                    Profile.Creature(MobId).Encounters++;
                    break;
                case PlaneEventType.MobHit:
                    s.DamageDealt += e.Size;
                    break;
                case PlaneEventType.MobDefeated:
                    s.DamageDealt += e.Size;
                    s.Kills++;
                    Profile.SeenCreatures.Add(MobId);
                    Profile.Creature(MobId).Defeated++;
                    break;
                case PlaneEventType.BossHit:
                    s.DamageDealt += e.Size;
                    break;
                case PlaneEventType.ArenaSealed:
                    Profile.SeenBosses.Add(QueenClamId);
                    Profile.Creature(QueenClamId).Encounters++;
                    break;
                case PlaneEventType.BossLanded:
                    _landedTick = world.Tick;
                    break;
                case PlaneEventType.BossDefeated when _landedTick >= 0:
                {
                    double seconds = (world.Tick - _landedTick) * PlaneWorld.Dt;
                    var record = Profile.Creature(QueenClamId);
                    record.BestSeconds = record.BestSeconds <= 0 ? seconds : Math.Min(record.BestSeconds, seconds);
                    break;
                }
                case PlaneEventType.BossFreed:
                    s.BossesFreed++;
                    Profile.Creature(QueenClamId).Defeated++;
                    break;
                case PlaneEventType.PlayerHit:
                    s.DamageTaken += e.Size;
                    break;
                case PlaneEventType.PearlCollected when world.LastPearl is { } id:
                    s.PearlsAbsorbed++;
                    Profile.SeenItems.Add(id);
                    var pearl = Profile.Pearl(id);
                    pearl.Absorbed++;
                    if (_runPearls.Add(id)) pearl.Runs++;
                    break;
            }
        }
    }

    /// <summary>She dived on: the level is cleared (the stats keep their old "room" names in the save).</summary>
    public void LevelCleared(PlaneWorld world, double seconds)
    {
        var s = Profile.Stats;
        s.RoomsCleared++;
        s.FastestRoomSeconds = s.FastestRoomSeconds <= 0 ? seconds : Math.Min(s.FastestRoomSeconds, seconds);
        s.MostShellsInRoom = Math.Max(s.MostShellsInRoom, world.Stats.ShellsCollected);
        if (world.Stats.DamageTaken <= 0f) s.UntouchableRooms++;
        foreach (string id in world.Run.Items.Distinct()) Profile.Pearl(id).RoomsCleared++;
        CloseRoom(world);
    }

    /// <summary>The run is over: she died (the world says what dealt the killing blow).</summary>
    public void RunDied(PlaneWorld world, double runSeconds)
    {
        var s = Profile.Stats;
        s.Deaths++;
        string cause = SourceId(world.LastHitSource);
        s.DeathsByCause[cause] = s.DeathsByCause.GetValueOrDefault(cause) + 1;
        if (CreatureOf(world.LastHitSource) is { } killer) Profile.Creature(killer).DefeatedYou++;
        foreach (string id in world.Run.Items.Distinct()) Profile.Pearl(id).RunsLost++;
        s.LongestRunSeconds = Math.Max(s.LongestRunSeconds, runSeconds);
        CloseRoom(world);
    }

    /// <summary>The run was abandoned from the title (its saved room is never played on).</summary>
    public void RunAbandoned(double runSeconds)
    {
        Profile.Stats.Abandoned++;
        Profile.Stats.LongestRunSeconds = Math.Max(Profile.Stats.LongestRunSeconds, runSeconds);
    }

    void CloseRoom(PlaneWorld world)
    {
        Profile.Stats.ShellsCollected += world.Stats.ShellsCollected;
        Profile.Stats.ShellsSpent += world.Stats.ShellsSpent;
    }
}
