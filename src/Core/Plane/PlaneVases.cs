using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;

namespace OctoShoots.Core.Plane;

/// <summary>Sunken amphorae (DESIGN-TOPDOWN §12.2): first guesses, tuned in play.</summary>
public static class VaseTuning
{
    /// <summary>Groups of pots per level, and pots per group (inclusive), at least GroupSpacing apart.</summary>
    public const int GroupsMin = 5, GroupsMax = 7, PerGroupMin = 1, PerGroupMax = 3;
    public const float GroupSpacing = 12f;
    /// <summary>They stand where the seabed is this deep (so they rise into her swim plane), within WallReach of rock.</summary>
    public const float FloorMin = -1.6f, FloorMax = -0.3f, WallReach = 3f;
    /// <summary>Kept clear of the start, of the way down, and of the places.</summary>
    public const float StartClear = 20f, PlaceClear = 4f;
    /// <summary>A pot's footprint on the swim plane: it blocks swimmers and shots like rock.</summary>
    public const float RadiusMin = 0.38f, RadiusMax = 0.55f;

    /// <summary>
    /// A hit breaks a pot when its impact (a bubble's damage, grown with distance) reaches this: a plain bubble (10)
    /// only rocks it. Captain's Hook's knockback (×2 or more) breaks it with any bubble; so does a Pearl Diver throw
    /// charged half way or more, an Ink Sac blast reaching it, or her ink dash into it.
    /// </summary>
    public const float BreakImpact = 18f, BreakKnockback = 2f, BreakCharge = 0.5f;
    /// <summary>In a surging canyon a pot may topple: this often a second at full flow (SurgeSpeedMax), less in a gentler one.</summary>
    public const float CurrentBreakPerSecond = 0.04f;
    /// <summary>What a broken pot leaves: shells (2–3) or a heart, else nothing.</summary>
    public const float ShellChance = 0.5f, HeartChance = 0.2f;
    public const int ShellsMin = 2, ShellsMax = 3;
}

/// <summary>A sunken amphora on the seabed: solid until something strong enough breaks it.</summary>
public sealed class PlaneVase
{
    public Vector2 Position;
    public float Radius;
    /// <summary>Its shape (0 a tall amphora, 1 a round jar, 2 a squat pot) and how it leans (radians, its own seed).</summary>
    public int Shape;
    public float Seed;
    public bool Broken;
}

public sealed partial class PlaneWorld
{
    public List<PlaneVase> Vases { get; } = new();
    Rng _vaseRng = null!;

    /// <summary>
    /// Groups of pots on the seabed by the walls, seeded: where the floor is shallow enough that they stand up into her
    /// swim plane, clear of the start, the way down, the places and the mobs' homes.
    /// </summary>
    void PlaceVases()
    {
        _vaseRng = new Rng(Map.Seed ^ 0x7A5E5UL ^ ((ulong)Map.Level << 20) ^ ((ulong)Map.Depth << 30) ^ ((ulong)Map.Attempt << 46));
        var rng = _vaseRng;
        var spots = new List<Vector2>();
        for (float y = LevelMap.RimWidth + 2f; y < LevelMap.Size - LevelMap.RimWidth - 2f; y += 3f)
        for (float x = LevelMap.RimWidth + 2f; x < LevelMap.Size - LevelMap.RimWidth - 2f; x += 3f)
        {
            var p = new Vector2(x, y);
            if (Fine(p, VaseTuning.RadiusMax + 0.3f) && NearWall(p)) spots.Add(p);
        }
        for (int i = spots.Count - 1; i > 0; i--)
        {
            int j = rng.Int(i + 1);
            (spots[i], spots[j]) = (spots[j], spots[i]);
        }
        int groups = VaseTuning.GroupsMin + rng.Int(VaseTuning.GroupsMax - VaseTuning.GroupsMin + 1);
        var centres = new List<Vector2>();
        foreach (var at in spots)
        {
            if (centres.Count >= groups) break;
            if (centres.Any(c => Vector2.Distance(c, at) < VaseTuning.GroupSpacing)) continue;
            centres.Add(at);
            int count = VaseTuning.PerGroupMin + rng.Int(VaseTuning.PerGroupMax - VaseTuning.PerGroupMin + 1);
            for (int k = 0, tries = 0; k < count && tries < 12; tries++)
            {
                float radius = rng.Range(VaseTuning.RadiusMin, VaseTuning.RadiusMax);
                float a = rng.Range(0f, MathF.Tau);
                var p = k == 0 ? at : at + new Vector2(MathF.Cos(a), MathF.Sin(a)) * rng.Range(1.1f, 1.8f);
                if (!Fine(p, radius + 0.1f) || Vases.Any(v => Vector2.Distance(v.Position, p) < v.Radius + radius + 0.15f)) continue;
                Vases.Add(new PlaneVase { Position = p, Radius = radius, Shape = rng.Int(3), Seed = rng.Range(0f, 100f) });
                k++;
            }
        }

        bool Fine(Vector2 p, float radius)
        {
            float h = Map.HeightAt(p);
            if (h < VaseTuning.FloorMin || h > VaseTuning.FloorMax || !Clear(p, radius)) return false;
            if (Vector2.Distance(p, Map.Start.Position) < VaseTuning.StartClear) return false;
            if (Vector2.Distance(p, Map.Exit.Position) < Map.Exit.Radius + VaseTuning.PlaceClear) return false;
            if (Map.Shaft.Contains(p)) return false;
            foreach (var poi in Map.Pois)
                if (poi.Kind != PoiKind.Start && Vector2.Distance(p, poi.Position) < poi.Radius + VaseTuning.PlaceClear) return false;
            return Mobs.All(m => Vector2.Distance(m.Home, p) > 1.5f);
        }

        bool NearWall(Vector2 p)
        {
            for (int i = 0; i < 12; i++)
            {
                float a = i * MathF.Tau / 12f;
                if (!Map.IsOpen(p + new Vector2(MathF.Cos(a), MathF.Sin(a)) * VaseTuning.WallReach)) return true;
            }
            return false;
        }
    }

