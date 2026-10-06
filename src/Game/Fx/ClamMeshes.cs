using Godot;

namespace OctoShoots.Game.Fx;

/// <summary>
/// A giant clam's two valves, modelled on Tridacna: heavy ribs that fan out from the hinge, fluted
/// edges whose zigzag lips interlock when shut, growth bands on the outside, mother-of-pearl inside
/// and a fleshy mantle along the lip. The lip plane is y = 0; the hinge runs along the back (z = -Depth).
/// See clam.gdshader for what the vertex colours mean.
/// </summary>
public static class ClamMeshes
{
    /// <summary>Half the clam's width (x) and half its length front to back (z).</summary>
    public const float HalfWidth = 0.82f, HalfLength = 0.62f;

    const float RibHeight = 0.05f;
    const float LipHeight = 0.075f;
    const int Folds = 10;
    const int RadialSteps = 28, AroundSteps = 80;

    /// <summary>The top valve (domes up) or the bottom one (cups down).</summary>
    public static ArrayMesh Valve(bool top) => Build(top ? 1f : -1f, top ? 0.36f : 0.33f);

    /// <summary>The lip's height at a point of the outline, shared by both valves so they meet.</summary>
    static float Fold(float x, float z) => Mathf.Cos(Mathf.Atan2(x, z + HalfLength) * Folds);

    static Vector3 Point(float t, float theta, float sign, float depth, bool inner)
    {
        float baseX = HalfWidth * t * Mathf.Cos(theta);
        float baseZ = HalfLength * t * Mathf.Sin(theta);
        float fold = Fold(baseX, baseZ);
        // The fluted edge: the outline bulges out on each fold.
        float flute = 1f + 0.07f * fold * t * t * t;
        float x = baseX * flute, z = baseZ * flute;

        float lip = Mathf.SmoothStep(0.55f, 1f, t);
        float dome = depth * Mathf.Pow(Mathf.Max(0f, 1f - t * t), 0.62f);
        float ribs = RibHeight * fold * t * (1f - lip);
        float growth = 0.006f * Mathf.Sin(t * 70f) * t;
        float y = inner
            ? sign * (dome * 0.8f + ribs * 0.5f)
            : sign * (dome + ribs + growth);
        y += LipHeight * fold * lip;
        return new Vector3(x, y, z);
    }

    static ArrayMesh Build(float sign, float depth)
    {
        var b = new MeshBuilder();
        foreach (bool inner in new[] { false, true })
        {
            var grid = new Vector3[RadialSteps + 1, AroundSteps];
            for (int i = 0; i <= RadialSteps; i++)
            for (int j = 0; j < AroundSteps; j++)
                grid[i, j] = Point(i / (float)RadialSteps, j / (float)AroundSteps * Mathf.Tau, sign, depth, inner);

            var ids = new int[RadialSteps + 1, AroundSteps];
            for (int i = 0; i <= RadialSteps; i++)
            for (int j = 0; j < AroundSteps; j++)
            {
                float t = i / (float)RadialSteps;
                Vector3 p = grid[i, j];
                // Outward is away from the valve's hollow: up and out for the outside, into the hollow for the inside.
                Vector3 reference = new Vector3(p.X * 0.6f, sign, p.Z * 0.6f) * (inner ? -1f : 1f);
                Vector3 n;
                if (i == 0) n = new Vector3(0f, sign * (inner ? -1f : 1f), 0f);
                else
                {
                    Vector3 dT = grid[Mathf.Min(i + 1, RadialSteps), j] - grid[i - 1, j];
                    Vector3 dA = grid[i, (j + 1) % AroundSteps] - grid[i, (j + AroundSteps - 1) % AroundSteps];
                    n = dA.Cross(dT).Normalized();
                    if (n.Dot(reference) < 0f) n = -n;
                }

                Color color;
                if (inner)
                {
                    float part = t > 0.7f ? 0.5f : 0f;
                    color = new Color(t, j / (float)AroundSteps, 0f, part);
                }
                else
                {
                    // Chalky cream, darker growth bands, paler rib crests and a lilac blush at the edge.
                    float fold = Fold(p.X, p.Z);
                    float band = 0.5f + 0.5f * Mathf.Sin(t * 44f);
                    var cream = new Color(0.93f, 0.88f, 0.8f);
                    var bandColor = new Color(0.74f, 0.66f, 0.6f);
                    color = cream.Lerp(bandColor, band * 0.45f * t);
                    color = color.Lerp(new Color(1f, 0.97f, 0.92f), Mathf.Max(0f, fold) * 0.3f * t);
                    color = color.Lerp(new Color(0.8f, 0.68f, 0.85f), Mathf.SmoothStep(0.8f, 1f, t) * 0.5f);
                    color.A = 1f;
                }
                ids[i, j] = b.Add(p, n, color);
            }
            for (int i = 0; i < RadialSteps; i++)
            for (int j = 0; j < AroundSteps; j++)
            {
                int j1 = (j + 1) % AroundSteps;
                b.Quad(ids[i, j], ids[i, j1], ids[i + 1, j1], ids[i + 1, j]);
            }
        }
        return b.Build();
    }
}
