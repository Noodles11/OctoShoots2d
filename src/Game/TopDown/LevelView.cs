using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Game.Fx;
using OctoShoots.Game.Util;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Everything about a level's look that takes time to work out — the ground's meshes, the cave roofs, the flora —
/// prepared off the main thread (<see cref="Prepare"/>), so the level below can be readied while she plays.
/// </summary>
public sealed class LevelShape
{
    public sealed record Surface(Vector3[] Vertices, Vector3[] Normals, Color[] Colors, int[] Indices);

    public LevelMap Map { get; private init; } = null!;
    public List<Surface> Ground { get; } = new();
    /// <summary>Per cave, its roof (null where the roof is flush with the ground).</summary>
    public List<Surface?> Lids { get; } = new();
    public List<FloraItem> Flora { get; } = new();
    public List<Transform3D> Rocks { get; } = new();
    public List<Transform3D> Glows { get; } = new();

    /// <summary>Pure work on the map: safe to run on any thread.</summary>
    public static LevelShape Prepare(LevelMap map)
    {
        var shape = new LevelShape { Map = map };
        const int chunk = 32;
        for (int cy = 0; cy < LevelMap.Size; cy += chunk)
        for (int cx = 0; cx < LevelMap.Size; cx += chunk)
            shape.Ground.Add(LevelView.ChunkSurface(map, cx, cy, Math.Min(cx + chunk, LevelMap.Size), Math.Min(cy + chunk, LevelMap.Size))!);
        foreach (var cave in map.Caves) shape.Lids.Add(LevelView.LidSurface(map, cave));
        if (!OS.GetCmdlineUserArgs().Contains("--dbg-nodecor")) LevelView.DecorItems(map, shape.Flora, shape.Rocks, shape.Glows);
        return shape;
    }
}

/// <summary>
/// Shows a <see cref="LevelMap"/>: the heightfield as static 32 m chunks coloured by height band, the canopy layer
/// (arches, cave roofs, overhangs) that fades while Clementine is underneath, the POIs in the landmark colour
/// language, weak rock, and the decoration the generator chose. Purely presentation: nothing here generates,
/// randomises or decides placement.
/// The level below (DESIGN-TOPDOWN §4.6) is shown by a second LevelView placed under the hole (<see cref="Place"/>), drawn
/// only through the shaft (<see cref="SetPortal"/>) until she dives; then it is promoted (<see cref="Promote"/>).
/// </summary>
public partial class LevelView : Node3D
{

    /// <summary>Landmark light language (DESIGN-TOPDOWN §4.2, THEME-BIBLE §6.3).</summary>
    public static Color PoiColor(PoiKind kind) => kind switch
    {
        PoiKind.TreasureCave => new Color(1f, 0.78f, 0.25f),   // gold
        PoiKind.Shop => new Color(0.35f, 0.95f, 0.45f),        // green
        PoiKind.CurseDen => new Color(1f, 0.25f, 0.25f),       // red
        PoiKind.Secret => new Color(0.7f, 0.4f, 1f),           // violet (no beacon)
        PoiKind.Exit => new Color(1f, 0.55f, 0.15f),           // orange (the Crack, the way down)
        PoiKind.Start => new Color(0.95f, 0.95f, 0.95f),       // white
        PoiKind.ShellCache => new Color(0.3f, 0.9f, 0.85f),    // teal (the treasure pool's colour)
        _ => new Color(1f, 0.45f, 0.3f),                       // ambushes: coral, warm but not gold
    };


    LevelMap _map = null!;
    ShaderMaterial _ground = null!;
    /// <summary>Every material of the level's ground, rock and flora: they all take its frame and the dive's portal.</summary>
    readonly List<ShaderMaterial> _materials = new();
    /// <summary>Drawn below the current level: no Crack of its own yet.</summary>
    bool _below;
    readonly List<(Canopy Canopy, ShaderMaterial Material, float Alpha)> _canopies = new();
    readonly List<ShaderMaterial> _lids = new();
    Shader _rock = null!;

    /// <summary>Free-standing reef rock in the reef's skin, lumped by <paramref name="lumps"/> metres (reef_rock.gdshader).</summary>
    ShaderMaterial Rock(float lumps, Color? tint = null, float cracks = 0f)
    {
        var m = new ShaderMaterial { Shader = _rock };
        Track(m);
        m.SetShaderParameter("lumps", lumps);
        m.SetShaderParameter("tint", tint ?? Colors.White);
        m.SetShaderParameter("cracks", cracks);
        return m;
    }
    FloraViews _flora = null!;

