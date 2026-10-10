using OctoShoots.Core.Plane;

namespace OctoShoots.Game;

/// <summary>Which build this is, shown on the title screen (bump with each notable change).</summary>
public static class GameVersion
{
    public const string Number = "0.4.0";
    /// <summary>The line of work this build comes from.</summary>
    public const string Line = "m1-corruption";

    /// <summary>For the title's footer: the version, its line, and whether the corruption war is on.</summary>
    public static string Label => $"v{Number} ({Line}) · corruption {(PlaneOptions.Default.Corruption ? "on" : "off")}";
}
