using System.Collections.Generic;
using System.Numerics;

namespace OctoShoots.Core.Sim;

/// <summary>One tick of player intent. Look angles are absolute; Dash and UseActive are press edges.</summary>
public struct PlayerInput
{
    public float Forward;
    public float Strafe;
    public float Vertical;
    public float Yaw;
    public float Pitch;
    public bool Fire;
    public bool Dash;
    public bool UseActive;
    public bool DropBomb;

    /// <summary>Half-angle of the cone the player can see; used for the off-screen attacker limit.</summary>
    public float ViewHalfAngleDeg;
}

public sealed class Player
{
    public Vector3 Position;
    public Vector3 PrevPosition;
    public Vector3 Velocity;
    public float Yaw;
    public float Pitch;
    public float Hp;
    public float Foam;
    public float IdleTime;
    public bool WasMoving;
    public Vector3 LastMoveDir = -Vector3.UnitZ;
    public float JetTimer;
    public float DashTimer;
    public float DashInvulnerableTimer;
    public float DashCooldownTimer;
    public float HurtTimer;
    public float FireCooldown;
    public bool OnFloor;
    public int Deaths;

    /// <summary>Bubbles waiting on the throwing tentacle's suckers.</summary>
    public int Bubbles;
    /// <summary>Time left before bubbles start regrowing after a throw.</summary>
    public float RegrowDelay;
    /// <summary>Progress of the next bubble growing, 0–1.</summary>
    public float RegrowProgress;

    /// <summary>Pearl or laser charge, 0–1, while LMB is held with a charge item.</summary>
    public float Charge;
    public float BeamTimer;
    public float BeamTickTimer;
    public float BeamSweep;

    public float ShieldTimer;
    public float GlowBurstTimer;
    public int GlowBurstShots;
    public float FrenzyTimer;
    public float FrenzyMult = 1f;
    /// <summary>Ink Cloud active: creatures lose track of her.</summary>
    public float HiddenTimer;
    /// <summary>Counts down from 2 s after she shoots, dashes or detonates a bomb: creatures notice her from twice as far.</summary>
    public float LoudTimer;

    public bool IsDashing => DashTimer > 0f;
    public Vector3 Forward => MathUtil.Forward(Yaw, Pitch);
}

public enum EnemyKind { Lanternfish, ClownNinja, SpanishDancer, Pufferling, Barracuda, MoonJelly, SeaUrchin, Crabby, Moray }

/// <summary>Nested = a clownfish ninja resting inside its anemone: out of sight and out of reach.</summary>
public enum EnemyState { Idle, Hunt, Telegraph, Recover, Dead, Nested,
    /// <summary>Startled: it has just noticed Clementine (0.6 s), then it acts.</summary>
    Alert,
    /// <summary>Mid-attack: a charge, lunge, leap, pulse or swollen chase.</summary>
    Attack }

/// <summary>Where a fish swims in its school around an anemone.</summary>
public sealed class SchoolSlot
{
    public int Home;
    public float Angle;
    public float Radius;
    public float Height;
    public float Speed;
    public float Phase;
}

/// <summary>A normal clownfish: neutral, can't be harmed, never attacks. It just makes the ninja harder to spot.</summary>
public sealed class Clownfish
{
    public int Id;
    /// <summary>A ninja that was freed this run (it lost its headband and joined the school).</summary>
    public bool Freed;
    public Vector3 Position;
    public Vector3 PrevPosition;
    public Vector3 Velocity;
    public Vector3 Facing = -Vector3.UnitZ;
    public required SchoolSlot Slot;
}

/// <summary>
/// Lanternfish: a swimmer that keeps its distance and fires telegraphed light orbs.
/// Clownfish ninja: lives in an anemone, hides in its school, throws tiny starfish.
/// </summary>
public sealed class Enemy
{
    public int Id;
    public EnemyKind Kind;
    /// <summary>Ninja only: the anemone it lives in, and its place in that anemone's school.</summary>
    public int HomeAnemone = -1;
    public SchoolSlot? Slot;
    public Vector3 Position;
    public Vector3 PrevPosition;
    public Vector3 Velocity;
    public Vector3 Facing = -Vector3.UnitZ;
    public float Hp;
    public EnemyState State;
    public float StateTimer;
    public float AttackCooldown;
    public float HurtTimer;
    public float RespawnTimer;
    public float OrbitSign = 1f;
    public bool OffscreenAttack;

