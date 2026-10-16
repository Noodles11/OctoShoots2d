using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OctoShoots.Core.Bosses;

/// <summary>
/// An authored fight idea (docs/BOSS-GENERATOR.md §4): which bodies carry it, what the player must work out, and the
/// fixed core of its fight. Everything else is rolled around it.
/// </summary>
public sealed record ConceptDef(
    BossConcept Id, BodyForm[] Forms, BossTier Tier, Exposure Exposure, BossAttack Signature, BossMove Move, BossTempo Tempo,
    BossSense Sense, BodySite Pearl, int Volleys, float Rest, float Window, string[] Tags, PhaseChange[] Changes);

/// <summary>
/// Grows a boss from the run seed, the loop and the depth (docs/BOSS-GENERATOR.md §2): band → body → concept → tier →
/// anatomy → mutations → pearl → fight → spice → name, then the validator; a failed roll is re-rolled from the anatomy
/// with the next sub-seed, and the concept's plainest roll is the last fallback (it always passes).
/// </summary>
public static class BossGenerator
{
    public const int Attempts = 24;

    static readonly BodyForm[] Clams = { BodyForm.GiantClam };

    /// <summary>The concepts (§4). Pearl sites are in the body's polar frame (angle from the facing, out of the radius).</summary>
    public static readonly ConceptDef[] Concepts =
    {
        new(BossConcept.TheLid, Clams, BossTier.Common, Exposure.OpenMoments, BossAttack.Ring, BossMove.Hold, BossTempo.Metronome,
            BossSense.Blind, new BodySite(0f, 0f, 1f, false), 4, 3f, 0f, new[] { "radial", "stationary", "lid" },
            new[] { PhaseChange.NewAttack, PhaseChange.Quicken }),
        new(BossConcept.SnappingGate, Clams, BossTier.Common, Exposure.OpenMoments, BossAttack.Helix, BossMove.Turn, BossTempo.Duelist,
            BossSense.Water, new BodySite(0f, 0.15f, 1f, false), 2, 2.6f, 0f, new[] { "close", "lid", "stationary" },
            new[] { PhaseChange.NewAttack, PhaseChange.ShorterWindow, PhaseChange.Quicken }),
        new(BossConcept.TurningStar, new[] { BodyForm.SeaStar, BodyForm.Urchin }, BossTier.Common, Exposure.MovingWindow, BossAttack.Ring,
            BossMove.Spin, BossTempo.Metronome, BossSense.Blind, new BodySite(0f, 0.72f, 1f, false), 3, 2.4f, 0f,
            new[] { "radial", "stationary", "armoured" }, new[] { PhaseChange.NewAttack, PhaseChange.Quicken, PhaseChange.MutationAdvances }),
        new(BossConcept.SlamAndSigh, new[] { BodyForm.SeaStar }, BossTier.Common, Exposure.Stagger, BossAttack.PulseRing, BossMove.Creep,
            BossTempo.SurgeAndRest, BossSense.Vibration, new BodySite(0f, 0f, 1f, false), 2, 2.2f, 2.2f,
            new[] { "radial", "spreader", "close" }, new[] { PhaseChange.NewAttack, PhaseChange.ShorterWindow, PhaseChange.MutationAdvances }),
        new(BossConcept.PulseBell, new[] { BodyForm.MoonJelly }, BossTier.Common, Exposure.OpenMoments, BossAttack.Ring, BossMove.DriftPulse,
            BossTempo.Metronome, BossSense.Water, new BodySite(0f, 0f, 1f, false), 3, 2.6f, 0f, new[] { "radial", "drifting", "soft" },
            new[] { PhaseChange.NewAttack, PhaseChange.Quicken, PhaseChange.MutationAdvances }),
        new(BossConcept.TrailingCurtain, new[] { BodyForm.Siphonophore }, BossTier.Rare, Exposure.MovingWindow, BossAttack.Wall, BossMove.Circle,
            BossTempo.Relentless, BossSense.Water, new BodySite(0f, 0.8f, 1f, false), 3, 2f, 0f, new[] { "ranged", "drifting", "soft" },
            new[] { PhaseChange.NewAttack, PhaseChange.Quicken }),
        new(BossConcept.TheSpreader, new[] { BodyForm.SeaHare }, BossTier.Common, Exposure.Stagger, BossAttack.ArcBombs, BossMove.Creep,
            BossTempo.SurgeAndRest, BossSense.Vibration, new BodySite(0f, 0.3f, 1f, false), 2, 2.4f, 2.4f,
            new[] { "spreader", "soft", "ranged" }, new[] { PhaseChange.NewAttack, PhaseChange.ShorterWindow, PhaseChange.MutationAdvances }),
        new(BossConcept.ShyShell, new[] { BodyForm.Snail }, BossTier.Rare, Exposure.OpenMoments, BossAttack.Homing, BossMove.Creep,
            BossTempo.Duelist, BossSense.Water, new BodySite(0f, 0.55f, 1f, false), 3, 2.6f, 0f, new[] { "armoured", "ranged", "soft" },
            new[] { PhaseChange.NewAttack, PhaseChange.Quicken }),
        new(BossConcept.BoilHeart, new[] { BodyForm.SeaHare, BodyForm.Snail, BodyForm.MoonJelly }, BossTier.Legendary, Exposure.CoaxOut,
            BossAttack.SacBurst, BossMove.Creep, BossTempo.Territorial, BossSense.Blind, new BodySite(0f, 0f, 1f, false), 2, 2.4f, 2.8f,
            new[] { "sac-heavy", "soft", "close" }, new[] { PhaseChange.MutationAdvances, PhaseChange.NewAttack }),
    };

