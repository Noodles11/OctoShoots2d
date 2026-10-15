using System;
using System.Collections.Generic;
using System.Globalization;
using OctoShoots.Core.Items;

namespace OctoShoots.Core.Plane;

/// <summary>
/// Her stats as the pause menu shows them: what she actually has now (in the plane's own units: damage per shot,
/// shots a second, metres, seconds), and how far each is from her bare base (no pearls). Read from the same loadout
/// and tuning the sim uses, so the sheet is exactly what plays.
/// </summary>
public static class PlaneStatSheet
{
    /// <summary>One stat: its name, its value (formatted), its change from her base (0: none), and whether lower is better.</summary>
    public readonly record struct Line(string Name, string Value, float Change, bool LowerIsBetter = false);

    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static List<Line> Of(PlaneRun run)
    {
        var t = run.Tuning;
        var stats = run.Loadout.Stats;
        var shot = run.Loadout.Shot;
        var bare = StatBlock.Base(t);
        bare.Apply(Array.Empty<StatMod>());
        float Share(float now, float was) => was > 0f ? now / was - 1f : 0f;

        float damage = PlaneCombatTuning.ShotDamage * shot.DamageMult * stats.Damage / MathF.Max(t.Damage, 0.01f);
        float rate = stats.FireRate / MathF.Max(t.FireRate, 0.01f) / PlaneCombatTuning.ShotInterval;
        float baseRate = 1f / PlaneCombatTuning.ShotInterval;
        int shots = Math.Max(1, shot.Multishot);
        float speed = PlaneCombatTuning.ShotSpeed * stats.ShotSpeed / MathF.Max(t.ShotSpeed, 0.01f);
        float range = PlaneCombatTuning.BubbleRange * stats.Range / MathF.Max(t.Range, 0.01f);
        float size = shot.SizeMult * stats[Stat.ShotSize];
        float swim = t.PlaneCruiseSpeed * stats.Speed;
        float dash = t.DashCooldown * stats[Stat.DashCooldown];
        bool minigun = run.Loadout.Flags.Contains("minigun");

        var lines = new List<Line>
        {
            new("Max HP", F(run.MaxHp, "0"), Share(run.MaxHp, bare.MaxHp)),
            new("Damage per shot", F(damage, "0.#"), Share(damage, PlaneCombatTuning.ShotDamage)),
            new("Shots per throw", shots.ToString(Inv), shots - 1f),
            new("Fire rate", F(rate, "0.#") + "/s" + (minigun ? $" (×{F(PlaneCombatTuning.MinigunMax, "0")} spun up)" : ""), Share(rate, baseRate)),
            new("Shot speed", F(speed, "0") + " m/s", Share(speed, PlaneCombatTuning.ShotSpeed)),
            new("Range", F(range, "0.#") + " m", Share(range, PlaneCombatTuning.BubbleRange)),
            new("Shot size", "×" + F(size, "0.##"), size - 1f),
            new("Swim speed", F(swim, "0.#") + " m/s", Share(swim, t.PlaneCruiseSpeed)),
            new("Dash cooldown", F(dash, "0.##") + " s", Share(dash, t.DashCooldown), LowerIsBetter: true),
            new("Knockback", "×" + F(stats[Stat.Knockback], "0.##"), stats[Stat.Knockback] - 1f),
            new("Glow", "×" + F(stats[Stat.Glow], "0.##"), stats[Stat.Glow] - 1f),
            new("Luck", F(stats.Luck, "0.#"), stats.Luck - bare.Luck),
        };
        if (stats[Stat.Regen] > 0f) lines.Add(new("Mending", F(stats[Stat.Regen], "0.##") + " HP/s", stats[Stat.Regen]));
        if (shot.CritChance > 0f) lines.Add(new("Critical hits", $"{F(shot.CritChance * 100f, "0")}% for ×{F(shot.CritMult, "0.#")}", shot.CritChance));
        if (run.Active is not null) lines.Add(new("Active recharge", "×" + F(stats[Stat.ActiveRecharge], "0.##"), stats[Stat.ActiveRecharge] - 1f));
        return lines;
    }

    static string F(float v, string format) => v.ToString(format, Inv);
}
