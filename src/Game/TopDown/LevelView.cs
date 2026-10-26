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
    /// <summary>Per roofed corridor (a shortcut tunnel), by its index, its roof.</summary>
    public Dictionary<int, Surface> TunnelRoofs { get; } = new();
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
        // The reef carries on past the rim, so swimming along the edge never shows the void beyond the square.
        var spans = new List<(int A, int B, bool Inside)> { (-LevelView.Skirt, 0, false) };
        for (int c = 0; c < LevelMap.Size; c += chunk) spans.Add((c, Math.Min(c + chunk, LevelMap.Size), true));
        spans.Add((LevelMap.Size, LevelMap.Size + LevelView.Skirt, false));
        foreach (var sy in spans)
        foreach (var sx in spans)
            if (!sx.Inside || !sy.Inside) shape.Ground.Add(LevelView.SkirtSurface(map, sx.A, sy.A, sx.B, sy.B)!);
        foreach (var cave in map.Caves) shape.Lids.Add(LevelView.LidSurface(map, cave));
        for (int c = 0; c < map.Corridors.Count; c++)
            if (map.Corridors[c].Roofed && LevelView.TunnelRoofSurface(map, map.Corridors[c]) is { } roof) shape.TunnelRoofs[c] = roof;
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
    readonly Dictionary<int, ShaderMaterial> _tunnelRoofs = new();
    Shader _rock = null!;

    /// <summary>Free-standing reef rock in the reef's skin, lumped by <paramref name="lumps"/> metres (reef_rock.gdshader).</summary>
    ShaderMaterial Rock(float lumps, Color? tint = null)
    {
        var m = new ShaderMaterial { Shader = _rock };
        Track(m);
        m.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("lumps"), lumps);
        m.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("tint"), tint ?? Colors.White);
        return m;
    }
    FloraViews _flora = null!;
    readonly List<(System.Numerics.Vector2 At, MeshInstance3D Wall, MeshInstance3D? Remains, float Height)> _seals = new();

    /// <summary>A sealed wall's slide down into the sand: how long it takes, and how far down it has gone at t (0..1 of its height).</summary>
    public const float SlideSeconds = 2.2f;

    /// <summary>
    /// The slide's curve: a jolt as it gives (a few centimetres), a moment's strain, then down — slow to start, as heavy rock
    /// is, steady, easing as it beds into the sand.
    /// </summary>
    public static float SlideAt(float t)
    {
        t = Mathf.Clamp(t, 0f, 1f);
        if (t < 0.08f) return 0.015f * Mathf.Sin(t / 0.08f * Mathf.Pi);
        float u = (t - 0.08f) / 0.92f;
        return u * u * (3f - 2f * u);
    }

    /// <summary>
    /// A sealed wall slides down into the sand: its stretch of wall shudders as it gives, then sinks out of sight. A long
    /// one opens only its gate: what stands either side of it (its remains) is there at once, and the wall sinks behind it,
    /// so only the gate is seen going down.
    /// </summary>
    public void Slide(System.Numerics.Vector2 at) => Move(at, up: false);

    /// <summary>The stretch of wall a seal stands as (its carvings are cut in it), if it has one.</summary>
    public MeshInstance3D? SealWall(System.Numerics.Vector2 at) => _seals.Find(s => s.At == at).Wall;

    /// <summary>A barred boss arena's way in rises back up out of the sand: the slide played backwards.</summary>
    public void Raise(System.Numerics.Vector2 at) => Move(at, up: true);

    void Move(System.Numerics.Vector2 at, bool up)
    {
        int i = _seals.FindIndex(s => s.At == at);
        if (i < 0) return;
        var (_, wall, remains, height) = _seals[i];
        if (remains is not null) remains.Visible = true;
        wall.Visible = true;
        var tween = CreateTween();
        tween.TweenMethod(Callable.From<float>(t =>
        {
            // A tremor all through it as it grinds.
            float shake = t < 0.95f ? 0.035f * Mathf.Sin(t * 170f) * (1f - t) : 0f;
            wall.Position = new Vector3(shake, -(height + 0.3f) * SlideAt(up ? 1f - t : t), shake * 0.6f);
        }), 0f, 1f, SlideSeconds);
        // Down, it is gone; up, it is whole again (its remains are part of it).
        tween.TweenCallback(Callable.From(() =>
        {
            wall.Visible = up;
            if (up && remains is not null) remains.Visible = false;
        }));
    }

    public override void _Ready()
    {
        _flora = new FloraViews { Unlimited = true };
        AddChild(_flora);
        _ground = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/topdown_ground.gdshader") };
        _rock = GD.Load<Shader>("res://assets/shaders/reef_rock.gdshader");
        if (OS.GetCmdlineUserArgs().Contains("--dbg-normals")) _ground.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("debug_normals"), true);
    }

    /// <summary>Shows a level: the current one, or (<paramref name="below"/>) the one under the hole, without its Crack.</summary>
    public void Show(LevelShape shape, bool below = false)
    {
        foreach (var child in GetChildren())
            if (child != _flora) child.QueueFree();
        _canopies.Clear();
        _materials.Clear();
        _seeThrough = default;
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
        _tunnelRoofs.Clear();
        foreach (var (corridor, surface) in shape.TunnelRoofs)
        {
            var roof = Rock(0f);
            _tunnelRoofs[corridor] = roof;
            AddChild(new MeshInstance3D { Mesh = ToMesh(surface), MaterialOverride = roof });
        }
        foreach (var canopy in map.Canopies) AddCanopy(canopy);
        // Sealed walls: the reef wall carried on across their passage (LevelMap.Sealed), in the reef's own ground, so each is
        // one more stretch of wall. SealView carves its emblem; Slide brings it down.
        _seals.Clear();
        foreach (var rock in map.WeakRocks)
        {
            if (SealSurface(map, rock) is not { } surface) continue;
            var wall = new MeshInstance3D { Mesh = ToMesh(surface), MaterialOverride = _ground };
            AddChild(wall);
            // A long one's remains (what stands either side of its gate once it opens), hidden till then.
            MeshInstance3D? remains = null;
            if (rock.Gate < rock.Radius && RemainsSurface(map, rock) is { } rest)
            {
                remains = new MeshInstance3D { Mesh = ToMesh(rest), MaterialOverride = _ground, Visible = false };
                AddChild(remains);
            }
            _seals.Add((rock.Center, wall, remains, SealDrop(map, rock)));
        }
        ShowDecor(shape);
        _cover = null;
        _sandFall = null;
        _crackOpen = 0f;
        _crackFade = 1f;
        if (map.HasBoss) ShowCover(map);
        foreach (var m in _materials) m.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("crack_mask"), below ? 0f : 1f);
        if (!below) ShowCrack(map);
        if (OS.GetCmdlineUserArgs().Contains("--dbg-nocanopy")) foreach (var c in _canopies) c.Material.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("fade"), 0f);
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
            m.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("level_offset"), offset);
            m.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("level_origin"), origin);
        }
    }

    /// <summary>The level's corruption on its floor (docs/CORRUPTION.md): the field's texture, or null for none.</summary>
    public void SetCorruption(Texture2D? texture)
    {
        foreach (var m in _materials)
        {
            m.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("corruption_map"), texture is null ? default(Variant) : texture);
            m.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("corruption_on"), texture is null ? 0f : 1f);
        }
    }

    /// <summary>The dive's portal: world xz centre, radius, and mode (0 off, 1 cut away inside, 2 only the inside).</summary>
    /// <summary>
    /// In a curse den's tunnel: rock between the camera and the tunnel round her (world xz, this far round, at her plane's
    /// height) thins away by this much (0 none), so the walls in front never hide the way (level_frame's lf_see_cut).
    /// </summary>
    public void SeeThrough(Vector3 her, float radius, float amount)
    {
        var v = new Vector4(her.X, her.Z, radius, amount);
        if (v == _seeThrough) return;
        _seeThrough = v;
        foreach (var m in _materials)
        {
            m.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("see_through"), v);
            m.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("see_through_y"), her.Y);
        }
    }
    Vector4 _seeThrough;

    public void SetPortal(Vector2 centre, float radius, int mode)
    {
        foreach (var m in _materials) m.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("portal"), new Vector4(centre.X, centre.Y, radius, mode));
    }

    /// <summary>The level below becomes the current one, back at the origin: its Crack, its lights.</summary>
    public void Promote(Vector2 origin)
    {
        _below = false;
        Place(Vector3.Zero, origin);
        SetPortal(Vector2.Zero, 0f, 0);
        foreach (var m in _materials) m.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("crack_mask"), 1f);
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
        float e = Mathf.Max(Mathf.Min(Mathf.Min(x, y), Mathf.Min(LevelMap.Size - x, LevelMap.Size - y)), 0f);
        if (e < LevelMap.RimWidth) c = c.Lerp(new Color(0.24f, 0.23f, 0.26f), 1f - e / LevelMap.RimWidth);
        // A little grain so flat colour reads as ground.
        uint hash = unchecked((uint)((int)(x * Sub) * 73856093 ^ (int)(y * Sub) * 19349663));
        float grain = 0.95f + 0.1f * ((hash & 1023) / 1023f);
        return new Color(c.R * grain, c.G * grain, c.B * grain);
    }

    /// <summary>
    /// The terrain as a smooth surface: bicubic (Catmull-Rom) through the height samples, so it still passes through
    /// every grid point the simulation collides against, but without creases between them. Around the Crack it is
    /// plain bilinear: its walls drop sheer, without the bicubic's ripples. (A blue hole's bowl slopes smoothly, and is
    /// smooth all through: no seam rings it.)
    /// </summary>
    static float SmoothHeight(LevelMap map, float x, float y) =>
        map.HasBoss && map.Shaft.Contains(new System.Numerics.Vector2(x, y), 3f) ? map.HeightAt(new System.Numerics.Vector2(x, y)) : SmoothSample((i, j) => map[i, j], x, y);

    /// <summary>A tunnel's roof where there is one, the terrain elsewhere (so its edge meets the ground exactly).</summary>
    static float SmoothTunnelRoof(LevelMap map, float x, float y) => SmoothSample((i, j) => float.IsNaN(map.RoofAt(i, j)) ? map[i, j] : map.RoofAt(i, j), x, y);

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
        float v = CatmullRom(Row(iy - 1), Row(iy), Row(iy + 1), Row(iy + 2), fy);
        // Never past the cell's own corners: over a big drop the spline would ring (ridges and pits along a cliff).
        float c00 = grid(ix, iy), c10 = grid(Math.Min(ix + 1, LevelMap.Size), iy), c01 = grid(ix, Math.Min(iy + 1, LevelMap.Size)), c11 = grid(Math.Min(ix + 1, LevelMap.Size), Math.Min(iy + 1, LevelMap.Size));
        return Mathf.Clamp(v, Mathf.Min(Mathf.Min(c00, c10), Mathf.Min(c01, c11)), Mathf.Max(Mathf.Max(c00, c10), Mathf.Max(c01, c11)));
    }

    static float CatmullRom(float p0, float p1, float p2, float p3, float t) =>
        0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (3f * p1 - p0 - 3f * p2 + p3) * t * t * t);

    /// <summary>The way a sealed wall faces: toward the side it is met from.</summary>
    public static System.Numerics.Vector2 SealFacing(LevelMap map, WeakRock rock) =>
        rock.Thick > 0f ? rock.Facing : rock.Cave >= 0 && rock.Cave < map.Caves.Count ? map.Caves[rock.Cave].Facing : System.Numerics.Vector2.UnitY;

    /// <summary>Whether a grid point is part of this sealed wall (nearer it than any other).</summary>
    static bool OfSeal(LevelMap map, WeakRock rock, int x, int y)
    {
        var p = new System.Numerics.Vector2(x, y);
        float reach = rock.Radius + (rock.Thick > 0f ? rock.Thick : rock.Radius) + 10f;
        if (System.Numerics.Vector2.Distance(p, rock.Center) > reach) return false;
        foreach (var other in map.WeakRocks)
            if (other != rock && System.Numerics.Vector2.Distance(p, other.Center) < System.Numerics.Vector2.Distance(p, rock.Center)) return false;
        return true;
    }

    /// <summary>The terrain with this sealed wall standing in it (LevelMap.Sealed where it stands, the ground elsewhere), smooth.</summary>
    public static float SealHeight(LevelMap map, WeakRock rock, float x, float y) =>
        SmoothSample((i, j) => float.IsNaN(map.SealedAt(i, j)) || !OfSeal(map, rock, i, j) ? map[i, j] : map.SealedAt(i, j), x, y);

    /// <summary>The terrain with what a long sealed wall leaves standing once its gate is open (LevelMap.SealedOpen), smooth.</summary>
    public static float RemainsHeight(LevelMap map, WeakRock rock, float x, float y) =>
        SmoothSample((i, j) => float.IsNaN(map.SealedOpenAt(i, j)) || !OfSeal(map, rock, i, j) ? map[i, j] : map.SealedOpenAt(i, j), x, y);

    /// <summary>A long sealed wall's remains: only where they stand above the ground.</summary>
    internal static LevelShape.Surface? RemainsSurface(LevelMap map, WeakRock rock)
    {
        int reach = (int)(rock.Radius + rock.Thick) + 10;
        int x0 = Math.Max(0, (int)rock.Center.X - reach), x1 = Math.Min(LevelMap.Size, (int)rock.Center.X + reach);
        int y0 = Math.Max(0, (int)rock.Center.Y - reach), y1 = Math.Min(LevelMap.Size, (int)rock.Center.Y + reach);
        return SurfaceMesh((x, y) => RemainsHeight(map, rock, x, y) + 0.02f, x0, y0, x1, y1, (x, y) => RemainsHeight(map, rock, x, y) > SmoothHeight(map, x, y) + 0.04f);
    }

    /// <summary>How far the sealed wall stands over the ground at its tallest (how far it has to slide down, below its remains too).</summary>
    public static float SealDrop(LevelMap map, WeakRock rock)
    {
        float most = 0f;
        int r = (int)(rock.Radius + rock.Thick) + 10;
        for (int y = Math.Max(0, (int)rock.Center.Y - r); y <= Math.Min(LevelMap.Size, (int)rock.Center.Y + r); y++)
        for (int x = Math.Max(0, (int)rock.Center.X - r); x <= Math.Min(LevelMap.Size, (int)rock.Center.X + r); x++)
            if (!float.IsNaN(map.SealedAt(x, y)) && OfSeal(map, rock, x, y)) most = Mathf.Max(most, map.SealedAt(x, y) - map[x, y]);
        return most;
    }

    /// <summary>
    /// Where a sealed wall meets the seabed: points along its feet, on the passage floor at the foot of its faces (the
    /// silt is pushed up there as it slides down).
    /// </summary>
    public static List<Vector3> SealFoot(LevelMap map, WeakRock rock)
    {
        var feet = new List<Vector3>();
        int r = (int)(rock.Radius + rock.Thick) + 4;
        for (int y = Math.Max(1, (int)rock.Center.Y - r); y <= Math.Min(LevelMap.Size - 1, (int)rock.Center.Y + r); y++)
        for (int x = Math.Max(1, (int)rock.Center.X - r); x <= Math.Min(LevelMap.Size - 1, (int)rock.Center.X + r); x++)
        {
            float s0 = map.SealedAt(x, y);
            if (float.IsNaN(s0) || s0 - map[x, y] < 0.4f || map[x, y] > LevelMap.BlockHeight || !OfSeal(map, rock, x, y)) continue;
            // A long one goes down only at its gate.
            if (rock.Gate < rock.Radius && MathF.Abs(System.Numerics.Vector2.Dot(new System.Numerics.Vector2(x, y) - rock.Center, rock.Across)) > rock.Gate + 0.5f) continue;
            // At its edge: a neighbour on the open floor with no wall over it.
            bool edge = false;
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                float sn = map.SealedAt(x + dx, y + dy);
                if ((float.IsNaN(sn) || sn - map[x + dx, y + dy] < 0.3f) && map[x + dx, y + dy] <= LevelMap.BlockHeight) edge = true;
            }
            if (edge) feet.Add(new Vector3(x, map[x, y] + 0.1f, y));
        }
        return feet;
    }

    /// <summary>A sealed wall's stretch of wall: only where it stands above the ground.</summary>
    internal static LevelShape.Surface? SealSurface(LevelMap map, WeakRock rock)
    {
        int reach = (int)(rock.Radius + rock.Thick) + 10;
        int x0 = Math.Max(0, (int)rock.Center.X - reach), x1 = Math.Min(LevelMap.Size, (int)rock.Center.X + reach);
        int y0 = Math.Max(0, (int)rock.Center.Y - reach), y1 = Math.Min(LevelMap.Size, (int)rock.Center.Y + reach);
        return SurfaceMesh((x, y) => SealHeight(map, rock, x, y) + 0.01f, x0, y0, x1, y1, (x, y) => SealHeight(map, rock, x, y) > SmoothHeight(map, x, y) + 0.04f);
    }

    /// <summary>
    /// A chunk of the ground. The shaft has no floor: through it, the level below shows. (An abyss reef's dropped floor is
    /// drawn all the way down, so the water's murk takes it smoothly rather than at a cut edge.)
    /// </summary>
    internal static LevelShape.Surface? ChunkSurface(LevelMap map, int x0, int y0, int x1, int y1) =>
        SurfaceMesh((x, y) => SmoothHeight(map, x, y), x0, y0, x1, y1,
            (x, y) => SmoothHeight(map, x, y) > LevelMap.ShaftBottom + 0.3f || map.Abyss && !map.Shaft.Contains(new System.Numerics.Vector2(x, y), 3f));

    /// <summary>How far (metres) the reef is drawn past the square on every side: wider than the view reaches.</summary>
    internal const int Skirt = 32;

    /// <summary>
    /// The reef past the square, never reached: the rim's own rock where it meets the edge, rising within a few metres
    /// into a ragged ridge at about the rim's top, so the edge reads as more reef rather than a cliff into nothing.
    /// </summary>
    internal static LevelShape.Surface? SkirtSurface(LevelMap map, int x0, int y0, int x1, int y1) =>
        SurfaceMesh((x, y) => SkirtHeight(map, x, y), x0, y0, x1, y1, null);

    static float SkirtHeight(LevelMap map, float x, float y)
    {
        float cx = Mathf.Clamp(x, 0f, LevelMap.Size), cy = Mathf.Clamp(y, 0f, LevelMap.Size);
        float edge = SmoothHeight(map, cx, cy);
        float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
        if (d <= 0f) return edge;
        // Ragged, but the same every time (it is drawn from the position alone).
        float ridge = ReefTerrain.HMax - 1f + 1.6f * Mathf.Sin(x * 0.37f + Mathf.Sin(y * 0.21f) * 2f) * Mathf.Sin(y * 0.29f + Mathf.Sin(x * 0.17f) * 2f);
        return Mathf.Lerp(edge, ridge, Mathf.SmoothStep(0f, 6f, d));
    }

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

    /// <summary>
    /// A shortcut tunnel's roof: the rock carried over it (<see cref="LevelMap.Roof"/>), meeting the ground at its edges,
    /// lumped like the rest of the reef's rock by its shader. It fades like any canopy while Clementine is near.
    /// </summary>
    internal static LevelShape.Surface? TunnelRoofSurface(LevelMap map, Corridor corridor)
    {
        float reach = corridor.HalfWidth + 5f;
        int x0 = Math.Max(0, (int)(corridor.Points.Min(p => p.X) - reach)), x1 = Math.Min(LevelMap.Size, (int)(corridor.Points.Max(p => p.X) + reach) + 1);
        int y0 = Math.Max(0, (int)(corridor.Points.Min(p => p.Y) - reach)), y1 = Math.Min(LevelMap.Size, (int)(corridor.Points.Max(p => p.Y) + reach) + 1);
        bool Along(float x, float y)
        {
            var q = new System.Numerics.Vector2(x, y);
            for (int i = 1; i < corridor.Points.Count; i++)
                if (Geo.SegmentDistance(q, corridor.Points[i - 1], corridor.Points[i]) < reach) return true;
            return false;
        }
        return SurfaceMesh((x, y) => SmoothTunnelRoof(map, x, y) + 0.03f, x0, y0, x1, y1,
            (x, y) => Along(x, y) && SmoothTunnelRoof(map, x, y) > SmoothHeight(map, x, y) + 0.05f);
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
            // Clockwise seen from above (Godot's front face), split along the flatter diagonal: on a steep edge (a wall's
            // top over the abyss) the fixed split leaves a row of teeth along it.
            if (Mathf.Abs(verts[a].Y - verts[d].Y) < Mathf.Abs(verts[b].Y - verts[c].Y)) indices.AddRange(new[] { a, b, d, a, d, c });
            else indices.AddRange(new[] { a, b, c, b, d, c });
        }
        if (indices.Count == 0) return null;
        return new LevelShape.Surface(verts, normals, colors, indices.ToArray());
    }

    // ───────────────────────── canopy ─────────────────────────

    void AddCanopy(Canopy c)
    {
        // A cave's roof is the mountain itself (its lid mesh), not a separate piece; a tunnel's, the rock over it.
        if (c.Kind == CanopyKind.CaveRoof)
        {
            int cave = CaveOf(c);
            if (cave >= 0) _canopies.Add((c, _lids[cave], 1f));
            return;
        }
        if (c.Kind == CanopyKind.TunnelRoof)
        {
            if (_tunnelRoofs.TryGetValue(c.Corridor, out var roof)) _canopies.Add((c, roof, 1f));
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
    /// The occlusion rule (DESIGN-TOPDOWN §3): she is always seen. Any canopy (arch, cave roof, tunnel roof, overhang)
    /// that stands over Clementine, or between her and the camera, dissolves, and returns once it no longer does — and
    /// already as she comes near it: her line of sight to the camera is followed up through each canopy's height, and a
    /// canopy within OcclusionMargin of it fades, well before her bell is under the rock. A cave's roof, and a tunnel's,
    /// fade whole.
    /// </summary>
    public void UpdateCanopy(System.Numerics.Vector2 player, float dt, Vector3 camera)
    {
        var her = new Vector3(player.X, LevelMap.SwimBand, player.Y);
        var sight = (camera - her).Normalized();
        var hiddenCaves = new HashSet<int>();
        var hiddenTunnels = new HashSet<int>();
        var hide = new bool[_canopies.Count];
        for (int i = 0; i < _canopies.Count; i++)
        {
            var c = _canopies[i].Canopy;
            if (!Blocks(c, her, sight)) continue;
            hide[i] = true;
            if (c.Kind == CanopyKind.CaveRoof && CaveOf(c) is int cave and >= 0) hiddenCaves.Add(cave);
            if (c.Kind == CanopyKind.TunnelRoof) hiddenTunnels.Add(c.Corridor);
        }
        for (int i = 0; i < _canopies.Count; i++)
        {
            var (c, material, alpha) = _canopies[i];
            bool gone = hide[i] || (c.Kind == CanopyKind.CaveRoof && hiddenCaves.Contains(CaveOf(c)))
                || (c.Kind == CanopyKind.TunnelRoof && hiddenTunnels.Contains(c.Corridor));
            // Quick to clear the way, gentle to come back; cleared, all but gone (a faint ghost of the rock).
            alpha = Mathf.MoveToward(alpha, gone ? Cleared : 1f, dt * (gone ? 7f : 3f));
            // Verification: --dbg-nocanopy keeps every canopy gone.
            if (_noCanopy) alpha = 0f;
            // A dithered dissolve: the rock stays opaque, so the depth focus still blurs it like other high rock.
            material.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("fade"), alpha);
            _canopies[i] = (c, material, alpha);
        }
    }

    /// <summary>How near (metres) her line of sight may pass a canopy's footprint before it fades, and how much of it stays then.</summary>
    const float OcclusionMargin = 4.5f, Cleared = 0.08f;
    static readonly bool _noCanopy = OS.GetCmdlineUserArgs().Contains("--dbg-nocanopy");

    /// <summary>Whether a canopy stands in (or within OcclusionMargin of) her line of sight to the camera, over its height.</summary>
    static bool Blocks(Canopy c, Vector3 her, Vector3 sight)
    {
        float lo = Mathf.Max(c.Bottom - 0.5f, her.Y), hi = c.Top + 0.5f;
        if (sight.Y <= 1e-3f) return Near(c, new System.Numerics.Vector2(her.X, her.Z));
        for (float h = lo; h <= hi + 1e-3f; h += 0.75f)
        {
            var at = her + sight * ((h - her.Y) / sight.Y);
            if (Near(c, new System.Numerics.Vector2(at.X, at.Z))) return true;
        }
        // And straight over her, too.
        return Near(c, new System.Numerics.Vector2(her.X, her.Z));
    }

    static bool Near(Canopy c, System.Numerics.Vector2 p)
    {
        var d = p - c.Center;
        if (c.Radius > 0f) return d.LengthSquared() <= (c.Radius + OcclusionMargin) * (c.Radius + OcclusionMargin);
        float along = System.Numerics.Vector2.Dot(d, c.Axis);
        float across = System.Numerics.Vector2.Dot(d, new System.Numerics.Vector2(-c.Axis.Y, c.Axis.X));
        return MathF.Abs(along) <= c.HalfLength + OcclusionMargin && MathF.Abs(across) <= c.HalfWidth + OcclusionMargin;
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
        // A blue hole is plain water; only a boss level's exit is the Crack (and it glows only once it is open).
        RenderingServer.GlobalShaderParameterSet(OctoShoots.Game.Fx.SN.Of("reef_crack"), map.HasBoss ? new Vector4(map.Exit.Position.X, map.Exit.Position.Y, 10f, CrackGlow) : Vector4.Zero);
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
            // Shadowed, so the fissure's lips and whatever stands at the Crack throw shadows up the arena.
            ShadowEnabled = OctoShoots.Game.Settings.ViewOptions.Shadows,
            OmniShadowMode = OmniLight3D.ShadowMode.DualParaboloid,
            ShadowBias = 0.08f,
            ShadowBlur = 2f,
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
        ApplyCrack();
    }

    OmniLight3D? _crackLight;
    CpuParticles3D? _crackBubbles;
    MeshInstance3D? _cover;
    ShaderMaterial? _coverMat;
    CpuParticles3D? _sandFall;
    /// <summary>How far the Crack has opened (0 sealed under sand → 1 open), and the dive's fade of it (1 → 0).</summary>
    float _crackOpen, _crackFade = 1f;

    /// <summary>The Crack's glow, light and bubbles: they come up as the last of the sand drains away.</summary>
    float CrackGlow => Mathf.SmoothStep(0.45f, 1f, _crackOpen) * _crackFade;

    /// <summary>
    /// The dive through the Crack: its glow, its warm light and its rising bubbles fade together (1 full, 0 gone), so
    /// nothing of it is switched off when this level is let go.
    /// </summary>
    public void FadeCrack(float k)
    {
        _crackFade = k;
        ApplyCrack();
    }

    /// <summary>
    /// The Crack opening (0 sealed → 1 open): the sand over it sags, turns and drains down into it, grains pouring
    /// off its rim, and then the Crack's glow, light and bubbles come up from below.
    /// </summary>
    public void SetCrackOpen(float t)
    {
        _crackOpen = Mathf.Clamp(t, 0f, 1f);
        ApplyCrack();
    }

    void ApplyCrack()
    {
        if (_map is null || !_map.HasBoss) return;
        float k = CrackGlow;
        if (!_below) RenderingServer.GlobalShaderParameterSet(OctoShoots.Game.Fx.SN.Of("reef_crack"), new Vector4(_map.Exit.Position.X, _map.Exit.Position.Y, 10f, k));
        if (_crackLight is not null) _crackLight.LightEnergy = 2.2f * k;
        if (_crackBubbles is not null)
        {
            _crackBubbles.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("fade"), k);
            _crackBubbles.Emitting = k > 0.01f;
        }
        _coverMat?.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("sink"), _crackOpen);
        if (_cover is not null) _cover.Visible = _crackOpen < 1f;
        if (_sandFall is not null) _sandFall.Emitting = _crackOpen > 0f && _crackOpen < 0.85f;
    }

    /// <summary>
    /// The sand sealing a boss level's Crack: a cap over the shaft (a little past its rim), each rim point at the
    /// ground's own height there and the middle at their average, so it meets the arena floor without a seam.
    /// </summary>
    void ShowCover(LevelMap map)
    {
        var sh = map.Shaft;
        var across = new System.Numerics.Vector2(-sh.Axis.Y, sh.Axis.X);
        const float grow = 0.8f;
        const int rings = 14, segments = 64;
        System.Numerics.Vector2 At(float r, float a, float extra) =>
            sh.Center + sh.Axis * (Mathf.Cos(a) * (sh.HalfLength + grow + extra) * r) + across * (Mathf.Sin(a) * (sh.HalfWidth + grow + extra) * r);
        var rim = new float[segments];
        float mean = 0f;
        for (int j = 0; j < segments; j++)
        {
            rim[j] = map.HeightAt(At(1f, j * Mathf.Tau / segments, 0.6f));
            mean += rim[j] / segments;
        }
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        st.SetNormal(Vector3.Up);
        st.SetUV(new Vector2(0f, 0f));
        st.AddVertex(new Vector3(sh.Center.X, mean + 0.03f, sh.Center.Y));
        for (int i = 1; i <= rings; i++)
        for (int j = 0; j < segments; j++)
        {
            float r = i / (float)rings, a = j * Mathf.Tau / segments;
            var p = At(r, a, 0f);
            st.SetNormal(Vector3.Up);
            st.SetUV(new Vector2(r, 0f));
            st.AddVertex(new Vector3(p.X, Mathf.Lerp(mean, rim[j], r * r) + 0.03f, p.Y));
        }
        int Index(int ring, int seg) => ring == 0 ? 0 : 1 + (ring - 1) * segments + (seg % segments);
        for (int j = 0; j < segments; j++)
        {
            // Clockwise seen from above (Godot's front face).
            st.AddIndex(0);
            st.AddIndex(Index(1, j));
            st.AddIndex(Index(1, j + 1));
        }
        for (int i = 1; i < rings; i++)
        for (int j = 0; j < segments; j++)
        {
            int a0 = Index(i, j), a1 = Index(i, j + 1), b0 = Index(i + 1, j), b1 = Index(i + 1, j + 1);
            st.AddIndex(a0);
            st.AddIndex(b0);
            st.AddIndex(a1);
            st.AddIndex(a1);
            st.AddIndex(b0);
            st.AddIndex(b1);
        }
        _coverMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/crack_cover.gdshader") };
        _coverMat.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("cover_center"), new Vector2(sh.Center.X, sh.Center.Y));
        _coverMat.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("drop"), mean - LevelMap.ShaftBottom + 1f);
        Track(_coverMat);
        AddChild(_cover = new MeshInstance3D { Mesh = st.Commit(), MaterialOverride = _coverMat });

        // Grains pouring off the cover's rim into the dark as it drains.
        var grain = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(0.78f, 0.7f, 0.52f),
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
        };
        AddChild(_sandFall = new CpuParticles3D
        {
            Position = new Vector3(sh.Center.X, mean + 0.2f, sh.Center.Y),
            Emitting = false,
            Amount = 220,
            Lifetime = 1.6f,
            Mesh = new QuadMesh { Size = new Vector2(0.07f, 0.07f), Material = grain },
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = Mathf.Max(sh.HalfLength, sh.HalfWidth) * 0.8f,
            Direction = Vector3.Down,
            Spread = 25f,
            Gravity = new Vector3(0f, -7f, 0f),
            InitialVelocityMin = 0.2f,
            InitialVelocityMax = 1.2f,
            ScaleAmountMin = 0.6f,
            ScaleAmountMax = 1.6f,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
    }

    // ───────────────────────── places ─────────────────────────

    /// <summary>The place's name, as the HUD shows it when Clementine enters.</summary>
    public static string PlaceName(PoiKind kind) => kind switch
    {
        PoiKind.ShellCache => "Shell cache",
        PoiKind.TreasureCave => "Clam room",
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
                new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(0.4f, 1f, 0.9f), EmissionEnabled = true, Emission = new Color(0.4f, 1f, 0.9f), EmissionEnergyMultiplier = 2f }, shape.Glows, shadow: false));
    }

    /// <summary>Maps the generator's decoration onto the existing flora art. Positions, yaw and scale all come from the map.</summary>
    internal static void DecorItems(LevelMap map, List<FloraItem> items, List<Transform3D> rocks, List<Transform3D> glows)
    {
        foreach (var d in map.Decor)
        {
            var pos = new Vector3(d.Position.X, d.Height, d.Position.Y);
            var basis = new Basis(Vector3.Up, d.Yaw).Scaled(Vector3.One * d.Scale);
            // Only the seagrass meadows and the brain corals (the sponge-like rocks): the rest of the dressing drew the eye
            // away from what matters (DESIGN-TOPDOWN §4.1).
            FloraKind? kind = d.Kind switch
            {
                DecorKind.GrassMeadow => FloraKind.TurtleGrass,
                DecorKind.Bommie => FloraKind.BrainCoral,
                _ => null,
            };
            if (kind is { } k)
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
    /// the seabed reads as a living reef rather than a beach (THEME-BIBLE §7, Depth 1: healthy neighbours everywhere),
    /// kept sparse so it never draws the eye from play: seagrass meadows in drifting patches, a few small brain corals at
    /// the feet of the walls and a lone coral head now and then out on the open sand. The exit is left clear round the
    /// shaft.
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
            if (nearWall && r < 0.06f)
                // The reef's foot: a few small brain corals.
                items.Add(Item(FloraKind.BrainCoral, 0.55f + 0.4f * size));
            else if (meadow > 0.1f && slope < 0.35f && r < 0.85f * Mathf.SmoothStep(0.1f, 0.35f, meadow))
                items.Add(Item(FloraKind.TurtleGrass, 1.9f + 1.1f * size));
            else if (slope < 0.3f && r < 0.012f)
                items.Add(Item(FloraKind.BrainCoral, 1.1f + 0.9f * size));
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

    /// <summary>One mesh many times; it casts shadows (the boulders) unless it is light itself (the glows).</summary>
    static MultiMeshInstance3D Instances(Mesh mesh, Material material, List<Transform3D> transforms, bool shadow = true)
    {
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = mesh, InstanceCount = transforms.Count };
        for (int i = 0; i < transforms.Count; i++) mm.SetInstanceTransform(i, transforms[i]);
        return new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = material, CastShadow = shadow ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off };
    }
}
