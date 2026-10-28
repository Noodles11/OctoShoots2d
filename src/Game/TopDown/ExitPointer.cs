using Godot;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The way down, once it is open: a small soft chevron floating just inside the screen's edge, pointing from her toward
/// the shaft while it is out of sight, drifting gently along the edge as she swims and bobbing toward it; it fades as
/// the shaft comes into view. Independent of the maps (and of Sea Chart Nautilus): always there once the way is open.
/// Laid out in the HUD's units (TopDownMain's HUD root).
/// </summary>
public partial class ExitPointer : Control
{
    /// <summary>How far in from the screen's edge it floats, and how far inside the edge the shaft counts as in sight.</summary>
    const float Inset = 70f, InSight = 40f;
    /// <summary>The chevron's half-length along its point, and its half-width.</summary>
    const float Length = 12f, Width = 10f;

    /// <summary>Its colour: her light (TopDownMain keeps it to her colour).</summary>
    public Color Tint = new(1f, 0.92f, 0.72f);

    float _shown, _time, _angle;
    Vector2 _at;
    bool _placed;

    public ExitPointer() => MouseFilter = MouseFilterEnum.Ignore;

    /// <summary>
    /// This frame: where the shaft is shown and where she is (HUD units), or no target while the way is shut (or the
    /// game is between moments: a level-up, an intro, a dive).
    /// </summary>
    public void Step(float dt, Vector2? target, Vector2 from)
    {
        _time += dt;
        bool want = false;
        if (target is { } t)
        {
            var sight = new Rect2(Vector2.Zero, Size).Grow(-InSight);
            want = !sight.HasPoint(t);
            var to = t - from;
            if (to.LengthSquared() > 1f)
            {
                var edge = new Rect2(Vector2.Zero, Size).Grow(-Inset);
                var at = OnEdge(edge, from, to.Normalized());
                float angle = to.Angle();
                // Eased, so it glides along the edge rather than jumping as she turns.
                float k = _placed ? 1f - Mathf.Exp(-8f * dt) : 1f;
                _at = _at.Lerp(at, k);
                _angle = Mathf.LerpAngle(_angle, angle, k);
                _placed = true;
            }
        }
        _shown = Mathf.MoveToward(_shown, want ? 1f : 0f, dt / 0.45f);
        if (!want && _shown <= 0f) _placed = false;
        QueueRedraw();
    }

    /// <summary>Where a ray from a point inside the rectangle leaves it.</summary>
    static Vector2 OnEdge(Rect2 r, Vector2 from, Vector2 dir)
    {
        from = from.Clamp(r.Position, r.End);
        float t = float.MaxValue;
        if (dir.X > 1e-4f) t = Mathf.Min(t, (r.End.X - from.X) / dir.X);
        if (dir.X < -1e-4f) t = Mathf.Min(t, (r.Position.X - from.X) / dir.X);
        if (dir.Y > 1e-4f) t = Mathf.Min(t, (r.End.Y - from.Y) / dir.Y);
        if (dir.Y < -1e-4f) t = Mathf.Min(t, (r.Position.Y - from.Y) / dir.Y);
        return from + dir * (t == float.MaxValue ? 0f : t);
    }

    public override void _Draw()
    {
        if (_shown <= 0f || !_placed) return;
        float ease = _shown * _shown * (3f - 2f * _shown);
        // A slow breath, and a nudge toward the shaft.
        float breath = 0.82f + 0.18f * Mathf.Sin(_time * 2.2f);
        var dir = Vector2.FromAngle(_angle);
        var side = new Vector2(-dir.Y, dir.X);
        var c = _at + dir * (4f * Mathf.Sin(_time * 2.6f));
        float a = 0.7f * ease * breath;
        // A faint halo under it, so it reads on bright water and dark rock alike.
        for (int i = 3; i >= 1; i--)
            DrawCircle(c, 9f + i * 7f, new Color(0.03f, 0.05f, 0.08f, 0.07f * ease));
        var tip = c + dir * Length;
        var left = c - dir * Length * 0.8f + side * Width;
        var right = c - dir * Length * 0.8f - side * Width;
        var stroke = new[] { left, tip, right };
        DrawPolyline(stroke, new Color(0.03f, 0.05f, 0.08f, 0.45f * ease), 7f, antialiased: true);
        DrawPolyline(stroke, new Color(Tint, a), 3.5f, antialiased: true);
        // A softer second chevron trailing behind it, as if it were drawing her on.
        var back = -dir * 9f;
        DrawPolyline(new[] { left + back, tip + back, right + back }, new Color(Tint, a * 0.35f), 2.5f, antialiased: true);
    }
}
