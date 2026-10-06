using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Core.Plane;

/// <summary>Placeholder numbers for shells (the currency) and the shop. Provisional, like the rest of plane combat.</summary>
public static class PlaneEconomyTuning
{
    /// <summary>Shells are picked up within this distance of her edge, and drift toward her from a little farther.</summary>
    public const float ShellReach = 0.6f, ShellMagnet = 2.5f, ShellMagnetSpeed = 9f;
    /// <summary>Every mob drops this many shells (inclusive range).</summary>
    public const int MobDropMin = 1, MobDropMax = 2;
    /// <summary>Shell caches at places: the item cache and secret rooms (inclusive ranges).</summary>
    public const int CacheMin = 6, CacheMax = 9, SecretMin = 8, SecretMax = 12;
    /// <summary>The shop: pearls, and a health top-up.</summary>
    public const int PearlPrice = 15, HealthPrice = 5;
    public const float HealthAmount = 25f;
    public const int PearlsForSale = 2;
    /// <summary>A stand sells when she touches it.</summary>
    public const float StandReach = 0.7f;
}

/// <summary>A shell lying on the seabed (or just dropped by a mob, still drifting).</summary>
public sealed class PlaneShell
{
    public Vector2 Position;
    public Vector2 Velocity;
    public int Value = 1;
    public bool Taken;
}

public enum StandKind { Pearl, Health }

/// <summary>Something for sale in the shop: touch it with enough shells to buy it.</summary>
public sealed class ShopStand
{
    public StandKind Kind;
    public string ItemId = "";
    public int Price;
    public Vector2 Position;
    public bool Sold;
}

/// <summary>What happened in the room, for the splash screen after the rift.</summary>
public sealed class PlaneStats
{
    public int MobsDefeated;
    public int ShellsCollected;
    public int ShellsSpent;
    public int PearlsFound;
    public float DamageTaken;
    public int BossesFreed;
}

public sealed partial class PlaneWorld
{
    public List<PlaneShell> Shells { get; } = new();
    public List<ShopStand> Stands { get; } = new();
    public PlaneStats Stats { get; } = new();

    Rng _drops = null!;
    /// <summary>The stand she was turned away from (too few shells), so she is told once per approach.</summary>
    int _denied = -1;

    /// <summary>Shell caches at the item cache and secret rooms, scattered round each place's middle.</summary>
    void PlaceShells()
    {
        _drops = new Rng(Map.Seed ^ 0x5E115UL ^ ((ulong)Map.Reef << 24) ^ ((ulong)Map.Attempt << 48));
        foreach (var poi in Map.Pois)
        {
            (int min, int max) = poi.Kind switch
            {
                PoiKind.ItemSpawn => (PlaneEconomyTuning.CacheMin, PlaneEconomyTuning.CacheMax),
                PoiKind.Secret => (PlaneEconomyTuning.SecretMin, PlaneEconomyTuning.SecretMax),
                _ => (0, 0),
            };
            int count = min + (max > min ? _drops.Int(max - min + 1) : 0);
            for (int i = 0; i < count; i++)
            {
                float a = _drops.Range(0f, MathF.Tau), r = _drops.Range(0.8f, MathF.Min(3f, poi.Radius - 1f));
                var at = poi.Position + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
                Shells.Add(new PlaneShell { Position = Clear(at, 0.2f) ? at : poi.Position });
            }
        }
    }

