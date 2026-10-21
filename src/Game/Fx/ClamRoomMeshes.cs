using Godot;

namespace OctoShoots.Game.Fx;

/// <summary>
/// The clam room's pieces (DESIGN-TOPDOWN §4.2), built once per room: the chamber's nacre floor (a disc laid over the
/// rock) and the shell wall lining the chamber (ribbed, scalloped at the top, leaning in, open at the mouth).
/// UV.x runs round (0 → 1), UV.y from the middle or the floor (0) to the edge or the top (1); clam_nacre.gdshader draws the
/// ribs from UV.x. The shells' normals point to their inside (the nacre), so the shader knows which side it sees.
/// </summary>
public static class ClamRoomMeshes
{
    const int Ribs = 18;

    /// <summary>
    /// The chamber's floor: a disc round (0, 0), or a ring from inner to outer radius, each point at height(xz offset) so it lies
    /// over the rock; UV.x runs round (the ribs fan out from the clam), UV.y from the inside out.
    /// </summary>
    public static ArrayMesh Floor(float inner, float outer, System.Func<Vector2, float> height)
    {
        const int rings = 16, segments = 96;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i <= rings; i++)
        for (int j = 0; j <= segments; j++)
        {
            float v = i / (float)rings, a = j / (float)segments * Mathf.Tau;
            float r = Mathf.Lerp(inner, outer, v);
            var xz = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            st.SetNormal(Vector3.Up);
            st.SetUV(new Vector2(j / (float)segments, v));
            st.AddVertex(new Vector3(xz.X, height(xz), xz.Y));
        }
        Grid(st, rings, segments);
        return st.Commit();
    }

    /// <summary>
    /// The room's shell wall round (0, 0): radius at its foot, rising height (leaning in a little), between the angles
    /// from and to (radians, the gap is the mouth); its top scalloped with the ribs.
    /// </summary>
    public static ArrayMesh ShellWall(float radius, float height, float from, float to)
    {
        const int rings = 10;
        int segments = Mathf.Max(24, (int)((to - from) / Mathf.Tau * 96f));
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i <= rings; i++)
        for (int j = 0; j <= segments; j++)
        {
            float v = i / (float)rings, a = Mathf.Lerp(from, to, j / (float)segments);
            float rib = Mathf.Sin(a * Ribs * 1.5f);
            float top = height * (1f + 0.06f * rib);
            float y = top * v;
            float r = radius * (1f - 0.16f * v * v) * (1f + 0.025f * rib);
            var p = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
            // Facing the middle of the room (the inside, its nacre).
            var n = new Vector3(-Mathf.Cos(a), 0.3f * v, -Mathf.Sin(a)).Normalized();
            st.SetNormal(n);
            st.SetUV(new Vector2((a - from) / Mathf.Tau, v));
            st.AddVertex(p);
        }
        Grid(st, rings, segments);
        return st.Commit();
    }

    static void Grid(SurfaceTool st, int rings, int segments)
    {
        for (int i = 0; i < rings; i++)
        for (int j = 0; j < segments; j++)
        {
            int a = i * (segments + 1) + j, b = a + 1, c = a + segments + 1, d = c + 1;
            st.AddIndex(a);
            st.AddIndex(c);
            st.AddIndex(b);
            st.AddIndex(b);
            st.AddIndex(c);
            st.AddIndex(d);
        }
    }
}
