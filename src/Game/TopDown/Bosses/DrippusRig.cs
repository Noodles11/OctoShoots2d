using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Bosses;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown.Bosses;

/// <summary>
/// Drippus Prime (docs/CORRUPTION.md): a murkling prime grown huge, its body the murklings' liquid ink
/// (murkling.gdshader, BlightView.Murklings' soft body: twelve points on springs, its volume kept, ringing when struck)
/// with a prime's magenta ember deep in it. It rises out of the arena's ink; each warning is its attack's own — bulges
/// swelling on its rim for a brood (each snaps off as a murkling), pulling in round and trembling for a ring, the
/// slingshot bend for a fan, its top swelling for the bombs, ripples running round it for the spiral — and the side
/// facing her dimples before it bounces her off, then springs out past round and rings. Her light tears it where it
/// strikes (the sim's tears); every birth shrinks it (its area follows its mass) with a slosh. Freed, it is gone at
/// once: it bursts (BlightView's pop) and the fish it took swim out.
/// </summary>
public partial class DrippusRig : BossRig
{
    const int Lobes = BlightView.Lobes;
    /// <summary>Its surface tension, damping (low: it rings), its neighbours' pull, how far a lobe may stand out.</summary>
    const float Tension = 120f, Damping = 4f, Coupling = 35f, LobeMax = 0.3f;
    /// <summary>It is a little flattened (a drop adrift, not a ball).</summary>
    const float Flat = 0.85f;
    /// <summary>The holes her light tears (radians: least, and per √strength).</summary>
    const float HoleLeast = 0.1f, HoleWide = 0.3f;
    const int MaxHoles = 6, MaxBuds = 4;

    Node3D _holder = null!;
    MeshInstance3D _body = null!;
    OmniLight3D _glow = null!;
    readonly List<MeshInstance3D> _buds = new();
    readonly float[] _r = new float[Lobes], _v = new float[Lobes];
    float _size = 1f, _snap = 10f, _lastSprang = 10f, _seed;
    int _lastMass = -1;
    readonly HashSet<BossTear> _felt = new();

    public override Node3D CrustParent => _holder;

    protected override ShaderMaterial MakeSkin() => new() { Shader = GD.Load<Shader>("res://assets/shaders/murkling.gdshader") };

    public override Vector3 Surface(float angle, float out_)
    {
        float r = Mathf.Clamp(out_, 0f, 1f);
        return new Vector3(Mathf.Cos(angle) * r, Mathf.Sqrt(Mathf.Max(0f, 1f - r * r)) * Flat, Mathf.Sin(angle) * r) * R;
    }

