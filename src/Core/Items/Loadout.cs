using System;
using System.Collections.Generic;
using System.Linq;

namespace OctoShoots.Core.Items;

/// <summary>Final stat values after every modifier.</summary>
public sealed class StatBlock
{
    readonly Dictionary<Stat, float> _values = new();

    public float this[Stat stat]
    {
        get => _values.TryGetValue(stat, out float v) ? v : 0f;
        set => _values[stat] = value;
    }

    public float MaxHp => this[Stat.MaxHp];
    public float Damage => this[Stat.Damage];
    public float FireRate => this[Stat.FireRate];
    public float ShotSpeed => this[Stat.ShotSpeed];
    public float Range => this[Stat.Range];
    public float Speed => this[Stat.Speed];
    public float Luck => this[Stat.Luck];

    /// <summary>The starting stats: Clementine's base block, with sim numbers taken from tuning.</summary>
    public static StatBlock Base(Tuning t)
    {
        var s = new StatBlock();
        s[Stat.MaxHp] = t.MaxHp;
        s[Stat.Damage] = t.Damage;
        s[Stat.FireRate] = t.FireRate;
        s[Stat.ShotSpeed] = t.ShotSpeed;
        s[Stat.Range] = t.Range;
        s[Stat.Speed] = 1f;
        s[Stat.Luck] = 0f;
        s[Stat.ShotSize] = 1f;
        s[Stat.Sink] = 1f;
        s[Stat.DashCooldown] = 1f;
        s[Stat.Glow] = 1f;
        s[Stat.EnemyShotSpeed] = 1f;
        s[Stat.Knockback] = 1f;
        s[Stat.Regen] = 0f;
        s[Stat.BubbleCapacity] = t.BubbleCapacity;
        s[Stat.BubbleRegrow] = 1f;
        s[Stat.BubblesPerThrow] = 1f;
        s[Stat.ActiveRecharge] = 1f;
        return s;
    }

    /// <summary>Applies all Adds, then all Mults, then clamps to sane limits.</summary>
    public void Apply(IEnumerable<StatMod> mods)
    {
        var list = mods.ToList();
        foreach (var m in list.Where(m => m.Op == StatOp.Add)) this[m.Stat] += m.Value;
        foreach (var m in list.Where(m => m.Op == StatOp.Mult)) this[m.Stat] *= m.Value;

        this[Stat.MaxHp] = Math.Clamp(this[Stat.MaxHp], 20f, HealthRules.MaxTotal);
        this[Stat.Damage] = MathF.Max(this[Stat.Damage], 0.5f);
        this[Stat.FireRate] = Math.Clamp(this[Stat.FireRate], 0.5f, 15f);
        this[Stat.ShotSpeed] = Math.Clamp(this[Stat.ShotSpeed], 0.3f, 3f);
        this[Stat.Range] = MathF.Max(this[Stat.Range], 2f);
        this[Stat.Speed] = Math.Clamp(this[Stat.Speed], 0.4f, 2f);
        this[Stat.ShotSize] = Math.Clamp(this[Stat.ShotSize], 0.3f, 3f);
        this[Stat.Sink] = MathF.Max(this[Stat.Sink], 0f);
        this[Stat.DashCooldown] = Math.Clamp(this[Stat.DashCooldown], 0.3f, 2f);
        this[Stat.BubbleCapacity] = MathF.Round(Math.Clamp(this[Stat.BubbleCapacity], 2f, 12f));
        this[Stat.BubbleRegrow] = Math.Clamp(this[Stat.BubbleRegrow], 0.25f, 4f);
        this[Stat.BubblesPerThrow] = MathF.Round(Math.Clamp(this[Stat.BubblesPerThrow], 1f, 6f));
        this[Stat.ActiveRecharge] = Math.Clamp(this[Stat.ActiveRecharge], 0.25f, 4f);
    }
}

public static class HealthRules
{
    /// <summary>Max HP + foam never exceed this (2D §23).</summary>
    public const float MaxTotal = 300f;
}

/// <summary>
/// Everything the owned items add up to: final stats, merged shot behaviour, trigger effects, flags,
/// active synergies and transformations. Rebuilt whenever the item list changes.
/// </summary>
public sealed class Loadout
{
    public StatBlock Stats { get; private init; } = new();
    public ShotSpec Shot { get; private init; } = new();
    public List<TriggerEffect> Effects { get; } = new();
    public HashSet<string> Flags { get; } = new();
    public HashSet<string> Synergies { get; } = new();
    public HashSet<string> Transformations { get; } = new();
    public ItemDef? Active { get; private init; }

    public bool Has(string synergyOrTransformation) =>
        Synergies.Contains(synergyOrTransformation) || Transformations.Contains(synergyOrTransformation);

    public static Loadout Empty(Tuning tuning)
    {
        var stats = StatBlock.Base(tuning);
        stats.Apply(Array.Empty<StatMod>());
        return new Loadout { Stats = stats };
    }

    public static Loadout Build(ItemCatalog? catalog, IReadOnlyList<string> itemIds, Tuning tuning)
    {
        if (catalog is null || itemIds.Count == 0) return Empty(tuning);

        var owned = itemIds.Where(catalog.Contains).Select(id => catalog[id]).ToList();
        var bundles = new List<EffectBundle>(owned);

        var ownedIds = owned.Select(i => i.Id).ToHashSet();
        var synergies = catalog.Synergies.Where(s => s.Requires.All(ownedIds.Contains)).ToList();
        bundles.AddRange(synergies);

        var tagCounts = owned.SelectMany(i => i.Tags.Distinct()).GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count());
        var transformations = catalog.Transformations.Where(t => tagCounts.GetValueOrDefault(t.Tag) >= t.Count).ToList();
        bundles.AddRange(transformations);

        var stats = StatBlock.Base(tuning);
        stats.Apply(bundles.SelectMany(b => b.Stats));

        var shot = new ShotSpec();
        foreach (var b in bundles)
            if (b.Shot is not null) shot.MergeFrom(b.Shot);

        var loadout = new Loadout
        {
            Stats = stats,
            Shot = shot,
            // The most recently picked active item is the one held (only one at a time).
            Active = owned.LastOrDefault(i => i.Kind == ItemKind.Active),
        };
        foreach (var b in bundles)
        {
            loadout.Effects.AddRange(b.Effects);
            foreach (var f in b.Flags) loadout.Flags.Add(f);
        }
        foreach (var s in synergies) loadout.Synergies.Add(s.Id);
        foreach (var t in transformations) loadout.Transformations.Add(t.Id);
        return loadout;
    }
}
