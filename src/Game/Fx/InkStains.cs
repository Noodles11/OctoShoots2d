using System;
using Godot;
using OctoShoots.Game.Util;

namespace OctoShoots.Game.Fx;

/// <summary>Ink splats left where shots meet rock (2D §21). Oldest stains are recycled.</summary>
public partial class InkStains : Node3D
{
    const int Max = 80;

    readonly Decal[] _decals = new Decal[Max];
    readonly Random _random = new(99);
    int _next;

    public override void _Ready()
    {
        var texture = BuildTexture();
        for (int i = 0; i < Max; i++)
        {
            _decals[i] = new Decal
            {
                TextureAlbedo = texture,
                Size = new Vector3(0.9f, 0.3f, 0.9f),
                NormalFade = 0.4f,
                Modulate = new Color(0.05f, 0.04f, 0.1f, 0.85f),
                Visible = false,
                CullMask = 1,
            };
            AddChild(_decals[i]);
        }
    }

    public void Add(Vector3 position, Vector3 normal, bool fromPlayer)
    {
        var decal = _decals[_next];
        _next = (_next + 1) % Max;
        float size = (fromPlayer ? 0.8f : 0.55f) * (0.8f + 0.4f * (float)_random.NextDouble());
        decal.Size = new Vector3(size, 0.3f, size);
        decal.Modulate = fromPlayer ? new Color(0.05f, 0.04f, 0.1f, 0.85f) : new Color(1f, 0.85f, 0.4f, 0.5f);
        decal.EmissionEnergy = fromPlayer ? 0f : 1f;
        decal.GlobalTransform = new Transform3D(Conv.AlignUp(normal, (float)_random.NextDouble() * Mathf.Tau), position);
        decal.Visible = true;
    }

    public void Clear()
    {
        foreach (var d in _decals) d.Visible = false;
    }

    static ImageTexture BuildTexture()
    {
        const int n = 128;
        var image = Image.CreateEmpty(n, n, false, Image.Format.Rgba8);
        var random = new Random(5);
        var lobes = new (Vector2 Center, float Radius)[9];
        lobes[0] = (new Vector2(0.5f, 0.5f), 0.26f);
        for (int i = 1; i < lobes.Length; i++)
        {
            float a = (float)random.NextDouble() * Mathf.Tau;
            float d = 0.12f + 0.22f * (float)random.NextDouble();
            lobes[i] = (new Vector2(0.5f + Mathf.Cos(a) * d, 0.5f + Mathf.Sin(a) * d), 0.04f + 0.09f * (float)random.NextDouble());
        }

        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            var uv = new Vector2((x + 0.5f) / n, (y + 0.5f) / n);
            float alpha = 0f;
            foreach (var (center, radius) in lobes)
                alpha = Mathf.Max(alpha, Mathf.SmoothStep(radius, radius * 0.7f, uv.DistanceTo(center)));
            image.SetPixel(x, y, new Color(1, 1, 1, alpha));
        }
        return ImageTexture.CreateFromImage(image);
    }
}