    /// <summary>The shop's stands in a row across its chamber: pearls she does not have yet, and a health top-up.</summary>
    void PlaceShop()
    {
        var shop = Map.Pois.FirstOrDefault(p => p.Kind == PoiKind.Shop);
        if (shop is null) return;
        Vector2 facing = shop.Cave >= 0 ? Map.Caves[shop.Cave].Facing : Vector2.UnitY;
        Vector2 side = new(-facing.Y, facing.X);
        var rng = new Rng(Map.Seed ^ 0x5409UL ^ ((ulong)Map.Reef << 28) ^ ((ulong)Map.Attempt << 50));
        var offer = PlaneRun.ShotPearls.Where(id => Run.CanOffer(id) && Pearls.All(q => q.ItemId != id)).ToList();
        var items = new List<ShopStand>();
        for (int i = 0; i < PlaneEconomyTuning.PearlsForSale && offer.Count > 0; i++)
        {
            int k = rng.Int(offer.Count);
            items.Add(new ShopStand { Kind = StandKind.Pearl, ItemId = offer[k], Price = PlaneEconomyTuning.PearlPrice });
            offer.RemoveAt(k);
        }
        items.Add(new ShopStand { Kind = StandKind.Health, Price = PlaneEconomyTuning.HealthPrice });
        for (int i = 0; i < items.Count; i++)
        {
            float along = (i - (items.Count - 1) * 0.5f) * 2.4f;
            items[i].Position = shop.Position + side * along - facing * 0.8f;
            Stands.Add(items[i]);
        }
    }

    /// <summary>A defeated mob leaves shells, flung a little way.</summary>
    void DropShells(Vector2 at)
    {
        int count = PlaneEconomyTuning.MobDropMin + _drops.Int(PlaneEconomyTuning.MobDropMax - PlaneEconomyTuning.MobDropMin + 1);
        for (int i = 0; i < count; i++)
        {
            float a = _drops.Range(0f, MathF.Tau);
            Shells.Add(new PlaneShell { Position = at, Velocity = new Vector2(MathF.Cos(a), MathF.Sin(a)) * _drops.Range(2f, 4f) });
        }
    }

    void StepEconomy()
    {
        var p = Player;
        foreach (var shell in Shells)
        {
            if (shell.Taken) continue;
            Vector2 to = p.Position - shell.Position;
            float d = to.Length();
            if (d < Radius + PlaneEconomyTuning.ShellMagnet && d > 1e-4f) shell.Velocity = to / d * PlaneEconomyTuning.ShellMagnetSpeed;
            else shell.Velocity *= MathF.Exp(-4f * Dt);
            Vector2 next = shell.Position + shell.Velocity * Dt;
            if (Map.IsOpen(next)) shell.Position = next;
            else shell.Velocity = Vector2.Zero;
            if (Vector2.Distance(shell.Position, p.Position) > Radius + PlaneEconomyTuning.ShellReach) continue;
            shell.Taken = true;
            Run.Shells += shell.Value;
            Stats.ShellsCollected += shell.Value;
            Events.Add(new PlaneEvent(PlaneEventType.ShellCollected, shell.Position, Vector2.Zero));
        }
        Shells.RemoveAll(s => s.Taken);

        int near = -1;
        for (int i = 0; i < Stands.Count; i++)
        {
            var stand = Stands[i];
            if (stand.Sold || Vector2.Distance(stand.Position, p.Position) > Radius + PlaneEconomyTuning.StandReach) continue;
            near = i;
            // A top-up is only sold to someone hurt.
            if (stand.Kind == StandKind.Health && p.Hp >= Run.MaxHp) continue;
            if (Run.Shells < stand.Price)
            {
                if (_denied != i) Events.Add(new PlaneEvent(PlaneEventType.CannotAfford, stand.Position, Vector2.Zero));
                _denied = i;
                continue;
            }
            Run.Shells -= stand.Price;
            Stats.ShellsSpent += stand.Price;
            stand.Sold = true;
            if (stand.Kind == StandKind.Health) p.Hp = MathF.Min(p.Hp + PlaneEconomyTuning.HealthAmount, Run.MaxHp);
            else
            {
                float before = Run.MaxHp;
                Run.Add(stand.ItemId);
                p.Hp = MathF.Min(p.Hp + MathF.Max(Run.MaxHp - before, 0f), Run.MaxHp);
                LastPearl = stand.ItemId;
                Stats.PearlsFound++;
                Events.Add(new PlaneEvent(PlaneEventType.PearlCollected, stand.Position, Vector2.Zero));
            }
            Events.Add(new PlaneEvent(PlaneEventType.Purchased, stand.Position, Vector2.Zero));
        }
        if (near < 0) _denied = -1;
    }
}
