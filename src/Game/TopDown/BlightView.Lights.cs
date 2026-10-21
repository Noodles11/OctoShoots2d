using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The corrupted creatures' own colours and light (docs/CORRUPTION.md): each kind has its base colour — murklings stay
/// ink-black, each with its core's colour; Blightroots (and their vines) blue, Blotters orange, Thumpsquirts acid green,
/// Inkwardens violet, Lashjaws crimson — and its projectiles share it. Each casts a little light in its core's or base
/// colour, so the creatures show in the dark depths: a pool of small shadowless lights goes to the ones nearest her,
/// stronger the darker the depth, always well under her own lantern.
/// </summary>
public partial class BlightView
{
    public static class Kin
    {
        public static readonly Color Blightroot = new(0.2f, 0.48f, 1f);
        public static readonly Color Blotter = new(1f, 0.5f, 0.1f);
        public static readonly Color Thumpsquirt = new(0.5f, 1f, 0.18f);
        public static readonly Color Inkwarden = new(0.66f, 0.3f, 1f);
        public static readonly Color Lashjaw = new(1f, 0.1f, 0.22f);
        /// <summary>The murklings' cores: a murkling's amber, a prime's magenta (a lesser one's is its fish's).</summary>
        public static readonly Color MurklingCore = new(1f, 0.42f, 0.16f);
        public static readonly Color PrimeCore = new(1f, 0.2f, 0.66f);

        public static Color Skin(Color c, float amount = 0.8f) => new(c.R, c.G, c.B, amount);
        public static Color Tint(Color c) => new(c.R, c.G, c.B, 1f);
    }

    /// <summary>The depth's darkness (ReefLook.Lantern): the creatures' lights are brighter the darker it is.</summary>
    public static float Lantern = 1f;

    /// <summary>How many of the creatures' lights are lit at once (the nearest to her).</summary>
    const int KinLights = 24;
    readonly List<OmniLight3D> _kinLights = new();
    readonly List<(Vector3 At, Color Color, float Energy, float Range, float Near)> _kinWants = new();

    void Want(Vector3 at, Color color, float energy, float range, System.Numerics.Vector2 her) =>
        _kinWants.Add((at, color, energy, range, (new System.Numerics.Vector2(at.X, at.Z) - her).LengthSquared()));

    void SyncKinLights(PlaneWorld world)
    {
        _kinWants.Clear();
        var her = world.Player.Position;
        float y = LevelMap.SwimBand + 0.4f;
        foreach (var m in world.Murklings)
        {
            if (!m.Alive || m.Budding) continue;
            var core = m.Fish is { } fish ? FishEmber(fish.Kind) : m.Prime ? Kin.PrimeCore : Kin.MurklingCore;
            float energy = m.Prime ? 1.3f : m.Lesser ? 0.3f : 0.55f, range = m.Prime ? 7f : m.Lesser ? 1.8f : 3.2f;
            Want(new Vector3(m.Position.X, y, m.Position.Y), core, energy, range, her);
        }
        foreach (var s in world.MurkShots)
            if (s.Life > 0f && s.Airborne) Want(new Vector3(s.Position.X, y, s.Position.Y), Kin.MurklingCore, 0.35f, 2.2f, her);
        foreach (var r in world.Blightroots)
            if (r.Alive) Want(new Vector3(r.Position.X, SacHeight, r.Position.Y), Kin.Blightroot, 1.1f, 6.5f, her);
        foreach (var b in world.Blotters)
            if (b.Alive && b.Rise <= 0f) Want(new Vector3(b.Position.X, y + 0.8f, b.Position.Y), Kin.Blotter, 1.1f, 6.5f, her);
        foreach (var bomb in world.Bombs) Want(new Vector3(bomb.Position.X, y + 1f, bomb.Position.Y), Kin.Blotter, 0.6f, 3f, her);
        foreach (var t in world.Thumpsquirts)
            if (t.Alive) Want(new Vector3(t.Position.X, y + 0.5f, t.Position.Y), Kin.Thumpsquirt, 0.9f, 5f, her);
        foreach (var w in world.Inkwardens)
            if (w.Alive) Want(new Vector3(w.Position.X, y + 0.6f, w.Position.Y), Kin.Inkwarden, 0.9f, 5f, her);
        foreach (var l in world.Lashjaws)
        {
            if (!l.Alive) continue;
            Want(new Vector3(l.Position.X, y, l.Position.Y), Kin.Lashjaw, 0.9f, 5f, her);
            if (l.TongueOut) Want(new Vector3(l.Tip.X, y, l.Tip.Y), Kin.Lashjaw, 0.45f, 2.5f, her);
        }
        _kinWants.Sort((a, b) => a.Near.CompareTo(b.Near));
        while (_kinLights.Count < KinLights)
        {
            var light = new OmniLight3D { ShadowEnabled = false, OmniAttenuation = 1.2f, LightSpecular = 0.3f, Visible = false };
            AddChild(light);
            _kinLights.Add(light);
        }
        // Brighter the darker the depth (never near her lantern, 2.6 × Lantern^1.8).
        float dark = Mathf.Pow(Mathf.Max(Lantern, 0.6f), 0.8f);
        for (int i = 0; i < _kinLights.Count; i++)
        {
            var light = _kinLights[i];
            light.Visible = i < _kinWants.Count;
            if (!light.Visible) continue;
            var (at, color, energy, range, _) = _kinWants[i];
            light.Position = at;
            light.LightColor = color;
            light.LightEnergy = energy * dark;
            light.OmniRange = range;
        }
    }
}