    /// <summary>The standing pot a circle at p overlaps, if any.</summary>
    PlaneVase? VaseAt(Vector2 p, float radius)
    {
        foreach (var v in Vases)
            if (!v.Broken && Vector2.Distance(p, v.Position) < v.Radius + radius) return v;
        return null;
    }

    /// <summary>
    /// One of her bubbles meets a pot. Strong enough (big, charged, or with the hook's knockback), the pot breaks and the
    /// bubble pops on it; otherwise the pot rocks, and the bubble pops on the bump or bounces off (like on rock).
    /// </summary>
    void BubbleMeetsVase(PlaneShot shot, PlaneVase vase, Vector2 dir)
    {
        Vector2 n = SafeNormalize(shot.Line - vase.Position);
        if (n == Vector2.Zero) n = -dir;
        bool strong = shot.Damage * GrowFactor(shot) >= VaseTuning.BreakImpact
            || shot.Charged >= VaseTuning.BreakCharge
            || Run.Loadout.Stats[Stat.Knockback] >= VaseTuning.BreakKnockback;
        Vector2 off = SafeNormalize(dir - 2f * Vector2.Dot(dir, -n) * -n);
        if (strong)
        {
            BreakVase(vase, dir);
            Pop(shot, -n);
            SplitBubble(shot, off);
            return;
        }
        Events.Add(new PlaneEvent(PlaneEventType.VaseHit, vase.Position, dir));
        if (shot.BouncesLeft <= 0 && _bubbleRng.NextFloat() < PlaneCombatTuning.BubblePopChance)
        {
            Pop(shot, -n);
            SplitBubble(shot, off);
            return;
        }
        if (shot.BouncesLeft > 0) shot.BouncesLeft--;
        if (Vector2.Dot(shot.Velocity, n) < 0f) shot.Velocity -= 2f * Vector2.Dot(shot.Velocity, n) * n;
    }

    /// <summary>A pot breaks: its shards fly along dir (the view), and it leaves shells, a heart, or nothing.</summary>
    void BreakVase(PlaneVase vase, Vector2 dir)
    {
        if (vase.Broken) return;
        vase.Broken = true;
        Events.Add(new PlaneEvent(PlaneEventType.VaseBroken, vase.Position, SafeNormalize(dir), vase.Radius));
        float roll = _vaseRng.NextFloat();
        if (roll < VaseTuning.HeartChance)
        {
            Hearts.Add(new PlaneHeart { Position = vase.Position, Velocity = Fling(1.5f, 2.5f) });
            return;
        }
        if (roll >= VaseTuning.HeartChance + VaseTuning.ShellChance) return;
        int count = VaseTuning.ShellsMin + _vaseRng.Int(VaseTuning.ShellsMax - VaseTuning.ShellsMin + 1);
        for (int i = 0; i < count; i++) Shells.Add(new PlaneShell { Position = vase.Position, Velocity = Fling(2f, 4f) });
    }

    /// <summary>Ink Sac: a blast breaks every pot it reaches.</summary>
    void BlastVases(Vector2 at, float reach)
    {
        foreach (var v in Vases)
            if (!v.Broken && Vector2.Distance(v.Position, at) < reach + v.Radius) BreakVase(v, v.Position - at);
    }

    /// <summary>
    /// Pots in her way when she dashes break (an ink dash is a ram); and in a surging canyon each standing pot may topple
    /// and break, more likely the stronger the flow.
    /// </summary>
    void StepVases()
    {
        var p = Player;
        if (p.IsDashing)
            foreach (var v in Vases)
                if (!v.Broken && Vector2.Distance(v.Position, p.Position) < v.Radius + Radius + 0.15f) BreakVase(v, v.Position - p.Position);
        if (Director.Active.Count == 0) return;
        foreach (var v in Vases)
        {
            if (v.Broken) continue;
            Vector2 flow = Director.FlowAt(v.Position, Time);
            float k = flow.Length() / ReefDirectorTuning.SurgeSpeedMax;
            if (k > 0.05f && _vaseRng.NextFloat() < VaseTuning.CurrentBreakPerSecond * k * Dt) BreakVase(v, flow);
        }
    }
}
