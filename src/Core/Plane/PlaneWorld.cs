using System;
using System.Collections.Generic;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Core.Plane;

/// <summary>One tick of player intent on the plane. Move is in map axes (x east, y south), length ≤ 1; Dash is a press edge.</summary>
public struct PlaneInput
{
    public Vector2 Move;
    public bool Dash;
    /// <summary>Unit aim direction on the plane (mouse or arrow keys).</summary>
    public Vector2 Aim;
    /// <summary>Held: shoot along Aim.</summary>
    public bool Fire;
}

/// <summary>Clementine on the locked depth band: a 2D body (DESIGN-TOPDOWN §2.2).</summary>
public sealed class PlaneBody
{
    public Vector2 Position;
    public Vector2 PrevPosition;
    public Vector2 Velocity;
    public Vector2 LastMoveDir = -Vector2.UnitY;
    public Vector2 Aim = -Vector2.UnitY;
    public bool WasMoving;
    public float IdleTime;
    public float JetTimer;
    public float DashTimer;
    public float DashInvulnerableTimer;
    public float DashCooldownTimer;
    public float Hp = PlaneCombatTuning.PlayerMaxHp;
    public float ShotTimer;
    public float HurtTimer;
    /// <summary>Slowed (Queen Clam's royal pearl) while this counts down.</summary>
    public float SlowTimer;

    public bool IsDashing => DashTimer > 0f;
}

/// <summary>The slowing ink cloud left by a dash.</summary>
public sealed class PlaneInk
{
    public Vector2 Position;
    public float Radius;
    public float Life;
    public float MaxLife;
}

public enum PlaneEventType { JetStarted, DashStarted, HitWall, Shot, MobHit, MobDefeated, PlayerHit, PlayerDefeated, GatewayEntered, PearlCollected, ShellCollected, Purchased, CannotAfford, AmbushSprung, ShotPopped,
    ArenaSealed, BossLanded, BossVolley, BossClosed, BossHit, BossStagger, BossSnapWarning, BossSnap, BossDefeated, BossFreed }

/// <summary>Size: a popped bubble's radius (0 for other events).</summary>
public readonly record struct PlaneEvent(PlaneEventType Type, Vector2 Position, Vector2 Direction, float Size = 0f);

/// <summary>
/// The plane-locked simulation (DESIGN-TOPDOWN §0, §11): fixed 60 Hz, deterministic, engine-free. Positions are 2D map
/// coordinates on the fixed swim band; there is no vertical movement, buoyancy, sinking or surface. Collision is a
/// circle against the level's heightfield: terrain above the swim band blocks, and the body slides along the slope.
/// Movement, placeholder combat (shots, one shooting mob) and the gateway (PlaneCombat.cs); the real creatures and items
/// are ported onto it next (DESIGN-TOPDOWN §11.1).
/// </summary>
public sealed partial class PlaneWorld
{
    public const int TickRate = 60;
    public const float Dt = 1f / TickRate;

    /// <param name="run">What she carries in from the last room (HP, pearls); a fresh run when null.</param>
    public PlaneWorld(LevelMap map, Tuning tuning, PlaneRun? run = null)
    {
        Map = map;
        Tuning = tuning;
        Run = run ?? new PlaneRun(null, tuning);
        Player.Position = Player.PrevPosition = map.Start.Position;
        Player.Hp = Run.Hp;
        _rocks = new List<WeakRock>(map.WeakRocks);
        PlaceMobs();
        PlacePearls();
        PlaceShop();
        PlaceShells();
        PlaceAmbushes();
        PlaceBoss();
    }

    public LevelMap Map { get; }
    public Tuning Tuning { get; set; }
    public long Tick { get; private set; }
    public PlaneBody Player { get; } = new();
    public List<PlaneInk> Clouds { get; } = new();
    public List<PlaneEvent> Events { get; } = new();

    readonly List<WeakRock> _rocks;

    /// <summary>Weak rock still standing (an ink bomb breaks it, once bombs are ported).</summary>
    public IReadOnlyList<WeakRock> WeakRocks => _rocks;

    public float Radius => Tuning.PlayerRadius;
    public float CruiseSpeed => Tuning.PlaneCruiseSpeed;

