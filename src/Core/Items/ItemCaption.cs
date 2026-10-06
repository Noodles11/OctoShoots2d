using System.Collections.Generic;
using System.Globalization;

namespace OctoShoots.Core.Items;

/// <summary>
/// One line per real effect, with numbers (2D §29): "+0.3 damage", "×1.5 damage",
/// "12% chance to freeze foes for 1.6s", "Recharges in 30s".
/// </summary>
public static class ItemCaption
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static List<string> Describe(EffectBundle bundle)
    {
        var lines = new List<string>();
        foreach (var m in bundle.Stats) lines.Add(StatLine(m));
        if (bundle.Shot is { } s) ShotLines(s, lines);
        foreach (var e in bundle.Effects) lines.Add(EffectLine(e));
        foreach (var f in bundle.Flags) lines.Add(FlagLine(f));
        if (bundle is ItemDef { Active: { } a }) lines.Add(ActiveLine(a));
        return lines;
    }

    static string N(float v) => v.ToString("0.##", Inv);

    static string Pct(float v) => (v * 100f).ToString("0", Inv) + "%";

    static string StatName(Stat s) => s switch
    {
        Stat.MaxHp => "max HP",
        Stat.Damage => "damage",
        Stat.FireRate => "fire rate",
        Stat.ShotSpeed => "shot speed",
        Stat.Range => "range",
        Stat.Speed => "speed",
        Stat.Luck => "luck",
        Stat.ShotSize => "shot size",
        Stat.Sink => "idle sink",
        Stat.DashCooldown => "dash cooldown",
        Stat.Glow => "glow radius",
        Stat.EnemyShotSpeed => "enemy shot speed",
        Stat.Knockback => "knockback",
        Stat.Regen => "HP regeneration per second",
        Stat.BubbleCapacity => "bubbles on the tentacle",
        Stat.BubbleRegrow => "bubble regrowth speed",
        Stat.BubblesPerThrow => "bubbles per throw",
        Stat.ActiveRecharge => "active item recharge speed",
        _ => s.ToString(),
    };

    static string StatLine(StatMod m)
    {
        if (m.Op == StatOp.Mult)
        {
            if (m.Stat == Stat.Sink && m.Value == 0f) return "No idle sinking";
            return $"×{N(m.Value)} {StatName(m.Stat)}";
        }
        return $"{(m.Value >= 0 ? "+" : "−")}{N(System.MathF.Abs(m.Value))} {StatName(m.Stat)}";
    }

    static void ShotLines(ShotSpec s, List<string> lines)
    {
        if (s.Multishot > 1)
        {
            lines.Add(s.Pattern switch
            {
                ShotPattern.Cone => $"Fires a {s.Multishot}-pellet cone",
                ShotPattern.Kraken8 => "Fires in 8 directions",
                _ => $"Fires {s.Multishot} shots",
            });
        }
        else if (s.Pattern == ShotPattern.Kraken8)
        {
            lines.Add("Fires in 8 directions");
        }
        if (s.Homing) lines.Add("Shots home onto foes");
        if (s.Pierce) lines.Add("Shots pierce foes");
        if (s.Spectral) lines.Add("Shots pass through rock");
        if (s.Bounces > 0) lines.Add($"Shots bounce off rock {s.Bounces}×");
        if (s.Split) lines.Add("Shots split in two on hit");
        if (s.Boomerang) lines.Add("Shots return to you");
        if (s.Wave) lines.Add("Shots twist in a double helix");
        if (s.Spiral) lines.Add("Shots spiral outward");
        if (s.Grow) lines.Add("Shots grow with distance");
        if (s.Explosive) lines.Add("Shots explode");
        if (s.Charge) lines.Add("Hold to charge a pearl (up to ×3 damage)");
        if (s.Laser) lines.Add("Hold to charge a laser beam");
        if (s.Rear) lines.Add("Also fires backwards");
        if (s.ChainTargets > 0) lines.Add($"Hits chain lightning to {s.ChainTargets} foes ({Pct(s.ChainDamage)} damage)");
        if (s.FreezeChance > 0) lines.Add($"{Pct(s.FreezeChance)} chance to freeze foes for {N(StatusRules.FreezeTime)}s");
        if (s.BurnChance > 0) lines.Add($"{Pct(s.BurnChance)} chance to burn foes for {N(StatusRules.BurnTime)}s");
        if (s.PoisonChance > 0) lines.Add($"{Pct(s.PoisonChance)} chance to poison foes for {N(StatusRules.PoisonTime)}s");
        if (s.CharmChance > 0) lines.Add($"{Pct(s.CharmChance)} chance to charm foes for {N(StatusRules.CharmTime)}s");
        if (s.SlowChance > 0) lines.Add($"{Pct(s.SlowChance)} chance to slow foes for {N(StatusRules.SlowTime)}s");
        if (s.CritChance > 0) lines.Add($"{Pct(s.CritChance)} chance of a ×{N(s.CritMult)} critical hit");
        if (s.SizeMult != 1f) lines.Add($"×{N(s.SizeMult)} shot size");
        if (s.DamageMult != 1f) lines.Add($"×{N(s.DamageMult)} shot damage");
    }

    static string EffectLine(TriggerEffect e)
    {
        string chance = e.Chance < 1f ? $"{Pct(e.Chance)} chance: " : "";
        string when = e.Trigger switch
        {
            Trigger.OnPickup => "",
            Trigger.OnKill => "Kills ",
            Trigger.OnHit => "Hits ",
            Trigger.OnShoot => "Shots ",
            Trigger.OnDamaged => "When hurt, ",

            Trigger.OnFloorStart => "Each new reef ",
            _ => "",
        };
        string what = e.Action switch
        {
            EffectAction.Heal => e.Trigger == Trigger.OnPickup ? $"Heals {N(e.Value)} HP" : $"heal {N(e.Value)} HP",
            EffectAction.Foam => e.Trigger == Trigger.OnPickup ? $"+{N(e.Value)} foam HP" : $"gives {N(e.Value)} foam HP",
            EffectAction.Coins => e.Trigger == Trigger.OnPickup ? $"+{N(e.Value)} sand dollars" : $"drop {N(e.Value)} sand dollars",
            EffectAction.Bombs => e.Trigger == Trigger.OnPickup ? $"+{N(e.Value)} ink bombs" : $"give {N(e.Value)} ink bombs",
            EffectAction.Frenzy => $"send you into a frenzy: ×{N(e.Value)} fire rate for {N(e.Duration)}s",
            EffectAction.Shards => $"burst into {N(e.Value)} spines",

            _ => e.Action.ToString(),
        };
        return chance + when + what;
    }

    static string FlagLine(string flag) => flag switch
    {
        "magnet" => "Pulls pickups toward you",
        "landmarks" => "Shows landmarks on the map",
        "inkTrail" => "Leaves an ink trail",
        "colorCycle" => "Shots cycle through neon colours",
        "richDrops" => "Foes drop more sand dollars",
        _ => flag,
    };

    static string ActiveLine(ActiveSpec a) => $"Recharges in {N(a.Recharge)}s";
}