    public static ConceptDef Concept(BossConcept id) => Concepts.First(c => c.Id == id);

    public static BodyPlan PlanOf(BodyForm form) => form switch
    {
        BodyForm.GiantClam => BodyPlan.Bivalve,
        BodyForm.SeaStar or BodyForm.Urchin => BodyPlan.Echinoderm,
        BodyForm.MoonJelly or BodyForm.Siphonophore => BodyPlan.Cnidarian,
        _ => BodyPlan.Gastropod,
    };

    /// <summary>What each body can throw: an attack only comes from anatomy that could make it (§7.1).</summary>
    public static bool Can(BodyForm form, BossAttack attack) => attack switch
    {
        BossAttack.Ring => form is not (BodyForm.SeaHare or BodyForm.Snail),
        BossAttack.Wall => form is BodyForm.GiantClam or BodyForm.Siphonophore,
        BossAttack.Helix => form is BodyForm.GiantClam or BodyForm.SeaStar or BodyForm.MoonJelly or BodyForm.SeaHare,
        BossAttack.Homing => form is BodyForm.GiantClam or BodyForm.MoonJelly or BodyForm.Siphonophore or BodyForm.Snail or BodyForm.SeaHare,
        BossAttack.Snap => form is BodyForm.GiantClam,
        BossAttack.ArcBombs => form is BodyForm.SeaStar or BodyForm.Urchin or BodyForm.SeaHare or BodyForm.Snail,
        BossAttack.PulseRing => form is BodyForm.SeaStar or BodyForm.Urchin or BodyForm.MoonJelly or BodyForm.SeaHare,
        BossAttack.SacBurst => true,
        _ => false,
    };

    static readonly Dictionary<BossAttack, string[]> AttackTags = new()
    {
        [BossAttack.Ring] = new[] { "radial", "stationary" },
        [BossAttack.Wall] = new[] { "ranged", "lid" },
        [BossAttack.Helix] = new[] { "radial", "drifting" },
        [BossAttack.Homing] = new[] { "ranged", "soft" },
        [BossAttack.Snap] = new[] { "close", "lid" },
        [BossAttack.ArcBombs] = new[] { "spreader", "ranged" },
        [BossAttack.PulseRing] = new[] { "radial", "spreader", "close" },
        [BossAttack.SacBurst] = new[] { "sac-heavy", "close" },
    };

