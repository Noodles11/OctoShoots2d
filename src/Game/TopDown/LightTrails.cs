using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Her light painted on the water (docs/LIGHT-BUBBLES.md, light_stroke.gdshader): the afterlight her shots leave, and
/// the lightning that crackles off whatever her light hits. Every stroke is laid in three passes — a wide warm wash, a
/// ragged dry-brushed body, a broken white-hot core.
/// <para>
/// A shot's afterlight is never a straight line: each point is thrown aside and a kink struck into every segment, the
/// kinks re-striking a dozen times a second and growing as the light ages, so it crackles. Its shape follows the
/// shot's pearls (<see cref="Style"/>): a lance stays straight and long, a wave or helix shot trails a second strand,
/// a fuse fizzes wider.
/// </para>
/// <para>
/// A hit charges the struck body like a coil for a moment: bolts of lightning crackle straight out of its own outline
/// (the hit outline its shader draws) — jagged at every scale, some forking — re-striking at new places a dozen times a
/// second, most on the side struck, shorter and fewer as the charge drains. The harder the hit, the more, the longer
/// and the longer-lasting they are.
/// </para>
/// All of it is one mesh, built in one upload a frame.
/// </summary>
public partial class LightTrails : Node3D
{
    /// <summary>How long a point of afterlight lasts, how far apart points are laid, and the body's width.</summary>
    const float Life = 0.32f, Spacing = 0.3f, Width = 0.26f;
    /// <summary>The wash and the core, as shares of the body's width.</summary>
    const float WashWidth = 3.4f, CoreWidth = 0.32f;
    /// <summary>A hit's charge: how long it crackles (a plain hit's, longer for a harder one), its bolts' width at the outline, how often they re-strike, and how many at once.</summary>
    const float FlareLife = 0.3f, BoltWidth = 0.12f, BoltStrikes = 15f;
    const int MaxFlares = 12;
    /// <summary>Jitter: aside at the head, aside when old, and how often it re-strikes.</summary>
    const float JitterNew = 0.03f, JitterOld = 0.2f, Strikes = 13f;

    /// <summary>How a shot's afterlight is drawn, from its pearls.</summary>
    public readonly struct Style
    {
        /// <summary>How far it crackles aside, how long it lasts and how wide (shares of the plain one's).</summary>
        public readonly float Jag, Life, Width;
        /// <summary>A second strand weaving round it (Double Helix, a wave), out of phase.</summary>
        public readonly bool Twin;
        public Style(float jag = 1f, float life = 1f, float width = 1f, bool twin = false) { Jag = jag; Life = life; Width = width; Twin = twin; }
        public static readonly Style Plain = new();
    }

    sealed class Trail
    {
        public readonly List<(Vector3 At, float Born, int Id)> Points = new();
        public Color Color;
        public float Strength;
        public Style Style = Style.Plain;
        public bool Fed;
        public int Seed;
    }

    sealed class Flare
    {
        public Vector3 Center;
        /// <summary>The outline's radius, the angle of the side struck, the hit's strength (a plain shot's 1).</summary>
        public float Radius, Facing, Strength;
        public float Age, Life;
        public int Seed;
    }

    readonly List<Flare> _flares = new();

    readonly Dictionary<PlaneShot, Trail> _live = new();
    readonly List<Trail> _fading = new();
    readonly ArrayMesh _mesh = new();
    MeshInstance3D _view = null!;
    FxParticles _sparks = null!;
    float _time;
    int _ids;

    /// <summary>Reused each frame: the wash (a glaze, mixed over the scene) and the light (added to it), each one surface.</summary>
    sealed class Layer
    {
        public readonly List<Vector3> Verts = new();
        public readonly List<Color> Colors = new();
        public readonly List<Vector2> Uvs = new(), Uv2s = new();
        public void Clear() { Verts.Clear(); Colors.Clear(); Uvs.Clear(); Uv2s.Clear(); }
    }
    readonly Layer _wash = new(), _light = new();
    ShaderMaterial _washMat = null!, _lightMat = null!;
    readonly List<(Vector3 At, float Width)> _path = new(), _scratch = new();

