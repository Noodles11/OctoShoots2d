using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Items;
using OctoShoots.Core.Run;
using OctoShoots.Core.Terrain;

namespace OctoShoots.Core.Sim;

/// <summary>
/// The deterministic gameplay simulation. Advance it with <see cref="Step"/> at a fixed 60 Hz;
/// read <see cref="Events"/> after each step.
/// </summary>
public sealed partial class World
{
    public const int TickRate = 60;
    public const float Dt = 1f / TickRate;

    readonly Rng _rng;
    readonly Rng _combat;
    readonly List<Projectile> _pendingProjectiles = new();
    int _nextId = 1;
    bool _restartPending;

    public World(Cave cave, Tuning tuning, ulong seed, ItemCatalog? catalog = null, ISet<string>? unlocked = null, CreatureCatalog? creatures = null)
    {
        Cave = cave;
        Creatures = creatures;
        Tuning = tuning;
        Seed = seed;
        Catalog = catalog;
        _streams = new RunStreams(SeedCode.FromValue(seed));
        _rng = _streams.EnemyAi();
        _combat = _streams.Combat();
        Loadout = Loadout.Empty(tuning);
        Bombs = tuning.StartBombs;
        _spawnOrder = OrderSpawns(cave);
        ResetPlayer();
        SyncEnemyCount();
        SetUpLoot(unlocked);
        SetUpNests();
        SetUpDens();
    }

    readonly Vector3[] _spawnOrder;

    /// <summary>Creature spawns nearest the start first (at least 10 m away), so a big reef wakes up around you.</summary>
    static Vector3[] OrderSpawns(Cave cave)
    {
        var all = cave.EnemySpawns.OrderBy(s => Vector3.DistanceSquared(s, cave.PlayerSpawn)).ToArray();
        var away = all.Where(s => Vector3.Distance(s, cave.PlayerSpawn) >= 10f).ToArray();
        return away.Length > 0 ? away : all;
    }

    public Cave Cave { get; }
    public VoxelSdf Sdf => Cave.Sdf;
    public Tuning Tuning { get; set; }
    public ulong Seed { get; }
    public long Tick { get; private set; }

    public Player Player { get; } = new();
    public List<Enemy> Enemies { get; } = new();
    public List<Projectile> Projectiles { get; } = new();
    public List<InkCloud> Clouds { get; } = new();

    /// <summary>Events produced by the most recent <see cref="Step"/> (or item change).</summary>
    public List<SimEvent> Events { get; } = new();

    public void Step(in PlayerInput input)
    {
        Events.Clear();
        Tick++;

        Player.PrevPosition = Player.Position;
        foreach (var e in Enemies) e.PrevPosition = e.Position;
        foreach (var p in Projectiles) p.PrevPosition = p.Position;
        foreach (var c in Critters) c.PrevPosition = c.Position;

        SyncEnemyCount();
        StepPlayer(input);
        StepActive();
        if (input.UseActive) UseActive();
        if (input.DropBomb) DropBomb();
        StepWeapon(input);
        FlushProjectiles();
        UpdateSenses();
        StepEnemies(input);
        StepClownfish();
        StepCritters();
        FlushProjectiles();
        StepProjectiles();
        FlushProjectiles();
        StepBombs();
        FlushProjectiles();
        StepLoot();
        StepClouds();

        if (_restartPending)
        {
            _restartPending = false;
            RestartAfterDeath();
        }
    }

    void ResetPlayer()
    {
        var p = Player;
        p.Position = p.PrevPosition = Cave.PlayerSpawn;
        p.Velocity = Vector3.Zero;
        p.Hp = Loadout.Stats.MaxHp;
        p.IdleTime = 0f;
        p.WasMoving = false;
        p.JetTimer = p.DashTimer = p.DashInvulnerableTimer = p.DashCooldownTimer = 0f;
        p.HurtTimer = p.FireCooldown = 0f;
        p.Charge = p.BeamTimer = 0f;
        p.Bubbles = BubbleCapacity;
        p.RegrowDelay = p.RegrowProgress = 0f;
        p.ShieldTimer = p.GlowBurstTimer = p.FrenzyTimer = p.HiddenTimer = 0f;
    }

