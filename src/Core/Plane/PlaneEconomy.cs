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
    /// <summary>Pirate's Doubloon: shell caches hold this many times as many shells.</summary>
    public const float RichCaches = 1.5f;
    /// <summary>Remora Sucker: shells drift to her from this far instead.</summary>
    public const float RemoraMagnet = 7.5f;
    /// <summary>
    /// What a freed mob leaves, one roll each: a heart (HeartChance), else shells (ShellChance: one, or two at
    /// TwoShells of those), else nothing. About one heart and six shells a level from mobs.
    /// </summary>
    public const float HeartChance = 0.06f, ShellChance = 0.3f, TwoShells = 0.25f;
    /// <summary>A heart pickup heals one heart (20 HP, 2D §23). She takes it only when hurt; otherwise it waits.</summary>
    public const float HeartHeal = 20f, HeartReach = 0.6f;
    /// <summary>Shell caches at places: the item cache and secret rooms (inclusive ranges).</summary>
    public const int CacheMin = 6, CacheMax = 9, SecretMin = 8, SecretMax = 12;
    /// <summary>The shop: at most one pearl (offered this often), and always a heart (a health top-up).</summary>
    public const int PearlPrice = 30, HealthPrice = 5;
    public const float HealthAmount = 25f;
    public const float PearlOffered = 0.5f;
    /// <summary>The shop's stand slots, left to right: the pearl, the heart, and a third kept for wares to come.</summary>
    public const int PearlSlot = 0, HeartSlot = 1, Slots = 3;
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

/// <summary>A heart dropped by a freed mob: it floats where it fell until she, hurt, swims over it.</summary>
public sealed class PlaneHeart
{
    public Vector2 Position;
    public Vector2 Velocity;
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
    public List<PlaneHeart> Hearts { get; } = new();
    public List<ShopStand> Stands { get; } = new();
    public PlaneStats Stats { get; } = new();

    Rng _drops = null!;
    /// <summary>The stand she was turned away from (too few shells), so she is told once per approach.</summary>
    int _denied = -1;

    /// <summary>Shell caches at the item cache and secret rooms, scattered round each place's middle.</summary>
    void PlaceShells()
    {
        _drops = new Rng(Map.Seed ^ 0x5E115UL ^ ((ulong)Map.Level << 24) ^ ((ulong)Map.Depth << 32) ^ ((ulong)Map.Attempt << 48));
        foreach (var poi in Map.Pois)
        {
            (int min, int max) = poi.Kind switch
            {
                PoiKind.ShellCache => (PlaneEconomyTuning.CacheMin, PlaneEconomyTuning.CacheMax),
                PoiKind.Secret => (PlaneEconomyTuning.SecretMin, PlaneEconomyTuning.SecretMax),
                _ => (0, 0),
            };
            int count = min + (max > min ? _drops.Int(max - min + 1) : 0);
            // Pirate's Doubloon: shell caches hold half as many again.
            if (poi.Kind == PoiKind.ShellCache && Run.Loadout.Flags.Contains("richCaches")) count = (int)MathF.Round(count * PlaneEconomyTuning.RichCaches);
            for (int i = 0; i < count; i++)
            {
                float a = _drops.Range(0f, MathF.Tau), r = _drops.Range(0.8f, MathF.Min(3f, poi.Radius - 1f));
                var at = poi.Position + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
                Shells.Add(new PlaneShell { Position = Clear(at, 0.2f) ? at : poi.Position });
            }
        }
    }