    public override void _Ready()
    {
        _washMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/light_wash.gdshader"), RenderPriority = 0 };
        _lightMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/light_stroke.gdshader"), RenderPriority = 1 };
        _view = new MeshInstance3D
        {
            Mesh = _mesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            // The mesh is rebuilt where the shots are: never cull it.
            ExtraCullMargin = 16384f,
        };
        AddChild(_view);
        _sparks = new FxParticles(600, additive: true);
        AddChild(_sparks);
    }

    /// <summary>One of her shots this frame: where it is drawn, how warm and how bright its light is, and its pearls' style.</summary>
    public void Feed(PlaneShot shot, Vector3 at, float warmth, float strength, Style style)
    {
        if (!_live.TryGetValue(shot, out var trail)) _live[shot] = trail = new Trail { Seed = _ids++ * 17 };
        trail.Fed = true;
        // (Prism Pearl: a third of her split light keeps its own colour.)
        trail.Color = CombatView.LightOf(shot.Hue).Lerp(new Color(1f, 0.95f, 0.84f), shot.Hue == OctoShoots.Core.Plane.PrismHue.None ? warmth : warmth * 0.3f);
        trail.Strength = strength;
        trail.Style = style;
        var pts = trail.Points;
        if (pts.Count == 0 || pts[^1].At.DistanceTo(at) >= Spacing) pts.Add((at, _time, _ids++));
        else pts[^1] = (at, pts[^1].Born, pts[^1].Id);
    }

    /// <summary>
    /// Her light struck a body here (its middle, its outline this far out): lightning crackles out of its outline, more
    /// and longer for a harder hit (strength: the hit's share of a plain shot's), most on the side struck (from: the way
    /// the light came, toward the body; none for a hit without one).
    /// </summary>
    public void Flares(Vector3 center, float radius, Vector3 from, float strength)
    {
        float s = Mathf.Clamp(strength, 0.4f, 4f);
        var hitSide = new Vector2(-from.X, -from.Z);
        float facing = hitSide.LengthSquared() > 1e-6f ? Mathf.Atan2(hitSide.Y, hitSide.X) : _sparks.RandRange(0f, Mathf.Tau);
        // Hits on the same body at once are one charge, renewed and made stronger.
        foreach (var old in _flares)
            if (old.Age < 0.08f && old.Center.DistanceSquaredTo(center) < 0.25f)
            {
                old.Age = 0f;
                old.Strength = Mathf.Min(old.Strength + s * 0.5f, 4f);
                return;
            }
        if (_flares.Count >= MaxFlares) _flares.RemoveAt(0);
        _flares.Add(new Flare
        {
            Center = center, Radius = radius, Facing = facing, Strength = s, Seed = _ids++ * 31,
            Life = Mathf.Min(FlareLife * (1f + 0.2f * s), 0.65f),
        });
        // The flash where it struck: a spray of white-gold sparks off the struck side of the outline.
        var at = center + new Vector3(Mathf.Cos(facing), 0f, Mathf.Sin(facing)) * radius;
        for (int i = 0; i < 6 + (int)(4f * s); i++)
        {
            var d = _sparks.RandDir();
            d.Y = Mathf.Abs(d.Y) * 0.6f + 0.1f;
            _sparks.Emit(at, d * _sparks.RandRange(2.5f, 5.5f), _sparks.RandRange(0.15f, 0.35f), _sparks.RandRange(0.05f, 0.1f), 0.01f,
                new Color(1.6f, 1.45f, 1.1f), new Color(1f, 0.55f, 0.15f, 0f), drag: 4f);
        }
    }

    /// <summary>A new level: no afterlight carries over.</summary>
    public void Clear()
    {
        _flares.Clear();
        _live.Clear();
        _fading.Clear();
        _mesh.ClearSurfaces();
    }

