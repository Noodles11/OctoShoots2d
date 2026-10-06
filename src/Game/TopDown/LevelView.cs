using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Game.Fx;
using OctoShoots.Game.Util;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Shows a <see cref="LevelMap"/>: the heightfield as static 32 m chunks coloured by height band, the canopy layer
/// (arches, cave roofs, overhangs) that fades while Clementine is underneath, the POIs in the landmark colour
/// language, weak rock, and the decoration the generator chose. Purely presentation: nothing here generates,
/// randomises or decides placement.
/// </summary>
public partial class LevelView : Node3D
{
    const int Chunk = 32;

    /// <summary>Landmark light language (DESIGN-TOPDOWN §4.2). Start, item cache and nests have no colour there (see §12).</summary>
    public static Color PoiColor(PoiKind kind) => kind switch
    {
        PoiKind.TreasureCave => new Color(1f, 0.78f, 0.25f),   // gold
        PoiKind.Shop => new Color(0.35f, 0.95f, 0.45f),        // green
        PoiKind.CurseDen => new Color(1f, 0.25f, 0.25f),       // red
        PoiKind.Secret => new Color(0.7f, 0.4f, 1f),           // violet (no beacon)
        PoiKind.Rift => new Color(1f, 0.55f, 0.15f),           // orange (the Crack)
        PoiKind.Start => new Color(0.95f, 0.95f, 0.95f),       // not in the language: white
        PoiKind.ItemSpawn => new Color(0.3f, 0.9f, 0.85f),     // not in the language: teal (the treasure pool's colour)
        _ => new Color(1f, 0.45f, 0.3f),                       // ambushes, not in the language: coral warning
    };


    LevelMap _map = null!;
    ShaderMaterial _ground = null!;
    readonly List<(Canopy Canopy, StandardMaterial3D Material, float Alpha)> _canopies = new();
    readonly List<StandardMaterial3D> _lids = new();
    FloraViews _flora = null!;

    public override void _Ready()
    {
        _flora = new FloraViews();
        AddChild(_flora);
        _ground = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/topdown_ground.gdshader") };
        if (OS.GetCmdlineUserArgs().Contains("--dbg-normals")) _ground.SetShaderParameter("debug_normals", true);
    }

