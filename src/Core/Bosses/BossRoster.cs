namespace OctoShoots.Core.Bosses;

/// <summary>
/// The bosses met, hardcoded one per depth (docs/BOSS-GENERATOR.md §1: the generator is parked until the hand-made
/// roster grows). For now every depth's boss is Drippus Prime (docs/CORRUPTION.md), tougher the deeper it is met and on
/// later loops. Queen Clam stays the generator's reference roll.
/// </summary>
public static class BossRoster
{
    /// <summary>Health grows by this share at Menace 1 (Depth 6 of the first loop; 0 at Depth 1). Its blows grow as all that hurts her does (PlaneCombatTuning.HurtPerMenace).</summary>
    public const float HpGrowth = 1.5f;

    /// <summary>The boss of this loop's depth (as the player numbers them, 1–6).</summary>
    public static BossSpec For(ulong runSeed, int cycle, int depth)
    {
        var spec = BossGenerator.DrippusPrime(runSeed, cycle, depth);
        spec.Hp *= 1f + HpGrowth * spec.Menace;
        return spec;
    }
}
