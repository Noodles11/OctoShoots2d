using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The curse den's dark on screen (DESIGN-TOPDOWN §4.2), under the HUD: black fog that closes in over the tunnel's
/// first metres, leaving a little faint water round her, and lifts slowly once she is out (or sent on by its teleport).
/// <see cref="Amount"/> also fades her light and the minimap.
/// </summary>
public partial class CurseDarkness : Control
{
    /// <summary>The dark closes in over this many metres past the mouth.</summary>
    const float Deepens = 6f;
    /// <summary>
    /// Round her (metres): the water stays readable within Inner, black from Outer — wide enough that both of the
    /// tunnel's walls (1.4 m each side of its line) show — and Veil of the dark still lies over her.
    /// </summary>
    const float Inner = 1.35f, Outer = 3.9f, Veil = 0.25f;
    /// <summary>It closes in fast (per second) and lifts slowly.</summary>
    const float CloseRate = 1.6f, LiftRate = 0.65f;

    ShaderMaterial _material = null!;

    /// <summary>0 none → 1 the full dark (eased).</summary>
    public float Amount { get; private set; }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _material = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/curse_dark.gdshader") };
        Material = _material;
    }

    public override void _Draw() => DrawRect(new Rect2(Vector2.Zero, Size), Colors.White);

    /// <summary>A new level: no dark.</summary>
    public void Clear() => Amount = 0f;

    /// <summary>Sent on by the curse: the screen goes fully dark at once (hiding the move), then lifts.</summary>
    public void Flash() => Amount = 1f;

    public void Sync(PlaneWorld world, Camera3D camera, Vector3 her, float dt)
    {
        float target = world.CurseDepth < 0f ? 0f : Mathf.SmoothStep(0f, Deepens, world.CurseDepth);
        Amount = Mathf.MoveToward(Amount, target, dt * (target > Amount ? CloseRate : LiftRate));
        Visible = Amount > 0.001f;
        if (!Visible) return;
        // Over the whole screen, whatever its size now.
        Position = Vector2.Zero;
        Size = GetViewportRect().Size;
        var at = camera.UnprojectPosition(her);
        // Her metres in pixels, along the screen's width (the camera looks almost straight down).
        float metre = (camera.UnprojectPosition(her + camera.GlobalBasis.X) - at).Length();
        _material.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("darkness"), Amount);
        _material.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("size"), Size);
        _material.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("centre"), at);
        _material.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("inner"), Inner * metre);
        _material.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("outer"), Outer * metre);
        _material.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("veil"), Veil);
        QueueRedraw();
    }
}
