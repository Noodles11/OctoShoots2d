using System;
using System.Numerics;
using OctoShoots.Core.Items;

namespace OctoShoots.Core.Sim;

public sealed partial class World
{
    /// <summary>Base swim speed scaled by the speed stat.</summary>
    public float SwimSpeed => Tuning.SwimSpeed * Loadout.Stats.Speed;

    void StepPlayer(in PlayerInput input)
    {
        var t = Tuning;
        var p = Player;
        var stats = Loadout.Stats;
        float swim = SwimSpeed;

        float pitchLimit = t.PitchLimit * MathUtil.Deg2Rad;
        p.Yaw = input.Yaw;
        p.Pitch = Math.Clamp(input.Pitch, -pitchLimit, pitchLimit);

        Vector3 forward = MathUtil.Forward(p.Yaw, p.Pitch);
        Vector3 right = MathUtil.Right(p.Yaw);
        Vector3 wish = forward * input.Forward + right * input.Strafe + Vector3.UnitY * input.Vertical;
        float wishLen = wish.Length();
        if (wishLen > 1f)
        {
            wish /= wishLen;
            wishLen = 1f;
        }
        bool hasInput = wishLen > 0.01f;
        Vector3 wishDir = hasInput ? wish / wishLen : Vector3.Zero;

        p.DashCooldownTimer -= Dt;
        p.DashInvulnerableTimer -= Dt;
        p.HurtTimer -= Dt;
        p.ShieldTimer -= Dt;
        p.GlowBurstTimer -= Dt;
        p.FrenzyTimer -= Dt;
        p.HiddenTimer -= Dt;
        if (p.Hp > 0f && stats[Stat.Regen] > 0f) p.Hp = MathF.Min(stats.MaxHp, p.Hp + stats[Stat.Regen] * Dt);

        if (input.Dash && p.DashCooldownTimer <= 0f)
        {
            Vector3 dir = hasInput ? wishDir : forward;
            p.DashTimer = t.DashCoast;
            p.DashInvulnerableTimer = t.DashInvulnerable;
            p.DashCooldownTimer = t.DashCooldown * stats[Stat.DashCooldown];
            p.Velocity = dir * swim * t.DashMultiplier;
            p.JetTimer = 0f;
            Clouds.Add(new InkCloud { Id = NextId(), Position = p.Position, Radius = t.InkCloudRadius, Life = t.InkCloudLife, MaxLife = t.InkCloudLife });
            Events.Add(new SimEvent(SimEventType.DashStarted, p.Position, dir));
        }

        if (p.DashTimer > 0f)
        {
            // Coasting: the dash keeps its velocity; collisions still slide it.
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
            bool fromRest = !p.WasMoving && p.Velocity.Length() < t.JetRestFraction * swim;
            bool sharpTurn = p.WasMoving && MathUtil.AngleBetween(wishDir, p.LastMoveDir) > t.JetTurnAngle * MathUtil.Deg2Rad;
            if ((fromRest || sharpTurn) && t.JetDuration > 0f)
            {
                p.JetTimer = t.JetDuration;
                Events.Add(new SimEvent(SimEventType.JetStarted, p.Position, wishDir));
            }

            float boost = 1f;
            if (p.JetTimer > 0f)
            {
                float k = MathUtil.Clamp01(p.JetTimer / t.JetDuration);
                boost = 1f + (t.JetMultiplier - 1f) * k * k * (3f - 2f * k);
            }

            float maxSpeed = swim * boost;
            Vector3 target = wish * maxSpeed;
            float rate = swim / t.AccelTime * boost;
            if (p.Velocity.Length() > maxSpeed + 0.01f) rate = MathF.Max(rate, t.OverspeedDecel);
            p.Velocity = MathUtil.MoveToward(p.Velocity, target, rate * Dt);

            p.IdleTime = 0f;
            p.WasMoving = true;
            p.LastMoveDir = wishDir;
        }
        else
        {
            p.IdleTime += Dt;
            p.WasMoving = false;
            p.JetTimer = 0f; // letting go ends the burst at once
            float ease = t.SinkEaseIn > 0f ? MathUtil.Smoothstep(p.IdleTime / t.SinkEaseIn) : 1f;
            var target = new Vector3(0f, -t.SinkSpeed * stats[Stat.Sink] * ease, 0f);
            float rate = swim / t.StopTime;
            if (p.Velocity.Length() > swim + 0.01f) rate = MathF.Max(rate, t.OverspeedDecel);
            p.Velocity = MathUtil.MoveToward(p.Velocity, target, rate * Dt);
        }
        p.JetTimer -= Dt;

        bool wasOnFloor = p.OnFloor;
        p.OnFloor = MoveSphere(ref p.Position, ref p.Velocity, t.PlayerRadius);
        ClampToSurface(ref p.Position, ref p.Velocity, t.PlayerRadius);
        if (p.OnFloor && !wasOnFloor)
            Events.Add(new SimEvent(SimEventType.Landed, p.Position - Vector3.UnitY * t.PlayerRadius, Vector3.UnitY));
    }

    /// <summary>Where bubbles leave from: the tip of the throwing tentacle, ahead and to the right of the eye.</summary>
    public Vector3 ThrowOrigin()
    {
        var p = Player;
        Vector3 origin = p.Position + p.Forward * 0.55f + MathUtil.Right(p.Yaw) * 0.12f - CameraUp() * 0.12f;
        return Sdf.Sample(origin) < 0.05f ? p.Position : origin;
    }

    public int BubbleCapacity => (int)Loadout.Stats[Stat.BubbleCapacity];

    /// <summary>The view's up vector (perpendicular to forward, no roll).</summary>
    Vector3 CameraUp() => Vector3.Cross(MathUtil.Right(Player.Yaw), Player.Forward);

    bool DamagePlayer(float amount, Vector3 source)
    {
        var p = Player;
        if (p.DashInvulnerableTimer > 0f || p.HurtTimer > 0f) return false;
        Vector3 fromDir = MathUtil.SafeNormalize(source - p.Position, Vector3.UnitY);
        if (p.ShieldTimer > 0f)
        {
            Events.Add(new SimEvent(SimEventType.ShieldBlocked, source, fromDir));
            return false;
        }

        if (!Tuning.Invincible)
        {
            // Foam soaks hits first (2D §23).
            float soaked = MathF.Min(p.Foam, amount);
            p.Foam -= soaked;
            p.Hp -= amount - soaked;
        }
        p.HurtTimer = Tuning.HurtInvulnerable;
        Events.Add(new SimEvent(SimEventType.PlayerHurt, source, fromDir, -1, amount));
        Dispatch(Trigger.OnDamaged, p.Position);

        if (p.Hp <= 0f)
        {
            Events.Add(new SimEvent(SimEventType.PlayerDied, p.Position, fromDir));
            _restartPending = true;
        }
        return true;
    }
}