    /// <summary>Ages the afterlight (shots not fed this frame are gone: their trails fade where they ended) and rebuilds it.</summary>
    public void Step(float dt, float time, Camera3D? camera)
    {
        _time = time;
        var gone = new List<PlaneShot>();
        foreach (var (shot, trail) in _live)
        {
            if (!trail.Fed)
            {
                gone.Add(shot);
                _fading.Add(trail);
            }
            trail.Fed = false;
        }
        foreach (var shot in gone) _live.Remove(shot);

        _wash.Clear();
        _light.Clear();
        foreach (var trail in _live.Values) Build(trail);
        for (int i = _fading.Count - 1; i >= 0; i--)
            if (!Build(_fading[i])) _fading.RemoveAt(i);
        for (int i = _flares.Count - 1; i >= 0; i--)
        {
            var flare = _flares[i];
            flare.Age += dt;
            if (flare.Age >= flare.Life) _flares.RemoveAt(i);
            else BuildFlare(flare);
        }
        if (camera is not null) _sparks.Tick(dt, camera.GlobalBasis);
        _mesh.ClearSurfaces();
        foreach (var (layer, material) in new[] { (_wash, _washMat), (_light, _lightMat) })
        {
            if (layer.Verts.Count == 0) continue;
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = layer.Verts.ToArray();
            arrays[(int)Mesh.ArrayType.Color] = layer.Colors.ToArray();
            arrays[(int)Mesh.ArrayType.TexUV] = layer.Uvs.ToArray();
            arrays[(int)Mesh.ArrayType.TexUV2] = layer.Uv2s.ToArray();
            _mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            _mesh.SurfaceSetMaterial(_mesh.GetSurfaceCount() - 1, material);
        }
    }

    // ───────────────────────── a shot's afterlight ─────────────────────────

    /// <summary>Adds a trail's strokes (false once it has wholly faded).</summary>
    bool Build(Trail trail)
    {
        var st = trail.Style;
        float life = Life * st.Life;
        var pts = trail.Points;
        while (pts.Count > 0 && _time - pts[0].Born > life) pts.RemoveAt(0);
        if (pts.Count < 2) return pts.Count > 0;
        int strike = (int)(_time * Strikes);
        for (int strand = 0; strand < (st.Twin ? 2 : 1); strand++)
        {
            _path.Clear();
            var fades = new List<float>(pts.Count * 2);
            for (int k = 0; k < pts.Count; k++)
            {
                var (at, born, id) = pts[k];
                float age = Mathf.Clamp((_time - born) / life, 0f, 1f);
                var along = (pts[Mathf.Min(k + 1, pts.Count - 1)].At - pts[Mathf.Max(k - 1, 0)].At) with { Y = 0f };
                var side = along.LengthSquared() > 1e-6f ? new Vector3(-along.Z, 0f, along.X).Normalized() : Vector3.Right;
                float amp = Mathf.Lerp(JitterNew, JitterOld, age) * st.Jag;
                // The head stays on the shot.
                if (k == pts.Count - 1) amp = 0f;
                // A twin strand weaves round the first, out of phase, opening as it ages.
                var twin = strand == 1 ? side * Mathf.Sin(id * 0.9f + _time * 9f) * (0.12f + 0.22f * age) : Vector3.Zero;
                float w = Width * st.Width * (0.45f + 0.55f * Fade(age)) * (strand == 1 ? 0.6f : 1f);
                _path.Add((at + side * Signed(id + strand * 991, strike) * amp + twin, w));
                fades.Add(Fade(age));
                if (k < pts.Count - 1)
                {
                    var next = pts[k + 1];
                    var mid = (at + next.At) * 0.5f;
                    float midAge = Mathf.Clamp((_time - (born + next.Born) * 0.5f) / life, 0f, 1f);
                    float ma = Mathf.Lerp(JitterNew, JitterOld, midAge) * st.Jag * 1.6f;
                    _path.Add((mid + side * Signed(id * 7 + 3 + strand * 577, strike) * ma + twin, Width * st.Width * (0.45f + 0.55f * Fade(midAge)) * (strand == 1 ? 0.6f : 1f)));
                    fades.Add(Fade(midAge));
                }
            }
            float strength = trail.Strength * (strand == 1 ? 0.7f : 1f);
            Strokes(_path, fades, trail.Color, strength, trail.Seed + strand * 13);
        }
        return true;
    }

