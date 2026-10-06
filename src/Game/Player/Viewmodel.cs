using System;
using System.Collections.Generic;
using Godot;

namespace OctoShoots.Game.Player;

/// <summary>
/// First-person tentacles (§3.1), procedural tubes rebuilt every frame and drawn on top of the world
/// so they never clip into rock.
/// The <b>throwing tentacle</b> is the only arm in view: it reaches in from the bottom right and
/// points at the crosshair like a gun, rolled so its suckers face into the screen. Bubbles sit in its
/// suckers; throws take them from the tip end and new ones swell back from the base end.
/// The <b>inventory tentacle</b> appears only while reviewing: it rises along the left of the view
/// with item pearls in its suckers turned toward the camera.
/// Both arms carry <see cref="SuckerPairs"/> pairs of suckers in two staggered rows along the underside:
/// a pale raised ring around a rosy cup, shrinking toward the tip.
/// </summary>
public partial class Viewmodel : Node3D
{
    const int Segments = 40;
    const int Sides = 18;

    /// <summary>Pairs of suckers on each arm.</summary>
    public const int SuckerPairs = 8;

    /// <summary>Suckers on each arm; bubble capacity and held pearls are capped to this.</summary>
    public const int Suckers = SuckerPairs * 2;

    /// <summary>Pearls the inventory tentacle can show, one per sucker; any more are held but not drawn.</summary>
    public const int SuckerSlots = Suckers;

    /// <summary>Render layer of the arms; the camera glow and shot lights skip it so the arms don't blow out.</summary>
    public const uint RenderLayer = 1u << 1;

    /// <summary>Angle of each sucker row either side of the underside's middle line (radians).</summary>
    const float RowAngle = 0.58f;

    ImmediateMesh _mesh = null!;
    ShaderMaterial _skin = null!;
    ShaderMaterial _suckerMaterial = null!;
    readonly MeshInstance3D[] _gunSuckers = new MeshInstance3D[Suckers];
    readonly MeshInstance3D[] _bubbles = new MeshInstance3D[Suckers];
    readonly MeshInstance3D[] _invSuckers = new MeshInstance3D[Suckers];
    readonly MeshInstance3D[] _pearls = new MeshInstance3D[Suckers];
    int _pearlCount;
    int _bubbleCount, _capacity = 8;
    float _regrow;

    // One frame per ring of the tube, shared by the skin and the suckers so they always line up.
    readonly Vector3[] _points = new Vector3[Segments + 1];
    readonly Vector3[] _belly = new Vector3[Segments + 1];
    readonly Vector3[] _side = new Vector3[Segments + 1];
    readonly Vector3[] _ring = new Vector3[Sides + 1];
    readonly Vector3[] _prevRing = new Vector3[Sides + 1];

    float _time;
    float _throw, _throwVel;
    float _whip, _whipVel;
    float _hurt;
    float _swimPhase, _swim;
    bool _reviewing;
    float _review, _reviewVel;
    // Water drag: the arm trails behind her movement and turns, springing back smoothly.
    Vector3 _drift, _driftVel;

    /// <summary>Highlighted pearl while reviewing.</summary>
    public int Selected { get; set; }

    public int PearlCount => _pearlCount;

    public override void _Ready()
    {
        _skin = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/tentacle.gdshader"), RenderPriority = 10 };
        _mesh = new ImmediateMesh();
        AddChild(new MeshInstance3D { Mesh = _mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Layers = RenderLayer });

        _suckerMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/sucker.gdshader"), RenderPriority = 11 };
        var bubbleMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/bubble_overlay.gdshader"), RenderPriority = 12 };
        bubbleMaterial.SetShaderParameter("wobble", 0.4f);
        var suckerMesh = BuildSucker();
        var sphere = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 16, Rings = 8 };

        MeshInstance3D Part(Mesh mesh, Material? material, bool visible = true)
        {
            var m = new MeshInstance3D { Mesh = mesh, MaterialOverride = material, Layers = RenderLayer, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Visible = visible };
            AddChild(m);
            return m;
        }

