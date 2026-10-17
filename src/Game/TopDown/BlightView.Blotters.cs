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
    const int SlugSegments = BlotterTuning.Segments;

    sealed class Slug
    {
        public Node3D Node = null!;
        public MeshInstance3D[] Body = null!;
        public MeshInstance3D[] Stalks = null!, Eyes = null!;
        public float Drip;
    }

    readonly Dictionary<Blotter, Slug> _slugs = new();
    readonly Dictionary<CorruptionBomb, (MeshInstance3D Ball, MeshInstance3D Mark)> _bombs = new();

    /// <summary>
    /// Corruption bombs: a wobbling ink ball arcing from a Blotter's mouth, and on the ground where it will land a dark
    /// ring that tightens and darkens as it falls (the warning to get out).
    /// </summary>
    void SyncBombs(PlaneWorld world)
    {
        var live = new HashSet<CorruptionBomb>(world.Bombs);
        var gone = new List<CorruptionBomb>();
        foreach (var (bomb, view) in _bombs)
            if (!live.Contains(bomb))
            {
                view.Ball.QueueFree();
                view.Mark.QueueFree();
                gone.Add(bomb);
            }
        foreach (var bomb in gone) _bombs.Remove(bomb);
        foreach (var bomb in world.Bombs)
        {
            if (!_bombs.TryGetValue(bomb, out var view))
            {
                var ball = new MeshInstance3D { Mesh = _sphere, MaterialOverride = _orb, Scale = Vector3.One * 0.32f, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
                ball.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("ember"), 0.08f);
                ball.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("wobble"), 1.4f);
                ball.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("seed"), bomb.From.X * 0.3f);
                var mark = new MeshInstance3D { Mesh = _quad, MaterialOverride = _ring, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
                mark.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("seed"), bomb.To.Y * 0.2f);
                AddChild(ball);
                AddChild(mark);
                _bombs[bomb] = view = (ball, mark);
            }
            float t = bomb.T;
            var at = bomb.Position;
            float from = LevelMap.SwimBand + 1.2f, to = Floor(bomb.To) + 0.2f;
            // A lob: up from the mouth and down onto the ground.
            float y = Mathf.Lerp(from, to, t) + 3.2f * 4f * t * (1f - t);
            view.Ball.Position = new Vector3(at.X, y, at.Y);
            float r = BlotterTuning.SplashRadius * (1.25f - 0.25f * t);
            view.Mark.Position = new Vector3(bomb.To.X, Floor(bomb.To) + 0.07f, bomb.To.Y);
            view.Mark.Scale = new Vector3(r, 1f, r);
            view.Mark.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("tint"), new Color(0.05f, 0.02f, 0.08f, 0.35f + 0.5f * t));
            view.Mark.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("width"), 0.12f + 0.1f * t);
        }
    }

    void ClearBombs()
    {
        foreach (var (ball, mark) in _bombs.Values)
        {
            ball.QueueFree();
            mark.QueueFree();
        }
        _bombs.Clear();
    }

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
            body[i].SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("seed"), b.Seed + i * 1.9f);
            body[i].SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("ember"), i == 0 ? 0.15f : 0.05f);
            body[i].SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("wobble"), 0.5f);
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
            stalks[i].SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("ember"), 0f);
            stalks[i].SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("wobble"), 0f);
            eyes[i] = new MeshInstance3D { Mesh = _sphere, MaterialOverride = _sacMat, Scale = Vector3.One * 0.13f, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            eyes[i].SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("ember"), 1.2f);
            eyes[i].SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("wobble"), 0f);
            node.AddChild(stalks[i]);
            node.AddChild(eyes[i]);
        }
        return new Slug { Node = node, Body = body, Stalks = stalks, Eyes = eyes };
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
            // Rising out of the floor when born.
            float rise = b.Rise > 0f ? 1f - Mathf.Clamp(b.Rise / 1.5f, 0f, 1f) : 1f;
            for (int i = 0; i < SlugSegments; i++)
            {
                // The sim's own body segments (its hitboxes), so what is drawn is what takes hits.
                var (at, _) = b.Segment(i);
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
}
