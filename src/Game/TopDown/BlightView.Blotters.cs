using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Blotters on screen (docs/CORRUPTION.md): hermit crabs the corruption took over. A huge round shell of ink like a
/// bao bun, its pleats twisting up into a knot on top, carried by a lobster-red crab far too small for it (eye stalks tipped with
/// amber, one big claw and one small one, four legs scuttling at its front). The shell bobs as it walks and ink beads
/// off it. Before each bomb it winds up (squashing, swelling, its knot twisting) and pushes the bomb out of its top
/// while the crab keeps walking. It rises out of the floor when born. Freed, the shell pops like a murkling, and the
/// bare crab, its soft pink tail showing, does a double take and scuttles off the screen, where it is gone.
/// </summary>
public partial class BlightView
{
    /// <summary>The shell winds up over the last this-many seconds before a bomb, and springs back over Release after.</summary>
    const float WindUp = 0.6f, Release = 0.35f;
    /// <summary>The knot on top of the shell, where its bombs come out.</summary>
    const float ShellTop = LevelMap.SwimBand + 0.6f;
    /// <summary>The shell's mesh spans this far below and above its middle (in its own units).</summary>
    const float ShellBelow = 0.37f, ShellAbove = 1.04f;
    /// <summary>The freed crab: its double take, its scuttle (m/s), and how long before it is gone even if still on screen.</summary>
    const float DoubleTake = 0.6f, ScuttleSpeed = 7f, ScuttleMax = 7f;
    /// <summary>The crab's colours: lobster red (the corruption took its shell, not its colour), its soft tail pink.</summary>
    static readonly Color CrabRed = new(0.86f, 0.13f, 0.05f), CrabPink = new(1f, 0.6f, 0.58f);
    /// <summary>How big the crab is drawn (its parts are modelled about a third of a metre across).</summary>
    const float CrabScale = 2f;
    StandardMaterial3D? _crabShell, _crabTail;

    sealed class Crab
    {
        public Node3D Node = null!;
        public MeshInstance3D Body = null!, Tail = null!, BigClaw = null!, SmallClaw = null!;
        public MeshInstance3D[] Stalks = null!, Eyes = null!, Legs = null!;
    }

    sealed class Hermit
    {
        public Node3D Node = null!, Pivot = null!;
        public MeshInstance3D Shell = null!;
        public Crab Crab = null!;
        public System.Numerics.Vector2 Last;
        public float Walk, Drip, LastBomb;
    }

    /// <summary>A freed crab running off the screen.</summary>
    sealed class Runaway
    {
        public Crab Crab = null!;
        public System.Numerics.Vector2 At, Dir;
        public float Since, Walk, Gone = -1f, Yaw;
    }

    readonly Dictionary<Blotter, Hermit> _hermits = new();
    readonly List<Runaway> _runaways = new();
    readonly Dictionary<CorruptionBomb, (MeshInstance3D Ball, MeshInstance3D Mark)> _bombs = new();
    ArrayMesh? _shellMesh;
    readonly CylinderMesh _strut = new() { TopRadius = 0.025f, BottomRadius = 0.045f, Height = 1f, RadialSegments = 6, Rings = 1 };

