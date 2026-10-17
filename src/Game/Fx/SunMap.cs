using System;
using Godot;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Game.Fx;

/// <summary>
/// Where the sun reaches into the water (sunlight.gdshaderinc's sun_shadow): for every point of the sea surface, the height
/// at which its ray of sunlight (along the sun's direction) first meets the reef — the terrain, a cave's roof, or an
/// arch or overhang. A point below that is in shade. Worked out once per level, off the main thread (about two million
/// steps): until it is ready the sun's shade is off. God rays end at the rock that cuts them, light falls through the
/// holes in arches, and the caustics on creatures, flora and drifting snow stop where the reef shades them.
/// </summary>
public static class SunMap
{
    /// <summary>Texels across the level's square (a texel about 0.75 m).</summary>
    public const int Size = 200;
    /// <summary>The sea surface the map is cast from (ReefLook.SetDrop with no drop).</summary>
    public const float Surface = 18f;

    static ImageTexture? _texture;
    static Image? _image;
    static System.Threading.Tasks.Task<byte[]>? _pending;
    static LevelMap? _pendingFor;
    static bool _wanted;

    /// <summary>Starts casting the level's sun map on a worker; <see cref="Poll"/> puts it in place when it is done.</summary>
    public static void Build(LevelMap map, Vector3 sunDir)
    {
        Enable(false);
        _pendingFor = map;
        _pending = System.Threading.Tasks.Task.Run(() => Cast(map, sunDir));
    }

    /// <summary>Once a frame: a finished map goes to the shaders (if it is still this level's) and the shade turns on.</summary>
    public static void Poll(LevelMap current)
    {
        if (_pending is not { IsCompleted: true } task) return;
        _pending = null;
        if (task.IsFaulted || _pendingFor != current)
        {
            if (task.IsFaulted) GD.PushError(task.Exception?.ToString());
            return;
        }
        var bytes = task.Result;
        if (_image is null || _texture is null)
        {
            _image = Image.CreateFromData(Size, Size, false, Image.Format.Rf, bytes);
            _texture = ImageTexture.CreateFromImage(_image);
        }
        else
        {
            _image.SetData(Size, Size, false, Image.Format.Rf, bytes);
            _texture.Update(_image);
        }
        RenderingServer.GlobalShaderParameterSet(SN.Of("sun_shadow"), _texture);
        _wanted = true;
        Apply();
    }

    static byte[] Cast(LevelMap map, Vector3 sunDir)
    {
        int n = LevelMap.Samples;
        // What overhangs each metre of the reef: a cave roof (its old mountain), or an arch or overhang's span.
        var roof = new float[n * n];
        var canopyBottom = new float[n * n];
        var canopyTop = new float[n * n];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            int i = y * n + x;
            roof[i] = map.LidAt(x, y);
            canopyBottom[i] = float.PositiveInfinity;
            canopyTop[i] = float.NegativeInfinity;
        }
        foreach (var c in map.Canopies)
        {
            if (c.Kind == CanopyKind.CaveRoof) continue;
            float reach = c.Radius > 0f ? c.Radius : MathF.Sqrt(c.HalfLength * c.HalfLength + c.HalfWidth * c.HalfWidth);
            int x0 = Math.Max(0, (int)(c.Center.X - reach)), x1 = Math.Min(n - 1, (int)(c.Center.X + reach) + 1);
            int y0 = Math.Max(0, (int)(c.Center.Y - reach)), y1 = Math.Min(n - 1, (int)(c.Center.Y + reach) + 1);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                if (!c.Contains(new System.Numerics.Vector2(x, y))) continue;
                int i = y * n + x;
                canopyBottom[i] = MathF.Min(canopyBottom[i], c.Bottom);
                canopyTop[i] = MathF.Max(canopyTop[i], c.Top);
            }
        }

        var l = sunDir.Normalized();
        float step = 0.5f;
        float extent = LevelMap.Size;
        var data = new float[Size * Size];
        for (int v = 0; v < Size; v++)
        for (int u = 0; u < Size; u++)
        {
            // Where this ray enters at the surface (texel centre), then down along the sun.
            float ex = (u + 0.5f) / Size * extent, ez = (v + 0.5f) / Size * extent;
            float hit = -100f;
            for (float t = 0f; t < 80f; t += step)
            {
                float px = ex + l.X * t, py = Surface + l.Y * t, pz = ez + l.Z * t;
                if (py < -12f) break;
                if (px < 0f || pz < 0f || px > extent || pz > extent) continue;
                if (py <= Height(map.Heights, px, pz))
                {
                    hit = py;
                    break;
                }
                int i = Math.Min(n - 1, (int)(pz + 0.5f)) * n + Math.Min(n - 1, (int)(px + 0.5f));
                if (!float.IsNaN(roof[i]) && py <= roof[i] || py >= canopyBottom[i] && py <= canopyTop[i])
                {
                    hit = py;
                    break;
                }
            }
            data[v * Size + u] = hit;
        }

        var bytes = new byte[data.Length * 4];
        Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    /// <summary>The terrain's height at (x, z): bilinear on the grid, read directly.</summary>
    static float Height(float[] heights, float x, float z)
    {
        const int n = LevelMap.Samples;
        x = Math.Clamp(x, 0f, LevelMap.Size - 0.0001f);
        z = Math.Clamp(z, 0f, LevelMap.Size - 0.0001f);
        int ix = (int)x, iz = (int)z;
        float tx = x - ix, tz = z - iz;
        int i = iz * n + ix;
        return (heights[i] + (heights[i + 1] - heights[i]) * tx) * (1f - tz) + (heights[i + n] + (heights[i + n + 1] - heights[i + n]) * tx) * tz;
    }

    /// <summary>The sun's shade on or off (off while the dive moves the sea surface between levels).</summary>
    public static void Enable(bool on)
    {
        _wanted = on;
        Apply();
    }

    static void Apply() => RenderingServer.GlobalShaderParameterSet(SN.Of("sun_has_shadow"), _wanted && _texture is not null && _pending is null && System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--dbg-nosunmap") < 0 ? 1f : 0f);
}