    /// <summary>The mutation themes each body can grow (§5).</summary>
    public static MutationTheme[] ThemesOf(BodyForm form) => PlanOf(form) switch
    {
        BodyPlan.Bivalve => new[] { MutationTheme.OvergrownShell, MutationTheme.InkSacs, MutationTheme.ExtraLid },
        BodyPlan.Echinoderm => form == BodyForm.SeaStar
            ? new[] { MutationTheme.ExtraArms, MutationTheme.InkSacs, MutationTheme.InvertedSpines }
            : new[] { MutationTheme.InvertedSpines, MutationTheme.InkSacs, MutationTheme.ExtraArms },
        BodyPlan.Cnidarian => new[] { MutationTheme.FusedBells, MutationTheme.ExtraTentacles, MutationTheme.InkSacs },
        _ => form == BodyForm.Snail
            ? new[] { MutationTheme.OvergrownShell, MutationTheme.InkSacs, MutationTheme.ExtraFlaps }
            : new[] { MutationTheme.ExtraFlaps, MutationTheme.InkSacs, MutationTheme.ExtraTentacles },
    };

    /// <summary>The body's mutated radius range (m), before depth.</summary>
    public static (float Min, float Max) SizeOf(BodyForm form) => form switch
    {
        BodyForm.GiantClam => (2.3f, 2.7f),
        BodyForm.SeaStar => (2.2f, 2.8f),
        BodyForm.Urchin => (1.9f, 2.4f),
        BodyForm.MoonJelly => (2f, 2.5f),
        BodyForm.Siphonophore => (1.8f, 2.2f),
        BodyForm.SeaHare => (2.1f, 2.6f),
        _ => (1.9f, 2.4f),
    };

    /// <summary>Its true colours (linear RGB): body and accent.</summary>
    static (Vector3 Body, Vector3 Accent) TrueColours(BodyForm form, Rng rng)
    {
        Vector3 C(float r, float g, float b) => new(r, g, b);
        return form switch
        {
            BodyForm.GiantClam => (C(0.78f, 0.72f, 0.6f), rng.NextFloat() < 0.5f ? C(0.15f, 0.55f, 0.7f) : C(0.35f, 0.6f, 0.3f)),
            BodyForm.SeaStar => rng.NextFloat() < 0.5f ? (C(0.95f, 0.45f, 0.2f), C(1f, 0.75f, 0.5f)) : (C(0.75f, 0.25f, 0.35f), C(0.95f, 0.6f, 0.55f)),
            BodyForm.Urchin => (C(0.4f, 0.18f, 0.45f), C(0.75f, 0.5f, 0.8f)),
            BodyForm.MoonJelly => (C(0.85f, 0.88f, 0.95f), C(0.95f, 0.55f, 0.7f)),
            BodyForm.Siphonophore => (C(0.75f, 0.85f, 0.95f), C(0.95f, 0.5f, 0.35f)),
            BodyForm.SeaHare => (C(0.45f, 0.38f, 0.22f), C(0.85f, 0.82f, 0.7f)),
            _ => (C(0.9f, 0.82f, 0.68f), C(0.45f, 0.42f, 0.4f)),
        };
    }

    /// <summary>The band a depth is in (§2): 0 early (Depths 1–2), 1 middle (3), 2 deep (4–6).</summary>
    public static int Band(int depth) => depth <= 2 ? 0 : depth == 3 ? 1 : 2;

    /// <summary>Menace by the depth the player sees: 0 at Depth 1, 1 at Depth 6, and on with each loop.</summary>
    public static float MenaceOf(int cycle, int depth) => ((cycle - 1) * 6 + depth - 1) / 5f;