    public void Step(in PlaneInput input)
    {
        Events.Clear();
        if (Defeated) return;
        Tick++;
        var p = Player;
        var t = Tuning;
        p.PrevPosition = p.Position;
        if (input.Aim.LengthSquared() > 1e-6f) p.Aim = Vector2.Normalize(input.Aim);

        Vector2 wish = input.Move;
        float wishLen = wish.Length();
        if (wishLen > 1f)
        {
            wish /= wishLen;
            wishLen = 1f;
        }
        bool hasInput = wishLen > 0.01f;
        Vector2 wishDir = hasInput ? wish / wishLen : Vector2.Zero;
        float cruise = CruiseSpeed * (p.SlowTimer > 0f ? PlaneBossTuning.SlowFactor : 1f);
        p.SlowTimer -= Dt;

        p.DashCooldownTimer -= Dt;
        p.DashInvulnerableTimer -= Dt;

        // Ink dash (DESIGN-2D §30): a burst that coasts, untouchable for a moment, leaving a slowing cloud.
        if (input.Dash && p.DashCooldownTimer <= 0f)
        {
            Vector2 dir = hasInput ? wishDir : p.Aim;
            p.DashTimer = t.DashCoast;
            p.DashInvulnerableTimer = t.DashInvulnerable;
            p.DashCooldownTimer = t.DashCooldown;
            p.Velocity = dir * cruise * t.DashMultiplier;
            p.JetTimer = 0f;
            Clouds.Add(new PlaneInk { Position = p.Position, Radius = t.InkCloudRadius, Life = t.InkCloudLife, MaxLife = t.InkCloudLife });
            Events.Add(new PlaneEvent(PlaneEventType.DashStarted, p.Position, dir));
        }

        if (p.DashTimer > 0f)
        {
            p.DashTimer -= Dt;
            if (hasInput)
            {
                p.WasMoving = true;
                p.IdleTime = 0f;
                p.LastMoveDir = wishDir;
            }
        }
        else if (hasInput)
        {
            // Jet start (DESIGN-2D §20): the first stroke from rest, and sharp turns, burst ahead.
            bool fromRest = !p.WasMoving && p.Velocity.Length() < t.JetRestFraction * cruise;
            bool sharpTurn = p.WasMoving && Angle(wishDir, p.LastMoveDir) > t.JetTurnAngle * MathUtil.Deg2Rad;
            if ((fromRest || sharpTurn) && t.JetDuration > 0f)
            {
                p.JetTimer = t.JetDuration;
                Events.Add(new PlaneEvent(PlaneEventType.JetStarted, p.Position, wishDir));
            }
            float boost = 1f;
            if (p.JetTimer > 0f)
            {
                float k = MathUtil.Clamp01(p.JetTimer / t.JetDuration);
                boost = 1f + (t.JetMultiplier - 1f) * k * k * (3f - 2f * k);
            }
            float maxSpeed = cruise * boost;
            float rate = cruise / t.AccelTime * boost;
            if (p.Velocity.Length() > maxSpeed + 0.01f) rate = MathF.Max(rate, t.OverspeedDecel);
            p.Velocity = MoveToward(p.Velocity, wish * maxSpeed, rate * Dt);
            p.IdleTime = 0f;
            p.WasMoving = true;
            p.LastMoveDir = wishDir;
        }
        else
        {
            // Idle drift: she glides to a stop (the visual bob is presentation only; there is no sinking).
            p.IdleTime += Dt;
            p.WasMoving = false;
            p.JetTimer = 0f;
            float rate = cruise / t.StopTime;
            if (p.Velocity.Length() > cruise + 0.01f) rate = MathF.Max(rate, t.OverspeedDecel);
            p.Velocity = MoveToward(p.Velocity, Vector2.Zero, rate * Dt);
        }
        p.JetTimer -= Dt;

        Move(ref p.Position, ref p.Velocity, Radius, barrier: ArenaBarrier.Inside);

        foreach (var c in Clouds) c.Life -= Dt;
        Clouds.RemoveAll(c => c.Life <= 0f);

        StepBoss();
        StepCombat(input);
        StepEconomy();
    }

    /// <summary>True when a circle of the given radius at p sits wholly in open water.</summary>
    public bool Clear(Vector2 p, float radius)
    {
        if (!Map.IsOpen(p)) return false;
        for (int i = 0; i < 8; i++)
        {
            float a = i * MathF.Tau / 8f;
            if (!Map.IsOpen(p + new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius)) return false;
        }
        foreach (var rock in _rocks)
            if (Vector2.Distance(p, rock.Center) < rock.Radius + radius) return false;
        return true;
    }