    // Den creatures (DEPTH1-BESTIARY).
    /// <summary>The den this creature belongs to; groups take turns attacking.</summary>
    public int Group = -1;
    /// <summary>Where it sleeps: the surface point of an urchin or moray, or the middle of its patrol.</summary>
    public Vector3 Anchor;
    /// <summary>Surface normal at the anchor, for clingers and holes.</summary>
    public Vector3 Up = Vector3.UnitY;
    /// <summary>Locked attack direction (charge, lunge, leap).</summary>
    public Vector3 Aim;
    /// <summary>General purpose timer: circling time, swollen time, lunge extension.</summary>
    public float Aux;
    /// <summary>A small bed urchin: never shoots, weaker thorn.</summary>
    public bool Small;
    /// <summary>Per-creature random phase for bobbing.</summary>
    public float Phase;
    /// <summary>On the ground (crabby).</summary>
    public bool Grounded;

    // Status effects (2D §9.3 bubble modifiers).
    public float FrozenTimer;
    public float BurnTimer;
    public float BurnDps;
    public float PoisonTimer;
    public float PoisonDps;
    public float CharmTimer;
    public float SlowTimer;
    public float StunTimer;

    public bool Alive => State is not (EnemyState.Dead or EnemyState.Nested);
    public bool Disabled => FrozenTimer > 0f || StunTimer > 0f;
}

/// <summary>
/// Healthy sea life (DEPTH1-BESTIARY §8): the same species as the corrupted creatures, in natural colours.
/// Never a target, never attacks, harmless to touch; it wanders near home and keeps clear of Clementine.
/// Some are there from the start, the rest are corrupted creatures she has freed.
/// </summary>
public sealed class Critter
{
    public int Id;
    public EnemyKind Kind;
    public Vector3 Position;
    public Vector3 PrevPosition;
    public Vector3 Velocity;
    public Vector3 Facing = -Vector3.UnitZ;
    /// <summary>Where it lives: the middle of its wandering, or the surface point of an urchin or a moray's hole.</summary>
    public Vector3 Anchor;
    public Vector3 Up = Vector3.UnitY;
    /// <summary>Moray: which way its head points, and how far it is out of the hole.</summary>
    public Vector3 Aim = Vector3.UnitY;
    public float Aux;
    public float Phase;
    public bool Small;
    public bool Grounded;
    /// <summary>Seconds since it was freed, or -1 if it was always healthy.</summary>
    public float FreedAge = -1f;
}

public enum ProjectileKind { Bubble, Pearl, Pellet, Shard, Orb, Star, Spore, Spine }

public sealed class Projectile
{
    public int Id;
    public ProjectileKind Kind;
    public bool FromPlayer;
    public Vector3 Position;
    public Vector3 PrevPosition;
    public Vector3 Velocity;
    public float Radius;
    public float Damage;
    public float Traveled;
    public float MaxRange;
    public float Age;
    public bool Alive = true;

    // Aim-assist bullet bend.
    public float BendDegPerSec;
    public float ConeDeg;

    // Shot modifiers, copied from the loadout at spawn so children can differ.
    public bool HasMods;
    public float BaseRadius;
    public float BaseDamage;
    public bool Homing;
    public float HomingDegPerSec;
    public bool Pierce;
    public bool Spectral;
    public int BouncesLeft;
    public bool CanSplit;
    public bool Boomerang;
    public bool Returning;
    public bool Grow;
    public bool Explosive;
    public bool Rear;
    public float Charge;
    public float PoisonChance;
    public float PoisonBoost = 1f;
    public HashSet<int>? HitEnemies;

