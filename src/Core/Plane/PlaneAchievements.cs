using System.Collections.Generic;
using OctoShoots.Core.Saves;

namespace OctoShoots.Core.Plane;

/// <summary>
/// Earns the plane's achievements (data/achievements.json, DESIGN-TOPDOWN §8) from the world's events and a level's
/// totals, into the profile. Each is earned once per profile; a run on a custom seed earns none (Profile.Award).
/// Deterministic: the same events earn the same achievements.
/// </summary>
public sealed class PlaneAchievements
{
    /// <summary>Bubble Bath: bubbles in one volley. Shucked in Fifteen: seconds from the boss landing to her freeing. Hermit Hoarder: shells on one level.</summary>
    public const int BathBubbles = 10, HoarderShells = 100;
    public const float ShuckSeconds = 15f;

    public PlaneAchievements(Profile profile, bool customSeed)
    {
        Profile = profile;
        CustomSeed = customSeed;
    }

    public Profile Profile { get; }
    public bool CustomSeed { get; }

    readonly List<string> _earned = new();
    long _landedTick = -1;

    /// <summary>A new level: the boss clock starts afresh.</summary>
    public void EnterLevel() => _landedTick = -1;

    /// <summary>Reads one step's events; returns the achievements newly earned in it (usually none).</summary>
    public IReadOnlyList<string> Observe(PlaneWorld world)
    {
        _earned.Clear();
        foreach (var e in world.Events)
        {
            switch (e.Type)
            {
                case PlaneEventType.FullBubbleFreed:
                    Award(Achievements.BigBubbleEnergy);
                    break;
                case PlaneEventType.Shot when e.Size >= BathBubbles:
                    Award(Achievements.BubbleBath);
                    break;
                case PlaneEventType.BossLanded:
                    _landedTick = world.Tick;
                    break;
                case PlaneEventType.BossDefeated when _landedTick >= 0 && (world.Tick - _landedTick) * PlaneWorld.Dt <= ShuckSeconds:
                    Award(Achievements.ShuckedInFifteen);
                    break;
            }
        }
        if (world.Stats.ShellsCollected >= HoarderShells) Award(Achievements.HermitHoarder);
        return _earned;
    }

    /// <summary>She dived on from a level: Untouchable if nothing hurt her on it.</summary>
    public IReadOnlyList<string> LevelCleared(PlaneWorld world)
    {
        _earned.Clear();
        if (world.Stats.DamageTaken <= 0f) Award(Achievements.Untouchable);
        return _earned;
    }

    void Award(string id)
    {
        if (Profile.Award(id, CustomSeed)) _earned.Add(id);
    }
}
