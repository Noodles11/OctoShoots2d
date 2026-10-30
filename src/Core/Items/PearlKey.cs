using System;
using System.Collections.Generic;
using System.Linq;

namespace OctoShoots.Core.Items;

/// <summary>
/// How a pearl looks follows what it does (docs/PEARLS.md, "Reading a pearl"): every effect it has stands for a colour
/// (damage red, fire rate orange, shot speed blue, range cyan, more or bigger shots violet, health green, mobility white,
/// luck and shells gold, finding the way tan, glow pale lemon, knockback bronze, defence silver, each status its own),
/// weighted by how strong it is; the strongest is the pearl's middle colour (its light), the next ones blend in beside it.
/// What kind of effect weighs most picks the icon engraved on it: an arrow a stat up, three dots in a chevron a change to
/// her shots, a shield defence, a nautilus shell luck, shells and maps, a feather mobility, a swirl a status, a five-armed
/// star an active pearl. Only what helps her colours it (a drawback, such as a smaller shot, does not).
/// </summary>
public static class PearlKey
{
    public const string Damage = "#e63946", FireRate = "#ff9f1c", ShotSpeed = "#3a6ff7", Range = "#3fd0e8",
        MoreShots = "#8a5cf6", Health = "#52d273", Mobility = "#f4f6f8", Luck = "#ffc93c", Wayfinding = "#d4a373",
        Glow = "#fff59d", Knockback = "#b07040", Defence = "#a9b8c9", Freeze = "#c6f1ff", Burn = "#ff4d00",
        Poison = "#b5e61d", Charm = "#ff6fb5", Slow = "#8e7cc3", Recharge = "#d9c2ff";

    /// <summary>One thing a pearl does: its colour, its family (the icon it stands for) and how much it weighs.</summary>
    public readonly record struct Part(string Color, PearlIcon Family, float Weight);

    /// <summary>The pearl's look from what it does: its 2–3 colours (the strongest in the middle) and its family's icon.</summary>
    public static PearlLook Look(ItemDef item)
    {
        var parts = Parts(item);
        if (parts.Count == 0) parts.Add(new Part(Defence, PearlIcon.Arrow, 1f));
        // The family that weighs most (the first named on a tie) sets the icon.
        var family = parts.GroupBy(p => p.Family).OrderByDescending(g => g.Sum(p => p.Weight)).First().Key;
        // Each colour once, by its whole weight (first named first on a tie).
        var colors = parts.GroupBy(p => p.Color).Select((g, i) => (g.Key, W: g.Sum(p => p.Weight), i))
            .OrderByDescending(c => c.W).ThenBy(c => c.i).Select(c => c.Key).Take(3).ToList();
        var look = new PearlLook { Icon = family };
        switch (colors.Count)
        {
            case 1:
                look.Colors.AddRange(new[] { Shade(colors[0], 0.45f), colors[0], Shade(colors[0], -0.45f) });
                break;
            case 2:
                look.Colors.AddRange(new[] { Shade(colors[0], 0.25f), colors[0], colors[1] });
                break;
            default:
                look.Colors.AddRange(new[] { colors[1], colors[0], colors[2] });
                break;
        }
        return look;
    }