    /// <summary>Wave (double helix) and spiral shots orbit a centre line that carries the motion.</summary>
    public HelixMode Helix;
    public Vector3 AxisPosition;
    public float HelixPhase;
}

public enum HelixMode { None, Wave, Spiral }

/// <summary>Left behind by the ink dash, Ink Cloud and Guided Inkfish; slows creatures inside.</summary>
public sealed class InkCloud
{
    public int Id;
    public Vector3 Position;
    public float Radius;
    public float Life;
    public float MaxLife;
}

/// <summary>A dropped ink bomb: sinks in a slow arc, rolls down slopes, explodes on its fuse (2D §4).</summary>
public sealed class InkBomb
{
    public int Id;
    public Vector3 Position;
    public Vector3 PrevPosition;
    public Vector3 Velocity;
    public float Fuse;
}

/// <summary>A loose pickup: sinks and rests on the floor (foam floats up), collected on touch.</summary>
public sealed class Pickup
{
    public int Id;
    public Loot.PickupKind Kind;
    public Vector3 Position;
    public Vector3 PrevPosition;
    public Vector3 Velocity;
    /// <summary>Index into the level's loot plan, or -1 for dropped and spilled pickups.</summary>
    public int PlanIndex = -1;
}

/// <summary>An item pearl in a shell. The shell opens when Clementine comes close; she takes the pearl by touching it.</summary>
public sealed class PearlShell
{
    public int Id;
    public int PlanIndex;
    public Vector3 Position;
    public string? ItemId;
    public int Price;
    public bool Open;
    public Vector3 PearlPosition => Position + new Vector3(0f, 0.65f, 0f);
}

/// <summary>A wooden treasure chest that bursts open when hit.</summary>
public sealed class TreasureChest
{
    public int Id;
    public int PlanIndex;
    public Vector3 Position;
    public bool Opened;
}

/// <summary>A sphere blown out of the reef; kept per reef and saved (§6.3).</summary>
/// <summary>
/// A place where creatures of one kind live (DEPTH1-BESTIARY §4). Count creatures sleep here until Clementine
/// comes near. Up is the surface normal for clingers and the direction a moray's hole faces.
/// </summary>
public sealed record DenSpot(EnemyKind Kind, Vector3 Position, Vector3 Up, int Count, bool Bed = false);

public readonly record struct Crater(Vector3 Center, float Radius);

public enum SimEventType
{
    ShotFired,
    ShotHitEnemy,
    ShotHitTerrain,
    ShotExpired,
    ShotBounced,
    ShotReturned,
    EnemyShotFired,
    EnemyTelegraph,
    EnemyHurt,
    EnemyCrit,
    EnemyDied,
    EnemySpawned,
    StatusApplied,
    PlayerHurt,
    PlayerDied,
    ShieldBlocked,
    DashStarted,
    JetStarted,
    Landed,
    Explosion,
    ChainArc,
    BeamTick,
    KrakenSlam,
    ItemGained,
    SynergyActivated,
    TransformationActivated,
    ActiveUsed,
    ActiveNotReady,
    ActiveCharged,
    BombDropped,
    TerrainCarved,
    BubblesEmpty,
    NinjaEmerged,
    NinjaHid,
    ShellOpened,
    ShellClosed,
    PearlTaken,
    TooPoor,
    ChestOpened,
    PickupCollected,
    CoinsDugUp,
    /// <summary>A creature noticed Clementine; Value is how far away it was.</summary>
    CreatureNoticed,
    /// <summary>A creature lost track of her and is going home.</summary>
    CreatureLost,
    /// <summary>A corrupted creature was freed: EntityId is the healthy critter (or clownfish) it became; Tag its kind; Value 1 if it was frozen.</summary>
    CreatureFreed,
}

/// <summary>Something the presentation layer should react to (sound, FX, camera, captions).</summary>
public readonly record struct SimEvent(SimEventType Type, Vector3 Position, Vector3 Direction, int EntityId = -1, float Value = 0f, string? Tag = null);
