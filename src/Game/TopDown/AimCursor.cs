using Godot;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The in-game pointer: a small orange circle where she aims. With the mouse it sits under the pointer (the system
/// cursor is hidden while she plays). With a controller it circles her at a fixed distance, half the screen's shorter
/// side, in the right stick's direction. Hidden under menus, splashes and dives.
/// </summary>
public partial class AimCursor : Control
{
    static readonly Color Orange = new(1f, 0.55f, 0.12f);
    const float Radius = 7f;

    /// <summary>Where it is drawn, in viewport pixels.</summary>
    public Vector2 At;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
    }

    /// <summary>Places it: under the mouse, or (controller) around her screen point along her aim.</summary>
    public void Place(Vector2 at)
    {
        At = at;
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawCircle(At, Radius + 1.6f, new Color(0f, 0f, 0f, 0.55f));
        DrawArc(At, Radius - 1.2f, 0f, Mathf.Tau, 32, Orange, 2.6f, true);
        DrawCircle(At, 1.6f, Orange);
    }
}
