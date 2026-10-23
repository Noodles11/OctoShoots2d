using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Kelp beds (DESIGN-TOPDOWN §4.1 stage 5, LevelMap.Kelp): every strand a thick blade on a chain of Nodes points
/// rooted in the floor, moved by verlet physics each frame — buoyant (the tips most), held to its length, stiff enough
/// not to fold on itself, dragged by the water round it, pushed aside by whatever swims through it, kept out of the
/// floor. The water is the reef's own slow swell (each strand a little out of step, and more at its tip, so it
/// wobbles), what every swimmer stirs (she, the creatures, the fish, her shots: they drag the water along and push it
/// aside round them, the faster the wider), her swells (WaterSwells: a push out from where she set off, turned or
/// dashed) and a current surge through its canyon. Cosmetic: the sim never reads it. Only beds near her move; the
/// rest hold their pose until she comes back.
/// </summary>
public partial class KelpView : Node3D
{
    const int Nodes = 10;
    /// <summary>Buoyancy (m/s², up, from Lift at the root to all of it at the tip) and the water's drag on it (per second, more at the tip).</summary>
    const float Buoyancy = 2.6f, Lift = 0.35f, DragRoot = 2f, DragTip = 3.4f;
    /// <summary>How close two points two apart may come, of their length laid straight (stiff at the root, freer to the tip), and how hard that holds.</summary>
    const float BendRoot = 0.92f, BendTip = 0.45f, BendStiff = 0.3f;
    /// <summary>The reef's slow swell (m/s): a steady drift the beds lean into, swelling along it and across it, each with its own pace.</summary>
    const float Drift = 0.45f, SwellAlong = 0.4f, SwellAcross = 0.35f;
    /// <summary>What a swimmer stirs: the water dragged along with it, and pushed out to its sides, within its radius plus Stir (plus StirPerSpeed a m/s).</summary>
    const float Drag = 0.85f, Push = 0.35f, Stir = 1.2f, StirPerSpeed = 0.15f;
    /// <summary>Beds within this of her move; the blades' half thickness for collisions.</summary>
    const float Awake = 24f, Thick = 0.12f;
    /// <summary>The longest a strand can reach from its root (metres).</summary>
    const float KelpReachMax = TopDownGenerator.KelpLengthMax + 0.5f;

    sealed class Strand
    {
        public Vector3 Root;
        public readonly Vector3[] P = new Vector3[Nodes], Q = new Vector3[Nodes];
        public float Seg, Width, Phase, Twist, Seed, Shade;
        public Vector3 Side;
    }

    sealed class Bed
    {
        public Vector2 Center;
        public float Radius;
        public readonly List<Strand> Strands = new();
    }

    readonly struct Mover
    {
        public readonly Vector3 At, Velocity;
        public readonly float Radius;
        public Mover(Vector3 at, Vector3 velocity, float radius) { At = at; Velocity = velocity; Radius = radius; }
    }

    readonly List<Bed> _beds = new();
    readonly List<Mover> _movers = new(), _near = new(), _local = new();
    /// <summary>Whether any of her swells reaches the strand being stepped.</summary>
    bool _swellHere;
    LevelMap? _map;
    ArrayMesh _mesh = new();
    MeshInstance3D _under = null!, _over = null!;
    ShaderMaterial _material = null!, _overMaterial = null!;
    Vector3[] _verts = System.Array.Empty<Vector3>();
    /// <summary>The blades' points as the vertex buffer holds them (x, y, z floats), streamed to it each frame.</summary>
    float[] _floats = System.Array.Empty<float>();
    byte[] _bytes = System.Array.Empty<byte>();
    Color[] _colors = System.Array.Empty<Color>();
    Vector2[] _uvs = System.Array.Empty<Vector2>();
    int[] _indices = System.Array.Empty<int>();
    /// <summary>Whether the blades' surface is built (then only its points are streamed in).</summary>
    bool _built;
    float _time;

    public override void _Ready()
    {
        _material = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/kelp.gdshader") };
        _overMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/kelp_over.gdshader"), RenderPriority = 3 };
        _under = new MeshInstance3D { Mesh = _mesh, MaterialOverride = _material, CastShadow = GeometryInstance3D.ShadowCastingSetting.On };
        _over = new MeshInstance3D { Mesh = _mesh, MaterialOverride = _overMaterial, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_under);
        AddChild(_over);
    }