    // ───────────────────────── a hit's lightning ─────────────────────────

    /// <summary>The bolts' colours: her light, and a warmer shade of it for their forks.</summary>
    static Color FlareColor => HerColor.Light;
    static Color ForkColor => HerColor.Light.Lerp(HerColor.Warm, 0.4f);

    /// <summary>
    /// A charge now: its bolts for this strike — each from a point on the outline (a hair inside it, so it comes out of
    /// the outline itself) straight out, jagged, tapering to nothing, a fork off some — struck anew a dozen times a
    /// second at new places, fewer and shorter as the charge drains.
    /// </summary>
    void BuildFlare(Flare flare)
    {
        float k = flare.Age / flare.Life;
        float s = flare.Strength;
        int strike = (int)(_time * BoltStrikes);
        int seed = flare.Seed + strike * 7919;
        float charge = 1f - k;
        float strength = Mathf.Min(charge * (0.85f + 0.15f * Signed(seed + 5, 0)) * (0.9f + 0.15f * s), 1.5f);
        int count = Mathf.Max(1, Mathf.RoundToInt((3f + 2.2f * s) * (0.4f + 0.6f * charge)));
        for (int b = 0; b < count; b++)
        {
            // Most come off the side struck; a few anywhere round it.
            float u = Signed(seed + b * 31, 0);
            float angle = b < count * 2 / 3 + 1 ? flare.Facing + u * 1.4f : u * Mathf.Pi;
            float toward = Mathf.Max(0f, Mathf.Cos(angle - flare.Facing));
            float len = (0.25f + 0.28f * s) * (0.55f + 0.45f * Signed(seed + b * 53, 1)) * (0.65f + 0.6f * toward) * (0.5f + 0.5f * charge);
            if (len < 0.05f) continue;
            var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            var foot = flare.Center + dir * flare.Radius * 0.97f;
            // Straight out, bent a little to one side.
            var side = new Vector3(-dir.Z, 0f, dir.X);
            var tip = foot + (dir + side * 0.35f * Signed(seed + b * 71, 2)).Normalized() * len;
            float w = BoltWidth * (0.6f + 0.25f * Mathf.Sqrt(s)) * (0.7f + 0.4f * toward);
            _path.Clear();
            Bolt(foot, tip, seed + b * 101, 0.32f, w, 0.05f, _path);
            Strokes(_path, null, FlareColor, strength, seed + b * 7);
            // A fork off some, from partway along, angling off.
            if (Signed(seed + b * 97, 3) > 0.1f && _path.Count > 4)
            {
                var (from, fw) = _path[_path.Count / 2];
                var forkTo = from + (dir + side * (Signed(seed + b * 13, 4) < 0f ? -0.9f : 0.9f)).Normalized() * len * 0.45f;
                _scratch.Clear();
                Bolt(from, forkTo, seed + b * 211, 0.35f, fw * 0.6f, 0f, _scratch);
                Strokes(_scratch, null, ForkColor, strength * 0.7f, seed + b * 11);
            }
        }
    }