    /// <summary>Clear water that the arena's wall (if sealed) also allows.</summary>
    bool Fits(Vector2 p, float radius, ArenaBarrier barrier) => Clear(p, radius) && BarrierAllows(p, radius, barrier);

    /// <summary>Uphill direction at p: the terrain gradient, or away from weak rock (or into the arena's wall).</summary>
    Vector2 WallNormal(Vector2 p, float radius, ArenaBarrier barrier = ArenaBarrier.None)
    {
        if (!BarrierAllows(p, radius, barrier)) return BarrierNormal(p, barrier);
        foreach (var rock in _rocks)
            if (Vector2.Distance(p, rock.Center) < rock.Radius + radius)
                return SafeNormalize(rock.Center - p);
        // The wall lies where the rim of the circle meets high ground (a trench's downhill slope beside it must not count).
        Vector2 blocked = Vector2.Zero;
        for (int i = 0; i < 16; i++)
        {
            float a = i * MathF.Tau / 16f;
            Vector2 d = new(MathF.Cos(a), MathF.Sin(a));
            if (!Map.IsOpen(p + d * radius)) blocked += d;
        }
        return blocked.LengthSquared() > 1e-6f ? SafeNormalize(blocked) : SafeNormalize(Map.Gradient(p));
    }

    /// <summary>Moves a circle along its velocity, blocking against high terrain and sliding along the slope.</summary>
    public void Move(ref Vector2 position, ref Vector2 velocity, float radius, bool report = true, ArenaBarrier barrier = ArenaBarrier.None)
    {
        // Pushed out of a wall (a crater or a spawn) first.
        for (int i = 0; i < 20 && !Fits(position, radius, barrier); i++) position -= WallNormal(position, radius, barrier) * 0.05f;

        Vector2 delta = velocity * Dt;
        int steps = Math.Max(1, (int)MathF.Ceiling(delta.Length() / (radius * 0.5f)));
        Vector2 step = delta / steps;
        bool hit = false;
        for (int s = 0; s < steps; s++)
        {
            Vector2 next = position + step;
            if (Fits(next, radius, barrier))
            {
                position = next;
                continue;
            }
            hit = true;
            Vector2 n = WallNormal(next, radius, barrier);
            float into = Vector2.Dot(step, n);
            if (into > 0f) step -= n * into;
            float vInto = Vector2.Dot(velocity, n);
            if (vInto > 0f) velocity -= n * vInto;
            next = position + step;
            if (Fits(next, radius, barrier)) position = next;
            else
            {
                velocity = Vector2.Zero;
                break;
            }
        }
        if (hit && report) Events.Add(new PlaneEvent(PlaneEventType.HitWall, position, velocity));
    }

    static Vector2 MoveToward(Vector2 from, Vector2 to, float maxDelta)
    {
        Vector2 d = to - from;
        float len = d.Length();
        return len <= maxDelta || len < 1e-6f ? to : from + d / len * maxDelta;
    }

    static float Angle(Vector2 a, Vector2 b) => MathF.Acos(Math.Clamp(Vector2.Dot(SafeNormalize(a), SafeNormalize(b)), -1f, 1f));

    static Vector2 SafeNormalize(Vector2 v)
    {
        float l = v.Length();
        return l > 1e-6f ? v / l : Vector2.Zero;
    }

    /// <summary>Hash of the body state, for determinism tests.</summary>
    public ulong StateHash()
    {
        ulong h = 0xcbf29ce484222325UL;
        void Mix(float f)
        {
            h ^= (uint)BitConverter.SingleToInt32Bits(f);
            h *= 0x100000001b3UL;
        }
        Mix(Player.Position.X);
        Mix(Player.Position.Y);
        Mix(Player.Velocity.X);
        Mix(Player.Velocity.Y);
        Mix(Player.Hp);
        foreach (var m in Mobs)
        {
            Mix(m.Position.X);
            Mix(m.Position.Y);
            Mix(m.Hp);
        }
        foreach (var s in Shots)
        {
            Mix(s.Position.X);
            Mix(s.Position.Y);
        }
        return h;
    }
}
