using System.Collections.Generic;
using OctoShoots.Core.Saves;

namespace OctoShoots.Core.Plane;

/// <summary>
/// Saving a level in play exactly (<see cref="Snapshot"/>): the objects a level regenerated from its seed shares with
/// the one that was saved. The world and the run are restored in place, so whoever holds them still does; the level
/// map with everything in it is restored in place too (the sim changes it: rock breaks, seals open); the item catalog,
/// the options, the tuning and her unlocks are the game's own and are left as they are.
/// </summary>
public sealed partial class PlaneWorld
{
    /// <summary>
    /// Right after the world is made, on both sides: the same objects in the same order. <paramref name="kept"/>: the
    /// game's own trackers of the level (its achievements, its profile recorder), saved and restored in place with it;
    /// <paramref name="profile"/> is left as it is (they write into it as they go, and it is saved on its own).
    /// </summary>
    public Snapshot.Anchors SaveAnchors(object? profile = null, params object?[] kept)
    {
        var roots = new List<(object?, bool, bool)> { (this, false, true), (Run, false, true) };
        foreach (var k in kept) roots.Add((k, false, true));
        roots.Add((Map, true, true));
        roots.Add((Run.Unlocked, false, false));
        roots.Add((profile, false, false));
        roots.Add((Run.Catalog, true, false));
        roots.Add((Options, true, false));
        roots.Add((Tuning, true, false));
        return Snapshot.Anchor(roots);
    }

    /// <summary>The level as it stands now.</summary>
    public byte[] SaveState(Snapshot.Anchors anchors) => Snapshot.Take(anchors);

    /// <summary>The level put back as it was saved (into a world just made from the same level and run).</summary>
    public void LoadState(Snapshot.Anchors anchors, byte[] state)
    {
        Snapshot.Restore(anchors, state);
        // What happened in the step before it was saved has been seen already.
        Events.Clear();
    }
}
