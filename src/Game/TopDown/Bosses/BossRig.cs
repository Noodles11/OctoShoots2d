using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Bosses;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown.Bosses;

/// <summary>What the sim says the boss is doing, for its rig to pose.</summary>
public struct RigPose
{
    public BossStage Stage;
    public BossAct Act;
    /// <summary>Seconds in its act, its telegraph's length, and how far through the telegraph it is (0..1).</summary>
    public float ActTime, Telegraph, Warn;
    /// <summary>Its pearl is open to her light now.</summary>
    public bool Open;
    /// <summary>A volley or pulse went out this frame.</summary>
    public bool Fired, Pulsed;
    /// <summary>0 while it fights; 0 → 1 as its mutations melt away in the freeing.</summary>
    public float Melt;
    /// <summary>The snap's warning is counting down; the snap just struck (0 → 1 after it).</summary>
    public bool SnapWarn;
    public float SnapSlam;
    /// <summary>Moving (creeping, leaving) this fast, m/s.</summary>
    public float Speed;
    public float Time, Dt;
}

/// <summary>
/// A generated boss's body (docs/BOSS-GENERATOR.md §13): built once from its BossSpec in its own frame (+X its facing, Y
/// up, the seat at y = 0), its mutations grown at their sites, and posed every frame from the sim. The shared pieces —
/// the pearl, the crust, the drip, the freeing — are BossView's; the rig says where they go on it.
/// </summary>
public abstract partial class BossRig : Node3D
{
    protected BossSpec Spec = null!;
    public BossSpec SpecOf => Spec;
    protected ShaderMaterial Skin = null!;
    /// <summary>Everything drawn with the boss's skin (its instance parameters are set together).</summary>
    protected readonly List<GeometryInstance3D> Skinned = new();
    /// <summary>The mutation parts at their sites (Grown: shown only once the mutation has advanced that far).</summary>
    public readonly List<(Node3D Node, BodySite Site, int Grown)> Mutations = new();
    /// <summary>The ink sacs (Coax out: each tracks one of the sim's sacs, in order).</summary>
    public readonly List<Node3D> Sacs = new();
    static ShaderMaterial? _orb;

    /// <summary>Its radius on the plane (m), and its stretch along the facing.</summary>
    protected float R => Spec.Radius;

    /// <summary>The pearl's height over the seat where it sits on this body.</summary>
    public float PearlHeight { get; protected set; } = 0.4f;

    /// <summary>Where the crust's lumps cling (its back, the top of its bell), and the point on it at polar (angle, out).</summary>
    public virtual Node3D CrustParent => this;
    public abstract Vector3 Surface(float angle, float out_);

    /// <summary>Its true colour shows on the freed body: true scale relative to the mutated one.</summary>
    public float TrueScale => Spec.Reference ? 1f : Spec.TrueScale;

    public void Build(BossSpec spec)
    {
        Spec = spec;
        Skin = MakeSkin();
        BuildBody();
        BuildMutations();
    }

    /// <summary>The boss's skin: boss_body (shell, skin) or boss_jelly (a bell), in its true colours.</summary>
    protected virtual ShaderMaterial MakeSkin()
    {
        var m = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/boss_body.gdshader") };
        m.SetShaderParameter("body_color", ToColor(Spec.TrueColor));
        m.SetShaderParameter("accent_color", ToColor(Spec.Accent));
        return m;
    }

    protected abstract void BuildBody();

    public abstract void Pose(in RigPose pose);

    /// <summary>The skin's state on every part: drained (1 corrupted → 0 freed), the hit flash, the ink surging, the fade.</summary>
    public void SetSkin(float drained, float flash, float surge, float fade)
    {
        foreach (var g in Skinned)
        {
            if (!IsInstanceValid(g)) continue;
            g.SetInstanceShaderParameter("drained", drained);
            g.SetInstanceShaderParameter("flash", flash);
            g.SetInstanceShaderParameter("surge", surge);
            // Leaving, it fades out (any material: the instance's own transparency).
            g.Transparency = 1f - fade;
        }
    }

    protected MeshInstance3D Part(Mesh mesh, Node3D parent, Material? material = null)
    {
        var m = new MeshInstance3D { Mesh = mesh, MaterialOverride = material ?? Skin };
        parent.AddChild(m);
        if (material is null || material == Skin) Skinned.Add(m);
        return m;
    }

    // ───────────────────────── the corruption on the body ─────────────────────────

    /// <summary>A body site in the rig's frame, on the plane (y = 0).</summary>
    protected Vector3 SiteLocal(BodySite s) =>
        new(Mathf.Cos(s.Angle) * s.Out * R * Spec.Stretch, 0f, Mathf.Sin(s.Angle) * s.Out * R);

