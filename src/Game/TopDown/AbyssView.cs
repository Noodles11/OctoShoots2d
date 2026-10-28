using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Life in the abyss under an abyss reef (docs/ADVENTURE.md §5), far below her plane, seen only through the water's
/// blur and murk: kelp fields rising from the abyss floor toward her (never near it), swaying; and big fish — long
/// fish and rays — cruising round her as dark shadows, keeping over the deep. Cosmetic: seeded per level, the sim
/// never reads it. Nothing on a level with a seabed.
/// </summary>
public partial class AbyssView : Node3D
{
    /// <summary>Fields' spacing, how many at most, their radius, and how clear of the way down they keep.</summary>
    const float FieldSpacing = 15f, FieldMin = 3.5f, FieldMax = 7f, ShaftClear = 16f;
    const int MaxFields = 14;
    /// <summary>The highest the floor under a field may lie (the walls' feet fall to the dropped floor, AbyssReef.DepthOf: only the deepest depths have kelp).</summary>
    const float FieldFloor = -19f;
    /// <summary>Strands per square metre of a field; their length (the tallest in its middle) and the highest a tip reaches.</summary>
    const float StrandDensity = 0.85f, StrandMin = 11f, StrandMax = 17f, TipTop = -8.5f;
    const int Segments = 12;
    /// <summary>The big fish: how many swim round her, how near and how far from her they come and go, how deep they swim.</summary>
    const int Shadows = 5;
    const float SpawnNear = 18f, SpawnFar = 30f, Stray = 22f, Leave = 40f, ShadowTop = -12f, ShadowBottom = -17f;
    /// <summary>The rock a big fish keeps under it (metres below its depth), and how far ahead it looks for it.</summary>
    const float Keel = 1.5f, LookAhead = 8f;

    sealed class Shadow
    {
        public Vector2 At, Heading;
        public float Y, Speed, Length, Width, Turn, Wander, Fade, Phase, Beat, Life;
        public bool Ray, Leaving, Live;
    }

    MeshInstance3D _kelp = null!;
    ShaderMaterial _kelpMaterial = null!;
    MultiMesh _shadowMesh = null!;
    ShaderMaterial _shadowMaterial = null!;
    readonly Shadow[] _shadows = new Shadow[Shadows];
    readonly RandomNumberGenerator _rng = new();
    LevelMap? _map;

