using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The afterlight her shots of light leave (docs/LIGHT-BUBBLES.md): a glowing thread along each one's path that fades
/// over about a second. It is never a straight line: every segment is kinked at its middle and every point is thrown
/// aside, and the kinks re-strike a dozen times a second and grow as the light ages, so it crackles like lightning.
/// A trail outlives its shot (it fades where the shot burst). All trails are one mesh, built in a single upload a frame.
/// </summary>
public partial class LightTrails : Node3D
{
    /// <summary>How long a point of afterlight lasts, how far apart points are laid, and how wide the ribbon is.</summary>
    const float Life = 1.1f, Spacing = 0.32f, Width = 0.34f;
    /// <summary>Jitter: aside at the head, aside when old, and how often it re-strikes.</summary>
    const float JitterNew = 0.03f, JitterOld = 0.2f, Strikes = 13f;

    sealed class Trail
    {
        public readonly List<(Vector3 At, float Born, int Id)> Points = new();
        public Color Color;
        public float Strength;
        public bool Fed;
    }

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

    /// <summary>A new level: no afterlight carries over.</summary>
    public void Clear()
    {
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
