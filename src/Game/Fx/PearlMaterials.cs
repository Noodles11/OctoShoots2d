using System;
using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Items;

namespace OctoShoots.Game.Fx;

/// <summary>One material per item pearl, built from its gradient and pattern (PearlKey: what it does; pearl.gdshader).</summary>
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
        material.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("c0"), c0);
        material.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("c1"), c1);
        material.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("c2"), c2);
        material.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("icon"), (int)look.Icon);
        material.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("seed"), (float)(StringComparer.Ordinal.GetHashCode(item.Id) % 97));
        material.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("glow"), 1.2f);
        Cache[item.Id] = material;
        return material;
    }

    /// <summary>A pearl for a menu (pearl_icon.gdshader on a ColorRect): its colours and its engraving; null, a plain white one.</summary>
    public static ShaderMaterial Flat(PearlLook? look)
    {
        var colors = look?.Colors ?? new List<string> { "#ffffff", "#dddddd" };
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/pearl_icon.gdshader") };
        material.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("c0"), Color.FromHtml(colors[0]));
        material.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("c1"), Color.FromHtml(colors[Math.Min(1, colors.Count - 1)]));
        material.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("c2"), Color.FromHtml(colors[^1]));
        material.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("icon"), (int)(look?.Icon ?? PearlIcon.Star));
        return material;
    }

    /// <summary>The pearl's middle colour, for its light.</summary>
    public static Color GlowColor(ItemDef item)
    {
        var colors = item.Pearl?.Colors;
        return colors is { Count: > 0 } ? Color.FromHtml(colors[Math.Min(1, colors.Count - 1)]) : Colors.White;
    }
}