    public override void _Ready()
    {
        _flora = new FloraViews { Unlimited = true };
        AddChild(_flora);
        _ground = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/topdown_ground.gdshader") };
        _rock = GD.Load<Shader>("res://assets/shaders/reef_rock.gdshader");
        if (OS.GetCmdlineUserArgs().Contains("--dbg-normals")) _ground.SetShaderParameter("debug_normals", true);
    }

    /// <summary>Shows a level: the current one, or (<paramref name="below"/>) the one under the hole, without its Crack.</summary>
    public void Show(LevelShape shape, bool below = false)
    {
        foreach (var child in GetChildren())
            if (child != _flora) child.QueueFree();
        _canopies.Clear();
        _materials.Clear();
        Track(_ground);
        foreach (var m in _flora.Materials) Track(m);
        var map = _map = shape.Map;
        _below = below;

        foreach (var surface in shape.Ground) AddChild(new MeshInstance3D { Mesh = ToMesh(surface), MaterialOverride = _ground });

        _lids.Clear();
        for (int i = 0; i < map.Caves.Count; i++)
        {
            var lid = Rock(0f);
            _lids.Add(lid);
            if (shape.Lids[i] is { } surface) AddChild(new MeshInstance3D { Mesh = ToMesh(surface), MaterialOverride = lid });
        }
        foreach (var canopy in map.Canopies) AddCanopy(canopy);
        // Weak rock plugging a secret: paler, fractured (an ink bomb will open it).
        foreach (var rock in map.WeakRocks)
        {
            AddChild(new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = rock.Radius, Height = rock.Radius * 2.4f, RadialSegments = 20, Rings = 10 },
                MaterialOverride = Rock(0.25f, new Color(1.18f, 1.1f, 1.05f), cracks: 1f),
                Position = new Vector3(rock.Center.X, map.HeightAt(rock.Center) + rock.Radius * 0.8f, rock.Center.Y),
            });
        }
        ShowDecor(shape);
        foreach (var m in _materials) m.SetShaderParameter("crack_mask", below ? 0f : 1f);
        if (!below) ShowCrack(map);
        if (OS.GetCmdlineUserArgs().Contains("--dbg-nocanopy")) foreach (var c in _canopies) c.Material.SetShaderParameter("fade", 0f);
    }

    void Track(ShaderMaterial m)
    {
        if (!_materials.Contains(m)) _materials.Add(m);
    }

    /// <summary>
    /// Where the level sits in the world (its offset) and its absolute origin, which its patterns are drawn from: a
    /// level looks the same wherever it is placed, so moving it back to the origin after the dive changes nothing.
    /// </summary>
    public void Place(Vector3 offset, Vector2 origin)
    {
        Position = offset;
        foreach (var m in _materials)
        {
            m.SetShaderParameter("level_offset", offset);
            m.SetShaderParameter("level_origin", origin);
        }
    }

    /// <summary>The level's corruption on its floor (docs/CORRUPTION.md): the field's texture, or null for none.</summary>
    public void SetCorruption(Texture2D? texture)
    {
        foreach (var m in _materials)
        {
            m.SetShaderParameter("corruption_map", texture is null ? default(Variant) : texture);
            m.SetShaderParameter("corruption_on", texture is null ? 0f : 1f);
        }
    }

    /// <summary>The dive's portal: world xz centre, radius, and mode (0 off, 1 cut away inside, 2 only the inside).</summary>
    public void SetPortal(Vector2 centre, float radius, int mode)
    {
        foreach (var m in _materials) m.SetShaderParameter("portal", new Vector4(centre.X, centre.Y, radius, mode));
    }

    /// <summary>The level below becomes the current one, back at the origin: its Crack, its lights.</summary>
    public void Promote(Vector2 origin)
    {
        _below = false;
        Place(Vector3.Zero, origin);
        SetPortal(Vector2.Zero, 0f, 0);
        foreach (var m in _materials) m.SetShaderParameter("crack_mask", 1f);
        ShowCrack(_map);
    }

    static ArrayMesh ToMesh(LevelShape.Surface surface)
    {
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = surface.Vertices;
        arrays[(int)Mesh.ArrayType.Normal] = surface.Normals;
        arrays[(int)Mesh.ArrayType.Color] = surface.Colors;
        arrays[(int)Mesh.ArrayType.Index] = surface.Indices;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    // ───────────────────────── ground ─────────────────────────

    /// <summary>Mesh vertices per metre: half-metre steps keep steep walls and summits smooth.</summary>
    const int Sub = 2;

    /// <summary>
    /// Colour by height: the seabed under the swim level by depth (sand shallows, sea-green mid water, blue-black deeps),
    /// the reef wall breaking the swim level, steep reef rock, coralline peaks, the dark rim.
    /// </summary>
    static Color Band(float h, float x, float y)
    {
        Color c;
        if (h < 0f)
        {
            float depth = -h;
            c = depth <= 4f ? new Color(0.8f, 0.72f, 0.53f).Lerp(new Color(0.55f, 0.6f, 0.47f), depth / 4f)
                : depth <= 10f ? new Color(0.55f, 0.6f, 0.47f).Lerp(new Color(0.22f, 0.36f, 0.4f), (depth - 4f) / 6f)
                : new Color(0.22f, 0.36f, 0.4f).Lerp(new Color(0.04f, 0.1f, 0.17f), Mathf.Clamp((depth - 10f) / 8f, 0f, 1f));
        }
        else if (h <= 6f) c = new Color(0.5f, 0.54f, 0.36f).Lerp(new Color(0.43f, 0.4f, 0.33f), h / 6f);
        else if (h <= 10f) c = new Color(0.43f, 0.4f, 0.33f).Lerp(new Color(0.4f, 0.36f, 0.32f), (h - 6f) / 4f);
        else c = new Color(0.4f, 0.36f, 0.32f).Lerp(new Color(0.66f, 0.43f, 0.52f), Mathf.Clamp((h - 10f) / 4f, 0f, 1f));
        float e = Mathf.Min(Mathf.Min(x, y), Mathf.Min(LevelMap.Size - x, LevelMap.Size - y));
        if (e < LevelMap.RimWidth) c = c.Lerp(new Color(0.24f, 0.23f, 0.26f), 1f - e / LevelMap.RimWidth);
        // A little grain so flat colour reads as ground.
        uint hash = unchecked((uint)((int)(x * Sub) * 73856093 ^ (int)(y * Sub) * 19349663));
        float grain = 0.95f + 0.1f * ((hash & 1023) / 1023f);
        return new Color(c.R * grain, c.G * grain, c.B * grain);
    }

    /// <summary>
    /// The terrain as a smooth surface: bicubic (Catmull-Rom) through the height samples, so it still passes through
    /// every grid point the simulation collides against, but without creases between them. Around the shaft it is
    /// plain bilinear: its walls drop sheer, without the bicubic's ripples.
    /// </summary>
    static float SmoothHeight(LevelMap map, float x, float y) =>
        map.Shaft.Contains(new System.Numerics.Vector2(x, y), 3f) ? map.HeightAt(new System.Numerics.Vector2(x, y)) : SmoothSample((i, j) => map[i, j], x, y);

    /// <summary>The cave roof where there is one, the terrain elsewhere (so a roof's edge meets the ground exactly).</summary>
    static float SmoothRoof(LevelMap map, float x, float y) => SmoothSample((i, j) => float.IsNaN(map.LidAt(i, j)) ? map[i, j] : map.LidAt(i, j), x, y);

    static float SmoothSample(Func<int, int, float> grid, float x, float y)
    {
        x = Mathf.Clamp(x, 0f, LevelMap.Size);
        y = Mathf.Clamp(y, 0f, LevelMap.Size);
        int ix = Math.Min((int)x, LevelMap.Size - 1), iy = Math.Min((int)y, LevelMap.Size - 1);
        float fx = x - ix, fy = y - iy;
        float Row(int j)
        {
            int yy = Math.Clamp(j, 0, LevelMap.Size);
            float H(int i) => grid(Math.Clamp(i, 0, LevelMap.Size), yy);
            return CatmullRom(H(ix - 1), H(ix), H(ix + 1), H(ix + 2), fx);
        }
        return CatmullRom(Row(iy - 1), Row(iy), Row(iy + 1), Row(iy + 2), fy);
    }

    static float CatmullRom(float p0, float p1, float p2, float p3, float t) =>
        0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (3f * p1 - p0 - 3f * p2 + p3) * t * t * t);

    /// <summary>A chunk of the ground. The shaft has no floor: through it, the level below shows.</summary>
    internal static LevelShape.Surface? ChunkSurface(LevelMap map, int x0, int y0, int x1, int y1) =>
        SurfaceMesh((x, y) => SmoothHeight(map, x, y), x0, y0, x1, y1, (x, y) => SmoothHeight(map, x, y) > LevelMap.ShaftBottom + 0.3f);

    /// <summary>
    /// A cave's roof: the mountain as it stood before the cave was hollowed out, over the chambers and a little past them
    /// (where it meets the terrain again). It fades like any canopy while Clementine is inside.
    /// </summary>
    internal static LevelShape.Surface? LidSurface(LevelMap map, CaveSite cave)
    {
        int x0 = Math.Max(0, (int)cave.Chambers.Min(c => c.Center.X - c.Radius) - 4), x1 = Math.Min(LevelMap.Size, (int)cave.Chambers.Max(c => c.Center.X + c.Radius) + 5);
        int y0 = Math.Max(0, (int)cave.Chambers.Min(c => c.Center.Y - c.Radius) - 4), y1 = Math.Min(LevelMap.Size, (int)cave.Chambers.Max(c => c.Center.Y + c.Radius) + 5);
        return SurfaceMesh((x, y) => SmoothRoof(map, x, y) + 0.03f, x0, y0, x1, y1, (x, y) => SmoothRoof(map, x, y) > SmoothHeight(map, x, y) + 0.05f);
    }

    /// <summary>A triangle is kept when any of its corners passes <paramref name="keep"/>.</summary>
    static LevelShape.Surface? SurfaceMesh(Func<float, float, float> height, int x0, int y0, int x1, int y1, Func<float, float, bool>? keep)
    {
        int w = (x1 - x0) * Sub + 1, h = (y1 - y0) * Sub + 1;
        var verts = new Vector3[w * h];
        var normals = new Vector3[w * h];
        var colors = new Color[w * h];
        const float e = 0.35f;
        for (int j = 0; j < h; j++)
        for (int i = 0; i < w; i++)
        {
            float x = x0 + i / (float)Sub, y = y0 + j / (float)Sub;
            float hgt = height(x, y);
            verts[j * w + i] = new Vector3(x, hgt, y);
            float hl = height(x - e, y), hr = height(x + e, y);
            float hd = height(x, y - e), hu = height(x, y + e);
            normals[j * w + i] = new Vector3(hl - hr, 2f * e, hd - hu).Normalized();
            colors[j * w + i] = Band(hgt, x, y);
        }
        var indices = new List<int>((w - 1) * (h - 1) * 6);
        for (int j = 0; j + 1 < h; j++)
        for (int i = 0; i + 1 < w; i++)
        {
            int a = j * w + i, b = a + 1, c = a + w, d = c + 1;
            if (keep is not null && !keep(verts[a].X, verts[a].Z) && !keep(verts[b].X, verts[b].Z) && !keep(verts[c].X, verts[c].Z) && !keep(verts[d].X, verts[d].Z)) continue;
            // Clockwise seen from above (Godot's front face).
            indices.AddRange(new[] { a, b, c, b, d, c });
        }
        if (indices.Count == 0) return null;
        return new LevelShape.Surface(verts, normals, colors, indices.ToArray());
    }

    // ───────────────────────── canopy ─────────────────────────

    void AddCanopy(Canopy c)
    {
        // A cave's roof is the mountain itself (its lid mesh), not a separate piece.
        if (c.Kind == CanopyKind.CaveRoof)
        {
            int cave = CaveOf(c);
            if (cave >= 0) _canopies.Add((c, _lids[cave], 1f));
            return;
        }
        var material = Rock(0.35f);
        if (c.Radius > 0f)
        {
            // An overhang: a lumpy shelf of rock.
            float thick = c.Top - c.Bottom;
            AddChild(new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 28, Rings = 12 },
                Scale = new Vector3(c.Radius * 1.05f, thick * 0.6f, c.Radius * 1.05f),
                Position = new Vector3(c.Center.X, (c.Bottom + c.Top) * 0.5f, c.Center.Y),
                MaterialOverride = material,
            });
        }
        else AddChild(new MeshInstance3D { Mesh = ArchMesh(c), MaterialOverride = material });
        _canopies.Add((c, material, 1f));
    }

    /// <summary>
    /// An arch: a stone bridge springing from the ground at one footing, swelling up over the corridor, and coming down
    /// at the other; thick at the feet, slimmer over the span, with a rounded cross-section (lumped in the shader).
    /// </summary>
    ArrayMesh ArchMesh(Canopy c)
    {
        const int along = 40, around = 14;
        var a = c.EndA;
        var b = c.EndB;
        float groundA = _map.HeightAt(a) - 0.6f, groundB = _map.HeightAt(b) - 0.6f;
        float mid = (c.Bottom + c.Top) * 0.5f, half = (c.Top - c.Bottom) * 0.5f;
        var side = new Vector3(-c.Axis.Y, 0f, c.Axis.X);
        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var indices = new List<int>();
        Vector3 Centre(float t)
        {
            var p = System.Numerics.Vector2.Lerp(a, b, t);
            // Up from the ground fast, a gentle crown over the span, down fast at the far end.
            float rise = Mathf.SmoothStep(0f, 0.2f, t), fall = Mathf.SmoothStep(1f, 0.8f, t);
            float y = Mathf.Lerp(groundA, mid, rise) * (1f - t) + Mathf.Lerp(groundB, mid, fall) * t;
            y = Mathf.Lerp(y, mid + 0.6f * Mathf.Sin(t * Mathf.Pi), rise * fall);
            return new Vector3(p.X, y, p.Y);
        }
        for (int i = 0; i <= along; i++)
        {
            float t = i / (float)along;
            var p = Centre(t);
            var tangent = (Centre(Mathf.Min(t + 0.01f, 1f)) - Centre(Mathf.Max(t - 0.01f, 0f))).Normalized();
            var upish = tangent.Cross(side).Normalized();
            if (upish.Y < 0f) upish = -upish;
            float feet = 1f - Mathf.SmoothStep(0f, 0.25f, Mathf.Min(t, 1f - t));
            float w = c.HalfWidth * (0.95f + 0.45f * feet), hgt = half * (1f + 0.6f * feet);
            for (int j = 0; j < around; j++)
            {
                float ang = j * Mathf.Tau / around;
                // A rounded box (superellipse) cross-section.
                float cx = Mathf.Cos(ang), cy = Mathf.Sin(ang);
                float sx = Mathf.Sign(cx) * Mathf.Pow(Mathf.Abs(cx), 0.6f), sy = Mathf.Sign(cy) * Mathf.Pow(Mathf.Abs(cy), 0.6f);
                verts.Add(p + side * (sx * w) + upish * (sy * hgt));
                normals.Add((side * cx * hgt + upish * cy * w).Normalized());
            }
        }
        for (int i = 0; i < along; i++)
        for (int j = 0; j < around; j++)
        {
            int j1 = (j + 1) % around;
            int p0 = i * around + j, p1 = i * around + j1, p2 = (i + 1) * around + j, p3 = (i + 1) * around + j1;
            indices.AddRange(new[] { p0, p2, p1, p1, p2, p3 });
        }
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = verts.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = normals.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    /// <summary>
    /// The occlusion rule (DESIGN-TOPDOWN §3): canopy over Clementine dissolves while she is underneath and returns when
    /// she leaves. Fade is done; the blur half of "blurs out and dissolves" is still a stub.
    /// </summary>
    public void UpdateCanopy(System.Numerics.Vector2 player, float dt)
    {
        int under = _map.CanopyAt(player);
        // A cave's roof is several pieces: uncover the whole cave at once.
        int cave = under >= 0 && _map.Canopies[under].Kind == CanopyKind.CaveRoof ? CaveOf(_map.Canopies[under]) : -1;
        for (int i = 0; i < _canopies.Count; i++)
        {
            var (c, material, alpha) = _canopies[i];
            bool hide = i == under || (cave >= 0 && c.Kind == CanopyKind.CaveRoof && CaveOf(c) == cave);
            alpha = Mathf.MoveToward(alpha, hide ? 0.12f : 1f, dt * 4f);
            // A dithered dissolve: the rock stays opaque, so the depth focus still blurs it like other high rock.
            material.SetShaderParameter("fade", alpha);
            _canopies[i] = (c, material, alpha);
        }
    }

    int CaveOf(Canopy roof)
    {
        for (int i = 0; i < _map.Caves.Count; i++)
            if (_map.Caves[i].Chambers.Any(ch => ch.Center == roof.Center)) return i;
        return -1;
    }

    // ───────────────────────── the Crack ─────────────────────────

    /// <summary>
    /// The Crack in the rift's arena (THEME-BIBLE §4.1, §6.3: the Crack orange): its fissure glows from inside (the
    /// reef shader, through reef_crack), warm light spills up over the arena floor, and bubbles rise out of it in a slow
    /// updraft — a doorway, not a wound.
    /// </summary>
    void ShowCrack(LevelMap map)
    {
        // A blue hole is plain water; only a boss level's exit is the Crack.
        RenderingServer.GlobalShaderParameterSet("reef_crack", map.HasBoss ? new Vector4(map.Exit.Position.X, map.Exit.Position.Y, 10f, 1f) : Vector4.Zero);
        if (!map.HasBoss) return;
        var rift = map.Exit.Position;
        var floor = new Vector3(rift.X, -6f, rift.Y);
        AddChild(_crackLight = new OmniLight3D
        {
            Position = floor + Vector3.Up * 2.5f,
            LightColor = new Color(1f, 0.5f, 0.18f),
            LightEnergy = 2.2f,
            OmniRange = 13f,
            OmniAttenuation = 1.6f,
            ShadowEnabled = false,
        });
        var bubble = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/plane_bubble.gdshader") };
        AddChild(_crackBubbles = new CpuParticles3D
        {
            Position = floor,
            Amount = 36,
            Lifetime = 5f,
            Preprocess = 5f,
            Mesh = new SphereMesh { Radius = 0.12f, Height = 0.24f, RadialSegments = 8, Rings = 4, Material = bubble },
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(5f, 0.3f, 5f),
            Direction = Vector3.Up,
            Spread = 12f,
            Gravity = new Vector3(0f, 0.6f, 0f),
            InitialVelocityMin = 0.6f,
            InitialVelocityMax = 1.4f,
            ScaleAmountMin = 0.5f,
            ScaleAmountMax = 1.6f,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
    }

    OmniLight3D? _crackLight;
    CpuParticles3D? _crackBubbles;

    /// <summary>
    /// The dive through the Crack: its glow, its warm light and its rising bubbles fade together (1 full, 0 gone), so
    /// nothing of it is switched off when this level is let go.
    /// </summary>
    public void FadeCrack(float k)
    {
        if (!_map.HasBoss) return;
        RenderingServer.GlobalShaderParameterSet("reef_crack", new Vector4(_map.Exit.Position.X, _map.Exit.Position.Y, 10f, k));
        if (_crackLight is not null) _crackLight.LightEnergy = 2.2f * k;
        _crackBubbles?.SetInstanceShaderParameter("fade", k);
    }

    // ───────────────────────── places ─────────────────────────

    /// <summary>The place's name, as the HUD shows it when Clementine enters.</summary>
    public static string PlaceName(PoiKind kind) => kind switch
    {
        PoiKind.ShellCache => "Shell cache",
        PoiKind.TreasureCave => "Treasure cave",
        PoiKind.CurseDen => "Curse den",
        PoiKind.Ambush => "Ambush",
        PoiKind.Exit => "Blue hole",
        _ => kind.ToString(),
    };

    /// <summary>A place's name on this level: a boss level's exit is the Crack.</summary>
    public static string PlaceName(LevelMap map, PoiKind kind) => kind == PoiKind.Exit && map.HasBoss ? "The Crack" : PlaceName(kind);

    // ───────────────────────── decoration ─────────────────────────

    void ShowDecor(LevelShape shape)
    {
        PaintFlora(ReefLook.For(shape.Map.Depth));
        _flora.Show(shape.Flora);
        AddChild(Instances(new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 18, Rings = 9 }, Rock(0.22f), shape.Rocks));
        if (shape.Glows.Count > 0)
            AddChild(Instances(new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 8, Rings = 4 },
                new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(0.4f, 1f, 0.9f), EmissionEnabled = true, Emission = new Color(0.4f, 1f, 0.9f), EmissionEnergyMultiplier = 2f }, shape.Glows));
    }

    /// <summary>Maps the generator's decoration onto the existing flora art. Positions, yaw and scale all come from the map.</summary>
    internal static void DecorItems(LevelMap map, List<FloraItem> items, List<Transform3D> rocks, List<Transform3D> glows)
    {
        foreach (var d in map.Decor)
        {
            var pos = new Vector3(d.Position.X, d.Height, d.Position.Y);
            var basis = new Basis(Vector3.Up, d.Yaw).Scaled(Vector3.One * d.Scale);
            FloraKind? kind = d.Kind switch
            {
                DecorKind.GrassMeadow => FloraKind.TurtleGrass,
                DecorKind.SeaRod => FloraKind.SeaRod,
                DecorKind.SeaFan => FloraKind.SeaFan,
                DecorKind.TubeSponge => FloraKind.TubeSponge,
                DecorKind.CliffCoral => FloraKind.Staghorn,
                DecorKind.EncrustingCoral => FloraKind.BrainCoral,
                DecorKind.Bommie => FloraKind.BrainCoral,
                DecorKind.CrownGarden => FloraKind.SeaFan,
                _ => null,
            };
            if (d.Kind == DecorKind.Boulder) rocks.Add(new Transform3D(basis.Scaled(new Vector3(0.9f, 0.6f, 0.8f)), pos));
            else if (d.Kind == DecorKind.BiolumAccent) glows.Add(new Transform3D(basis.Scaled(Vector3.One * 0.12f), pos + Vector3.Up * 0.3f));
            else if (kind is { } k)
            {
                // Seen from 30 m up, reef life is drawn a little larger than life, so it reads.
                basis = basis.Scaled(Vector3.One * (d.Kind switch
                {
                    DecorKind.Bommie => 2.2f,
                    DecorKind.GrassMeadow => 1.6f,
                    DecorKind.SeaFan or DecorKind.CrownGarden => 1.45f,
                    DecorKind.EncrustingCoral => 1.35f,
                    _ => 1.3f,
                }));
                // Variant (palette a or b) and a little brightness jitter, from the spot's own yaw and scale.
                float variant = Mathf.PosMod(d.Yaw * 3.7f, 1f) < 0.42f ? 1f : 0f;
                float bright = 0.88f + 0.24f * Mathf.PosMod(d.Scale * 13.1f, 1f);
                items.Add(new FloraItem(k, new Transform3D(basis, pos), new Color(variant, bright, 1f, d.Yaw / Mathf.Tau)));
            }
        }
        Scatter(map, items, rocks);
    }

    /// <summary>
    /// Ground cover, for the look only (nothing in play depends on it): a deterministic scatter from the level's seed so
    /// the seabed reads as a living reef rather than a beach (THEME-BIBLE §7, Depth 1: healthy neighbours everywhere).
    /// Seagrass meadows in drifting patches; small corals, sponges and sea rods crowding the feet of the walls; lone
    /// coral heads and bits of rubble out on the open sand. The exit is left clear around the shaft.
    /// </summary>
    static void Scatter(LevelMap map, List<FloraItem> items, List<Transform3D> rocks)
    {
        uint seed = unchecked((uint)(map.Seed ^ (map.Seed >> 32)) ^ (uint)(map.Level * 7919 + map.Depth * 104729 + map.Cycle * 15485863));
        float Hash(float x, float y, int k)
        {
            uint h = unchecked((uint)((int)(x * 10f) * 73856093) ^ (uint)((int)(y * 10f) * 19349663) ^ (uint)(k * 83492791) ^ seed);
            h ^= h >> 13;
            h = unchecked(h * 0x5bd1e995u);
            h ^= h >> 15;
            return (h & 0xFFFFFF) / 16777216f;
        }
        var rift = map.Exit;
        const float step = 1.1f;
        for (float y = LevelMap.RimWidth + 1f; y < LevelMap.Size - LevelMap.RimWidth - 1f; y += step)
        for (float x = LevelMap.RimWidth + 1f; x < LevelMap.Size - LevelMap.RimWidth - 1f; x += step)
        {
            var p = new System.Numerics.Vector2(x + Hash(x, y, 1) * step, y + Hash(x, y, 2) * step);
            float h = map.HeightAt(p);
            // The seabed of the shallows only: not the walls, not the deeps.
            if (h > -0.6f || h < -6f) continue;
            float slope = map.Gradient(p).Length();
            if (slope > 0.9f) continue;
            if (map.HasBoss ? System.Numerics.Vector2.Distance(p, rift.Position) < rift.Radius * 0.6f : map.Shaft.Contains(p, 1.5f)) continue;
            bool nearWall = false;
            for (int i = 0; i < 6 && !nearWall; i++)
            {
                float a = i * Mathf.Tau / 6f;
                nearWall = map.HeightAt(p + new System.Numerics.Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2.6f) > 0.2f;
            }
            float meadow = TerrainNoise.Fbm(p.X * 0.045f, p.Y * 0.045f, seed, 3);
            float r = Hash(x, y, 3), yaw = Hash(x, y, 4) * Mathf.Tau, size = Hash(x, y, 5);
            var at = new Vector3(p.X, h - 0.05f, p.Y);
            var tint = new Color(Hash(x, y, 6) < 0.42f ? 1f : 0f, 0.86f + 0.26f * Hash(x, y, 7), 1f, Hash(x, y, 8));
            FloraItem Item(FloraKind kind, float scale) => new(kind, new Transform3D(new Basis(Vector3.Up, yaw).Scaled(Vector3.One * scale), at), tint);
            if (nearWall && r < 0.32f)
            {
                // The reef's foot: a crowd of small life.
                float pick = Hash(x, y, 9);
                var kind = pick < 0.3f ? FloraKind.BrainCoral : pick < 0.55f ? FloraKind.TubeSponge : pick < 0.78f ? FloraKind.Staghorn : FloraKind.SeaRod;
                items.Add(Item(kind, kind == FloraKind.BrainCoral ? 0.55f + 0.4f * size : 0.75f + 0.5f * size));
            }
            else if (meadow > 0.1f && slope < 0.35f && r < 0.85f * Mathf.SmoothStep(0.1f, 0.35f, meadow))
                items.Add(Item(FloraKind.TurtleGrass, 1.9f + 1.1f * size));
            else if (slope < 0.3f && r < 0.012f)
                items.Add(Item(FloraKind.BrainCoral, 1.1f + 0.9f * size));
            else if (slope < 0.4f && r > 0.988f)
                rocks.Add(new Transform3D(new Basis(Vector3.Up, yaw).Scaled(new Vector3(0.35f, 0.22f, 0.3f) * (0.7f + 0.8f * size)), at + Vector3.Up * 0.05f));
        }
    }

    /// <summary>The depth's healthy reef palette on the flora (THEME-BIBLE §6.3); soft things sway like water.</summary>
    void PaintFlora(ReefLook look)
    {
        static Color Dark(Color c, float k = 0.42f) => new(c.R * k, c.G * k, c.B * k);
        _flora.SetPalette(FloraKind.TurtleGrass, Dark(look.Grass, 0.4f), look.Grass, Dark(look.Grass, 0.45f), look.Grass.Lerp(look.Butter, 0.35f), sway: 0.16f);
        _flora.SetPalette(FloraKind.SeaFan, Dark(look.FanA), look.FanA, Dark(look.FanB), look.FanB, sway: 0.09f);
        _flora.SetPalette(FloraKind.SeaRod, Dark(look.Coral2), look.Coral2.Lerp(look.FanB, 0.3f), Dark(look.SoftCoral), look.SoftCoral, sway: 0.1f);
        _flora.SetPalette(FloraKind.TubeSponge, Dark(look.Sponge, 0.35f), look.Sponge, Dark(look.Coral2, 0.35f), look.Coral2);
        _flora.SetPalette(FloraKind.Staghorn, Dark(look.SoftCoral, 0.5f), look.SoftCoral, Dark(look.HardCoral, 0.5f), look.HardCoral);
        _flora.SetPalette(FloraKind.Elkhorn, Dark(look.HardCoral, 0.5f), look.HardCoral, Dark(look.SoftCoral, 0.5f), look.SoftCoral);
        _flora.SetPalette(FloraKind.BrainCoral, Dark(look.HardCoral, 0.5f), look.HardCoral, Dark(look.SoftCoral, 0.5f), look.SoftCoral.Lerp(look.HardCoral, 0.45f));
        _flora.SetPalette(FloraKind.BarrelSponge, Dark(look.Coral, 0.35f), look.Coral.Lerp(look.Rock, 0.4f), Dark(look.Coral2, 0.35f), look.Coral2.Lerp(look.Rock, 0.4f));
    }

    static MultiMeshInstance3D Instances(Mesh mesh, Material material, List<Transform3D> transforms)
    {
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = mesh, InstanceCount = transforms.Count };
        for (int i = 0; i < transforms.Count; i++) mm.SetInstanceTransform(i, transforms[i]);
        return new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
    }
}
