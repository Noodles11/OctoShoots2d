using System;
using Godot;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Game.Fx;

/// <summary>
/// Where a point light reaches over the reef (DESIGN-TOPDOWN §5.3): a polar horizon map. The reef is a heightfield, so
/// from a light at L a point P is lit exactly when the slope from L up to P clears the steepest slope of the terrain
/// between them. For each of <see cref="Angles"/> directions out from the light and <see cref="Rings"/> distances along
/// each, the map holds that running steepest slope (the terrain's height over the light, divided by the distance, the
/// maximum so far); a shader tests a point with one lookup (her_vis in light_field.gdshaderinc). It costs 8 192 height
/// samples, a fraction of a millisecond, and is redone only when the light moves.
/// </summary>
public sealed class HorizonMap
{
    public const int Angles = 256, Rings = 32;

    readonly float[] _slopes = new float[Angles * Rings];
    readonly byte[] _bytes = new byte[Angles * Rings * 4];
    Image? _image;
    Vector3 _for = new(float.NaN, 0f, 0f);
    float _rangeFor;
    LevelMap? _mapFor;

    public ImageTexture Texture { get; } = new();

    /// <summary>
    /// The map for a light at <paramref name="light"/> (level coordinates: x, height, z) reaching <paramref name="range"/>
    /// metres. Redone only when the light has moved a few centimetres, its reach changed or the level is new.
    /// </summary>
    public bool Update(LevelMap map, Vector3 light, float range)
    {
        if (map == _mapFor && light.DistanceSquaredTo(_for) < 0.0025f && Mathf.Abs(range - _rangeFor) < 0.05f) return false;
        _mapFor = map;
        _for = light;
        _rangeFor = range;
        float step = range / Rings * 0.5f;
        var heights = map.Heights;
        const int n = LevelMap.Samples;
        const float max = LevelMap.Size - 0.0001f;
        for (int a = 0; a < Angles; a++)
        {
            // The same angles the shader reads: atan(dz, dx) over the full turn, texel centres.
            float angle = ((a + 0.5f) / Angles - 0.5f) * Mathf.Tau;
            float dx = MathF.Cos(angle) * step, dz = MathF.Sin(angle) * step;
            float steepest = float.NegativeInfinity;
            // Two samples a ring (so a wall thinner than a ring is not stepped over), the grid read directly.
            for (int k = 1; k <= Rings * 2; k++)
            {
                float x = Math.Clamp(light.X + dx * k, 0f, max), z = Math.Clamp(light.Z + dz * k, 0f, max);
                int ix = (int)x, iz = (int)z;
                float tx = x - ix, tz = z - iz;
                int i = iz * n + ix;
                float h = (heights[i] + (heights[i + 1] - heights[i]) * tx) * (1f - tz) + (heights[i + n] + (heights[i + n + 1] - heights[i + n]) * tx) * tz;
                steepest = MathF.Max(steepest, (h - light.Y) / (step * k));
                if ((k & 1) == 0) _slopes[(k / 2 - 1) * Angles + a] = steepest;
            }
        }
        Buffer.BlockCopy(_slopes, 0, _bytes, 0, _bytes.Length);
        if (_image is null)
        {
            _image = Image.CreateFromData(Angles, Rings, false, Image.Format.Rf, _bytes);
            Texture.SetImage(_image);
        }
        else
        {
            _image.SetData(Angles, Rings, false, Image.Format.Rf, _bytes);
            Texture.Update(_image);
        }
        return true;
    }
}
