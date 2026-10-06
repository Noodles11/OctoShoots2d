using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OctoShoots.Core.Sim;

/// <summary>What a creature notices Clementine with (DEPTH1-BESTIARY §2).</summary>
public enum Sense
{
    /// <summary>Needs a clear line of sight.</summary>
    Sight,
    /// <summary>Tastes or feels the water: no line of sight needed.</summary>
    Water,
    /// <summary>Feels vibrations through the seabed: far when she is close to a surface, otherwise it relies on sight at a shorter range.</summary>
    Vibration,
}

/// <summary>
/// One creature type's numbers. Several fields are shared by different creatures with different
/// meanings; each creature's doc entry says which it uses. Every field is live-tunable in the F1 panel.
/// </summary>
public sealed class CreatureDef
{
    [Tune("", 1f, 200f)] public float Hp = 20f;
    [Tune("", 0.1f, 2f)] public float Radius = 0.4f;
    [Tune("", 0f, 12f)] public float Speed = 2f;
    [Tune("")] public Sense Sense = Sense.Sight;
    [Tune("", 2f, 120f)] public float NoticeRange = 20f;
    /// <summary>Vibration sense: the sight range used when Clementine is not close to a surface.</summary>
    [Tune("", 2f, 120f)] public float NoticeRangeAlt = 20f;
    [Tune("", 2f, 150f)] public float GiveUpRange = 30f;
    /// <summary>Contact, thorn, bite or claw damage.</summary>
    [Tune("", 0f, 50f)] public float Damage = 10f;
    /// <summary>How close she must be for it to attack.</summary>
    [Tune("", 0f, 40f)] public float AttackRange = 6f;
    [Tune("", 0.45f, 2f)] public float Telegraph = 0.8f;
    [Tune("", 0f, 10f)] public float Cooldown = 3f;
    [Tune("", 0f, 5f)] public float Recover = 1f;
    /// <summary>Charge, lunge, leap, pulse or inflated-chase speed.</summary>
    [Tune("", 0f, 30f)] public float BurstSpeed = 0f;
    /// <summary>Charge length, lunge length or leap height.</summary>
    [Tune("", 0f, 40f)] public float Reach = 0f;
    /// <summary>How long a pufferling stays swollen.</summary>
    [Tune("", 0f, 5f)] public float HoldTime = 0f;
    [Tune("", 0, 24)] public int ShotCount = 0;
    [Tune("", 0f, 20f)] public float ShotSpeed = 0f;
    [Tune("", 0f, 50f)] public float ShotDamage = 0f;
    [Tune("", 0f, 40f)] public float ShotRange = 0f;
    [Tune("", 0.05f, 0.6f)] public float ShotRadius = 0.15f;

    /// <summary>Beds of small urchins: no shooting, less health, a weaker thorn (DEPTH1-BESTIARY §3).</summary>
    public const float BedHp = 12f;
    public const float BedThorn = 8f;
    public const float BedScale = 0.6f;
}

/// <summary>All creature types of the dens (data/creatures.json). The clownfish ninja and lanternfish use tuning instead.</summary>
public sealed class CreatureCatalog
{
    public static readonly EnemyKind[] Kinds =
    {
        EnemyKind.SpanishDancer, EnemyKind.Pufferling, EnemyKind.Barracuda, EnemyKind.MoonJelly,
        EnemyKind.SeaUrchin, EnemyKind.Crabby, EnemyKind.Moray,
    };

    static readonly JsonSerializerOptions Json = new()
    {
        IncludeFields = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    readonly Dictionary<EnemyKind, CreatureDef> _defs;

    CreatureCatalog(Dictionary<EnemyKind, CreatureDef> defs) => _defs = defs;

    public CreatureDef this[EnemyKind kind] => _defs.TryGetValue(kind, out var d) ? d : throw new KeyNotFoundException($"No creature definition for {kind}");

    public IEnumerable<KeyValuePair<EnemyKind, CreatureDef>> All => _defs.OrderBy(kv => kv.Key);

    /// <summary>Parses and validates; throws <see cref="InvalidOperationException"/> listing every problem.</summary>
    public static CreatureCatalog FromJson(string json)
    {
        var defs = JsonSerializer.Deserialize<Dictionary<EnemyKind, CreatureDef>>(json, Json) ?? throw new InvalidOperationException("creatures.json is empty");
        var problems = new List<string>();
        foreach (var kind in Kinds)
        {
            if (!defs.TryGetValue(kind, out var d))
            {
                problems.Add($"{kind} is missing");
                continue;
            }
            if (d.Hp <= 0f) problems.Add($"{kind}: hp must be positive");
            if (d.Radius <= 0f) problems.Add($"{kind}: radius must be positive");
            if (d.Telegraph < 0.45f) problems.Add($"{kind}: telegraph {d.Telegraph} is under the 0.45 s fairness minimum");
            if (d.NoticeRange <= 0f) problems.Add($"{kind}: notice range must be positive");
            if (d.GiveUpRange < d.NoticeRange) problems.Add($"{kind}: gives up ({d.GiveUpRange}) before it can notice ({d.NoticeRange})");
        }
        if (problems.Count > 0) throw new InvalidOperationException("creatures.json is invalid:\n  " + string.Join("\n  ", problems));
        return new CreatureCatalog(defs);
    }

    public string ToJson() => JsonSerializer.Serialize(_defs, Json);

    public CreatureCatalog Clone() => FromJson(ToJson());
}
