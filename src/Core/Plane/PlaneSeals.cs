using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Core.Plane;

/// <summary>Secret rooms' seals (DESIGN-TOPDOWN §4.2): first guesses, tuned in play.</summary>
public static class SealTuning
{
    /// <summary>Shots of her light it takes to fill the emblem (each counts once, whatever its damage).</summary>
    public const int Hits = 6;
    /// <summary>A shot counts when it strikes the seal within this far of its emblem.</summary>
    public const float Reach = 1.8f;
    /// <summary>How far out on the seal's outer face the emblem is carved (share of its radius).</summary>
    public const float EmblemOut = 1f;
}

/// <summary>
/// A secret room's way in, sealed by what looks like any other wall: an X is carved faintly in it on the canyon side.
/// Her shots striking the X fill it with light; filled, the wall crumbles and the way in is open.
/// </summary>
public sealed class SecretSeal
{
    public WeakRock Rock;
    /// <summary>Out of the cave toward the canyon, and the carved emblem's place on the seal's outer face.</summary>
    public Vector2 Facing, Emblem;
    public int Hits;
    public bool Open;
    /// <summary>How full of light the emblem is, 0..1.</summary>
    public float Glow => Hits / (float)SealTuning.Hits;
}

public sealed partial class PlaneWorld
{
    /// <summary>The secret rooms' seals, one per weak rock.</summary>
    public List<SecretSeal> Seals { get; } = new();

    void PlaceSeals()
    {
        foreach (var rock in Map.WeakRocks)
        {
            var facing = rock.Cave >= 0 && rock.Cave < Map.Caves.Count ? Map.Caves[rock.Cave].Facing : Vector2.UnitY;
            Seals.Add(new SecretSeal { Rock = rock, Facing = facing, Emblem = rock.Center + facing * rock.Radius * SealTuning.EmblemOut });
        }
    }

    /// <summary>One of her shots struck rock here: on a seal's emblem it adds its light; full, the seal crumbles.</summary>
    void StrikeSeal(Vector2 at)
    {
        foreach (var seal in Seals)
        {
            if (seal.Open || Vector2.Distance(at, seal.Rock.Center) > seal.Rock.Radius + 0.3f || Vector2.Distance(at, seal.Emblem) > SealTuning.Reach) continue;
            seal.Hits++;
            Events.Add(new PlaneEvent(PlaneEventType.SealLit, seal.Emblem, seal.Facing, seal.Glow));
            if (seal.Hits < SealTuning.Hits) return;
            seal.Open = true;
            _rocks.Remove(seal.Rock);
            Events.Add(new PlaneEvent(PlaneEventType.SealOpened, seal.Rock.Center, seal.Facing, seal.Rock.Radius));
            return;
        }
    }

    /// <summary>Verification: a shot of her light striking the first seal's emblem.</summary>
    public void DebugStrikeSeal()
    {
        if (Seals.FirstOrDefault(x => !x.Open) is { } seal) StrikeSeal(seal.Emblem - seal.Facing * 0.3f);
    }

    /// <summary>The caves still sealed (the maps draw them as wall).</summary>
    public IEnumerable<int> SealedCaves => Seals.Where(s => !s.Open).Select(s => s.Rock.Cave);
}
