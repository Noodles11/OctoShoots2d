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

    public Camera3D Camera { get; private set; } = null!;
    Vector3 _focus;
    ShaderMaterial _post = null!;
    bool _snapped;
    float _shake, _shakeTime;

    /// <summary>A jolt (a boss landing, a slam): the view shakes, easing out over about half a second.</summary>
    public void Shake(float amount) => _shake = Mathf.Max(_shake, amount);

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
        Vector3 offset = new(0f, Distance * Mathf.Sin(tilt), Distance * Mathf.Cos(tilt));
        _shakeTime += dt;
        _shake = Mathf.MoveToward(_shake, 0f, dt * 1.6f);
        Vector3 jolt = _shake > 0f ? new Vector3(Mathf.Sin(_shakeTime * 53f), Mathf.Sin(_shakeTime * 61f + 1f), Mathf.Sin(_shakeTime * 47f + 2f)) * _shake * _shake : Vector3.Zero;
        Camera.GlobalTransform = new Transform3D(Basis.FromEuler(new Vector3(-tilt, 0f, 0f)), _focus + offset + jolt);
        _post.SetShaderParameter("player_pos", focus);
    }
}
