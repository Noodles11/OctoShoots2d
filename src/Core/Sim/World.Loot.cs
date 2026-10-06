using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Items;
using OctoShoots.Core.Loot;
using OctoShoots.Core.Run;
using OctoShoots.Core.Saves;

namespace OctoShoots.Core.Sim;

/// <summary>Loot: pearl shells, treasure chests, pickups, creature drops and buried coins.</summary>
public sealed partial class World
{
    readonly HashSet<int> _dugCoins = new();
    RunStreams _streams = null!;

    public List<Pickup> Pickups { get; } = new();
    public List<PearlShell> Shells { get; } = new();
    public List<TreasureChest> Chests { get; } = new();

    /// <summary>Achievements that unlock items in the pools (from the profile).</summary>
    public ISet<string> Unlocked { get; private set; } = new HashSet<string>();

    void SetUpLoot(ISet<string>? unlocked)
    {
        Unlocked = unlocked ?? new HashSet<string>();
        var plan = Cave.Loot;
        var placed = new HashSet<string>();
        for (int i = 0; i < plan.Shells.Count; i++)
        {
            var spot = plan.Shells[i];
            string? item = null;
            if (Catalog is not null)
            {
                var drawn = ItemPools.Draw(Catalog, spot.Pool, _streams.Shell(plan.Depth, plan.Reef, i), Unlocked, placed);
                if (drawn is not null)
                {
                    item = drawn.Id;
                    placed.Add(item);
                }
            }
            int price = spot.ForSale && item is not null ? LootRules.Price(Catalog![item].Quality) : 0;
            Shells.Add(new PearlShell { Id = NextId(), PlanIndex = i, Position = spot.Position, ItemId = item, Price = price });
        }
        for (int i = 0; i < plan.Chests.Count; i++)
            Chests.Add(new TreasureChest { Id = NextId(), PlanIndex = i, Position = plan.Chests[i].Position });
        for (int i = 0; i < plan.Pickups.Count; i++)
            Pickups.Add(new Pickup { Id = NextId(), Kind = plan.Pickups[i].Kind, Position = plan.Pickups[i].Position, PrevPosition = plan.Pickups[i].Position, PlanIndex = i });
    }

    void StepLoot()
    {
        var p = Player;

        foreach (var s in Shells)
        {
            float d = Vector3.Distance(s.Position, p.Position);
            if (!s.Open && d < LootRules.ShellOpenRadius)
            {
                s.Open = true;
                Events.Add(new SimEvent(SimEventType.ShellOpened, s.Position, Vector3.UnitY, s.Id, s.Price, s.ItemId));
            }
            else if (s.Open && d > LootRules.ShellCloseRadius)
            {
                s.Open = false;
                Events.Add(new SimEvent(SimEventType.ShellClosed, s.Position, Vector3.UnitY, s.Id));
            }
            if (s.Open && s.ItemId is not null && Vector3.Distance(s.PearlPosition, p.Position) < LootRules.PearlTakeRadius) TakePearl(s);
        }

        foreach (var k in Pickups)
        {
            k.PrevPosition = k.Position;
            float buoyancy = k.Kind == PickupKind.FoamHeart ? 0.8f : -1.5f;
            k.Velocity.Y += buoyancy * Dt;
            k.Velocity *= MathF.Max(0f, 1f - 2f * Dt);
            Vector3 toPlayer = p.Position - k.Position;
            float dist = toPlayer.Length();
            if (Loadout.Flags.Contains("magnet") && dist < LootRules.MagnetRadius && dist > 0.01f)
                k.Velocity += toPlayer / dist * 9f * Dt;
            MoveSphere(ref k.Position, ref k.Velocity, LootRules.PickupRadius);
            ClampToSurface(ref k.Position, ref k.Velocity, LootRules.PickupRadius);
        }
        var collected = Pickups.Where(k => Vector3.Distance(k.Position, p.Position) < LootRules.CollectRadius && TryCollect(k)).ToList();
        foreach (var k in collected) Pickups.Remove(k);
    }

    void TakePearl(PearlShell s)
    {
        if (s.Price > 0)
        {
            if (Coins < s.Price)
            {
                // Only nag once per approach: push her back a little.
                Events.Add(new SimEvent(SimEventType.TooPoor, s.PearlPosition, Vector3.UnitY, s.Id, s.Price, s.ItemId));
                Player.Velocity += MathUtil.SafeNormalize(Player.Position - s.PearlPosition, Vector3.UnitY) * 3f;
                return;
            }
            Coins -= s.Price;
        }
        string item = s.ItemId!;
        s.ItemId = null;
        Events.Add(new SimEvent(SimEventType.PearlTaken, s.PearlPosition, Vector3.UnitY, s.Id, s.Price, item));
        GiveItem(item);
    }

    /// <summary>Applies a pickup if it does anything right now (hearts wait while HP is full).</summary>
    bool TryCollect(Pickup k)
    {
        var p = Player;
        float maxHp = Loadout.Stats.MaxHp;
        switch (k.Kind)
        {
            case PickupKind.Coin:
            case PickupKind.Nickel:
            case PickupKind.Dime:
                Coins += LootRules.Coins(k.Kind);
                break;
            case PickupKind.Heart:
            case PickupKind.HalfHeart:
                if (p.Hp >= maxHp) return false;
                p.Hp = MathF.Min(maxHp, p.Hp + (k.Kind == PickupKind.Heart ? LootRules.HeartHeal : LootRules.HalfHeartHeal));
                break;
            case PickupKind.FoamHeart:
                if (maxHp + p.Foam >= HealthRules.MaxTotal) return false;
                p.Foam = MathF.Min(p.Foam + LootRules.FoamAmount, HealthRules.MaxTotal - maxHp);
                break;
            case PickupKind.Bomb:
                Bombs++;
                break;
            case PickupKind.GlowJelly:
                if (ActiveMaxCharge <= 0f || ActiveReady) return false;
                RechargeActive(0.3f);
                break;
        }
        _collectedPlan.Add(k.PlanIndex);
        Events.Add(new SimEvent(SimEventType.PickupCollected, k.Position, Vector3.UnitY, k.Id, LootRules.Coins(k.Kind), k.Kind.ToString()));
        return true;
    }

