using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The adventure on screen (docs/ADVENTURE.md), presentation only: the ink dots floating at her plane (ink_dot.gdshader,
/// one MultiMesh) — a rim dot of a fed patch winding up and snapping back as it splits, the new dot sliding out of it,
/// and violet bracket marks hopping out from the vents to the rim dot by dot; the vents in the walls (a rock lip round a dark
/// throat glowing violet; lit gold by her hits; sealed, it sinks into the wall as a small bump, with a puff of magic
/// particles); dots her light clears go up in magic particles.
/// </summary>
public partial class AdventureView : Node3D
{
    /// <summary>A dot appearing (dividing out of its neighbour, welling from a vent) swells in over this long.</summary>
    const float GrowSeconds = 0.3f;
    /// <summary>A rim dot winds up (swelling WindSwell larger) over WindSeconds before it divides.</summary>
    const float WindSeconds = 0.7f, WindSwell = 0.45f;
    /// <summary>
    /// The brackets hopping out from the vents: HopRate a second from each open vent within HopReach of her, a hop every
    /// HopSeconds (arcing under HopGravity), at most MaxHoppers at once.
    /// </summary>
    const float HopRate = 16f, HopReach = 30f, HopSeconds = 0.2f, HopGravity = 9f, HopSize = 0.21f;
    const int MaxHoppers = 260;
    /// <summary>A sealed vent sinks into the wall over this long.</summary>
    const float SealSeconds = 0.7f;
    /// <summary>A vent's rock lip: its radius at the wall and at the mouth, and how far it stands out of the rock.</summary>
    const float LipBase = 1.0f, LipTop = 0.62f, LipOut = 0.75f, Throat = 0.46f;

    static readonly Color Gold = new(1.25f, 0.85f, 0.38f);
    static readonly Color VentGlow = new(0.5f, 0.16f, 0.9f);

    sealed class VentNode
    {
        public Node3D Root = null!;
        public MeshInstance3D Lip = null!, Throat = null!, Ring = null!, Bump = null!;
        public OmniLight3D Light = null!;
        public StandardMaterial3D ThroatMat = null!, RingMat = null!;
        /// <summary>Seconds since it was sealed (negative: open).</summary>
        public float Shut = -1f;
    }

    MultiMeshInstance3D _dotsView = null!;
    MultiMesh _dots = null!;
    float[] _buffer = System.Array.Empty<float>();
    float[] _born = System.Array.Empty<float>();
    /// <summary>Per cell: the dot it split from (−1: none), and when it last split a dot off (its snap back).</summary>
    int[] _parent = System.Array.Empty<int>();
    float[] _snap = System.Array.Empty<float>();
    MagicParticles _brackets = null!, _magic = null!;
    readonly List<VentNode> _vents = new();
    readonly RandomNumberGenerator _rng = new();
    StandardMaterial3D _rock = null!;
    float _time;

    public override void _Ready()
    {
        _dots = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 14, Rings = 7, Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/ink_dot.gdshader") } },
            InstanceCount = AdventureTuning.MaxDots,
            VisibleInstanceCount = 0,
        };
        _dotsView = new MultiMeshInstance3D
        {
            Multimesh = _dots,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            CustomAabb = new Aabb(new Vector3(-10f, -10f, -10f), new Vector3(LevelMap.Size + 20f, 30f, LevelMap.Size + 20f)),
        };
        AddChild(_dotsView);
        _buffer = new float[AdventureTuning.MaxDots * 12];
        _brackets = new MagicParticles(1200);
        AddChild(_brackets);
        _magic = new MagicParticles(2400);
        AddChild(_magic);
        _rock = new StandardMaterial3D { AlbedoColor = new Color(0.2f, 0.17f, 0.16f), Roughness = 1f };
        _rng.Seed = 7117;
    }

    /// <summary>A new level: its vents; no dots yet.</summary>
    public void Show(PlaneWorld world)
    {
        foreach (var v in _vents) v.Root.QueueFree();
        _vents.Clear();
        _brackets.Clear();
        _magic.Clear();
        _born = new float[InkDots.N * InkDots.N];
        System.Array.Fill(_born, -1f);
        _parent = new int[InkDots.N * InkDots.N];
        _snap = new float[InkDots.N * InkDots.N];
        System.Array.Fill(_snap, -10f);
        _hoppers.Clear();
        _dots.VisibleInstanceCount = 0;
        Visible = world.Dots is not null;
        foreach (var v in world.Vents) _vents.Add(MakeVent(v));
    }

    VentNode MakeVent(PlaneVent v)
    {
        var f = new Vector3(v.Facing.X, 0f, v.Facing.Y).Normalized();
        var x = Vector3.Up.Cross(f).Normalized();
        var basis = new Basis(x, f, x.Cross(f));
        var root = new Node3D { Position = new Vector3(v.Wall.X, LevelMap.SwimBand, v.Wall.Y) - f * 0.25f, Basis = basis };
        AddChild(root);
        // The lip: a short cone of rock standing out of the wall, its mouth toward the water.
        var lip = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = LipTop, BottomRadius = LipBase, Height = LipOut, RadialSegments = 20, Rings = 1 },
            MaterialOverride = _rock, Position = new Vector3(0f, LipOut * 0.5f, 0f),
        };
        root.AddChild(lip);
        var throatMat = new StandardMaterial3D { AlbedoColor = new Color(0.02f, 0.01f, 0.03f), EmissionEnabled = true, Emission = VentGlow, EmissionEnergyMultiplier = 1.4f, Roughness = 1f };
        var throat = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = Throat, BottomRadius = Throat * 0.6f, Height = 0.06f, RadialSegments = 20, Rings = 1 },
            MaterialOverride = throatMat, Position = new Vector3(0f, LipOut + 0.005f, 0f),
        };
        root.AddChild(throat);
        var ringMat = new StandardMaterial3D { AlbedoColor = Colors.Black, EmissionEnabled = true, Emission = Gold, EmissionEnergyMultiplier = 0f, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
        var ring = new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = Throat + 0.02f, OuterRadius = Throat + 0.11f, Rings = 24, RingSegments = 8 },
            MaterialOverride = ringMat, Position = new Vector3(0f, LipOut + 0.02f, 0f), Visible = false,
        };
        root.AddChild(ring);
        // What is left once it is sealed: a small rounded bump on the wall.
        var bump = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 16, Rings = 8 },
            MaterialOverride = _rock, Position = new Vector3(0f, -0.05f, 0f), Scale = Vector3.Zero, Visible = false,
        };
        root.AddChild(bump);
        var light = new OmniLight3D { LightColor = VentGlow, LightEnergy = 1.6f, OmniRange = 4f, ShadowEnabled = false, Position = new Vector3(0f, LipOut + 0.4f, 0f) };
        root.AddChild(light);
        var node = new VentNode { Root = root, Lip = lip, Throat = throat, Ring = ring, Bump = bump, Light = light, ThroatMat = throatMat, RingMat = ringMat };
        if (v.Sealed) node.Shut = 10f;
        return node;
    }

    public void Sync(PlaneWorld world, float dt, Camera3D? camera)
    {
        _time += dt;
        if (world.Dots is not { } dots) return;
        SyncDots(world, dots);
        SyncVents(world, dt);
        if (camera is null) return;
        _brackets.Tick(dt, camera.GlobalBasis);
        _magic.Tick(dt, camera.GlobalBasis);
    }

    void SyncDots(PlaneWorld world, InkDots dots)
    {
        var live = dots.Live;
        int n = System.Math.Min(live.Count, AdventureTuning.MaxDots);
        // Cells that lost their dot forget it (so a new one there swells in again).
        for (int i = 0; i < _born.Length; i++)
            if (_born[i] >= 0f && dots.Ink[i] <= 0f) _born[i] = -1f;
        // New dots first: each split from a neighbour (or welled from a vent).
        foreach (int i in live)
            if (_born[i] < 0f) Born(dots, i);
        for (int k = 0; k < n; k++)
        {
            int i = live[k];
            float age = (_time - _born[i]) / GrowSeconds;
            // Swelling in with a little overshoot, then shrinking as her light wears it down.
            float grow = age >= 1f ? 1f : Mathf.Max(0.05f, 1f + 0.3f * Mathf.Sin(age * Mathf.Pi) - (1f - age) * (1f - age));
            float s = AdventureTuning.DotRadius * grow * (0.45f + 0.55f * dots.Ink[i]) * WindUp(world, dots, i);
            var c = InkDots.Centre(i);
            // Just split off: it slides out of its parent into its own cell.
            if (age < 1f && _parent[i] >= 0)
            {
                float e = 1f - (1f - age) * (1f - age);
                c = System.Numerics.Vector2.Lerp(InkDots.Centre(_parent[i]), c, e);
            }
            int o = k * 12;
            _buffer[o] = s; _buffer[o + 1] = 0f; _buffer[o + 2] = 0f; _buffer[o + 3] = c.X;
            _buffer[o + 4] = 0f; _buffer[o + 5] = s; _buffer[o + 6] = 0f; _buffer[o + 7] = LevelMap.SwimBand;
            _buffer[o + 8] = 0f; _buffer[o + 9] = 0f; _buffer[o + 10] = s; _buffer[o + 11] = c.Y;
        }
        _dots.Buffer = _buffer;
        _dots.VisibleInstanceCount = n;
        Hop(world, dots, (float)GetProcessDeltaTime());
    }

    /// <summary>A dot has just appeared: its parent (the neighbour it split from, if any) snaps back from its wind-up.</summary>
    void Born(InkDots dots, int cell)
    {
        _born[cell] = _time;
        _parent[cell] = -1;
        for (int k = 0; k < 4; k++)
        {
            int j = dots.Neighbour(cell, k);
            if (j < 0 || !dots.Has(j) || _born[j] < 0f || _time - _born[j] < GrowSeconds) continue;
            _parent[cell] = j;
            _snap[j] = _time;
            break;
        }
    }

    /// <summary>
    /// A rim dot of a fed patch winds up before it divides: it swells over WindSeconds, and as it splits it snaps back
    /// (a quick squash past its size and a settle).
    /// </summary>
    float WindUp(PlaneWorld world, InkDots dots, int cell)
    {
        float since = _time - _snap[cell];
        float snap = since < 0.25f ? 1f - 0.18f * Mathf.Sin(since / 0.25f * Mathf.Pi) : 1f;
        if (!dots.Fed[cell] || dots.Ink[cell] < 1f || !Rim(dots, cell)) return snap;
        float t = world.TicksToSpread(cell) * PlaneWorld.Dt;
        if (t >= WindSeconds) return snap;
        float k = 1f - t / WindSeconds;
        // Straining: it swells, trembling a little as it nears the split.
        return snap * (1f + WindSwell * k * k * (1f + 0.08f * Mathf.Sin(_time * 40f + cell)));
    }

    static bool Rim(InkDots dots, int cell)
    {
        for (int k = 0; k < 4; k++)
        {
            int j = dots.Neighbour(cell, k);
            if (j >= 0 && !dots.Has(j)) return true;
        }
        return false;
    }

    // ───────────────────────── the brackets hopping out from the vents ─────────────────────────

    struct Hopper
    {
        public int Cell;
        public float Wait;
    }

    readonly List<Hopper> _hoppers = new();
    float _hopDebt;

    /// <summary>
    /// Through a fed patch, violet brackets hop out from the vents to the rim, dot to dot (each hop one step further
    /// from the vent: InkDots.Hops), a little arc each; at the rim they pop.
    /// </summary>
    void Hop(PlaneWorld world, InkDots dots, float dt)
    {
        var her = world.Player.Position;
        // New ones at the mouths of the open vents near her.
        int vents = 0;
        foreach (var v in world.Vents)
            if (!v.Sealed && System.Numerics.Vector2.Distance(v.Position, her) < HopReach) vents++;
        _mouths.Clear();
        if (vents > 0)
            foreach (int i in dots.Live)
                if (dots.Fed[i] && dots.Hops[i] == 0 && System.Numerics.Vector2.Distance(InkDots.Centre(i), her) < HopReach) _mouths.Add(i);
        _hopDebt = _mouths.Count == 0 ? 0f : Mathf.Min(_hopDebt + HopRate * vents * dt, 6f);
        while (_hopDebt >= 1f && _hoppers.Count < MaxHoppers)
        {
            _hopDebt -= 1f;
            _hoppers.Add(new Hopper { Cell = _mouths[_rng.RandiRange(0, _mouths.Count - 1)], Wait = 0f });
        }
        for (int h = _hoppers.Count - 1; h >= 0; h--)
        {
            var hop = _hoppers[h];
            hop.Wait -= dt;
            if (hop.Wait > 0f)
            {
                _hoppers[h] = hop;
                continue;
            }
            // The next dot out; none (the rim, or the ink gone under it): it pops.
            int from = hop.Cell, next = -1, options = 0;
            if (dots.Has(from) && dots.Fed[from])
                for (int k = 0; k < 4; k++)
                {
                    int j = dots.Neighbour(from, k);
                    if (j < 0 || !dots.Has(j) || !dots.Fed[j] || dots.Hops[j] != dots.Hops[from] + 1) continue;
                    if (_rng.RandiRange(0, options++) == 0) next = j;
                }
            var a = InkDots.Centre(from);
            var at = new Vector3(a.X, LevelMap.SwimBand + 0.1f, a.Y);
            if (next < 0)
            {
                for (int b = 0; b < 2; b++)
                    _brackets.EmitBracket(at, new Vector3(_rng.RandfRange(-0.6f, 0.6f), _rng.RandfRange(0.3f, 0.8f), _rng.RandfRange(-0.6f, 0.6f)), 0.5f, HopSize * 0.8f, color: MagicParticles.BrightViolet);
                _hoppers.RemoveAt(h);
                continue;
            }
            // One hop: an arc onto the next dot (thrown up just enough to land on it as its life ends).
            var to = InkDots.Centre(next);
            var flat = new Vector3(to.X - a.X, 0f, to.Y - a.Y) / HopSeconds;
            _brackets.EmitBracket(at, flat + Vector3.Up * (HopGravity * HopSeconds * 0.5f), HopSeconds, HopSize, HopGravity, 0f, popOut: false,
                color: (hop.Cell & 1) == 0 ? MagicParticles.BrightViolet : MagicParticles.Violet);
            hop.Cell = next;
            hop.Wait = HopSeconds;
            _hoppers[h] = hop;
        }
    }

    readonly List<int> _mouths = new();

    void SyncVents(PlaneWorld world, float dt)
    {
        for (int k = 0; k < _vents.Count && k < world.Vents.Count; k++)
        {
            var v = world.Vents[k];
            var node = _vents[k];
            if (v.Sealed && node.Shut < 0f) node.Shut = 0f;
            if (node.Shut >= 0f)
            {
                // Sealed: its cone sinks back into the wall, the glow going out, until only a small rounded bump is left.
                node.Shut += dt;
                float k01 = Mathf.Clamp(node.Shut / SealSeconds, 0f, 1f);
                float e = k01 * k01 * (3f - 2f * k01);
                float out_ = Mathf.Max(1f - e, 0.001f);
                node.Lip.Scale = new Vector3(1f - 0.3f * e, out_, 1f - 0.3f * e);
                node.Lip.Position = new Vector3(0f, LipOut * 0.5f * out_, 0f);
                node.Lip.Visible = e < 1f;
                node.Throat.Position = new Vector3(0f, LipOut * out_ + 0.005f, 0f);
                node.Throat.Scale = Vector3.One * (1f - e);
                node.Throat.Visible = e < 1f;
                node.ThroatMat.EmissionEnergyMultiplier = 1.4f * (1f - e);
                node.Ring.Visible = false;
                node.Light.LightEnergy = 1.6f * (1f - e);
                node.Light.Visible = e < 1f;
                // The bump swells as the cone goes, with a little settle at the end.
                float settle = 1f + 0.12f * Mathf.Sin(Mathf.Clamp((node.Shut - SealSeconds) / 0.25f, 0f, 1f) * Mathf.Pi);
                node.Bump.Visible = true;
                node.Bump.Scale = new Vector3(0.55f, 0.26f, 0.55f) * Mathf.Max(e, 0.001f) * settle;
                continue;
            }
            // Open: its throat throbs; her hits light the ring round it.
            float throb = 0.75f + 0.25f * Mathf.Sin(_time * 3.2f + k * 1.7f);
            node.ThroatMat.EmissionEnergyMultiplier = 1.4f * throb;
            node.Light.LightEnergy = 1.6f * throb;
            node.Ring.Visible = v.Hits > 0;
            node.RingMat.EmissionEnergyMultiplier = 0.4f + 3f * v.Glow;
        }
    }

    /// <summary>The adventure's events: dots cleared go up in magic particles; a vent sealed puffs them out.</summary>
    public void OnEvent(PlaneWorld world, in PlaneEvent e)
    {
        var at = new Vector3(e.Position.X, LevelMap.SwimBand, e.Position.Y);
        switch (e.Type)
        {
            case PlaneEventType.DotCleared:
                for (int i = 0; i < 3; i++)
                    _magic.Emit(at + new Vector3(_rng.RandfRange(-0.2f, 0.2f), 0f, _rng.RandfRange(-0.2f, 0.2f)),
                        new Vector3(_rng.RandfRange(-0.6f, 0.6f), _rng.RandfRange(0.4f, 1.2f), _rng.RandfRange(-0.6f, 0.6f)), _rng.RandfRange(0.5f, 0.9f), _rng.RandfRange(0.14f, 0.22f));
                break;
            case PlaneEventType.DotEaten:
                for (int i = 0; i < 2; i++)
                    _magic.Emit(at, new Vector3(_rng.RandfRange(-0.5f, 0.5f), _rng.RandfRange(0.3f, 0.9f), _rng.RandfRange(-0.5f, 0.5f)), _rng.RandfRange(0.4f, 0.7f), _rng.RandfRange(0.12f, 0.18f));
                break;
            case PlaneEventType.VentLit:
                _magic.Emit(at + new Vector3(e.Direction.X, 0f, e.Direction.Y) * 0.3f, Vector3.Up * 0.8f, 0.5f, 0.2f);
                break;
            case PlaneEventType.VentSealed:
                // A puff of magic particles: flung out of its mouth all at once, stopping short in the water and
                // drifting up as they pop (its ring of light clearing the ink round it shows in the dots going up).
                var f = new Vector3(e.Direction.X, 0f, e.Direction.Y);
                var side = new Vector3(-f.Z, 0f, f.X);
                for (int i = 0; i < 40; i++)
                {
                    float a = _rng.RandfRange(-1.4f, 1.4f);
                    var dir = (f * Mathf.Cos(a) + side * Mathf.Sin(a) + Vector3.Up * _rng.RandfRange(-0.2f, 0.8f)).Normalized();
                    _magic.Emit(at + f * 0.3f, dir * _rng.RandfRange(3f, 7f) + Vector3.Up * 0.4f, _rng.RandfRange(0.6f, 1.1f), _rng.RandfRange(0.14f, 0.26f), drag: 6f);
                }
                break;
        }
    }
}
