using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Items;
using OctoShoots.Core.Saves;

namespace OctoShoots.Core.Sim;

public sealed partial class World
{
    readonly List<string> _items = new();

    public ItemCatalog? Catalog { get; }
    public Loadout Loadout { get; private set; }
    public IReadOnlyList<string> Items => _items;

    public int Coins { get; private set; }
    public int Bombs { get; private set; }
    public int Kills { get; private set; }

    /// <summary>Seconds of recharge built up on the held active item; it is ready at <see cref="ActiveMaxCharge"/>.</summary>
    public float ActiveCharge { get; private set; }

    /// <summary>Recharge time of the held active item in seconds (0 when none is held).</summary>
    public float ActiveMaxCharge => Loadout.Active?.Active?.Recharge ?? 0f;

    public bool ActiveReady => ActiveMaxCharge > 0f && ActiveCharge >= ActiveMaxCharge;

    /// <summary>Active items recharge over time (faster with the recharge stat), not by clearing anything.</summary>
    void StepActive()
    {
        float max = ActiveMaxCharge;
        if (max <= 0f || ActiveCharge >= max) return;
        ActiveCharge = MathF.Min(max, ActiveCharge + Dt * Loadout.Stats[Stat.ActiveRecharge]);
        if (ActiveCharge >= max)
            Events.Add(new SimEvent(SimEventType.ActiveCharged, Player.Position, Vector3.Zero, -1, 0f, Loadout.Active?.Id));
    }

    /// <summary>Adds a share (0–1) of the full recharge time, e.g. from a glow jelly.</summary>
    void RechargeActive(float fraction) => ActiveCharge = MathF.Min(ActiveMaxCharge, ActiveCharge + fraction * ActiveMaxCharge);

    /// <summary>Picks up an item: adds it, rebuilds the loadout and runs its onPickup effects.</summary>
    public void GiveItem(string id)
    {
        if (Catalog is null || !Catalog.TryGet(id, out var item)) throw new ArgumentException($"Unknown item '{id}'", nameof(id));
        var before = Loadout;

        // Only one active item is held: a new one replaces the old.
        if (item.Kind == ItemKind.Active) _items.RemoveAll(i => Catalog[i].Kind == ItemKind.Active);
        _items.Add(id);
        RefreshLoadout();

        Events.Add(new SimEvent(SimEventType.ItemGained, Player.Position, Vector3.Zero, -1, 0f, id));
        if (item.Kind == ItemKind.Active) ActiveCharge = ActiveMaxCharge; // fresh actives come charged
        Dispatch(Trigger.OnPickup, Player.Position, item.Effects);
        AnnounceNewCombos(before);
    }

    /// <summary>
    /// Replaces the whole item list (debug picker). onPickup effects run for newly added items unless
    /// <paramref name="runPickupEffects"/> is false (carrying items over to a restarted world).
    /// </summary>
    public void SetItems(IEnumerable<string> ids, bool runPickupEffects = true)
    {
        var wanted = ids.ToList();
        var before = Loadout;
        var added = wanted.Where(id => !_items.Contains(id)).ToList();
        _items.Clear();
        _items.AddRange(wanted.Where(id => Catalog?.Contains(id) == true));
        RefreshLoadout();
        if (!runPickupEffects)
        {
            ActiveCharge = ActiveMaxCharge;
            return;
        }
        foreach (var id in added)
        {
            if (Catalog is null || !Catalog.TryGet(id, out var item)) continue;
            Events.Add(new SimEvent(SimEventType.ItemGained, Player.Position, Vector3.Zero, -1, 0f, id));
            Dispatch(Trigger.OnPickup, Player.Position, item.Effects);
            if (item.Kind == ItemKind.Active) ActiveCharge = ActiveMaxCharge;
        }
        AnnounceNewCombos(before);
    }

