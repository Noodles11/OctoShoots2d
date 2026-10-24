using Godot;

namespace OctoShoots.Game.Fx;

/// <summary>
/// The collectible shell (the currency, DESIGN-TOPDOWN): a round nautilus, silver-coated — one mesh and one material shared by the
/// shells lying in the reef (CombatView), the ones flying to the counter and the counter's own (HudView).
/// <para>
/// The mesh is a planispiral: a tube wound round the Y axis on a logarithmic spiral, each whorl three times the size of
/// the one before and wide enough to wrap the last (so the outer whorl hides the inner ones but for the small coil in the
/// navel), its section a little deeper along the axis than across (a plump, round shell), the tube left open at its end (the aperture). It spans one
/// unit across; its UV runs along the whorls (x, in turns) and round the tube (y), for the shader's growth lines.
/// </para>
/// </summary>
public static class NautilusShell
{
    static ArrayMesh? _mesh;
    static ShaderMaterial? _material;

    public static ArrayMesh Mesh => _mesh ??= Build();

    public static ShaderMaterial Material => _material ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/nautilus.gdshader") };

    /// <summary>The coil: how many turns, how much each turn grows, the tube's radius (share of its distance from the axis), its flattening.</summary>
    const float Turns = 3.2f, GrowthPerTurn = 3f, Tube = 0.6f, Flat = 1.2f;
    const int Along = 160, Round = 28;

    static ArrayMesh Build()
    {
        float b = Mathf.Log(GrowthPerTurn) / Mathf.Tau;
        float thetaEnd = Turns * Mathf.Tau;
        // Sized so the outer whorl's far side sits at 0.5.
        float rEnd = Mathf.Exp(b * thetaEnd);
        float scale = 0.5f / (rEnd * (1f + Tube));
        var st = new SurfaceTool();
        st.Begin(Godot.Mesh.PrimitiveType.Triangles);
        for (int i = 0; i <= Along; i++)
        {
            float t = i / (float)Along;
            float theta = t * thetaEnd;
            float r = Mathf.Exp(b * theta) * scale;
            float rho = r * Tube;
            var radial = new Vector3(Mathf.Cos(theta), 0f, Mathf.Sin(theta));
            var centre = radial * r;
            for (int j = 0; j <= Round; j++)
            {
                float phi = j / (float)Round * Mathf.Tau;
                float c = Mathf.Cos(phi), s = Mathf.Sin(phi);
                var p = centre + radial * (rho * c) + Vector3.Up * (rho * Flat * s);
                var n = (radial * c + Vector3.Up * (s / Flat)).Normalized();
                st.SetNormal(n);
                st.SetUV(new Vector2(t * Turns, j / (float)Round));
                st.AddVertex(p);
            }
        }
        for (int i = 0; i < Along; i++)
            for (int j = 0; j < Round; j++)
            {
                int a = i * (Round + 1) + j, a1 = a + 1, c0 = a + Round + 1, c1 = c0 + 1;
                st.AddIndex(a); st.AddIndex(a1); st.AddIndex(c0);
                st.AddIndex(a1); st.AddIndex(c1); st.AddIndex(c0);
            }
        return st.Commit();
    }
}
