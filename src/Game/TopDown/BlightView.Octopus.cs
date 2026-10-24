using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The Blightroot as it is drawn (docs/CORRUPTION.md), all of it painted as the murklings are: a pole of ink standing
/// from a splay of rootlets on the seabed up to its head at her plane — the target her light drains. The head is a
/// liquid body (the murklings' soft body, BlightView.Murklings): it breathes, swells on the pulse as a swell of ink runs
/// up the pole into it, sways on its pole, and her hits dent it, tear holes in it that close as its corruption grows
/// back, and rip chunks of ink from it.
/// <para>
/// It stands tall, its head well over her plane, so it can stand between her and the camera: its head and pole are
/// then drawn over her where they cover her, as kelp is (over_her.gdshaderinc), and she wears her outline.
/// It stands like a boxing pole, its foot held fast in the sand: its pole tilts on a spring and rings back to upright.
/// Her hits push it back (each by as much as it was strong, a run of them further, never past TiltMax); its hammer
/// (PlaneBlight.StepHammer) rears it back away from her, stretching up, then swings its head down at her and back. Its
/// pulse: as it gathers, it stretches up, then it squashes all the way down to the sand as the ring runs out, and
/// springs back up.
/// </para>
/// <para>
/// Freed, it becomes what it was, smoothly: its ink bursts off its head, the pole runs away from the top down into
/// drips and the rootlets draw back into the sand, while the head takes its own healthy colour and draws out into an
/// octopus's mantle and eight arms grow out under it, hanging, then curling; then it swims away from her in strokes
/// of its arms (each a contraction that drives it on, then a glide as they flare), rising, trailing clean bubbles,
/// fading out in the distance. It has no eyes.
/// </para>
/// </summary>
public partial class BlightView
{
    const int Arms = 8, Rootlets = 7;
    /// <summary>Freed, it changes over ChangeSeconds and is gone FleeSeconds after it was freed.</summary>
    const float ChangeSeconds = 1.1f, FleeSeconds = 3.4f;
    /// <summary>Its colour once freed: a healthy reef octopus.</summary>
    static readonly Color Healthy = new(0.95f, 0.4f, 0.28f);
    /// <summary>Its skin once freed, for the painted ink shading (which keeps a kind's colour dark under the ink): bright, nearly all of it.</summary>
    static readonly Color HealthySkin = new(Healthy.R * 2.4f, Healthy.G * 2.4f, Healthy.B * 2.4f, 0.95f);
    /// <summary>Its head's size (shares of the sac's radius) and the pole's thickness (metres).</summary>
    const float HeadSize = 0.6f, PoleRadius = 0.17f;

    sealed class Root
    {
        public Node3D Node = null!, PoleBase = null!, Holder = null!;
        public MeshInstance3D Head = null!, HeadOver = null!, Pole = null!, PoleOver = null!, Ring = null!;
        public Node3D[] Arms = null!, Rootlets = null!;
        public float[] Angle = null!, Phase = null!;
        public float Floor, Height, Seed;
        public bool Burst;
        public float Since, Drip;
        public Vector3 FleeDir, FleeVelocity, FleeAt;
        /// <summary>How far its pole leans (radians, toward x east and y south), and how fast it is leaning.</summary>
        public Vector2 Tilt, TiltVel;
        /// <summary>How far its pole is drawn out (a share of its length), and how fast.</summary>
        public float Stretch = 1f, StretchVel;
        /// <summary>Its swing: where the strike set off from (its lean and stretch), whether it has landed, and how long ago.</summary>
        public Vector2 StrikeFromTilt;
        public float StrikeFromStretch, SinceLanded = 10f;
        public bool Striking, Landed;
    }

    /// <summary>
    /// The boxing pole: its spring back to upright (lightly damped, so it rings), how far her hits may push it back,
    /// how far its hammer swings it down at her, and how hard a hit pushes (a plain one, and per its share of the meter).
    /// </summary>
    const float PoleStiff = 55f, PoleDamp = 5f, TiltMax = 0.45f, SlamTilt = 1.05f, HitPush = 1.4f, HitPushPerShare = 6f;
    /// <summary>The pulse: it stretches up as it gathers, then squashes all the way down to the sand (Flat) in Down seconds.</summary>
    const float PulseStretch = 0.16f, Flat = 0.07f, Down = 0.07f;
    /// <summary>
    /// Its hammer: rearing back it leans RearTilt away from her and draws up to RearStretch; it rests on what it struck
    /// for Rest seconds, then the water's drag lets it rise back slowly (RecoverStiff, on its spring); on her, its head
    /// sits this far over her plane (on top of her bell).
    /// </summary>
    const float RearTilt = 0.38f, RearStretch = 1.3f, Rest = 0.1f, RecoverStiff = 22f, OnHer = 0.62f;

