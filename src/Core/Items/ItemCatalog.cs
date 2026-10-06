using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OctoShoots.Core.Items;

/// <summary>All items, synergies and transformations, loaded from data/items.json.</summary>
public sealed class ItemCatalog
{
    public static readonly string[] KnownPools = { "treasure", "shop", "boss", "secret", "siren", "whale", "curse", "goldenClam" };
    public static readonly string[] KnownTags = { "tentacle", "glow", "predator", "coral", "galleon" };

    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    sealed class FileModel
    {
        public int Version { get; set; }
        public List<ItemDef> Items { get; set; } = new();
        public List<SynergyDef> Synergies { get; set; } = new();
        public List<TransformationDef> Transformations { get; set; } = new();
    }

    readonly Dictionary<string, ItemDef> _byId;

    ItemCatalog(FileModel model)
    {
        Version = model.Version;
        Items = model.Items;
        Synergies = model.Synergies;
        Transformations = model.Transformations;
        _byId = Items.ToDictionary(i => i.Id);
    }

    public int Version { get; }
    public IReadOnlyList<ItemDef> Items { get; }
    public IReadOnlyList<SynergyDef> Synergies { get; }
    public IReadOnlyList<TransformationDef> Transformations { get; }

    public ItemDef this[string id] => _byId.TryGetValue(id, out var item) ? item : throw new KeyNotFoundException($"Unknown item '{id}'");

    public bool Contains(string id) => _byId.ContainsKey(id);

    public bool TryGet(string id, out ItemDef item) => _byId.TryGetValue(id, out item!);

    /// <summary>Parses and validates; throws <see cref="InvalidOperationException"/> listing every problem.</summary>
    public static ItemCatalog FromJson(string json)
    {
        var model = JsonSerializer.Deserialize<FileModel>(json, Json) ?? throw new InvalidOperationException("items.json is empty");
        var problems = Validate(model);
        if (problems.Count > 0) throw new InvalidOperationException("items.json is invalid:\n  " + string.Join("\n  ", problems));
        return new ItemCatalog(model);
    }

    static bool IsHexColor(string s) =>
        s.Length == 7 && s[0] == '#' && s.Skip(1).All(Uri.IsHexDigit);

    static List<string> Validate(FileModel m)
    {
        var problems = new List<string>();
        var ids = new HashSet<string>();
        foreach (var item in m.Items)
        {
            string who = $"item '{item.Id}'";
            if (string.IsNullOrWhiteSpace(item.Id)) problems.Add("an item has no id");
            else if (!ids.Add(item.Id)) problems.Add($"{who} is defined twice");
            if (string.IsNullOrWhiteSpace(item.Name)) problems.Add($"{who} has no name");
            if (item.Quality is < 0 or > 4) problems.Add($"{who} quality {item.Quality} is outside 0–4");
            if (item.Weight <= 0f) problems.Add($"{who} weight must be positive");
            if (item.Pools.Count == 0) problems.Add($"{who} is in no pool");
            foreach (var pool in item.Pools)
                if (!KnownPools.Contains(pool)) problems.Add($"{who} uses unknown pool '{pool}'");
            foreach (var tag in item.Tags)
                if (!KnownTags.Contains(tag)) problems.Add($"{who} uses unknown tag '{tag}'");
            if (item.Kind == ItemKind.Active && (item.Active is null || item.Active.Recharge <= 0f))
                problems.Add($"{who} is active but has no recharge time");
            if (item.Kind == ItemKind.Passive && item.Active is not null)
                problems.Add($"{who} is passive but defines an active effect");
            if (item.Pearl is null) problems.Add($"{who} has no pearl look");
            else if (item.Pearl.Colors.Count is < 2 or > 3 || !item.Pearl.Colors.All(IsHexColor))
                problems.Add($"{who} pearl needs 2–3 colours like \"#ff8800\"");
        }
        foreach (var s in m.Synergies)
        {
            if (s.Requires.Count < 2) problems.Add($"synergy '{s.Id}' needs at least two items");
            foreach (var r in s.Requires)
                if (!ids.Contains(r)) problems.Add($"synergy '{s.Id}' requires unknown item '{r}'");
        }
        foreach (var t in m.Transformations)
        {
            if (!KnownTags.Contains(t.Tag)) problems.Add($"transformation '{t.Id}' uses unknown tag '{t.Tag}'");
            int carriers = m.Items.Count(i => i.Tags.Contains(t.Tag));
            if (carriers < t.Count) problems.Add($"transformation '{t.Id}' needs {t.Count} '{t.Tag}' items but only {carriers} exist");
        }
        return problems;
    }
}