    /// <summary>Rebuilds stats from items and the current tuning (call after editing tuning live).</summary>
    public void RefreshLoadout()
    {
        Loadout = Loadout.Build(Catalog, _items, Tuning);
        var p = Player;
        p.Hp = MathF.Min(p.Hp, Loadout.Stats.MaxHp);
        p.Foam = MathF.Min(p.Foam, MathF.Max(0f, HealthRules.MaxTotal - Loadout.Stats.MaxHp));
        ActiveCharge = Math.Min(ActiveCharge, ActiveMaxCharge);
        p.Bubbles = Math.Min(p.Bubbles, BubbleCapacity);
    }

    void AnnounceNewCombos(Loadout before)
    {
        foreach (var s in Loadout.Synergies.Except(before.Synergies))
            Events.Add(new SimEvent(SimEventType.SynergyActivated, Player.Position, Vector3.Zero, -1, 0f, s));
        foreach (var t in Loadout.Transformations.Except(before.Transformations))
            Events.Add(new SimEvent(SimEventType.TransformationActivated, Player.Position, Vector3.Zero, -1, 0f, t));
    }

    void Dispatch(Trigger trigger, Vector3 at) => Dispatch(trigger, at, Loadout.Effects);

    void Dispatch(Trigger trigger, Vector3 at, IEnumerable<TriggerEffect> effects)
    {
        foreach (var e in effects)
        {
            if (e.Trigger != trigger) continue;
            if (e.Chance < 1f && _combat.NextFloat() >= StatusRules.WithLuck(e.Chance, Loadout.Stats.Luck)) continue;
            ApplyEffect(e, at);
        }
    }

    void ApplyEffect(TriggerEffect e, Vector3 at)
    {
        var p = Player;
        switch (e.Action)
        {
            case EffectAction.Heal:
                p.Hp = MathF.Min(Loadout.Stats.MaxHp, p.Hp + e.Value);
                break;
            case EffectAction.Foam:
                p.Foam = MathF.Min(p.Foam + e.Value, MathF.Max(0f, HealthRules.MaxTotal - Loadout.Stats.MaxHp));
                break;
            case EffectAction.Coins:
                Coins += (int)e.Value;
                break;
            case EffectAction.Bombs:
                Bombs += (int)e.Value;
                break;
            case EffectAction.Frenzy:
                p.FrenzyTimer = MathF.Max(p.FrenzyTimer, e.Duration);
                p.FrenzyMult = MathF.Max(1f, e.Value);
                break;
            case EffectAction.Shards:
                SpawnShards(at, (int)e.Value);
                break;
        }
    }

    void OnEnemyKilled(Enemy e)
    {
        Kills++;
        Dispatch(Trigger.OnKill, e.Position);
    }

    // ───────────────────────── active items ─────────────────────────