    public void Show(LevelMap map)
    {
        foreach (var child in GetChildren())
            if (child != _flora) child.QueueFree();
        _canopies.Clear();
        _map = map;

        for (int cy = 0; cy < LevelMap.Size; cy += Chunk)
        for (int cx = 0; cx < LevelMap.Size; cx += Chunk)
            AddChild(new MeshInstance3D { Mesh = ChunkMesh(map, cx, cy, Math.Min(cx + Chunk, LevelMap.Size), Math.Min(cy + Chunk, LevelMap.Size)), MaterialOverride = _ground });

        _lids.Clear();
        foreach (var cave in map.Caves)
        {
            var lid = new StandardMaterial3D { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 0.92f, DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Always };
            _lids.Add(lid);
            if (LidMesh(map, cave) is { } mesh) AddChild(new MeshInstance3D { Mesh = mesh, MaterialOverride = lid });
        }
        foreach (var canopy in map.Canopies) AddCanopy(canopy);
        foreach (var rock in map.WeakRocks)
        {
            var mat = new StandardMaterial3D { AlbedoColor = new Color(0.5f, 0.42f, 0.38f), Roughness = 0.95f };
            AddChild(new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = rock.Radius, Height = rock.Radius * 2.4f },
                MaterialOverride = mat,
                Position = new Vector3(rock.Center.X, map.HeightAt(rock.Center) + rock.Radius * 0.8f, rock.Center.Y),
            });
        }
        if (!OS.GetCmdlineUserArgs().Contains("--dbg-nodecor")) ShowDecor(map);
        if (OS.GetCmdlineUserArgs().Contains("--dbg-nocanopy")) foreach (var c in _canopies) c.Material.AlbedoColor = new Color(c.Material.AlbedoColor, 0f);
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
    /// every grid point the simulation collides against, but without creases between them.
    /// </summary>
    static float SmoothHeight(LevelMap map, float x, float y) => SmoothSample((i, j) => map[i, j], x, y);

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

    static ArrayMesh ChunkMesh(LevelMap map, int x0, int y0, int x1, int y1) =>
        SurfaceMesh((x, y) => SmoothHeight(map, x, y), x0, y0, x1, y1, null)!;

    /// <summary>
    /// A cave's roof: the mountain as it stood before the cave was hollowed out, over the chambers and a little past them
    /// (where it meets the terrain again). It fades like any canopy while Clementine is inside.
    /// </summary>
    static ArrayMesh? LidMesh(LevelMap map, CaveSite cave)
    {
        int x0 = Math.Max(0, (int)cave.Chambers.Min(c => c.Center.X - c.Radius) - 4), x1 = Math.Min(LevelMap.Size, (int)cave.Chambers.Max(c => c.Center.X + c.Radius) + 5);
        int y0 = Math.Max(0, (int)cave.Chambers.Min(c => c.Center.Y - c.Radius) - 4), y1 = Math.Min(LevelMap.Size, (int)cave.Chambers.Max(c => c.Center.Y + c.Radius) + 5);
        return SurfaceMesh((x, y) => SmoothRoof(map, x, y) + 0.03f, x0, y0, x1, y1, (x, y) => SmoothRoof(map, x, y) > SmoothHeight(map, x, y) + 0.05f);
    }

    static ArrayMesh? SurfaceMesh(Func<float, float, float> height, int x0, int y0, int x1, int y1, Func<float, float, bool>? keep)
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
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = verts;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.Color] = colors;
        arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
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
        var material = new StandardMaterial3D
        {
            AlbedoColor = c.Kind == CanopyKind.Arch ? new Color(0.48f, 0.42f, 0.38f) : new Color(0.38f, 0.34f, 0.31f),
            Roughness = 0.9f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Always,
        };
        var node = new Node3D { Position = new Vector3(c.Center.X, (c.Bottom + c.Top) * 0.5f, c.Center.Y) };
        AddChild(node);
        float thick = c.Top - c.Bottom;
        if (c.Radius > 0f)
        {
            node.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = c.Radius * 0.9f, BottomRadius = c.Radius, Height = thick, RadialSegments = 20 }, MaterialOverride = material });
        }
        else
        {
            node.Rotation = new Vector3(0f, Mathf.Atan2(-c.Axis.Y, c.Axis.X), 0f);
            node.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(c.HalfLength * 2f, thick, c.HalfWidth * 2f) }, MaterialOverride = material });
            if (c.Kind == CanopyKind.Arch)
            {
                // Footings down to the ground at both ends.
                foreach (var end in new[] { c.EndA, c.EndB })
                {
                    float ground = _map.HeightAt(end);
                    float height = c.Bottom - ground + 0.5f;
                    if (height <= 0.2f) continue;
                    AddChild(new MeshInstance3D
                    {
                        Mesh = new CylinderMesh { TopRadius = c.HalfWidth * 0.9f, BottomRadius = c.HalfWidth * 1.3f, Height = height, RadialSegments = 10 },
                        MaterialOverride = material,
                        Position = new Vector3(end.X, ground + height * 0.5f - 0.3f, end.Y),
                    });
                }
            }
        }
        _canopies.Add((c, material, 1f));
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
            alpha = Mathf.MoveToward(alpha, hide ? 0.1f : 1f, dt * 4f);
            material.AlbedoColor = new Color(material.AlbedoColor, alpha);
            // Opaque unless fading, so the depth focus blurs it like the rest of the high rock.
            material.Transparency = alpha >= 0.999f ? BaseMaterial3D.TransparencyEnum.Disabled : BaseMaterial3D.TransparencyEnum.Alpha;
            _canopies[i] = (c, material, alpha);
        }
    }

    int CaveOf(Canopy roof)
    {
        for (int i = 0; i < _map.Caves.Count; i++)
            if (_map.Caves[i].Chambers.Any(ch => ch.Center == roof.Center)) return i;
        return -1;
    }

    // ───────────────────────── places ─────────────────────────

    /// <summary>The place's name, as the HUD shows it when Clementine enters.</summary>
    public static string PlaceName(PoiKind kind) => kind switch
    {
        PoiKind.ItemSpawn => "Item cache",
        PoiKind.TreasureCave => "Treasure cave",
        PoiKind.CurseDen => "Curse den",
        PoiKind.Ambush => "Ambush",
        PoiKind.Rift => "The Rift",
        _ => kind.ToString(),
    };

    // ───────────────────────── decoration ─────────────────────────

    /// <summary>Maps the generator's decoration onto the existing flora art. Positions, yaw and scale all come from the map.</summary>
    void ShowDecor(LevelMap map)
    {
        var items = new List<FloraItem>();
        var rocks = new List<Transform3D>();
        var glows = new List<Transform3D>();
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
                DecorKind.TrenchSponge => FloraKind.BarrelSponge,
                _ => null,
            };
            if (d.Kind == DecorKind.Boulder) rocks.Add(new Transform3D(basis.Scaled(new Vector3(0.9f, 0.6f, 0.8f)), pos));
            else if (d.Kind == DecorKind.BiolumAccent) glows.Add(new Transform3D(basis.Scaled(Vector3.One * 0.12f), pos + Vector3.Up * 0.3f));
            else if (kind is { } k)
            {
                if (d.Kind == DecorKind.Bommie) basis = basis.Scaled(Vector3.One * 2.2f);
                items.Add(new FloraItem(k, new Transform3D(basis, pos), new Color(1f, 1f, 1f, d.Yaw / Mathf.Tau)));
            }
        }
        _flora.Show(items);
        AddChild(Instances(new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 10, Rings = 6 },
            new StandardMaterial3D { AlbedoColor = new Color(0.45f, 0.42f, 0.4f), Roughness = 0.95f }, rocks));
        AddChild(Instances(new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 8, Rings = 4 },
            new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(0.4f, 1f, 0.9f), EmissionEnabled = true, Emission = new Color(0.4f, 1f, 0.9f), EmissionEnergyMultiplier = 2f }, glows));
    }

    static MultiMeshInstance3D Instances(Mesh mesh, Material material, List<Transform3D> transforms)
    {
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = mesh, InstanceCount = transforms.Count };
        for (int i = 0; i < transforms.Count; i++) mm.SetInstanceTransform(i, transforms[i]);
        return new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
    }
}
