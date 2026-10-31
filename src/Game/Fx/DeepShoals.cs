using System;
using System.Collections.Generic;
using Godot;

namespace OctoShoots.Game.Fx;

/// <summary>
/// Vague shadows of fish far under her plane (topdown_post's deep_fish): a few great loners, some middling ones and small
/// schools, all one soft fish shape, swimming about her on their own courses. The deeper, the softer. Cosmetic only: nothing
/// in the game knows of them. Each group wanders on until it is Far from her, then comes back in from somewhere else.
/// </summary>
public sealed class DeepShoals
{
    public const int Max = 16;
    /// <summary>A group leaves past Far metres from her, and comes back in from NearIn–FarIn out, off the screen.</summary>
    const float Far = 20f, NearIn = 15f, FarIn = 19f;

    /// <summary>Per fish: x, z (world), how far under her plane (metres), its length (metres).</summary>
    public Vector4[] Fish { get; } = new Vector4[Max];
    /// <summary>Per fish: its heading (x, y = world z; unit), its tail's beat (z, radians), how soft it shows (w: 1 and up).</summary>
    public Vector4[] Ways { get; } = new Vector4[Max];
    public int Count { get; private set; }

    sealed class Member
    {
        public Vector2 Offset;
        public float Deeper, Length, Beat, BeatRate;
    }

    sealed class Group
    {
        public Vector2 At;
        public float Heading, Turn, Speed, Depth;
        public readonly List<Member> Members = new();
    }

    readonly List<Group> _groups = new();
    readonly Random _rng = new(5150);
    bool _placed;

    float Range(float a, float b) => a + (b - a) * (float)_rng.NextDouble();

    public DeepShoals()
    {
        // Great loners, deep and slow.
        Add(1, 5.5f, 8f, 0f, 0.9f, 12f, 16f);
        Add(1, 5f, 7f, 0f, 0.9f, 11f, 15f);
        // Middling ones.
        Add(1, 2.5f, 3.6f, 0f, 1.5f, 7f, 11f);
        Add(1, 2.2f, 3.2f, 0f, 1.7f, 6f, 10f);
        // Small schools, quick and nearer.
        Add(5, 1f, 1.5f, 2.6f, 2.3f, 4f, 7f);
        Add(7, 0.8f, 1.3f, 3.2f, 2.6f, 4f, 8f);
    }

    /// <summary>A group of this many, each this long, spread about this far, this quick, this deep.</summary>
    void Add(int count, float lengthMin, float lengthMax, float spread, float speed, float depthMin, float depthMax)
    {
        var group = new Group { Speed = speed * Range(0.85f, 1.15f), Depth = Range(depthMin, depthMax) };
        for (int i = 0; i < count; i++)
        {
            float length = Range(lengthMin, lengthMax);
            group.Members.Add(new Member
            {
                Offset = new Vector2(Range(-1f, 1f), Range(-1f, 1f)) * spread,
                Deeper = count > 1 ? Range(-0.8f, 0.8f) : 0f,
                Length = length,
                Beat = Range(0f, Mathf.Tau),
                BeatRate = 2.5f + 5f / length,
            });
        }
        _groups.Add(group);
    }

    /// <summary>A group comes in from out of sight, swimming toward her more or less. Placed at the start, it may already be near.</summary>
    void Enter(Group group, Vector2 around, float from, float to)
    {
        float a = Range(0f, Mathf.Tau);
        group.At = around + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Range(from, to);
        group.Heading = a + Mathf.Pi + Range(-0.7f, 0.7f);
        group.Turn = 0f;
    }

    /// <summary>A step: they swim on, round where she is now.</summary>
    public void Step(Vector2 around, float dt)
    {
        if (!_placed)
        {
            _placed = true;
            foreach (var group in _groups) Enter(group, around, 4f, FarIn);
        }
        int n = 0;
        foreach (var group in _groups)
        {
            // Wandering: its turning drifts, never for long one way.
            group.Turn = Mathf.Clamp(group.Turn + Range(-0.5f, 0.5f) * dt, -0.18f, 0.18f);
            group.Heading += group.Turn * dt;
            var way = new Vector2(Mathf.Cos(group.Heading), Mathf.Sin(group.Heading));
            group.At += way * group.Speed * dt;
            if (group.At.DistanceTo(around) > Far) Enter(group, around, NearIn, FarIn);
            foreach (var m in group.Members)
            {
                if (n >= Max) break;
                m.Beat = Mathf.Wrap(m.Beat + m.BeatRate * dt, 0f, Mathf.Tau);
                var at = group.At + m.Offset;
                float depth = group.Depth + m.Deeper;
                Fish[n] = new Vector4(at.X, at.Y, depth, m.Length);
                // The deeper the softer, and the small ones softened as much in metres as the great ones.
                Ways[n] = new Vector4(way.X, way.Y, m.Beat, 1f + depth / 28f + 0.8f / m.Length);
                n++;
            }
        }
        Count = n;
    }
}
