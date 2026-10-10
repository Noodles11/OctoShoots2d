using System.Linq;
using Godot;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The top-down camera (DESIGN-TOPDOWN §3): tilted ~60°, narrow perspective (~35° FOV), fixed yaw with north always up,
/// smoothed follow, no rotation. About 30×22 m of the level fit on screen (§4.5).
/// </summary>
public partial class CameraRig : Node3D
{
    /// <summary>Downward tilt from the horizontal.</summary>
    public const float Tilt = 60f;
    public const float Fov = 35f;
    /// <summary>Distance from the followed point; puts ~22 m of ground top to bottom and ~33 m across at 16:9.</summary>
    public const float Distance = 34f;
    const float Follow = 6f;
    /// <summary>The zoom setting: 1.5 by default, from 1 (50% farther, more of the reef) to 2 (50% closer).</summary>
    public const float DefaultZoom = 1.5f, MinZoom = 1f, MaxZoom = 2f;
    /// <summary>Verification: `--dbg-zoom=metres` brings the camera closer (to inspect Clementine); it overrides the setting.</summary>
    readonly float? _dbgDistance = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--dbg-zoom=")) is { } zoom
        && float.TryParse(zoom["--dbg-zoom=".Length..], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float d) ? d : null;
    float _distance = Distance / DefaultZoom;

    /// <summary>The camera-zoom setting (1 is the designed 34 m framing; 1.5 the default). The dive's Zoom multiplies it.</summary>
    public float ZoomSetting
    {
        set => _distance = _dbgDistance ?? Distance / Mathf.Clamp(value, MinZoom, MaxZoom);
    }

    public Camera3D Camera { get; private set; } = null!;
    /// <summary>The dive pulls the camera in (1 is the usual distance).</summary>
    public float Zoom { get; set; } = 1f;
    Vector3 _focus;
    ShaderMaterial _post = null!;
    bool _snapped;
    float _shake, _shakeTime;

    /// <summary>A jolt (a boss landing, a slam): the view shakes, easing out over about half a second.</summary>
    public void Shake(float amount)
    {
        if (ShakeEnabled) _shake = Mathf.Max(_shake, amount);
    }

    /// <summary>The swim level the depth focus is sharp at (the dive slides it down to the level below).</summary>
    public void SetSwimLevel(float y) => _post.SetShaderParameter("swim_level", y);

    /// <summary>The absolute position of the world's origin (the god rays follow it).</summary>
    public void SetWorldOrigin(Vector2 origin) => _post.SetShaderParameter("world_origin", origin);

    /// <summary>Moves the camera with the world, keeping the view exactly as it was (her halo in the water too).</summary>
    public void Shift(Vector3 by)
    {
        _focus += by;
        Camera.GlobalPosition += by;
        _player += by;
        _post.SetShaderParameter("player_pos", _player);
    }

    /// <summary>The point her halo in the water is centred on (the last followed point).</summary>
    Vector3 _player;

    /// <summary>The reduced-motion setting: no refraction wobble in the water.</summary>
    public void SetReducedMotion(bool reduced) => _post.SetShaderParameter("wobble", reduced ? 0f : 1f);

    /// <summary>Light bubbles: the halos her bubbles and their pop blooms cast on the swim layer (x, z, radius, strength).</summary>
    public void SetBubbleLights(Vector4[] lights, int count)
    {
        // A vec4 array uniform, sent flattened (the form every renderer accepts).
        for (int i = 0; i < count && i < lights.Length; i++)
        {
            _lights[i * 4] = lights[i].X;
            _lights[i * 4 + 1] = lights[i].Y;
            _lights[i * 4 + 2] = lights[i].Z;
            _lights[i * 4 + 3] = lights[i].W;
        }
        _post.SetShaderParameter("bubble_lights", _lights);
        _post.SetShaderParameter("bubble_count", Mathf.Min(count, lights.Length));
    }

    readonly float[] _lights = new float[CombatView.MaxLights * 4];

    /// <summary>A big-merge kill: the screen's edges lift toward white for 60 ms.</summary>
    public void EdgeFlash() => _edgeFlash = 1f;
    float _edgeFlash;
    const float EdgeFlashSeconds = 0.06f;

    /// <summary>The camera-shake setting.</summary>
    public bool ShakeEnabled { get; set; } = true;

    public override void _Ready()
    {
        Camera = new Camera3D { Fov = Fov, Near = 0.5f, Far = 200f, Current = true };
        AddChild(Camera);
        // The depth focus: the swim level sharp, the deeps blurred into fog, high rock blurred into silhouette (Below's look).
        _post = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/topdown_post.gdshader"), RenderPriority = -100 };
        Camera.AddChild(new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(2f, 2f) },
            MaterialOverride = _post,
            ExtraCullMargin = 16384f,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            // Verification: `--no-focus` shows the plain render.
            Visible = !OS.GetCmdlineUserArgs().Contains("--no-focus"),
        });
    }

    /// <summary>Follow a point on the swim band; snap jumps straight there (level start, teleports).</summary>
    public void Track(Vector3 focus, float dt, bool snap = false)
    {
        if (snap || !_snapped)
        {
            _focus = focus;
            _snapped = true;
        }
        else
        {
            _focus = _focus.Lerp(focus, 1f - Mathf.Exp(-Follow * dt));
        }
        float tilt = Mathf.DegToRad(Tilt);
        // North is −Z: the camera sits south of the focus, looking north and down.
        Vector3 offset = new Vector3(0f, Mathf.Sin(tilt), Mathf.Cos(tilt)) * _distance * Zoom;
        _shakeTime += dt;
        _shake = Mathf.MoveToward(_shake, 0f, dt * 1.6f);
        Vector3 jolt = _shake > 0f ? new Vector3(Mathf.Sin(_shakeTime * 53f), Mathf.Sin(_shakeTime * 61f + 1f), Mathf.Sin(_shakeTime * 47f + 2f)) * _shake * _shake : Vector3.Zero;
        Camera.GlobalTransform = new Transform3D(Basis.FromEuler(new Vector3(-tilt, 0f, 0f)), _focus + offset + jolt);
        _player = focus;
        _post.SetShaderParameter("player_pos", focus);
        _edgeFlash = Mathf.MoveToward(_edgeFlash, 0f, dt / EdgeFlashSeconds);
        _post.SetShaderParameter("edge_flash", _edgeFlash);
    }
}