    /// <summary>The boss of this loop's depth (the depth as the player sees it, 1–6).</summary>
    public static BossSpec For(ulong runSeed, int cycle, int depth)
    {
        var root = new Rng(runSeed).Stream($"boss-generator/{cycle}/{depth}");
        float menace = MenaceOf(cycle, depth);
        int band = Band(depth);

        var tierRoll = root.Stream("tier").NextFloat();
        var tier = tierRoll < 0.05f ? BossTier.Legendary : tierRoll < 0.30f ? BossTier.Rare : BossTier.Common;
        var plan = Weighted(root.Stream("plan"), new[]
        {
            (BodyPlan.Bivalve, new[] { 30f, 15f, 5f }[band]),
            (BodyPlan.Echinoderm, new[] { 30f, 20f, 10f }[band]),
            (BodyPlan.Cnidarian, new[] { 20f, 25f, 25f }[band]),
            (BodyPlan.Gastropod, new[] { 20f, 25f, 20f }[band]),
        });
        // The concepts this body carries at this tier or below (a Legendary may still roll a Common concept).
        var concepts = Concepts.Where(c => c.Forms.Any(f => PlanOf(f) == plan) && c.Tier <= tier).ToList();
        var concept = Weighted(root.Stream("concept"), concepts.Select(c => (c, c.Tier == tier ? 2f : 1f)).ToArray());
        var form = concept.Forms.Where(f => PlanOf(f) == plan).ToArray() is { Length: > 0 } forms
            ? forms[root.Stream("form").Int(forms.Length)] : concept.Forms[0];

        // The plainest Bivalve of the early depths may be Queen Clam herself (the reference roll).
        if (concept.Id == BossConcept.TheLid && band == 0 && root.Stream("queen").NextFloat() < 0.5f)
            return QueenClam(runSeed, cycle, depth);

        for (int attempt = 0; attempt < Attempts; attempt++)
        {
            var spec = Roll(root.Stream($"attempt/{attempt}"), runSeed, cycle, depth, menace, tier, form, concept, plain: false);
            if (BossValidator.Validate(spec) is null) return spec;
        }
        return Roll(root.Stream("plain"), runSeed, cycle, depth, menace, BossTier.Common, form, concept, plain: true);
    }

    /// <summary>A boss of a named concept (tests, debug starts): rolled as For rolls, on one of the concept's bodies.</summary>
    public static BossSpec ForConcept(ulong runSeed, int cycle, int depth, BossConcept id, BossTier? tier = null)
    {
        var root = new Rng(runSeed).Stream($"boss-generator/{cycle}/{depth}/concept/{id}");
        var concept = Concept(id);
        var form = concept.Forms[root.Stream("form").Int(concept.Forms.Length)];
        var t = tier is { } given && given >= concept.Tier ? given : concept.Tier;
        float menace = MenaceOf(cycle, depth);
        for (int attempt = 0; attempt < Attempts; attempt++)
        {
            var spec = Roll(root.Stream($"attempt/{attempt}"), runSeed, cycle, depth, menace, t, form, concept, plain: false);
            if (BossValidator.Validate(spec) is null) return spec;
        }
        return Roll(root.Stream("plain"), runSeed, cycle, depth, menace, concept.Tier, form, concept, plain: true);
    }

    /// <summary>
    /// Queen Clam: The Lid with her own hand-tuned numbers (PlaneBossTuning), the roll every generated boss is held to.
    /// </summary>
    public static BossSpec QueenClam(ulong runSeed, int cycle, int depth)
    {
        var rng = new Rng(runSeed).Stream($"boss-generator/{cycle}/{depth}/queen");
        var concept = Concept(BossConcept.TheLid);
        var (body, accent) = TrueColours(BodyForm.GiantClam, rng);
        var spec = new BossSpec
        {
            Seed = rng.Seed, Cycle = cycle, Depth = depth, Menace = MenaceOf(cycle, depth), Tier = BossTier.Common,
            Plan = BodyPlan.Bivalve, Form = BodyForm.GiantClam, Concept = BossConcept.TheLid, Reference = true, Name = "Queen Clam",
            Radius = 2.5f, Stretch = 1f, Count = 18, Bulk = 0.5f, TrueColor = body, Accent = accent, TrueScale = 0.65f,
            Theme = MutationTheme.OvergrownShell, Mutation = 0.3f,
            Exposure = Exposure.OpenMoments, Pearl = concept.Pearl, PearlRadius = 0.9f,
            Hp = 800f, Damage = 12f, Telegraph = 0.9f, Rest = 3f, Volleys = 4, VolleyInterval = 1.2f, PearlSpeed = 7f,
            RingCount = 18, WallCount = 15, HelixArms = 3,
            Signature = BossAttack.Ring, Move = BossMove.Hold, Tempo = BossTempo.Metronome, Sense = BossSense.Blind,
            Spice = BossSpice.ExtraFiller,
        };
        spec.Fillers.Add(BossAttack.Wall);
        // Her second phase: the helix takes the second volley, the royal pearl comes with the first, and she snaps.
        spec.Phases.Add(new BossPhase(0.5f, PhaseChange.Quicken, BossAttack.Helix));
        spec.Phases.Add(new BossPhase(0.5f, PhaseChange.NewAttack, BossAttack.Homing));
        spec.Phases.Add(new BossPhase(0.5f, PhaseChange.NewAttack, BossAttack.Snap));
        // Her shell overgrown along the hinge, a touch of ink in the grooves.
        spec.Sites.Add(new BodySite(MathF.PI, 0.85f, 0.5f, false));
        spec.Sites.Add(new BodySite(MathF.PI * 0.75f, 0.9f, 0.35f, false));
        spec.Sites.Add(new BodySite(-MathF.PI * 0.75f, 0.9f, 0.35f, false));
        return spec;
    }

