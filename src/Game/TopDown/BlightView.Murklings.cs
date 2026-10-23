using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Murklings as liquid (murkling.gdshader): each one's body a soft drop of ink, like mercury in no gravity.
/// <para>
/// Twelve points round it (the icosahedron's corners) each stand out of round or in, on a spring back to round (its
/// surface tension) lightly damped, so it rings; each is pulled toward its neighbours (so a dent spreads and smooths);
/// and the whole is kept at its volume (what one lobe takes in, the rest push out). Its inertia shapes it: when it
/// speeds up the ink lags, its trailing side bulging and its leading side flattening; when it stops, it sloshes on;
/// moving fast, it draws out along its way. Her hit dents it where the light struck, and it rebounds past round.
/// </para>
/// <para>
/// Each hit tears a hole there (the stronger the shot, the wider: about a quarter of a radian for a plain shot, up to
/// about 0.7 for one that takes all of it), ragged, its lip glowing with the ember inside, and rips chunks of it away (wobbling
/// blobs of ink that drag to a stop, sink and break into drops). The holes close as its corruption grows back: they hold
/// while the meter waits (MurkDelay), then shrink as it refills, gone when it is full again.
/// </para>
/// <para>
/// Its throw: drawing the slingshot back, its body sloshes back with the strain; at the snap the front is flung out and
/// the body recoils, ringing as the piece leaves it.
/// </para>
/// </summary>
public partial class BlightView
{
    sealed class MurkBody
    {
        public readonly float[] R = new float[Lobes], V = new float[Lobes];
        public readonly ShaderMaterial Material;
        public System.Numerics.Vector2 LastVelocity;
        public bool Seen, WasWinding;
        public float LastThrowAge = 10f;
        /// <summary>Holes torn in it: where (on it), how strong the hit was (its share of the meter).</summary>
        public readonly List<(Vector3 Dir, float Strength)> Holes = new();
        /// <summary>The meter's emptiness when the holes were last torn (they close as it falls back to 0).</summary>
        public float OpenAt;
        /// <summary>A second material drawing it over her (a Blightroot's head: murkling_over), kept in step with Material.</summary>
        public ShaderMaterial? Over;
        public MurkBody(ShaderMaterial material) { Material = material; }
    }

    const int Lobes = 12;
    /// <summary>Its surface tension (spring), its damping (low: it rings), its neighbours' pull, how hard its inertia shapes it, how far it may stand out.</summary>
    const float Tension = 170f, Damping = 5f, Coupling = 40f, Inertia = 0.03f, LobeMax = 0.45f;
    /// <summary>A hit: how deep it dents (per its share of the meter), and the holes' widths (radians: least, and per √share).</summary>
    const float HitDent = 8f, HoleLeast = 0.12f, HoleWide = 0.6f;
    const int MaxHoles = 6;

    static readonly Vector3[] LobeDirs =
    {
        new(-0.5257f, 0.8507f, 0f), new(0.5257f, 0.8507f, 0f), new(-0.5257f, -0.8507f, 0f), new(0.5257f, -0.8507f, 0f),
        new(0f, -0.5257f, 0.8507f), new(0f, 0.5257f, 0.8507f), new(0f, -0.5257f, -0.8507f), new(0f, 0.5257f, -0.8507f),
        new(0.8507f, 0f, -0.5257f), new(0.8507f, 0f, 0.5257f), new(-0.8507f, 0f, -0.5257f), new(-0.8507f, 0f, 0.5257f),
    };

    /// <summary>Each lobe's five neighbours (the icosahedron's edges).</summary>
    static readonly int[][] LobeNear = BuildNear();

    static int[][] BuildNear()
    {
        var near = new int[Lobes][];
        for (int i = 0; i < Lobes; i++)
        {
            var list = new List<int>();
            for (int j = 0; j < Lobes; j++)
                if (j != i && LobeDirs[i].Dot(LobeDirs[j]) > 0.4f) list.Add(j);
            near[i] = list.ToArray();
        }
        return near;
    }

    /// <summary>Liquid bodies by what they belong to: a murkling, or a Blightroot's head.</summary>
    readonly Dictionary<object, MurkBody> _bodies = new();
    Shader? _murkShader;

    MurkBody BodyOf(object m)
    {
        if (_bodies.TryGetValue(m, out var b)) return b;
        _murkShader ??= GD.Load<Shader>("res://assets/shaders/murkling.gdshader");
        b = new MurkBody(new ShaderMaterial { Shader = _murkShader });
        _bodies[m] = b;
        return b;
    }

