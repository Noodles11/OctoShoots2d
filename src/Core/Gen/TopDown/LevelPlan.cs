using System;
using System.Collections.Generic;
using System.Linq;
using OctoShoots.Core.Run;

namespace OctoShoots.Core.Gen.TopDown;

/// <summary>
/// Which level of the run (DESIGN-TOPDOWN §8): the loop through the seven depths, the depth (biome) and the level within
/// it. A depth is a chain of 4–5 levels; its boss stands on the last one. The first depth (Depth 1 here, the Sunlit
/// Shallows) is the tutorial, Depth 0 to the player: two levels, no boss; runs start below it (Depth 2 here, Depth 1 to
/// the player) once she has passed it. Depth is kept 1-based in code and saves; only its name is one less.
/// </summary>
public readonly record struct LevelId(int Cycle, int Depth, int Level)
{
    /// <summary>The tutorial's first level: where a new player's first run starts.</summary>
    public static readonly LevelId First = new(1, LevelPlan.Tutorial, 1);
    /// <summary>Where a run starts once she has passed the tutorial: the first real depth.</summary>
    public static readonly LevelId FirstRun = new(1, LevelPlan.Tutorial + 1, 1);

    /// <summary>The depth's number as the player sees it: the tutorial is Depth 0.</summary>
    public int ShownDepth => Depth - LevelPlan.Tutorial;

    public bool IsTutorial => Depth == LevelPlan.Tutorial;

    public override string ToString() => Cycle > 1 ? $"Cycle {Cycle} · Depth {ShownDepth} · Level {Level}" : $"Depth {ShownDepth} · Level {Level}";
}

/// <summary>
/// What a level holds (DESIGN-TOPDOWN §4.1 stage 1): rolled per level from the run seed, before the level is shaped.
/// At most <see cref="MaxPlaces"/> places besides the start and the exit; over the cap, places are dropped in the order
/// ambushes, curse den, secret, shell cache.
/// </summary>
public static class LevelStocking
{
    public const int MaxPlaces = 5;
    /// <summary>
    /// At most two treasure rooms per depth: always one on level 1, and this often a second on one level from 2 to the
    /// boss level (4 or 5).
    /// </summary>
    public const float SecondTreasure = 0.5f;
    public const float Secret = 0.4f, Curse = 0.3f;
    /// <summary>From level 2 of a depth: a shop, and a guarded shell cache.</summary>
    public const float Shop = 0.45f, Cache = 0.6f;
    public const int MaxAmbushes = 2;
    /// <summary>The tutorial's two levels, between them every kind of place: level 1 a clam room, a secret, a shell cache
    /// and an ambush; level 2 a shop, a curse den, an ambush and the open rift (the arena with no boss).</summary>
    public const int TutorialLevels = 2;
    /// <summary>The boss stands on level 4 this often; otherwise on level 5.</summary>
    public const float BossOnFour = 0.5f;
}

/// <summary>
/// A level's plan, from the run seed and its <see cref="LevelId"/> alone: whether its boss stands here, what places it
/// holds, its Menace, and the seeds of the two stamps it shares with its neighbours — the rock around its start (shared
/// with the hole of the level above) and the rock around its exit (shared with the start of the level below). Any level
/// can be generated on its own; no level needs another one generated first.
/// </summary>
public sealed class LevelPlan
{
    public const int Depths = 7;
    /// <summary>The tutorial depth (Depth 0 to the player): never part of a loop after the first.</summary>
    public const int Tutorial = 1;

    public LevelId Id { get; init; }
    /// <summary>The exit is an arena round the Crack: a boss level, or the tutorial's open rift.</summary>
    public bool HasBoss { get; init; }
    /// <summary>The tutorial's last level: its arena holds no boss, and the Crack in it stands open (with HasBoss).</summary>
    public bool Rift { get; init; }
    public int Treasures { get; init; }
    public int Secrets { get; init; }
    public int Curses { get; init; }
    public int Shops { get; init; }
    public int Caches { get; init; }
    public int Ambushes { get; init; }
    /// <summary>The rock around the start: the same stamp as the exit of the level above.</summary>
    public ulong EntryStamp { get; init; }
    /// <summary>The level above was a boss level: she arrives on its arena's floor.</summary>
    public bool EntryFromArena { get; init; }
    /// <summary>The rock around the exit: the same stamp as the start of the level below.</summary>
    public ulong ExitStamp { get; init; }
    /// <summary>
    /// 0 at the first real depth (and in the tutorial), 1 at the last, and on up by the same step through every later
    /// loop (DESIGN-TOPDOWN §6.2).
    /// </summary>
    public float Menace { get; init; }
    public LevelId Next { get; init; }

