using System;
using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Items;

namespace OctoShoots.Game.Fx;

/// <summary>One material per item pearl, built from its hand-picked gradient and pattern (pearl.gdshader).</summary>
public static class PearlMaterials
{
    static readonly Dictionary<string, ShaderMaterial> Cache = new();
    static Shader? _shader;

    public static ShaderMaterial Get(ItemDef item)
    {
        if (Cache.TryGetValue(item.Id, out var cached)) return cached;
        _shader ??= GD.Load<Shader>("res://assets/shaders/pearl.gdshader");

        var look = item.Pearl ?? new PearlLook { Colors = { "#ffffff", "#dddddd" } };
        Color c0 = Color.FromHtml(look.Colors[0]);
        Color c1 = Color.FromHtml(look.Colors[Math.Min(1, look.Colors.Count - 1)]);
        Color c2 = Color.FromHtml(look.Colors[^1]);
        var material = new ShaderMaterial { Shader = _shader };
        material.SetShaderParameter("c0", c0);
        material.SetShaderParameter("c1", c1);
        material.SetShaderParameter("c2", c2);
        material.SetShaderParameter("pattern", (int)look.Pattern);
        material.SetShaderParameter("seed", (float)(StringComparer.Ordinal.GetHashCode(item.Id) % 97));
        material.SetShaderParameter("glow", 1.2f);
        Cache[item.Id] = material;
        return material;
    }

    /// <summary>The pearl's middle colour, for its light.</summary>
    public static Color GlowColor(ItemDef item)
    {
        var colors = item.Pearl?.Colors;
        return colors is { Count: > 0 } ? Color.FromHtml(colors[Math.Min(1, colors.Count - 1)]) : Colors.White;
    }
}