    /// <summary>One roll of a concept on a body (plain: its middle values, no spice — the fallback that always passes).</summary>
    static BossSpec Roll(Rng rng, ulong runSeed, int cycle, int depth, float menace, BossTier tier, BodyForm form, ConceptDef concept, bool plain)
    {
        float m = MathF.Min(menace, 1.6f);
        float F(float a, float b) => plain ? (a + b) * 0.5f : rng.Range(a, b);
        var (body, accent) = TrueColours(form, rng);
        var (rMin, rMax) = SizeOf(form);
        var spec = new BossSpec
        {
            Seed = rng.Seed, Cycle = cycle, Depth = depth, Menace = menace, Tier = tier, Plan = PlanOf(form), Form = form, Concept = concept.Id,
            Radius = F(rMin, rMax) * (1f + 0.07f * MathF.Min(depth - 1, 5)),
            Stretch = 1f + F(-0.15f, 0.15f),
            TrueColor = body, Accent = accent, TrueScale = F(0.55f, 0.7f),
            Exposure = concept.Exposure, Pearl = concept.Pearl,
            Signature = concept.Signature, Move = concept.Move, Tempo = concept.Tempo, Sense = concept.Sense,
        };
        (spec.Count, spec.Bulk) = form switch
        {
            BodyForm.GiantClam => (16 + (plain ? 2 : rng.Int(5)), F(0.4f, 0.6f)),
            BodyForm.SeaStar => (5 + (plain ? 0 : rng.Int(3)), F(0.55f, 0.85f)),
            BodyForm.Urchin => (10 + (plain ? 2 : rng.Int(5)), F(0.4f, 0.75f)),
            BodyForm.MoonJelly => (4, F(0.35f, 0.6f)),
            BodyForm.Siphonophore => (4 + (plain ? 1 : rng.Int(3)), F(0.4f, 0.7f)),
            BodyForm.SeaHare => (2, F(0.45f, 0.8f)),
            _ => (3 + (plain ? 0 : rng.Int(2)), F(0.45f, 0.75f)),
        };

        // The corruption: one dominant theme (the plan's first two are the common ones), a minor one from Rare on.
        var themes = ThemesOf(form);
        spec.Theme = concept.Id == BossConcept.BoilHeart ? MutationTheme.InkSacs
            : plain ? themes[0] : Weighted(rng, new[] { (themes[0], 3f), (themes[1], 2f), (themes[2], 1f) });
        if (tier >= BossTier.Rare && !plain)
            spec.Minor = themes.Where(t => t != spec.Theme).ElementAt(rng.Int(themes.Length - 1));
        spec.Mutation = Math.Clamp(0.25f + 0.6f * m + (tier == BossTier.Legendary ? 0.1f : 0f), 0f, 1f);
        if (tier >= BossTier.Rare && !plain && rng.NextFloat() < 0.4f) spec.Lopsided = rng.NextFloat() < 0.5f ? -F(0.5f, 1f) : F(0.5f, 1f);
        Grow(spec, rng, plain);

        // The fight's numbers, in the depth's budget (§7.4).
        spec.Hp = 800f * (1f + m);
        spec.Damage = 12f + 6f * m;
        spec.Telegraph = MathF.Max(BossValidator.TelegraphFloor(m), 0.9f - 0.45f * m);
        spec.PearlSpeed = 7f * (1f + 0.3f * m);
        spec.PearlRadius = concept.Exposure == Exposure.MovingWindow ? 0.75f : 0.9f;
        spec.PearlOrbit = concept.Exposure == Exposure.MovingWindow ? F(0.45f, 0.65f) * (rng.NextFloat() < 0.5f || plain ? 1f : -1f) : 0f;
        spec.RingCount = 16 + (int)MathF.Round(6f * m) + (plain ? 1 : rng.Int(3));
        spec.WallCount = 13 + (int)MathF.Round(4f * m);
        spec.HelixArms = m >= 0.6f ? 4 : 3;
        spec.Volleys = concept.Volleys;
        spec.VolleyInterval = 1.2f * (1f - 0.2f * m);
        spec.Rest = concept.Rest * (1f - 0.25f * m);
        spec.Window = concept.Window * (1f - 0.15f * m);
        spec.MoveRate = concept.Move switch
        {
            BossMove.Turn => F(0.6f, 1f),
            BossMove.Spin => F(0.4f, 0.6f) * (rng.NextFloat() < 0.5f || plain ? 1f : -1f),
            BossMove.Creep => F(0.9f, 1.3f) + 0.3f * m,
            BossMove.DriftPulse => F(4f, 5f),
            BossMove.Circle => F(0.12f, 0.18f),
            _ => 0f,
        };
        switch (concept.Tempo)
        {
            case BossTempo.Relentless:
                spec.Rest *= 0.6f;
                spec.Volleys += 1;
                break;
            case BossTempo.SurgeAndRest:
                spec.Volleys += 1;
                spec.VolleyInterval *= 0.75f;
                spec.Window *= 1.25f;
                break;
            case BossTempo.Duelist:
                spec.Rest *= 1.1f;
                break;
        }
        if (concept.Exposure == Exposure.CoaxOut) spec.SacHp = 90f * (1f + 0.5f * m);
        // Snapping Gate punishes her up close while it is open.
        if (concept.Id == BossConcept.SnappingGate) spec.Fillers.Add(BossAttack.Snap);

        // Fillers: by the concept's tags, from what this body can throw.
        int fillers = tier switch { BossTier.Common => 1, _ => 2 };
        if (concept.Id == BossConcept.TheLid && !spec.Fillers.Contains(BossAttack.Wall)) spec.Fillers.Add(BossAttack.Wall);
        AddFillers(spec, rng, concept, fillers, plain);

        // Phases (§7.3): by depth, Legendary three.
        int phases = tier == BossTier.Legendary ? 3 : depth <= 2 ? (plain ? 1 : 1 + rng.Int(2)) : depth == 3 ? 2 : (plain ? 2 : 2 + rng.Int(2));
        float[] at = phases == 2 ? new[] { 0.5f } : phases == 3 ? new[] { 0.34f, 0.67f } : Array.Empty<float>();
        foreach (float when in at)
        {
            var change = plain ? concept.Changes[0] : concept.Changes[rng.Int(concept.Changes.Length)];
            BossAttack? joins = null;
            if (change == PhaseChange.NewAttack)
            {
                var spare = Spare(spec, concept, rng, plain);
                if (spare is { } a) joins = a;
                else change = PhaseChange.Quicken;
            }
            spec.Phases.Add(new BossPhase(when, change, joins));
        }

        // The spice (§4.1).
        if (!plain)
        {
            var spices = new List<BossSpice> { BossSpice.FastLongWindows, BossSpice.OffCentrePearl };
            if (Spare(spec, concept, rng, peek: true) is not null) spices.Add(BossSpice.ExtraFiller);
            if (spec.Phases.Count > 0) spices.Add(BossSpice.EarlyAdvance);
            if (spec.Lopsided != 0f) spices.Add(BossSpice.LopsidedOrigin);
            spec.Spice = spices[rng.Int(spices.Count)];
            switch (spec.Spice)
            {
                case BossSpice.ExtraFiller:
                    if (Spare(spec, concept, rng, plain: false) is { } extra) spec.Fillers.Add(extra);
                    break;
                case BossSpice.FastLongWindows:
                    spec.Rest *= 0.8f;
                    spec.Window *= 1.2f;
                    spec.VolleyInterval *= 1.1f;
                    break;
                case BossSpice.EarlyAdvance:
                    var first = spec.Phases[0];
                    spec.Phases[0] = first with { Change = PhaseChange.MutationAdvances, Joins = first.Joins };
                    break;
                case BossSpice.OffCentrePearl:
                    if (spec.Exposure != Exposure.MovingWindow)
                        spec.Pearl = spec.Pearl with { Angle = rng.Range(-0.6f, 0.6f), Out = MathF.Min(0.45f, spec.Pearl.Out + 0.25f) };
                    break;
            }
        }
        else spec.Spice = BossSpice.FastLongWindows;

        spec.Name = BossNames.Name(spec, rng);
        return spec;
    }

