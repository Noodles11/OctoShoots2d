using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OctoShoots.Core.Gen.TopDown;

/// <summary>Which level of a depth a template may stand on.</summary>
public enum TemplateSlot { First, Middle, BeforeBoss, Boss }

/// <summary>The corrupted kinds a template casts (murklings bud; their rules are the template's rules).</summary>
public enum CastKind { Blightroot, Blotter, Thumpsquirt, Inkwarden, Lashjaw }

/// <summary>A place rule: so many required, a chance of one, or none at all (unnamed kinds keep the general roll).</summary>
public sealed class PlaceRule
{
    public int Required { get; set; }
    public float? Chance { get; set; }
    public bool None { get; set; }
}

/// <summary>
/// A cast rule: how many of a kind (a range at Menace 0, plus PerMenace × Menace) and where each stands (anchors, used
/// in turn: the first creature at the first anchor, the second at the second, and round again), or none at all.
/// </summary>
public sealed class CastRule
{
    public int[] Count { get; set; } = { 0, 0 };
    public float PerMenace { get; set; }
    public string[] Anchors { get; set; } = { "spread" };
    public bool None { get; set; }

    /// <summary>The count for a level (rolled from its range).</summary>
    public int Roll(Rng rng, float menace)
    {
        int lo = Count.Length > 0 ? Count[0] : 0, hi = Count.Length > 1 ? Count[1] : lo;
        return lo + rng.Int(Math.Max(hi - lo, 0) + 1) + (int)MathF.Round(PerMenace * menace);
    }
}

/// <summary>The cave's shape, as multipliers on the cave generator's ranges (1: as it is).</summary>
public sealed class LayoutRule
{
    public float ChamberRadius { get; set; } = 1f;
    public float TunnelWidth { get; set; } = 1f;
    public float TunnelLength { get; set; } = 1f;
    public float Loops { get; set; } = 1f;
}

/// <summary>A template's extra rules, on top of the general ones.</summary>
public sealed class TemplateRules
{
    /// <summary>A murkling prime waits by this place from the start.</summary>
    public PoiKind? Prime { get; set; }
    /// <summary>The nearest Blightroot's guardians bud round this place (not round the root).</summary>
    public PoiKind? Guardians { get; set; }
    /// <summary>Murkling budding: its interval × 1/BudRate, its cap + BudCap.</summary>
    public float BudRate { get; set; } = 1f;
    public int BudCap { get; set; }
    /// <summary>The corruption's cover share, plus this (still capped).</summary>
    public float Ink { get; set; }
    /// <summary>Light springs (null: the general chance of one).</summary>
    public int? Springs { get; set; }
    /// <summary>Reef fish, × this.</summary>
    public float Fish { get; set; } = 1f;
    /// <summary>Inkwardens stand guard by this kind (else by Blightroots).</summary>
    public CastKind? WardenFor { get; set; }
    /// <summary>No ambush springs and no Blotter is born all level.</summary>
    public bool Quiet { get; set; }
    /// <summary>Side tunnels sealed by a wall with an X (opened as a secret's is).</summary>
    public int PathSeals { get; set; }
}

/// <summary>
/// A level template (docs/LEVEL-TEMPLATES-PROPOSAL.md, DESIGN-TOPDOWN §4.1): what a level is about. It names the places
/// a level must hold or may hold (with what chance) or must not; the cave's shape; which corrupted kinds it casts, how
/// many and where; and its extra rules. The general rules stay underneath and win: the validator, the place cap, the
/// treasure rooms per depth, the kinds' depth gates and Menace.
/// </summary>
public sealed class LevelTemplate
{
    public string Id { get; set; } = "";
    /// <summary>The name shown under the level title (empty: none).</summary>
    public string Name { get; set; } = "";
    public string Summary { get; set; } = "";
    public float Weight { get; set; } = 1f;
    /// <summary>Its weight on particular slots (else Weight).</summary>
    public Dictionary<TemplateSlot, float> SlotWeights { get; set; } = new();
    /// <summary>Shown depths it may stand in (inclusive).</summary>
    public int[] Depths { get; set; } = { 1, 6 };
    public TemplateSlot[] Slots { get; set; } = Array.Empty<TemplateSlot>();
    /// <summary>At most this many levels of a depth (0: no limit).</summary>
    public int MaxPerDepth { get; set; }
    public Dictionary<PoiKind, PlaceRule> Places { get; set; } = new();
    public LayoutRule Layout { get; set; } = new();
    public Dictionary<CastKind, CastRule> Cast { get; set; } = new();
    public TemplateRules Rules { get; set; } = new();

    public float WeightOn(TemplateSlot slot) => SlotWeights.TryGetValue(slot, out float w) ? w : Weight;

    public bool Fits(int shownDepth, TemplateSlot slot) =>
        Slots.Contains(slot) && shownDepth >= Depths[0] && shownDepth <= Depths[^1] && WeightOn(slot) > 0f;

    /// <summary>Whether it forbids a place kind.</summary>
    public bool Forbids(PoiKind kind) => Places.TryGetValue(kind, out var r) && r.None;

