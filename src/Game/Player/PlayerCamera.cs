using Godot;
using OctoShoots.Game.Settings;

namespace OctoShoots.Game.Player;

/// <summary>
/// First-person camera (§3.1): look angles applied at render rate, position interpolated between
/// sim ticks, trauma-based shake, and Clementine's warm glow as the main light. No head bob.
/// </summary>
public partial class PlayerCamera : Node3D
{
    public Camera3D Camera { get; private set; } = null!;
    public Viewmodel Viewmodel { get; private set; } = null!;
    public OmniLight3D Glow { get; private set; } = null!;

    float _trauma;
    float _shakeTime;
    Vector3 _lastPosition;
    float _lastYaw, _lastPitch;
    bool _hasLast;

    public override void _Ready()
    {
        Camera = new Camera3D
        {
            KeepAspect = Camera3D.KeepAspectEnum.Width,
            Near = 0.03f,
            Far = 200f,
            Current = true,
        };
        AddChild(Camera);

        Glow = new OmniLight3D
        {
            LightColor = new Color(1f, 0.62f, 0.3f),
            LightEnergy = 1.3f,
            OmniRange = 11f,
            OmniAttenuation = 1.2f,
            ShadowEnabled = false,
            LightCullMask = ~Viewmodel.RenderLayer,
            Position = new Vector3(0f, 0.1f, 0f),
        };
        AddChild(Glow);

        Viewmodel = new Viewmodel();
        Camera.AddChild(Viewmodel);
    }

    public void AddTrauma(float amount) => _trauma = Mathf.Min(1f, _trauma + amount);


    public void Tick(float dt, Vector3 position, float yaw, float pitch, float speed01, ViewOptions view)
    {
        Camera.Fov = view.Fov;

        _trauma = Mathf.MoveToward(_trauma, 0f, dt * 1.6f);
        _shakeTime += dt * 30f;
        float shake = view.CameraShake ? _trauma * _trauma * view.ShakeAmount : 0f;
        float shakeYaw = Mathf.Sin(_shakeTime * 1.3f) * 0.05f * shake;
        float shakePitch = Mathf.Sin(_shakeTime * 1.7f + 1f) * 0.05f * shake;
        float shakeRoll = Mathf.Sin(_shakeTime * 1.1f + 2f) * 0.04f * shake;

        // How she moves and turns, in view space, so the arm can trail in the water.
        Vector3 velocity = Vector3.Zero;
        float yawRate = 0f, pitchRate = 0f;
        if (_hasLast && dt > 1e-4f)
        {
            velocity = Basis.FromEuler(new Vector3(pitch, yaw, 0f)).Inverse() * ((position - _lastPosition) / dt);
            yawRate = Mathf.AngleDifference(_lastYaw, yaw) / dt;
            pitchRate = (pitch - _lastPitch) / dt;
        }
        _lastPosition = position;
        _lastYaw = yaw;
        _lastPitch = pitch;
        _hasLast = true;

        GlobalPosition = position;
        Rotation = new Vector3(pitch + shakePitch, yaw + shakeYaw, 0f);
        Camera.Rotation = new Vector3(0f, 0f, shakeRoll);
        Viewmodel.Tick(dt, speed01, velocity, yawRate, pitchRate);
    }
}
