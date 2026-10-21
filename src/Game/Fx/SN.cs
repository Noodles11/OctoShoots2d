using System.Collections.Generic;
using Godot;

namespace OctoShoots.Game.Fx;

/// <summary>
/// Shader parameter names, made once. Godot's C# API takes them as StringName, and a string passed in its place makes a
/// new native StringName on every call (an object the garbage collector must then finalise); set on every frame for
/// every bubble, murkling and meter, those were most of the game's garbage and the collections that stuttered it.
/// </summary>
public static class SN
{
    static readonly Dictionary<string, StringName> Names = new();

    public static StringName Of(string name)
    {
        if (!Names.TryGetValue(name, out var sn)) Names[name] = sn = new StringName(name);
        return sn;
    }
}
