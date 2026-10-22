using Godot;

namespace OctoShoots.Game.Fx;

/// <summary>
/// Swells in the water round Clementine (topdown_post's swells): when she sets off, turns sharply or dashes, the water
/// she pushes against shifts for a moment — a faint ring of refraction spreading from behind her, a brief change in
/// the water's density rather than a ripple on a surface. Cosmetic only. Each lives Life seconds, spreading to about
/// Reach metres, strongest behind her (the way she pushed the water).
/// </summary>
public sealed class WaterSwells
{
    public const int Max = 6;
    const float Life = 0.7f;

    /// <summary>Per swell: x, z (world), age (0 → 1), strength (0..1).</summary>
    public Vector4[] Rings { get; } = new Vector4[Max];
    /// <summary>Per swell: the way she was going (x, z, unit), so the swell is strongest behind her.</summary>
    public Vector4[] Dirs { get; } = new Vector4[Max];
    public int Count { get; private set; }

    readonly float[] _age = new float[Max];

    /// <summary>A new swell at <paramref name="at"/> (world x, z), her heading <paramref name="dir"/>; the oldest gives way when full.</summary>
    public void Add(Vector2 at, Vector2 dir, float strength)
    {
        int i = Count < Max ? Count++ : Oldest();
        dir = dir.LengthSquared() > 1e-6f ? dir.Normalized() : Vector2.Zero;
        Rings[i] = new Vector4(at.X, at.Y, 0f, Mathf.Clamp(strength, 0f, 1f));
        Dirs[i] = new Vector4(dir.X, dir.Y, 0f, 0f);
        _age[i] = 0f;
    }

    int Oldest()
    {
        int o = 0;
        for (int i = 1; i < Count; i++)
            if (_age[i] > _age[o]) o = i;
        return o;
    }

    public void Tick(float dt)
    {
        for (int i = Count - 1; i >= 0; i--)
        {
            _age[i] += dt;
            if (_age[i] >= Life)
            {
                Count--;
                Rings[i] = Rings[Count];
                Dirs[i] = Dirs[Count];
                _age[i] = _age[Count];
                continue;
            }
            Rings[i].Z = _age[i] / Life;
        }
    }

    public void Clear() => Count = 0;

    // ───────────────────────── what sets them off ─────────────────────────

    Vector2 _heading;
    float _speed, _cooldown;
    bool _dashing;

    /// <summary>
    /// Watches her motion each frame (map axes: x east, y south = world z): setting off from near still, a sharp turn
    /// at speed (over TurnAngle from the way she was holding; gentle curves never count) and a dash each start a swell.
    /// </summary>
    public void Watch(Vector2 at, Vector2 velocity, bool dashing, float dt)
    {
        const float StillBelow = 0.8f, MovingAbove = 1.8f, TurnAngle = 1.0f, Cooldown = 0.28f;
        _cooldown -= dt;
        Tick(dt);
        float speed = velocity.Length();
        var dir = speed > 1e-3f ? velocity / speed : Vector2.Zero;
        if (dashing && !_dashing)
        {
            Add(at, dir, 1f);
            _cooldown = Cooldown;
            _heading = dir;
        }
        else if (_cooldown <= 0f && _speed < StillBelow && speed > MovingAbove)
        {
            Add(at, dir, 0.65f);
            _cooldown = Cooldown;
            _heading = dir;
        }
        else if (speed > MovingAbove && _heading != Vector2.Zero)
        {
            if (_cooldown <= 0f && Mathf.Abs(_heading.AngleTo(dir)) > TurnAngle)
            {
                Add(at, dir, 0.5f);
                _cooldown = Cooldown;
                _heading = dir;
            }
            // The way she holds follows her slowly, so a long curve never builds up to a turn.
            else _heading = _heading.Slerp(dir, Mathf.Min(1f, dt * 4f)).Normalized();
        }
        else if (speed > MovingAbove) _heading = dir;
        _speed = speed;
        _dashing = dashing;
    }
}
