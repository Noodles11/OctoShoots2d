using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace OctoShoots.Core.Items;

/// <summary>An achievement (data/achievements.json): what it is called, the line its banner reads, and the pearl it unlocks.</summary>
public sealed class AchievementDef
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Line { get; set; } = "";
    /// <summary>The line's flavour on the banner: "candy" or "dark".</summary>
    public string Tone { get; set; } = "candy";
    /// <summary>The pearl it unlocks.</summary>
    public string Pearl { get; set; } = "";
    /// <summary>How to earn it (shown while it is unearned).</summary>
    public string Hint { get; set; } = "";
}

/// <summary>The achievements, in their listed order, loaded from data/achievements.json and checked against the items.</summary>
public sealed class AchievementCatalog
{
    sealed class FileModel
    {
        public List<AchievementDef> Achievements { get; set; } = new();
    }

    readonly Dictionary<string, AchievementDef> _byId;

    AchievementCatalog(List<AchievementDef> all)
    {
        All = all;
        _byId = all.ToDictionary(a => a.Id);
    }

    public IReadOnlyList<AchievementDef> All { get; }

    public bool TryGet(string id, out AchievementDef achievement) => _byId.TryGetValue(id, out achievement!);

    /// <summary>
    /// Reads the file and checks it: ids unique, every pearl in the item catalog and unlocked by exactly this
    /// achievement (its items.json `unlock`), and no pearl unlocked twice.
    /// </summary>
    public static AchievementCatalog FromJson(string json, ItemCatalog items)
    {
        var model = JsonSerializer.Deserialize<FileModel>(json, ItemCatalog.Json) ?? throw new InvalidOperationException("achievements.json is empty");
        var problems = new List<string>();
        foreach (var dup in model.Achievements.GroupBy(a => a.Id).Where(g => g.Count() > 1)) problems.Add($"achievement {dup.Key} listed twice");
        foreach (var dup in model.Achievements.GroupBy(a => a.Pearl).Where(g => g.Count() > 1)) problems.Add($"pearl {dup.Key} unlocked twice");
        foreach (var a in model.Achievements)
        {
            if (!items.TryGet(a.Pearl, out var pearl)) problems.Add($"{a.Id}: no pearl {a.Pearl}");
            else if (pearl.Unlock != a.Id) problems.Add($"{a.Id}: pearl {a.Pearl} waits for '{pearl.Unlock}', not this achievement");
            if (string.IsNullOrWhiteSpace(a.Title) || string.IsNullOrWhiteSpace(a.Line) || string.IsNullOrWhiteSpace(a.Hint)) problems.Add($"{a.Id}: title, line and hint are required");
        }
        if (problems.Count > 0) throw new InvalidOperationException("achievements.json: " + string.Join("; ", problems));
        return new AchievementCatalog(model.Achievements);
    }
}
