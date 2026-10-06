using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Placeholder Clementine (DESIGN-TOPDOWN §2.1): a glowing translucent bell on the swim band with a warm light,
/// a slow breathing pulse and the gentle idle bob. The soft-body bell, tentacles and pattern composer come later.
/// Ink clouds left by dashes are drawn here too.
/// </summary>
public partial class BellView : Node3D
{
    MeshInstance3D _bell = null!;
    StandardMaterial3D _inkMaterial = null!;
    readonly System.Collections.Generic.List<MeshInstance3D> _ink = new();
    float _time;

    public override void _Ready()
    {
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.62f, 0.5f, 0.75f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            EmissionEnabled = true,
            Emission = new Color(1f, 0.55f, 0.4f),
            EmissionEnergyMultiplier = 1.6f,
            RimEnabled = true,
            Rim = 1f,
            Roughness = 0.3f,
            // Always seen, even behind a wall (it is never cut away).
            NoDepthTest = true,
        };
        _bell = new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.4f, Height = 0.8f, IsHemisphere = true, RadialSegments = 24, Rings = 8 }, MaterialOverride = material };
        AddChild(_bell);
        // Her glow is the lantern of the scene: a warm pool in the dark water around her.
        AddChild(new OmniLight3D { LightColor = new Color(1f, 0.72f, 0.52f), LightEnergy = 2.6f, OmniRange = 11f, OmniAttenuation = 1.4f, ShadowEnabled = false, Position = new Vector3(0f, 1.2f, 0f) });
        _inkMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.06f, 0.04f, 0.12f, 0.55f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
    }

    public void Sync(PlaneWorld world, float alpha, float dt)
    {
        _time += dt;
        var p = world.Player;
        var pos = System.Numerics.Vector2.Lerp(p.PrevPosition, p.Position, alpha);
        float speed = p.Velocity.Length();
        // Idle drift bobs gently on the depth band (presentation only: the sim is plane-locked).
        float bob = 0.5f * Mathf.Sin(_time * 1.1f) * Mathf.Clamp(1f - speed / 2f, 0f, 1f);
        Position = new Vector3(pos.X, LevelMap.SwimBand + bob, pos.Y);
        float breathe = 1f + 0.08f * Mathf.Sin(_time * (2f + speed));
        _bell.Scale = new Vector3(breathe, 1f / breathe, breathe);
        _bell.Visible = p.DashInvulnerableTimer <= 0f || Mathf.PosMod(_time * 20f, 2f) < 1.4f;

        while (_ink.Count < world.Clouds.Count)
        {
            var m = new MeshInstance3D { Mesh = new SphereMesh { Radius = 1f, Height = 1.2f }, MaterialOverride = _inkMaterial };
            GetParent().AddChild(m);
            _ink.Add(m);
        }
        for (int i = 0; i < _ink.Count; i++)
        {
            bool used = i < world.Clouds.Count;
            _ink[i].Visible = used;
            if (!used) continue;
            var c = world.Clouds[i];
            float k = c.Life / c.MaxLife;
            _ink[i].GlobalPosition = new Vector3(c.Position.X, LevelMap.SwimBand, c.Position.Y);
            _ink[i].Scale = Vector3.One * c.Radius * (1.3f - 0.3f * k);
        }
    }
}
