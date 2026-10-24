using Godot;

namespace OctoShoots.Game.Fx;

/// <summary>
/// The mouse pointer while she plays with the mouse (TopDownMain) and on the title screen (TitleMain): a small orange
/// crosshair of her four gonads, its hot spot at its heart.
/// </summary>
public static class GonadCursor
{
    static ImageTexture? _texture;

    /// <summary>The crosshair for the arrow and the pointing hand (the menus' buttons) alike.</summary>
    public static void Use()
    {
        var image = Texture();
        var hot = new Vector2(image.GetWidth(), image.GetHeight()) * 0.5f;
        Input.SetCustomMouseCursor(image, Input.CursorShape.Arrow, hot);
        Input.SetCustomMouseCursor(image, Input.CursorShape.PointingHand, hot);
    }

    /// <summary>Back to the system's own pointers.</summary>
    public static void Release()
    {
        Input.SetCustomMouseCursor(null, Input.CursorShape.Arrow);
        Input.SetCustomMouseCursor(null, Input.CursorShape.PointingHand);
    }

    /// <summary>
    /// The crosshair: her four gonads, small horseshoes at the quarters (as on her bell, at 45°), open toward the middle
    /// where she aims, in her warm orange with a dark edge so it reads on bright sand and in the deeps; a dot at its heart.
    /// </summary>
    public static ImageTexture Texture()
    {
        if (_texture is not null) return _texture;
        const int size = 32;
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        var orange = new Color(1f, 0.58f, 0.18f);
        var edge = new Color(0.12f, 0.05f, 0.02f, 0.85f);
        var c = new Vector2(size * 0.5f, size * 0.5f);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var q = new Vector2(x + 0.5f, y + 0.5f) - c;
                float ink = 0f, line = 0f;
                for (int k = 0; k < 4; k++)
                {
                    float a = Mathf.Pi * 0.25f + k * Mathf.Pi * 0.5f;
                    var at = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 8.5f;
                    var d = q - at;
                    // Open toward the middle.
                    float open = d.Length() > 1e-3f ? d.Normalized().Dot(-at.Normalized()) : 0f;
                    if (open > 0.55f) continue;
                    float ring = Mathf.Abs(d.Length() - 4f);
                    line = Mathf.Max(line, Mathf.Clamp(1.6f - ring, 0f, 1f));
                    ink = Mathf.Max(ink, Mathf.Clamp(2.9f - ring, 0f, 1f));
                }
                float dot = q.Length();
                line = Mathf.Max(line, Mathf.Clamp(1.8f - dot, 0f, 1f));
                ink = Mathf.Max(ink, Mathf.Clamp(3f - dot, 0f, 1f));
                if (ink <= 0f) continue;
                var col = edge.Lerp(orange, line);
                image.SetPixel(x, y, col with { A = Mathf.Max(edge.A * ink, line) });
            }
        // Verification: --dbg-crosshair=path saves it.
        foreach (string arg in OS.GetCmdlineUserArgs())
            if (arg.StartsWith("--dbg-crosshair=")) image.SavePng(arg["--dbg-crosshair=".Length..]);
        return _texture = ImageTexture.CreateFromImage(image);
    }
}
