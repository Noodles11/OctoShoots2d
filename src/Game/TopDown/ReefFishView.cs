using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The reef fish on screen (docs/REEF-FISH-PROPOSAL.md §4), presentation only: a MultiMesh per school, per anemone's
/// clownfish and for the tangs (ReefFishMeshes in reef_fish.gdshader), bounded to where its fish are so a group off the
/// screen is not drawn. Each fish swum from the sim's position, heading and speed — its stroke quickening with its pace,
/// a bank into turns, a slow bob, a tang dipping its nose to graze — at its kind's depth in the water: clownfish low over
/// their anemones (drawn here too), chromis schools a little under the swim level, tangs at it.
/// </summary>
public partial class ReefFishView : Node3D
{
    sealed class Pose
    {
        public float Yaw, Roll, Pitch, Tail, Fin, Swim, Height = float.NaN, Time;
        public int Group, Slot;
    }

    /// <summary>Fish drawn together: one kind's, about one anemone or in one school (or all the tangs).</summary>
    sealed class Group
    {
        public MultiMesh Mesh = null!;
        public Vector3 Min, Max;
    }

    /// <summary>A kind's swimming: its cruising speed, its stroke and fin beats (Hz, at rest → at cruise), its shader's reach.</summary>
    sealed record Style(ReefFishMeshes.Spec Spec, float Cruise, float StrokeRest, float StrokeCruise, float FinBeat, float Waves, float WaveAmp, float TailAmp, float FinAmp);

    static readonly Style[] Styles =
    {
        new(ReefFishMeshes.Clownfish, ReefFishTuning.ClownSpeed, 1.2f, 3.4f, 3.6f, 0.8f, 0.07f, 0.35f, 0.6f),
        new(ReefFishMeshes.BlueTang, ReefFishTuning.TangSpeed, 0.5f, 1.8f, 2.2f, 0.7f, 0.04f, 0.22f, 0.75f),
        new(ReefFishMeshes.Chromis, 1.5f, 2f, 5f, 5.5f, 1f, 0.09f, 0.4f, 0.4f),
    };

    static readonly Color[] AnemoneColours = { new(0.74f, 0.62f, 0.42f), new(0.6f, 0.7f, 0.45f), new(0.76f, 0.56f, 0.72f) };

    readonly Mesh[] _bodies = new Mesh[3];
    readonly ShaderMaterial[] _materials = new ShaderMaterial[3];
    ShaderMaterial _anemone = null!;
    Mesh _anemoneMesh = null!;
    readonly List<Group> _groups = new();
    readonly List<Node> _nodes = new();
    readonly List<Pose> _poses = new();
    readonly List<float> _hostTops = new();
    LevelMap? _map;

    public override void _Ready()
    {
        var shader = GD.Load<Shader>("res://assets/shaders/reef_fish.gdshader");
        for (int k = 0; k < Styles.Length; k++)
        {
            var st = Styles[k];
            var material = new ShaderMaterial { Shader = shader };
            material.SetShaderParameter("kind", k);
            material.SetShaderParameter("body_length", st.Spec.Length);
            material.SetShaderParameter("waves", st.Waves);
            material.SetShaderParameter("wave_amp", st.WaveAmp);
            material.SetShaderParameter("tail_amp", st.TailAmp);
            material.SetShaderParameter("fin_amp", st.FinAmp);
            _materials[k] = material;
            _bodies[k] = ReefFishMeshes.Build(st.Spec);
        }
        _anemone = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/anemone.gdshader") };
        _anemone.SetShaderParameter("tip_color", new Color(0.97f, 0.9f, 0.72f));
        _anemoneMesh = ReefMeshes.Anemone();
    }

