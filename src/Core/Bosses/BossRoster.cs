namespace OctoShoots.Core.Bosses;

/// <summary>
/// The bosses met, hardcoded one per depth (docs/BOSS-GENERATOR.md §1: the generator is parked until the hand-made
/// roster grows). For now every depth's boss is Queen Clam, tougher the deeper she is met and on later loops.
/// </summary>
public static class BossRoster
{
    /// <summary>Health and pearl damage grow by these shares at Menace 1 (Depth 6 of the first loop; 0 at Depth 1).</summary>
    public const float HpGrowth = 0.6f, DamageGrowth = 0.4f;

    /// <summary>The boss of this loop's depth (as the player numbers them, 1–6).</summary>
    public static BossSpec For(ulong runSeed, int cycle, int depth)
    {
        var spec = BossGenerator.QueenClam(runSeed, cycle, depth);
        spec.Hp *= 1f + HpGrowth * spec.Menace;
        spec.Damage *= 1f + DamageGrowth * spec.Menace;
        return spec;
    }
}
