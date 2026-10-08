using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OctoShoots.Core;

/// <summary>Marks a field as live-tunable in the debug panel.</summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class TuneAttribute : Attribute
{
    public TuneAttribute(string group, float min, float max)
    {
        Group = group;
        Min = min;
        Max = max;
    }

    public TuneAttribute(string group) : this(group, 0f, 1f) { }

    public string Group { get; }
    public float Min { get; }
    public float Max { get; }
}

public enum AimAssistLevel { Off, Low, Medium, High }

/// <summary>Every simulation number in one place. Defaults follow DESIGN-3D.md.</summary>
public sealed class Tuning
{
    // Movement (§2)
    [Tune("Movement", 1f, 10f)] public float SwimSpeed = 4.5f;
    /// <summary>Top-down cruise speed (DESIGN-TOPDOWN §4.5). §2.2 also says "4.5 m/s equivalent": an open conflict, see §12.</summary>
    [Tune("Movement", 1f, 12f)] public float PlaneCruiseSpeed = 6f;
    [Tune("Movement", 0.01f, 0.5f)] public float AccelTime = 0.08f;
    [Tune("Movement", 0.01f, 1f)] public float StopTime = 0.12f;
    [Tune("Movement", 5f, 80f)] public float OverspeedDecel = 30f;
    [Tune("Movement", 0f, 1f)] public float JetDuration = 0.38f;
    [Tune("Movement", 1f, 3f)] public float JetMultiplier = 1.8f;
    [Tune("Movement", 45f, 180f)] public float JetTurnAngle = 110f;
    [Tune("Movement", 0f, 1f)] public float JetRestFraction = 0.25f;
    [Tune("Movement", 0.1f, 0.6f)] public float PlayerRadius = 0.35f;

    // Ink dash (§2, 2D §30)
    [Tune("Dash", 1f, 6f)] public float DashMultiplier = 3.4f;
    [Tune("Dash", 0.05f, 0.6f)] public float DashCoast = 0.24f;
    [Tune("Dash", 0f, 1f)] public float DashInvulnerable = 0.32f;
    [Tune("Dash", 0.1f, 3f)] public float DashCooldown = 0.85f;
    [Tune("Dash", 0.5f, 4f)] public float InkCloudRadius = 1.6f;
    [Tune("Dash", 0.5f, 5f)] public float InkCloudLife = 2f;
    [Tune("Dash", 0.1f, 1f)] public float InkCloudSlow = 0.4f;

    // Shooting (§4.1)
    [Tune("Shooting", 0.5f, 20f)] public float Damage = 3.5f;
    [Tune("Shooting", 0.5f, 10f)] public float FireRate = 2.7f;
    [Tune("Shooting", 0.3f, 3f)] public float ShotSpeed = 1f;
    [Tune("Shooting", 5f, 30f)] public float ShotSpeedScale = 14f;
    [Tune("Shooting", 2f, 15f)] public float Range = 6.5f;
    [Tune("Shooting", 0.5f, 3f)] public float RangeScale = 1.5f;
    [Tune("Shooting", 0.05f, 0.6f)] public float ShotRadius = 0.25f;
    [Tune("Shooting", 0f, 1f)] public float InheritVelocity = 0.3f;
    [Tune("Shooting", 0f, 6f)] public float ShotKnockback = 1.5f;

    // Bubbles held on the throwing tentacle's suckers (§4.1)
    [Tune("Bubbles", 2f, 12f)] public int BubbleCapacity = 8;
    [Tune("Bubbles", 0f, 3f)] public float BubbleRegrowDelay = 0.6f;
    [Tune("Bubbles", 0.05f, 2f)] public float BubbleRegrowInterval = 0.35f;

    // Aim assist (§4.2)
    [Tune("Aim assist")] public AimAssistLevel AimAssist = AimAssistLevel.Medium;
    [Tune("Aim assist", 0f, 20f)] public float LowCone = 3f;
    [Tune("Aim assist", 0f, 40f)] public float LowBend = 2f;
    [Tune("Aim assist", 0f, 0.8f)] public float LowSlowdown = 0.15f;
    [Tune("Aim assist", 0f, 20f)] public float MediumCone = 5f;
    [Tune("Aim assist", 0f, 40f)] public float MediumBend = 6f;
    [Tune("Aim assist", 0f, 0.8f)] public float MediumSlowdown = 0.25f;
    [Tune("Aim assist", 0f, 20f)] public float HighCone = 6.5f;
    [Tune("Aim assist", 0f, 40f)] public float HighBend = 8f;
    [Tune("Aim assist", 0f, 0.8f)] public float HighSlowdown = 0.3f;

    // Player
    [Tune("Player", 10f, 999f)] public float MaxHp = 100f;
    [Tune("Player", 0f, 3f)] public float HurtInvulnerable = 1f;
    [Tune("Player", 0.05f, 0.6f)] public float PlayerHurtRadius = 0.25f;
    [Tune("Player")] public bool Invincible = false;

