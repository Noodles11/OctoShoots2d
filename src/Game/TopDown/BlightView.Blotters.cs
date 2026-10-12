using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Blotters on screen (docs/CORRUPTION.md): a giant ink slug, a chain of glossy dark mounds from the floor up past her
/// plane that follow its head along its trail, two eye stalks tipped with violet, and ink beading off it as it creeps.
/// It rises out of the floor when born and bursts into gold when cleansed.
/// </summary>
public partial class BlightView
{
    const int SlugSegments = 6;
    const float SlugSpacing = 0.75f;

    sealed class Slug
    {
        public Node3D Node = null!;
        public MeshInstance3D[] Body = null!;
        public MeshInstance3D[] Stalks = null!, Eyes = null!;
        /// <summary>Where its head has been (newest first), a point every 0.25 m.</summary>
        public readonly List<System.Numerics.Vector2> Trail = new();
        public float Drip;
    }

    readonly Dictionary<Blotter, Slug> _slugs = new();

    void ClearSlugs()
    {
        foreach (var s in _slugs.Values) s.Node.QueueFree();
        _slugs.Clear();
    }

    Slug MakeSlug(Blotter b)
    {
        var node = new Node3D();
        AddChild(node);
        var body = new MeshInstance3D[SlugSegments];
        for (int i = 0; i < SlugSegments; i++)
        {
            body[i] = new MeshInstance3D { Mesh = _sphere, MaterialOverride = _orb, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            body[i].SetInstanceShaderParameter("seed", b.Seed + i * 1.9f);
            body[i].SetInstanceShaderParameter("ember", i == 0 ? 0.15f : 0.05f);
            body[i].SetInstanceShaderParameter("wobble", 0.5f);
            node.AddChild(body[i]);
        }
        var stalks = new MeshInstance3D[2];
        var eyes = new MeshInstance3D[2];
        for (int i = 0; i < 2; i++)
        {
            stalks[i] = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.12f, Height = 1f, RadialSegments = 6, Rings = 1 },
                MaterialOverride = _orb,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            stalks[i].SetInstanceShaderParameter("ember", 0f);
            stalks[i].SetInstanceShaderParameter("wobble", 0f);
            eyes[i] = new MeshInstance3D { Mesh = _sphere, MaterialOverride = _sacMat, Scale = Vector3.One * 0.13f, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            eyes[i].SetInstanceShaderParameter("ember", 1.2f);
            eyes[i].SetInstanceShaderParameter("wobble", 0f);
            node.AddChild(stalks[i]);
            node.AddChild(eyes[i]);
        }
        var slug = new Slug { Node = node, Body = body, Stalks = stalks, Eyes = eyes };
        for (int i = 0; i < SlugSegments * 4; i++) slug.Trail.Add(b.Position - b.Heading * (i * 0.25f));
        return slug;
    }

    void SyncSlugs(PlaneWorld world, float dt)
    {
        var alive = new HashSet<Blotter>(world.Blotters);
        var gone = new List<Blotter>();
        foreach (var (b, s) in _slugs)
            if (!alive.Contains(b))
            {
                s.Node.QueueFree();
                gone.Add(b);
            }
        foreach (var b in gone) _slugs.Remove(b);

        foreach (var b in world.Blotters)
        {
            if (!_slugs.TryGetValue(b, out var slug)) _slugs[b] = slug = MakeSlug(b);
            // Its trail: the body follows where the head has been.
            if (System.Numerics.Vector2.Distance(b.Position, slug.Trail[0]) >= 0.25f) slug.Trail.Insert(0, b.Position);
            else slug.Trail[0] = b.Position;
            while (slug.Trail.Count > SlugSegments * 4 + 2) slug.Trail.RemoveAt(slug.Trail.Count - 1);
            // Rising out of the floor when born.
            float rise = b.Rise > 0f ? 1f - Mathf.Clamp(b.Rise / 1.5f, 0f, 1f) : 1f;
            for (int i = 0; i < SlugSegments; i++)
            {
                var at = Along(slug.Trail, i * SlugSpacing);
                float floor = Floor(at);
                float taper = i == 0 ? 0.85f : 1f - 0.12f * (i - 1);
                // A slow wave runs down its back as it creeps.
                float wave = 1f + 0.08f * Mathf.Sin(_time * 3f - i * 0.9f + b.Seed);
                float top = LevelMap.SwimBand + 0.35f;
                float half = Mathf.Max((top - floor) * 0.5f, 0.4f) * taper * wave * rise;
                float wide = BlotterTuning.Radius * taper * (2f - wave) * rise;
                slug.Body[i].Position = new Vector3(at.X, floor + half * 0.9f, at.Y);
                slug.Body[i].Scale = new Vector3(wide, half, wide);
            }
            // Eye stalks: up and forward from the head, swaying.
            var head = slug.Body[0].Position;
            var fwd = new Vector3(b.Heading.X, 0f, b.Heading.Y);
            var side = new Vector3(-fwd.Z, 0f, fwd.X);
            for (int i = 0; i < 2; i++)
            {
                float sway = Mathf.Sin(_time * 2.4f + b.Seed + i * 1.7f) * 0.15f;
                var root = head + Vector3.Up * slug.Body[0].Scale.Y * 0.7f + fwd * 0.4f + side * (i == 0 ? 0.35f : -0.35f);
                var tip = root + (Vector3.Up * 0.9f + fwd * 0.5f + side * ((i == 0 ? 0.25f : -0.25f) + sway)) * rise;
                var mid = (root + tip) * 0.5f;
                var dir = (tip - root).Normalized();
                slug.Stalks[i].Position = mid;
                slug.Stalks[i].Basis = new Basis(new Quaternion(Vector3.Up, dir)).Scaled(new Vector3(1f, (tip - root).Length(), 1f));
                slug.Eyes[i].Position = tip;
            }
            // Ink beads off it as it creeps.
            slug.Drip += dt * 6f * rise;
            while (slug.Drip >= 1f)
            {
                slug.Drip -= 1f;
                int seg = (int)_droplets.RandRange(0f, SlugSegments - 0.01f);
                var p = slug.Body[seg].Position + _droplets.RandDir() * slug.Body[seg].Scale.X * 0.8f;
                _droplets.Emit(p, Vector3.Down * 0.6f + _droplets.RandDir() * 0.3f, _droplets.RandRange(0.5f, 0.9f), 0.12f, 0.03f,
                    new Color(0.03f, 0.01f, 0.05f, 0.9f), new Color(0.2f, 0.06f, 0.35f, 0f), drag: 1.5f);
            }
        }
    }

    /// <summary>The point this far back along a trail (newest first).</summary>
    static System.Numerics.Vector2 Along(List<System.Numerics.Vector2> trail, float distance)
    {
        for (int i = 1; i < trail.Count; i++)
        {
            float seg = System.Numerics.Vector2.Distance(trail[i - 1], trail[i]);
            if (distance <= seg) return trail[i - 1] + (trail[i] - trail[i - 1]) * (seg > 1e-5f ? distance / seg : 0f);
            distance -= seg;
        }
        return trail[^1];
    }
}