    ShaderMaterial? _stalkMat, _plainMurk;
    Shader? _murkOverShader;

    static readonly StringName[] HeadParams = { "skin", "seed", "ember", "ember_tint", "swell", "fade", "hit", "worn" };
    static readonly StringName[] PoleParams = { "skin", "seed", "pulse", "melt" };

    /// <summary>Its over-her copies wear what the head and the pole wear.</summary>
    static void Mirror(Root view)
    {
        foreach (var p in HeadParams)
            if (view.Head.GetInstanceShaderParameter(p) is { VariantType: not Variant.Type.Nil } v) view.HeadOver.SetInstanceShaderParameter(p, v);
        foreach (var p in PoleParams)
            if (view.Pole.GetInstanceShaderParameter(p) is { VariantType: not Variant.Type.Nil } v) view.PoleOver.SetInstanceShaderParameter(p, v);
    }

    /// <summary>
    /// Whether a Blightroot stands between her and the camera now (its head or its pole nearer the camera than her bell
    /// and over her on screen): its over-her copies cover her then, and she wears her outline.
    /// </summary>
    public bool Covers(Vector3 bell, Camera3D? camera)
    {
        if (camera is null) return false;
        var fwd = -camera.GlobalBasis.Z;
        float herDepth = (bell - camera.GlobalPosition).Dot(fwd);
        var herPx = camera.UnprojectPosition(bell);
        float px = herPx.DistanceTo(camera.UnprojectPosition(bell + camera.GlobalBasis.X));
        bool Over(Vector3 p, float radius) =>
            (p - camera.GlobalPosition).Dot(fwd) < herDepth - 0.15f && camera.UnprojectPosition(p).DistanceTo(herPx) < (0.55f + radius) * px;
        foreach (var view in _roots)
        {
            if (!view.Node.Visible || view.Node.Position.DistanceTo(new Vector3(bell.X, 0f, bell.Z)) > 8f) continue;
            var head = view.Head.GlobalPosition;
            if (Over(head, view.Head.GlobalBasis.Scale.X)) return true;
            if (!view.Pole.Visible) continue;
            var foot = view.PoleBase.GlobalPosition;
            var top = view.Pole.GlobalTransform * new Vector3(0f, 0.5f, 0f);
            for (int k = 1; k <= 6; k++)
                if (Over(foot.Lerp(top, k / 6f), view.Pole.GlobalBasis.Scale.X)) return true;
        }
        return false;
    }