    /// <summary>
    /// A fractal bolt from a to b: the line halved again and again, each midpoint thrown aside by rough × its segment's
    /// length (so it is jagged at every scale), never at the ends. Its width runs from w at a, tapering to taper × w at b.
    /// </summary>
    static void Bolt(Vector3 a, Vector3 b, int seed, float rough, float w, float taper, List<(Vector3 At, float Width)> into)
    {
        var pts = new List<Vector3> { a, b };
        int depth = Mathf.Clamp((int)Mathf.Ceil(Mathf.Log((a - b).Length() / 0.08f) / Mathf.Log(2f)), 1, 5);
        for (int level = 0; level < depth; level++)
        {
            var next = new List<Vector3>(pts.Count * 2);
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                var p = pts[i];
                var q = pts[i + 1];
                var d = (q - p) with { Y = 0f };
                float len = d.Length();
                var side = len > 1e-5f ? new Vector3(-d.Z, 0f, d.X) / len : Vector3.Right;
                next.Add(p);
                next.Add((p + q) * 0.5f + side * Signed(seed + level * 7717 + i * 131, 0) * rough * len);
            }
            next.Add(pts[^1]);
            pts = next;
        }
        for (int i = 0; i < pts.Count; i++)
            into.Add((pts[i], w * Mathf.Lerp(1f, taper, i / (float)(pts.Count - 1))));
    }

    // ───────────────────────── painting ─────────────────────────

    /// <summary>
    /// A painted stroke along a path (each point with its width, and its strength's share: null for all of it): the
    /// wash, the body and the core, one over the other.
    /// </summary>
    void Strokes(List<(Vector3 At, float Width)> path, List<float>? fades, Color color, float strength, int seed)
    {
        if (path.Count < 2) return;
        float s0 = (seed & 255) * 0.37f;
        for (int pass = 0; pass < 3; pass++)
        {
            float scale = pass == 0 ? WashWidth : pass == 1 ? 1f : CoreWidth;
            float along = 0f;
            for (int k = 1; k < path.Count; k++)
            {
                var (a, wa) = path[k - 1];
                var (b, wb) = path[k];
                var dir = (b - a) with { Y = 0f };
                float len = dir.Length();
                if (len < 1e-5f) continue;
                // Mitred a little: each end's side from the neighbouring segments too, so the kinks join.
                var sa = Side(path, k - 1);
                var sb = Side(path, k);
                float fa = (fades?[k - 1] ?? 1f) * strength, fb = (fades?[k] ?? 1f) * strength;
                var ca = color with { A = fa };
                var cb = color with { A = fb };
                float ha = wa * scale * 0.5f, hb = wb * scale * 0.5f;
                Quad(a - sa * ha, a + sa * ha, b - sb * hb, b + sb * hb, ca, cb, along, along + len, pass, s0);
                along += len;
            }
        }
    }

    static Vector3 Side(List<(Vector3 At, float Width)> path, int i)
    {
        var d = (path[Mathf.Min(i + 1, path.Count - 1)].At - path[Mathf.Max(i - 1, 0)].At) with { Y = 0f };
        return d.LengthSquared() > 1e-8f ? new Vector3(-d.Z, 0f, d.X).Normalized() : Vector3.Right;
    }

    /// <summary>Eases out: bright, then a long tail.</summary>
    static float Fade(float age) => (1f - age) * (1f - age);

    /// <summary>A stable −1..1 for a point and a strike (the jitter holds still between strikes).</summary>
    static float Signed(int id, int strike)
    {
        uint h = (uint)(id * 374761393 + strike * 668265263);
        h = (h ^ (h >> 13)) * 1274126177u;
        return ((h ^ (h >> 16)) & 0xFFFF) / 32767.5f - 1f;
    }

    void Quad(Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1, Color ca, Color cb, float sa, float sb, int pass, float seed)
    {
        var layer = pass == 0 ? _wash : _light;
        void V(Vector3 v, Color c, float u, float s)
        {
            layer.Verts.Add(v);
            layer.Colors.Add(c);
            layer.Uvs.Add(new Vector2(u, s));
            layer.Uv2s.Add(new Vector2(pass, seed));
        }
        V(a0, ca, 0f, sa); V(a1, ca, 1f, sa); V(b1, cb, 1f, sb);
        V(a0, ca, 0f, sa); V(b1, cb, 1f, sb); V(b0, cb, 0f, sb);
    }
}
