namespace OctoShoots.Core.Run;

/// <summary>
/// Independent RNG streams derived from the run seed (2D §10). Each is created fresh from its name,
/// so what one system draws never shifts another: killing foes in a different order cannot change
/// what the next treasure chamber holds.
/// </summary>
public sealed class RunStreams
{
    readonly Rng _root;

    public RunStreams(SeedCode seed)
    {
        Seed = seed;
        _root = new Rng(seed.Value);
    }

    public SeedCode Seed { get; }

    public Rng Layout(int depth, int reef) => _root.Stream($"layout/{depth}/{reef}");

    /// <summary>Retry streams for layouts that fail validation; attempt 0 is the plain layout stream.</summary>
    public Rng Layout(int depth, int reef, int attempt) => attempt == 0 ? Layout(depth, reef) : _root.Stream($"layout/{depth}/{reef}/{attempt}");

    public Rng Chamber(int depth, int reef, int chamber) => _root.Stream($"chamber/{depth}/{reef}/{chamber}");
    public Rng Items(int depth, int reef, int chamber) => _root.Stream($"items/{depth}/{reef}/{chamber}");
    public Rng Pickups(int depth, int reef) => _root.Stream($"pickups/{depth}/{reef}");
    public Rng Chest(int depth, int reef, int chest) => _root.Stream($"chest/{depth}/{reef}/{chest}");
    public Rng Shell(int depth, int reef, int shell) => _root.Stream($"shell/{depth}/{reef}/{shell}");
    public Rng Nests(int depth, int reef) => _root.Stream($"nests/{depth}/{reef}");
    public Rng Dens(int depth, int reef) => _root.Stream($"dens/{depth}/{reef}");
    public Rng Critters(int depth, int reef) => _root.Stream($"critters/{depth}/{reef}");
    /// <summary>One stage of the top-down level generator (DESIGN-TOPDOWN §4.1); each attempt gets fresh streams.</summary>
    public Rng TopDown(int depth, int reef, int attempt, string stage) => _root.Stream($"topdown/{depth}/{reef}/{attempt}/{stage}");
    public Rng Snacks() => _root.Stream("snacks");
    public Rng EnemyAi() => _root.Stream("enemyAI");
    public Rng Combat() => _root.Stream("combat");
    public Rng Cosmetic() => _root.Stream("cosmetic");
}
