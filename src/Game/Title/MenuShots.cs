using System;
using System.Collections.Generic;
using Godot;

namespace OctoShoots.Game.Title;

/// <summary>
/// Her shots at the title's items (TitleMain): a bolt of her light from her to the item chosen — a hot white heart in
/// her glow with a short tail, on a slight arc, quick — that bursts on the item in a ring and a spray of sparks, and
/// what the item does happens then. One at a time. Drawn over the menu, in the page's units.
/// </summary>
public partial class MenuShots : Control
{
    /// <summary>A shot's flight (seconds), and how long its burst plays.</summary>
    const float FlightSeconds = 0.22f, BurstSeconds = 0.45f;

    /// <summary>Where she is on the page this frame (the shots set off from her).</summary>
    public Vector2 From;

    sealed class Shot
    {
        public Vector2 From, To;
        public float T, Bow;
        public Action? Hit;
    }

    sealed class Burst
    {
        public Vector2 At;
        public float Age;
        public readonly List<(Vector2 Dir, float Speed)> Sparks = new();
    }

    Shot? _shot;
    readonly List<Burst> _bursts = new();
    readonly Random _random = new(7);

    /// <summary>A shot is on its way (another waits for it).</summary>
    public bool Busy => _shot is not null;

    static Color Light => OctoShoots.Game.Fx.HerColor.Light;

    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

    /// <summary>She shoots at this point of the page; when it strikes, hit runs.</summary>
    public void Fire(Vector2 to, Action hit)
    {
        _shot = new Shot { From = From, To = to, Bow = (_random.NextDouble() < 0.5 ? -1f : 1f) * 0.12f, Hit = hit };
    }

    Vector2 At(Shot s, float t)
    {
        var d = s.To - s.From;
        var mid = (s.From + s.To) * 0.5f + new Vector2(-d.Y, d.X) * s.Bow;
        float u = 1f - t;
        return u * u * s.From + 2f * u * t * mid + t * t * s.To;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (_shot is { } s)
        {
            s.T += dt / FlightSeconds;
            if (s.T >= 1f)
            {
                var burst = new Burst { At = s.To };
                for (int i = 0; i < 14; i++)
                {
                    float a = (float)(_random.NextDouble() * Math.Tau);
                    burst.Sparks.Add((new Vector2(Mathf.Cos(a), Mathf.Sin(a)), 140f + (float)_random.NextDouble() * 220f));
                }
                _bursts.Add(burst);
                _shot = null;
                s.Hit?.Invoke();
            }
        }
        for (int i = _bursts.Count - 1; i >= 0; i--)
        {
            _bursts[i].Age += dt;
            if (_bursts[i].Age >= BurstSeconds) _bursts.RemoveAt(i);
        }
        if (_shot is not null || _bursts.Count > 0 || IsVisibleInTree()) QueueRedraw();
    }

    public override void _Draw()
    {
        if (_shot is { } s)
        {
            float t = s.T * s.T * (3f - 2f * s.T) * 0.5f + s.T * 0.5f;
            // The tail: her glow, thinning behind it.
            for (int j = 10; j >= 1; j--)
            {
                float back = Mathf.Max(t - j * 0.035f, 0f);
                float k = 1f - j / 11f;
                DrawCircle(At(s, back), 3f + 9f * k, Light with { A = 0.35f * k * k });
            }
            var head = At(s, t);
            DrawCircle(head, 18f, Light with { A = 0.35f });
            DrawCircle(head, 10f, Light.Lerp(Colors.White, 0.5f));
            DrawCircle(head, 5f, Colors.White);
        }
        foreach (var b in _bursts)
        {
            float k = b.Age / BurstSeconds, e = 1f - (1f - k) * (1f - k);
            // A ring of her light opening out, and sparks flung off, slowing.
            DrawArc(b.At, 14f + 70f * e, 0f, Mathf.Tau, 48, Light with { A = 0.85f * (1f - k) }, 4f * (1f - k) + 1f, true);
            DrawCircle(b.At, 20f * (1f - k), Colors.White with { A = 0.8f * (1f - k) });
            foreach (var (dir, speed) in b.Sparks)
            {
                var p = b.At + dir * speed * (0.45f * e);
                DrawLine(p, p + dir * 10f * (1f - k), Light.Lerp(Colors.White, 0.4f) with { A = 1f - k }, 2.5f, true);
            }
        }
    }
}
