using System;
using System.Collections.Generic;
using System.Linq;
using OctoShoots.Core.Bosses;
using Xunit;
using Xunit.Abstractions;

namespace OctoShoots.Core.Tests;

/// <summary>The boss generator (docs/BOSS-GENERATOR.md): seeded, valid, varied, and held to Queen Clam.</summary>
public class BossGeneratorTests
{
    readonly ITestOutputHelper _out;

    public BossGeneratorTests(ITestOutputHelper output) => _out = output;

    static IEnumerable<BossSpec> Many(int seeds = 400)
    {
        for (int s = 0; s < seeds; s++)
        for (int depth = 1; depth <= 5; depth++)
            yield return BossGenerator.For(0x9E3779B97F4A7C15UL * (ulong)(s + 1), 1, depth);
    }

    static string Describe(BossSpec s) =>
        $"{s.Name} ({s.Tier} {s.Form} · {s.Concept}) r {s.Radius:0.0} · {s.Theme}{(s.Minor is { } m ? "+" + m : "")} ×{s.Sites.Count} " +
        $"· {s.Exposure} · {s.Signature} + [{string.Join(", ", s.Fillers)}] · {s.Move} {s.Tempo} {s.Sense} · " +
        $"{s.Phases.Count + 1} phases [{string.Join(", ", s.Phases.Select(p => p.Change + (p.Joins is { } j ? ":" + j : "")))}] · spice {s.Spice} · hp {s.Hp:0}";

    [Fact]
    public void TheSameSeedGrowsTheSameBoss_AnotherLoopOrDepthAnother()
    {
        var a = BossGenerator.For(12345UL, 1, 3);
        var b = BossGenerator.For(12345UL, 1, 3);
        Assert.Equal(Describe(a), Describe(b));
        Assert.Equal(a.Sites, b.Sites);
        Assert.Equal(a.Seed, b.Seed);
        var others = new[] { BossGenerator.For(12345UL, 2, 3), BossGenerator.For(12345UL, 1, 4), BossGenerator.For(54321UL, 1, 3) };
        Assert.Contains(others, o => Describe(o) != Describe(a));
    }

    [Fact]
    public void EveryRollPassesTheValidatorAndQueenClamDoesToo()
    {
        Assert.Null(BossValidator.Validate(BossGenerator.QueenClam(1UL, 1, 1)));
        var failed = Many().Select(s => (s, Why: BossValidator.Validate(s))).Where(x => x.Why is not null).ToList();
        foreach (var (s, why) in failed.Take(10)) _out.WriteLine($"{why}: {Describe(s)}");
        Assert.Empty(failed);
    }

    [Fact]
    public void EveryConceptAndBodyOccurs_TiersNearTheirWeights_EveryBossHasThreeTells()
    {
        var all = Many().ToList();
        foreach (var c in Enum.GetValues<BossConcept>()) Assert.Contains(all, s => s.Concept == c);
        foreach (var f in Enum.GetValues<BodyForm>()) Assert.Contains(all, s => s.Form == f);
        Assert.Contains(all, s => s.Reference);
        float rare = all.Count(s => s.Tier == BossTier.Rare) / (float)all.Count, legendary = all.Count(s => s.Tier == BossTier.Legendary) / (float)all.Count;
        Assert.InRange(rare, 0.18f, 0.32f);
        Assert.InRange(legendary, 0.02f, 0.08f);
        // Wrong anatomy (sites), and the ink look and the drip follow the sites: never a boss without them.
        Assert.All(all, s => Assert.NotEmpty(s.Sites));
        // Coherent: every attack the body can make, and a sac burst only with sacs.
        Assert.All(all, s => Assert.All(s.Kit, a => Assert.True(BossGenerator.Can(s.Form, a))));
        foreach (var s in all.Where(s => s.Depth == 1).Take(12)) _out.WriteLine(Describe(s));
        foreach (var s in all.Where(s => s.Depth == 5).Take(12)) _out.WriteLine(Describe(s));
    }

    [Fact]
    public void DeeperBossesAreHarderAndMoreMutated()
    {
        var shallow = Many(200).Where(s => s.Depth == 1).ToList();
        var deep = Many(200).Where(s => s.Depth == 5).ToList();
        Assert.True(deep.Average(s => s.Hp) > shallow.Average(s => s.Hp) * 1.4f);
        Assert.True(deep.Average(s => s.Mutation) > shallow.Average(s => s.Mutation) + 0.3f);
        Assert.True(deep.Average(s => s.Telegraph) < shallow.Average(s => s.Telegraph));
        Assert.True(deep.Average(s => s.Phases.Count) > shallow.Average(s => s.Phases.Count));
        Assert.All(deep.Concat(shallow), s => Assert.True(s.Telegraph >= BossValidator.TelegraphFloor(s.Menace) - 1e-4f));
    }

    [Fact]
    public void TwoBossesInARowNeverShareABodyAndRarelyAFight()
    {
        int sameFight = 0, pairs = 0;
        for (int s = 0; s < 300; s++)
        {
            ulong seed = 0xC0FFEEUL * (ulong)(s + 1);
            BossSpec? before = null;
            for (int cycle = 1; cycle <= 2; cycle++)
            for (int depth = 1; depth <= BossGenerator.LastDepth; depth++)
            {
                var boss = BossGenerator.For(seed, cycle, depth);
                if (before is not null)
                {
                    pairs++;
                    Assert.NotEqual(before.Plan, boss.Plan);
                    if (boss.Exposure == before.Exposure && boss.Signature == before.Signature) sameFight++;
                }
                before = boss;
            }
        }
        Assert.True(sameFight < pairs * 0.01f, $"{sameFight} of {pairs} pairs fight alike");
    }

    [Fact]
    public void TheValidatorRejectsWhatCannotBeWonOrRead()
    {
        var q = BossGenerator.QueenClam(1UL, 1, 1);
        q.Telegraph = 0.2f;
        Assert.NotNull(BossValidator.Validate(q));
        q = BossGenerator.QueenClam(1UL, 1, 1);
        q.Fillers.Add(BossAttack.SacBurst);
        Assert.NotNull(BossValidator.Validate(q));
        q = BossGenerator.QueenClam(1UL, 1, 1);
        q.Volleys = 1;
        q.VolleyInterval = 0.5f;
        Assert.NotNull(BossValidator.Validate(q));
        q = BossGenerator.QueenClam(1UL, 1, 1);
        q.Name = "Grinning Clam";
        Assert.NotNull(BossValidator.Validate(q));
    }
}