    /// <summary>A level's kelp, standing in still water; its colours from the depth's look.</summary>
    public void Build(LevelMap map, ReefLook look)
    {
        _map = map;
        _beds.Clear();
        var rng = new RandomNumberGenerator { Seed = (ulong)map.Seed ^ 0x6B31F00Dul ^ ((ulong)map.Id.Depth << 20) ^ ((ulong)map.Id.Level << 28) };
        foreach (var kb in map.Kelp)
        {
            var bed = new Bed { Center = new Vector2(kb.Center.X, kb.Center.Y), Radius = kb.Radius };
            for (int k = 0; k < kb.Roots.Count; k++)
            {
                var r = kb.Roots[k];
                var root = new Vector3(r.X, map.HeightAt(r) + 0.05f, r.Y);
                var st = new Strand
                {
                    Root = root,
                    Seg = kb.Lengths[k] / (Nodes - 1),
                    Width = rng.RandfRange(0.26f, 0.48f),
                    Phase = rng.RandfRange(0f, Mathf.Tau),
                    Twist = rng.RandfRange(-2.5f, 2.5f),
                    Seed = rng.Randf(),
                    Shade = rng.Randf(),
                };
                float a = rng.RandfRange(0f, Mathf.Tau);
                st.Side = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                // Standing, leaning a little.
                var lean = new Vector3(rng.RandfRange(-0.15f, 0.15f), 1f, rng.RandfRange(-0.15f, 0.15f)).Normalized();
                for (int i = 0; i < Nodes; i++) st.P[i] = st.Q[i] = root + lean * st.Seg * i;
                bed.Strands.Add(st);
            }
            _beds.Add(bed);
        }
        int strands = 0;
        foreach (var b in _beds) strands += b.Strands.Count;
        _verts = new Vector3[strands * Nodes * 2];
        _floats = new float[_verts.Length * 3];
        _bytes = new byte[_floats.Length * 4];
        _colors = new Color[_verts.Length];
        _uvs = new Vector2[_verts.Length];
        // What never changes: each point's place along its blade, and the blade's seed and shade.
        {
            int sIndex = 0;
            foreach (var b in _beds)
            foreach (var st in b.Strands)
            {
                for (int i = 0; i < Nodes; i++)
                {
                    int v = (sIndex * Nodes + i) * 2;
                    var c = new Color(st.Seed, st.Shade, 0f, i / (float)(Nodes - 1));
                    _colors[v] = _colors[v + 1] = c;
                    _uvs[v] = new Vector2(0f, st.Seg * i);
                    _uvs[v + 1] = new Vector2(1f, st.Seg * i);
                }
                sIndex++;
            }
        }
        _mesh.ClearSurfaces();
        _built = false;
        _indices = new int[strands * (Nodes - 1) * 6];
        int n = 0;
        for (int s = 0; s < strands; s++)
        for (int i = 0; i < Nodes - 1; i++)
        {
            int v = (s * Nodes + i) * 2;
            _indices[n++] = v; _indices[n++] = v + 2; _indices[n++] = v + 1;
            _indices[n++] = v + 1; _indices[n++] = v + 2; _indices[n++] = v + 3;
        }
        foreach (var m in new[] { _material, _overMaterial })
        {
            // Olive at the stipe, the depth's grass toward the tip, never brighter than the reef round it.
            m.SetShaderParameter(SN.Of("base_col"), look.Grass.Darkened(0.68f).Lerp(look.Rock, 0.3f));
            m.SetShaderParameter(SN.Of("tip_col"), look.Grass.Lerp(look.Rock, 0.2f).Darkened(0.28f));
        }
        Visible = strands > 0;
        Draw(null);
    }

    public void Clear()
    {
        _beds.Clear();
        _map = null;
        _mesh.ClearSurfaces();
        _built = false;
        Visible = false;
    }

