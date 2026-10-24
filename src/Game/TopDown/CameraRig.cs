using System.Collections.Generic;
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
    public void SetSwimLevel(float y) => _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("swim_level"), y);

    /// <summary>The absolute position of the world's origin (the god rays follow it).</summary>
    public void SetWorldOrigin(Vector2 origin) => _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("world_origin"), origin);

    /// <summary>Moves the camera with the world, keeping the view exactly as it was (her halo in the water too).</summary>
    public void Shift(Vector3 by)
    {
        _focus += by;
        Camera.GlobalPosition += by;
        _player += by;
        _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("player_pos"), _player);
    }

    /// <summary>The point her halo in the water is centred on (the last followed point).</summary>
    Vector3 _player;

    /// <summary>The reduced-motion setting: no refraction wobble in the water.</summary>
    public void SetReducedMotion(bool reduced) => _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("wobble"), reduced ? 0f : 1f);

    /// <summary>Light bubbles: the halos her bubbles and their pop blooms cast on the swim layer (x, z, radius, strength).</summary>
    /// <summary>The clam rooms' glows on the water (xz, radius, strength), at most six.</summary>
    public void SetClamGlows(IReadOnlyList<Vector4> glows, IReadOnlyList<Color> colors)
    {
        int n = Mathf.Min(glows.Count, 6);
        // Sent flattened, as the bubble lights are; each glow in its pearl's colour.
        var flat = new float[24];
        var tint = new float[18];
        for (int i = 0; i < n && i < colors.Count; i++)
        {
            var l = colors[i].SrgbToLinear();
            tint[i * 3] = l.R;
            tint[i * 3 + 1] = l.G;
            tint[i * 3 + 2] = l.B;
        }
        _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("clam_colors"), tint);
        for (int i = 0; i < n; i++)
        {
            flat[i * 4] = glows[i].X;
            flat[i * 4 + 1] = glows[i].Y;
            flat[i * 4 + 2] = glows[i].Z;
            flat[i * 4 + 3] = glows[i].W;
        }
        _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("clam_glows"), flat);
        _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("clam_count"), n);
    }

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
        _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("bubble_lights"), _lights);
        _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("bubble_count"), Mathf.Min(count, lights.Length));
    }

    readonly float[] _lights = new float[CombatView.MaxLights * 4];

    /// <summary>Her bubbles as lenses (x, height, z, radius): the rings of light they throw on the floor.</summary>
    public void SetLenses(Vector4[] lenses, int count)
    {
        for (int i = 0; i < count && i < lenses.Length; i++)
        {
            _lenses[i * 4] = lenses[i].X;
            _lenses[i * 4 + 1] = lenses[i].Y;
            _lenses[i * 4 + 2] = lenses[i].Z;
            _lenses[i * 4 + 3] = lenses[i].W;
        }
        _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("lenses"), _lenses);
        _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("lens_count"), Mathf.Min(count, lenses.Length));
    }

    readonly float[] _lenses = new float[CombatView.MaxLenses * 4];

    /// <summary>The swells in the water round her (WaterSwells): where, how old and strong, and the way she was going.</summary>
    public void SetSwells(OctoShoots.Game.Fx.WaterSwells swells)
    {
        for (int i = 0; i < swells.Count; i++)
        {
            for (int c = 0; c < 4; c++)
            {
                _swells[i * 4 + c] = swells.Rings[i][c];
                _swellDirs[i * 4 + c] = swells.Dirs[i][c];
            }
        }
        _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("swells"), _swells);
        _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("swell_dirs"), _swellDirs);
        _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("swell_count"), swells.Count);
    }

    readonly float[] _swells = new float[OctoShoots.Game.Fx.WaterSwells.Max * 4], _swellDirs = new float[OctoShoots.Game.Fx.WaterSwells.Max * 4];

    /// <summary>
    /// A blast's mark (topdown_post's blurs): a small round smear of the picture on her plane at this point, this wide,
    /// fading over BlurSeconds. At most four at once (the oldest gives way).
    /// </summary>
    public void AddBlur(System.Numerics.Vector2 at, float radius)
    {
        int i = _blurCount < MaxBlurs ? _blurCount++ : OldestBlur();
        _blurSpots[i] = new Vector4(at.X, at.Y, 0f, radius);
    }

    int OldestBlur()
    {
        int o = 0;
        for (int i = 1; i < _blurCount; i++)
            if (_blurSpots[i].Z > _blurSpots[o].Z) o = i;
        return o;
    }

    void TickBlurs(float dt)
    {
        for (int i = _blurCount - 1; i >= 0; i--)
        {
            _blurSpots[i].Z += dt / BlurSeconds;
            if (_blurSpots[i].Z >= 1f) _blurSpots[i] = _blurSpots[--_blurCount];
        }
        for (int i = 0; i < _blurCount; i++)
            for (int c = 0; c < 4; c++) _blurFlat[i * 4 + c] = _blurSpots[i][c];
        _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("blurs"), _blurFlat);
        _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("blur_count"), _blurCount);
    }

    const int MaxBlurs = 4;
    const float BlurSeconds = 0.9f;
    readonly Vector4[] _blurSpots = new Vector4[MaxBlurs];
    readonly float[] _blurFlat = new float[MaxBlurs * 4];
    int _blurCount;

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
        if (System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--dbg-lightview") >= 0) _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("debug_view"), 1);
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

    /// <summary>
    /// Follow a point on the swim band; snap jumps straight there (level start, teleports). The camera centres look
    /// metres past it (the level-up frames her at the screen's left); her halo stays on the point itself.
    /// </summary>
    public void Track(Vector3 focus, float dt, bool snap = false, Vector3 look = default)
    {
        TickBlurs(dt);
        var aim = focus + look;
        if (snap || !_snapped)
        {
            _focus = aim;
            _snapped = true;
        }
        else
        {
            _focus = _focus.Lerp(aim, 1f - Mathf.Exp(-Follow * dt));
        }
        float tilt = Mathf.DegToRad(Tilt);
        // North is −Z: the camera sits south of the focus, looking north and down.
        Vector3 offset = new Vector3(0f, Mathf.Sin(tilt), Mathf.Cos(tilt)) * _distance * Zoom;
        _shakeTime += dt;
        _shake = Mathf.MoveToward(_shake, 0f, dt * 1.6f);
        Vector3 jolt = _shake > 0f ? new Vector3(Mathf.Sin(_shakeTime * 53f), Mathf.Sin(_shakeTime * 61f + 1f), Mathf.Sin(_shakeTime * 47f + 2f)) * _shake * _shake : Vector3.Zero;
        Camera.GlobalTransform = new Transform3D(Basis.FromEuler(new Vector3(-tilt, 0f, 0f)), _focus + offset + jolt);
        _player = focus;
        _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("player_pos"), focus);
        _edgeFlash = Mathf.MoveToward(_edgeFlash, 0f, dt / EdgeFlashSeconds);
        _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("edge_flash"), _edgeFlash);
    }
}