    /// <summary>The mutation's sites on the body: one theme repeated, mirrored (or crowding the grown-out side).</summary>
    static void Grow(BossSpec spec, Rng rng, bool plain)
    {
        int count = 2 + (int)MathF.Round(spec.Mutation * 4f);
        if (spec.Concept == BossConcept.BoilHeart) count = 3 + (int)MathF.Round(spec.Mutation * 2f);
        bool sacs = spec.Theme == MutationTheme.InkSacs;
        float side = spec.Lopsided;
        for (int i = 0; i < count; i++)
        {
            // Pairs mirrored across the facing; a lopsided body crowds its sites onto the grown-out side.
            float a = plain ? MathF.PI * (0.35f + 0.5f * (i / 2) / Math.Max(1, count / 2)) : rng.Range(0.35f, 2.8f);
            if (side != 0f) a = MathF.Abs(a) * MathF.Sign(side) * -1f;
            else if (i % 2 == 1) a = -spec.Sites[^1].Angle;
            float size = (plain ? 0.35f : rng.Range(0.25f, 0.45f)) * (0.7f + 0.6f * spec.Mutation);
            float outward = spec.Concept == BossConcept.BoilHeart ? 0.45f + 0.1f * (i % 2) : plain ? 0.8f : rng.Range(0.65f, 0.95f);
            spec.Sites.Add(new BodySite(a, outward, size, sacs));
        }
        if (spec.Minor == MutationTheme.InkSacs)
            spec.Sites.Add(new BodySite(MathF.PI, 0.7f, 0.3f, true));
    }

