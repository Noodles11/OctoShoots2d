using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Sealed walls (DESIGN-TOPDOWN §4.2), presentation: the carving on each wall's face — an X, faint, on a secret's or a
/// tunnel's; a sword on the way into the boss's arena, its crimson smouldering — filling with light as her shots strike
/// it and flashing with each, draining if they stop. Filled, the wall slides down into the sand (LevelView.Slide): it
/// shudders as it gives, the carving sinking with it, silt pushed up and rolling out all along its feet where it meets the
/// seabed, chips of rock flicked out, a dull grinding under it; the screen shakes as long as it goes (Sliding). A barred
/// boss arena's way in rises back up behind her as the fight begins (the slide played backwards) and carries the sword on
/// its inner face too, burning crimson while the fight runs; freed, the boss lets it slide down again.
/// </summary>
public partial class SealView : Node3D
{
    sealed class Emblem
    {
        public MeshInstance3D Mesh = null!;
        public ShaderMaterial Material = null!;
        public float Fill, Flash, Fade = 1f;
    }

    sealed class Slide
    {
        public List<Emblem> Emblems = new();
        public List<Vector3> Feet = new();
        public float Age, Height;
        public bool Up;
    }

    readonly List<Emblem> _emblems = new();
    /// <summary>The swords on the inner faces of a barred arena's ways in, by seal.</summary>
    readonly Dictionary<int, Emblem> _inner = new();
    readonly List<Slide> _slides = new();
    FxParticles _dust = null!, _silt = null!;
    LevelMap? _map;

    /// <summary>A wall is sliding down now (the screen shakes meanwhile).</summary>
    public bool Sliding => _slides.Count > 0;

    /// <summary>The boss's sword: crimson.</summary>
    static readonly Color Crimson = new(1f, 0.12f, 0.16f);

    public override void _Ready()
    {
        _dust = new FxParticles(512, additive: false);
        AddChild(_dust);
        _silt = new FxParticles(768, additive: false);
        AddChild(_silt);
    }

    public void Show(PlaneWorld world, LevelView level)
    {
        _map = world.Map;
        // (Those on the last level's walls went with them.)
        foreach (var e in _emblems) if (IsInstanceValid(e.Mesh)) e.Mesh.QueueFree();
        foreach (var e in _inner.Values) if (IsInstanceValid(e.Mesh)) e.Mesh.QueueFree();
        _emblems.Clear();
        _inner.Clear();
        _slides.Clear();
        var shader = GD.Load<Shader>("res://assets/shaders/seal_emblem.gdshader");
        bool barred = world.Boss is { Spec.Blockade: true };
        for (int i = 0; i < world.Seals.Count; i++)
        {
            var seal = world.Seals[i];
            _emblems.Add(Carve(world.Map, seal, seal.Facing, shader, seal.Open ? 0f : 1f, level));
            // A barred arena's ways in carry the sword on their inner face as well, seen only while the fight holds them shut.
            if (barred && seal.Kind == SealKind.Boss) _inner[i] = Carve(world.Map, seal, -seal.Facing, shader, 0f, level);
        }
    }