    // Clownfish ninjas and their anemone nests (§7.5)
    [Tune("Ninja")] public bool NestsEnabled = true;
    [Tune("Ninja", 1f, 100f)] public float NinjaHp = 14f;
    [Tune("Ninja", 0.1f, 0.6f)] public float NinjaRadius = 0.32f;
    [Tune("Ninja", 5f, 60f)] public float NinjaEmergeRange = 11f;
    [Tune("Ninja", 10f, 100f)] public float NinjaReturnRange = 45f;
    [Tune("Ninja", 4f, 40f)] public float NinjaAttackRange = 15f;
    [Tune("Ninja", 0.1f, 2f)] public float NinjaTelegraph = 0.5f;
    [Tune("Ninja", 0.5f, 10f)] public float NinjaCooldown = 3f;
    [Tune("Ninja", 0f, 5f)] public float NinjaFirstDelay = 1.5f;
    [Tune("Ninja", 2f, 25f)] public float NinjaShotSpeed = 9f;
    [Tune("Ninja", 0.05f, 0.5f)] public float NinjaShotRadius = 0.15f;
    [Tune("Ninja", 0f, 50f)] public float NinjaShotDamage = 8f;
    [Tune("Ninja", 5f, 40f)] public float NinjaShotRange = 20f;
    [Tune("Ninja", 0.5f, 6f)] public float SchoolSpeed = 2.2f;

    // Ink bombs and craters (§6.3, 2D §4)
    [Tune("Bombs & craters", 0f, 20f)] public int StartBombs = 3;
    [Tune("Bombs & craters", 0.3f, 4f)] public float BombFuse = 1.5f;
    [Tune("Bombs & craters", 0f, 100f)] public float BombDamage = 30f;
    [Tune("Bombs & craters", 0f, 50f)] public float BombSelfDamage = 20f;
    [Tune("Bombs & craters", 1f, 6f)] public float BombBlastRadius = 3f;
    [Tune("Bombs & craters", 0.5f, 5f)] public float BombCraterRadius = 2.5f;
    [Tune("Bombs & craters", 0f, 2f)] public float InkCraterRadius = 0.6f;
    [Tune("Bombs & craters", 0f, 2f)] public float PearlCraterRadius = 0.8f;
    [Tune("Bombs & craters", 0f, 1f)] public float BeamCraterRadius = 0.4f;

    // Lanternfish (§7)
    [Tune("Enemy", 0f, 8f)] public int EnemyCount = 0;
    [Tune("Enemy", 1f, 100f)] public float EnemyHp = 18f;
    [Tune("Enemy", 0.5f, 8f)] public float EnemySpeed = 2.6f;
    [Tune("Enemy", 1f, 20f)] public float EnemyAccel = 6f;
    [Tune("Enemy", 0.2f, 1.2f)] public float EnemyRadius = 0.45f;
    [Tune("Enemy", 2f, 15f)] public float EnemyPreferredDistance = 7f;
    [Tune("Enemy", 5f, 60f)] public float EnemyAggroRange = 25f;
    [Tune("Enemy", 3f, 40f)] public float EnemyAttackRange = 15f;
    [Tune("Enemy", 0.1f, 2f)] public float EnemyTelegraph = 0.45f;
    [Tune("Enemy", 0.3f, 6f)] public float EnemyAttackCooldown = 2.2f;
    [Tune("Enemy", 1f, 20f)] public float EnemyShotSpeed = 6f;
    [Tune("Enemy", 0.05f, 1f)] public float EnemyShotRadius = 0.3f;
    [Tune("Enemy", 3f, 40f)] public float EnemyShotRange = 22f;
    [Tune("Enemy", 0f, 50f)] public float EnemyShotDamage = 10f;
    [Tune("Enemy", 0f, 50f)] public float EnemyContactDamage = 10f;
    [Tune("Enemy", 0f, 8f)] public int MaxOffscreenAttackers = 3;
    [Tune("Enemy", 0f, 10f)] public float EnemyRespawnDelay = 3f;

    public float ShotSpeedMetres => ShotSpeed * ShotSpeedScale;
    public float RangeMetres => Range * RangeScale;

    public (float ConeDeg, float BendDegPerSec, float Slowdown) AimProfile => AimAssist switch
    {
        AimAssistLevel.Low => (LowCone, LowBend, LowSlowdown),
        AimAssistLevel.Medium => (MediumCone, MediumBend, MediumSlowdown),
        AimAssistLevel.High => (HighCone, HighBend, HighSlowdown),
        _ => (0f, 0f, 0f),
    };

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        IncludeFields = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static Tuning FromJson(string json) =>
        JsonSerializer.Deserialize<Tuning>(json, JsonOptions) ?? new Tuning();

    public Tuning Clone() => FromJson(ToJson());
}
