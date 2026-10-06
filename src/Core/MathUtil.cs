using System;
using System.Numerics;

namespace OctoShoots.Core;

/// <summary>Small vector helpers. Coordinates match Godot: Y up, yaw 0 looks along -Z.</summary>
public static class MathUtil
{
    public const float Deg2Rad = MathF.PI / 180f;
    public const float Rad2Deg = 180f / MathF.PI;

    public static float Clamp01(float t) => t < 0f ? 0f : (t > 1f ? 1f : t);

    public static float Smoothstep(float t)
    {
        t = Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    public static float Lerp(float a, float b, float t) => a + (b - a) * t;

    public static Vector3 Forward(float yaw, float pitch)
    {
        float cp = MathF.Cos(pitch);
        return new Vector3(-MathF.Sin(yaw) * cp, MathF.Sin(pitch), -MathF.Cos(yaw) * cp);
    }

    public static Vector3 Right(float yaw) => new(MathF.Cos(yaw), 0f, -MathF.Sin(yaw));

    public static Vector3 MoveToward(Vector3 from, Vector3 to, float maxDelta)
    {
        Vector3 d = to - from;
        float len = d.Length();
        if (len <= maxDelta || len < 1e-6f) return to;
        return from + d / len * maxDelta;
    }

    public static Vector3 SafeNormalize(Vector3 v, Vector3 fallback)
    {
        float len = v.Length();
        return len > 1e-6f ? v / len : fallback;
    }

    /// <summary>Angle in radians between two non-zero vectors.</summary>
    public static float AngleBetween(Vector3 a, Vector3 b)
    {
        float d = Vector3.Dot(Vector3.Normalize(a), Vector3.Normalize(b));
        return MathF.Acos(Math.Clamp(d, -1f, 1f));
    }

    /// <summary>Rotates unit vector <paramref name="from"/> toward unit vector <paramref name="to"/> by at most maxRadians.</summary>
    public static Vector3 RotateToward(Vector3 from, Vector3 to, float maxRadians)
    {
        float angle = AngleBetween(from, to);
        if (angle <= maxRadians) return to;
        Vector3 axis = Vector3.Cross(from, to);
        if (axis.LengthSquared() < 1e-10f) return from;
        var q = Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), maxRadians);
        return Vector3.Normalize(Vector3.Transform(from, q));
    }
}
