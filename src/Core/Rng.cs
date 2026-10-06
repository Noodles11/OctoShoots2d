using System.Numerics;

namespace OctoShoots.Core;

/// <summary>xoshiro256** seeded through SplitMix64. Named streams keep systems independent.</summary>
public sealed class Rng
{
    ulong _s0, _s1, _s2, _s3;

    public ulong Seed { get; }

    public Rng(ulong seed)
    {
        Seed = seed;
        ulong x = seed;
        _s0 = SplitMix(ref x);
        _s1 = SplitMix(ref x);
        _s2 = SplitMix(ref x);
        _s3 = SplitMix(ref x);
    }

    /// <summary>An independent generator derived from this seed and a stream name.</summary>
    public Rng Stream(string name) => new(Seed ^ (Fnv1a(name) * 0x9E3779B97F4A7C15UL));

    public ulong NextU64()
    {
        ulong result = Rotl(_s1 * 5, 7) * 9;
        ulong t = _s1 << 17;
        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = Rotl(_s3, 45);
        return result;
    }

    /// <summary>Uniform in [0, 1).</summary>
    public float NextFloat() => (NextU64() >> 40) * (1f / 16777216f);

    public float Range(float min, float max) => min + (max - min) * NextFloat();

    public int Int(int maxExclusive) => (int)((NextU64() >> 33) % (ulong)maxExclusive);

    public Vector3 InsideUnitSphere()
    {
        while (true)
        {
            var v = new Vector3(Range(-1f, 1f), Range(-1f, 1f), Range(-1f, 1f));
            if (v.LengthSquared() <= 1f) return v;
        }
    }

    public static ulong Fnv1a(string s)
    {
        ulong h = 0xcbf29ce484222325UL;
        foreach (char c in s)
        {
            h ^= c;
            h *= 0x100000001b3UL;
        }
        return h;
    }

    static ulong SplitMix(ref ulong x)
    {
        x += 0x9E3779B97F4A7C15UL;
        ulong z = x;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    static ulong Rotl(ulong x, int k) => (x << k) | (x >> (64 - k));
}
