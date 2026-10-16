using System;
using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Items;

namespace OctoShoots.Game.Fx;

/// <summary>
/// A pearl's own light (a clam room's pearl lights its room): its colour for the room's fill and spill and the water's
/// haze over it. Its pattern is painted over the room by the shell's shader (pearl_glow.gdshaderinc).
/// </summary>
public static class PearlLight
{
    /// <summary>The pearl's mean colour (for its spill and the water's haze over its room).</summary>
    public static Color Mean(ItemDef item)
    {
        var colors = item.Pearl?.Colors;
        if (colors is not { Count: > 0 }) return Colors.White;
        float r = 0f, g = 0f, b = 0f;
        foreach (string hex in colors)
        {
            var c = Color.FromHtml(hex);
            r += c.R;
            g += c.G;
            b += c.B;
        }
        var mean = new Color(r / colors.Count, g / colors.Count, b / colors.Count);
        // Lifted toward full saturation, so a pastel pearl still tints the water noticeably.
        return Color.FromHsv(mean.H, Mathf.Clamp(mean.S * 1.3f, 0f, 1f), 1f);
    }
}