    static void AddFillers(BossSpec spec, Rng rng, ConceptDef concept, int count, bool plain)
    {
        for (int i = 0; spec.Fillers.Count(f => f != BossAttack.Snap) < count && i < 8; i++)
            if (Spare(spec, concept, rng, plain) is { } a) spec.Fillers.Add(a);
            else break;
    }

    /// <summary>An attack the body can throw that it does not have yet, preferring the concept's tags (none ignores them).</summary>
    static BossAttack? Spare(BossSpec spec, ConceptDef concept, Rng rng, bool plain = false, bool peek = false)
    {
        var have = spec.Kit.ToHashSet();
        var options = Enum.GetValues<BossAttack>()
            .Where(a => a != BossAttack.Snap && !have.Contains(a) && Can(spec.Form, a) && (a != BossAttack.SacBurst || spec.HasSacs))
            .Select(a => (a, Shared: AttackTags[a].Count(t => concept.Tags.Contains(t))))
            .Where(x => x.Shared > 0)
            .ToList();
        if (options.Count == 0) return null;
        if (peek) return options[0].a;
        if (plain) return options.OrderByDescending(x => x.Shared).ThenBy(x => (int)x.a).First().a;
        return Weighted(rng, options.Select(x => (x.a, 1f + x.Shared)).ToArray());
    }

    static T Weighted<T>(Rng rng, (T Item, float Weight)[] items)
    {
        float total = items.Sum(i => i.Weight), r = rng.NextFloat() * total;
        foreach (var (item, w) in items)
        {
            if (r < w) return item;
            r -= w;
        }
        return items[^1].Item;
    }
}
