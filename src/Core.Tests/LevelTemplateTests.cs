using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Core.Run;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Level templates (docs/LEVEL-TEMPLATES-PROPOSAL.md): rolled per depth, and what they make of a level.</summary>
public class LevelTemplateTests
{
    static readonly string[] Seeds = { "KELP7Q2Z", "REEF2345", "TANKHAND" };
    static readonly PlaneOptions War = new() { Corruption = true, Pufferlings = false };

    [Fact]
    public void TheCatalogueLoadsWithItsDefault()
    {
        var all = LevelTemplates.All;
        Assert.True(all.Count >= 9);
        Assert.NotNull(LevelTemplates.Get("default"));
        Assert.Equal(all.Count, all.Select(t => t.Id).Distinct().Count());
        Assert.Contains(all, t => t.Slots.Contains(TemplateSlot.Boss));
    }

    [Fact]
    public void EachDepthRollsItsTemplatesOnceAndWithVariety()
    {
        foreach (string s in Seeds)
        {
            var seed = SeedCode.Parse(s);
            for (int depth = LevelPlan.Tutorial + 1; depth <= LevelPlan.Depths; depth++)
            {
                var (templates, treasures) = LevelPlan.DepthTemplates(seed, 1, depth);
                // The same again (any level's plan is made on its own).
                Assert.Equal(templates, LevelPlan.DepthTemplates(seed, 1, depth).Templates);
                for (int i = 1; i < templates.Length; i++)
                    if (templates[i]?.Id != "default") Assert.NotEqual(templates[i - 1], templates[i]);
                if (templates.Length >= 3) Assert.True(templates.Distinct().Count() >= 3, $"{s} depth {depth}: {string.Join(", ", templates.Select(t => t?.Id))}");
                // Every level's template fits its slot, and none forbids a clam room where the level holds one.
                for (int level = 1; level <= templates.Length; level++)
                {
                    var t = templates[level - 1]!;
                    var plan = LevelPlan.For(seed, new LevelId(1, depth, level));
                    Assert.Same(t, plan.Template);
                    if (plan.Treasures > 0) Assert.False(t.Forbids(PoiKind.TreasureCave));
                    if (level == templates.Length) Assert.Contains(TemplateSlot.Boss, t.Slots);
                    else if (level == 1) Assert.Contains(TemplateSlot.First, t.Slots);
                }
                Assert.Contains(1, treasures);
                Assert.InRange(treasures.Count, 1, 2);
            }
        }
    }

    [Fact]
    public void ATemplatesPlacesAndCastHold()
    {
        var seen = new HashSet<string>();
        foreach (string s in Seeds.Take(2))
        {
            var seed = SeedCode.Parse(s);
            var streams = new RunStreams(seed);
            for (int depth = LevelPlan.Tutorial + 1; depth <= LevelPlan.Depths; depth++)
            for (int level = 1; level <= LevelPlan.BossLevel(seed, 1, depth); level++)
            {
                var id = new LevelId(1, depth, level);
                var plan = LevelPlan.For(seed, id);
                var t = plan.Template!;
                // Each template once (its first level on these seeds).
                if (t.Id == "default" || !seen.Add(t.Id)) continue;
                // Its places: required ones there (unless the cap or level 1 took them), forbidden ones not.
                var counts = new Dictionary<PoiKind, int>
                {
                    [PoiKind.Secret] = plan.Secrets, [PoiKind.CurseDen] = plan.Curses, [PoiKind.Shop] = plan.Shops,
                    [PoiKind.ShellCache] = plan.Caches, [PoiKind.TreasureCave] = plan.Treasures,
                };
                foreach (var (kind, rule) in t.Places)
                {
                    if (rule.None) Assert.Equal(0, counts[kind]);
                    if (rule.Required > 0 && kind is not (PoiKind.TreasureCave or PoiKind.Shop or PoiKind.ShellCache))
                        Assert.Equal(rule.Required, counts[kind]);
                }
                // Its cast: forbidden kinds absent, the others never past their range.
                var w = new PlaneWorld(TopDownGenerator.Generate(streams, id), new Tuning(), null, War);
                var cast = new Dictionary<CastKind, int>
                {
                    [CastKind.Blightroot] = w.Blightroots.Count, [CastKind.Blotter] = w.Blotters.Count,
                    [CastKind.Thumpsquirt] = w.Thumpsquirts.Count, [CastKind.Inkwarden] = w.Inkwardens.Count, [CastKind.Lashjaw] = w.Lashjaws.Count,
                };
                foreach (var (kind, rule) in t.Cast)
                {
                    if (rule.None) Assert.True(cast[kind] == 0, $"{t.Id}: {kind} should be absent ({cast[kind]})");
                    else Assert.True(cast[kind] <= rule.Count[^1] + (int)MathF.Round(rule.PerMenace * plan.Menace), $"{t.Id}: {cast[kind]} {kind}");
                }
                // A prime waiting by its place from the start.
                if (t.Rules.Prime is { } at && w.Map.Pois.FirstOrDefault(p => p.Kind == at) is { } place)
                    Assert.Contains(w.Murklings, m => m.Prime && !m.Budding && Vector2.Distance(m.Position, place.Position) < 10f + place.Radius);
            }
        }
        Assert.Contains("hoard", seen);
        Assert.Contains("long_haul", seen);
    }
}
