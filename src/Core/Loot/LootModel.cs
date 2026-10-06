using System.Collections.Generic;
using System.Numerics;

namespace OctoShoots.Core.Loot;

/// <summary>Small loot (2D §8): sand dollars, hearts, foam, ink bombs, glow jellies.</summary>
public enum PickupKind { Coin, Nickel, Dime, Heart, HalfHeart, FoamHeart, Bomb, GlowJelly }

/// <summary>An item pearl resting in a shell. Shop shells cost sand dollars.</summary>
public sealed record ShellSpot(Vector3 Position, string Pool, bool ForSale);

/// <summary>A closed wooden treasure chest; it opens when hit.</summary>
public sealed record ChestSpot(Vector3 Position);

/// <summary>A pickup lying loose on the seafloor or in a pocket.</summary>
public sealed record PickupSpot(Vector3 Position, PickupKind Kind);

/// <summary>Coins buried under the floor; a crater on top digs them out (2D §26).</summary>
public sealed record BuriedCoins(Vector3 Position, int Amount);

/// <summary>Where a level's loot starts. Item pearls are drawn from their pools when the world is created.</summary>
public sealed class LootPlan
{
    /// <summary>Which level this is, for seeded item draws (0 = the grey-box cave).</summary>
    public int Depth { get; init; }
    public int Reef { get; init; }
    public List<ShellSpot> Shells { get; init; } = new();
    public List<ChestSpot> Chests { get; init; } = new();
    public List<PickupSpot> Pickups { get; init; } = new();
    public List<BuriedCoins> Buried { get; init; } = new();
}

/// <summary>Values, prices and drop tables for loot.</summary>
public static class LootRules
{
    public const float ShellOpenRadius = 4.5f;
    public const float ShellCloseRadius = 6.5f;
    public const float PearlTakeRadius = 1.2f;
    public const float PickupRadius = 0.18f;
    public const float CollectRadius = 0.85f;
    public const float MagnetRadius = 6f;
    public const float ChestRadius = 0.6f;
    public const float HeartHeal = 15f;
    public const float HalfHeartHeal = 8f;
    public const float FoamAmount = 15f;

    /// <summary>Shop price: 15 sand dollars, 20 for quality 3–4 items.</summary>
    public static int Price(int quality) => quality >= 3 ? 20 : 15;

    public static int Coins(PickupKind kind) => kind switch
    {
        PickupKind.Coin => 1,
        PickupKind.Nickel => 5,
        PickupKind.Dime => 10,
        _ => 0,
    };

    /// <summary>What a defeated creature leaves behind, if anything (luck tips the odds).</summary>
    public static PickupKind? RollDrop(Rng rng, float luck)
    {
        float r = rng.NextFloat() / (1f + 0.05f * luck);
        if (r < 0.35f) return PickupKind.Coin;
        if (r < 0.43f) return PickupKind.HalfHeart;
        if (r < 0.48f) return PickupKind.Heart;
        if (r < 0.54f) return PickupKind.Bomb;
        if (r < 0.57f) return PickupKind.FoamHeart;
        if (r < 0.60f) return PickupKind.GlowJelly;
        return null;
    }

    /// <summary>A chest's haul: a handful of sand dollars plus one or two other things.</summary>
    public static List<PickupKind> RollChest(Rng rng)
    {
        var loot = new List<PickupKind>();
        int coins = 2 + rng.Int(4);
        for (int i = 0; i < coins; i++) loot.Add(rng.NextFloat() < 0.15f ? PickupKind.Nickel : PickupKind.Coin);
        int extras = 1 + rng.Int(2);
        for (int i = 0; i < extras; i++)
        {
            float r = rng.NextFloat();
            loot.Add(r < 0.3f ? PickupKind.Heart : r < 0.5f ? PickupKind.HalfHeart : r < 0.75f ? PickupKind.Bomb : r < 0.9f ? PickupKind.FoamHeart : PickupKind.GlowJelly);
        }
        return loot;
    }

    /// <summary>Loose seafloor loot, mostly sand dollars.</summary>
    public static PickupKind RollLoose(Rng rng)
    {
        float r = rng.NextFloat();
        return r < 0.55f ? PickupKind.Coin : r < 0.62f ? PickupKind.Nickel : r < 0.74f ? PickupKind.HalfHeart : r < 0.80f ? PickupKind.Heart
            : r < 0.90f ? PickupKind.Bomb : r < 0.96f ? PickupKind.FoamHeart : PickupKind.GlowJelly;
    }
}
