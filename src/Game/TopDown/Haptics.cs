using System.Collections.Generic;
using Godot;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Controller vibration: short pulses (a hit landed or taken, a dash) and a steady hum (a water
/// current carrying her) are mixed into one motor level each frame, the strongest of each winning, and sent to the
/// controller she plays with. Silent with the mouse and keyboard, when turned off, or while paused.
/// </summary>
public sealed class Haptics
{
    readonly List<(float Weak, float Strong, float Life, float Age)> _pulses = new();
    float _holdWeak, _holdStrong, _sentWeak, _sentStrong, _resend;
    bool _on;

    /// <summary>The controller to buzz (the last one used).</summary>
    public int Device;

    /// <summary>A pulse: weak (high-frequency) and strong (low) motors 0..1, fading out over seconds.</summary>
    public void Pulse(float weak, float strong, float seconds) => _pulses.Add((weak, strong, seconds, 0f));

    /// <summary>A steady level held for this frame only (call every frame it applies).</summary>
    public void Hold(float weak, float strong)
    {
        _holdWeak = Mathf.Max(_holdWeak, weak);
        _holdStrong = Mathf.Max(_holdStrong, strong);
    }

    /// <summary>Mixes and sends this frame's level; enabled is false with keyboard and mouse, off, or paused.</summary>
    public void Tick(float dt, bool enabled)
    {
        float weak = _holdWeak, strong = _holdStrong;
        _holdWeak = _holdStrong = 0f;
        for (int i = _pulses.Count - 1; i >= 0; i--)
        {
            var (w, s, life, age) = _pulses[i];
            age += dt;
            if (age >= life)
            {
                _pulses.RemoveAt(i);
                continue;
            }
            // A sharp onset, then fading.
            float k = 1f - age / life;
            weak = Mathf.Max(weak, w * k);
            strong = Mathf.Max(strong, s * k * k);
            _pulses[i] = (w, s, life, age);
        }
        if (!enabled)
        {
            _pulses.Clear();
            Stop();
            return;
        }
        weak = Mathf.Clamp(weak, 0f, 1f);
        strong = Mathf.Clamp(strong, 0f, 1f);
        _resend -= dt;
        if (weak < 0.01f && strong < 0.01f)
        {
            Stop();
            return;
        }
        // Resent when it changes, or before the last one runs out.
        if (Mathf.Abs(weak - _sentWeak) < 0.03f && Mathf.Abs(strong - _sentStrong) < 0.03f && _resend > 0f) return;
        Input.StartJoyVibration(Device, weak, strong, 0.15f);
        _sentWeak = weak;
        _sentStrong = strong;
        _resend = 0.1f;
        _on = true;
    }

    public void Stop()
    {
        if (!_on) return;
        Input.StopJoyVibration(Device);
        _on = false;
        _sentWeak = _sentStrong = 0f;
    }
}