    readonly HashSet<int> _collectedPlan = new();

    void SpawnPickup(PickupKind kind, Vector3 at, Vector3 velocity) =>
        Pickups.Add(new Pickup { Id = NextId(), Kind = kind, Position = at, PrevPosition = at, Velocity = velocity });

    /// <summary>Shots and blasts break chests open; the haul bursts out (seeded per chest).</summary>
    void OpenChest(TreasureChest c)
    {
        if (c.Opened) return;
        c.Opened = true;
        var plan = Cave.Loot;
        var rng = _streams.Chest(plan.Depth, plan.Reef, c.PlanIndex);
        foreach (var kind in LootRules.RollChest(rng))
            SpawnPickup(kind, c.Position + new Vector3(0f, 0.5f, 0f), new Vector3(rng.Range(-1.5f, 1.5f), rng.Range(1.5f, 3f), rng.Range(-1.5f, 1.5f)));
        Events.Add(new SimEvent(SimEventType.ChestOpened, c.Position, Vector3.UnitY, c.Id));
    }

    TreasureChest? ChestAt(Vector3 p, float radius)
    {
        foreach (var c in Chests)
            if (!c.Opened && Vector3.Distance(c.Position + new Vector3(0f, 0.2f, 0f), p) < LootRules.ChestRadius + radius) return c;
        return null;
    }

    void OpenChestsInBlast(Vector3 at, float radius)
    {
        foreach (var c in Chests)
            if (!c.Opened && Vector3.Distance(c.Position, at) < radius + LootRules.ChestRadius) OpenChest(c);
    }

    void DropLoot(Enemy e)
    {
        // Moon jellies sometimes leave a glow jelly, which recharges the active item.
        if (e.Kind == EnemyKind.MoonJelly && _combat.NextFloat() < 0.2f)
        {
            SpawnPickup(PickupKind.GlowJelly, e.Position, new Vector3(0f, 1f, 0f));
            return;
        }
        var kind = LootRules.RollDrop(_combat, Loadout.Stats.Luck);
        if (kind is { } k) SpawnPickup(k, e.Position, new Vector3(0f, 1.5f, 0f));
        if (Loadout.Flags.Contains("richDrops") && _combat.NextFloat() < 0.3f) SpawnPickup(PickupKind.Coin, e.Position, new Vector3(0.5f, 2f, 0f));
    }

    /// <summary>A crater on top of buried coins digs them out (2D §26).</summary>
    void DigUpCoins(Vector3 center, float radius, bool announce)
    {
        var buried = Cave.Loot.Buried;
        for (int i = 0; i < buried.Count; i++)
        {
            if (_dugCoins.Contains(i) || Vector3.Distance(buried[i].Position, center) > radius + 0.3f) continue;
            _dugCoins.Add(i);
            if (!announce) continue;
            for (int k = 0; k < buried[i].Amount; k++)
                SpawnPickup(PickupKind.Coin, buried[i].Position + new Vector3(0f, 0.3f, 0f), new Vector3(_combat.Range(-1f, 1f), _combat.Range(2f, 3.5f), _combat.Range(-1f, 1f)));
            Events.Add(new SimEvent(SimEventType.CoinsDugUp, buried[i].Position, Vector3.UnitY, -1, buried[i].Amount));
        }
    }

    /// <summary>Nothing swims above the sea surface.</summary>
    void ClampToSurface(ref Vector3 position, ref Vector3 velocity, float radius)
    {
        float top = Cave.SurfaceY - radius;
        if (position.Y <= top) return;
        position.Y = top;
        if (velocity.Y > 0f) velocity.Y = 0f;
    }

    void SaveLoot(SuspendedRun run)
    {
        run.TakenShells = Shells.Where(s => s.ItemId is null).Select(s => s.PlanIndex).ToList();
        run.OpenedChests = Chests.Where(c => c.Opened).Select(c => c.PlanIndex).ToList();
        run.CollectedPickups = _collectedPlan.Where(i => i >= 0).ToList();
        run.DugCoins = _dugCoins.ToList();
    }

    void RestoreLoot(SuspendedRun run)
    {
        foreach (var s in Shells.Where(s => run.TakenShells.Contains(s.PlanIndex))) s.ItemId = null;
        foreach (var c in Chests.Where(c => run.OpenedChests.Contains(c.PlanIndex))) c.Opened = true;
        Pickups.RemoveAll(k => k.PlanIndex >= 0 && run.CollectedPickups.Contains(k.PlanIndex));
        foreach (int i in run.CollectedPickups) _collectedPlan.Add(i);
        foreach (int i in run.DugCoins) _dugCoins.Add(i);
    }

    /// <summary>Debug: give sand dollars.</summary>
    public void AddCoins(int amount) => Coins = Math.Max(0, Coins + amount);
}
