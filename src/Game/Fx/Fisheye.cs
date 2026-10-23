using Godot;

namespace OctoShoots.Game.Fx;

/// <summary>
/// A slight fisheye over the view (fisheye.gdshader): the finished 3D picture bends toward the screen's edges, the
/// corners staying the corners, drawn under the HUD. What is shown at a screen point lies, undistorted, at Source of it
/// (aiming with the mouse goes through it); a world point projected to the screen is shown at Shown of it (the HUD's
/// markers over the world go through that).
/// </summary>
public partial class Fisheye : CanvasLayer
{
    public const float Strength = 0.08f;

    public override void _Ready()
    {
        Layer = -1;
        var rect = new ColorRect { MouseFilter = Control.MouseFilterEnum.Ignore };
        rect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/fisheye.gdshader") };
        material.SetShaderParameter(SN.Of("strength"), Strength);
        rect.Material = material;
        AddChild(rect);
        // Verification: --dbg-nofisheye shows the view straight (the pointer's sums still assume it; for comparing only).
        if (System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--dbg-nofisheye") >= 0) Visible = false;
    }

    /// <summary>The undistorted screen point shown at this one (the shader's sums).</summary>
    public static Vector2 Source(Vector2 shown, Vector2 size)
    {
        var a = new Vector2(size.X / size.Y, 1f);
        var r = (shown / size - new Vector2(0.5f, 0.5f)) * a;
        float rmax2 = (a * 0.5f).LengthSquared(), r2 = r.LengthSquared();
        var src = r * (1f + Strength * r2 * r2) / (1f + Strength * rmax2 * rmax2);
        return (src / a + new Vector2(0.5f, 0.5f)) * size;
    }

    /// <summary>Where an undistorted screen point is shown (Source undone, by a few steps of it).</summary>
    public static Vector2 Shown(Vector2 source, Vector2 size)
    {
        var a = new Vector2(size.X / size.Y, 1f);
        var s = (source / size - new Vector2(0.5f, 0.5f)) * a;
        float rmax2 = (a * 0.5f).LengthSquared(), norm = 1f + Strength * rmax2 * rmax2;
        var r = s;
        for (int i = 0; i < 6; i++)
        {
            float r2 = r.LengthSquared();
            r = s * norm / (1f + Strength * r2 * r2);
        }
        return (r / a + new Vector2(0.5f, 0.5f)) * size;
    }
}