    void UseActive()
    {
        var item = Loadout.Active;
        var p = Player;
        if (item?.Active is not { } a) return;
        if (ActiveCharge < a.Recharge)
        {
            Events.Add(new SimEvent(SimEventType.ActiveNotReady, p.Position, Vector3.Zero, -1, ActiveCharge, item.Id));
            return;
        }
        ActiveCharge = 0;
        Vector3 forward = p.Forward;
        bool implemented = true;

        switch (a.Action)
        {
            case ActiveAction.ConchHorn:
                foreach (var e in Enemies)
                {
                    if (!e.Alive || Vector3.Distance(e.Position, p.Position) > a.Radius) continue;
                    e.StunTimer = MathF.Max(e.StunTimer, a.Duration);
                    e.Velocity += MathUtil.SafeNormalize(e.Position - p.Position, Vector3.UnitY) * StatusRules.StunKnockback;
                    if (e.State == EnemyState.Telegraph) e.State = EnemyState.Hunt;
                }
                break;
            case ActiveAction.BubbleShield:
                p.ShieldTimer = a.Duration;
                break;
            case ActiveAction.TidalWave:
                foreach (var e in Enemies)
                {
                    if (!e.Alive) continue;
                    Vector3 to = e.Position - p.Position;
                    if (to.Length() > a.Radius || MathUtil.AngleBetween(forward, to) > 50f * MathUtil.Deg2Rad) continue;
                    e.Velocity += forward * 10f;
                    DamageEnemy(e, a.Value, forward, 0f);
                    if (Loadout.Has("black_tide")) Explode(e.Position, 1.5f, Loadout.Stats.Damage * 2f);
                }
                break;
            case ActiveAction.GlowBurst:
                p.GlowBurstTimer = a.Duration;
                p.GlowBurstShots = (int)a.Value;
                break;
            case ActiveAction.KrakenCall:
                var alive = Enemies.Where(e => e.Alive).ToList();
                for (int i = 0; i < 3 && alive.Count > 0; i++)
                {
                    var e = alive[_combat.Int(alive.Count)];
                    if (!e.Alive) continue;
                    Events.Add(new SimEvent(SimEventType.KrakenSlam, e.Position, Vector3.UnitY, e.Id));
                    e.StunTimer = MathF.Max(e.StunTimer, 0.5f);
                    DamageEnemy(e, a.Value, -Vector3.UnitY, 0f);
                }
                break;
            case ActiveAction.InkCloud:
                p.HiddenTimer = a.Duration;
                Clouds.Add(new InkCloud { Id = NextId(), Position = p.Position, Radius = a.Radius, Life = a.Duration, MaxLife = a.Duration });
                foreach (var e in Enemies)
                    if (e.State == EnemyState.Telegraph) e.State = EnemyState.Hunt;
                break;
            case ActiveAction.WhaleSong:
                p.Hp = MathF.Min(Loadout.Stats.MaxHp, p.Hp + a.Value);
                break;
            default:
                // Treasure Map, Mimic Clam, Sea Dice and Anchor Drop act on the map, pedestals, pickups
                // and terrain, which arrive in later steps.
                implemented = false;
                break;
        }
        Events.Add(new SimEvent(SimEventType.ActiveUsed, p.Position, forward, -1, implemented ? 1f : 0f, item.Id));
    }

    // ───────────────────────── saves ─────────────────────────

    /// <summary>Captures the run for "Save & Quit" (2D §13.1).</summary>
    public SuspendedRun Snapshot(string seedCode, bool customSeed)
    {
        var run = SnapshotCore(seedCode, customSeed);
        SaveLoot(run);
        return run;
    }

    SuspendedRun SnapshotCore(string seedCode, bool customSeed) => new()
    {
        Seed = seedCode,
        CustomSeed = customSeed,
        Items = _items.ToList(),
        ActiveCharge = ActiveCharge,
        Hp = Player.Hp,
        Foam = Player.Foam,
        Coins = Coins,
        Bombs = Bombs,
        Position = new[] { Player.Position.X, Player.Position.Y, Player.Position.Z },
        Yaw = Player.Yaw,
        Pitch = Player.Pitch,
        Ticks = Tick,
        Kills = Kills,
        Craters = _craters.Select(c => new[] { c.Center.X, c.Center.Y, c.Center.Z, c.Radius }).ToList(),
    };

    /// <summary>Restores a suspended run into this (freshly created) world without re-running pickup effects.</summary>
    public void Restore(SuspendedRun run)
    {
        _items.Clear();
        _items.AddRange(run.Items.Where(id => Catalog?.Contains(id) == true));
        RefreshLoadout();
        var p = Player;
        p.Hp = MathF.Min(run.Hp, Loadout.Stats.MaxHp);
        p.Foam = run.Foam;
        p.Position = p.PrevPosition = new Vector3(run.Position[0], run.Position[1], run.Position[2]);
        p.Yaw = run.Yaw;
        p.Pitch = run.Pitch;
        Coins = run.Coins;
        Bombs = run.Bombs;
        Kills = run.Kills;
        ActiveCharge = Math.Min(run.ActiveCharge, ActiveMaxCharge);
        Tick = run.Ticks;
        RestoreCraters(run.Craters);
        RestoreLoot(run);
    }
}
