using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OctoShoots.Core.Sim;

/// <summary>The destructible reef (§6.3): ink bombs and craters.</summary>
public sealed partial class World
{
    const float BombRadius = 0.25f;
    const float BombSink = 3.5f;

    readonly List<Crater> _craters = new();

    public List<InkBomb> LiveBombs { get; } = new();
    public IReadOnlyList<Crater> Craters => _craters;

    /// <summary>
    /// Subtracts a sphere from the reef. The outer shell never changes. Returns true when rock was
    /// removed; the crater is recorded and a TerrainCarved event tells the view to remesh.
    /// </summary>
    public bool Carve(Vector3 center, float radius, bool announce = true)
    {
        if (radius <= 0f || Sdf.CarveSphere(center, radius, Cave.ShellCells) is null) return false;
        _craters.Add(new Crater(center, radius));
        _flow?.Invalidate(center, radius);
        DigUpCoins(center, radius, announce);
        if (announce) Events.Add(new SimEvent(SimEventType.TerrainCarved, center, Vector3.UnitY, -1, radius));
        return true;
    }

    void DropBomb()
    {
        var p = Player;
        if (Bombs <= 0)
        {
            Events.Add(new SimEvent(SimEventType.ActiveNotReady, p.Position, Vector3.Zero, -1, 0f, "bomb"));
            return;
        }
        Bombs--;
        Vector3 forward = p.Forward;
        Vector3 at = p.Position + forward * 0.5f - Vector3.UnitY * 0.2f;
        if (Sdf.Sample(at) < BombRadius) at = p.Position;
        var bomb = new InkBomb
        {
            Id = NextId(),
            Position = at,
            PrevPosition = at,
            Velocity = forward * 3f + p.Velocity * 0.5f,
            Fuse = Tuning.BombFuse,
        };
        LiveBombs.Add(bomb);
        Events.Add(new SimEvent(SimEventType.BombDropped, at, forward, bomb.Id));
    }

    void StepBombs()
    {
        foreach (var b in LiveBombs)
        {
            b.PrevPosition = b.Position;
            b.Velocity.Y -= BombSink * Dt;
            b.Velocity *= MathF.Max(0f, 1f - 1.2f * Dt);
            MoveSphere(ref b.Position, ref b.Velocity, BombRadius);
            b.Fuse -= Dt;
        }
        foreach (var b in LiveBombs.Where(b => b.Fuse <= 0f).ToList())
        {
            LiveBombs.Remove(b);
            ExplodeBomb(b);
        }
    }

    /// <summary>Digs a big crater, hurts creatures in the blast — and Clementine too if she's close (2D §23).</summary>
    void ExplodeBomb(InkBomb b)
    {
        var t = Tuning;
        Explode(b.Position, t.BombBlastRadius, t.BombDamage, "bomb");
        Carve(b.Position, t.BombCraterRadius);
        Vector3 away = Player.Position - b.Position;
        if (away.Length() < t.BombBlastRadius && DamagePlayer(t.BombSelfDamage, b.Position))
            Player.Velocity += MathUtil.SafeNormalize(away, Vector3.UnitY) * 4f;
    }

    /// <summary>Debug: move Clementine instantly.</summary>
    public void Teleport(Vector3 at)
    {
        Player.Position = Player.PrevPosition = at;
        Player.Velocity = Vector3.Zero;
    }

    /// <summary>Debug and pickups: add ink bombs.</summary>
    public void AddBombs(int count) => Bombs = Math.Max(0, Bombs + count);

    void RestoreCraters(IEnumerable<float[]> craters)
    {
        // The terrain may already contain these craters (carved while loading); keep the record either way.
        foreach (var c in craters)
        {
            if (c.Length != 4) continue;
            var crater = new Crater(new Vector3(c[0], c[1], c[2]), c[3]);
            if (!Carve(crater.Center, crater.Radius, announce: false)) _craters.Add(crater);
        }
    }
}