        for (int i = 0; i < Suckers; i++)
        {
            _gunSuckers[i] = Part(suckerMesh, _suckerMaterial);
            _bubbles[i] = Part(sphere, bubbleMaterial, false);
        }
        for (int i = 0; i < Suckers; i++)
        {
            _invSuckers[i] = Part(suckerMesh, _suckerMaterial, false);
            _pearls[i] = Part(sphere, null, false);
        }
    }

    /// <summary>A swim stroke ripples down the arm.</summary>
    public void OnStroke() => _swim = 1f;

    /// <summary>The dash whips the arm back.</summary>
    public void OnDash() => _whipVel += 14f;

    /// <summary>A throw: the tip lunges toward the crosshair.</summary>
    public void OnThrow() => _throwVel += 22f;

    public void OnHurt() => _hurt = 1f;

    public void SetReviewing(bool reviewing) => _reviewing = reviewing;

    /// <summary>How many bubbles sit on the suckers, how many suckers hold them, and how far the next one has grown (0–1).</summary>
    public void SetBubbles(int bubbles, int capacity, float regrow)
    {
        _capacity = Math.Clamp(capacity, 1, Suckers);
        _bubbleCount = Math.Clamp(bubbles, 0, _capacity);
        _regrow = Mathf.Clamp(regrow, 0f, 1f);
    }

    /// <summary>Pearl materials in pickup order; only the first <see cref="SuckerSlots"/> are drawn.</summary>
    public void SetPearls(IReadOnlyList<Material> pearls)
    {
        _pearlCount = Math.Min(pearls.Count, SuckerSlots);
        for (int i = 0; i < SuckerSlots; i++)
            if (i < _pearlCount) _pearls[i].MaterialOverride = pearls[i];
        Selected = Math.Clamp(Selected, 0, Math.Max(0, _pearlCount - 1));
    }

    /// <summary>World positions of the held pearls (for labels while reviewing).</summary>
    public Vector3 PearlGlobalPosition(int i) => _pearls[i].GlobalPosition;

    /// <summary>
    /// speed01: swim speed relative to base speed. velocity: her velocity in view space (m/s);
    /// yawRate and pitchRate: how fast she turns (rad/s). The arm trails against all three, like a limb in water.
    /// </summary>
    public void Tick(float dt, float speed01, Vector3 velocity = default, float yawRate = 0f, float pitchRate = 0f)
    {
        _time += dt;
        _swimPhase += dt * (2f + speed01 * 6f);
        _swim = Mathf.MoveToward(_swim, Mathf.Min(speed01, 1f) * 0.6f, dt * 2f);
        _hurt = Mathf.MoveToward(_hurt, 0f, dt * 3f);
        Spring(ref _throw, ref _throwVel, 0f, 260f, 20f, dt);
        Spring(ref _whip, ref _whipVel, 0f, 70f, 11f, dt);
        Spring(ref _review, ref _reviewVel, _reviewing ? 1f : 0f, 120f, 16f, dt);

        // Moving forward pushes the arm back toward her, strafing sways it the other way, sinking lifts it;
        // turning leaves the tip lagging behind. Teleports and dashes are clamped so it never flies off.
        Vector3 target = new Vector3(
            -velocity.X * 0.006f + yawRate * 0.012f,
            -velocity.Y * 0.007f - pitchRate * 0.012f,
            -velocity.Z * 0.006f);
        target = target.LimitLength(0.05f);
        SpringV(ref _drift, ref _driftVel, target, 30f, 9f, dt);

        _skin.SetShaderParameter("flush", _hurt);
        _skin.SetShaderParameter("glow", Mathf.Clamp(_throw * 2f, 0f, 1f));
        _suckerMaterial.SetShaderParameter("flush", _hurt);
        Rebuild();
    }

    /// <summary>Damped spring, substepped at 240 Hz so stiff springs stay stable at any frame rate.</summary>
    static void Spring(ref float x, ref float v, float target, float stiffness, float damping, float dt)
    {
        const float maxStep = 1f / 240f;
        while (dt > 0f)
        {
            float h = MathF.Min(dt, maxStep);
            v += ((target - x) * stiffness - v * damping) * h;
            x += v * h;
            dt -= h;
        }
    }

    static void SpringV(ref Vector3 x, ref Vector3 v, Vector3 target, float stiffness, float damping, float dt)
    {
        const float maxStep = 1f / 240f;
        while (dt > 0f)
        {
            float h = MathF.Min(dt, maxStep);
            v += ((target - x) * stiffness - v * damping) * h;
            x += v * h;
            dt -= h;
        }
    }

    // ───────────────────────── poses ─────────────────────────

    static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
    {
        float u = 1f - t;
        return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
    }

    /// <summary>
    /// The throwing arm: in from the bottom right in a relaxed S-curve, the tip loosely toward the crosshair.
    /// It floats: a slow drift in all three axes, trailing against her movement and turns.
    /// </summary>
    Vector3 GunPose(float s, float review)
    {
        Vector3 lunge = new Vector3(-0.02f, 0.02f, -0.1f) * _throw;
        Vector3 whip = new Vector3(0.04f, -0.08f, 0.14f) * _whip;
        Vector3 shake = new Vector3(Mathf.Sin(_time * 53f), Mathf.Sin(_time * 47f + 1f), 0f) * 0.012f * _hurt;
        Vector3 aside = new Vector3(0.06f, -0.1f, 0.05f) * review;

        var a = new Vector3(0.4f, -0.5f, -0.1f);
        var b = new Vector3(0.36f, -0.26f, -0.3f) + whip * 0.5f;
        var c = new Vector3(0.2f, -0.24f, -0.4f) + lunge * 0.6f + whip;
        var d = new Vector3(0.15f, -0.15f, -0.53f) + lunge + whip;
        Vector3 p = Bezier(a, b, c, d, s);

        // Floating: slow, unsynchronised drifts that grow toward the tip, so the arm never looks posed.
        float w = s * s;
        Vector3 floatDrift = new Vector3(
            Mathf.Sin(_time * 0.7f + s * 1.5f) * 0.009f,
            Mathf.Sin(_time * 0.53f + 1.3f + s * 2.2f) * 0.011f,
            Mathf.Sin(_time * 0.41f + 2.1f) * 0.006f) * w;
        // A ripple running down the arm while swimming, and a tip that curls and uncurls.
        float ripple = Mathf.Sin(s * 6f - _swimPhase) * 0.014f * _swim * s;
        float tip = Mathf.Max(0f, s - 0.7f) / 0.3f;
        Vector3 curl = new Vector3(-0.3f, 0.7f, 0.2f) * (0.03f * tip * tip * (0.55f + 0.45f * Mathf.Sin(_time * 0.8f)));
        // Water drag from her movement, felt most at the tip.
        Vector3 drag = _drift * Mathf.Pow(s, 1.4f);
        return p + floatDrift + new Vector3(0f, ripple, 0f) + curl + drag + shake * s + aside;
    }

    /// <summary>The inventory arm held up along the left of the view, tip curling inward.</summary>
    Vector3 ReviewPose(float s)
    {
        float bow = Mathf.Sin(s * Mathf.Pi);
        float tip = Mathf.Max(0f, s - 0.82f) / 0.18f;
        float x = Mathf.Lerp(-0.31f, -0.25f, s) - 0.025f * bow + 0.05f * tip * tip + Mathf.Sin(_time * 1.1f + s * 3f) * 0.003f;
        float y = -0.4f + 0.66f * s - 0.03f * tip * tip;
        float z = -0.42f - 0.03f * bow;
        return new Vector3(x, y, z);
    }

    static float GunRadius(float s) => 0.05f * Mathf.Pow(1f - s, 0.6f) + 0.006f;

    static float InventoryRadius(float s) => 0.036f * Mathf.Pow(1f - s, 0.65f) + 0.002f;

    /// <summary>The throwing arm's suckers face the eye, both rows in view.</summary>
    static Vector3 GunBelly(Vector3 p) => (Vector3.Zero - p).Normalized();

    /// <summary>The inventory arm's suckers face the camera, turned slightly inward.</summary>
    static Vector3 InventoryBelly(Vector3 p) => new Vector3(0.4f, 0f, 1f).Normalized();

    // ───────────────────────── mesh ─────────────────────────

    void Rebuild()
    {
        float review = Mathf.SmoothStep(0f, 1f, Mathf.Clamp(_review, 0f, 1f));
        _mesh.ClearSurfaces();
        _mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, _skin);

        for (int i = 0; i <= Segments; i++) _points[i] = GunPose(i / (float)Segments, review);
        Tube(GunRadius, GunBelly);
        PlaceGunSuckers();

        bool showInventory = review > 0.01f;
        if (showInventory)
        {
            // Rises from below the view into the review pose.
            Vector3 below = new(0f, -0.65f * (1f - review), 0.1f * (1f - review));
            for (int i = 0; i <= Segments; i++) _points[i] = ReviewPose(i / (float)Segments) + below;
            Tube(InventoryRadius, InventoryBelly);
            PlaceInventorySuckers(review);
        }
        for (int i = 0; i < Suckers; i++)
        {
            _invSuckers[i].Visible = showInventory;
            _pearls[i].Visible = showInventory && i < _pearlCount;
        }
        _mesh.SurfaceEnd();
    }

    /// <summary>
    /// The skin: a tapered tube along <see cref="_points"/>. Each ring is framed by the belly direction, so
    /// UV.y = 0 runs down the middle of the sucker side and the shader can paint belly and back.
    /// </summary>
    void Tube(Func<float, float> radiusAt, Func<Vector3, Vector3> bellyAt)
    {
        float length = 0f;
        for (int i = 0; i <= Segments; i++)
        {
            float s = i / (float)Segments;
            float radius = radiusAt(s);
            Vector3 tangent = (_points[Mathf.Min(i + 1, Segments)] - _points[Mathf.Max(i - 1, 0)]).Normalized();
            Vector3 belly = bellyAt(_points[i]);
            belly = (belly - tangent * belly.Dot(tangent)).Normalized();
            Vector3 side = tangent.Cross(belly).Normalized();
            _belly[i] = belly;
            _side[i] = side;
            if (i > 0) length += _points[i].DistanceTo(_points[i - 1]);

            // The very tip closes into a rounded point.
            float cap = i == Segments ? 0.2f : 1f;
            for (int k = 0; k <= Sides; k++)
            {
                float a = k / (float)Sides * Mathf.Tau;
                Vector3 dir = belly * Mathf.Cos(a) + side * Mathf.Sin(a);
                _ring[k] = _points[i] + dir * radius * cap;
            }

            if (i > 0)
            {
                float sPrev = (i - 1) / (float)Segments;
                float lengthPrev = length - _points[i].DistanceTo(_points[i - 1]);
                for (int k = 0; k < Sides; k++)
                {
                    float v0 = k / (float)Sides, v1 = (k + 1) / (float)Sides;
                    Vertex(_prevRing[k], _points[i - 1], tangent, new Vector2(lengthPrev, v0), new Vector2(sPrev, radiusAt(sPrev)));
                    Vertex(_ring[k + 1], _points[i], tangent, new Vector2(length, v1), new Vector2(s, radius));
                    Vertex(_prevRing[k + 1], _points[i - 1], tangent, new Vector2(lengthPrev, v1), new Vector2(sPrev, radiusAt(sPrev)));
                    Vertex(_prevRing[k], _points[i - 1], tangent, new Vector2(lengthPrev, v0), new Vector2(sPrev, radiusAt(sPrev)));
                    Vertex(_ring[k], _points[i], tangent, new Vector2(length, v0), new Vector2(s, radius));
                    Vertex(_ring[k + 1], _points[i], tangent, new Vector2(length, v1), new Vector2(s, radius));
                }
            }
            Array.Copy(_ring, _prevRing, Sides + 1);
        }
    }

    void Vertex(Vector3 p, Vector3 center, Vector3 tangent, Vector2 uv, Vector2 uv2)
    {
        _mesh.SurfaceSetNormal((p - center).Normalized());
        _mesh.SurfaceSetTangent(new Plane(tangent, 1f));
        _mesh.SurfaceSetUV(uv);
        _mesh.SurfaceSetUV2(uv2);
        _mesh.SurfaceAddVertex(p);
    }

    /// <summary>Position along the arm of sucker <paramref name="index"/> (0 = base end): pairs evenly spaced, the two rows staggered.</summary>
    static float SuckerS(int index, float from, float to)
    {
        int pair = index / 2;
        float step = (to - from) / SuckerPairs;
        return from + step * (pair + (index % 2 == 0 ? 0.25f : 0.75f));
    }

    /// <summary>Where sucker <paramref name="index"/> sits on the current tube, and the way it faces.</summary>
    (Vector3 At, Vector3 Normal, Vector3 Tangent, float Size) SuckerFrame(int index, float from, float to, Func<float, float> radiusAt, float sizeScale)
    {
        float s = SuckerS(index, from, to);
        float f = Mathf.Clamp(s, 0f, 1f) * Segments;
        int i0 = Math.Min((int)f, Segments - 1);
        float t = f - i0;
        Vector3 p = _points[i0].Lerp(_points[i0 + 1], t);
        Vector3 tangent = (_points[i0 + 1] - _points[i0]).Normalized();
        Vector3 belly = _belly[i0].Lerp(_belly[i0 + 1], t).Normalized();
        Vector3 side = _side[i0].Lerp(_side[i0 + 1], t).Normalized();

        float angle = index % 2 == 0 ? -RowAngle : RowAngle;
        Vector3 n = (belly * Mathf.Cos(angle) + side * Mathf.Sin(angle)).Normalized();
        float radius = radiusAt(s);
        float size = radius * sizeScale;
        return (p + n * radius * 0.86f, n, tangent, size);
    }

    static Transform3D Oriented(Vector3 at, Vector3 n, Vector3 tangent, float size)
    {
        Vector3 x = tangent.Cross(n).Normalized();
        Vector3 z = x.Cross(n).Normalized();
        return new Transform3D(new Basis(x * size, n * size * 0.85f, z * size), at);
    }

    /// <summary>
    /// The throwing arm's 16 suckers. Bubbles sit in evenly spread suckers (capacity may be fewer than 16):
    /// bubble 0 nearest the base, thrown last; the next to regrow swells in the suckers' base-end order.
    /// </summary>
    void PlaceGunSuckers()
    {
        var frames = new (Vector3 At, Vector3 Normal, Vector3 Tangent, float Size)[Suckers];
        for (int i = 0; i < Suckers; i++)
        {
            frames[i] = SuckerFrame(i, 0.34f, 0.97f, GunRadius, 0.46f);
            _gunSuckers[i].Transform = Oriented(frames[i].At, frames[i].Normal, frames[i].Tangent, frames[i].Size);
            _gunSuckers[i].SetInstanceShaderParameter("holding", 0f);
            _bubbles[i].Visible = false;
        }

        for (int b = 0; b < _capacity; b++)
        {
            int index = Math.Min(Suckers - 1, (int)((b + 0.5f) * Suckers / _capacity));
            float grow = b < _bubbleCount ? 1f : b == _bubbleCount ? _regrow : 0f;
            if (grow <= 0.02f) continue;
            var (at, n, _, size) = frames[index];
            float bubbleSize = size * 0.95f * Mathf.Sqrt(grow);
            _bubbles[index].Visible = true;
            _bubbles[index].Transform = new Transform3D(Basis.Identity.Scaled(new Vector3(bubbleSize, bubbleSize, bubbleSize)), at + n * bubbleSize * 0.7f);
            _gunSuckers[index].SetInstanceShaderParameter("holding", grow);
        }
    }

    /// <summary>The inventory arm's suckers, each holding a pearl, turned toward the camera.</summary>
    void PlaceInventorySuckers(float review)
    {
        for (int i = 0; i < Suckers; i++)
        {
            var (at, n, tangent, size) = SuckerFrame(i, 0.27f, 0.97f, InventoryRadius, 0.5f);
            _invSuckers[i].Transform = Oriented(at, n, tangent, size);
            _invSuckers[i].SetInstanceShaderParameter("holding", i < _pearlCount ? 1f : 0f);

            bool selected = review > 0.5f && i == Selected;
            float pearlSize = size * 0.95f * (selected ? 1.4f : 1f);
            _pearls[i].Transform = new Transform3D(Basis.Identity.Scaled(new Vector3(pearlSize, pearlSize, pearlSize)), at + n * pearlSize * 0.7f);
            if (_pearls[i].MaterialOverride is ShaderMaterial pearl)
                pearl.SetShaderParameter("highlight", selected ? 0.5f + 0.5f * Mathf.Sin(_time * 6f) : 0f);
        }
    }

    // ───────────────────────── the sucker mesh ─────────────────────────

    /// <summary>
    /// One sucker, unit radius, facing +Y: a skirt that melts into the skin, a raised pale ring, a rosy cup
    /// with fine radial grooves, and a dark pit in the middle. Built outside-in so it paints correctly
    /// without a depth test. COLOR.a marks the ring's crest.
    /// </summary>
    static ArrayMesh BuildSucker()
    {
        (float R, float H, Color C, float Crest)[] profile =
        {
            (1.18f, -0.06f, new Color(0.98f, 0.66f, 0.56f), 0f), // skirt, the skin's belly colour
            (1.0f, 0.1f, new Color(0.98f, 0.68f, 0.58f), 0.1f),
            (0.9f, 0.3f, new Color(0.97f, 0.64f, 0.54f), 0.6f),  // outer wall of the ring
            (0.79f, 0.4f, new Color(0.99f, 0.73f, 0.62f), 1f),   // crest
            (0.68f, 0.33f, new Color(0.95f, 0.6f, 0.54f), 0.5f), // inner lip
            (0.55f, 0.16f, new Color(0.92f, 0.48f, 0.46f), 0f),  // cup wall
            (0.32f, 0.06f, new Color(0.84f, 0.38f, 0.4f), 0f),   // cup floor
            (0.15f, 0.03f, new Color(0.66f, 0.22f, 0.28f), 0f),  // pit rim
            (0f, -0.08f, new Color(0.3f, 0.05f, 0.09f), 0f),     // pit
        };
        const int around = 24;
        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var colors = new List<Color>();
        var indices = new List<int>();

        for (int r = 0; r < profile.Length; r++)
        {
            var prev = profile[Math.Max(r - 1, 0)];
            var next = profile[Math.Min(r + 1, profile.Length - 1)];
            float dr = next.R - prev.R, dh = next.H - prev.H;
            var n2 = new Vector2(dh, -dr).Normalized();
            if (r == profile.Length - 1) n2 = new Vector2(0f, 1f);
            for (int k = 0; k < around; k++)
            {
                float a = k / (float)around * Mathf.Tau;
                var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var (radius, height, color, crest) = profile[r];
                // Radial grooves in the cup.
                if (r >= 5 && r <= 6) color = color * (0.9f + 0.1f * Mathf.Cos(a * 14f));
                verts.Add(radial * radius + Vector3.Up * height);
                normals.Add((radial * n2.X + Vector3.Up * n2.Y).Normalized());
                colors.Add(new Color(color.R, color.G, color.B, crest));
            }
        }
        for (int r = 0; r + 1 < profile.Length; r++)
        for (int k = 0; k < around; k++)
        {
            int a = r * around + k, b = r * around + (k + 1) % around;
            int c = (r + 1) * around + (k + 1) % around, d = (r + 1) * around + k;
            // Clockwise seen from above (+Y) is front-facing in Godot.
            indices.AddRange(new[] { a, b, c, a, c, d });
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = verts.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = normals.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }
}
