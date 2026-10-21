using System;
using System.Collections.Generic;
using Godot;

namespace OctoShoots.Game.Fx;

/// <summary>
/// Mesh builders for the generated bosses' rigs (docs/BOSS-GENERATOR.md §13): lathes (bells, discs, domes, sacs), tubes
/// along a path (arms, tentacles, bodies), sheets (flaps) and a spiral (a snail's shell). Every vertex is painted by the
/// caller's paint function (COLOR: r stain, g accent, b groove — see boss_body.gdshader).
/// </summary>
public static class BossMeshes
{
    public delegate Color Paint(Vector3 local, float u, float v);

    static readonly Paint Plain = (_, _, _) => new Color(0f, 0f, 0f);

    /// <summary>
    /// A surface of revolution round Y: profile(t) gives (radius, height) for t 0 (top) → 1 (bottom). Closed at a
    /// zero radius; open where the radius is not zero (a bell's open mouth).
    /// </summary>
    public static ArrayMesh Lathe(Func<float, Vector2> profile, int rings, int segments, Paint? paint = null, Func<float, float, float>? wobble = null)
    {
        paint ??= Plain;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i <= rings; i++)
        for (int j = 0; j <= segments; j++)
        {
            float t = i / (float)rings, a = j / (float)segments * Mathf.Tau;
            var rh = profile(t);
            float r = rh.X * (1f + (wobble?.Invoke(t, a) ?? 0f));
            var p = new Vector3(Mathf.Cos(a) * r, rh.Y, Mathf.Sin(a) * r);
            st.SetColor(paint(p, j / (float)segments, t));
            st.SetUV(new Vector2(j / (float)segments, t));
            st.AddVertex(p);
        }
        Grid(st, rings, segments);
        st.GenerateNormals();
        return st.Commit();
    }

    /// <summary>
    /// A tube along a path: radius(t) and flatten(t) (its cross-section squashed vertically) for t 0 → 1 along it; the
    /// end is capped to a point when the radius there is 0.
    /// </summary>
    public static ArrayMesh Tube(IReadOnlyList<Vector3> path, Func<float, float> radius, int sides, Paint? paint = null, Func<float, float>? flatten = null)
    {
        paint ??= Plain;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        int n = path.Count;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)(n - 1);
            var along = (path[Math.Min(i + 1, n - 1)] - path[Math.Max(i - 1, 0)]).Normalized();
            var side = along.Cross(Vector3.Up);
            if (side.LengthSquared() < 1e-6f) side = along.Cross(Vector3.Right);
            side = side.Normalized();
            var up = side.Cross(along).Normalized();
            float r = radius(t), f = flatten?.Invoke(t) ?? 1f;
            for (int j = 0; j <= sides; j++)
            {
                float a = j / (float)sides * Mathf.Tau;
                var off = side * (Mathf.Cos(a) * r) + up * (Mathf.Sin(a) * r * f);
                var p = path[i] + off;
                st.SetColor(paint(p, t, j / (float)sides));
                st.SetUV(new Vector2(t, j / (float)sides));
                st.AddVertex(p);
            }
        }
        Grid(st, n - 1, sides);
        st.GenerateNormals();
        return st.Commit();
    }

    /// <summary>A sheet over a grid: at(u, v) for u, v in 0..1 (both sides drawn by the material).</summary>
    public static ArrayMesh Sheet(Func<float, float, Vector3> at, int us, int vs, Paint? paint = null)
    {
        paint ??= Plain;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i <= vs; i++)
        for (int j = 0; j <= us; j++)
        {
            float u = j / (float)us, v = i / (float)vs;
            var p = at(u, v);
            st.SetColor(paint(p, u, v));
            st.SetUV(new Vector2(u, v));
            st.AddVertex(p);
        }
        Grid(st, vs, us);
        st.GenerateNormals();
        return st.Commit();
    }

    /// <summary>A path sampled from a function of t (0..1).</summary>
    public static List<Vector3> Path(Func<float, Vector3> at, int steps)
    {
        var list = new List<Vector3>(steps + 1);
        for (int i = 0; i <= steps; i++) list.Add(at(i / (float)steps));
        return list;
    }

    /// <summary>A cone along +Y, base radius 1 at y = 0, tip at y = 1 (spines).</summary>
    public static ArrayMesh Cone(int sides = 6) => Lathe(t => new Vector2(t, 1f - t), 2, sides);

    /// <summary>A unit sphere (sacs, lumps), painted.</summary>
    public static ArrayMesh Ball(int rings = 10, int segments = 16, Paint? paint = null) =>
        Lathe(t => new Vector2(Mathf.Sin(t * Mathf.Pi), Mathf.Cos(t * Mathf.Pi)), rings, segments, paint);

    static void Grid(SurfaceTool st, int rows, int cols)
    {
        for (int i = 0; i < rows; i++)
        for (int j = 0; j < cols; j++)
        {
            int a = i * (cols + 1) + j, b = a + 1, c = a + cols + 1, d = c + 1;
            st.AddIndex(a);
            st.AddIndex(b);
            st.AddIndex(c);
            st.AddIndex(b);
            st.AddIndex(d);
            st.AddIndex(c);
        }
    }
}
