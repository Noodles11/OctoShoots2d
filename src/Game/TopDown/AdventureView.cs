using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The adventure on screen (docs/ADVENTURE.md), presentation only: the ink dots floating at her plane, each three violet
/// marks twitching between "×" and "+" (ink_dot.gdshader, one MultiMesh) — a rim dot of a fed patch winding up and snapping back as it splits, the new dot sliding out of it,
/// and violet bracket marks hopping out from the vents to the rim dot by dot; the vents in the walls (a jagged gash torn
/// along the wall at her plane, its torn rock lips standing out, plugged by a blister of iridescent ink goo bulging out
/// into the water — vent_goo.gdshader: an oily film over the reef's inside, where a current pushes corruption along the
/// gash — breathing and pushing dark motes of corruption out into the water; her hits dent it and splash goo and magic
/// particles, and light it gold from its edges; sealed, the goo shrinks into it and the gash closes to a scar on a
/// small bump, with a puff of magic particles); dots her light clears go up in magic particles.
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
    /// <summary>A sealed vent's goo shrinks into it and its gash closes over this long.</summary>
    const float SealSeconds = 0.7f;
    /// <summary>
    /// A vent's gash: its length along the wall and its height, how far its goo bulges out into the water, how far its torn
    /// lips reach, and how far up it is turned (out of the wall and up its slope).
    /// </summary>
    const float GashLength = 2.6f, GashHeight = 1.5f, GooBulge = 0.8f, LipReach = 0.35f, GashTilt = 0.9f;
    /// <summary>The motes of corruption its current pushes out (a second, from a vent within HopReach of her).</summary>
    const float OozeRate = 10f;

    static readonly Color VentGlow = new(0.5f, 0.16f, 0.9f);
    /// <summary>The goo's own colours, for its splashes: violet, teal, magenta, gold (dark, as an oil slick's).</summary>
    static readonly Color[] GooColours = { new(0.3f, 0.1f, 0.5f), new(0.08f, 0.32f, 0.36f), new(0.45f, 0.08f, 0.32f), new(0.42f, 0.3f, 0.08f) };

    sealed class VentNode
    {
        public Node3D Root = null!;
        public MeshInstance3D Goo = null!, Lips = null!, Bump = null!;
        /// <summary>Goo running out of its lower edge down the wall: each drip, its length, its beat.</summary>
        public readonly List<(MeshInstance3D Mesh, Vector3 At, float Length, float Width, float Phase)> Drips = new();
        public OmniLight3D Light = null!;
        /// <summary>Seconds since it was sealed (negative: open); since her last hit, and where on the goo it struck (along it, 0..1).</summary>
        public float Shut = -1f, HitAge = 100f, HitAt = 0.5f;
        /// <summary>Motes owed to the water.</summary>
        public float Ooze;
    }

    /// <summary>A dot's marks: how many, how big (metres across, as the ink's particles are), and how far out from its middle.</summary>
    const int Marks = 3;
    const float MarkSize = 0.17f, MarkOut = 0.19f;
    /// <summary>Floats per mark in the buffer: its transform and its seed.</summary>
    const int MarkFloats = 16;

    MultiMeshInstance3D _dotsView = null!;
    MultiMesh _dots = null!;
    float[] _buffer = System.Array.Empty<float>();
    float[] _born = System.Array.Empty<float>();
    /// <summary>Per cell: the dot it split from (−1: none), and when it last split a dot off (its snap back).</summary>
    int[] _parent = System.Array.Empty<int>();
    float[] _snap = System.Array.Empty<float>();
    MagicParticles _brackets = null!, _magic = null!;
    /// <summary>The goo's splashes and the dark motes of corruption (drawn over), and their glints (added).</summary>
    FxParticles _goo = null!, _glints = null!;
    ShaderMaterial _gooMat = null!;
    readonly List<VentNode> _vents = new();
    readonly RandomNumberGenerator _rng = new();
    StandardMaterial3D _rock = null!;
    float _time;

    public override void _Ready()
    {
        _dots = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseCustomData = true,
            Mesh = new QuadMesh { Size = Vector2.One, Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/ink_dot.gdshader") } },
            InstanceCount = AdventureTuning.MaxDots * Marks,
            VisibleInstanceCount = 0,
        };
        _dotsView = new MultiMeshInstance3D
        {
            Multimesh = _dots,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            CustomAabb = new Aabb(new Vector3(-10f, -10f, -10f), new Vector3(LevelMap.Size + 20f, 30f, LevelMap.Size + 20f)),
        };
        AddChild(_dotsView);
        _buffer = new float[AdventureTuning.MaxDots * Marks * MarkFloats];
        _brackets = new MagicParticles(1200);
        AddChild(_brackets);
        _magic = new MagicParticles(2400);
        AddChild(_magic);
        _goo = new FxParticles(900, additive: false);
        AddChild(_goo);
        _glints = new FxParticles(600, additive: true);
        AddChild(_glints);
        _gooMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/vent_goo.gdshader") };
        _gooMat.SetShaderParameter(SN.Of("size"), new Vector2(GashLength, GashHeight));
        _rock = new StandardMaterial3D { AlbedoColor = new Color(0.4f, 0.36f, 0.31f), Roughness = 1f, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        _rng.Seed = 7117;
    }

    /// <summary>A new level: its vents; no dots yet.</summary>
    public void Show(PlaneWorld world)
    {
        foreach (var v in _vents) v.Root.QueueFree();
        _vents.Clear();
        _brackets.Clear();
        _magic.Clear();
        _goo.Clear();
        _glints.Clear();
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
        // Its own frame: X along the wall, Y out of it, Z across it (down the wall). It opens up and out of the wall's
        // slope, so it reads from above whichever way the wall faces.
        var out_ = (f + Vector3.Up * GashTilt).Normalized();
        var basis = new Basis(x, out_, x.Cross(out_));
        var root = new Node3D { Position = new Vector3(v.Wall.X, LevelMap.SwimBand, v.Wall.Y) - f * 0.5f, Basis = basis };
        AddChild(root);
        var rng = new RandomNumberGenerator { Seed = (ulong)(Mathf.Abs(v.Wall.X * 131f + v.Wall.Y * 977f) + 1f) };
        var (gooMesh, lipMesh, lower) = GashMeshes(rng);
        var goo = new MeshInstance3D { Mesh = gooMesh, MaterialOverride = _gooMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        goo.SetInstanceShaderParameter(SN.Of("seed"), rng.RandfRange(0f, 50f));
        root.AddChild(goo);
        var lips = new MeshInstance3D { Mesh = lipMesh, MaterialOverride = _rock };
        root.AddChild(lips);
        // What is left once it is sealed: a small rounded bump on the wall, the closed gash a scar across it.
        var bump = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 16, Rings = 8 },
            MaterialOverride = _rock, Position = new Vector3(0f, -0.05f, 0f), Scale = Vector3.Zero, Visible = false,
        };
        root.AddChild(bump);
        var light = new OmniLight3D { LightColor = VentGlow, LightEnergy = 1.6f, OmniRange = 4f, ShadowEnabled = false, Position = new Vector3(0f, GooBulge + 0.4f, 0f) };
        root.AddChild(light);
        var node = new VentNode { Root = root, Goo = goo, Lips = lips, Bump = bump, Light = light };
        // Goo running out of its lower edge, down the wall: a few drips, each slowly stretching and drawing back.
        int drips = rng.RandiRange(2, 4);
        for (int d = 0; d < drips; d++)
        {
            int i = rng.RandiRange(lower.Length / 5, lower.Length * 4 / 5);
            float u = i / (float)(lower.Length - 1);
            var drip = new MeshInstance3D { Mesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 12, Rings = 8 }, MaterialOverride = _gooMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            drip.SetInstanceShaderParameter(SN.Of("seed"), rng.RandfRange(0f, 50f));
            root.AddChild(drip);
            node.Drips.Add((drip, new Vector3((u - 0.5f) * GashLength, 0.12f, lower[i]), rng.RandfRange(0.25f, 0.55f), rng.RandfRange(0.1f, 0.17f), rng.RandfRange(0f, Mathf.Tau)));
        }
        if (v.Sealed) node.Shut = 10f;
        return node;
    }

    /// <summary>
    /// A gash torn along the wall, pinched at its ends, its top and bottom edges jagged (each vent its own), and the goo
    /// that plugs it: a blister bulging out of it into the water, sagging a little; its torn rock lips stand out along
    /// both edges. In the vent's frame (X along the wall, Y out, Z down). The goo's UV runs along (x) and across (y) it;
    /// its COLOR.r is how far inside its edge a point is.
    /// </summary>
    static (ArrayMesh Goo, ArrayMesh Lips, float[] Lower) GashMeshes(RandomNumberGenerator rng)
    {
        const int Cols = 32, Rows = 14;
        var top = new float[Cols + 1];
        var bottom = new float[Cols + 1];
        var lipTop = new float[Cols + 1];
        var lipBottom = new float[Cols + 1];
        // Smooth wanders along it (torn edges, lumps), with a fine ragged jitter on the edges.
        var wanderTop = Wander(rng, Cols, 5);
        var wanderBottom = Wander(rng, Cols, 5);
        var lump = Wander(rng, Cols, 8);
        for (int i = 0; i <= Cols; i++)
        {
            float s = 2f * i / Cols - 1f;
            float half = 0.5f * GashHeight * Mathf.Pow(Mathf.Max(0f, 1f - s * s), 0.55f);
            // Torn: each edge wanders in and out, with a notch here and there.
            top[i] = half * (1f + 0.45f * wanderTop[i] + rng.RandfRange(-0.06f, 0.06f));
            bottom[i] = half * (1.1f + 0.45f * wanderBottom[i] + rng.RandfRange(-0.06f, 0.06f));
            lipTop[i] = LipReach * rng.RandfRange(0.55f, 1.1f);
            lipBottom[i] = LipReach * rng.RandfRange(0.55f, 1.1f);
        }
        var p = new Vector3[Cols + 1, Rows + 1];
        var inside = new float[Cols + 1, Rows + 1];
        for (int i = 0; i <= Cols; i++)
        {
            float u = i / (float)Cols, s = 2f * u - 1f;
            float taper = Mathf.Sqrt(Mathf.Max(0f, 1f - s * s));
            for (int j = 0; j <= Rows; j++)
            {
                float v = j / (float)Rows;
                float across = 1f - Mathf.Abs(2f * v - 1f);
                float r = Mathf.Pow(across, 0.8f) * taper;
                inside[i, j] = r;
                // The blister: bulging out of the gash, fuller low down, and sagging under its own weight.
                // Lumpy: the goo swells more here than there along the gash.
                float y = GooBulge * Mathf.Pow(r, 0.7f) * (0.9f + 0.2f * v) * (1f + 0.3f * lump[i]);
                float z = Mathf.Lerp(-top[i], bottom[i], v) + 0.16f * r * r;
                p[i, j] = new Vector3((u - 0.5f) * GashLength, y, z);
            }
        }
        var goo = new SurfaceTool();
        goo.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i <= Cols; i++)
            for (int j = 0; j <= Rows; j++)
            {
                var du = p[Mathf.Min(i + 1, Cols), j] - p[Mathf.Max(i - 1, 0), j];
                var dv = p[i, Mathf.Min(j + 1, Rows)] - p[i, Mathf.Max(j - 1, 0)];
                var n = du.Cross(dv);
                n = n.LengthSquared() > 1e-10f ? n.Normalized() : Vector3.Up;
                if (n.Y < 0f) n = -n;
                goo.SetNormal(n);
                goo.SetUV(new Vector2(i / (float)Cols, j / (float)Rows));
                goo.SetColor(new Color(inside[i, j], 0f, 0f));
                goo.AddVertex(p[i, j]);
            }
        Grid(goo, Cols, Rows);

        // The torn rock lips: a ridge along each edge, standing out of the wall at the goo and falling back into it.
        var lips = new SurfaceTool();
        lips.Begin(Mesh.PrimitiveType.Triangles);
        foreach (int side in new[] { -1, 1 })
        {
            var ring = new Vector3[Cols + 1, 3];
            for (int i = 0; i <= Cols; i++)
            {
                float s = 2f * i / Cols - 1f;
                float taper = Mathf.Sqrt(Mathf.Max(0f, 1f - s * s));
                float edge = side < 0 ? -top[i] : bottom[i], reach = side < 0 ? lipTop[i] : lipBottom[i];
                float x = s * 0.5f * GashLength;
                ring[i, 0] = new Vector3(x, 0.05f + 0.12f * taper, edge - side * 0.03f);
                ring[i, 1] = new Vector3(x, 0.08f + 0.1f * taper, edge + side * reach * 0.4f);
                ring[i, 2] = new Vector3(x * 1.08f, -0.06f, edge + side * reach);
            }
            int start = (Cols + 1) * 3 * (side < 0 ? 0 : 1);
            for (int i = 0; i <= Cols; i++)
                for (int j = 0; j < 3; j++)
                {
                    var du = ring[Mathf.Min(i + 1, Cols), j] - ring[Mathf.Max(i - 1, 0), j];
                    var dv = ring[i, Mathf.Min(j + 1, 2)] - ring[i, Mathf.Max(j - 1, 0)];
                    var n = du.Cross(dv);
                    n = n.LengthSquared() > 1e-10f ? n.Normalized() : Vector3.Up;
                    if (n.Y < 0f) n = -n;
                    lips.SetNormal(n);
                    lips.AddVertex(ring[i, j]);
                }
            Grid(lips, Cols, 2, start);
        }
        return (goo.Commit(), lips.Commit(), bottom);
    }

    /// <summary>A smooth wander (−1..1) over cols + 1 points: random values every `every` points, eased between.</summary>
    static float[] Wander(RandomNumberGenerator rng, int cols, int every)
    {
        var keys = new float[cols / every + 2];
        for (int k = 0; k < keys.Length; k++) keys[k] = rng.RandfRange(-1f, 1f);
        var w = new float[cols + 1];
        for (int i = 0; i <= cols; i++)
        {
            int k = i / every;
            float t = (i - k * every) / (float)every;
            w[i] = Mathf.Lerp(keys[k], keys[k + 1], t * t * (3f - 2f * t));
        }
        return w;
    }

    /// <summary>The triangles of a grid of (cols + 1) × (rows + 1) vertices laid column by column, from vertex start.</summary>
    static void Grid(SurfaceTool st, int cols, int rows, int start = 0)
    {
        for (int i = 0; i < cols; i++)
            for (int j = 0; j < rows; j++)
            {
                int k = start + i * (rows + 1) + j, k1 = k + 1, n0 = k + rows + 1, n1 = n0 + 1;
                st.AddIndex(k); st.AddIndex(n0); st.AddIndex(k1);
                st.AddIndex(k1); st.AddIndex(n0); st.AddIndex(n1);
            }
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
        _goo.Tick(dt, camera.GlobalBasis);
        _glints.Tick(dt, camera.GlobalBasis);
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
            // Its marks round its middle, each dot's triangle turned its own way, all sized as the dot is.
            float share = s / AdventureTuning.DotRadius, size = MarkSize * share, turn = i * 2.399963f;
            for (int m = 0; m < Marks; m++)
            {
                float a = turn + m * Mathf.Tau / Marks;
                int o = (k * Marks + m) * MarkFloats;
                _buffer[o] = size; _buffer[o + 1] = 0f; _buffer[o + 2] = 0f; _buffer[o + 3] = c.X + Mathf.Cos(a) * MarkOut * share;
                _buffer[o + 4] = 0f; _buffer[o + 5] = size; _buffer[o + 6] = 0f; _buffer[o + 7] = LevelMap.SwimBand;
                _buffer[o + 8] = 0f; _buffer[o + 9] = 0f; _buffer[o + 10] = size; _buffer[o + 11] = c.Y + Mathf.Sin(a) * MarkOut * share;
                _buffer[o + 12] = ((i * Marks + m) * 0.7548777f) % 1f;
            }
        }
        _dots.Buffer = _buffer;
        _dots.VisibleInstanceCount = n * Marks;
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
        var her = world.Player.Position;
        for (int k = 0; k < _vents.Count && k < world.Vents.Count; k++)
        {
            var v = world.Vents[k];
            var node = _vents[k];
            node.HitAge += dt;
            node.Goo.SetInstanceShaderParameter(SN.Of("hit"), new Vector3(node.HitAt, 0.45f, node.HitAge));
            if (v.Sealed && node.Shut < 0f) node.Shut = 0f;
            if (node.Shut >= 0f)
            {
                // Sealed: its goo shrinks back into it and the gash closes to a scar, the glow going out, on a small
                // rounded bump.
                node.Shut += dt;
                float k01 = Mathf.Clamp(node.Shut / SealSeconds, 0f, 1f);
                float e = k01 * k01 * (3f - 2f * k01);
                node.Goo.Scale = new Vector3(1f - 0.5f * e, Mathf.Max(1f - e, 0.001f), Mathf.Max(1f - 0.85f * e, 0.001f));
                node.Goo.Visible = e < 1f;
                node.Goo.SetInstanceShaderParameter(SN.Of("shut"), e);
                node.Lips.Scale = new Vector3(1f, 1f - 0.5f * e, 1f - 0.8f * e);
                node.Light.LightEnergy = 1.6f * (1f - e);
                node.Light.Visible = e < 1f;
                foreach (var drip in node.Drips) drip.Mesh.Visible = false;
                // The bump swells as the gash closes, with a little settle at the end.
                float settle = 1f + 0.12f * Mathf.Sin(Mathf.Clamp((node.Shut - SealSeconds) / 0.25f, 0f, 1f) * Mathf.Pi);
                node.Bump.Visible = true;
                node.Bump.Scale = new Vector3(0.75f, 0.22f, 0.45f) * Mathf.Max(e, 0.001f) * settle;
                continue;
            }
            // Open: its glow throbs; her hits light it gold from its edges; its drips stretch and draw back.
            float throb = 0.75f + 0.25f * Mathf.Sin(_time * 3.2f + k * 1.7f);
            foreach (var (mesh, dripAt, length, width, phase) in node.Drips)
            {
                float l = length * (1f + 0.3f * Mathf.Sin(_time * 0.7f + phase));
                mesh.Position = dripAt + new Vector3(0f, 0f, l * 0.75f);
                mesh.Scale = new Vector3(width, width * 0.8f, l);
            }
            node.Light.LightEnergy = 1.6f * throb;
            node.Goo.SetInstanceShaderParameter(SN.Of("lit"), v.Hits > 0 ? 0.15f + 0.85f * v.Glow : 0f);
            // The current pushes motes of corruption out through the goo into the water.
            if (System.Numerics.Vector2.Distance(v.Position, her) > HopReach) continue;
            node.Ooze += dt * OozeRate;
            while (node.Ooze >= 1f)
            {
                node.Ooze -= 1f;
                float along = _rng.RandfRange(-0.4f, 0.4f);
                var from = GooPoint(node, 0.5f + along, _rng.RandfRange(0.5f, 0.9f));
                var out_ = node.Root.GlobalBasis.Y.Normalized();
                var drift = out_ * _rng.RandfRange(0.5f, 1.3f) + node.Root.GlobalBasis.X.Normalized() * along * 0.8f + Vector3.Up * _rng.RandfRange(-0.1f, 0.25f);
                _goo.Emit(from, drift, _rng.RandfRange(1.2f, 2.2f), _rng.RandfRange(0.05f, 0.1f), 0.02f, new Color(0.03f, 0.01f, 0.06f, 0.9f), new Color(0.2f, 0.05f, 0.35f, 0f), drag: 1.2f);
                if (_rng.Randf() < 0.35f)
                    _glints.Emit(from, drift * 0.8f, _rng.RandfRange(0.8f, 1.4f), _rng.RandfRange(0.03f, 0.06f), 0.01f, new Color(0.7f, 0.25f, 1.1f), new Color(0.3f, 0.1f, 0.6f, 0f), drag: 1.2f);
            }
        }
    }

    /// <summary>A point on a vent's goo (along: 0..1 along the gash; out: how far out of the wall, a share of its bulge), in the world.</summary>
    Vector3 GooPoint(VentNode node, float along, float out_)
    {
        float s = 2f * along - 1f;
        float taper = Mathf.Sqrt(Mathf.Max(0f, 1f - s * s));
        return node.Root.ToGlobal(new Vector3(s * 0.5f * GashLength, GooBulge * Mathf.Pow(taper, 0.7f) * out_, 0.1f));
    }

    /// <summary>Goo flung off a vent: dark drops in its oily colours, flying out and sinking, a few glints with them.</summary>
    void Splash(VentNode node, Vector3 at, int count, float speed)
    {
        var out_ = node.Root.GlobalBasis.Y.Normalized();
        var side = node.Root.GlobalBasis.X.Normalized();
        for (int i = 0; i < count; i++)
        {
            var c = GooColours[_rng.RandiRange(0, GooColours.Length - 1)];
            var v = out_ * _rng.RandfRange(0.4f, 1f) * speed + side * _rng.RandfRange(-0.6f, 0.6f) * speed + Vector3.Up * _rng.RandfRange(0.2f, 0.9f) * speed;
            _goo.Emit(at, v, _rng.RandfRange(0.6f, 1.1f), _rng.RandfRange(0.09f, 0.2f), 0.03f, c with { A = 0.95f }, c.Darkened(0.5f) with { A = 0f }, drag: 2.2f, buoyancy: -1.6f);
            if (i % 2 == 0)
                _glints.Emit(at, v * 0.9f, _rng.RandfRange(0.35f, 0.7f), _rng.RandfRange(0.05f, 0.09f), 0.01f, c.Lightened(0.6f) * 2f, c with { A = 0f }, drag: 2.2f, buoyancy: -1.6f);
        }
    }

    /// <summary>The vent at a mouth (an event's), or null.</summary>
    VentNode? VentAt(PlaneWorld world, System.Numerics.Vector2 mouth)
    {
        for (int k = 0; k < _vents.Count && k < world.Vents.Count; k++)
            if (System.Numerics.Vector2.Distance(world.Vents[k].Position, mouth) < 0.01f) return _vents[k];
        return null;
    }

    /// <summary>Her light's steam where it struck (CombatView.Steam): the place, the way the light went, the body's radius, how hard.</summary>
    public System.Action<Vector3, Vector3, float, float>? Steam;
    /// <summary>A vent's width for the steam, how hard a hit on it is, and how hard its sealing.</summary>
    const float VentSteamRadius = 0.6f, VentSteamHit = 0.3f, VentSteamSealed = 1f;

    /// <summary>The adventure's events: dots cleared (or decayed on their own) go up in magic particles; a vent sealed puffs them out.</summary>
    public void OnEvent(PlaneWorld world, in PlaneEvent e)
    {
        var at = new Vector3(e.Position.X, LevelMap.SwimBand, e.Position.Y);
        switch (e.Type)
        {
            case PlaneEventType.DotCleared:
            case PlaneEventType.DotDecayed:
                for (int i = 0; i < 3; i++)
                    _magic.Emit(at + new Vector3(_rng.RandfRange(-0.2f, 0.2f), 0f, _rng.RandfRange(-0.2f, 0.2f)),
                        new Vector3(_rng.RandfRange(-0.6f, 0.6f), _rng.RandfRange(0.4f, 1.2f), _rng.RandfRange(-0.6f, 0.6f)), _rng.RandfRange(0.5f, 0.9f), _rng.RandfRange(0.14f, 0.22f));
                break;
            case PlaneEventType.DotEaten:
                for (int i = 0; i < 2; i++)
                    _magic.Emit(at, new Vector3(_rng.RandfRange(-0.5f, 0.5f), _rng.RandfRange(0.3f, 0.9f), _rng.RandfRange(-0.5f, 0.5f)), _rng.RandfRange(0.4f, 0.7f), _rng.RandfRange(0.12f, 0.18f));
                break;
            case PlaneEventType.VentLit:
            {
                // Struck: its goo dents where her light met it (the side she shot from) and rings out; goo splashes
                // off it and magic particles fly.
                if (VentAt(world, e.Position) is not { } node) break;
                var from = node.Root.ToLocal(new Vector3(world.Player.Position.X, LevelMap.SwimBand, world.Player.Position.Y));
                node.HitAt = Mathf.Clamp(from.X / GashLength + 0.5f, 0.15f, 0.85f);
                node.HitAge = 0f;
                var hitAt = GooPoint(node, node.HitAt, 1f);
                Splash(node, hitAt, 16, 2.8f);
                var f = new Vector3(e.Direction.X, 0f, e.Direction.Y);
                Steam?.Invoke(hitAt + f * 0.4f, -f, VentSteamRadius, VentSteamHit);
                for (int i = 0; i < 7; i++)
                    _magic.Emit(hitAt, (f * _rng.RandfRange(0.8f, 2.2f) + new Vector3(_rng.RandfRange(-1f, 1f), _rng.RandfRange(0.4f, 1.4f), _rng.RandfRange(-1f, 1f))), _rng.RandfRange(0.45f, 0.8f), _rng.RandfRange(0.14f, 0.22f), drag: 3f);
                break;
            }
            case PlaneEventType.VentSealed:
            {
                // A puff of magic particles: flung out of its mouth all at once, stopping short in the water and
                // drifting up as they pop (its ring of light clearing the ink round it shows in the dots going up);
                // the last of its goo bursts off it.
                if (VentAt(world, e.Position) is { } sealedNode) Splash(sealedNode, GooPoint(sealedNode, 0.5f, 0.8f), 30, 3.5f);
                var f = new Vector3(e.Direction.X, 0f, e.Direction.Y);
                var side = new Vector3(-f.Z, 0f, f.X);
                Steam?.Invoke(at + f * 0.6f, -f, VentSteamRadius, VentSteamSealed);
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
}
