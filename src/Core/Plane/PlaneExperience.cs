using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Items;

namespace OctoShoots.Core.Plane;

/// <summary>
/// Experience (DESIGN-TOPDOWN §2.6): she earns it by cleansing the corruption — a step at every tenth of a level's
/// corruption cleansed (its highest mark, never for the same ground twice), not continuously — by freeing the fish the
/// murklings turned, and by freeing bosses. Enough of it is a level, and every level a skill point.
/// </summary>
public static class ExperienceTuning
{
    /// <summary>A step every CleanseStep of the level's corruption cleansed (its highest mark so far), worth CleanseXp.</summary>
    public const float CleanseStep = 0.1f;
    public const int CleanseXp = 10;
    /// <summary>A fish freed (a lesser murkling turned back), and a boss freed.</summary>
    public const int FishXp = 6, BossXp = 120;
    /// <summary>From level 1 to 2 takes FirstLevel; every level after it Growth times the one before.</summary>
    public const int FirstLevel = 240;
    public const float Growth = 1.35f;
    /// <summary>A level-up waits until she has been out of the fight (Calm) this long.</summary>
    public const float CalmSeconds = 1.5f;
    /// <summary>Calm: no corrupted creature this close to her.</summary>
    public const float CalmDistance = 12f;

    /// <summary>The experience it takes to go from this level to the next.</summary>
    public static int Need(int level) => (int)MathF.Round(FirstLevel * MathF.Pow(Growth, Math.Max(level - 1, 0)));
}

/// <summary>
/// The modifiers a skill point can go to: each point adds one Step to the stat (before any pearl's multiplier), in the
/// sim's own units, read from the tuning so a point is the same share of her base however it is tuned.
/// </summary>
public static class Skills
{
    /// <summary>A modifier: its stat, its name, what one point adds, and how that reads ("+10", "+6%").</summary>
    public readonly record struct Skill(Stat Stat, string Name, float Step, string Gain);

    public static IReadOnlyList<Skill> All(Tuning t) => new Skill[]
    {
        new(Stat.MaxHp, "Max HP", 10f, "+10"),
        new(Stat.Damage, "Damage", t.Damage * 0.06f, "+6%"),
        new(Stat.FireRate, "Fire rate", t.FireRate * 0.05f, "+5%"),
        new(Stat.ShotSpeed, "Shot speed", t.ShotSpeed * 0.06f, "+6%"),
        new(Stat.Range, "Range", t.Range * 0.05f, "+5%"),
        new(Stat.ShotSize, "Shot size", 0.06f, "+6%"),
        new(Stat.Speed, "Swim speed", 0.04f, "+4%"),
        new(Stat.DashCooldown, "Dash cooldown", -0.05f, "−5%"),
        new(Stat.Glow, "Glow", 0.08f, "+8%"),
    };

    /// <summary>The stat changes the points add up to.</summary>
    public static IEnumerable<StatMod> Mods(IReadOnlyDictionary<Stat, int> points, Tuning t)
    {
        foreach (var skill in All(t))
            if (points.TryGetValue(skill.Stat, out int n) && n > 0)
                yield return new StatMod { Stat = skill.Stat, Op = StatOp.Add, Value = skill.Step * n };
    }
}

public sealed partial class PlaneWorld
{
    /// <summary>The level's highest cleansing mark so far (in CleanseSteps).</summary>
    int _cleanseMarks;
    float _calmFor;

    /// <summary>
    /// Out of the fight: no battle on (the sealed arena, a creature after her), no corrupted creature near, no vine
    /// coiling her and no hit taken, for CalmSeconds. A level-up waits for it.
    /// </summary>
    public bool Calm => _calmFor >= ExperienceTuning.CalmSeconds;

    /// <summary>Spends a skill point (the level-up drawer's choice); more max HP comes filled, in play as well.</summary>
    public bool SpendSkill(OctoShoots.Core.Items.Stat stat)
    {
        Run.Hp = Player.Hp;
        if (!Run.Spend(stat)) return false;
        Player.Hp = Run.Hp;
        return true;
    }

    /// <summary>The first level of a run: her first level-up waits for the next one, however much she does here.</summary>
    public bool OpeningLevel => Map.Cycle == 1 && Map.Level == 1 && Map.Depth <= Gen.TopDown.LevelId.FirstRun.Depth;

    void PlaceExperience() => _cleanseMarks = Corruption is { } field ? Marks(field.Cleansed) : 0;

    static int Marks(float cleansed) => (int)(cleansed / ExperienceTuning.CleanseStep + 1e-4f);

    /// <summary>The step's experience, from what happened in it, and whether it was calm.</summary>
    void StepExperience()
    {
        int gained = 0;
        if (Corruption is { } field)
        {
            int marks = Marks(field.Cleansed);
            if (marks > _cleanseMarks)
            {
                gained += (marks - _cleanseMarks) * ExperienceTuning.CleanseXp;
                _cleanseMarks = marks;
            }
        }
        bool hurt = false;
        foreach (var e in Events)
        {
            if (e.Type == PlaneEventType.FishFreed) gained += ExperienceTuning.FishXp;
            else if (e.Type == PlaneEventType.VentSealed) gained += AdventureTuning.VentXp;
            else if (e.Type == PlaneEventType.BossFreed) gained += ExperienceTuning.BossXp;
            else if (e.Type == PlaneEventType.PlayerHit) hurt = true;
        }
        if (gained > 0)
        {
            int levels = Run.Gain(gained, hold: OpeningLevel);
            Events.Add(new PlaneEvent(PlaneEventType.XpGained, Player.Position, Vector2.Zero, gained));
            for (int i = 0; i < levels; i++) Events.Add(new PlaneEvent(PlaneEventType.LevelUp, Player.Position, Vector2.Zero, Run.XpLevel));
        }
        bool quiet = !hurt && !InBattle && !CreatureNear();
        _calmFor = quiet ? _calmFor + Dt : 0f;
    }

    bool CreatureNear()
    {
        var at = Player.Position;
        float r = ExperienceTuning.CalmDistance;
        bool Near(Vector2 p) => Vector2.DistanceSquared(p, at) < r * r;
        return Murklings.Any(m => m.Alive && Near(m.Position)) || Blotters.Any(b => b.Alive && Near(b.Position))
            || Thumpsquirts.Any(t => t.Alive && Near(t.Position)) || Inkwardens.Any(w => w.Alive && Near(w.Position))
            || Lashjaws.Any(l => l.Alive && Near(l.Position)) || Boss is { Freed: false } && BossBattle;
    }
}