    /// <summary>One frame: the water everything stirs, the strands moved by it, the blades rebuilt.</summary>
    public void Sync(PlaneWorld world, float dt, Vector3 bell, WaterSwells swells, Camera3D? camera)
    {
        if (_map is null || _beds.Count == 0) return;
        dt = Mathf.Min(dt, 1f / 30f);
        _time += dt;
        Gather(world, bell);
        var her = new Vector2(world.Player.Position.X, world.Player.Position.Y);
        // The surge, if one runs: its canyon and its flow now.
        Corridor? canyon = null;
        float surgeSpeed = 0f;
        foreach (var e in world.Director.Active)
            if (e.Kind == ReefEventKind.CurrentSurge)
            {
                canyon = world.Map.Corridors[e.Corridor];
                surgeSpeed = e.Speed * e.Envelope(world.Time) * (e.Reverse ? -1f : 1f);
            }
        const int Steps = 2;
        float h = dt / Steps;
        foreach (var bed in _beds)
        {
            if (bed.Center.DistanceTo(her) > Awake + bed.Radius) continue;
            // Only the swimmers near this bed stir its water.
            _near.Clear();
            foreach (var m in _movers)
                if (new Vector2(m.At.X, m.At.Z).DistanceTo(bed.Center) < bed.Radius + m.Radius + Stir + StirPerSpeed * m.Velocity.Length() + KelpReachMax) _near.Add(m);
            Vector3 surge = Vector3.Zero;
            if (canyon is not null)
            {
                ReefDirector.Nearest(canyon, new System.Numerics.Vector2(bed.Center.X, bed.Center.Y), out float d, out var along);
                float reach = canyon.HalfWidth + ReefDirectorTuning.CosmeticReach;
                if (d < reach + bed.Radius) surge = new Vector3(along.X, 0f, along.Y) * surgeSpeed * 0.7f * (1f - Mathf.Clamp((d - reach) / bed.Radius, 0f, 1f));
            }
            foreach (var st in bed.Strands)
            {
                // Only what can reach this strand stirs it (most strands, most of the time: nothing).
                float reachOf = st.Seg * (Nodes - 1) + 0.5f;
                var root = new Vector2(st.Root.X, st.Root.Z);
                _local.Clear();
                foreach (var m in _near)
                    if (new Vector2(m.At.X, m.At.Z).DistanceTo(root) < reachOf + m.Radius + Stir + StirPerSpeed * m.Velocity.Length()) _local.Add(m);
                _swellHere = false;
                for (int i = 0; i < swells.Count && !_swellHere; i++)
                    _swellHere = new Vector2(swells.Rings[i].X, swells.Rings[i].Y).DistanceTo(root) < reachOf + 4f + 3f * swells.Dirs[i].Z;
                for (int step = 0; step < Steps; step++) Step(st, h, surge, swells);
            }
        }
        Draw(camera);
    }

    /// <summary>
    /// Whether a blade is drawn over her now: any point of a strand near her that lies nearer the camera than her bell
    /// and, on screen, within her bell's radius plus the blade's half width of her (kelp_over draws exactly those).
    /// </summary>
    public bool Covers(Vector3 bell, Camera3D? camera)
    {
        if (camera is null || _beds.Count == 0) return false;
        var her = new Vector2(bell.X, bell.Z);
        var eye = camera.GlobalPosition;
        float herDepth = (bell - eye).Dot(-camera.GlobalBasis.Z);
        var herPx = camera.UnprojectPosition(bell);
        float px = herPx.DistanceTo(camera.UnprojectPosition(bell + camera.GlobalBasis.X));
        float bellPx = BellRadius * px;
        foreach (var bed in _beds)
        {
            if (bed.Center.DistanceTo(her) > bed.Radius + KelpReachMax + 2f) continue;
            foreach (var st in bed.Strands)
            {
                if (new Vector2(st.Root.X, st.Root.Z).DistanceTo(her) > st.Seg * (Nodes - 1) + 2f) continue;
                for (int i = 1; i < Nodes; i++)
                {
                    var p = st.P[i];
                    if ((p - eye).Dot(-camera.GlobalBasis.Z) > herDepth - 0.15f) continue;
                    if (camera.UnprojectPosition(p).DistanceTo(herPx) < bellPx + st.Width * 0.5f * px) return true;
                }
            }
        }
        return false;
    }

    /// <summary>Her bell's radius on screen for <see cref="Covers"/> (metres).</summary>
    const float BellRadius = 0.55f;

    /// <summary>Everything that stirs the water this frame: her, the creatures, the fish, her shots.</summary>
    void Gather(PlaneWorld world, Vector3 bell)
    {
        _movers.Clear();
        Vector3 V(System.Numerics.Vector2 v) => new(v.X, 0f, v.Y);
        Vector3 At(System.Numerics.Vector2 p, float y) => new(p.X, y, p.Y);
        float plane = LevelMap.SwimBand;
        _movers.Add(new Mover(bell, V(world.Player.Velocity), world.Radius));
        foreach (var m in world.Murklings) if (m.Alive && !m.Budding) _movers.Add(new Mover(At(m.Position, plane), V(m.Velocity) + V(m.Knock), m.Body));
        foreach (var b in world.Blotters) if (b.Alive && b.Rise <= 0f) _movers.Add(new Mover(At(b.Position, plane - 0.2f), V(b.Velocity) + V(b.Knock), BlotterTuning.Radius));
        foreach (var w in world.Inkwardens) if (w.Alive) _movers.Add(new Mover(At(w.Position, plane + 0.3f), V(w.Velocity) + V(w.Knock), WardenTuning.Radius));
        foreach (var t in world.Thumpsquirts) if (t.Alive) _movers.Add(new Mover(At(t.Position, plane + 0.2f), V(t.Velocity) + V(t.Knock), ThumpTuning.Body));
        foreach (var m in world.Mobs) if (m.Alive) _movers.Add(new Mover(At(m.Position, plane), V(m.Velocity) + V(m.Knock), m.Radius));
        foreach (var f in world.Fish) _movers.Add(new Mover(At(f.Position, plane), V(f.Velocity), 0.35f));
        foreach (var f in world.ReefFish) _movers.Add(new Mover(At(f.Position, plane), V(f.Velocity), 0.2f));
        foreach (var s in world.Shots)
            if (s.FromPlayer && s.Life > 0f) _movers.Add(new Mover(At(s.Position, plane + 0.7f), V(s.Velocity) * 0.2f, s.Radius));
    }

