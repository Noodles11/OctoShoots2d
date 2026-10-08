using Godot;

namespace OctoShoots.Game.Fx;

/// <summary>
/// The sun as seen from under the water: one direction for the light and its shadows, shared with the shaders'
/// caustics and god rays (sunlight.gdshaderinc, via the sun_dir global). The level has no shadow map of its own
/// (sun_has_shadow stays 0), so caustics fall everywhere.
/// </summary>
public partial class SunLight : Node3D
{
    DirectionalLight3D _light = null!;
    Color _color = new(0.7f, 0.93f, 1f);
    float _energy = 0.45f;

    /// <summary>The sun of this depth: its colour and strength.</summary>
    public void Configure(Color color, float energy)
    {
        _color = color;
        _energy = energy;
        if (_light is null) return;
        _light.LightColor = color;
        _light.LightEnergy = energy;
        _light.Visible = energy > 0.01f;
    }

    /// <summary>Direction the light travels: steep, a little slanted, so shafts read as diagonal beams.</summary>
    public static readonly Vector3 Direction = new Vector3(0.42f, -0.86f, 0.28f).Normalized();

    public override void _Ready()
    {
        var light = _light = new DirectionalLight3D
        {
            LightColor = _color,
            LightEnergy = _energy,
            ShadowEnabled = true,
            ShadowBlur = 2.5f,
            ShadowBias = 0.06f,
            ShadowNormalBias = 2.2f,
            DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel2Splits,
            DirectionalShadowMaxDistance = 90f,
            LightAngularDistance = 2.5f,
        };
        light.Transform = new Transform3D(Basis.LookingAt(Direction, Vector3.Forward), Vector3.Zero);
        AddChild(light);
        RenderingServer.GlobalShaderParameterSet("sun_dir", Direction);
    }

    public void SetStrength(float strength) => RenderingServer.GlobalShaderParameterSet("light_strength", strength);
}
