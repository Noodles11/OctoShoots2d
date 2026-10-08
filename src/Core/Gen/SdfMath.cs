using System;
using System.Numerics;

namespace OctoShoots.Core.Gen;

/// <summary>Signed-distance primitives (negative inside the shape), smooth blends and value noise.</summary>
public static class SdfMath
{
    public static float Ellipsoid(Vector3 p, Vector3 c, Vector3 r)
    {
        Vector3 q = p - c;
        float k0 = (q / r).Length();
        float k1 = (q / (r * r)).Length();
        return k1 < 1e-6f ? -MathF.Min(r.X, MathF.Min(r.Y, r.Z)) : k0 * (k0 - 1f) / k1;
    }

    public static float Sphere(Vector3 p, Vector3 c, float r) => (p - c).Length() - r;

    public static float Capsule(Vector3 p, Vector3 a, Vector3 b, float r)
    {
        Vector3 pa = p - a, ba = b - a;
        float h = MathUtil.Clamp01(Vector3.Dot(pa, ba) / Vector3.Dot(ba, ba));
        return (pa - ba * h).Length() - r;
    }

    /// <summary>Capsule whose radius blends from ra at a to rb at b.</summary>
    public static float TaperedCapsule(Vector3 p, Vector3 a, Vector3 b, float ra, float rb)
    {
        Vector3 pa = p - a, ba = b - a;
        float h = MathUtil.Clamp01(Vector3.Dot(pa, ba) / Vector3.Dot(ba, ba));
        return (pa - ba * h).Length() - MathUtil.Lerp(ra, rb, h);
    }

    /// <summary>Torus around the Z axis (stands upright like an arch in the XY plane).</summary>
    public static float TorusZ(Vector3 p, Vector3 c, float major, float minor)
    {
        Vector3 q = p - c;
        float ring = MathF.Sqrt(q.X * q.X + q.Y * q.Y) - major;
        return MathF.Sqrt(ring * ring + q.Z * q.Z) - minor;
    }

    /// <summary>Torus around an arbitrary horizontal axis given by its yaw.</summary>
    public static float Arch(Vector3 p, Vector3 c, float yaw, float major, float minor)
    {
        Vector3 q = p - c;
        float cs = MathF.Cos(yaw), sn = MathF.Sin(yaw);
        var local = new Vector3(q.X * cs - q.Z * sn, q.Y, q.X * sn + q.Z * cs);
        return TorusZ(local, Vector3.Zero, major, minor);
    }

    /// <summary>A slab of the given thickness across axis n, limited to a ball of radius r.</summary>
    public static float Plug(Vector3 p, Vector3 c, Vector3 n, float thickness, float r) =>
        MathF.Max(MathF.Abs(Vector3.Dot(p - c, n)) - thickness * 0.5f, (p - c).Length() - r);

    public static float SMin(float a, float b, float k)
    {
        float h = MathUtil.Clamp01(0.5f + 0.5f * (b - a) / k);
        return MathUtil.Lerp(b, a, h) - k * h * (1f - h);
    }

    public static float SMax(float a, float b, float k) => -SMin(-a, -b, k);

    /// <summary>Value noise in [-1, 1].</summary>
    public static float Noise(Vector3 p, uint seed)
    {
        int x = (int)MathF.Floor(p.X), y = (int)MathF.Floor(p.Y), z = (int)MathF.Floor(p.Z);
        float fx = MathUtil.Smoothstep(p.X - x), fy = MathUtil.Smoothstep(p.Y - y), fz = MathUtil.Smoothstep(p.Z - z);
        float c000 = Hash(x, y, z, seed), c100 = Hash(x + 1, y, z, seed);
        float c010 = Hash(x, y + 1, z, seed), c110 = Hash(x + 1, y + 1, z, seed);
        float c001 = Hash(x, y, z + 1, seed), c101 = Hash(x + 1, y, z + 1, seed);
        float c011 = Hash(x, y + 1, z + 1, seed), c111 = Hash(x + 1, y + 1, z + 1, seed);
        float a = MathUtil.Lerp(MathUtil.Lerp(c000, c100, fx), MathUtil.Lerp(c010, c110, fx), fy);
        float b = MathUtil.Lerp(MathUtil.Lerp(c001, c101, fx), MathUtil.Lerp(c011, c111, fx), fy);
        return MathUtil.Lerp(a, b, fz);
    }

    static float Hash(int x, int y, int z, uint seed)
    {
        uint h = unchecked((uint)x * 0x8da6b343u ^ (uint)y * 0xd8163841u ^ (uint)z * 0xcb1ab31fu ^ seed * 0x9E3779B9u);
        h ^= h >> 13;
        h = unchecked(h * 0x5bd1e995u);
        h ^= h >> 15;
        return (h & 0xFFFFFF) / (float)0xFFFFFF * 2f - 1f;
    }
}