    /// <summary>
    /// The carving on the face of a seal looking along <paramref name="facing"/>: cut into the wall's own surface — a
    /// patch of it lifted a hair off the stone, part of the wall's mesh (LevelView.SealWall), so it goes wherever the wall
    /// goes. Laid out as seen from above (the camera's view), so the steep face does not squash it; it shows only on the
    /// rock, never on the seabed at its foot.
    /// </summary>
    Emblem Carve(LevelMap map, SecretSeal seal, System.Numerics.Vector2 facing, Shader shader, float fade, LevelView level)
    {
        bool boss = seal.Kind == SealKind.Boss;
        float half = boss ? 1.7f : 1f;
        float H(System.Numerics.Vector2 q) => LevelView.SealHeight(map, seal.Rock, q.X, q.Y);
        // Out along the face from the wall's middle to the seabed in front, and the wall's top above it.
        float top = float.MinValue, d = 0f;
        for (; d <= seal.Rock.Thick + seal.Rock.Radius + 6f; d += 0.05f)
        {
            float h = H(seal.Rock.Center + facing * d);
            top = Mathf.Max(top, h);
            if (h <= LevelMap.SwimBand) break;
        }
        float floor = H(seal.Rock.Center + facing * (d + 1.5f));
        // Its middle on the face, a little over halfway up, where her shots strike it.
        var mid = seal.Rock.Center + facing * d;
        float want = floor + 0.55f * (top - floor);
        for (float k = d; k >= 0f; k -= 0.05f)
        {
            mid = seal.Rock.Center + facing * k;
            if (H(mid) >= want) break;
        }
        // Its blade (or the X's) along the way out of the wall, across the passage the other way.
        var along = facing;
        var across = new System.Numerics.Vector2(-facing.Y, facing.X);
        const int n = 28;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        for (int y = 0; y <= n; y++)
            for (int x = 0; x <= n; x++)
            {
                float u = x / (float)n, v = y / (float)n;
                var q = mid + across * ((u * 2f - 1f) * half) + along * ((v * 2f - 1f) * half);
                float h = H(q);
                const float e = 0.15f;
                var normal = new Vector3(H(q - new System.Numerics.Vector2(e, 0f)) - H(q + new System.Numerics.Vector2(e, 0f)), 2f * e,
                    H(q - new System.Numerics.Vector2(0f, e)) - H(q + new System.Numerics.Vector2(0f, e))).Normalized();
                // On the rock only: gone where the stone meets the seabed.
                float onRock = Mathf.SmoothStep(floor + 0.2f, floor + 0.6f, h);
                st.SetColor(new Color(1f, 1f, 1f, onRock));
                st.SetUV(new Vector2(u, v));
                st.SetNormal(normal);
                st.AddVertex(new Vector3(q.X, h, q.Y) + normal * 0.05f);
            }
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int a = y * (n + 1) + x, b = a + 1, c = a + n + 1, dd = c + 1;
                st.AddIndex(a); st.AddIndex(c); st.AddIndex(b);
                st.AddIndex(b); st.AddIndex(c); st.AddIndex(dd);
            }
        var material = new ShaderMaterial { Shader = shader };
        material.SetShaderParameter(SN.Of("shape"), boss ? 1 : 0);
        if (boss)
        {
            material.SetShaderParameter(SN.Of("light_color"), Crimson);
            material.SetShaderParameter(SN.Of("ember"), 1f);
        }
        var mesh = new MeshInstance3D { Mesh = st.Commit(), MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        // In the wall's own frame (both are laid out in the level's metres), so it rises and sinks with it.
        if (level.SealWall(seal.Rock.Center) is { } wall) wall.AddChild(mesh);
        else AddChild(mesh);
        return new Emblem { Mesh = mesh, Material = material, Fade = fade };
    }