    /// <summary>A strand's step: its points moved by buoyancy and the water's drag (verlet), then held together and out of what is in the way.</summary>
    void Step(Strand st, float h, Vector3 surge, WaterSwells swells)
    {
        var drift = new Vector3(0.8f, 0f, 0.6f);
        var across = new Vector3(-drift.Z, 0f, drift.X);
        float t = _time;
        // The reef's slow swell where it stands (the bed sways together, a little out of step), and the steady drift.
        var swell = drift * (Drift + SwellAlong * System.MathF.Sin(t * 0.55f + st.Phase * 0.3f + st.Root.X * 0.12f + st.Root.Z * 0.08f)) + surge;
        bool stirred = _local.Count > 0 || _swellHere;
        for (int i = 1; i < Nodes; i++)
        {
            float u = i / (float)(Nodes - 1);
            var p = st.P[i];
            var v = (p - st.Q[i]) / h;
            // Out of step along the strand, so it wobbles, more toward the tip.
            var water = swell
                + across * (SwellAcross * System.MathF.Sin(t * 0.9f + st.Phase * 1.7f + u * 2.4f))
                + new Vector3(System.MathF.Sin(t * 1.4f + st.Phase * 2.3f + u * 4f), 0f, System.MathF.Sin(t * 1.2f + st.Phase * 3.1f + u * 3.3f + 1.57f)) * (0.12f * u);
            if (stirred) water += Stirred(p, swells);
            var acc = Vector3.Up * Buoyancy * (Lift + (1f - Lift) * u) + (water - v) * Mathf.Lerp(DragRoot, DragTip, u);
            var next = p + (p - st.Q[i]) * 0.995f + acc * h * h;
            st.Q[i] = p;
            st.P[i] = next;
        }
        for (int it = 0; it < 3; it++)
        {
            st.P[0] = st.Root;
            for (int i = 1; i < Nodes; i++)
            {
                var d = st.P[i] - st.P[i - 1];
                float len = d.Length();
                if (len < 1e-5f) continue;
                var c = d * ((len - st.Seg) / len);
                if (i == 1) st.P[i] -= c;
                else
                {
                    st.P[i - 1] += c * 0.5f;
                    st.P[i] -= c * 0.5f;
                }
            }
            // Stiff enough not to fold: two points apart never closer than their length laid nearly straight.
            for (int i = 2; i < Nodes; i++)
            {
                float u = i / (float)(Nodes - 1);
                float least = 2f * st.Seg * Mathf.Lerp(BendRoot, BendTip, u);
                var d = st.P[i] - st.P[i - 2];
                float len = d.Length();
                if (len >= least || len < 1e-5f) continue;
                var c = d * ((len - least) / len) * BendStiff;
                if (i - 2 > 0) st.P[i - 2] += c * 0.5f;
                st.P[i] -= c * (i - 2 > 0 ? 0.5f : 1f);
            }
            // Pushed aside by what swims through it (bodies as squat spheres).
            foreach (var m in _local)
            {
                float r = m.Radius + Thick;
                for (int i = 1; i < Nodes; i++)
                {
                    var d = st.P[i] - m.At;
                    d.Y *= 0.7f;
                    float len2 = d.LengthSquared();
                    if (len2 >= r * r || len2 < 1e-8f) continue;
                    float len = Mathf.Sqrt(len2);
                    var out_ = d / len * (r - len);
                    out_.Y /= 0.7f;
                    st.P[i] += out_;
                }
            }
        }
        // Never into the floor (nor into the rock it is pushed against): only what has come down near it is looked at.
        for (int i = 1; i < Nodes; i++)
        {
            if (st.P[i].Y > st.Root.Y + 1.5f) continue;
            float floor = _map!.HeightAt(new System.Numerics.Vector2(st.P[i].X, st.P[i].Z)) + 0.06f;
            if (st.P[i].Y < floor) st.P[i].Y = floor;
        }
    }

