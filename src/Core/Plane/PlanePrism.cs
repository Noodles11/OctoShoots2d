using System;
using System.Numerics;
using OctoShoots.Core.Items;

namespace OctoShoots.Core.Plane;

/// <summary>
/// Prism Pearl (docs/PEARLS.md): its active splits her light into its three colours for a while, as a prism does — three
/// smaller Clementines, red, green and blue, in a triangle round the point she steers (where the camera stays). She is
/// the green one, at its front corner toward her aim; the red and the blue follow at the back corners, on her left and
/// her right. Each shoots on its own beat at where she aims, in its own colour; a hit on any of them is a hit on her (one
/// body of health, one grace after a hit). Then they flow back into her. First guesses.
/// </summary>
public static class PrismTuning
{
    /// <summary>Splitting apart (and flowing back together) takes this long.</summary>
    public const float Split = 0.45f;
    /// <summary>
    /// How far each corner of the triangle lies from the point she steers (its middle; the corners 120° apart, the green
    /// toward her aim), and their size (a share of hers: all three are smaller).
    /// </summary>
    public const float Spread = 1.4f, Size = 0.62f;
    /// <summary>How tightly the three keep their corners (a spring rate, per second).</summary>
    public const float Follow = 14f;
}

/// <summary>Her light's colours apart: none (her own), or the red, green or blue third of it.</summary>
public enum PrismHue { None, Red, Green, Blue }

/// <summary>One of the three Clementines while her light is split: where it is, and its own beat of fire.</summary>
public sealed class PrismPart
{
    public Vector2 Position, PrevPosition, Velocity;
    public float ShotTimer;
    public PrismHue Hue;
}

public sealed partial class PlaneWorld
{
    /// <summary>The red (the back corner on her left) and the blue (on her right) while her light is split.</summary>
    public PrismPart[] PrismParts { get; } = { new() { Hue = PrismHue.Red }, new() { Hue = PrismHue.Blue } };

    /// <summary>The green, herself, at the triangle's front corner while her light is split.</summary>
    public PrismPart PrismLead { get; } = new() { Hue = PrismHue.Green };

    /// <summary>Where her own body is: at the triangle's front corner while her light is split, else where she steers.</summary>
    public Vector2 HerBody => Prism ? PrismLead.Position : Player.Position;

    /// <summary>Her light is split now (from the first moment of the split to the last of the rejoining).</summary>
    public bool Prism => Player.PrismTimer > 0f;

    /// <summary>How far apart her light is: 0 whole → 1 split, easing in over the split and back out over the rejoining.</summary>
    public float PrismOpen
    {
        get
        {
            var p = Player;
            if (p.PrismTimer <= 0f) return 0f;
            float k = MathF.Min(MathF.Min((p.PrismTotal - p.PrismTimer) / PrismTuning.Split, p.PrismTimer / PrismTuning.Split), 1f);
            return k * k * (3f - 2f * k);
        }
    }

    /// <summary>The active: her light splits apart for its duration (the split and the rejoining included).</summary>
    void BeginPrism(float duration)
    {
        var p = Player;
        p.PrismTotal = p.PrismTimer = MathF.Max(duration, 2f * PrismTuning.Split);
        for (int k = 0; k < PrismParts.Length; k++)
        {
            var part = PrismParts[k];
            part.Position = part.PrevPosition = p.Position;
            part.Velocity = p.Velocity;
            // Each on its own beat: a third and two thirds of her interval after her.
            part.ShotTimer = ShotInterval * (k + 1) / 3f;
        }
        PrismLead.Position = PrismLead.PrevPosition = p.Position;
        PrismLead.Velocity = p.Velocity;
        Events.Add(new PlaneEvent(PlaneEventType.PrismSplit, p.Position, p.Aim));
    }

    /// <summary>
    /// While her light is split: the three keep their corners of the triangle round the point she steers (out as far as
    /// it has split), turning with her aim, swimming round rock rather than through it; the red and the blue each fire
    /// their own volleys at where she aims while she holds fire (she fires her own from her corner). At the end they
    /// flow back together.
    /// </summary>
    void StepPrism(bool fire)
    {
        var p = Player;
        if (p.PrismTimer <= 0f) return;
        p.PrismTimer -= Dt;
        float open = PrismOpen;
        var side = new Vector2(-p.Aim.Y, p.Aim.X);
        if (side.LengthSquared() < 1e-6f) side = Vector2.UnitX;
        var target = p.Position + p.Aim * ThrowReach;
        // The corners: hers toward her aim, the red's and the blue's 120° round from it either way.
        var ahead = new Vector2(side.Y, -side.X);
        KeepCorner(PrismLead, ahead);
        for (int k = 0; k < PrismParts.Length; k++)
        {
            var part = PrismParts[k];
            KeepCorner(part, -0.5f * ahead + side * ((k == 0 ? -1f : 1f) * 0.8660254f));
            part.ShotTimer -= Dt;
            if (!fire || part.ShotTimer > 0f || open < 0.5f) continue;
            part.ShotTimer = ShotInterval;
            var aim = SafeNormalize(target - part.Position);
            if (aim == Vector2.Zero) aim = p.Aim;
            Volley(aim, from: part.Position, hue: part.Hue, reach: Vector2.Distance(target, part.Position));
        }
        if (p.PrismTimer <= 0f)
        {
            foreach (var part in PrismParts) part.Position = part.PrevPosition = p.Position;
            PrismLead.Position = PrismLead.PrevPosition = p.Position;
            Events.Add(new PlaneEvent(PlaneEventType.PrismJoined, p.Position, p.Aim));
        }

        // One of the three toward its corner (this way from the middle), round rock; caught far off, back to the middle.
        void KeepCorner(PrismPart part, Vector2 corner)
        {
            part.PrevPosition = part.Position;
            var want = p.Position + corner * PrismTuning.Spread * open;
            part.Velocity = (want - part.Position) * PrismTuning.Follow;
            Move(ref part.Position, ref part.Velocity, Radius * PrismTuning.Size, report: false);
            if (Vector2.Distance(part.Position, p.Position) > PrismTuning.Spread * 2.5f) part.Position = p.Position;
        }
    }

    /// <summary>
    /// Something of this size at a point reaches her: her own body (at her corner while her light is split), or the red
    /// or the blue. Where it struck is in <paramref name="struck"/>.
    /// </summary>
    bool ReachesHer(Vector2 at, float radius, out Vector2 struck)
    {
        struck = HerBody;
        if (Vector2.Distance(at, struck) < Radius * (Prism ? PrismTuning.Size : 1f) + radius) return true;
        if (PrismOpen < 0.5f) return false;
        foreach (var part in PrismParts)
            if (Vector2.Distance(at, part.Position) < Radius * PrismTuning.Size + radius)
            {
                struck = part.Position;
                return true;
            }
        return false;
    }
}
