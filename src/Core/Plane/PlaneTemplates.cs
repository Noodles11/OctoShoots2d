using System;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Core.Plane;

/// <summary>
/// The level template's say over the world (docs/LEVEL-TEMPLATES-PROPOSAL.md): which corrupted kinds it casts, how many
/// and where (each kind's own placement keeps its checks and asks here whether a spot holds its anchor), and its extra
/// rules: a prime waiting by a place, guardians budding round a place, budding faster or slower, more ink, springs,
/// fish, quiet. With no template (the tutorial) or no rule for a kind, the general rules alone apply.
/// </summary>
public sealed partial class PlaneWorld
{
    LevelTemplate? Template => Map.Template;
    TemplateRules? Rules => Map.Template?.Rules;

    /// <summary>The template's rule for a kind (null: its general rule).</summary>
    CastRule? CastOf(CastKind kind) => Template?.CastOf(kind);

    /// <summary>How many of a kind the template casts (rolled), capped by the kind's own maximum.</summary>
    int CastCount(CastRule rule, int max) => Math.Min(rule.Roll(_blightRng, Menace), max);

    /// <summary>Whether a spot holds the anchor the n-th creature of a kind stands at (its anchors in turn).</summary>
    bool Anchored(CastRule? rule, int index, Vector2 at)
    {
        if (rule is null || rule.Anchors.Length == 0) return true;
        var anchor = TemplateAnchor.Parse(rule.Anchors[index % rule.Anchors.Length]);
        return anchor is not { } a || a.Holds(Map, at);
    }

    /// <summary>The place kind the n-th creature of a kind stands by (null when its anchor is not a place).</summary>
    static PoiKind? AnchorPlace(CastRule? rule, int index) =>
        rule is { Anchors.Length: > 0 } && TemplateAnchor.Parse(rule.Anchors[index % rule.Anchors.Length]) is { Kind: AnchorKind.Place } a ? a.Place : null;

    /// <summary>Murkling budding, by the template: its interval's factor and its cap's bonus.</summary>
    float BudRateFactor => Rules?.BudRate is > 0f and var r ? r : 1f;
    int BudCapBonus => Rules?.BudCap ?? 0;
    bool Quiet => Rules?.Quiet ?? false;

    /// <summary>
    /// The template's prime: a murkling prime already waiting by its place (5–10 m out, in water she can reach), made of
    /// ink drunk from round it as any bud is; and its guardians: the Blightroot nearest that place buds its guardians
    /// round the place.
    /// </summary>
    void PlaceTemplateGuards()
    {
        if (Rules is not { } rules || Corruption is not { } field) return;
        if (rules.Guardians is { } guarded && Map.Pois.FirstOrDefault(p => p.Kind == guarded) is { } place)
            if (Blightroots.OrderBy(b => Vector2.Distance(b.Position, place.Position)).FirstOrDefault() is { } root)
                root.GuardAt = place.Position;
        if (rules.Prime is not { } kind || Map.Pois.FirstOrDefault(p => p.Kind == kind) is not { } at) return;
        var reach = LevelValidator.Distances(Map, Map.Start.Position);
        for (int tries = 0; tries < 200; tries++)
        {
            float a = _blightRng.Range(0f, MathF.Tau), r = _blightRng.Range(5f, 10f) + at.Radius * 0.5f;
            var p = at.Position + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
            if (!Clear(p, BlightTuning.PrimeBody) || float.IsPositiveInfinity(LevelValidator.DistanceAt(reach, p))) continue;
            if (Vector2.Distance(p, Map.Start.Position) < BlightTuning.PrimeClearance) continue;
            var area = Blightroots.OrderBy(b => Vector2.Distance(b.Position, p)).FirstOrDefault()?.Area ?? -1;
            if (Bud(p, area, prime: true) is not { } prime) return;
            // Already out of the ground: its ink drunk at once.
            if (prime.Draw is { } draw) prime.Matter += field.Drink(draw, 1f);
            prime.Draw = null;
            prime.Bud = 0f;
            return;
        }
    }
}