    /// <summary>The water every swimmer near stirs at p (m/s), and her swells' push.</summary>
    Vector3 Stirred(Vector3 p, WaterSwells swells)
    {
        var w = Vector3.Zero;
        foreach (var m in _local)
        {
            float speed = m.Velocity.Length();
            float reach = m.Radius + Stir + StirPerSpeed * speed;
            var d = p - m.At;
            d.Y *= 0.6f;
            float dist = d.Length();
            if (dist >= reach) continue;
            float k = 1f - dist / reach;
            k *= k;
            // Dragged along behind it, pushed out to its sides.
            var flat = new Vector3(d.X, 0f, d.Z);
            var outward = flat.LengthSquared() > 1e-6f ? flat.Normalized() : Vector3.Zero;
            w += m.Velocity * Drag * k + outward * speed * Push * k;
        }
        for (int i = 0; i < swells.Count && _swellHere; i++)
        {
            var s = swells.Rings[i];
            var d = new Vector2(p.X - s.X, p.Z - s.Y);
            float r = d.Length();
            if (r < 1e-3f) continue;
            float age = s.Z;
            float reach = swells.Dirs[i].Z > 0f ? swells.Dirs[i].Z : 1f;
            float front = Mathf.Lerp(0.25f, 2.6f * reach, 1f - (1f - age) * (1f - age));
            float width = (0.35f + 0.55f * age) * Mathf.Sqrt(reach);
            float band = Mathf.Exp(-Mathf.Pow((r - front) / width, 2f));
            float k = s.W * band * (1f - age) * (1f - age) * 2.5f;
            w += new Vector3(d.X / r, 0f, d.Y / r) * k;
        }
        return w;
    }

    /// <summary>
    /// The blades: each strand a ribbon turned mostly toward the camera (so it shows its breadth), twisting a little
    /// along it. Built once; after that only the points are streamed into its vertex buffer (the shader turns the
    /// blades' faces to the camera).
    /// </summary>
    void Draw(Camera3D? camera)
    {
        var toCam = camera is not null ? camera.GlobalBasis.Z : new Vector3(0f, 0.87f, 0.5f);
        int s = 0;
        foreach (var bed in _beds)
        foreach (var st in bed.Strands)
        {
            for (int i = 0; i < Nodes; i++)
            {
                float u = i / (float)(Nodes - 1);
                var tangent = (st.P[Mathf.Min(i + 1, Nodes - 1)] - st.P[Mathf.Max(i - 1, 0)]).Normalized();
                var facing = tangent.Cross(toCam);
                if (facing.LengthSquared() < 1e-6f) facing = st.Side;
                facing = facing.Normalized();
                var own = st.Side.Rotated(Vector3.Up, st.Twist * u);
                var side = facing.Lerp(own - tangent * own.Dot(tangent), 0.35f).Normalized();
                // A kelp blade: a thin stipe at the root, widest two thirds up, a rounded tip.
                float width = st.Width * (0.12f + 0.88f * Mathf.SmoothStep(0f, 0.3f, u) * (1f - 0.55f * u * u * u));
                int v = (s * Nodes + i) * 2;
                _verts[v] = st.P[i] - side * width * 0.5f;
                _verts[v + 1] = st.P[i] + side * width * 0.5f;
            }
            s++;
        }
        if (_verts.Length == 0) return;
        if (!_built)
        {
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = _verts;
            arrays[(int)Mesh.ArrayType.Color] = _colors;
            arrays[(int)Mesh.ArrayType.TexUV] = _uvs;
            arrays[(int)Mesh.ArrayType.Index] = _indices;
            _mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            _mesh.CustomAabb = new Aabb(new Vector3(-10f, -12f, -10f), new Vector3(LevelMap.Size + 20f, 30f, LevelMap.Size + 20f));
            _built = true;
            return;
        }
        for (int k = 0; k < _verts.Length; k++)
        {
            _floats[k * 3] = _verts[k].X;
            _floats[k * 3 + 1] = _verts[k].Y;
            _floats[k * 3 + 2] = _verts[k].Z;
        }
        System.Buffer.BlockCopy(_floats, 0, _bytes, 0, _bytes.Length);
        RenderingServer.MeshSurfaceUpdateVertexRegion(_mesh.GetRid(), 0, 0, _bytes);
    }
}