    public bool Requires(PoiKind kind) => Places.TryGetValue(kind, out var r) && r.Required > 0;

    /// <summary>Its rule for a corrupted kind, or null (the kind's general rule).</summary>
    public CastRule? CastOf(CastKind kind) => Cast.TryGetValue(kind, out var r) ? r : null;
}

/// <summary>The level templates (data/level_templates.json, embedded in Core so every level is made by the same ones).</summary>
public static class LevelTemplates
{
    static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    sealed class FileModel
    {
        public List<LevelTemplate> Templates { get; set; } = new();
    }

    static IReadOnlyList<LevelTemplate>? _all;

    /// <summary>Every template, in the file's order.</summary>
    public static IReadOnlyList<LevelTemplate> All => _all ??= Load();

    public static LevelTemplate? Get(string id) => All.FirstOrDefault(t => t.Id == id);

    static IReadOnlyList<LevelTemplate> Load()
    {
        using var stream = typeof(LevelTemplates).Assembly.GetManifestResourceStream("OctoShoots.Core.level_templates.json")
            ?? throw new InvalidOperationException("level_templates.json is not embedded in Core");
        using var reader = new StreamReader(stream);
        return FromJson(reader.ReadToEnd());
    }

    public static IReadOnlyList<LevelTemplate> FromJson(string json)
    {
        var model = JsonSerializer.Deserialize<FileModel>(json, Json) ?? throw new InvalidOperationException("level_templates.json is empty");
        var problems = new List<string>();
        var ids = new HashSet<string>();
        foreach (var t in model.Templates)
        {
            if (string.IsNullOrEmpty(t.Id) || !ids.Add(t.Id)) problems.Add($"template id '{t.Id}' is missing or repeated");
            if (t.Slots.Length == 0) problems.Add($"{t.Id}: no slots");
            if (t.Depths.Length is < 1 or > 2) problems.Add($"{t.Id}: depths must be [min, max]");
            foreach (var (kind, rule) in t.Cast)
                foreach (string anchor in rule.Anchors)
                    if (TemplateAnchor.Parse(anchor) is null) problems.Add($"{t.Id}: {kind} has an unknown anchor '{anchor}'");
        }
        if (!model.Templates.Any(t => t.Id == "default")) problems.Add("no 'default' template");
        if (problems.Count > 0) throw new InvalidOperationException("level_templates.json is invalid:\n  " + string.Join("\n  ", problems));
        return model.Templates;
    }
}

/// <summary>Where a cast creature stands.</summary>
public enum AnchorKind { Spread, Path, Tunnels, Chambers, DeadEnds, Walls, Place }

/// <summary>An anchor: its kind, and for Place the place kind it stands by.</summary>
public readonly record struct TemplateAnchor(AnchorKind Kind, PoiKind Place = PoiKind.Start)
{
    public static TemplateAnchor? Parse(string text)
    {
        if (text.StartsWith("place:", StringComparison.OrdinalIgnoreCase))
            return Enum.TryParse<PoiKind>(text[6..], true, out var poi) ? new TemplateAnchor(AnchorKind.Place, poi) : null;
        return Enum.TryParse<AnchorKind>(text, true, out var kind) && kind != AnchorKind.Place ? new TemplateAnchor(kind) : null;
    }

    /// <summary>A place's anchor ring: open water this far from it (its mouth, for a room in rock).</summary>
    public const float PlaceNear = 4f, PlaceFar = 14f;

    /// <summary>Whether a point satisfies the anchor on this level.</summary>
    public bool Holds(LevelMap map, Vector2 p) => Kind switch
    {
        AnchorKind.Spread => true,
        AnchorKind.Path => map.Corridors.Any(c => c.Kind == CorridorKind.Main && c.DistanceToCentre(p) < c.HalfWidth + 1.5f)
                           || map.Chambers.Any(c => c.Main && Vector2.Distance(c.At, p) < c.Radius),
        AnchorKind.Tunnels => map.Corridors.Any(c => c.Kind is CorridorKind.Main or CorridorKind.Side && c.DistanceToCentre(p) < c.HalfWidth)
                              && !map.Chambers.Any(c => Vector2.Distance(c.At, p) < c.Radius + 1f),
        AnchorKind.Chambers => map.Chambers.Any(c => Vector2.Distance(c.At, p) < c.Radius * 0.85f),
        AnchorKind.DeadEnds => map.Chambers.Any(c => c.DeadEnd && Vector2.Distance(c.At, p) < c.Radius * 0.9f),
        AnchorKind.Walls => !map.IsOpen(p + new Vector2(2.5f, 0f)) || !map.IsOpen(p - new Vector2(2.5f, 0f))
                            || !map.IsOpen(p + new Vector2(0f, 2.5f)) || !map.IsOpen(p - new Vector2(0f, 2.5f)),
        _ => NearPlace(map, Place, p),
    };

    static bool NearPlace(LevelMap map, PoiKind kind, Vector2 p) =>
        map.Pois.Any(q => q.Kind == kind && Vector2.Distance(q.Position, p) >= PlaceNear && Vector2.Distance(q.Position, p) <= PlaceFar + q.Radius * 0.5f);
}
