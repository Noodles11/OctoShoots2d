using Godot;

namespace OctoShoots.Game.Fx;

/// <summary>
/// The clam room's pieces (DESIGN-TOPDOWN §4.2), built once per room: the giant clam's lower valve (a deep ribbed bowl
/// with a scalloped rim, its foot on the cave floor), the pink velvet tongue the pearl lies on (a soft cushion with a
/// frilled edge), and the shell wall lining the chamber (ribbed, scalloped at the top, leaning in, open at the mouth).
/// UV.x runs round (0 → 1), UV.y from the rim or floor (0) to the bottom or top (1); clam_nacre.gdshader draws the
/// ribs from UV.x. The shells' normals point to their inside (the nacre), so the shader knows which side it sees.
/// </summary>
public static class ClamRoomMeshes
{
    const int Ribs = 18;

    /// <summary>A deep bowl, rim at y = 0 with a scalloped lip, falling to y = −depth; radius at the rim.</summary>
    public static ArrayMesh LowerValve(float radius, float depth)
    {
        const int rings = 14, segments = 72;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i <= rings; i++)
        for (int j = 0; j <= segments; j++)
        {
            float v = i / (float)rings, a = j / (float)segments * Mathf.Tau;
            // Steep sides, a rounded foot.
            float r = radius * Mathf.Cos(v * Mathf.Pi * 0.5f * 0.92f);
            float y = -depth * Mathf.Sin(v * Mathf.Pi * 0.5f);
            // The scallop: the lip rises and falls with the ribs; the ribs flute the sides a little.
            float rib = Mathf.Sin(a * Ribs);
            y += (1f - v) * (1f - v) * 0.09f * rib;
            r *= 1f + 0.035f * rib * (1f - v * 0.6f);
            var p = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
            // Toward the inside of the bowl (up and in): the nacre side.
            var n = new Vector3(-Mathf.Cos(a) * Mathf.Cos(v * 1.4f), Mathf.Sin(v * 1.4f) * 0.8f + 0.2f, -Mathf.Sin(a) * Mathf.Cos(v * 1.4f)).Normalized();
            st.SetNormal(n);
            st.SetUV(new Vector2(j / (float)segments, v));
            st.AddVertex(p);
        }
        Grid(st, rings, segments);
        return st.Commit();
    }

    /// <summary>The tongue: a soft flattened cushion, centred at y = 0, its edge frilled and gently waved.</summary>
    public static ArrayMesh Tongue(float radius, float height)
    {
        const int rings = 12, segments = 64;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i <= rings; i++)
        for (int j = 0; j <= segments; j++)
        {
            float v = i / (float)rings, a = j / (float)segments * Mathf.Tau;
            // From the top (v 0) round the swollen edge to the underside (v 1).
            float t = v * Mathf.Pi;
            float frill = 1f + 0.07f * Mathf.Sin(a * 9f) * Mathf.Sin(t);
            float r = radius * Mathf.Sin(t) * frill;
            float y = height * 0.5f * Mathf.Cos(t) + 0.05f * Mathf.Sin(a * 5f + 1f) * Mathf.Sin(t);
            var p = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
            var n = new Vector3(Mathf.Cos(a) * Mathf.Sin(t) / radius, Mathf.Cos(t) / (height * 0.5f), Mathf.Sin(a) * Mathf.Sin(t) / radius).Normalized();
            st.SetNormal(n);
            st.SetUV(new Vector2(j / (float)segments, v));
            st.AddVertex(p);
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