    public int Places => Treasures + Secrets + Curses + Shops + Caches + Ambushes + (HasBoss ? 1 : 0);
    /// <summary>The level's template (docs/LEVEL-TEMPLATES-PROPOSAL.md): null in the tutorial.</summary>
    public LevelTemplate? Template { get; init; }

    /// <summary>The level of this depth (in this loop) where its boss stands: 4 or 5 (the tutorial's rift: its 2nd).</summary>
    public static int BossLevel(SeedCode seed, int cycle, int depth) =>
        depth == Tutorial ? LevelStocking.TutorialLevels : new RunStreams(seed).BossRoll(cycle, depth).NextFloat() < LevelStocking.BossOnFour ? 4 : 5;

    /// <summary>The level of this depth (in this loop) with its second treasure room (2 to the boss level), or 0 for none.</summary>
    public static int SecondTreasureLevel(SeedCode seed, int cycle, int depth)
    {
        if (depth == Tutorial) return 0;
        var rng = new RunStreams(seed).TreasureRoll(cycle, depth);
        if (rng.NextFloat() >= LevelStocking.SecondTreasure) return 0;
        int boss = BossLevel(seed, cycle, depth);
        return 2 + rng.Int(boss - 1);
    }

    public static LevelId NextOf(SeedCode seed, LevelId id)
    {
        if (id.Level < BossLevel(seed, id.Cycle, id.Depth)) return id with { Level = id.Level + 1 };
        // After the last depth the loop begins again below the tutorial.
        return id.Depth < Depths ? new LevelId(id.Cycle, id.Depth + 1, 1) : new LevelId(id.Cycle + 1, Tutorial + 1, 1);
    }

    /// <summary>The level she dived from, or null for the first level of the run.</summary>
    public static LevelId? PreviousOf(SeedCode seed, LevelId id)
    {
        if (id.Level > 1) return id with { Level = id.Level - 1 };
        // Only the first loop has the tutorial above its first real depth.
        if (id.Depth > (id.Cycle > 1 ? Tutorial + 1 : 1)) return new LevelId(id.Cycle, id.Depth - 1, BossLevel(seed, id.Cycle, id.Depth - 1));
        if (id.Cycle > 1) return new LevelId(id.Cycle - 1, Depths, BossLevel(seed, id.Cycle - 1, Depths));
        return null;
    }

    public static float MenaceOf(LevelId id) =>
        id.Depth == Tutorial ? 0f : ((id.Cycle - 1) * (Depths - 1) + id.Depth - Tutorial - 1) / (float)(Depths - Tutorial - 1);

    public static LevelPlan For(SeedCode seed, LevelId id)
    {
        var streams = new RunStreams(seed);
        var rng = streams.Plan(id);
        if (id.Depth == Tutorial) return Tutorial_(seed, streams, id);
        bool boss = id.Level == BossLevel(seed, id.Cycle, id.Depth);
        bool first = id.Level == 1;
        var (templates, treasureLevels) = DepthTemplates(seed, id.Cycle, id.Depth);
        var template = templates[id.Level - 1];
        int treasures = treasureLevels.Contains(id.Level) ? 1 : 0;
        int secrets = rng.NextFloat() < LevelStocking.Secret ? 1 : 0;
        int curses = rng.NextFloat() < LevelStocking.Curse ? 1 : 0;
        int shops = !first && rng.NextFloat() < LevelStocking.Shop ? 1 : 0;
        int caches = !first && rng.NextFloat() < LevelStocking.Cache ? 1 : 0;
        int ambushes = rng.Int(LevelStocking.MaxAmbushes + 1);

        // The template's places over the general roll: required, a chance, or none (treasure rooms keep the depth's rule).
        var required = new HashSet<PoiKind>();
        int Apply(PoiKind kind, int general)
        {
            if (template is null || !template.Places.TryGetValue(kind, out var rule)) return general;
            if (rule.None) return 0;
            if (rule.Required > 0)
            {
                required.Add(kind);
                return rule.Required;
            }
            return rule.Chance is { } p ? (rng.NextFloat() < p ? 1 : 0) : general;
        }
        secrets = Apply(PoiKind.Secret, secrets);
        curses = Apply(PoiKind.CurseDen, curses);
        shops = Apply(PoiKind.Shop, shops);
        caches = Apply(PoiKind.ShellCache, caches);
        ambushes = Apply(PoiKind.Ambush, ambushes);
        // The general rule wins: a depth's first level has no shop and no shell cache.
        if (first) shops = caches = 0;

        // Over the cap: drop ambushes, then the curse den, the secret, the shell cache — what the template requires last.
        // Treasure rooms stay.
        int Total() => treasures + secrets + curses + shops + caches + ambushes + (boss ? 1 : 0);
        for (int pass = 0; pass < 2 && Total() > LevelStocking.MaxPlaces; pass++)
            while (Total() > LevelStocking.MaxPlaces)
            {
                bool Free(PoiKind k) => pass == 1 || !required.Contains(k);
                if (ambushes > 0 && Free(PoiKind.Ambush)) ambushes--;
                else if (curses > 0 && Free(PoiKind.CurseDen)) curses--;
                else if (secrets > 0 && Free(PoiKind.Secret)) secrets--;
                else if (caches > 0 && Free(PoiKind.ShellCache)) caches--;
                else break;
            }

        var previous = PreviousOf(seed, id);
        var next = NextOf(seed, id);
        return new LevelPlan
        {
            Id = id,
            HasBoss = boss,
            Treasures = treasures,
            Secrets = secrets,
            Curses = curses,
            Shops = shops,
            Caches = caches,
            Ambushes = ambushes,
            EntryStamp = streams.StampSeed(id),
            EntryFromArena = previous is { } prev && prev.Level == BossLevel(seed, prev.Cycle, prev.Depth),
            ExitStamp = streams.StampSeed(next),
            Menace = MenaceOf(id),
            Next = next,
            Template = template,
        };
    }

