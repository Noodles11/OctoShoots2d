using System;
using System.Text;

namespace OctoShoots.Core.Run;

public enum SpecialSeed { None, Debug, BigJelly, TinyJelly, DarkDeep, PartyFish }

/// <summary>
/// 8-character run seeds like <c>KELP 7Q2Z</c> (2D §10): A–Z and 2–9 without the ambiguous
/// 0/O and 1/I, so 32 symbols = 5 bits each = a 40-bit value.
/// </summary>
public readonly record struct SeedCode
{
    public const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    public const int Length = 8;

    SeedCode(ulong value, SpecialSeed special)
    {
        Value = value;
        Special = special;
    }

    /// <summary>The 40-bit seed value.</summary>
    public ulong Value { get; }
    public SpecialSeed Special { get; }

    /// <summary>"KELP 7Q2Z"</summary>
    public override string ToString()
    {
        if (Special == SpecialSeed.Debug) return "DEBUG";
        var sb = new StringBuilder(Length + 1);
        ulong v = Value;
        var chars = new char[Length];
        for (int i = Length - 1; i >= 0; i--)
        {
            chars[i] = Alphabet[(int)(v & 31)];
            v >>= 5;
        }
        sb.Append(chars, 0, 4).Append(' ').Append(chars, 4, 4);
        return sb.ToString();
    }

    public static SeedCode FromValue(ulong value) => new(value & ((1UL << 40) - 1), SpecialSeed.None);

    /// <summary>A fresh random seed (not reproducible, which is the point).</summary>
    public static SeedCode NewRandom() => FromValue((ulong)Random.Shared.NextInt64());

    /// <summary>Parses user input: case-insensitive, spaces ignored, O→0-free alphabet. "DEBUG" is special.</summary>
    public static bool TryParse(string? text, out SeedCode seed)
    {
        seed = default;
        if (text is null) return false;
        string s = text.Replace(" ", "").Replace("-", "").ToUpperInvariant();
        if (s == "DEBUG")
        {
            seed = new SeedCode(Rng.Fnv1a("DEBUG") & ((1UL << 40) - 1), SpecialSeed.Debug);
            return true;
        }
        if (s.Length != Length) return false;
        ulong v = 0;
        foreach (char c in s)
        {
            int d = Alphabet.IndexOf(c);
            if (d < 0) return false;
            v = (v << 5) | (uint)d;
        }
        seed = new SeedCode(v, SpecialOf(s));
        return true;
    }

    public static SeedCode Parse(string text) =>
        TryParse(text, out var seed) ? seed : throw new FormatException($"'{text}' is not a seed code");

    static SpecialSeed SpecialOf(string s) => s switch
    {
        "HUGEJELL" => SpecialSeed.BigJelly,
        "TEENYJEL" => SpecialSeed.TinyJelly,
        "DARKDEEP" => SpecialSeed.DarkDeep,
        "PARTYFSH" => SpecialSeed.PartyFish,
        _ => SpecialSeed.None,
    };
}