    protected override void BuildBody()
    {
        _seed = (Spec.Seed % 1000UL) * 0.1f;
        _holder = new Node3D { Scale = new Vector3(R, R * Flat, R) };
        AddChild(_holder);
        _body = new MeshInstance3D { Mesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 64, Rings = 32 }, MaterialOverride = Skin, CastShadow = GeometryInstance3D.ShadowCastingSetting.On };
        _holder.AddChild(_body);
        _body.SetInstanceShaderParameter(SN.Of("ember_tint"), BlightView.Kin.Tint(BlightView.Kin.PrimeCore));
        _body.SetInstanceShaderParameter(SN.Of("seed"), _seed);
        _body.SetInstanceShaderParameter(SN.Of("ember"), 0.2f);
        // The brood's bulges: small drops of its ink swelling out of its rim (round: no lobes of their own).
        var bud = new ShaderMaterial { Shader = Skin.Shader };
        for (int i = 0; i < MaxBuds; i++)
        {
            var b = new MeshInstance3D { Mesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 24, Rings = 12 }, MaterialOverride = bud, Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.On };
            b.SetInstanceShaderParameter(SN.Of("ember_tint"), BlightView.Kin.Tint(BlightView.Kin.PrimeCore));
            b.SetInstanceShaderParameter(SN.Of("seed"), _seed + i * 7f);
            AddChild(b);
            _buds.Add(b);
        }
        // Its ember's glow on the reef round it (as each creature of ink casts a little light in its core's colour).
        _glow = new OmniLight3D { LightColor = BlightView.Kin.PrimeCore, OmniRange = R * 3f, ShadowEnabled = false };
        AddChild(_glow);
        PearlHeight = 0f;
    }

    /// <summary>Its drip wells up from these points round its top (no mutation is drawn: it is all ink).</summary>
    protected override Node3D? Mutation(MutationTheme theme, BodySite site)
    {
        float r = Mathf.Clamp(site.Out, 0f, 1f);
        var marker = new Node3D { Position = new Vector3(Mathf.Cos(site.Angle) * r, Mathf.Sqrt(1f - r * r), Mathf.Sin(site.Angle) * r) };
        _holder.AddChild(marker);
        return marker;
    }

    /// <summary>A hit flashes its outline as her light's does on a murkling; leaving, it fades (it is gone at once).</summary>
    public override void SetSkin(float drained, float flash, float surge, float fade)
    {
        _body.SetInstanceShaderParameter(SN.Of("hit"), flash);
        _body.SetInstanceShaderParameter(SN.Of("fade"), fade);
    }

    /// <summary>A direction on the map (world x, z) in its own frame.</summary>
    Vector3 Local(System.Numerics.Vector2 d) => (GlobalBasis.Inverse() * new Vector3(d.X, 0f, d.Y)).Normalized();

    public override void Pose(in RigPose pose)
    {
        var boss = pose.Boss;
        float dt = Mathf.Min(pose.Dt, 1f / 30f);
        if (boss is null) return;
        // Freed, it is gone at once: it bursts (BlightView pops it) and leaves the fish it took.
        bool shown = pose.Stage != BossStage.Freed;
        _holder.Visible = shown;
        _glow.Visible = shown;
        foreach (var b in _buds) b.Visible = false;
        if (!shown) return;

        // Rising out of the ink: nothing while the ink streams in, then it swells up to its size.
        float grow = 1f;
        if (pose.Stage == BossStage.Drop)
        {
            float k = Mathf.Clamp(boss.StageTime / DrippusTuning.Rise, 0f, 1f);
            grow = Mathf.SmoothStep(0f, 1f, Mathf.Clamp((k - DrippusTuning.RiseSolid) / (1f - DrippusTuning.RiseSolid), 0f, 1f));
        }
        else if (pose.Stage == BossStage.Waiting || pose.Stage == BossStage.Banner) grow = 0f;

        // Each birth shrinks it: it eases to its new size with a slosh, recoiling from where the piece left it.
        if (_lastMass < 0) _lastMass = boss.Mass;
        if (boss.Mass < _lastMass)
        {
            for (int i = 0; i < Lobes; i++) _v[i] += (Mathf.Sin(i * 2.3f + pose.Time * 7f)) * 2.2f;
            _lastMass = boss.Mass;
        }
        _size = Mathf.MoveToward(_size, boss.Size, dt * (1f - boss.Size + 0.1f) / 0.4f);

        // The warning, by its attack.
        float warn = pose.Act == BossAct.Telegraph ? pose.Warn : 0f;
        var next = boss.Next;
        float squeeze = 1f, tremble = 0f;
        var strain = new float[Lobes];
        if (warn > 0f)
            switch (next)
            {
                case BossAttack.Brood:
                    squeeze = 1f - 0.06f * warn;
                    break;
                case BossAttack.Ring:
                    squeeze = 1f - 0.1f * warn * warn;
                    tremble = 0.06f * warn;
                    break;
                case BossAttack.ArcBombs:
                    for (int i = 0; i < Lobes; i++) strain[i] += Mathf.Max(0f, BlightView.LobeDirs[i].Y) * 0.25f * warn;
                    break;
            }
        // The spiral: ripples run round it through its warning and its spin.
        if (next == BossAttack.Helix && (warn > 0f || pose.Act == BossAct.Helix))
            for (int i = 0; i < Lobes; i++)
            {
                var d = BlightView.LobeDirs[i];
                strain[i] += 0.12f * Mathf.Sin(Mathf.Atan2(d.Z, d.X) * 3f - pose.Time * 9f);
            }
        // The bounce: the side facing her dimples in, then springs out past round.
        var toward = Local(boss.BounceDir);
        if (boss.Bounce >= 0f)
        {
            float k = 1f - boss.Bounce / DrippusTuning.Tell;
            for (int i = 0; i < Lobes; i++) strain[i] -= Mathf.Pow(Mathf.Max(0f, BlightView.LobeDirs[i].Dot(toward)), 2f) * 0.28f * k;
        }
        if (boss.Sprang < _lastSprang)
            for (int i = 0; i < Lobes; i++) _v[i] += Mathf.Pow(Mathf.Max(0f, BlightView.LobeDirs[i].Dot(toward)), 2f) * 7f - 0.8f;
        _lastSprang = boss.Sprang;
        // Her light: a dent where each new tear opened, ringing back.
        foreach (var t in boss.Tears)
        {
            if (!_felt.Add(t)) continue;
            var dir = Local(new System.Numerics.Vector2(Mathf.Cos(t.Angle), Mathf.Sin(t.Angle)));
            for (int i = 0; i < Lobes; i++) _v[i] -= Mathf.Pow(Mathf.Max(0f, BlightView.LobeDirs[i].Dot(dir)), 2f) * (2f + 3f * t.Strength);
        }
        _felt.RemoveWhere(t => !boss.Tears.Contains(t));
        // At the bombs' heave the top is flung up; at the ring's release it springs out round.
        if (pose.Fired && next == BossAttack.ArcBombs)
            for (int i = 0; i < Lobes; i++) _v[i] += Mathf.Max(0f, BlightView.LobeDirs[i].Y) * 4f;
        if (pose.Fired && next == BossAttack.Ring)
            for (int i = 0; i < Lobes; i++) _v[i] += 1.5f;
        SoftStep(strain, pose.Time, dt);

        // The fan: the slingshot bend back away from its aim, the aim following her; the snap forward, the piece swelling
        // out of its front, then a wobble back to round (as a murkling throws).
        var aim = Local(boss.Aim);
        if (pose.Fired && next == BossAttack.Fan) _snap = 0f;
        _snap += dt;
        float pull = 0f, piece = 0f;
        if (warn > 0f && next == BossAttack.Fan)
        {
            pull = -(1f - (1f - warn) * (1f - warn)) * 0.6f;
            piece = 0.2f * warn;
        }
        else if (_snap < 0.7f)
        {
            pull = Mathf.Exp(-7f * _snap) * Mathf.Cos(24f * _snap) * 0.6f;
            piece = 0.5f * (1f - Mathf.SmoothStep(0f, 0.1f, _snap));
        }
        _body.SetInstanceShaderParameter(SN.Of("aim"), aim);
        _body.SetInstanceShaderParameter(SN.Of("pull"), pull);
        _body.SetInstanceShaderParameter(SN.Of("bud"), piece);

        // Its size: its mass's (eased), drawn in on a warning, a slow breath, trembling on the ring's warning.
        float breathe = 1f + 0.02f * Mathf.Sin(pose.Time * 1.3f + _seed);
        float s = R * _size * squeeze * breathe * grow;
        _holder.Scale = new Vector3(s, s * Flat, s);
        _holder.Position = tremble > 0f ? new Vector3(Mathf.Sin(pose.Time * 63f), 0f, Mathf.Cos(pose.Time * 51f)) * tremble * R : Vector3.Zero;
        // Its ember deep in it: a dim glow (huge, a bright one would fill it), stirring as it warns.
        _body.SetInstanceShaderParameter(SN.Of("ember"), 0.16f + 0.2f * warn + 0.04f * Mathf.Sin(pose.Time * 2.1f));
        _glow.LightEnergy = (0.8f + 0.6f * warn) * Mathf.Max(BlightView.Lantern, 1f) * grow;
        _glow.OmniRange = R * _size * 3f;

        // The brood's bulges: one swelling out of its rim where each murkling will leave it, drawn out on a neck at the end.
        if (warn > 0f && next == BossAttack.Brood)
            for (int i = 0; i < boss.BudDirs.Count && i < _buds.Count; i++)
            {
                var b = _buds[i];
                var d = Local(boss.BudDirs[i]);
                float size = BlightTuning.MurkRadius * 1.4f * Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, warn * 1.5f));
                float neck = Mathf.SmoothStep(0.75f, 1f, warn);
                b.Visible = true;
                // At her plane (the seat is a little under it), standing out of its rim.
                b.Position = d * (s * 0.92f + size * (0.4f + 0.9f * neck)) + Vector3.Up * 0.5f;
                b.Basis = Basis.LookingAt(d, Vector3.Up).Scaled(new Vector3(size, size, size * (1f + 0.5f * neck)));
                b.SetInstanceShaderParameter(SN.Of("ember"), 0.5f + 0.5f * warn);
            }

        // Her light's tears: holes in it where it struck, closing as they age.
        var holes = new Vector4[MaxHoles];
        for (int i = 0; i < boss.Tears.Count && i < MaxHoles; i++)
        {
            var t = boss.Tears[i];
            var d = Local(new System.Numerics.Vector2(Mathf.Cos(t.Angle), Mathf.Sin(t.Angle)));
            var dir = (d + Vector3.Up * 0.35f).Normalized();
            float open = Mathf.Sqrt(Mathf.Clamp(1f - t.Age / DrippusTuning.TearClose, 0f, 1f));
            holes[i] = new Vector4(dir.X, dir.Y, dir.Z, (HoleLeast + HoleWide * Mathf.Sqrt(t.Strength)) * open);
        }
        Skin.SetShaderParameter(SN.Of("holes"), holes);
    }

    /// <summary>One frame of its soft body (as BlightView's SoftStep: springs, neighbours, its volume kept), with a strain on each lobe.</summary>
    void SoftStep(float[] strain, float time, float dt)
    {
        const int Steps = 3;
        float h = dt / Steps;
        for (int step = 0; step < Steps; step++)
        {
            for (int i = 0; i < Lobes; i++)
            {
                float near = 0f;
                foreach (int j in BlightView.LobeNear[i]) near += _r[j];
                near /= BlightView.LobeNear[i].Length;
                float stir = Mathf.Sin(time * (1.1f + 0.23f * i) + _seed * 3f + i * 1.7f) * 0.08f;
                float f = -Tension * (_r[i] - strain[i] - stir) - Damping * _v[i] + Coupling * (near - _r[i]);
                _v[i] += f * h;
            }
            float mean = 0f;
            for (int i = 0; i < Lobes; i++)
            {
                _r[i] = Mathf.Clamp(_r[i] + _v[i] * h, -LobeMax, LobeMax);
                mean += _r[i];
            }
            mean /= Lobes;
            for (int i = 0; i < Lobes; i++) _r[i] -= mean;
        }
        Skin.SetShaderParameter(SN.Of("lobes"), _r);
    }
}
