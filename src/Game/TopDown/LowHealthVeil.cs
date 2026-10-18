using Godot;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Her low health on screen (DESIGN-TOPDOWN §2.5), under the HUD: a dark violet veil that blurs the reef radially and
/// closes in from the edges as her health drains — from a hint in the corners just under 85% to 70% of the screen at
/// a quarter (critical), where it breathes slowly with her last ring (steady with reduced motion).
/// </summary>
public partial class LowHealthVeil : Control
{
    /// <summary>The veil starts below this share of her health and is full at <see cref="Critical"/>.</summary>
    const float Starts = 0.85f, Critical = 0.25f;
    /// <summary>It closes in fast after a hit (per second) and lifts a little slower as she heals.</summary>
    const float CloseRate = 2f, LiftRate = 1.2f;
    /// <summary>The critical pulse's period (seconds).</summary>
    const float BreathSeconds = 1.8f;

    ShaderMaterial _material = null!;
    float _time;

    /// <summary>0 none → 1 critical (eased).</summary>
    public float Danger { get; private set; }
    public bool ReducedMotion;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _material = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/low_health.gdshader") };
        Material = _material;
        Visible = false;
    }

    public override void _Draw() => DrawRect(new Rect2(Vector2.Zero, Size), Colors.White);

    /// <summary>Her health as a share of her maximum (0…1).</summary>
    public void Sync(float health, float dt)
    {
        float target = Mathf.Clamp((Starts - health) / (Starts - Critical), 0f, 1f);
        Danger = Mathf.MoveToward(Danger, target, dt * (target > Danger ? CloseRate : LiftRate));
        Visible = Danger > 0.001f;
        if (!Visible) return;
        Position = Vector2.Zero;
        Size = GetViewportRect().Size;
        _time += dt;
        float breath = health <= Critical && !ReducedMotion ? Mathf.Sin(_time * Mathf.Tau / BreathSeconds) : 0f;
        _material.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("danger"), Danger);
        _material.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("breath"), breath);
        QueueRedraw();
    }
}
