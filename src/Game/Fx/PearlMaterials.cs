using System;
using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Items;

namespace OctoShoots.Game.Fx;

/// <summary>One material per item pearl, built from its hand-picked gradient and pattern.</summary>
public static class PearlMaterials
{
    static readonly Dictionary<(string, bool), ShaderMaterial> Cache = new();
    static Shader? _world, _overlay;

    /// <param name="overlay">Drawn over everything, for pearls held on the inventory tentacle.</param>
    public static ShaderMaterial Get(ItemDef item, bool overlay)
    {
        if (Cache.TryGetValue((item.Id, overlay), out var cached)) return cached;
        _world ??= GD.Load<Shader>("res://assets/shaders/pearl.gdshader");
        _overlay ??= GD.Load<Shader>("res://assets/shaders/pearl_overlay.gdshader");

        var look = item.Pearl ?? new PearlLook { Colors = { "#ffffff", "#dddddd" } };
        Color c0 = Color.FromHtml(look.Colors[0]);
        Color c1 = Color.FromHtml(look.Colors[Math.Min(1, look.Colors.Count - 1)]);
        Color c2 = Color.FromHtml(look.Colors[^1]);
        var material = new ShaderMaterial { Shader = overlay ? _overlay : _world, RenderPriority = overlay ? 12 : 0 };
        material.SetShaderParameter("c0", c0);
        material.SetShaderParameter("c1", c1);
        material.SetShaderParameter("c2", c2);
        material.SetShaderParameter("pattern", (int)look.Pattern);
        material.SetShaderParameter("seed", (float)(StringComparer.Ordinal.GetHashCode(item.Id) % 97));
        material.SetShaderParameter("glow", overlay ? 0.8f : 1.2f);
        Cache[(item.Id, overlay)] = material;
        return material;
    }

    /// <summary>The pearl's middle colour, for its light.</summary>
    public static Color GlowColor(ItemDef item)
    {
        var colors = item.Pearl?.Colors;
        return colors is { Count: > 0 } ? Color.FromHtml(colors[Math.Min(1, colors.Count - 1)]) : Colors.White;
    }
}
