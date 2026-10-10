using System.Runtime.CompilerServices;
using OctoShoots.Core.Plane;

namespace OctoShoots.Core.Tests;

/// <summary>The sim tests written before the corruption run the older rules (pufferlings, no corruption) by default.</summary>
static class TestSetup
{
    [ModuleInitializer]
    internal static void Init() => PlaneOptions.Default = new PlaneOptions { Corruption = false, Pufferlings = true };
}
