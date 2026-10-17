using System;
using System.Numerics;

namespace OctoShoots.Core.Plane;

/// <summary>Murklings corrupting the reef's fish (docs/CORRUPTION.md, "Lesser murklings"): first guesses, tuned in play.</summary>
public static class MurkFishTuning
{
    /// <summary>A hunting murkling keeps about Keep metres from the fish it is after, and looks for another every Look seconds.</summary>
    public const float Keep = 6f, Look = 0.5f;
    /// <summary>A throw that strikes a fish rolls for every healthy fish within Splash of where it burst.</summary>
    public const float Splash = 0.8f;
    /// <summary>Each fish struck turns with this chance (+ per point of Menace, at most ChanceMax); else it darts off.</summary>
    public const float Chance = 0.35f, ChancePerMenace = 0.6f, ChanceMax = 0.9f;
    /// <summary>How long the ink takes to creep over a struck fish before it is a lesser murkling.</summary>
    public const float TurnSeconds = 0.6f;
    /// <summary>Fish veer off from a murkling within Fear; clownfish tuck in when one is within ClownHide of their anemone.</summary>
    public const float Fear = 4f, ClownHide = 3f;
    /// <summary>Lesser murklings chase her at this many times a murkling's speed; at most MaxClinging cling at once.</summary>
    public const float Speed = 1.25f;
    public const int MaxClinging = 8;
    /// <summary>A swarm keeps about Spacing between its members.</summary>
    public const float Spacing = 0.45f;
    /// <summary>With every place on her taken, the rest of a swarm mills round her about this far out.</summary>
    public const float Mill = 1.4f;

    /// <summary>A lesser murkling's body radius, corruption meter (before Menace) and drain while clinging (HP/s), by the fish it was.</summary>
    public static (float Radius, float Meter, float Drain) Of(ReefFishKind kind) => kind switch
    {
        ReefFishKind.Chromis => (0.16f, 8f, 0.8f),
        ReefFishKind.Clownfish => (0.24f, 14f, 1.2f),
        _ => (0.32f, 20f, 1.6f),
    };

    /// <summary>The chance a struck fish turns, at this Menace.</summary>
    public static float ChanceAt(float menace) => MathF.Min(Chance + ChancePerMenace * menace, ChanceMax);
}

public sealed partial class PlaneWorld
{
    /// <summary>The fish a hunting murkling is after: the nearest healthy one within its sense, in sight; looked for twice a second.</summary>
    PlaneReefFish? Prey(Murkling m)
    {
        m.PreyTimer -= Dt;
        if (m.Prey is { } held && m.PreyTimer > 0f && held.Huntable) return held;
        m.PreyTimer = MurkFishTuning.Look;
        m.Prey = null;
        float best = m.Sense;
        PlaneReefFish? pick = null;
        foreach (var f in ReefFish)
        {
            if (!f.Huntable) continue;
            float d = Vector2.Distance(f.Position, m.Position);
            if (d >= best) continue;
            best = d;
            pick = f;
        }
        if (pick is not null && LineOfSight(m.Position, pick.Position)) m.Prey = pick;
        return m.Prey;
    }

    /// <summary>
    /// A throw meets a fish (fish never dodge them): it bursts there, and every healthy fish in its splash may turn.
    /// True if it struck.
    /// </summary>
    bool StrikeFish(MurkShot s)
    {
        PlaneReefFish? struck = null;
        foreach (var f in ReefFish)
        {
            if (!f.Huntable) continue;
            float r = f.Kind switch { ReefFishKind.Chromis => ReefFishTuning.ChromisRadius, ReefFishKind.Clownfish => ReefFishTuning.ClownRadius, _ => ReefFishTuning.TangRadius };
            if (Vector2.Distance(f.Position, s.Position) < r + s.Radius)
            {
                struck = f;
                break;
            }
        }
        if (struck is null) return false;
        Events.Add(new PlaneEvent(PlaneEventType.FishStruck, s.Position, SafeNormalize(s.Velocity)));
        float chance = MurkFishTuning.ChanceAt(Menace);
        foreach (var f in ReefFish)
        {
            if (!f.Huntable || (f != struck && Vector2.Distance(f.Position, s.Position) > s.Splash)) continue;
            if (_blightRng.NextFloat() < chance) f.Turning = MurkFishTuning.TurnSeconds;
            else
            {
                // It did not take: the fish bolts.
                var away = SafeNormalize(f.Position - s.Position);
                if (away == Vector2.Zero) away = SafeNormalize(s.Velocity);
                if (f.Kind == ReefFishKind.Chromis)
                {
                    f.Dart = 0.6f;
                    f.DartAway = away;
                }
                else if (f.Kind == ReefFishKind.BlueTang)
                {
                    f.Startle = ReefFishTuning.TangStartleSeconds;
                    f.Wait = 0f;
                    f.Target = f.Position + away * 5f;
                }
                else f.Hiding = true;
            }
        }
        return true;
    }