    MultiMesh Add(Mesh mesh, Material material, int count, bool shadow)
    {
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseCustomData = true, Mesh = mesh, InstanceCount = count };
        var node = new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = material, CastShadow = shadow ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(node);
        _nodes.Add(node);
        return mm;
    }

    /// <summary>A new level: its anemones in place, a pose for every fish.</summary>
    public void Show(PlaneWorld world)
    {
        _map = world.Map;
        foreach (var node in _nodes) node.QueueFree();
        _nodes.Clear();
        _groups.Clear();
        _poses.Clear();
        var index = new Dictionary<(ReefFishKind, int), int>();
        var counts = new List<int>();
        var kinds = new List<ReefFishKind>();
        foreach (var f in world.ReefFish)
        {
            var key = (f.Kind, f.Kind == ReefFishKind.BlueTang ? -1 : f.Host);
            if (!index.TryGetValue(key, out int g))
            {
                index[key] = g = counts.Count;
                counts.Add(0);
                kinds.Add(f.Kind);
            }
            _poses.Add(new Pose { Group = g, Slot = counts[g]++, Time = f.Id * 0.73f });
        }
        for (int g = 0; g < counts.Count; g++)
            _groups.Add(new Group { Mesh = Add(_bodies[(int)kinds[g]], _materials[(int)kinds[g]], counts[g], shadow: false) });

        _hostTops.Clear();
        for (int i = 0; i < world.Anemones.Count; i++)
        {
            var a = world.Anemones[i];
            float floor = world.Map.HeightAt(a.Position);
            var basis = new Basis(Vector3.Up, i * 2.4f).Scaled(Vector3.One * a.Scale);
            var mm = Add(_anemoneMesh, _anemone, 1, shadow: true);
            mm.SetInstanceTransform(0, new Transform3D(basis, new Vector3(a.Position.X, floor, a.Position.Y)));
            var c = AnemoneColours[i % AnemoneColours.Length];
            mm.SetInstanceCustomData(0, new Color(c.R, c.G, c.B, 0f));
            // Where its clownfish nestle: among the tentacles, a little under their tips.
            _hostTops.Add(floor + 0.95f * a.Scale);
        }
    }

    public void Sync(PlaneWorld world, float dt)
    {
        if (_map is null || _poses.Count != world.ReefFish.Count) return;
        foreach (var g in _groups)
        {
            g.Min = Vector3.Inf;
            g.Max = -Vector3.Inf;
        }
        for (int i = 0; i < world.ReefFish.Count; i++)
        {
            var f = world.ReefFish[i];
            var p = _poses[i];
            var st = Styles[(int)f.Kind];
            p.Time += dt;
            float speed = f.Velocity.Length();
            p.Swim = Mathf.Lerp(p.Swim, Mathf.Clamp(speed / st.Cruise, 0f, 1.5f), 1f - Mathf.Exp(-5f * dt));
            p.Tail = Mathf.PosMod(p.Tail + dt * Mathf.Tau * Mathf.Lerp(st.StrokeRest, st.StrokeCruise, Mathf.Min(p.Swim, 1.5f)), Mathf.Tau);
            p.Fin = Mathf.PosMod(p.Fin + dt * Mathf.Tau * st.FinBeat * (0.8f + 0.3f * p.Swim), Mathf.Tau);
            p.Yaw = Mathf.LerpAngle(p.Yaw, -f.Heading, 1f - Mathf.Exp(-16f * dt));
            p.Roll = Mathf.Lerp(p.Roll, Mathf.Clamp(-f.TurnRate * 0.12f, -0.4f, 0.4f), 1f - Mathf.Exp(-6f * dt));
            // A resting tang tips its nose down to graze; a swimmer dips a hair into its stroke.
            float pitch = f.Kind == ReefFishKind.BlueTang && p.Swim < 0.2f ? -0.3f : -0.05f * p.Swim;
            p.Pitch = Mathf.Lerp(p.Pitch, pitch, 1f - Mathf.Exp(-2.5f * dt));

            float floor = _map.HeightAt(f.Position);
            float want = f.Kind switch
            {
                ReefFishKind.Clownfish => _hostTops[f.Host] + (f.Hiding ? -0.2f : 0.12f),
                ReefFishKind.BlueTang => LevelMap.SwimBand + 0.05f,
                _ => LevelMap.SwimBand - 0.3f + 0.5f * (Mathf.PosMod(f.Id * 0.618f, 1f) - 0.5f),
            };
            want = Mathf.Clamp(want, floor + 0.2f, LevelMap.SwimBand + 0.3f);
            p.Height = float.IsNaN(p.Height) ? want : Mathf.Lerp(p.Height, want, 1f - Mathf.Exp(-3f * dt));
            float bob = 0.05f * Mathf.Sin(p.Time * 1.1f + f.Id * 1.9f) * st.Spec.Length / 0.45f;

            float turn = Mathf.Clamp(f.TurnRate / 2.5f, -1f, 1f);
            var basis = new Basis(Vector3.Up, p.Yaw) * new Basis(Vector3.Right, p.Roll) * new Basis(Vector3.Back, p.Pitch);
            var group = _groups[p.Group];
            var at = new Vector3(f.Position.X, p.Height + bob, f.Position.Y);
            group.Mesh.SetInstanceTransform(p.Slot, new Transform3D(basis, at));
            group.Mesh.SetInstanceCustomData(p.Slot, new Color(p.Tail, p.Fin, p.Swim, turn));
            group.Min = group.Min.Min(at);
            group.Max = group.Max.Max(at);
        }
        // Each group's bounds round its fish (a body length and its swing spare), so the camera skips the ones out of view.
        foreach (var g in _groups)
            if (g.Min.X <= g.Max.X) g.Mesh.CustomAabb = new Aabb(g.Min - Vector3.One, g.Max - g.Min + Vector3.One * 2f);
    }
}