    /// <summary>
    /// The templates of a depth's levels (from its own stream, so any level's is known on its own), and the levels that
    /// hold its treasure rooms. Each level draws from the templates that fit its depth and slot (the first level, a
    /// middle one, the one before the boss, the boss's), by weight: never the same as the level before, no more of one
    /// than its MaxPerDepth, at least three different ones in a depth of three or more, and never one that forbids the
    /// treasure room a level must hold. The first level always holds one; a template that requires one elsewhere takes
    /// the depth's second (at most one such), else the second falls where the general roll put it.
    /// </summary>
    public static (LevelTemplate?[] Templates, HashSet<int> TreasureLevels) DepthTemplates(SeedCode seed, int cycle, int depth)
    {
        int boss = BossLevel(seed, cycle, depth);
        int second = SecondTreasureLevel(seed, cycle, depth);
        var all = LevelTemplates.All;
        var fallback = LevelTemplates.Get("default");
        var rng = new RunStreams(seed).TemplateRoll(cycle, depth);
        int shown = depth - Tutorial;
        for (int attempt = 0; ; attempt++)
        {
            var picked = new LevelTemplate?[boss];
            var treasures = new HashSet<int> { 1 };
            bool secondFree = true;
            for (int level = 1; level <= boss; level++)
            {
                var slot = level == boss ? TemplateSlot.Boss : level == 1 ? TemplateSlot.First : level == boss - 1 ? TemplateSlot.BeforeBoss : TemplateSlot.Middle;
                bool mustTreasure = level == 1 || secondFree && level == second;
                var previous = level > 1 ? picked[level - 2] : null;
                var fits = all.Where(t => t.Fits(shown, slot) && t != previous
                    && (t.MaxPerDepth <= 0 || picked.Count(x => x == t) < t.MaxPerDepth)
                    && !(mustTreasure && t.Forbids(PoiKind.TreasureCave))
                    && !(!mustTreasure && t.Requires(PoiKind.TreasureCave) && !secondFree)).ToList();
                LevelTemplate? choice = fallback;
                float sum = fits.Sum(t => t.WeightOn(slot));
                if (sum > 0f)
                {
                    float roll = rng.NextFloat() * sum;
                    foreach (var t in fits)
                    {
                        roll -= t.WeightOn(slot);
                        choice = t;
                        if (roll <= 0f) break;
                    }
                }
                picked[level - 1] = choice;
                if (level == second && secondFree && !(choice?.Forbids(PoiKind.TreasureCave) ?? false))
                {
                    treasures.Add(level);
                    secondFree = false;
                }
                else if (level > 1 && choice is not null && choice.Requires(PoiKind.TreasureCave) && secondFree)
                {
                    treasures.Add(level);
                    secondFree = false;
                }
            }
            // A depth of three levels or more shows at least three different ones (a few rolls, then as it is).
            if (boss < 3 || picked.Distinct().Count() >= 3 || attempt >= 12) return (picked, treasures);
        }
    }

    /// <summary>The tutorial's levels: every kind of place between its two, set rather than rolled.</summary>
    static LevelPlan Tutorial_(SeedCode seed, RunStreams streams, LevelId id)
    {
        bool rift = id.Level >= LevelStocking.TutorialLevels;
        var next = NextOf(seed, id);
        return new LevelPlan
        {
            Id = id,
            HasBoss = rift,
            Rift = rift,
            Treasures = rift ? 0 : 1,
            Secrets = rift ? 0 : 1,
            Caches = rift ? 0 : 1,
            Shops = rift ? 1 : 0,
            Curses = rift ? 1 : 0,
            Ambushes = 1,
            EntryStamp = streams.StampSeed(id),
            EntryFromArena = false,
            ExitStamp = streams.StampSeed(next),
            Menace = 0f,
            Next = next,
        };
    }
}