    /// <summary>M0 cave: death refills HP and resets the creatures; items are kept for testing.</summary>
    void RestartAfterDeath()
    {
        Player.Deaths++;
        ResetPlayer();
        Projectiles.Clear();
        _pendingProjectiles.Clear();
        Clouds.Clear();
        LiveBombs.Clear();
        int lantern = 0;
        // The reef resets: everything she freed is corrupted again.
        Critters.RemoveAll(c => c.FreedAge >= 0f);
        Clownfish.RemoveAll(f => f.Freed);
        foreach (var e in Enemies)
        {
            if (e.Kind == EnemyKind.ClownNinja) ResetNinja(e);
            else if (IsDenCreature(e.Kind)) SendHome(e);
            else SpawnEnemy(e, _spawnOrder[lantern++ % _spawnOrder.Length]);
        }
    }

    void SyncEnemyCount()
    {
        // Only lanternfish follow EnemyCount; ninjas belong to their nests.
        int target = Math.Clamp(Tuning.EnemyCount, 0, _spawnOrder.Length);
        int have = Enemies.Count(e => e.Kind == EnemyKind.Lanternfish);
        while (have < target)
        {
            var e = new Enemy { Id = _nextId++ };
            SpawnEnemy(e, _spawnOrder[have]);
            Enemies.Add(e);
            have++;
        }
        while (have > target)
        {
            Enemies.RemoveAt(Enemies.FindLastIndex(e => e.Kind == EnemyKind.Lanternfish));
            have--;
        }
    }

    void SpawnEnemy(Enemy e, Vector3 at)
    {
        e.Position = e.PrevPosition = at;
        e.Velocity = Vector3.Zero;
        e.Hp = Tuning.EnemyHp;
        e.State = EnemyState.Idle;
        e.StateTimer = 0f;
        e.AttackCooldown = 1.5f + _rng.Range(0f, 1f);
        e.HurtTimer = 0f;
        e.OffscreenAttack = false;
        e.FrozenTimer = e.BurnTimer = e.PoisonTimer = e.CharmTimer = e.SlowTimer = e.StunTimer = 0f;
        e.OrbitSign = _rng.NextFloat() < 0.5f ? -1f : 1f;
        Events.Add(new SimEvent(SimEventType.EnemySpawned, at, Vector3.Zero, e.Id));
    }

    /// <summary>Moves a sphere through the terrain, sliding along walls. Returns true when resting on a floor-like surface.</summary>
    bool MoveSphere(ref Vector3 position, ref Vector3 velocity, float radius)
    {
        Vector3 delta = velocity * Dt;
        int steps = Math.Max(1, (int)MathF.Ceiling(delta.Length() / (radius * 0.5f)));
        Vector3 stepDelta = delta / steps;
        bool floor = false;
        for (int s = 0; s < steps; s++)
        {
            position += stepDelta;
            for (int i = 0; i < 4; i++)
            {
                float d = Sdf.Sample(position);
                if (d >= radius) break;
                Vector3 n = Sdf.Gradient(position);
                position += n * (radius - d);
                float vn = Vector3.Dot(velocity, n);
                if (vn < 0f) velocity -= vn * n;
                if (n.Y > 0.6f) floor = true;
            }
        }
        return floor;
    }

    int NextId() => _nextId++;

    /// <summary>Projectiles spawned mid-update join the list here, so no list changes while iterating.</summary>
    void AddProjectile(Projectile p) => _pendingProjectiles.Add(p);

    void FlushProjectiles()
    {
        if (_pendingProjectiles.Count == 0) return;
        Projectiles.AddRange(_pendingProjectiles);
        _pendingProjectiles.Clear();
    }

    /// <summary>Hash of the gameplay state, for determinism tests.</summary>
    public ulong StateHash()
    {
        ulong h = 0xcbf29ce484222325UL;
        void Mix(float f)
        {
            h ^= (uint)BitConverter.SingleToInt32Bits(f);
            h *= 0x100000001b3UL;
        }
        void MixV(Vector3 v)
        {
            Mix(v.X);
            Mix(v.Y);
            Mix(v.Z);
        }

        MixV(Player.Position);
        MixV(Player.Velocity);
        Mix(Player.Hp);
        Mix(Player.Foam);
        foreach (var e in Enemies)
        {
            MixV(e.Position);
            Mix(e.Hp);
            Mix((float)e.State);
        }
        foreach (var p in Projectiles) MixV(p.Position);
        foreach (var f in Clownfish) MixV(f.Position);
        foreach (var c in Critters) MixV(c.Position);
        foreach (var c in _craters) MixV(c.Center);
        return h;
    }
}