    /// <summary>
    /// The shop's stands in fixed slots across its chamber: a pearl she does not have yet (some visits none), a heart,
    /// and an empty third slot for wares to come.
    /// </summary>
    void PlaceShop()
    {
        var shop = Map.Pois.FirstOrDefault(p => p.Kind == PoiKind.Shop);
        if (shop is null) return;
        Vector2 facing = shop.Cave >= 0 ? Map.Caves[shop.Cave].Facing : Vector2.UnitY;
        Vector2 side = new(-facing.Y, facing.X);
        var rng = new Rng(Map.Seed ^ 0x5409UL ^ ((ulong)Map.Level << 28) ^ ((ulong)Map.Depth << 36) ^ ((ulong)Map.Attempt << 50));
        var offer = PlaneRun.PortedPearls.Where(id => Run.CanOffer(id) && Pearls.All(q => q.ItemId != id)).ToList();
        Vector2 Slot(int i) => shop.Position + side * ((i - (PlaneEconomyTuning.Slots - 1) * 0.5f) * 2.4f) - facing * 0.8f;
        if (offer.Count > 0 && rng.NextFloat() < PlaneEconomyTuning.PearlOffered)
            Stands.Add(new ShopStand { Kind = StandKind.Pearl, ItemId = offer[rng.Int(offer.Count)], Price = PlaneEconomyTuning.PearlPrice, Position = Slot(PlaneEconomyTuning.PearlSlot) });
        Stands.Add(new ShopStand { Kind = StandKind.Health, Price = PlaneEconomyTuning.HealthPrice, Position = Slot(PlaneEconomyTuning.HeartSlot) });
    }

    /// <summary>A defeated mob leaves shells, flung a little way.</summary>
    void Drop(Vector2 at)
    {
        // Lucky Sea Glass: each point of luck is one more shell from every freed foe.
        int lucky = (int)MathF.Round(Run.Loadout.Stats.Luck);
        for (int i = 0; i < lucky; i++) Shells.Add(new PlaneShell { Position = at, Velocity = Fling(2f, 4f) });
        float roll = _drops.NextFloat();
        if (roll < PlaneEconomyTuning.HeartChance)
        {
            Hearts.Add(new PlaneHeart { Position = at, Velocity = Fling(1.5f, 2.5f) });
            return;
        }
        if (roll >= PlaneEconomyTuning.HeartChance + PlaneEconomyTuning.ShellChance) return;
        int count = _drops.NextFloat() < PlaneEconomyTuning.TwoShells ? 2 : 1;
        for (int i = 0; i < count; i++) Shells.Add(new PlaneShell { Position = at, Velocity = Fling(2f, 4f) });
    }

    /// <summary>A drop flung a little way in a random direction.</summary>
    Vector2 Fling(float min, float max)
    {
        float a = _drops.Range(0f, MathF.Tau);
        return new Vector2(MathF.Cos(a), MathF.Sin(a)) * _drops.Range(min, max);
    }

    void StepEconomy()
    {
        var p = Player;
        float magnet = Run.Loadout.Flags.Contains("magnet") ? PlaneEconomyTuning.RemoraMagnet : PlaneEconomyTuning.ShellMagnet;
        foreach (var shell in Shells)
        {
            if (shell.Taken) continue;
            Vector2 to = p.Position - shell.Position;
            float d = to.Length();
            if (d < Radius + magnet && d > 1e-4f) shell.Velocity = to / d * PlaneEconomyTuning.ShellMagnetSpeed;
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

        // Hearts: they drift to a stop; hurt, she takes one by swimming over it (Remora Sucker draws it to her).
        bool hurt = p.Hp < Run.MaxHp;
        foreach (var heart in Hearts)
        {
            Vector2 to = p.Position - heart.Position;
            float d = to.Length();
            if (hurt && d < Radius + magnet && d > 1e-4f) heart.Velocity = to / d * PlaneEconomyTuning.ShellMagnetSpeed;
            else heart.Velocity *= MathF.Exp(-4f * Dt);
            Vector2 next = heart.Position + heart.Velocity * Dt;
            if (Map.IsOpen(next)) heart.Position = next;
            else heart.Velocity = Vector2.Zero;
            if (!hurt || d > Radius + PlaneEconomyTuning.HeartReach) continue;
            heart.Taken = true;
            p.Hp = MathF.Min(p.Hp + PlaneEconomyTuning.HeartHeal, Run.MaxHp);
            hurt = p.Hp < Run.MaxHp;
            Events.Add(new PlaneEvent(PlaneEventType.HeartCollected, heart.Position, Vector2.Zero));
        }
        Hearts.RemoveAll(h => h.Taken);

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
            else Absorb(stand.ItemId, stand.Position);
            Events.Add(new PlaneEvent(PlaneEventType.Purchased, stand.Position, Vector2.Zero));
        }
        if (near < 0) _denied = -1;
    }
}