    /// <summary>Everything a pearl does that helps her, as colours, families and weights, in the order it is listed.</summary>
    public static List<Part> Parts(ItemDef item)
    {
        var parts = new List<Part>();
        void Add(string color, PearlIcon family, float weight = 1f) => parts.Add(new Part(color, family, Math.Clamp(weight, 0.5f, 2f)));

        foreach (var m in item.Stats)
        {
            // How far it moves the stat, against a usual step (a pearl's typical +/×), and whether that helps her.
            bool lowerHelps = m.Stat is Stat.DashCooldown or Stat.EnemyShotSpeed;
            float size = m.Op == StatOp.Mult ? MathF.Log(MathF.Max(m.Value, 0.01f)) / MathF.Log(1.3f) : m.Value / Step(m.Stat);
            if (lowerHelps) size = -size;
            if (size <= 0f) continue;
            var (color, family) = Of(m.Stat);
            Add(color, family, size);
        }
        if (item.Shot is { } s)
        {
            const PearlIcon Shots = PearlIcon.Chevron, Status = PearlIcon.Swirl;
            if (s.Multishot > 1) Add(MoreShots, Shots, (s.Multishot - 1) / 2f + 0.5f);
            if (s.Split || s.Wave || s.Rear || s.Grow) Add(MoreShots, Shots);
            if (s.SizeMult > 1f) Add(MoreShots, PearlIcon.Arrow, MathF.Log(s.SizeMult) / MathF.Log(1.3f));
            if (s.DamageMult > 1f) Add(Damage, PearlIcon.Arrow, MathF.Log(s.DamageMult) / MathF.Log(1.3f));
            if (s.Explosive || s.Charge || s.Laser || s.ChainTargets > 0 || s.CritChance > 0f) Add(Damage, Shots);
            if (s.Pierce || s.Spectral || s.Bounces > 0) Add(Range, Shots);
            if (s.Homing || s.Boomerang || s.Spiral) Add(ShotSpeed, Shots);
            if (s.FreezeChance > 0f) Add(Freeze, Status);
            if (s.BurnChance > 0f) Add(Burn, Status);
            if (s.PoisonChance > 0f) Add(Poison, Status);
            if (s.CharmChance > 0f) Add(Charm, Status);
            if (s.SlowChance > 0f) Add(Slow, Status);
        }
        foreach (var e in item.Effects)
            switch (e.Action)
            {
                case EffectAction.Heal: Add(Health, PearlIcon.Arrow); break;
                case EffectAction.Foam: Add(Defence, PearlIcon.Shield); break;
                case EffectAction.Coins: Add(Luck, PearlIcon.Nautilus); break;
                case EffectAction.Bombs or EffectAction.Shards: Add(Damage, PearlIcon.Chevron); break;
                case EffectAction.Frenzy: Add(FireRate, PearlIcon.Arrow); break;
            }
        foreach (string f in item.Flags)
            switch (f)
            {
                case "minigun" or "surge": Add(FireRate, PearlIcon.Chevron, 1.5f); break;
                case "cavitation": Add(Damage, PearlIcon.Chevron); break;
                case "magnet" or "richCaches" or "richDrops": Add(Luck, PearlIcon.Nautilus); break;
                case "landmarks" or "minimap": Add(Wayfinding, PearlIcon.Nautilus); break;
                case "mergeBubbles": Add(MoreShots, PearlIcon.Chevron); break;
            }
        if (item.Active is { } a)
        {
            const PearlIcon Halo = PearlIcon.Star;
            foreach (string c in a.Action switch
            {
                ActiveAction.Prism => new[] { Damage, Health, ShotSpeed },
                ActiveAction.PushWave => new[] { Knockback, Defence },
                ActiveAction.BubbleShield or ActiveAction.InkCloud => new[] { Defence },
                ActiveAction.WhaleSong => new[] { Health },
                ActiveAction.ConchHorn => new[] { Knockback, Slow },
                ActiveAction.TidalWave => new[] { Knockback, Damage },
                ActiveAction.TreasureMap => new[] { Wayfinding },
                ActiveAction.MimicClam or ActiveAction.SeaDice => new[] { Luck },
                ActiveAction.GlowBurst => new[] { Glow },
                _ => new[] { Damage },
            })
                Add(c, Halo, 1.5f);
        }
        return parts;
    }

    /// <summary>A stat's usual step on a pearl (what counts as one ordinary helping of it).</summary>
    static float Step(Stat s) => s switch
    {
        Stat.MaxHp => 20f,
        Stat.Damage => 0.5f,
        Stat.FireRate => 0.4f,
        Stat.ShotSpeed => 0.3f,
        Stat.Range => 1f,
        Stat.Speed => 0.25f,
        Stat.Luck => 1f,
        Stat.Regen => 0.2f,
        Stat.BubbleCapacity => 3f,
        Stat.BubblesPerThrow => 1f,
        _ => 1f,
    };

    /// <summary>A stat's colour and family.</summary>
    static (string Color, PearlIcon Family) Of(Stat s) => s switch
    {
        Stat.Damage => (Damage, PearlIcon.Arrow),
        Stat.FireRate => (FireRate, PearlIcon.Arrow),
        Stat.ShotSpeed => (ShotSpeed, PearlIcon.Arrow),
        Stat.Range => (Range, PearlIcon.Arrow),
        Stat.ShotSize or Stat.BubbleCapacity or Stat.BubbleRegrow or Stat.BubblesPerThrow or Stat.BubbleHover => (MoreShots, PearlIcon.Arrow),
        Stat.MaxHp or Stat.Regen => (Health, PearlIcon.Arrow),
        Stat.Speed or Stat.DashCooldown or Stat.Sink => (Mobility, PearlIcon.Feather),
        Stat.Luck => (Luck, PearlIcon.Nautilus),
        Stat.Glow => (Glow, PearlIcon.Arrow),
        Stat.Knockback => (Knockback, PearlIcon.Arrow),
        Stat.EnemyShotSpeed => (Defence, PearlIcon.Shield),
        Stat.ActiveRecharge => (Recharge, PearlIcon.Arrow),
        _ => (Defence, PearlIcon.Arrow),
    };

    /// <summary>A colour lightened toward white (amount > 0) or darkened toward black (amount < 0).</summary>
    static string Shade(string hex, float amount)
    {
        int r = Convert.ToInt32(hex.Substring(1, 2), 16), g = Convert.ToInt32(hex.Substring(3, 2), 16), b = Convert.ToInt32(hex.Substring(5, 2), 16);
        int To(int c) => (int)MathF.Round(amount >= 0f ? c + (255 - c) * amount : c * (1f + amount));
        return $"#{To(r):x2}{To(g):x2}{To(b):x2}";
    }
}
