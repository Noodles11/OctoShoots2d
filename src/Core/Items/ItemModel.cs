using System.Collections.Generic;

namespace OctoShoots.Core.Items;

public enum Stat
{
    MaxHp,
    Damage,
    FireRate,
    ShotSpeed,
    Range,
    Speed,
    Luck,
    ShotSize,
    Sink,
    DashCooldown,
    Glow,
    EnemyShotSpeed,
    Knockback,
    Regen,
    BubbleCapacity,
    BubbleRegrow,
    BubblesPerThrow,
    /// <summary>How fast the held active item recharges (1 = its listed recharge time).</summary>
    ActiveRecharge,
    /// <summary>How long a bubble hovers where it stopped before it pops (a factor on its own 0.8–1.2 s).</summary>
    BubbleHover,
}

public enum StatOp { Add, Mult }

/// <summary>One stat change. All Adds apply before all Mults (2D §9.1 "fixed order").</summary>
public sealed class StatMod
{
    public Stat Stat { get; set; }
    public StatOp Op { get; set; } = StatOp.Add;
    public float Value { get; set; }
}

public enum ShotPattern { Fan, Cone, Kraken8 }

/// <summary>
/// Composable projectile behaviour (2D §9.1). Items, synergies and transformations each carry one;
/// the loadout merges them: flags OR, counts add, chances combine, multipliers multiply.
/// </summary>
public sealed class ShotSpec
{
    /// <summary>Total shots per volley. Merging adds the extras: Triple (3) + Double Helix (2) = 4.</summary>
    public int Multishot { get; set; } = 1;
    public ShotPattern Pattern { get; set; } = ShotPattern.Fan;
    public float SpreadDeg { get; set; } = 8f;

    public bool Homing { get; set; }
    public bool Pierce { get; set; }
    public bool Spectral { get; set; }
    public int Bounces { get; set; }
    public bool Split { get; set; }
    public bool Boomerang { get; set; }
    public bool Wave { get; set; }
    public bool Spiral { get; set; }
    public bool Grow { get; set; }
    public bool Explosive { get; set; }
    public bool Charge { get; set; }
    public bool Laser { get; set; }
    public bool Rear { get; set; }

    public int ChainTargets { get; set; }
    public float ChainRange { get; set; } = 4f;
    public float ChainDamage { get; set; } = 0.5f;

    public float FreezeChance { get; set; }
    public float BurnChance { get; set; }
    public float PoisonChance { get; set; }
    public float CharmChance { get; set; }
    public float SlowChance { get; set; }
    public float CritChance { get; set; }
    public float CritMult { get; set; } = 1f;

    public float SizeMult { get; set; } = 1f;
    public float DamageMult { get; set; } = 1f;

    public ShotSpec Clone() => (ShotSpec)MemberwiseClone();

    public void MergeFrom(ShotSpec o)
    {
        Multishot += o.Multishot - 1;
        if (o.Pattern != ShotPattern.Fan) Pattern = o.Pattern;
        if (o.Pattern != ShotPattern.Fan || o.Multishot > 1) SpreadDeg = o.SpreadDeg;
        Homing |= o.Homing;
        Pierce |= o.Pierce;
        Spectral |= o.Spectral;
        Bounces += o.Bounces;
        Split |= o.Split;
        Boomerang |= o.Boomerang;
        Wave |= o.Wave;
        Spiral |= o.Spiral;
        Grow |= o.Grow;
        Explosive |= o.Explosive;
        Charge |= o.Charge;
        Laser |= o.Laser;
        Rear |= o.Rear;
        if (o.ChainTargets > 0)
        {
            ChainTargets += o.ChainTargets;
            ChainRange = System.MathF.Max(ChainRange, o.ChainRange);
            ChainDamage = System.MathF.Max(ChainDamage, o.ChainDamage);
        }
        FreezeChance = Combine(FreezeChance, o.FreezeChance);
        BurnChance = Combine(BurnChance, o.BurnChance);
        PoisonChance = Combine(PoisonChance, o.PoisonChance);
        CharmChance = Combine(CharmChance, o.CharmChance);
        SlowChance = Combine(SlowChance, o.SlowChance);
        CritChance = Combine(CritChance, o.CritChance);
        CritMult = System.MathF.Max(CritMult, o.CritMult);
        SizeMult *= o.SizeMult;
        DamageMult *= o.DamageMult;
    }

    static float Combine(float a, float b) => 1f - (1f - a) * (1f - b);
}

public enum Trigger { OnPickup, OnShoot, OnHit, OnKill, OnDamaged, OnFloorStart }

public enum EffectAction { Heal, Foam, Coins, Bombs, Frenzy, Shards }

/// <summary>"When X happens, do Y" (2D §9.1 triggers).</summary>
public sealed class TriggerEffect
{
    public Trigger Trigger { get; set; }
    public EffectAction Action { get; set; }
    public float Value { get; set; }
    public float Duration { get; set; }
    public float Chance { get; set; } = 1f;
}

/// <summary>What an item, synergy or transformation grants.</summary>
public class EffectBundle
{
    public List<StatMod> Stats { get; set; } = new();
    public ShotSpec? Shot { get; set; }
    public List<TriggerEffect> Effects { get; set; } = new();
    public List<string> Flags { get; set; } = new();
}

public enum ItemKind { Passive, Active }

public enum ActiveAction
{
    ConchHorn,
    BubbleShield,
    TidalWave,
    TreasureMap,
    MimicClam,
    GlowBurst,
    SeaDice,
    KrakenCall,
    InkCloud,
    WhaleSong,
    AnchorDrop,
}

public sealed class ActiveSpec
{
    /// <summary>Seconds to recharge fully after use.</summary>
    public float Recharge { get; set; }
    public ActiveAction Action { get; set; }
    public float Value { get; set; }
    public float Duration { get; set; }
    public float Radius { get; set; }
}

/// <summary>How an item's pearl is painted: a hand-picked 2–3 colour gradient laid out in a pattern.</summary>
public enum PearlPattern { Bands, Swirl, Spots, Marble, Rings, Stripes, Speckle, Halo }

public sealed class PearlLook
{
    /// <summary>Gradient stops as "#rrggbb", 2 or 3 of them.</summary>
    public List<string> Colors { get; set; } = new();
    public PearlPattern Pattern { get; set; }
}

public sealed class ItemDef : EffectBundle
{
    /// <summary>Items are found and carried as glowing pearls.</summary>
    public PearlLook? Pearl { get; set; }

    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Tagline { get; set; } = "";
    public ItemKind Kind { get; set; } = ItemKind.Passive;

    /// <summary>0–4, like Isaac.</summary>
    public int Quality { get; set; }
    public float Weight { get; set; } = 1f;
    public List<string> Pools { get; set; } = new();
    public List<string> Tags { get; set; } = new();

    /// <summary>Achievement id that adds this item to the pools, or null if always available.</summary>
    public string? Unlock { get; set; }
    public ActiveSpec? Active { get; set; }
}

/// <summary>A named combo of specific items (2D §9.4). Bespoke behaviour is keyed by Id in the sim.</summary>
public sealed class SynergyDef : EffectBundle
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<string> Requires { get; set; } = new();
    public string Description { get; set; } = "";
}

/// <summary>Granted by owning Count items with Tag (2D §9.4).</summary>
public sealed class TransformationDef : EffectBundle
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Tag { get; set; } = "";
    public int Count { get; set; } = 3;
    public string Description { get; set; } = "";
}
