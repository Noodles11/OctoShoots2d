using System.Linq;

namespace OctoShoots.Core.Bosses;

/// <summary>
/// Boss names for the title card (docs/BOSS-GENERATOR.md §10): storybook, "[Epithet] [Noun]", big and warm early, thin and
/// cold deep. The noun follows the body; the epithet may name what the Leak did, never the Leak and never intent.
/// </summary>
public static class BossNames
{
    static readonly string[][] Epithets =
    {
        new[] { "Old", "Big", "Sleepy", "Grumbly", "King", "Mama", "Papa", "Lumpy" },
        new[] { "Gilded", "Twisted", "Swollen", "Overgrown", "Crooked", "Heavy" },
        new[] { "Mother", "The Hollow", "The Pale", "The Patient", "Drowned", "The Unmade", "Half-Grown" },
    };

    /// <summary>An epithet that names the mutation, preferred when it fits.</summary>
    static string? FromMutation(BossSpec spec, int band) => (spec.Theme, band) switch
    {
        (MutationTheme.ExtraArms, 0) => "Three-Arm",
        (MutationTheme.ExtraArms, _) => "Many-Armed",
        (MutationTheme.OvergrownShell, 1) => "Overgrown",
        (MutationTheme.InkSacs, 1) => "Swollen",
        (MutationTheme.FusedBells, 2) => "Half-Grown",
        _ => null,
    };

    static string[] Nouns(BossSpec spec, int band) => spec.Form switch
    {
        BodyForm.GiantClam => new[] { "Clam" },
        BodyForm.SeaStar => band == 0 ? new[] { "Star", "Gus" } : new[] { "Star" },
        BodyForm.Urchin => band == 2 ? new[] { "Spines", "Urchin" } : new[] { "Urchin" },
        BodyForm.MoonJelly => band == 0 ? new[] { "Jelly", "Bell" } : new[] { "Bell" },
        BodyForm.Siphonophore => band == 2 ? new[] { "Siphonophore" } : new[] { "Curtain" },
        BodyForm.SeaHare => band == 0 ? new[] { "Slug", "Hare" } : new[] { "Hare" },
        _ => band == 0 ? new[] { "Snail" } : new[] { "Whelk" },
    };

    public static string Name(BossSpec spec, Rng rng)
    {
        int band = BossGenerator.Band(spec.Depth);
        string epithet = FromMutation(spec, band) is { } m && rng.NextFloat() < 0.5f ? m : Epithets[band][rng.Int(Epithets[band].Length)];
        var nouns = Nouns(spec, band);
        string noun = nouns[rng.Int(nouns.Length)];
        string name = $"{epithet} {noun}";
        // Queen Clam is the reference roll's alone.
        return name == "Queen Clam" ? "Old Clam" : name;
    }

    /// <summary>Words a boss name never carries (No-Villain test, THEME-BIBLE §12).</summary>
    public static readonly string[] Forbidden = { "Evil", "Grinning", "Wicked", "Cruel", "Leak", "Dark Lord" };

    public static bool Allowed(string name) => !Forbidden.Any(w => name.Contains(w, System.StringComparison.OrdinalIgnoreCase));
}
