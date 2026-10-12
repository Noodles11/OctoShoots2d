using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The afterlight her shots of light leave (docs/LIGHT-BUBBLES.md): a glowing thread along each one's path that fades
/// over about a second. It is never a straight line: every segment is kinked at its middle and every point is thrown
/// aside, and the kinks re-strike a dozen times a second and grow as the light ages, so it crackles like lightning.
/// A trail outlives its shot only briefly, like an afterimage. When her light strikes something, a power surge arcs
/// between the impact and her for half a second. All trails and surges are one mesh, built in a single upload a frame.
/// </summary>
public partial class LightTrails : Node3D
{
    /// <summary>How long a point of afterlight lasts, how far apart points are laid, and how wide the ribbon is.</summary>
    const float Life = 0.3f, Spacing = 0.32f, Width = 0.34f;
    /// <summary>A surge: how long it lasts, its width, how far it is thrown aside, how often it re-strikes, how many at once.</summary>
    const float SurgeLife = 0.5f, SurgeWidth = 0.26f, SurgeJag = 0.32f, SurgeStrikes = 16f;
    const int MaxSurges = 8;
    /// <summary>Jitter: aside at the head, aside when old, and how often it re-strikes.</summary>
    const float JitterNew = 0.03f, JitterOld = 0.2f, Strikes = 13f;

    sealed class Trail
    {
        public readonly List<(Vector3 At, float Born, int Id)> Points = new();
        public Color Color;
        public float Strength;
        public bool Fed;
    }

    sealed class Surge
    {
        public Vector3 From;
        public float Age;
        public int Seed;
    }

    readonly List<Surge> _surges = new();

    /// <summary>Where Clementine is (the far end of every surge), set each frame.</summary>
    public Vector3 Her;

    readonly Dictionary<PlaneShot, Trail> _live = new();
    readonly List<Trail> _fading = new();
    readonly ArrayMesh _mesh = new();
    MeshInstance3D _view = null!;
    float _time;
    int _ids;

    // Reused each frame (one surface upload).
    readonly List<Vector3> _verts = new();
    readonly List<Color> _colors = new();
    readonly List<Vector2> _uvs = new();

    public override void _Ready()
    {
        _view = new MeshInstance3D
        {
            Mesh = _mesh,
            MaterialOverride = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/light_trail.gdshader"), RenderPriority = 1 },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            // The mesh is rebuilt where the shots are: never cull it.
            ExtraCullMargin = 16384f,
        };
        AddChild(_view);
    }

    /// <summary>One of her shots this frame: where it is drawn, how warm and how bright its light is.</summary>
    public void Feed(PlaneShot shot, Vector3 at, float warmth, float strength)
    {
        if (!_live.TryGetValue(shot, out var trail)) _live[shot] = trail = new Trail();
        trail.Fed = true;
        trail.Color = new Color(1f, 0.769f, 0.42f).Lerp(new Color(1f, 0.95f, 0.84f), warmth);
        trail.Strength = strength;
        var pts = trail.Points;
        if (pts.Count == 0 || pts[^1].At.DistanceTo(at) >= Spacing) pts.Add((at, _time, _ids++));
        else pts[^1] = (at, pts[^1].Born, pts[^1].Id);
    }

    /// <summary>Her light struck something at this point: a surge arcs between it and her.</summary>
    public void Strike(Vector3 at)
    {
        // Several hits in one place at once are one surge, renewed.
        foreach (var s in _surges)
            if (s.Age < 0.1f && s.From.DistanceSquaredTo(at) < 0.5f)
            {
                s.Age = 0f;
                return;
            }
        if (_surges.Count >= MaxSurges) _surges.RemoveAt(0);
        _surges.Add(new Surge { From = at, Seed = _ids++ * 31 });
    }

    /// <summary>A new level: no afterlight carries over.</summary>
    public void Clear()
    {
        _surges.Clear();
        _live.Clear();
        _fading.Clear();
        _mesh.ClearSurfaces();
    }

    /// <summary>Ages the afterlight (shots not fed this frame are gone: their trails fade where they ended) and rebuilds it.</summary>
    public void Step(float dt, float time)
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

