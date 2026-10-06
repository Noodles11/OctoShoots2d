using System;
using Godot;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// What Clementine has seen of the level, per metre of the grid: 0 unseen, 1 seen, soft in between. Revealed around
/// her as she swims (a disc with a soft rim) and kept for the rest of the level; the minimap draws it as fog.
/// </summary>
public sealed class FogOfWar
{
    const int N = LevelMap.Samples;
    readonly float[] _reveal = new float[N * N];
    readonly Image _image = Image.CreateEmpty(N, N, false, Image.Format.Rf);
    System.Numerics.Vector2 _last = new(-1000f, -1000f);

    public FogOfWar() => Texture = ImageTexture.CreateFromImage(_image);

    /// <summary>The revealed amount per grid point, for shaders (sample at (world + 0.5) / samples).</summary>
    public ImageTexture Texture { get; }

    /// <summary>How much of the point has been seen (0–1), from the nearest grid point.</summary>
    public float At(System.Numerics.Vector2 p)
    {
        int x = Math.Clamp((int)MathF.Round(p.X), 0, N - 1), y = Math.Clamp((int)MathF.Round(p.Y), 0, N - 1);
        return _reveal[y * N + x];
    }

    public void Reset()
    {
        Array.Clear(_reveal);
        _image.Fill(new Color(0f, 0f, 0f));
        Texture.Update(_image);
        _last = new(-1000f, -1000f);
    }

    /// <summary>Reveals a disc of the given radius around p, its last <paramref name="soft"/> metres fading out.</summary>
    public void Reveal(System.Numerics.Vector2 p, float radius, float soft)
    {
        if (System.Numerics.Vector2.Distance(p, _last) < 0.4f) return;
        _last = p;
        bool changed = false;
        int x0 = Math.Max(0, (int)(p.X - radius)), x1 = Math.Min(N - 1, (int)(p.X + radius) + 1);
        int y0 = Math.Max(0, (int)(p.Y - radius)), y1 = Math.Min(N - 1, (int)(p.Y + radius) + 1);
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            float d = System.Numerics.Vector2.Distance(new System.Numerics.Vector2(x, y), p);
            float v = 1f - Mathf.SmoothStep(radius - soft, radius, d);
            if (v <= _reveal[y * N + x]) continue;
            _reveal[y * N + x] = v;
            _image.SetPixel(x, y, new Color(v, 0f, 0f));
            changed = true;
        }
        if (changed) Texture.Update(_image);
    }
}
