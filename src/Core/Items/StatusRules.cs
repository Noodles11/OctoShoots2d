namespace OctoShoots.Core.Items;

/// <summary>Durations and strengths of status effects that shots and actives inflict on creatures.</summary>
public static class StatusRules
{
    public const float FreezeTime = 1.6f;
    public const float BurnTime = 3f;
    /// <summary>Burn damage per second as a fraction of the hit's damage.</summary>
    public const float BurnDpsFactor = 0.4f;
    public const float PoisonTime = 4f;
    public const float PoisonDpsFactor = 0.3f;
    public const float CharmTime = 3f;
    public const float SlowTime = 2f;
    public const float SlowFactor = 0.5f;
    public const float StunKnockback = 6f;

    /// <summary>Luck raises proc chances by 10% of their base per point, capped at 100%.</summary>
    public static float WithLuck(float chance, float luck) =>
        chance <= 0f ? 0f : System.Math.Clamp(chance * (1f + 0.1f * luck), 0f, 1f);
}