    /// <summary>The ink's stain at a point of the body: near the mutation sites and the pearl (§5: densest round the pearl).</summary>
    protected float Stain(Vector3 local)
    {
        float s = 0f;
        var flat = new Vector2(local.X, local.Z);
        foreach (var site in Spec.Sites)
        {
            var at = SiteLocal(site);
            float reach = (0.5f + site.Size * 1.8f) * (0.7f + 0.6f * Spec.Mutation);
            s = Mathf.Max(s, 1f - flat.DistanceTo(new Vector2(at.X, at.Z)) / reach);
        }
        var pearl = SiteLocal(Spec.Pearl);
        s = Mathf.Max(s, 0.9f * (1f - flat.DistanceTo(new Vector2(pearl.X, pearl.Z)) / (0.9f + 0.6f * Spec.Mutation)));
        // Deeper, the stain runs over more of the body.
        return Mathf.Clamp(s + 0.25f * Spec.Mutation - 0.1f, 0f, 1f);
    }

    /// <summary>Grows the mutation's parts at its sites, and the extra ones a phase may advance into (hidden until then).</summary>
    void BuildMutations()
    {
        var rng = new RandomNumberGenerator { Seed = Spec.Seed };
        foreach (var site in Spec.Sites) AddMutation(site, Spec.Minor is { } m && site.Sac && m == MutationTheme.InkSacs ? MutationTheme.InkSacs : Spec.Theme, 0);
        // A phase's advance: two more of the dominant theme per step, near the others.
        int advances = 0;
        foreach (var phase in Spec.Phases)
            if (phase.Change == PhaseChange.MutationAdvances) advances++;
        if (Spec.Spice == BossSpice.EarlyAdvance && advances == 0) advances = 1;
        for (int k = 1; k <= advances; k++)
            for (int i = 0; i < 2 && Spec.Sites.Count > 0; i++)
            {
                var near = Spec.Sites[rng.RandiRange(0, Spec.Sites.Count - 1)];
                var grown = near with { Angle = near.Angle + rng.RandfRange(-0.5f, 0.5f), Out = Mathf.Clamp(near.Out + rng.RandfRange(-0.15f, 0.1f), 0.3f, 0.95f) };
                AddMutation(grown, Spec.Theme, 2 * (k - 1) + i + 1);
            }
    }

    void AddMutation(BodySite site, MutationTheme theme, int grown)
    {
        var node = theme == MutationTheme.InkSacs || site.Sac ? Sac(site) : Mutation(theme, site);
        if (node is null) return;
        if (node.GetParent() is null) AddChild(node);
        Mutations.Add((node, site, grown));
        if (site.Sac && grown == 0) Sacs.Add(node);
    }

    /// <summary>A glossy ink sac (corruption_orb) swelling on the body at a site.</summary>
    protected virtual Node3D Sac(BodySite site)
    {
        _orb ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/corruption_orb.gdshader") };
        var at = Surface(site.Angle, site.Out);
        float size = (0.22f + site.Size * 0.5f) * (0.8f + 0.5f * Spec.Mutation);
        var sac = new MeshInstance3D { Mesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 18, Rings = 9 }, MaterialOverride = _orb };
        sac.SetInstanceShaderParameter("ember", 0.12f);
        sac.SetInstanceShaderParameter("wobble", 0.35f);
        sac.SetInstanceShaderParameter("seed", site.Angle * 3.1f);
        var holder = new Node3D { Position = at + Vector3.Up * size * 0.45f, Scale = Vector3.One * size };
        holder.AddChild(sac);
        return holder;
    }

    /// <summary>One part of the mutation theme at a site (the plan's own; null: the theme draws nothing here).</summary>
    protected abstract Node3D? Mutation(MutationTheme theme, BodySite site);

    /// <summary>Paints a mutation part: inked at its root, still dark at its tip.</summary>
    protected static BossMeshes.Paint Inked(float groove = 0.3f) => (_, u, v) => new Color(1f - 0.25f * u, 0f, groove);

    /// <summary>
    /// Shows the mutation as far as it has advanced, the sacs the sim still has, and melts it all away in the freeing
    /// (the parts shrink back into the body).
    /// </summary>
    public void SyncMutations(PlaneBoss boss, float melt, float time)
    {
        int s = 0;
        for (int i = 0; i < Mutations.Count; i++)
        {
            var (node, site, grown) = Mutations[i];
            bool shown = grown <= boss.Grown;
            if (site.Sac && grown == 0 && boss.Spec.Exposure == Exposure.CoaxOut)
            {
                shown &= s < boss.Sacs.Count && boss.Sacs[s].Alive;
                s++;
            }
            node.Visible = shown && melt < 0.999f;
            float k = 1f - Mathf.SmoothStep(0f, 1f, melt);
            // Sacs throb slowly; a sac swells while the body warns of a sac burst.
            float throb = site.Sac ? 1f + 0.06f * Mathf.Sin(time * 2.2f + site.Angle * 3f) : 1f;
            node.Scale = BaseScale(node) * (k * throb);
        }
    }

    readonly Dictionary<Node3D, Vector3> _baseScale = new();

    Vector3 BaseScale(Node3D node)
    {
        if (!_baseScale.TryGetValue(node, out var s)) _baseScale[node] = s = node.Scale;
        return s;
    }

    public static Color ToColor(System.Numerics.Vector3 c) => new(c.X, c.Y, c.Z);
}