    /// <summary>The ink has crept over it: the fish is a lesser murkling, sized as it was.</summary>
    void TurnFish(PlaneReefFish f)
    {
        f.Turning = 0f;
        f.Corrupted = true;
        var (radius, meter, drain) = MurkFishTuning.Of(f.Kind);
        var m = new Murkling
        {
            Position = f.Position, Velocity = f.Velocity, Bud = 0f, Fish = f, Radius = radius, Body = radius, ClingDrain = drain,
            Swarm = f.Kind == ReefFishKind.Chromis ? f.Host : -1, Seed = _blightRng.Range(0f, 100f),
        };
        m.Corruption.Regen *= 1f + 0.5f * Menace;
        m.Corruption.Max = m.Corruption.Value = meter * (1f + BlightTuning.MeterPerMenace * Menace);
        Murklings.Add(m);
        Events.Add(new PlaneEvent(PlaneEventType.FishTurned, f.Position, Vector2.Zero, radius, DamageSource.None));
    }

    /// <summary>A lesser murkling freed: it is its fish again, where it was, and it cannot be turned again this level.</summary>
    void FreeFish(Murkling m)
    {
        if (m.Fish is not { } f) return;
        f.Corrupted = false;
        f.Freed = true;
        f.Position = m.Position;
        f.Velocity = m.Velocity * 0.3f;
        f.Hiding = false;
        f.Wait = 0f;
        f.Target = default;
        Events.Add(new PlaneEvent(PlaneEventType.FishFreed, m.Position, Vector2.Zero, m.Radius));
    }

    /// <summary>
    /// A lesser murkling: it never throws. It chases her once she is within its sense (a swarm once she is near its
    /// middle), a little faster than a murkling, and clings on touch, drinking her light; a swarm keeps together.
    /// </summary>
    void StepLesser(Murkling m)
    {
        var p = Player;
        float speed = (BlightTuning.MurkSpeed + BlightTuning.MurkSpeedPerMenace * Menace) * MurkFishTuning.Speed;
        Vector2 centre = m.Position, apart = Vector2.Zero;
        if (m.Swarm >= 0)
        {
            Vector2 sum = Vector2.Zero;
            int n = 0;
            foreach (var o in Murklings)
            {
                if (!o.Alive || o.Swarm != m.Swarm || o.Clinging) continue;
                sum += o.Position;
                n++;
                if (o == m) continue;
                Vector2 d = m.Position - o.Position;
                float l = d.Length();
                if (l < MurkFishTuning.Spacing) apart += (l > 1e-3f ? d / l : new Vector2(MathF.Cos(m.Seed), MathF.Sin(m.Seed))) * (1f - l / MurkFishTuning.Spacing);
            }
            if (n > 0) centre = sum / n;
        }
        bool chase = Vector2.Distance(centre, p.Position) < BlightTuning.Sense;
        Vector2 wish = chase ? SafeNormalize(p.Position - m.Position) * speed : Vector2.Zero;
        // A swarm swims as one: pulled to its middle, apart from its neighbours.
        if (m.Swarm >= 0) wish += (centre - m.Position) * (chase ? 0.6f : 1.2f) + apart * 4f;
        wish += new Vector2(MathF.Sin(Time * 2.3f + m.Seed), MathF.Cos(Time * 1.9f + m.Seed * 2f)) * (chase ? 0.4f : 0.6f);
        // No room left on her: the rest of the swarm mills round her just out of reach, waiting for a place.
        float herDist = Vector2.Distance(m.Position, p.Position);
        if (chase && LesserClingers >= MurkFishTuning.MaxClinging && herDist < MurkFishTuning.Mill)
        {
            var out_ = SafeNormalize(m.Position - p.Position);
            float spin = m.Seed % 2f < 1f ? 1f : -1f;
            wish = out_ * speed * (1f - herDist / MurkFishTuning.Mill) * 2f + new Vector2(-out_.Y, out_.X) * spin * speed * 0.6f + apart * 4f;
        }
        if (wish.Length() > speed) wish = Vector2.Normalize(wish) * speed;
        m.Target = chase ? p.Position : centre;
        m.Velocity = MoveToward(m.Velocity, wish, 7f * Dt);
        Move(ref m.Position, ref m.Velocity, m.Radius, report: false, barrier: ArenaBarrier.Outside);

        // It clings on touch: no hit, only the drain.
        if (Vector2.Distance(m.Position, p.Position) < m.Radius + Radius && p.DashInvulnerableTimer <= 0f && p.ShieldTimer <= 0f
            && LesserClingers < MurkFishTuning.MaxClinging)
        {
            m.Clinging = true;
            m.ClingOffset = SafeNormalize(m.Position - p.Position) * (Radius * 0.85f);
            Events.Add(new PlaneEvent(PlaneEventType.MurklingCling, m.Position, Vector2.Zero));
        }
    }

    /// <summary>The push away from murklings near a fish: none past Fear, growing to full beside one.</summary>
    Vector2 Fear(Vector2 at)
    {
        Vector2 push = Vector2.Zero;
        foreach (var m in Murklings)
        {
            if (!m.Alive || m.Budding || m.Clinging) continue;
            Vector2 away = at - m.Position;
            float d = away.Length();
            if (d >= MurkFishTuning.Fear) continue;
            push += (d > 1e-3f ? away / d : Vector2.UnitX) * (1f - d / MurkFishTuning.Fear);
        }
        return push;
    }

    /// <summary>A murkling (out of the ground, not on her) within reach of a point.</summary>
    bool MurklingNear(Vector2 at, float reach)
    {
        foreach (var m in Murklings)
            if (m.Alive && !m.Budding && !m.Clinging && Vector2.Distance(m.Position, at) < reach) return true;
        return false;
    }
}