        _verts.Clear();
        _colors.Clear();
        _uvs.Clear();
        foreach (var trail in _live.Values) Build(trail);
        for (int i = _fading.Count - 1; i >= 0; i--)
        {
            if (!Build(_fading[i])) _fading.RemoveAt(i);
        }
        for (int i = _surges.Count - 1; i >= 0; i--)
        {
            var surge = _surges[i];
            surge.Age += dt;
            if (surge.Age >= SurgeLife) _surges.RemoveAt(i);
            else BuildSurge(surge);
        }
        _mesh.ClearSurfaces();
        if (_verts.Count == 0) return;
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = _verts.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = _colors.ToArray();
        arrays[(int)Mesh.ArrayType.TexUV] = _uvs.ToArray();
        _mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
    }

    /// <summary>Adds a trail's ribbon (false once it has wholly faded).</summary>
    bool Build(Trail trail)
    {
        var pts = trail.Points;
        while (pts.Count > 0 && _time - pts[0].Born > Life) pts.RemoveAt(0);
        if (pts.Count < 2) return pts.Count > 0;
        int strike = (int)(_time * Strikes);
        // The jagged line: each point thrown aside, and a kink struck into the middle of every segment.
        var line = new List<(Vector3 At, float Age)>(pts.Count * 2);
        for (int k = 0; k < pts.Count; k++)
        {
            var (at, born, id) = pts[k];
            float age = Mathf.Clamp((_time - born) / Life, 0f, 1f);
            var along = (pts[Mathf.Min(k + 1, pts.Count - 1)].At - pts[Mathf.Max(k - 1, 0)].At) with { Y = 0f };
            var side = along.LengthSquared() > 1e-6f ? new Vector3(-along.Z, 0f, along.X).Normalized() : Vector3.Right;
            float amp = Mathf.Lerp(JitterNew, JitterOld, age);
            // The head stays on the shot.
            if (k == pts.Count - 1) amp = 0f;
            line.Add((at + side * Signed(id, strike) * amp, age));
            if (k < pts.Count - 1)
            {
                var next = pts[k + 1];
                var mid = (at + next.At) * 0.5f;
                float midAge = Mathf.Clamp((_time - (born + next.Born) * 0.5f) / Life, 0f, 1f);
                line.Add((mid + side * Signed(id * 7 + 3, strike) * Mathf.Lerp(JitterNew, JitterOld, midAge) * 1.6f, midAge));
            }
        }
        for (int k = 1; k < line.Count; k++)
        {
            var (a, ageA) = line[k - 1];
            var (b, ageB) = line[k];
            var dir = (b - a) with { Y = 0f };
            if (dir.LengthSquared() < 1e-8f) continue;
            var side = new Vector3(-dir.Z, 0f, dir.X).Normalized();
            float fa = Fade(ageA) * trail.Strength, fb = Fade(ageB) * trail.Strength;
            // Thinner as it fades.
            float wa = Width * (0.45f + 0.55f * Fade(ageA)) * 0.5f, wb = Width * (0.45f + 0.55f * Fade(ageB)) * 0.5f;
            Vector3 a0 = a - side * wa, a1 = a + side * wa, b0 = b - side * wb, b1 = b + side * wb;
            var ca = trail.Color with { A = fa };
            var cb = trail.Color with { A = fb };
            Quad(a0, a1, b0, b1, ca, cb);
        }
        return true;
    }

    static readonly Color SurgeCore = new(1f, 0.74f, 0.32f), SurgeFork = new(1f, 0.62f, 0.22f);

    /// <summary>
    /// A surge: a jagged bolt from the impact to her, re-struck many times a second (each strike a new path, flickering
    /// in strength), with a fork or two splitting off it, fading out over its half second.
    /// </summary>
    void BuildSurge(Surge surge)
    {
        float k = surge.Age / SurgeLife;
        int strike = (int)(_time * SurgeStrikes);
        // A hard flash at first, then flickering down.
        float flicker = 0.65f + 0.35f * Signed(surge.Seed + 5, strike);
        float strength = (1f - k) * (1f - k * 0.5f) * flicker * 0.55f;
        var from = surge.From;
        var to = Her;
        var path = Bolt(from, to, surge.Seed, strike, SurgeJag);
        Ribbon(path, SurgeCore, strength, SurgeWidth * (1f - 0.4f * k));
        // Forks: from a point along the bolt, a short jagged branch off to one side.
        for (int f = 0; f < 1; f++)
        {
            int at = (int)((0.25f + 0.5f * (Signed(surge.Seed + 11 + f, strike) * 0.5f + 0.5f)) * (path.Count - 1));
            var start = path[at];
            var along = (to - from) with { Y = 0f };
            if (along.LengthSquared() < 1e-6f) return;
            var side = new Vector3(-along.Z, 0f, along.X).Normalized() * (f == 0 ? 1f : -1f);
            float len = Mathf.Min(along.Length() * 0.25f, 2.5f);
            var end = start + (along.Normalized() * 0.6f + side).Normalized() * len * (0.6f + 0.4f * Signed(surge.Seed + 17 + f, strike));
            Ribbon(Bolt(start, end, surge.Seed + 101 * (f + 1), strike, SurgeJag * 0.6f), SurgeFork, strength * 0.5f, SurgeWidth * 0.6f);
        }
    }

    /// <summary>A jagged path from a to b: a point every half metre, thrown aside most in the middle, never at the ends.</summary>
    static List<Vector3> Bolt(Vector3 a, Vector3 b, int seed, int strike, float jag)
    {
        var d = (b - a) with { Y = 0f };
        float len = d.Length();
        int n = Mathf.Clamp((int)(len / 0.5f), 2, 64);
        var side = len > 1e-4f ? new Vector3(-d.Z, 0f, d.X) / len : Vector3.Right;
        var pts = new List<Vector3>(n + 1);
        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n;
            float pin = Mathf.Sin(t * Mathf.Pi);
            pts.Add(a.Lerp(b, t) + side * Signed(seed + i * 13, strike) * jag * pin);
        }
        return pts;
    }

    /// <summary>A flat ribbon along a path, of one colour and strength.</summary>
    void Ribbon(List<Vector3> path, Color color, float strength, float width)
    {
        var c = color with { A = strength };
        for (int k = 1; k < path.Count; k++)
        {
            var a = path[k - 1];
            var b = path[k];
            var dir = (b - a) with { Y = 0f };
            if (dir.LengthSquared() < 1e-8f) continue;
            var side = new Vector3(-dir.Z, 0f, dir.X).Normalized() * width * 0.5f;
            Quad(a - side, a + side, b - side, b + side, c, c);
        }
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

    void Quad(Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1, Color ca, Color cb)
    {
        void V(Vector3 v, Color c, float u)
        {
            _verts.Add(v);
            _colors.Add(c);
            _uvs.Add(new Vector2(u, 0f));
        }
        V(a0, ca, 0f); V(a1, ca, 1f); V(b1, cb, 1f);
        V(a0, ca, 0f); V(b1, cb, 1f); V(b0, cb, 0f);
    }
}