    public void Sync(PlaneWorld world, float dt, Camera3D? camera)
    {
        for (int i = 0; i < _emblems.Count && i < world.Seals.Count; i++)
        {
            var e = _emblems[i];
            var seal = world.Seals[i];
            // Fills quickly with each hit; drains slower, as the sim lets it go.
            e.Fill = Mathf.MoveToward(e.Fill, seal.Glow, dt * (seal.Glow > e.Fill ? 2.5f : 1f));
            e.Flash = Mathf.MoveToward(e.Flash, 0f, dt * 3f);
            e.Mesh.Visible = e.Fade > 0.001f;
            e.Material.SetShaderParameter(SN.Of("fill"), e.Fill);
            e.Material.SetShaderParameter(SN.Of("flash"), e.Flash);
            e.Material.SetShaderParameter(SN.Of("fade"), e.Fade);
        }
        // The inner swords burn crimson while the fight holds the way shut.
        foreach (var (i, e) in _inner)
        {
            if (i >= world.Seals.Count) continue;
            e.Fill = Mathf.MoveToward(e.Fill, world.Seals[i].Barred ? 1f : 0f, dt * 1.5f);
            e.Mesh.Visible = e.Fade > 0.001f;
            e.Material.SetShaderParameter(SN.Of("fill"), e.Fill);
            e.Material.SetShaderParameter(SN.Of("fade"), e.Fade);
        }
        for (int i = _slides.Count - 1; i >= 0; i--)
        {
            var s = _slides[i];
            s.Age += dt;
            float t = s.Age / LevelView.SlideSeconds;
            float down = LevelView.SlideAt(s.Up ? 1f - t : t);
            // The carvings, cut in their wall, go down with it glowing out (or come up with it, glowing in).
            foreach (var em in s.Emblems)
                em.Fade = s.Up ? Mathf.SmoothStep(0.1f, 0.6f, t) : 1f - Mathf.SmoothStep(0.4f, 0.9f, t);
            // Silt pushed up along its feet as it goes into the sand, rolling out low and settling; chips of rock flicked off.
            if (s.Feet.Count > 0 && t > 0.06f && t < 1f)
            {
                float rate = 260f * (0.3f + Mathf.Sin(Mathf.Pi * Mathf.Clamp((t - 0.06f) / 0.94f, 0f, 1f)));
                float n = rate * dt;
                int count = (int)n + (_silt.RandRange(0f, 1f) < n - (int)n ? 1 : 0);
                for (int k = 0; k < count; k++)
                {
                    var foot = s.Feet[(int)_silt.RandRange(0f, s.Feet.Count - 0.001f)];
                    // Out from under the wall, low along the floor, then up as it slows (fine silt lifts in the water).
                    var away = new Vector3(_silt.RandRange(-1f, 1f), 0f, _silt.RandRange(-1f, 1f)).Normalized();
                    var v = away * _silt.RandRange(0.6f, 2f) + Vector3.Up * _silt.RandRange(0.3f, 0.9f);
                    float size = _silt.RandRange(0.25f, 0.5f);
                    _silt.Emit(foot + away * 0.2f, v, _silt.RandRange(1.2f, 2.2f), size, size * 2.6f,
                        new Color(0.6f, 0.62f, 0.55f, 0.5f), new Color(0.62f, 0.64f, 0.58f, 0f), drag: 1.8f, buoyancy: 0.35f);
                    if (_dust.RandRange(0f, 1f) < 0.18f)
                        _dust.Emit(foot + Vector3.Up * 0.2f, away * _dust.RandRange(1.5f, 3.2f) + Vector3.Up * _dust.RandRange(1.2f, 2.4f), _dust.RandRange(0.5f, 0.8f), _dust.RandRange(0.07f, 0.13f), 0.05f,
                            new Color(0.3f, 0.32f, 0.3f, 1f), new Color(0.32f, 0.34f, 0.3f, 0f), drag: 2.2f, buoyancy: -2.5f);
                }
            }
            if (s.Age > LevelView.SlideSeconds + 0.2f) _slides.RemoveAt(i);
        }
        if (camera is not null)
        {
            _dust.Tick(dt, camera.GlobalBasis);
            _silt.Tick(dt, camera.GlobalBasis);
        }
    }

    public void OnEvent(PlaneWorld world, PlaneEvent e)
    {
        if (_map is null) return;
        if (e.Type == PlaneEventType.SealLit)
        {
            int i = world.Seals.FindIndex(s => s.Emblem == e.Position);
            if (i >= 0 && i < _emblems.Count) _emblems[i].Flash = 1f;
        }
        else if (e.Type is PlaneEventType.SealOpened or PlaneEventType.SealClosed)
        {
            // The wall gives and slides down into the sand (or rises back up out of it), its carvings with it.
            int i = world.Seals.FindIndex(s => s.Rock.Center == e.Position);
            if (i < 0) return;
            var seal = world.Seals[i];
            var slide = new Slide
            {
                Feet = LevelView.SealFoot(_map, seal.Rock),
                Height = LevelView.SealDrop(_map, seal.Rock),
                Up = e.Type == PlaneEventType.SealClosed,
            };
            if (i < _emblems.Count) slide.Emblems.Add(_emblems[i]);
            if (_inner.TryGetValue(i, out var inner)) slide.Emblems.Add(inner);
            _slides.Add(slide);
        }
    }
}
