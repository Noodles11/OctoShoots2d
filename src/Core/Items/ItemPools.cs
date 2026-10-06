using System;
using System.Collections.Generic;
using System.Linq;

namespace OctoShoots.Core.Items;

/// <summary>
/// Weighted, seeded draws from an item pool (2D §9.2). Each room draws from its own RNG stream
/// (<see cref="Run.RunStreams.Items"/>), so player actions elsewhere never change what a room holds.
/// </summary>
public static class ItemPools
{
    /// <summary>
    /// Picks one item from the pool that is unlocked and not excluded (owned or already offered this run).
    /// Falls back to the treasure pool when the pool is exhausted; returns null when nothing is left.
    /// </summary>
    public static ItemDef? Draw(ItemCatalog catalog, string pool, Rng rng, ISet<string> unlockedAchievements, ISet<string> excluded)
    {
        var candidates = Candidates(catalog, pool, unlockedAchievements, excluded);
        if (candidates.Count == 0 && pool != "treasure") candidates = Candidates(catalog, "treasure", unlockedAchievements, excluded);
        if (candidates.Count == 0) return null;

        float total = candidates.Sum(i => i.Weight);
        float roll = rng.NextFloat() * total;
        foreach (var item in candidates)
        {
            roll -= item.Weight;
            if (roll < 0f) return item;
        }
        return candidates[^1];
    }

    public static bool IsUnlocked(ItemDef item, ISet<string> unlockedAchievements) =>
        item.Unlock is null || unlockedAchievements.Contains(item.Unlock);

    static List<ItemDef> Candidates(ItemCatalog catalog, string pool, ISet<string> unlocked, ISet<string> excluded) =>
        catalog.Items.Where(i => i.Pools.Contains(pool) && IsUnlocked(i, unlocked) && !excluded.Contains(i.Id)).ToList();
}