    /// <summary>One arm (or a rootlet): a tapering tube lying along +X from its base, its tip curling round.</summary>
    static ArrayMesh ArmMesh(float length, float curl)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        const int segs = 18, sides = 7;
        var centre = new Vector3[segs + 1];
        for (int k = 0; k <= segs; k++) centre[k] = ArmPoint(k / (float)segs, length, curl);
        Vector3[]? prev = null;
        for (int k = 0; k <= segs; k++)
        {
            float t = k / (float)segs;
            var fwd = (centre[Mathf.Min(k + 1, segs)] - centre[Mathf.Max(k - 1, 0)]).Normalized();
            var side = fwd.Cross(Vector3.Up).Normalized();
            var up = side.Cross(fwd).Normalized();
            float r = ArmRadius(t);
            var ring = new Vector3[sides];
            for (int s = 0; s < sides; s++)
            {
                float a = s * Mathf.Tau / sides;
                ring[s] = centre[k] + side * Mathf.Cos(a) * r + up * Mathf.Sin(a) * r * (Mathf.Sin(a) > 0f ? 0.8f : 0.45f);
            }
            if (prev is not null)
                for (int s = 0; s < sides; s++)
                {
                    int s1 = (s + 1) % sides;
                    st.AddVertex(prev[s]); st.AddVertex(ring[s]); st.AddVertex(ring[s1]);
                    st.AddVertex(prev[s]); st.AddVertex(ring[s1]); st.AddVertex(prev[s1]);
                }
            prev = ring;
        }
        st.GenerateNormals();
        return st.Commit();
    }

    /// <summary>An arm's radius at a share of its length (thick at its base, thin at the tip).</summary>
    static float ArmRadius(float t) => Mathf.Lerp(0.38f, 0.05f, Mathf.Pow(t, 0.75f));

    /// <summary>A point along an arm (local: out along +X, a little off the floor), its last third curling.</summary>
    static Vector3 ArmPoint(float t, float length, float curl)
    {
        float x = 0.3f + t * length;
        var p = new Vector3(x, 0.2f * (1f - t) + 0.08f + 0.05f * Mathf.Sin(t * 5f), 0f);
        if (t > 0.65f)
        {
            float u = (t - 0.65f) / 0.35f;
            p.Z += curl * u * u * length * 0.28f;
            p.X -= u * u * length * 0.08f;
        }
        return p;
    }

    Root MakeRoot(Blightroot root)
    {
        _murkShader ??= GD.Load<Shader>("res://assets/shaders/murkling.gdshader");
        _stalkMat ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/ink_stalk.gdshader") };
        _plainMurk ??= new ShaderMaterial { Shader = _murkShader };
        float floor = Floor(root.Position);
        var node = new Node3D { Position = new Vector3(root.Position.X, 0f, root.Position.Y) };
        AddChild(node);
        float seed = root.Position.X * 0.37f + root.Position.Y * 0.11f;
        var view = new Root
        {
            Node = node, Floor = floor, Seed = seed, Arms = new Node3D[Arms], Rootlets = new Node3D[Rootlets],
            Angle = new float[Arms], Phase = new float[Arms], Height = Mathf.Max(SacHeight - floor - 0.35f, 0.5f),
        };
        // Rootlets splayed over the sand round its foot.
        for (int i = 0; i < Rootlets; i++)
        {
            float a = i * Mathf.Tau / Rootlets + 0.3f * Mathf.Sin(seed + i * 2.1f);
            var pivot = new Node3D { Position = new Vector3(0f, floor, 0f), Basis = new Basis(Vector3.Up, -a) * new Basis(Vector3.Back, -0.08f), Scale = Vector3.One * 0.34f };
            node.AddChild(pivot);
            var r = new MeshInstance3D { Mesh = ArmMesh(1.8f + 0.9f * (0.5f + 0.5f * Mathf.Sin(seed * 1.3f + i * 1.7f)), i % 2 == 0 ? 1f : -1f), MaterialOverride = _plainMurk, CastShadow = GeometryInstance3D.ShadowCastingSetting.On };
            Paint(r, Kin.Skin(Kin.Blightroot, 0.55f), seed + i);
            r.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("hollow"), 0f);
            pivot.AddChild(r);
            view.Rootlets[i] = pivot;
        }
        // The pole, from the sand up to its head.
        view.PoleBase = new Node3D { Position = new Vector3(0f, floor, 0f) };
        node.AddChild(view.PoleBase);
        view.Pole = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.65f, BottomRadius = 1f, Height = 1f, RadialSegments = 14, Rings = 12 },
            MaterialOverride = _stalkMat,
            Position = new Vector3(0f, view.Height * 0.5f, 0f),
            Scale = new Vector3(PoleRadius, view.Height, PoleRadius),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
        };
        view.Pole.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("skin"), Kin.Skin(Kin.Blightroot, 0.5f));
        view.Pole.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("seed"), seed);
        view.PoleBase.AddChild(view.Pole);
        // Its pole again over her, where it covers her (over_her.gdshaderinc, as kelp is): it follows the pole.
        view.PoleOver = new MeshInstance3D { Mesh = view.Pole.Mesh, MaterialOverride = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/ink_stalk_over.gdshader"), RenderPriority = 3 }, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        view.Pole.AddChild(view.PoleOver);
        // The head, on top of it: a liquid body (its material its own: its shape and holes).
        view.Holder = new Node3D { Position = new Vector3(0f, SacHeight, 0f) };
        node.AddChild(view.Holder);
        view.Head = new MeshInstance3D { Mesh = _sphere, MaterialOverride = BodyOf(root).Material, CastShadow = GeometryInstance3D.ShadowCastingSetting.On };
        Paint(view.Head, Kin.Skin(Kin.Blightroot, 0.6f), seed);
        view.Head.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("ember_tint"), Kin.Tint(new Color(0.55f, 0.8f, 1f)));
        view.Holder.AddChild(view.Head);
        // Its head again over her, where it covers her (she is drawn over the scene; this is drawn over her, only there,
        // as kelp is: over_her.gdshaderinc). It follows the head.
        var body = BodyOf(root);
        _murkOverShader ??= GD.Load<Shader>("res://assets/shaders/murkling_over.gdshader");
        body.Over = new ShaderMaterial { Shader = _murkOverShader, RenderPriority = 4 };
        view.HeadOver = new MeshInstance3D { Mesh = _sphere, MaterialOverride = body.Over, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        view.Head.AddChild(view.HeadOver);
        // The arms it will have again: hidden until it is freed.
        for (int i = 0; i < Arms; i++)
        {
            view.Angle[i] = i * Mathf.Tau / Arms + 0.2f * Mathf.Sin(seed + i * 2.3f);
            view.Phase[i] = seed + i * 1.37f;
            var pivot = new Node3D { Visible = false };
            view.Holder.AddChild(pivot);
            var arm = new MeshInstance3D { Mesh = ArmMesh(2f + 0.5f * (0.5f + 0.5f * Mathf.Sin(seed * 1.7f + i * 1.9f)), i % 2 == 0 ? 1f : -1f), MaterialOverride = _plainMurk, CastShadow = GeometryInstance3D.ShadowCastingSetting.On };
            Paint(arm, HealthySkin, seed + 10 + i);
            arm.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("hollow"), 0f);
            pivot.AddChild(arm);
            view.Arms[i] = pivot;
        }
        view.Ring = new MeshInstance3D { Mesh = _quad, MaterialOverride = _ring, Visible = false, Position = new Vector3(0f, LevelMap.SwimBand - 0.35f, 0f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        node.AddChild(view.Ring);
        return view;
    }

    static void Paint(MeshInstance3D mesh, Color skin, float seed)
    {
        mesh.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("ember"), 0f);
        mesh.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("skin"), skin);
        mesh.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("seed"), seed);
    }

    void SyncRoots(PlaneWorld world, float dt)
    {
        var her = world.Player.Position;
        for (int i = 0; i < _roots.Count && i < world.Blightroots.Count; i++)
        {
            var view = _roots[i];
            var root = world.Blightroots[i];
            if (!root.Alive)
            {
                Flee(view, root, dt, her);
                continue;
            }
            // The boxing pole: what it leans toward (upright, or its hammer's swing), and how far it is drawn out.
            float starve = Mathf.Clamp(root.Corruption.Fraction / 0.45f, 0f, 1f);
            float breathe = 1f + 0.06f * Mathf.Sin(_time * 2.1f + i) + 0.025f * Mathf.Sin(_time * 5.3f + i * 2f);
            float size = BlightTuning.SacRadius * HeadSize * breathe * (0.75f + 0.25f * starve) * (1f + 0.3f * root.Inflate);
            var want = Vector2.Zero;
            float stiff = PoleStiff, stretchWant = 1f, max = TiltMax;
            bool posed = false;
            if (root.Swing < 0f)
            {
                view.Striking = view.Landed = false;
            }
            else
            {
                var d = new Vector2(root.SwingDir.X, root.SwingDir.Y);
                float lands = BlightTuning.HammerWindup + BlightTuning.HammerStrike;
                if (root.Swing < BlightTuning.HammerWindup)
                {
                    // Rearing back away from her, drawing itself up tall.
                    float k = Mathf.SmoothStep(0f, 1f, root.Swing / BlightTuning.HammerWindup);
                    want = -d * RearTilt * k;
                    stretchWant = Mathf.Lerp(1f, RearStretch, k);
                    max = RearTilt + 0.1f;
                }
                else if (root.Swing < lands + Rest)
                {
                    // The blow: from where it reared to where it lands, ever faster (a hammer falls), its pole drawn out to
                    // reach it: on top of her, or the sand as far as it reaches.
                    if (!view.Striking)
                    {
                        view.Striking = true;
                        view.StrikeFromTilt = view.Tilt;
                        view.StrikeFromStretch = view.Stretch;
                    }
                    var (slamTilt, slamStretch) = SlamPose(view, root, world, size);
                    float u = Mathf.Clamp((root.Swing - BlightTuning.HammerWindup) / BlightTuning.HammerStrike, 0f, 1f);
                    float e = u * u;
                    view.Tilt = view.StrikeFromTilt.Lerp(slamTilt, e);
                    view.Stretch = Mathf.Lerp(view.StrikeFromStretch, slamStretch, e);
                    view.TiltVel = Vector2.Zero;
                    view.StretchVel = 0f;
                    posed = true;
                    if (u >= 1f && !view.Landed)
                    {
                        view.Landed = true;
                        view.SinceLanded = 0f;
                        Slammed(view, root, size);
                    }
                }
                else
                {
                    // Rising off what it struck, slowly through the water, on its spring back to upright.
                    stiff = RecoverStiff;
                    max = SlamTilt + 0.4f;
                }
            }
            view.SinceLanded += dt;
            if (!posed)
            {
                view.TiltVel += ((want - view.Tilt) * stiff - view.TiltVel * PoleDamp) * dt;
                view.Tilt += view.TiltVel * dt;
                view.StretchVel += ((stretchWant - view.Stretch) * stiff - view.StretchVel * PoleDamp) * dt;
                view.Stretch += view.StretchVel * dt;
                // Its foot is held: it leans so far and no further.
                if (view.Tilt.Length() > max)
                {
                    var n = view.Tilt.Normalized();
                    view.Tilt = n * max;
                    float outward = view.TiltVel.Dot(n);
                    if (outward > 0f) view.TiltVel -= n * outward;
                }
            }
            // And the water's slow sway on top (not while it is set on its blow).
            var lean = view.Tilt + new Vector2(
                0.05f * Mathf.Sin(_time * 1.1f + view.Seed) + 0.025f * Mathf.Sin(_time * 2.7f + view.Seed * 2f),
                0.04f * Mathf.Sin(_time * 0.8f + view.Seed * 1.7f) + 0.02f * Mathf.Sin(_time * 2.1f + view.Seed)) * (posed ? 0f : 1f);
            float angle = lean.Length();
            view.PoleBase.Basis = angle > 1e-4f ? new Basis(new Vector3(lean.Y, 0f, -lean.X) / angle, angle) : Basis.Identity;
            float stretch = view.Stretch;
            // Its pulse: stretching up as it gathers, then squashed all the way down to the sand as the ring runs out,
            // springing back up.
            float tall = stretch;
            if (root.PulseRadius >= 0f)
            {
                float tr = root.PulseRadius / BlightTuning.PulseSpeed;
                tall = tr < Down ? Mathf.Lerp(1f + PulseStretch, Flat, Mathf.SmoothStep(0f, 1f, tr / Down))
                    : Flat + (1f - Flat) * (1f - Mathf.Exp(-(tr - Down) * 6f) * Mathf.Cos((tr - Down) * 10f));
            }
            else if (root.Inflate > 0f) tall *= 1f + PulseStretch * Mathf.SmoothStep(0f, 1f, root.Inflate);
            float h = view.Height * Mathf.Max(tall, Flat);
            // Drawn out, it thins (its ink spread along it); squashed, it thickens.
            float thick = PoleRadius * (1f + 0.4f * (1f - Mathf.Min(tall, 1f))) / Mathf.Sqrt(Mathf.Max(tall, 1f));
            view.Pole.Scale = new Vector3(thick, h, thick);
            view.Pole.Position = new Vector3(0f, h * 0.5f, 0f);
            // Flattened as it squashes down; and where it struck, its liquid head flattened on the blow, ringing back.
            float squash = Mathf.Clamp(1f - tall, 0f, 1f);
            float blow = view.SinceLanded < 0.6f ? 0.32f * Mathf.Exp(-9f * view.SinceLanded) * Mathf.Cos(22f * view.SinceLanded) : 0f;
            var top = view.PoleBase.Position + view.PoleBase.Basis * new Vector3(0f, h + size * 0.75f * (1f - 0.4f * squash), 0f);
            view.Holder.Position = top;
            view.Head.Scale = new Vector3(1f + 0.4f * squash + 0.45f * blow, 1f - 0.45f * squash - blow, 1f + 0.4f * squash + 0.45f * blow) * size;

            view.Head.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("swell"), root.Inflate);
            view.Head.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("ember"), 0.2f + 0.8f * root.Inflate);
            view.Pole.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("pulse"), root.Inflate);
            Mirror(view);
            SoftStep(BodyOf(root), System.Numerics.Vector2.Zero, Vector3.Zero, false, false, root.Corruption, view.Seed, dt, 0.08f);
            // Ink running down its pole now and then, sinking into the sand.
            view.Drip += dt * 3f;
            while (view.Drip >= 1f)
            {
                view.Drip -= 1f;
                float a = _droplets.RandRange(0f, Mathf.Tau);
                var from = view.Node.Position + view.Holder.Position + new Vector3(Mathf.Cos(a), -0.6f, Mathf.Sin(a)) * size * 0.8f;
                _droplets.Emit(from, new Vector3(0f, -1.2f, 0f), 0.8f, 0.06f, 0.02f, InkDrop, InkDropFade, drag: 1.5f, buoyancy: -1.4f);
            }
            // Its ring of ink running out over its reach.
            view.Ring.Visible = root.PulseRadius >= 0f;
            if (view.Ring.Visible)
            {
                float r = Mathf.Max(root.PulseRadius, 0.1f);
                view.Ring.Scale = Vector3.One * (r / 0.8f);
                float fadeOut = 1f - Mathf.Clamp(r / BlightTuning.Reach, 0f, 1f);
                view.Ring.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("tint"), new Color(Kin.Blightroot.R * 0.5f, Kin.Blightroot.G * 0.5f, Kin.Blightroot.B * 0.6f, 0.85f * (0.35f + 0.65f * fadeOut)));
                view.Ring.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("width"), 0.09f + 0.4f / r);
                view.Ring.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("seed"), i * 1.7f);
            }
        }
    }

    /// <summary>
    /// Where its blow lands, as the lean and stretch of its pole that put its head there: on top of her (her bell, at
    /// OnHer over her plane) if she is in reach in front of it — as the sim judges it — else on the sand as far as it
    /// reaches her way.
    /// </summary>
    (Vector2 Tilt, float Stretch) SlamPose(Root view, Blightroot root, PlaneWorld world, float size)
    {
        var p = world.Player.Position;
        var to = p - root.Position;
        float her = to.Length();
        bool inFront = her < 1e-3f || System.Numerics.Vector2.Dot(to / her, root.SwingDir) > 0.3f;
        bool on = her <= BlightTuning.HammerHit + world.Radius && inFront;
        Vector3 head;
        if (on) head = new Vector3(to.X, LevelMap.SwimBand + OnHer, to.Y);
        else
        {
            var at = root.SwingDir * Mathf.Min(Mathf.Max(her, 1f), BlightTuning.HammerHit);
            head = new Vector3(at.X, Floor(root.Position + at) + size * 0.7f, at.Y);
        }
        // The pole from its foot to under the head (the head rides size × 0.75 out past its top).
        var d = head - view.PoleBase.Position;
        float len = d.Length();
        if (len < 1e-3f) return (Vector2.Zero, 1f);
        float theta = Mathf.Acos(Mathf.Clamp(d.Y / len, -1f, 1f));
        var flat = new Vector2(d.X, d.Z);
        flat = flat.LengthSquared() > 1e-6f ? flat.Normalized() : new Vector2(root.SwingDir.X, root.SwingDir.Y);
        return (flat * theta, Mathf.Max((len - size * 0.75f) / view.Height, 0.3f));
    }

    /// <summary>
    /// Its head comes down: its liquid flattened on the blow (a dent that rings back), drops of it torn off and flung
    /// out round it (slowed hard by the water, sinking), a few chunks ripped away, and the water shoved out in rings
    /// (the swells' distortion).
    /// </summary>
    void Slammed(Root view, Blightroot root, float size)
    {
        var at = view.Node.Position + view.Holder.Position;
        var down = (Vector3.Down * 0.8f + new Vector3(root.SwingDir.X, 0f, root.SwingDir.Y) * 0.6f).Normalized();
        var b = BodyOf(root);
        for (int i = 0; i < Lobes; i++)
        {
            float k = LobeDirs[i].Dot(down);
            // The face it struck with pushed in; its sides bulging out (its volume keeps the rest).
            b.V[i] -= 9f * Mathf.Max(k, 0f);
            b.V[i] += 3.5f * (1f - Mathf.Abs(k));
        }
        // Drops torn off its rim and flung out round it, low, the water slowing them fast; a few sinking slower.
        int drops = 16 + (int)(_droplets.RandRange(0f, 6f));
        for (int k = 0; k < drops; k++)
        {
            float a = k * Mathf.Tau / drops + _droplets.RandRange(-0.25f, 0.25f);
            var out_ = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            var from = at + out_ * size * 0.8f + Vector3.Down * size * 0.3f;
            var v = out_ * _droplets.RandRange(3f, 6.5f) + Vector3.Up * _droplets.RandRange(0.2f, 1.6f);
            float sz = _droplets.RandRange(0.07f, 0.15f);
            _droplets.Emit(from, v, _droplets.RandRange(0.5f, 0.9f), sz, sz * 0.3f, InkDrop, InkDropFade, drag: 4.2f, buoyancy: -1.2f);
        }
        // Chunks of its mercury ripped away.
        int chunks = 3 + (int)(_droplets.RandRange(0f, 2.99f));
        for (int c = 0; c < chunks; c++)
        {
            float a = _droplets.RandRange(0f, Mathf.Tau);
            var out_ = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            AddChunk(at + out_ * size * 0.7f, out_ * _droplets.RandRange(2f, 3.6f) + Vector3.Up * _droplets.RandRange(0.6f, 1.4f), size * _droplets.RandRange(0.18f, 0.28f), view.Seed + c * 3.1f);
        }
        // The water shoved out: two rings of refraction, a quick wide one and a slower, closer one.
        var g = view.Holder.GlobalPosition;
        Swells?.AddRing(new Vector2(g.X, g.Z), 1.8f, 1.5f);
        Swells?.AddRing(new Vector2(g.X, g.Z), 1.2f, 0.8f);
    }

    /// <summary>Her light struck a Blightroot's head (from InkSplash): torn as a murkling is.</summary>
    void TearRoot(Blightroot root, Vector3 back, float hitShare)
    {
        int i = -1;
        if (_lastWorld is { } w) i = w.Blightroots.IndexOf(root);
        if (i < 0 || i >= _roots.Count) return;
        var view = _roots[i];
        var at = view.Node.Position + view.Holder.Position;
        Tear(BodyOf(root), root.Corruption, back, hitShare, at, BlightTuning.SacRadius * HeadSize, view.Seed);
        // Pushed back, away from the shot, as hard as it struck: a run of hits pushes it further (to TiltMax).
        view.TiltVel += new Vector2(-back.X, -back.Z) * (HitPush + HitPushPerShare * hitShare);
    }

    /// <summary>
    /// Freed: its ink bursts; the pole runs away into drips and the rootlets draw into the sand; the head takes its own
    /// colour, draws out into a mantle and grows its eight arms; then it swims up and away in quick strokes — up off the
    /// seabed toward the light, drifting a little away from her — fading as it goes.
    /// </summary>
    void Flee(Root view, Blightroot root, float dt, System.Numerics.Vector2 her)
    {
        var headAt = view.Node.Position + view.Holder.Position;
        if (!view.Burst)
        {
            view.Burst = true;
            view.Since = 0f;
            Pop(headAt, BlightTuning.SacRadius, Kin.Blightroot);
            view.Ring.Visible = false;
            var away = new Vector3(root.Position.X - her.X, 0f, root.Position.Y - her.Y);
            away = away.LengthSquared() > 1e-4f ? away.Normalized() : new Vector3(0f, 0f, -1f);
            // Mostly up, toward the light; a little away from her.
            view.FleeDir = (away * 0.4f + Vector3.Up).Normalized();
            view.FleeAt = view.Holder.Position;
            view.FleeVelocity = Vector3.Zero;
            // Its holes close at once and it rounds out: it is whole again.
            if (_bodies.TryGetValue(root, out var body))
            {
                body.Holes.Clear();
                body.Material.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("holes"), new Vector4[MaxHoles]);
                body.Material.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("lobes"), new float[Lobes]);
            }
        }
        view.Since += dt;
        float t = view.Since;
        if (t > FleeSeconds)
        {
            view.Node.Visible = false;
            return;
        }
        // The pole runs away from the top down into drips; the rootlets draw back into the sand.
        float melt = Mathf.SmoothStep(0f, 0.9f, t);
        view.Pole.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("melt"), melt);
        view.Pole.Visible = melt < 0.999f;
        if (melt < 1f && _droplets.RandRange(0f, 1f) < 0.8f)
        {
            float hgt = view.Height * (1f - melt) * _droplets.RandRange(0.5f, 1f);
            var p = view.Node.Position + view.PoleBase.Position + view.PoleBase.Basis * new Vector3(0f, hgt, 0f);
            _droplets.Emit(p, new Vector3(_droplets.RandRange(-0.3f, 0.3f), -0.8f, _droplets.RandRange(-0.3f, 0.3f)), 0.6f, 0.07f, 0.02f, InkDrop, InkDropFade, drag: 1.5f, buoyancy: -1.6f);
        }
        float sink = Mathf.SmoothStep(0.1f, 1f, t);
        foreach (var r in view.Rootlets)
        {
            r.Scale = Vector3.One * 0.34f * Mathf.Max(1f - sink, 0.01f);
            r.Visible = sink < 0.999f;
        }
        // Its own colour coming back, its head drawing out into a mantle.
        float heal = Mathf.SmoothStep(0.15f, 1.1f, t);
        var skin = Kin.Skin(Kin.Blightroot, 0.6f).Lerp(HealthySkin, heal);
        view.Head.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("skin"), skin);
        view.Head.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("ember"), 0.2f * (1f - heal));
        view.Head.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("swell"), 0f);
        Mirror(view);
        // Its arms growing out under it.
        float grow = Mathf.SmoothStep(0.35f, ChangeSeconds, t);
        // Then it swims: strokes every StrokeSeconds — a contraction (arms closing back, the mantle squeezed) that drives
        // it on, then a glide as they flare open again.
        const float StrokeSeconds = 0.5f;
        float swim = Mathf.Max(0f, t - ChangeSeconds);
        float phase = swim / StrokeSeconds;
        float squeeze = swim > 0f ? Mathf.Pow(0.5f - 0.5f * Mathf.Cos(Mathf.Tau * phase), 1.5f) : 0f;
        if (swim > 0f)
        {
            view.FleeVelocity += view.FleeDir * 24f * squeeze * dt;
            view.FleeVelocity *= Mathf.Exp(-1.6f * dt);
            view.FleeAt += view.FleeVelocity * dt;
            if (_motes.RandRange(0f, 1f) < 0.25f + 0.6f * squeeze)
                _motes.Emit(view.Node.Position + view.FleeAt - view.FleeDir * 0.5f, new Vector3(_motes.RandRange(-0.4f, 0.4f), -0.2f, _motes.RandRange(-0.4f, 0.4f)) - view.FleeDir * 0.8f,
                    1.2f, 0.07f, 0.13f, new Color(0.85f, 0.95f, 1f, 0.7f), new Color(0.85f, 0.95f, 1f, 0f), drag: 1f, buoyancy: 1.2f);
        }
        view.Holder.Position = view.FleeAt;
        // Swimming, its mantle points up its way (tipped a little toward where it drifts), arms trailing under it.
        float lean = Mathf.SmoothStep(0f, 1f, swim / 0.4f) * 0.4f;
        var flat = new Vector3(view.FleeDir.X, 0f, view.FleeDir.Z);
        var axis = flat.LengthSquared() > 1e-4f ? Vector3.Up.Cross(flat.Normalized()) : Vector3.Right;
        view.Holder.Basis = new Basis(axis.Normalized(), lean);
        float size = BlightTuning.SacRadius * HeadSize;
        float mantleLong = 1f + 0.35f * heal;
        view.Head.Scale = new Vector3(1f - 0.18f * squeeze, mantleLong, 1f - 0.18f * squeeze) * size * (1f - 0.1f * heal);
        view.Head.Position = new Vector3(0f, size * 0.25f * heal, 0f);
        for (int a = 0; a < Arms; a++)
        {
            var pivot = view.Arms[a];
            pivot.Visible = grow > 0.01f;
            if (!pivot.Visible) continue;
            // Hanging as they grow, curling a little; swimming, closing back with each stroke and flaring between.
            float wave = 0.12f * Mathf.Sin(_time * 3.1f + view.Phase[a]) * (1f - squeeze);
            float droop = Mathf.Lerp(1.3f, Mathf.Lerp(0.75f, 1.5f, squeeze), Mathf.SmoothStep(0f, 1f, swim / 0.5f)) + wave;
            pivot.Basis = new Basis(Vector3.Up, -view.Angle[a]) * new Basis(Vector3.Back, -droop);
            pivot.Scale = Vector3.One * 0.5f * grow * (1f + 0.06f * Mathf.Sin(_time * 4f + view.Phase[a]));
            pivot.Position = new Vector3(0f, -size * 0.35f, 0f);
        }
        float fade = 1f - Mathf.Clamp((t - FleeSeconds + 0.8f) / 0.8f, 0f, 1f);
        foreach (var mesh in Meshes(view.Holder)) mesh.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("fade"), fade);
    }

    static IEnumerable<MeshInstance3D> Meshes(Node node)
    {
        if (node is MeshInstance3D m) yield return m;
        foreach (var child in node.GetChildren())
            foreach (var inner in Meshes(child)) yield return inner;
    }
}