    /// <summary>One frame of a murkling's liquid body: its inertia, its throw's strain and snap, its springs, its holes closing.</summary>
    void StepBody(Murkling m, MurkBody b, float dt)
    {
        // The throw's drawing back: it sloshes away from its aim, then is flung forward at the snap and recoils.
        var aim = new Vector3(m.Aim.X, 0f, m.Aim.Y);
        bool winding = m.Windup > BlightTuning.SnapSeconds;
        bool snapped = m.ThrowAge < b.LastThrowAge && m.ThrowAge < 0.1f;
        b.LastThrowAge = m.ThrowAge;
        SoftStep(b, m.Velocity, aim, winding, snapped, m.Corruption, m.Seed, dt);
    }

    /// <summary>
    /// One frame of a liquid body (a murkling's, a Blightroot's head): its inertia, any strain (drawn back from aim) and
    /// snap, its springs and volume, its holes closing as meter refills; sent to its material. stir: how restless the
    /// water round it keeps it.
    /// </summary>
    void SoftStep(MurkBody b, System.Numerics.Vector2 velocity, Vector3 aim, bool winding, bool snapped, CorruptionMeter meter, float seed, float dt, float stirAmount = 0.06f)
    {
        b.Seen = true;
        dt = Mathf.Min(dt, 1f / 30f);
        // Its acceleration (map axes: x east, y south = world z): the ink lags it.
        var acc = (velocity - b.LastVelocity) / Mathf.Max(dt, 1e-4f);
        b.LastVelocity = velocity;
        var a = new Vector3(acc.X, 0f, acc.Y);
        var v = new Vector3(velocity.X, 0f, velocity.Y);
        float speed = v.Length();
        var along = speed > 1e-3f ? v / speed : Vector3.Zero;
        const int Steps = 3;
        float h = dt / Steps;
        for (int step = 0; step < Steps; step++)
        {
            float mean = 0f;
            for (int i = 0; i < Lobes; i++)
            {
                var d = LobeDirs[i];
                float near = 0f;
                foreach (int j in LobeNear[i]) near += b.R[j];
                near /= LobeNear[i].Length;
                // Moving fast it draws out along its way; speeding up, the ink lags behind.
                float stretch = speed * 0.035f * (d.Dot(along) * d.Dot(along) - 0.33f);
                float lag = Mathf.Clamp(-d.Dot(a) * Inertia, -0.3f, 0.3f);
                float strain = winding ? -d.Dot(aim) * 0.12f : 0f;
                // The water round it never quite still: a slow stir on each lobe, out of step.
                float stir = Mathf.Sin(_time * (2.1f + 0.37f * i) + seed * 3f + i * 1.7f) * stirAmount;
                float f = -Tension * (b.R[i] - stretch - strain - stir - lag) - Damping * b.V[i] + Coupling * (near - b.R[i]);
                b.V[i] += f * h;
            }
            for (int i = 0; i < Lobes; i++)
            {
                b.R[i] = Mathf.Clamp(b.R[i] + b.V[i] * h, -LobeMax, LobeMax);
                mean += b.R[i];
            }
            // Its volume kept: what one lobe takes in, the rest push out.
            mean /= Lobes;
            for (int i = 0; i < Lobes; i++) b.R[i] -= mean;
        }
        if (snapped)
            // The snap: the front flung out, the back pulled in (it rings as the piece leaves it).
            for (int i = 0; i < Lobes; i++) b.V[i] += LobeDirs[i].Dot(aim) * 3.2f;
        b.WasWinding = winding;
        var lobes = new float[Lobes];
        for (int i = 0; i < Lobes; i++) lobes[i] = b.R[i];
        b.Material.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("lobes"), lobes);
        b.Over?.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("lobes"), lobes);

        // Its holes close as its corruption grows back: they hold while it waits, shrink as it refills, gone when full.
        float empty = meter.Empty ? 1f : 1f - meter.Fraction;
        if (empty <= 0.001f)
        {
            b.Holes.Clear();
            b.OpenAt = 0f;
        }
        float open = b.OpenAt > 1e-4f ? Mathf.Clamp(empty / b.OpenAt, 0f, 1f) : 0f;
        var holes = new Vector4[MaxHoles];
        for (int i = 0; i < b.Holes.Count && i < MaxHoles; i++)
        {
            var (dir, strength) = b.Holes[i];
            float width = (HoleLeast + HoleWide * Mathf.Sqrt(strength)) * Mathf.Sqrt(open);
            holes[i] = new Vector4(dir.X, dir.Y, dir.Z, width);
        }
        b.Material.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("holes"), holes);
        b.Over?.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("holes"), holes);
    }

    /// <summary>
    /// Her light struck a murkling (from InkSplash): a dent where it struck, ringing back; a hole torn there, as wide
    /// as the hit was strong; chunks of its ink ripped away.
    /// </summary>
    void TearMurkling(Murkling m, Vector3 back, float hitShare, Vector3 at, float radius) =>
        Tear(BodyOf(m), m.Corruption, back, hitShare, at, radius, m.Seed);

    /// <summary>A liquid body struck (a murkling, a Blightroot's head): dented, torn open, chunks of it ripped away.</summary>
    void Tear(MurkBody b, CorruptionMeter meter, Vector3 back, float hitShare, Vector3 at, float radius, float seed)
    {
        // Where it struck, on the body (the side facing the shot, a little up toward the camera).
        var dir = (back + Vector3.Up * 0.35f).Normalized();
        for (int i = 0; i < Lobes; i++)
        {
            float k = Mathf.Max(0f, LobeDirs[i].Dot(dir));
            b.V[i] -= HitDent * (0.5f + hitShare * 2f) * k * k;
        }
        // The hole: a new one, or the nearest widened when it is full of them.
        if (b.Holes.Count < MaxHoles) b.Holes.Add((Jitter(dir, 0.25f), hitShare));
        else
        {
            int best = 0;
            for (int i = 1; i < b.Holes.Count; i++)
                if (b.Holes[i].Dir.Dot(dir) > b.Holes[best].Dir.Dot(dir)) best = i;
            b.Holes[best] = (b.Holes[best].Dir, Mathf.Min(1f, b.Holes[best].Strength + hitShare));
        }
        b.OpenAt = Mathf.Max(b.OpenAt, meter.Empty ? 1f : 1f - meter.Fraction);
        // Chunks ripped out of it: blobs of its ink flung off the struck side.
        int chunks = 1 + (int)(hitShare * 5f);
        for (int c = 0; c < chunks; c++)
        {
            var side = new Vector3(-back.Z, 0f, back.X);
            var v = back * _droplets.RandRange(2f, 3.8f) + side * _droplets.RandRange(-1.8f, 1.8f) + Vector3.Up * _droplets.RandRange(0.5f, 1.6f);
            AddChunk(at + back * radius * 0.8f, v, radius * _droplets.RandRange(0.16f, 0.26f) * (0.8f + hitShare), seed + c);
        }
    }

    Vector3 Jitter(Vector3 d, float amount) =>
        (d + new Vector3(_droplets.RandRange(-amount, amount), _droplets.RandRange(-amount, amount), _droplets.RandRange(-amount, amount))).Normalized();

    // ───────────────────────── ripped chunks ─────────────────────────

    sealed class Chunk
    {
        public MeshInstance3D View = null!;
        public Vector3 Position, Velocity;
        public float Size, Age, Life, Seed;
    }

    readonly List<Chunk> _chunks = new();
    const int MaxChunks = 40;

    void AddChunk(Vector3 at, Vector3 velocity, float size, float seed)
    {
        if (_chunks.Count >= MaxChunks)
        {
            _chunks[0].View.QueueFree();
            _chunks.RemoveAt(0);
        }
        var view = new MeshInstance3D { Mesh = _sphere, MaterialOverride = _orb, CastShadow = GeometryInstance3D.ShadowCastingSetting.On };
        view.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("ember"), 0.15f);
        view.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("wobble"), 1.6f);
        view.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("seed"), seed);
        AddChild(view);
        _chunks.Add(new Chunk { View = view, Position = at, Velocity = velocity, Size = size, Life = _droplets.RandRange(0.55f, 0.9f), Seed = seed });
    }

    /// <summary>
    /// The chunks fly on, dragged by the water, sinking (ink is heavier); each wobbles as a drop does (stretched along
    /// its flight, ringing back), shrinks, sheds a drop now and then, and at its end breaks into a few.
    /// </summary>
    void StepChunks(float dt)
    {
        for (int i = _chunks.Count - 1; i >= 0; i--)
        {
            var c = _chunks[i];
            c.Age += dt;
            if (c.Age >= c.Life)
            {
                for (int k = 0; k < 3; k++)
                    _droplets.Emit(c.Position, c.Velocity * 0.4f + _droplets.RandDir() * 0.5f, _droplets.RandRange(0.3f, 0.5f), c.Size * 0.7f, c.Size * 0.2f, InkDrop, InkDropFade, drag: 3f, buoyancy: -2f);
                c.View.QueueFree();
                _chunks.RemoveAt(i);
                continue;
            }
            c.Velocity *= Mathf.Exp(-3.2f * dt);
            c.Velocity += Vector3.Down * 2.2f * dt;
            c.Position += c.Velocity * dt;
            float k01 = c.Age / c.Life;
            float speed = c.Velocity.Length();
            float ring = 1f + 0.35f * Mathf.Exp(-6f * c.Age) * Mathf.Sin(28f * c.Age + c.Seed);
            float stretch = (1f + Mathf.Min(speed * 0.12f, 0.6f)) * ring;
            float s = c.Size * (1f - 0.6f * k01);
            c.View.Position = c.Position;
            c.View.Basis = speed > 1e-3f ? Basis.LookingAt(c.Velocity / speed, Vector3.Up).Scaled(new Vector3(s / Mathf.Sqrt(stretch), s / Mathf.Sqrt(stretch), s * stretch)) : Basis.Identity.Scaled(Vector3.One * s);
            c.View.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("fade"), 1f - Mathf.SmoothStep(0.7f, 1f, k01));
            if (_droplets.RandRange(0f, 1f) < dt * 6f)
                _droplets.Emit(c.Position, c.Velocity * 0.3f, 0.35f, s * 0.5f, s * 0.15f, InkDrop, InkDropFade, drag: 3f, buoyancy: -2f);
        }
    }

    /// <summary>Bodies of murklings no longer here are let go.</summary>
    void ForgetBodies()
    {
        var gone = new List<object>();
        foreach (var (m, b) in _bodies)
        {
            if (!b.Seen) gone.Add(m);
            b.Seen = false;
        }
        foreach (var m in gone) _bodies.Remove(m);
    }

    void ClearMurkBodies()
    {
        _bodies.Clear();
        foreach (var c in _chunks) c.View.QueueFree();
        _chunks.Clear();
    }

    // ───────────────────────── every creature: a flinch when struck ─────────────────────────

    /// <summary>Views scaled for a flinch this frame (put back before the next frame's views are drawn).</summary>
    readonly List<(Node3D Node, Vector3 Scale)> _flinched = new();

    /// <summary>Puts back what last frame's flinches scaled (before this frame's views set their own).</summary>
    void UnFlinch()
    {
        foreach (var (node, scale) in _flinched)
            if (GodotObject.IsInstanceValid(node)) node.Scale = scale;
        _flinched.Clear();
    }

    /// <summary>
    /// A creature her light has just struck contracts for a moment, then rebounds past its size and settles — a sharp
    /// flinch, harder for a harder hit (CorruptionMeter.LastHit, Since).
    /// </summary>
    void Flinch(Node3D? node, CorruptionMeter meter)
    {
        if (node is null || meter.LastHit <= 0f || meter.Since > 0.3f || meter.Empty) return;
        float share = Mathf.Clamp(meter.LastHit / Mathf.Max(meter.Max, 1f), 0.05f, 1f);
        float t = meter.Since;
        float k = 1f - (0.1f + 0.2f * share) * Mathf.Exp(-14f * t) * Mathf.Cos(26f * t);
        _flinched.Add((node, node.Scale));
        node.Scale *= k;
    }

    void FlinchAll(PlaneWorld world)
    {
        for (int i = 0; i < _murks.Count && i < world.Murklings.Count; i++)
            if (_murks[i].Visible) Flinch(_murks[i], world.Murklings[i].Corruption);
        for (int i = 0; i < _roots.Count && i < world.Blightroots.Count; i++) Flinch(_roots[i].Node as Node3D, world.Blightroots[i].Corruption);
        foreach (var (b, h) in _hermits) Flinch(h.Node as Node3D, b.Corruption);
        foreach (var (t, tube) in _tubes) Flinch(tube.Node as Node3D, t.Corruption);
        foreach (var (w, view) in _wardens) Flinch(view.Node as Node3D, w.Corruption);
        foreach (var (l, jaw) in _jaws) Flinch(jaw.Node as Node3D, l.Corruption);
    }
}
