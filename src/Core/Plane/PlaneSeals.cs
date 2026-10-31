using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Core.Plane;

/// <summary>Sealed walls (DESIGN-TOPDOWN §4.2): first guesses, tuned in play.</summary>
public static class SealTuning
{
    /// <summary>Shots of her light it takes to fill the carving (each counts once, whatever its damage): an X, the boss's sword.</summary>
    public const int Hits = 6, BossHits = 8;
    /// <summary>A shot counts when it strikes the wall within this far of its carving.</summary>
    public const float Reach = 2.2f;
    /// <summary>How far out on the seal's outer face the emblem is carved (share of its radius; a disc seal).</summary>
    public const float EmblemOut = 1f;
    /// <summary>The hits must come one after another: Hold seconds after the last, its light drains, a hit's worth every Drain seconds.</summary>
    public const float Hold = 1.4f, Drain = 0.3f;
}

/// <summary>
/// A sealed wall, looking like any other reef wall: a secret room's way in or a tunnel (an X carved faintly in it), or
/// the way into the boss's arena (a sword). Her shots striking the carving one after another fill it with light (if
/// they stop, it drains); filled, the wall slides down into the sand and the way is open. The boss's takes no light
/// while a vent on the level stands open.
/// </summary>
public sealed class SecretSeal
{
    public WeakRock Rock;
    /// <summary>Out toward the side it is met from, and the carving's place on that face.</summary>
    public Vector2 Facing, Emblem;
    public int Hits;
    /// <summary>Light short of a whole hit (a small shot is only part of one).</summary>
    public float Credit;
    public bool Open;
    /// <summary>Shut by a barred boss arena while its fight runs (it cannot be lit meanwhile); whether it stood open before.</summary>
    public bool Barred, WasOpen;
    /// <summary>Since the last hit that counted (its light drains once Hold has passed), and the drain's clock.</summary>
    public float Since, DrainClock;
    public SealKind Kind => Rock.Kind;
    public int Needed => Kind == SealKind.Boss ? SealTuning.BossHits : SealTuning.Hits;
    /// <summary>How full of light the carving is, 0..1.</summary>
    public float Glow => Hits / (float)Needed;
}

public sealed partial class PlaneWorld
{
    /// <summary>The secret rooms' seals, one per weak rock.</summary>
    public List<SecretSeal> Seals { get; } = new();

    void PlaceSeals()
    {
        foreach (var rock in Map.WeakRocks)
        {
            var facing = rock.Thick > 0f ? rock.Facing : rock.Cave >= 0 && rock.Cave < Map.Caves.Count ? Map.Caves[rock.Cave].Facing : Vector2.UnitY;
            var emblem = rock.Thick > 0f ? rock.Center + facing * rock.Thick : rock.Center + facing * rock.Radius * SealTuning.EmblemOut;
            Seals.Add(new SecretSeal { Rock = rock, Facing = facing, Emblem = emblem });
        }
    }

    /// <summary>The carvings' light drains when her hits stop coming.</summary>
    void StepSeals()
    {
        foreach (var seal in Seals)
        {
            if (seal.Open || seal.Hits <= 0) continue;
            seal.Since += Dt;
            if (seal.Since < SealTuning.Hold) continue;
            seal.DrainClock += Dt;
            if (seal.DrainClock < SealTuning.Drain) continue;
            seal.DrainClock = 0f;
            seal.Hits--;
        }
    }

    /// <summary>One of her shots struck rock here: on a seal's emblem it adds its light; full, the seal crumbles.</summary>
    void StrikeSeal(Vector2 at, float light = 1f)
    {
        foreach (var seal in Seals)
        {
            if (seal.Open || seal.Barred || !seal.Rock.Contains(at, 0.3f) || Vector2.Distance(at, seal.Emblem) > SealTuning.Reach) continue;
            if (seal.Kind == SealKind.Boss && VentsLeft > 0)
            {
                Events.Add(new PlaneEvent(PlaneEventType.SealRefused, seal.Emblem, seal.Facing, VentsLeft));
                return;
            }
            seal.Since = seal.DrainClock = 0f;
            seal.Credit += light;
            if (seal.Credit < 0.999f) return;
            seal.Credit -= 1f;
            seal.Hits++;
            Events.Add(new PlaneEvent(PlaneEventType.SealLit, seal.Emblem, seal.Facing, seal.Glow));
            if (seal.Hits < seal.Needed) return;
            seal.Open = true;
            // Only its gate gives way: a long wall's sides stay standing.
            _rocks.Remove(seal.Rock);
            _rocks.AddRange(seal.Rock.Remains());
            Events.Add(new PlaneEvent(PlaneEventType.SealOpened, seal.Rock.Center, seal.Facing, seal.Rock.Radius));
            return;
        }
    }

    /// <summary>
    /// A barred boss arena's fight begins: every sealed wall into it is shut for the fight. One she opened rises back up
    /// out of the sand behind her (its whole wall, gate and all); the others simply stay, and none can be lit meanwhile.
    /// What stands in a wall as it rises is pushed out of it (PlaneWorld.Move's way out of rock).
    /// </summary>
    void BarArena()
    {
        foreach (var seal in Seals)
        {
            if (seal.Kind != SealKind.Boss) continue;
            seal.Barred = true;
            seal.WasOpen = seal.Open;
            seal.Hits = 0;
            if (!seal.Open) continue;
            seal.Open = false;
            foreach (var part in seal.Rock.Remains()) _rocks.Remove(part);
            _rocks.Add(seal.Rock);
            Events.Add(new PlaneEvent(PlaneEventType.SealClosed, seal.Rock.Center, seal.Facing, seal.Rock.Radius));
        }
    }

    /// <summary>The fight is over: the walls it shut slide back down by themselves; the others can be lit again.</summary>
    void UnbarArena()
    {
        foreach (var seal in Seals)
        {
            if (!seal.Barred) continue;
            seal.Barred = false;
            if (!seal.WasOpen) continue;
            seal.Open = true;
            _rocks.Remove(seal.Rock);
            _rocks.AddRange(seal.Rock.Remains());
            Events.Add(new PlaneEvent(PlaneEventType.SealOpened, seal.Rock.Center, seal.Facing, seal.Rock.Radius));
        }
    }

    /// <summary>Verification: a shot of her light striking this seal's emblem (or the first one still shut).</summary>
    public void DebugStrikeSeal(SecretSeal? which = null)
    {
        if ((which ?? Seals.FirstOrDefault(x => !x.Open)) is { } seal) StrikeSeal(seal.Emblem - seal.Facing * 0.3f);
    }

    /// <summary>The caves still sealed (the maps draw them as wall).</summary>
    public IEnumerable<int> SealedCaves => Seals.Where(s => !s.Open && s.Rock.Cave >= 0).Select(s => s.Rock.Cave);
}
