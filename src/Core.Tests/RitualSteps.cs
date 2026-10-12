using OctoShoots.Core.Plane;

namespace OctoShoots.Core.Tests;

/// <summary>Pearls are absorbed only when their ritual completes: steps a world through one.</summary>
static class RitualSteps
{
    public static void StepRitual(this PlaneWorld w)
    {
        for (int i = 0; i < (int)(PearlRitual.Total / PlaneWorld.Dt) + 2; i++) w.Step(default);
    }
}
