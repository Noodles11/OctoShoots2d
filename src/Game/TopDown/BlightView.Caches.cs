using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Buried caches on screen (docs/CORRUPTION.md): a glossy bump in the ink where loot lies under it, a faint warm glint
/// now and then showing through. It sinks as the ink over it thins, and when the ground is cleansed it breaks open in
/// a puff of gold as the loot floats up.
/// </summary>
public partial class BlightView
{
    readonly Dictionary<BuriedCache, MeshInstance3D> _bumps = new();
    ShaderMaterial? _bumpMat;

    void ClearBumps()
    {
        foreach (var m in _bumps.Values) m.QueueFree();
        _bumps.Clear();
    }

    void SyncBumps(PlaneWorld world)
    {
        if (world.Corruption is not { } field) return;
        foreach (var c in world.Caches)
        {
            if (!_bumps.TryGetValue(c, out var bump))
            {
                if (c.Revealed) continue;
                _bumpMat ??= new ShaderMaterial { Shader = _orb.Shader };
                _bumpMat.SetShaderParameter("ember_color", new Color(1f, 0.75f, 0.35f));
                bump = new MeshInstance3D { Mesh = _sphere, MaterialOverride = _bumpMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
                bump.SetInstanceShaderParameter("seed", c.Seed);
                bump.SetInstanceShaderParameter("wobble", 0.25f);
                AddChild(bump);
                _bumps[c] = bump;
            }
            if (c.Revealed)
            {
                bump.Visible = false;
                continue;
            }
            // It sinks as the ink over it thins.
            float ink = Mathf.Clamp(field.At(c.Position) / CacheTuning.Buried, 0f, 1f);
            float floor = Floor(c.Position);
            float breathe = 1f + 0.05f * Mathf.Sin(_time * 1.3f + c.Seed);
            bump.Position = new Vector3(c.Position.X, floor - 0.05f, c.Position.Y);
            bump.Scale = new Vector3(0.8f * breathe, (0.12f + 0.3f * ink) * breathe, 0.8f * breathe);
            // A faint warm glint shows through now and then: something lies beneath.
            float glint = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(_time * 0.9f + c.Seed * 3f)), 12f);
            bump.SetInstanceShaderParameter("ember", 0.05f + 0.35f * glint);
        }
    }
}