    /// <summary>
    /// Corruption bombs: a wobbling ink ball arcing from a Blotter's shell, and on the ground where it will land a dark
    /// ring that tightens and darkens as it falls (the warning to get out).
    /// </summary>
    void SyncBombs(PlaneWorld world)
    {
        var live = new HashSet<CorruptionBomb>(world.Bombs);
        var gone = new List<CorruptionBomb>();
        foreach (var (bomb, view) in _bombs)
            if (!live.Contains(bomb))
            {
                view.Ball.QueueFree();
                view.Mark.QueueFree();
                gone.Add(bomb);
            }
        foreach (var bomb in gone) _bombs.Remove(bomb);
        foreach (var bomb in world.Bombs)
        {
            if (!_bombs.TryGetValue(bomb, out var view))
            {
                var ball = new MeshInstance3D { Mesh = _sphere, MaterialOverride = _orb, Scale = Vector3.One * 0.32f, CastShadow = GeometryInstance3D.ShadowCastingSetting.On };
                ball.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("ember"), 0.08f);
                ball.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("wobble"), 1.4f);
                ball.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("seed"), bomb.From.X * 0.3f);
                ball.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("skin"), Kin.Skin(Kin.Blotter, 0.85f));
                ball.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("ember_tint"), Kin.Tint(Kin.Blotter));
                var mark = new MeshInstance3D { Mesh = _quad, MaterialOverride = _ring, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
                mark.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("seed"), bomb.To.Y * 0.2f);
                AddChild(ball);
                AddChild(mark);
                _bombs[bomb] = view = (ball, mark);
            }
            float t = bomb.T;
            var at = bomb.Position;
            // A Blotter's comes out of the knot on top of its shell; a boss's from higher up.
            float from = bomb.FromBoss ? LevelMap.SwimBand + 1.2f : ShellTop, to = Floor(bomb.To) + 0.2f;
            // A lob: up from the shell and down onto the ground.
            float y = Mathf.Lerp(from, to, t) + 3.2f * 4f * t * (1f - t);
            view.Ball.Position = new Vector3(at.X, y, at.Y);
            // Pushed out of the knot: it swells to its size as it leaves.
            view.Ball.Scale = Vector3.One * 0.32f * Mathf.Min(1f, 0.4f + t * 6f);
            float r = BlotterTuning.SplashRadius * (1.25f - 0.25f * t);
            view.Mark.Position = new Vector3(bomb.To.X, Floor(bomb.To) + 0.07f, bomb.To.Y);
            view.Mark.Scale = new Vector3(r, 1f, r);
            view.Mark.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("tint"), new Color(0.45f, 0.18f, 0.02f, 0.35f + 0.5f * t));
            view.Mark.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("width"), 0.12f + 0.1f * t);
        }
    }

    void ClearBombs()
    {
        foreach (var (ball, mark) in _bombs.Values)
        {
            ball.QueueFree();
            mark.QueueFree();
        }
        _bombs.Clear();
    }

    void ClearHermits()
    {
        foreach (var h in _hermits.Values) h.Node.QueueFree();
        _hermits.Clear();
        foreach (var r in _runaways) r.Crab.Node.QueueFree();
        _runaways.Clear();
    }

    /// <summary>
    /// The shell: a round bun of ink, flat underneath, its pleats twisting up into a pinched knot on top (a bao, or a
    /// Bulbasaur's bulb). Unit-sized round its middle; ShellBelow down to ShellAbove up.
    /// </summary>
    static ArrayMesh ShellMesh()
    {
        const int rows = 32, cols = 72;
        var grid = new Vector3[rows + 1, cols + 1];
        for (int i = 0; i <= rows; i++)
        {
            float th = i * Mathf.Pi / rows;
            float up = Mathf.Cos(th), round = Mathf.Sin(th);
            // 1 at the top, 0 from a little below its widest.
            float top = Mathf.Clamp(1f - th / (0.62f * Mathf.Pi), 0f, 1f);
            float twist = 1.9f * top * top;
            // Bunched into the knot: narrower and lifted at the very top.
            float pinch = 1f - 0.35f * Mathf.SmoothStep(0.55f, 1f, top);
            float knot = 0.32f * Mathf.Exp(-(th / 0.26f) * (th / 0.26f));
            for (int j = 0; j <= cols; j++)
            {
                float ph = j * Mathf.Tau / cols;
                // Soft pleats like a bun's folds, swirling up from its waist into the knot: rounded out at its sides,
                // raised as ridges over its top (so they show from above).
                float fold = Mathf.Sin(9f * (ph + twist));
                float pleat = 1f + 0.06f * fold * Mathf.SmoothStep(0f, 0.45f, top);
                float ridge = 0.07f * fold * Mathf.SmoothStep(0.15f, 0.7f, top) * (1f - Mathf.SmoothStep(0.85f, 1f, top));
                float y = (up < 0f ? up * 0.45f : up * 0.82f) + knot + ridge;
                grid[i, j] = new Vector3(round * pleat * pinch * Mathf.Cos(ph), y, round * pleat * pinch * Mathf.Sin(ph));
            }
        }
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        void Tri(Vector3 a, Vector3 b, Vector3 c)
        {
            // Wound to face outward (the shell is round about its middle): Godot's front faces wind clockwise, so
            // the cross of their edges points in.
            if ((b - a).Cross(c - a).Dot(a + b + c) > 0f) (b, c) = (c, b);
            st.AddVertex(a);
            st.AddVertex(b);
            st.AddVertex(c);
        }
        for (int i = 0; i < rows; i++)
            for (int j = 0; j < cols; j++)
            {
                var a = grid[i, j]; var b = grid[i + 1, j]; var c = grid[i + 1, j + 1]; var d = grid[i, j + 1];
                if (i > 0) Tri(a, b, d);
                if (i < rows - 1) Tri(d, b, c);
            }
        st.GenerateNormals();
        return st.Commit();
    }

    MeshInstance3D Part(Node3D parent, Mesh mesh, Color skin, float ember = 0f, float seed = 0f, ShaderMaterial? material = null)
    {
        var m = new MeshInstance3D { Mesh = mesh, MaterialOverride = material ?? _orb, CastShadow = GeometryInstance3D.ShadowCastingSetting.On };
        m.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("ember"), ember);
        m.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("wobble"), 0f);
        m.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("seed"), seed);
        m.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("skin"), skin);
        parent.AddChild(m);
        return m;
    }

    /// <summary>One of the crab's own parts: lobster red (or pink), solid, not ink.</summary>
    MeshInstance3D CrabPart(Node3D parent, Mesh mesh, bool tail = false)
    {
        _crabShell ??= new StandardMaterial3D { AlbedoColor = CrabRed, Roughness = 0.45f, EmissionEnabled = true, Emission = CrabRed, EmissionEnergyMultiplier = 0.35f, RimEnabled = true, Rim = 0.4f };
        _crabTail ??= new StandardMaterial3D { AlbedoColor = CrabPink, Roughness = 0.6f, EmissionEnabled = true, Emission = CrabPink, EmissionEnergyMultiplier = 0.25f };
        var m = new MeshInstance3D { Mesh = mesh, MaterialOverride = tail ? _crabTail : _crabShell, CastShadow = GeometryInstance3D.ShadowCastingSetting.On };
        parent.AddChild(m);
        return m;
    }

    /// <summary>The crab (forward +Z, standing on y 0): a little body, its tail tucked away in the shell, its claws, stalks and legs.</summary>
    Crab MakeCrab(Node3D parent, float seed)
    {
        var node = new Node3D { Scale = Vector3.One * CrabScale };
        parent.AddChild(node);
        var crab = new Crab { Node = node };
        crab.Body = CrabPart(node, _sphere);
        crab.Body.Position = new Vector3(0f, 0.3f, 0.02f);
        crab.Body.Scale = new Vector3(0.3f, 0.19f, 0.25f);
        crab.Tail = CrabPart(node, _sphere, tail: true);
        crab.Tail.Position = new Vector3(0f, 0.27f, -0.32f);
        crab.Tail.Scale = new Vector3(0.2f, 0.17f, 0.3f);
        crab.Tail.Visible = false;
        crab.BigClaw = CrabPart(node, _sphere);
        crab.BigClaw.Scale = new Vector3(0.17f, 0.13f, 0.27f);
        crab.SmallClaw = CrabPart(node, _sphere);
        crab.SmallClaw.Scale = new Vector3(0.08f, 0.07f, 0.13f);
        crab.Stalks = new MeshInstance3D[2];
        crab.Eyes = new MeshInstance3D[2];
        for (int i = 0; i < 2; i++)
        {
            crab.Stalks[i] = CrabPart(node, _strut);
            crab.Eyes[i] = Part(node, _sphere, new Color(0f, 0f, 0f, 0f), 1.2f, seed, _sacMat);
            crab.Eyes[i].Scale = Vector3.One * 0.065f;
            crab.Eyes[i].SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("ember_tint"), Kin.Tint(new Color(1f, 0.75f, 0.2f)));
        }
        // Four legs, two each side, each an upper and a lower strut.
        crab.Legs = new MeshInstance3D[8];
        for (int i = 0; i < 8; i++) crab.Legs[i] = CrabPart(node, _strut);
        return crab;
    }

    /// <summary>A strut (the shared unit cylinder) from a to b, in its parent's space.</summary>
    static void Strut(MeshInstance3D m, Vector3 a, Vector3 b)
    {
        var d = b - a;
        float len = d.Length();
        if (len < 1e-4f) return;
        m.Position = (a + b) * 0.5f;
        m.Basis = new Basis(new Quaternion(Vector3.Up, d / len)) * Basis.FromScale(new Vector3(1f, len, 1f));
    }

    /// <summary>
    /// Poses the crab: its legs stepping by walk (radians of gait), its claws held up (the big one raised by brandish),
    /// its eye stalks swaying (stretched by alarm, 0..1).
    /// </summary>
    void PoseCrab(Crab crab, float walk, float seed, float alarm = 0f, float brandish = 0f)
    {
        for (int i = 0; i < 4; i++)
        {
            float side = i < 2 ? 1f : -1f;
            float z0 = (i % 2 == 0 ? 0.1f : -0.08f);
            // Alternate legs step together: a scuttle.
            float ph = walk + (i % 2 == 0 ? 0f : Mathf.Pi) + (side > 0f ? 0f : Mathf.Pi);
            var hip = new Vector3(side * 0.24f, 0.29f, z0);
            var foot = new Vector3(side * 0.62f, 0.07f * Mathf.Max(0f, Mathf.Sin(ph + Mathf.Pi * 0.5f)), z0 + 0.13f * Mathf.Sin(ph) + (i % 2 == 0 ? 0.1f : -0.08f));
            var knee = new Vector3(side * 0.5f, 0.5f, (hip.Z + foot.Z) * 0.5f);
            Strut(crab.Legs[i * 2], hip, knee);
            Strut(crab.Legs[i * 2 + 1], knee, foot);
        }
        // The big claw on its right, the small on its left, held up before it.
        float lift = 0.05f * Mathf.Sin(walk * 0.5f + seed) + 0.25f * brandish;
        crab.BigClaw.Position = new Vector3(-0.27f, 0.33f + lift, 0.36f);
        crab.BigClaw.Rotation = new Vector3(-0.35f - brandish, -0.25f, 0f);
        crab.SmallClaw.Position = new Vector3(0.2f, 0.3f, 0.32f + 0.03f * Mathf.Sin(walk + seed));
        for (int i = 0; i < 2; i++)
        {
            float s = i == 0 ? 1f : -1f;
            float sway = Mathf.Sin(_time * 2.6f + seed + i * 1.7f) * 0.05f;
            var root = new Vector3(s * 0.08f, 0.42f, 0.18f);
            var tip = root + new Vector3(s * (0.05f + 0.06f * alarm) + sway, 0.3f + 0.22f * alarm, 0.08f - 0.05f * alarm);
            Strut(crab.Stalks[i], root, tip);
            crab.Eyes[i].Position = tip;
        }
    }

    Hermit MakeHermit(Blotter b)
    {
        var node = new Node3D();
        AddChild(node);
        var pivot = new Node3D();
        node.AddChild(pivot);
        _shellMesh ??= ShellMesh();
        var shell = Part(pivot, _shellMesh, Kin.Skin(Kin.Blotter, 0.75f), 0.12f, b.Seed);
        shell.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("wobble"), 0.35f);
        var crab = MakeCrab(node, b.Seed);
        return new Hermit { Node = node, Pivot = pivot, Shell = shell, Crab = crab, Last = b.Position, LastBomb = b.BombTimer };
    }

    void SyncHermits(PlaneWorld world, float dt)
    {
        var alive = new HashSet<Blotter>(world.Blotters);
        var gone = new List<Blotter>();
        foreach (var (b, h) in _hermits)
            if (!alive.Contains(b))
            {
                h.Node.QueueFree();
                gone.Add(b);
            }
        foreach (var b in gone) _hermits.Remove(b);

        foreach (var b in world.Blotters)
        {
            if (!_hermits.TryGetValue(b, out var h)) _hermits[b] = h = MakeHermit(b);
            // Rising out of the floor when born.
            float rise = b.Rise > 0f ? 1f - Mathf.Clamp(b.Rise / 1.5f, 0f, 1f) : 1f;
            var at = b.Position;
            float floor = Floor(at);
            h.Walk += System.Numerics.Vector2.Distance(h.Last, at) * 5.5f;
            h.Last = at;
            h.Node.Position = new Vector3(at.X, 0f, at.Y);
            h.Node.Rotation = new Vector3(0f, Mathf.Atan2(b.Heading.X, b.Heading.Y), 0f);

            // The shell: from just off the floor (the crab carries it) up to its knot.
            float bottom = floor + 0.22f;
            float sy = Mathf.Max((ShellTop - bottom) / (ShellBelow + ShellAbove), 0.85f);
            float sx = BlotterTuning.ShellRadius * 0.92f;
            float twist = 0f, swell = 0f;
            // Winding up before a bomb: it squashes, swells, its knot twists; then springs up as the bomb leaves.
            if (b.BombTimer < WindUp)
            {
                float k = 1f - b.BombTimer / WindUp;
                float k2 = k * k;
                sy *= 1f - 0.2f * k2;
                sx *= 1f + 0.08f * k2;
                swell = 0.7f * k;
                twist = 1.5f * k2;
            }
            float since = BlotterTuning.BombEvery - b.BombTimer;
            if (since < Release)
            {
                float k = 1f - since / Release;
                sy *= 1f + 0.24f * k * k;
                sx *= 1f - 0.07f * k * k;
                twist = 1.5f * k * k * k;
            }
            // Just thrown: a gout of ink off the knot.
            if (b.BombTimer > h.LastBomb + 1f)
                _droplets.Burst(new Vector3(at.X, ShellTop, at.Y), Vector3.Up, 14, 2.2f, 0.5f, 0.7f, 0.12f, 0.04f,
                    new Color(0.03f, 0.01f, 0.05f, 0.9f), new Color(Kin.Blotter.R * 0.5f, Kin.Blotter.G * 0.4f, 0.1f, 0f), drag: 2.5f);
            h.LastBomb = b.BombTimer;
            // It bobs and rocks on the crab's steps.
            float bob = 0.06f * Mathf.Abs(Mathf.Sin(h.Walk * 0.5f));
            h.Pivot.Position = new Vector3(0f, (bottom + ShellBelow * sy + bob) * rise + floor * (1f - rise), 0f);
            h.Pivot.Rotation = new Vector3(0.03f * Mathf.Sin(h.Walk * 0.5f), twist, 0.045f * Mathf.Sin(h.Walk * 0.5f + 1.3f));
            h.Pivot.Scale = new Vector3(sx, sy, sx) * Mathf.Max(rise, 0.01f);
            h.Shell.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("swell"), swell);
            h.Shell.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("ember"), 0.12f + 0.5f * swell);

            // The crab, poking out of the shell's mouth at its front.
            h.Crab.Node.Visible = rise > 0.8f;
            float front = sx * 0.95f;
            var crabAt = at + b.Heading * front;
            h.Crab.Node.Position = new Vector3(0f, Floor(crabAt), front);
            PoseCrab(h.Crab, h.Walk, b.Seed);

            // Ink beads off the shell as it goes.
            h.Drip += dt * 6f * rise;
            while (h.Drip >= 1f)
            {
                h.Drip -= 1f;
                var dir = _droplets.RandDir();
                dir.Y = Mathf.Abs(dir.Y) * 0.6f;
                var p = h.Pivot.GlobalPosition + new Vector3(dir.X * sx, dir.Y * sy, dir.Z * sx) * 0.9f;
                _droplets.Emit(p, Vector3.Down * 0.6f + _droplets.RandDir() * 0.3f, _droplets.RandRange(0.5f, 0.9f), 0.12f, 0.03f,
                    new Color(0.03f, 0.01f, 0.05f, 0.9f), new Color(0.2f, 0.06f, 0.35f, 0f), drag: 1.5f);
            }
        }
    }

    /// <summary>
    /// A Blotter freed: its shell pops like a murkling, its ink showering out orange; the bare crab is left behind, to
    /// run. Its drawn shell, where it stood (the world has already let it go).
    /// </summary>
    void FreeHermit(System.Numerics.Vector2 at)
    {
        foreach (var (b, h) in _hermits)
        {
            if (b.Alive || b.Position != at) continue;
            var middle = h.Pivot.GlobalPosition;
            var scale = h.Pivot.Scale;
            Pop(middle + Vector3.Up * scale.Y * 0.2f, BlotterTuning.ShellRadius * 0.8f, Kin.Blotter);
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.Tau / 5f + b.Seed;
                Pop(middle + new Vector3(Mathf.Cos(a) * scale.X * 0.6f, scale.Y * 0.4f, Mathf.Sin(a) * scale.X * 0.6f), 0.7f, Kin.Blotter);
            }
            // The crab stays where it stood, out of the shell.
            var crab = h.Crab;
            var where = crab.Node.GlobalTransform;
            h.Node.RemoveChild(crab.Node);
            AddChild(crab.Node);
            crab.Node.GlobalTransform = where;
            crab.Tail.Visible = true;
            var xz = new System.Numerics.Vector2(where.Origin.X, where.Origin.Z);
            _runaways.Add(new Runaway { Crab = crab, At = xz, Yaw = h.Node.Rotation.Y, Walk = h.Walk });
            h.Node.QueueFree();
            _hermits.Remove(b);
            return;
        }
        Pop(new Vector3(at.X, LevelMap.SwimBand, at.Y), BlotterTuning.ShellRadius * 0.8f, Kin.Blotter);
    }

    /// <summary>
    /// Freed crabs: a double take (a hop, eyes up, a look round), then off sideways as crabs go, away
    /// from her and off the screen, steering round rock; out of sight (or after a while), gone.
    /// </summary>
    void SyncRunaways(PlaneWorld world, float dt, Camera3D? camera)
    {
        for (int i = _runaways.Count - 1; i >= 0; i--)
        {
            var r = _runaways[i];
            r.Since += dt;
            float t = r.Since;
            var crab = r.Crab;

            if (t < DoubleTake)
            {
                // The double take: a hop, its eyes shooting up, a quick look one way and the other.
                float k = t / DoubleTake;
                float hop = 0.25f * Mathf.Max(0f, Mathf.Sin(k * Mathf.Pi * 2f)) * (1f - k);
                float look = 0.6f * Mathf.Sin(k * Mathf.Tau * 1.5f) * (1f - k);
                crab.Node.Position = new Vector3(r.At.X, Floor(r.At) + hop, r.At.Y);
                crab.Node.Rotation = new Vector3(0f, r.Yaw + look, 0f);
                PoseCrab(crab, r.Walk, 0f, alarm: Mathf.Min(1f, k * 4f), brandish: Mathf.Min(1f, k * 3f));
                // Its way out: away from her, toward the screen's edge.
                var away = r.At - world.Player.Position;
                r.Dir = away.LengthSquared() > 1e-4f ? System.Numerics.Vector2.Normalize(away) : new System.Numerics.Vector2(0f, 1f);
                continue;
            }

            // Off, sideways, round any rock in its way.
            float speed = ScuttleSpeed * Mathf.Min(1f, (t - DoubleTake) / 0.3f);
            r.Dir = Steer(r.At, r.Dir);
            r.At += r.Dir * speed * dt;
            r.Walk += speed * dt * 9f;
            crab.Node.Position = new Vector3(r.At.X, Floor(r.At), r.At.Y);
            // Its side to where it goes (local +X), turning quickly.
            float want = Mathf.Atan2(r.Dir.X, r.Dir.Y) - Mathf.Pi * 0.5f;
            r.Yaw = Mathf.LerpAngle(r.Yaw, want, Mathf.Min(1f, dt * 12f));
            crab.Node.Rotation = new Vector3(0f, r.Yaw, 0.08f * Mathf.Sin(r.Walk));
            PoseCrab(crab, r.Walk, 0f, alarm: 1f, brandish: 0.6f);
            // A puff of sand now and then.
            if (_motes.RandRange(0f, 1f) < 0.3f)
                _droplets.Emit(crab.Node.Position + Vector3.Up * 0.05f, Vector3.Up * 0.3f + _droplets.RandDir() * 0.2f, 0.6f, 0.08f, 0.16f,
                    new Color(0.7f, 0.65f, 0.55f, 0.25f), new Color(0.7f, 0.65f, 0.55f, 0f), drag: 3f);

            bool seen = camera is not null && camera.IsPositionInFrustum(crab.Node.Position + Vector3.Up * 0.3f);
            if (r.Gone < 0f && (!seen || t > ScuttleMax)) r.Gone = 0f;
            if (r.Gone >= 0f)
            {
                r.Gone += dt;
                // Gone: it shrinks away to nothing.
                float fade = 1f - r.Gone / 0.4f;
                crab.Node.Scale = Vector3.One * CrabScale * Mathf.Max(fade, 0.01f);
                if (fade <= 0f)
                {
                    crab.Node.QueueFree();
                    _runaways.RemoveAt(i);
                }
            }
        }
    }

    /// <summary>The way on from p nearest to dir that is open water (no rock a little ahead).</summary>
    System.Numerics.Vector2 Steer(System.Numerics.Vector2 p, System.Numerics.Vector2 dir)
    {
        if (_map is null) return dir;
        foreach (float turn in new[] { 0f, 0.4f, -0.4f, 0.8f, -0.8f, 1.2f, -1.2f, 1.6f, -1.6f, 2.2f, -2.2f, Mathf.Pi })
        {
            float c = Mathf.Cos(turn), s = Mathf.Sin(turn);
            var d = new System.Numerics.Vector2(dir.X * c - dir.Y * s, dir.X * s + dir.Y * c);
            if (_map.IsOpen(p + d * 0.9f) && _map.IsOpen(p + d * 0.45f)) return d;
        }
        return dir;
    }
}