    public override void _Ready()
    {
        _kelpMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/abyss_kelp.gdshader") };
        _kelp = new MeshInstance3D { MaterialOverride = _kelpMaterial, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 4f };
        AddChild(_kelp);
        _shadowMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/abyss_shadow.gdshader") };
        _shadowMesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseCustomData = true,
            InstanceCount = Shadows,
            Mesh = new PlaneMesh { Size = new Vector2(1f, 1f), SubdivideWidth = 14, SubdivideDepth = 6 },
        };
        AddChild(new MultiMeshInstance3D
        {
            Multimesh = _shadowMesh,
            MaterialOverride = _shadowMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            ExtraCullMargin = 200f,
        });
        for (int i = 0; i < Shadows; i++) _shadows[i] = new Shadow();
    }

    /// <summary>A level's abyss: its kelp fields grown, its big fish yet to come; its colours from the depth's look.</summary>
    public void Build(LevelMap map, ReefLook look)
    {
        _map = map.Abyss ? map : null;
        Visible = _map is not null;
        foreach (var s in _shadows) s.Live = false;
        _rng.Seed = (ulong)map.Seed ^ 0xA6155EEDul ^ ((ulong)map.Id.Depth << 20) ^ ((ulong)map.Id.Level << 28);
        _kelp.Mesh = _map is null ? null : GrowKelp(map);
        _kelpMaterial.SetShaderParameter(SN.Of("base_col"), look.Grass.Darkened(0.72f).Lerp(look.Rock, 0.35f));
        _kelpMaterial.SetShaderParameter(SN.Of("tip_col"), look.Grass.Lerp(look.Butter, 0.15f).Darkened(0.42f));
        _shadowMaterial.SetShaderParameter(SN.Of("body_col"), look.Murk.Darkened(0.6f));
        for (int i = 0; i < Shadows; i++) Place(i);
    }

    public void Clear()
    {
        _map = null;
        _kelp.Mesh = null;
        Visible = false;
    }

    /// <summary>Whether the abyss floor lies fully dropped at a point (nothing above it but water).</summary>
    bool Deep(LevelMap map, System.Numerics.Vector2 p, float below) =>
        p.X > 2f && p.Y > 2f && p.X < LevelMap.Size - 2f && p.Y < LevelMap.Size - 2f && map.HeightAt(p) < below;

    ArrayMesh? GrowKelp(LevelMap map)
    {
        // Where fields can grow: the deep floor, clear of the way down, apart from each other; some of it left bare.
        var spots = new List<System.Numerics.Vector2>();
        for (float y = 4f; y < LevelMap.Size - 4f; y += 4f)
        for (float x = 4f; x < LevelMap.Size - 4f; x += 4f)
        {
            var p = new System.Numerics.Vector2(x + _rng.RandfRange(-1.5f, 1.5f), y + _rng.RandfRange(-1.5f, 1.5f));
            if (Deep(map, p, FieldFloor) && System.Numerics.Vector2.Distance(p, map.Shaft.Center) > ShaftClear) spots.Add(p);
        }
        for (int i = spots.Count - 1; i > 0; i--)
        {
            int j = _rng.RandiRange(0, i);
            (spots[i], spots[j]) = (spots[j], spots[i]);
        }
        var fields = new List<System.Numerics.Vector2>();
        foreach (var p in spots)
        {
            if (fields.Count >= MaxFields) break;
            if (_rng.Randf() < 0.3f) continue;
            bool apart = true;
            foreach (var f in fields) apart &= System.Numerics.Vector2.Distance(f, p) >= FieldSpacing;
            if (apart) fields.Add(p);
        }
        if (fields.Count == 0) return null;

        var verts = new List<Vector3>();
        var colors = new List<Color>();
        var uvs = new List<Vector2>();
        var uv2s = new List<Vector2>();
        var indices = new List<int>();
        foreach (var centre in fields)
        {
            float radius = _rng.RandfRange(FieldMin, FieldMax);
            int strands = (int)(Mathf.Pi * radius * radius * StrandDensity * 0.35f) + 6;
            for (int k = 0; k < strands; k++)
            {
                float a = _rng.RandfRange(0f, Mathf.Tau), d = radius * Mathf.Sqrt(_rng.Randf());
                var root = centre + new System.Numerics.Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
                if (!Deep(map, root, FieldFloor + 2f)) continue;
                float floor = map.HeightAt(root);
                // Tallest in the field's middle; never up near her plane.
                float length = Mathf.Min(_rng.RandfRange(StrandMin, StrandMax) * (1f - 0.35f * d / radius), TipTop - floor);
                float width = _rng.RandfRange(0.8f, 1.4f);
                float seed = _rng.Randf(), shade = _rng.Randf();
                // Turned mostly to the camera (it looks north), a little either way; leaning a little.
                float turn = _rng.RandfRange(-0.5f, 0.5f);
                var side = new Vector3(Mathf.Cos(turn), 0f, Mathf.Sin(turn));
                var lean = new Vector3(_rng.RandfRange(-0.12f, 0.12f), 1f, _rng.RandfRange(-0.12f, 0.12f)).Normalized();
                int first = verts.Count;
                for (int i = 0; i <= Segments; i++)
                {
                    float t = i / (float)Segments;
                    // A gentle curve as it rises, the blade narrowing to its tip.
                    var c = new Vector3(root.X, floor, root.Y) + lean * length * t + side * (Mathf.Sin(t * 2.2f + seed * 9f) * 0.4f * t);
                    float w = width * Mathf.Lerp(0.5f, 1f, Mathf.SmoothStep(0f, 0.25f, t)) * (1f - 0.55f * Mathf.SmoothStep(0.75f, 1f, t));
                    foreach (float s in new[] { -0.5f, 0.5f })
                    {
                        verts.Add(c + side * (s * w));
                        colors.Add(new Color(seed, shade, 0f, t));
                        uvs.Add(new Vector2(s + 0.5f, length * t));
                        uv2s.Add(new Vector2(length, 0f));
                    }
                    if (i > 0)
                    {
                        int v = first + (i - 1) * 2;
                        indices.AddRange(new[] { v, v + 2, v + 1, v + 1, v + 2, v + 3 });
                    }
                }
            }
        }
        if (indices.Count == 0) return null;
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = verts.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();
        arrays[(int)Mesh.ArrayType.TexUV] = uvs.ToArray();
        arrays[(int)Mesh.ArrayType.TexUV2] = uv2s.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    /// <summary>One frame: the big fish cruise on; one wandered too far fades away and another comes in somewhere else.</summary>
    public void Sync(PlaneWorld world, float dt)
    {
        if (_map is null) return;
        var her = new Vector2(world.Player.Position.X, world.Player.Position.Y);
        for (int i = 0; i < Shadows; i++)
        {
            var s = _shadows[i];
            if (!s.Live)
            {
                if (!Spawn(s, her)) { Place(i); continue; }
            }
            s.Life -= dt;
            if (s.Life <= 0f || s.At.DistanceTo(her) > Leave) s.Leaving = true;
            s.Fade = Mathf.MoveToward(s.Fade, s.Leaving ? 0f : 1f, dt / 2.5f);
            if (s.Leaving && s.Fade <= 0f) { s.Live = false; Place(i); continue; }
            Steer(s, her, dt);
            s.At += s.Heading * s.Speed * dt;
            Place(i);
        }
    }

    /// <summary>A big fish comes in, out of sight round her, over the deep; false when there is no room for one.</summary>
    bool Spawn(Shadow s, Vector2 her)
    {
        for (int attempt = 0; attempt < 12; attempt++)
        {
            float a = _rng.RandfRange(0f, Mathf.Tau), d = _rng.RandfRange(SpawnNear, SpawnFar);
            var at = her + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
            s.Ray = _rng.Randf() < 0.4f;
            s.Y = _rng.RandfRange(ShadowBottom, ShadowTop);
            if (!Clear(at, s.Y)) continue;
            // Crossing her way, more or less (toward her, turned aside), along water clear ahead of it.
            var toward = (her - at).Normalized();
            bool open = false;
            for (int k = 0; k < 6 && !open; k++)
            {
                s.Heading = toward.Rotated(_rng.RandfRange(-1.1f, 1.1f));
                open = Clear(at + s.Heading * LookAhead, s.Y) && Clear(at + s.Heading * LookAhead * 2f, s.Y);
            }
            if (!open) continue;
            s.At = at;
            s.Length = s.Ray ? _rng.RandfRange(3.5f, 5f) : _rng.RandfRange(5f, 8f);
            s.Width = s.Ray ? s.Length * 1.7f : s.Length * 0.42f;
            s.Speed = s.Ray ? _rng.RandfRange(1f, 1.6f) : _rng.RandfRange(1.3f, 2.4f);
            s.Beat = s.Ray ? _rng.RandfRange(1.2f, 1.7f) : _rng.RandfRange(2f, 3f) * 2.2f / s.Speed;
            s.Phase = _rng.RandfRange(0f, Mathf.Tau);
            s.Wander = _rng.RandfRange(0f, 100f);
            s.Turn = 0f;
            s.Life = _rng.RandfRange(20f, 40f);
            s.Fade = 0f;
            s.Leaving = false;
            s.Live = true;
            return true;
        }
        return false;
    }

    /// <summary>Whether a big fish at this depth has water under it here (and the rock well below).</summary>
    bool Clear(Vector2 at, float y) => Deep(_map!, new System.Numerics.Vector2(at.X, at.Y), y - Keel);

    /// <summary>It wanders, turning slowly; it turns away from rock ahead, and back toward her when it strays.</summary>
    void Steer(Shadow s, Vector2 her, float dt)
    {
        s.Wander += dt * 0.35f;
        float want = Mathf.Sin(s.Wander) * 0.25f + Mathf.Sin(s.Wander * 2.3f + 1.7f) * 0.12f;
        bool near = !Clear(s.At + s.Heading * LookAhead * 0.5f, s.Y);
        if (near || !Clear(s.At + s.Heading * LookAhead, s.Y))
        {
            // The side with water (looking wider if both are shut), harder the closer the rock.
            int left = Open(s, -0.7f) + Open(s, -1.4f), right = Open(s, 0.7f) + Open(s, 1.4f);
            float hard = near ? 1.6f : 1f;
            want = (left > right ? -hard : right > left ? hard : s.Turn >= 0f ? hard : -hard);
        }
        else if (s.At.DistanceTo(her) > Stray && !s.Leaving)
            want += 0.5f * Mathf.Sign(s.Heading.Cross(her - s.At));
        s.Turn = Mathf.MoveToward(s.Turn, want, dt * 3f);
        s.Heading = s.Heading.Rotated(s.Turn * dt).Normalized();
        // Rock up to its depth after all (it could not turn in time): it fades away.
        if (!Clear(s.At, s.Y + Keel)) s.Leaving = true;
    }

    int Open(Shadow s, float turn) => Clear(s.At + s.Heading.Rotated(turn) * LookAhead, s.Y) ? 1 : 0;

    void Place(int i)
    {
        var s = _shadows[i];
        if (!s.Live || s.Fade <= 0f)
        {
            _shadowMesh.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(Vector3.One * 0.001f), new Vector3(0f, -100f, 0f)));
            _shadowMesh.SetInstanceCustomData(i, new Color(0f, 0f, 0f, 0f));
            return;
        }
        var along = new Vector3(s.Heading.X, 0f, s.Heading.Y);
        var across = new Vector3(-s.Heading.Y, 0f, s.Heading.X);
        var basis = new Basis(along * s.Length, Vector3.Up * s.Width, across * s.Width);
        _shadowMesh.SetInstanceTransform(i, new Transform3D(basis, new Vector3(s.At.X, s.Y, s.At.Y)));
        _shadowMesh.SetInstanceCustomData(i, new Color(s.Ray ? 1f : 0f, s.Phase, s.Fade, s.Beat));
    }
}
