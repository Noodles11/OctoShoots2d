using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Clementine (THEME-BIBLE §5.1, DESIGN-TOPDOWN §2.1), presentation only: the sim's position, nothing more.
/// A translucent tangerine bell (clementine_bell.gdshader) with sixteen thin marginal tentacles and eight ruffled oral
/// arms on verlet chains (clementine_trail.gdshader). Every stroke is a contraction beat — quick squeeze, slow relax,
/// a little recoil — paced by her speed; a jet or dash starts one at once. The bell leans into the swim on a soft
/// spring, the strands trail behind and swing through turns, and when she stops they float out around her while she
/// bobs. A hit flushes her magenta and whips the tentacles out; the ink dash turns her into a dark ghost.
/// Ink clouds left by dashes are drawn here too.
/// </summary>
public partial class BellView : Node3D
{
    const float BellRadius = 0.42f, BellHeight = 0.3f;
    const int Tentacles = 16, OralArms = 8;
    /// <summary>The strands' fixed step: steady motion at any frame rate.</summary>
    const float Step = 1f / 120f;
    static readonly Color Glow = new(1f, 0.72f, 0.52f);

    sealed class Strand
    {
        public Vector3[] Pos = null!, Prev = null!;
        public float Angle, Length, RootWidth, TipWidth, Stiffness;
        public bool Arm;
        /// <summary>Which way the strand floats when nothing moves it, in the bell's frame.</summary>
        public Vector3 Rest;
        public Vector3 Anchor, LastAnchor;
        public float Seg => Length / (Pos.Length - 1);
    }

    Node3D _body = null!;
    ShaderMaterial _bellMaterial = null!, _strandMaterial = null!;
    ImmediateMesh _strandMesh = null!;
    OmniLight3D _light = null!;
    StandardMaterial3D _inkMaterial = null!;
    readonly List<MeshInstance3D> _ink = new();
    readonly List<Strand> _strands = new();
    bool _strandsReady;
    Vector3 _lastCentre;
    float _accum, _strandTime;

    /// <summary>1 at rest, low at speed: how much the strands hold their floating shape rather than stream behind.</summary>
    float _calm = 1f;
    /// <summary>Across her course (horizontal): the way trailing strands ripple.</summary>
    Vector3 _across = Vector3.Right;
    float _time, _phase = 0.5f, _amp = 0.3f, _pulse, _hurt, _inkGhost, _idle = 1f, _spin;
    float _lastJet, _lastDash, _lastHurt;
    Vector2 _lean, _leanVel;

    /// <summary>Bubble Shield: a big iridescent bubble round her (how far it has grown in, and a ripple when it turns a hit).</summary>
    MeshInstance3D _shield = null!;
    float _shieldShown, _ripple;
    /// <summary>
    /// Her vitals on the bell (DESIGN-TOPDOWN §2.5): health as shown by the gonad rings (eased, so a lost quarter
    /// gutters out rather than blinking off), and the rim's snuff after the active pearl is used (1 → 0).
    /// </summary>
    float _health = 1f, _snuff, _lastCharge = 1f;
    const float SnuffSeconds = 0.35f;

    /// <summary>Pearl Diver: the pearl swelling in front of her while she charges it.</summary>
    MeshInstance3D _chargeOrb = null!;
    ShaderMaterial _chargeMaterial = null!;
    /// <summary>
    /// Light bubbles: the light she has to spend, 0..1, presentation only. Each volley spends a little (the bell dims to
    /// 0.85 when it is gone, never dark) and it rekindles while she holds her fire.
    /// </summary>
    float _reserve = 1f, _breath = 1f;
    const float SpendPerBubble = 0.07f, Rekindle = 0.45f;

    public override void _Ready()
    {
        _body = new Node3D();
        AddChild(_body);
        _bellMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/clementine_bell.gdshader"), RenderPriority = 2 };
        _body.AddChild(new MeshInstance3D { Mesh = BakeBell(), MaterialOverride = _bellMaterial, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });

        _strandMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/clementine_trail.gdshader"), RenderPriority = 1 };
        _strandMesh = new ImmediateMesh();
        // The strands live in world space: they trail where she has been, not where she is.
        AddChild(new MeshInstance3D { Mesh = _strandMesh, TopLevel = true, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 8f });
        for (int k = 0; k < OralArms; k++)
        {
            float a = k * Mathf.Tau / OralArms + 0.2f;
            _strands.Add(new Strand
            {
                Arm = true,
                Angle = a,
                Length = 1.05f + 0.3f * Hash(k + 40),
                RootWidth = 0.13f,
                TipWidth = 0.05f,
                Stiffness = 4f,
                Rest = new Vector3(Mathf.Cos(a) * 0.6f, -1f, Mathf.Sin(a) * 0.6f).Normalized(),
                Pos = new Vector3[10],
                Prev = new Vector3[10],
            });
        }
        for (int k = 0; k < Tentacles; k++)
        {
            // One in each notch of the scalloped margin.
            float a = Mathf.Pi / 16f + k * Mathf.Tau / Tentacles;
            _strands.Add(new Strand
            {
                Angle = a,
                Length = 1.35f + 0.6f * Hash(k),
                RootWidth = 0.075f,
                TipWidth = 0.028f,
                Stiffness = 2.2f,
                Rest = new Vector3(Mathf.Cos(a), -0.45f, Mathf.Sin(a)).Normalized(),
                Pos = new Vector3[16],
                Prev = new Vector3[16],
            });
        }

        // Her glow is the lantern of the scene: a warm pool in the dark water around her.
        _light = new OmniLight3D { LightColor = Glow, LightEnergy = 2.6f, OmniRange = 11f, OmniAttenuation = 1.4f, ShadowEnabled = false, Position = new Vector3(0f, 1.2f, 0f) };
        AddChild(_light);
        var sphere = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 32, Rings = 16 };
        var shieldMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/plane_bubble.gdshader"), RenderPriority = 3 };
        shieldMaterial.SetShaderParameter("tint", new Color(0.75f, 0.92f, 1f));
        _shield = new MeshInstance3D { Mesh = sphere, MaterialOverride = shieldMaterial, Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_shield);
        // Pearl Diver's charge: the light she gathers to throw, the same lantern as her bubbles (one substance).
        _chargeMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/plane_bubble.gdshader"), RenderPriority = 3 };
        _chargeOrb = new MeshInstance3D { Mesh = sphere, MaterialOverride = _chargeMaterial, Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_chargeOrb);
        _inkMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.06f, 0.04f, 0.12f, 0.55f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
    }

    /// <summary>The dive (DESIGN-TOPDOWN §4.6): how far below her swim plane she has sunk, and how far she has turned
    /// apex-down (0 upright, 1 head-first).</summary>
    public float DiveDepth { get; set; }
    public float DiveTurn { get; set; }

    /// <summary>A pearl's lustre: creamy white, a pink-gold sheen at the rim, glowing a little (Pearl Diver's throws).</summary>
    public static StandardMaterial3D PearlMaterial() => new()
    {
        AlbedoColor = new Color(1f, 0.97f, 0.92f),
        Roughness = 0.12f,
        Metallic = 0.35f,
        RimEnabled = true,
        Rim = 1f,
        RimTint = 0.6f,
        EmissionEnabled = true,
        Emission = new Color(1f, 0.85f, 0.9f),
        EmissionEnergyMultiplier = 0.8f,
    };

    /// <summary>The shield turned a hit away: it wobbles.</summary>
    public void ShieldRipple() => _ripple = 1f;

    /// <summary>She threw a volley of light bubbles (how many): her bell gives up a little of its light.</summary>
    public void Spend(float bubbles) => _reserve = Mathf.Max(0f, _reserve - SpendPerBubble * Mathf.Max(bubbles, 1f));

    /// <summary>A hard stroke now (the dive's first contraction).</summary>
    public void Kick()
    {
        _phase = 0f;
        _amp = 1f;
    }

    /// <summary>
    /// Moves her with the world (the dive moves the new level back to the origin): the bell and her trailing strands,
    /// redrawn at once, so the frame that moves the world shows her where she is (no frame drawn at the old place).
    /// </summary>
    public void Shift(Vector3 by)
    {
        Position += by;
        RenderingServer.GlobalShaderParameterSet("her_glow", new Vector4(GlobalPosition.X, GlobalPosition.Y, GlobalPosition.Z, _glowRadius));
        foreach (var s in _strands)
        {
            for (int i = 0; i < s.Pos.Length; i++)
            {
                s.Pos[i] += by;
                s.Prev[i] += by;
            }
            s.Anchor += by;
            s.LastAnchor += by;
        }
        _lastCentre += by;
        var camera = GetViewport().GetCamera3D();
        if (camera != null) DrawStrands(camera.GlobalPosition);
    }

    /// <summary>The player she was last drawn for: a new level brings a new one, and its timers start afresh.</summary>
    PlaneBody? _drawnFor;

    public void Sync(PlaneWorld world, float alpha, float dt)
    {
        _time += dt;
        float h = Mathf.Min(dt, 0.05f);
        var p = world.Player;
        // A new level's body: its jet, dash and hurt timers start from zero, which must not read as a stroke or a hit.
        if (!ReferenceEquals(p, _drawnFor))
        {
            _drawnFor = p;
            _lastJet = p.JetTimer;
            _lastDash = p.DashTimer;
            _lastHurt = p.HurtTimer;
        }
        var at = System.Numerics.Vector2.Lerp(p.PrevPosition, p.Position, alpha);
        var vel = new Vector2(p.Velocity.X, p.Velocity.Y);
        float speed = vel.Length();
        float pace = Mathf.Clamp(speed / Mathf.Max(world.CruiseSpeed, 0.1f), 0f, 1.6f);

        // The beat. A jet or a dash starts a contraction at once (input overrides pulse timing); otherwise the pulse
        // runs slow and shallow at rest (breathing) and quicker and deeper with speed.
        bool jet = p.JetTimer > _lastJet + 0.05f, dash = p.DashTimer > _lastDash + 0.05f, hit = p.HurtTimer > _lastHurt + 0.05f;
        _lastJet = p.JetTimer;
        _lastDash = p.DashTimer;
        _lastHurt = p.HurtTimer;
        if ((jet || dash) && _phase > 0.3f) _phase = 0f;
        bool burst = p.JetTimer > 0f || p.IsDashing || DiveDepth > 0f;
        float ampTarget = burst ? 1f : 0.3f + 0.55f * Mathf.Min(pace, 1f);
        _amp = Mathf.Lerp(_amp, ampTarget, 1f - Mathf.Exp(-4f * h));
        _phase = Mathf.PosMod(_phase + h * (burst ? 1.8f : 0.42f + 0.95f * Mathf.Min(pace, 1.3f)), 1f);
        float before = _pulse;
        _pulse = Mathf.Lerp(_pulse, Contraction(_phase) * _amp, 1f - Mathf.Exp(-25f * h));
        float pulseRate = h > 0f ? (_pulse - before) / h : 0f;
        _hurt = hit ? 1f : Mathf.MoveToward(_hurt, 0f, h * 2.2f);
        _inkGhost = Mathf.MoveToward(_inkGhost, p.DashInvulnerableTimer > 0f ? 1f : 0f, h * 10f);

        // The lean: the apex leads into the swim on a critically damped spring; at rest she tips a little toward where
        // she aims (curiosity).
        Vector2 leanTarget = speed > 0.05f ? vel / speed * Mathf.Min(pace, 1.5f) * 0.42f : new Vector2(p.Aim.X, p.Aim.Y) * 0.07f;
        const float w = 6f;
        _leanVel += ((leanTarget - _lean) * w * w - _leanVel * 2f * w) * h;
        _lean += _leanVel * h;

        // Idle drift: once she settles she bobs gently (~1 m) on the swim plane; each stroke lifts her a touch.
        _idle = Mathf.MoveToward(_idle, speed < 0.6f ? 1f : 0f, h * 1.2f);
        float ease = _idle * _idle * (3f - 2f * _idle);
        float bob = 0.5f * Mathf.Sin(_time * 1.1f) * ease + 0.06f * _pulse;
        Position = new Vector3(at.X, LevelMap.SwimBand + bob * (1f - DiveTurn) - DiveDepth, at.Y);
        _spin += h * 0.08f;
        float lean = _lean.Length();
        var tilt = lean > 1e-4f ? new Basis(new Vector3(_lean.Y, 0f, -_lean.X) / lean, lean) : Basis.Identity;
        _body.Basis = tilt * new Basis(Vector3.Up, _spin);
        // Diving, she turns head-first (apex down), leaning south so the camera sees her turn.
        if (DiveTurn > 0f) _body.Basis = new Basis(Vector3.Right, -DiveTurn * Mathf.Pi * 0.85f) * _body.Basis;

        _bellMaterial.SetShaderParameter("pulse", _pulse);
        _bellMaterial.SetShaderParameter("wave", _phase < 0.55f ? _phase / 0.55f : 1.3f);
        _bellMaterial.SetShaderParameter("hurt", _hurt);
        _bellMaterial.SetShaderParameter("ink", _inkGhost);
        _reserve = Mathf.Min(1f, _reserve + Rekindle * h);
        // Clinging murklings drink her glow (each a little), and a coiling gloomvine makes it flicker; never dark.
        float drunk = 1f - 0.1f * world.Clingers - (world.Player.RootTimer > 0f ? 0.08f * (0.5f + 0.5f * Mathf.Sin(_time * 30f)) : 0f);
        _breath = Mathf.Lerp(_breath, (0.85f + 0.15f * _reserve) * Mathf.Max(drunk, 0.65f), 1f - Mathf.Exp(-8f * h));
        _bellMaterial.SetShaderParameter("breath", _breath);
        SyncVitals(world, h);
        _strandMaterial.SetShaderParameter("hurt", _hurt);
        _strandMaterial.SetShaderParameter("ink", _inkGhost);
        // Lantern Pearl: her glow reaches farther and shines brighter.
        float glow = world.Run.Loadout.Stats[OctoShoots.Core.Items.Stat.Glow];
        _light.OmniRange = 11f * glow;
        _light.LightEnergy = 2.6f * (1f + 0.4f * (glow - 1f)) * (0.94f + 0.12f * _pulse) * (0.7f + 0.3f * _breath);
        _light.LightColor = Glow.Lerp(new Color(1f, 0.3f, 0.55f), _hurt * 0.5f);

        if (speed > 0.3f) _across = new Vector3(-vel.Y, 0f, vel.X) / speed;
        _calm = Mathf.Lerp(_calm, 1f - 0.85f * Mathf.Min(pace, 1f), 1f - Mathf.Exp(-3f * h));
        StepStrands(h, pulseRate, hit);
        var camera = GetViewport().GetCamera3D();
        if (camera != null) DrawStrands(camera.GlobalPosition);
        SyncInk(world);
        SyncShield(p, h);
        SyncCharge(p);
        SyncGlowPool(world);
    }

    /// <summary>
    /// Her glow on the reef beneath her (reef_surface.gdshaderinc, rs_her_glow): where the bell is, how wide the warm pool
    /// is (3 m, wider with Lantern Pearl), and how strong; the pool fades as she turns head-down into a dive.
    /// </summary>
    void SyncGlowPool(PlaneWorld world)
    {
        float glow = world.Run.Loadout.Stats[OctoShoots.Core.Items.Stat.Glow];
        var at = GlobalPosition;
        _glowRadius = 3f * Mathf.Sqrt(Mathf.Max(glow, 0.1f));
        RenderingServer.GlobalShaderParameterSet("her_glow", new Vector4(at.X, at.Y, at.Z, _glowRadius));
        RenderingServer.GlobalShaderParameterSet("her_glow_strength", (1f - DiveTurn) * (0.9f + 0.1f * _pulse));
    }

    float _glowRadius = 3f;

    public override void _ExitTree() => RenderingServer.GlobalShaderParameterSet("her_glow_strength", 0f);

    /// <summary>
    /// Health into the gonad rings (draining at most 0.8 of her max a second, healing back faster) and the active pearl's
    /// charge into the rim; when the charge drops from ready, the rim snuffs out.
    /// </summary>
    void SyncVitals(PlaneWorld world, float h)
    {
        float target = world.Run.MaxHp > 0f ? Mathf.Clamp(world.Player.Hp / world.Run.MaxHp, 0f, 1f) : 0f;
        _health = Mathf.MoveToward(_health, target, h * (target < _health ? 0.8f : 1.5f));
        bool holds = world.Run.Active?.Active is not null;
        float charge = world.Run.ActiveCharge;
        if (holds && _lastCharge >= 1f && charge < 1f) _snuff = 1f;
        _lastCharge = holds ? charge : 1f;
        _snuff = Mathf.MoveToward(_snuff, 0f, h / SnuffSeconds);
        _bellMaterial.SetShaderParameter("health", _health);
        _bellMaterial.SetShaderParameter("charge", holds ? charge : -1f);
        _bellMaterial.SetShaderParameter("snuff", _snuff);
        _bellMaterial.SetShaderParameter("spin", _spin);
    }

    /// <summary>Bubble Shield: it swells round her, shimmers, wobbles when it turns a hit, and flickers before it goes.</summary>
    void SyncShield(PlaneBody p, float h)
    {
        _shieldShown = Mathf.MoveToward(_shieldShown, p.ShieldTimer > 0f ? 1f : 0f, h * 7f);
        _ripple = Mathf.MoveToward(_ripple, 0f, h * 4f);
        _shield.Visible = _shieldShown > 0.01f;
        if (!_shield.Visible) return;
        float flicker = p.ShieldTimer is > 0f and < 0.7f ? 0.55f + 0.45f * Mathf.Sin(_time * 38f) : 1f;
        float wobble = 0.1f * _ripple * Mathf.Sin(_time * 30f);
        float r = 1.05f * (0.55f + 0.45f * _shieldShown);
        _shield.Scale = new Vector3(r * (1f + wobble), r * (1f - wobble), r * (1f + wobble)) * (1f + 0.025f * Mathf.Sin(_time * 4f));
        _shield.SetInstanceShaderParameter("fade", _shieldShown * flicker);
        _shield.SetInstanceShaderParameter("rainbow", 0.35f + 0.4f * _ripple);
    }

    /// <summary>Pearl Diver: the pearl grows in front of her as she charges it, and shimmers once it is full.</summary>
    void SyncCharge(PlaneBody p)
    {
        _chargeOrb.Visible = p.Charge > 0f;
        if (!_chargeOrb.Visible) return;
        float k = Mathf.Clamp(p.Charge / PlaneCombatTuning.ChargeSeconds, 0f, 1f);
        bool full = k >= 1f;
        float size = PlaneCombatTuning.ShotRadius * (1f + (PlaneCombatTuning.ChargeSize - 1f) * k) * 1.3f * (full ? 1f + 0.07f * Mathf.Sin(_time * 14f) : 1f);
        var aim = new Vector3(p.Aim.X, 0f, p.Aim.Y);
        _chargeOrb.Position = aim * (BellRadius + 0.25f + size) + Vector3.Down * 0.1f;
        _chargeOrb.Scale = Vector3.One * size;
        _chargeOrb.SetInstanceShaderParameter("lantern", 0.3f + 0.6f * k * k + (full ? 0.15f * Mathf.Sin(_time * 14f) : 0f));
        _chargeOrb.SetInstanceShaderParameter("warmth", full ? 0.6f : 0.3f * k);
        _chargeOrb.SetInstanceShaderParameter("seed", 1.7f);
        _chargeOrb.SetInstanceShaderParameter("wobble", 0.3f);
    }

    /// <summary>The contraction over one beat: a quick squeeze, then a slow relax that overshoots into a slight flare.</summary>
    static float Contraction(float phase)
    {
        if (phase < 0.3f)
        {
            float k = phase / 0.3f;
            return k * k * (3f - 2f * k);
        }
        float r = (phase - 0.3f) / 0.7f;
        return Mathf.Exp(-3.5f * r) * Mathf.Cos(1.25f * Mathf.Pi * r);
    }

    static float Hash(int k) => Mathf.PosMod(Mathf.Sin(k * 78.233f + 1.7f) * 43758.547f, 1f);

    // ───────────────────────── the bell ─────────────────────────

    /// <summary>How far the margin reaches at an angle: sixteen lappets with notches between.</summary>
    static float Scallop(float a, float u) => 1f + 0.05f * (Mathf.Pow(Mathf.Abs(Mathf.Cos(8f * a)), 0.6f) - 0.6f) * Mathf.Pow(u, 6f);

    /// <summary>A point of the resting bell: u from the apex (0) to the margin (1), which drops vertically like a skirt.</summary>
    static Vector3 BellPoint(float u, float a)
    {
        float rho = BellRadius * (1f - Mathf.Pow(1f - u, 1.6f)) * Scallop(a, u);
        float y = BellHeight * Mathf.Pow(Mathf.Max(Mathf.Cos(u * Mathf.Pi / 2f), 0f), 0.8f);
        return new Vector3(rho * Mathf.Cos(a), y, rho * Mathf.Sin(a));
    }

    /// <summary>The margin at an angle under a contraction, matching the bell shader's vertex().</summary>
    static Vector3 MarginAt(float a, float pulse)
    {
        float rho = BellRadius * Scallop(a, 1f) * (1f - 0.25f * pulse);
        return new Vector3(rho * Mathf.Cos(a), -0.04f * pulse, rho * Mathf.Sin(a));
    }

    static ArrayMesh BakeBell()
    {
        const int rings = 26, segments = 112;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i <= rings; i++)
        for (int j = 0; j <= segments; j++)
        {
            float u = (float)i / rings, a = (float)j / segments * Mathf.Tau;
            Vector3 point = BellPoint(u, a);
            float us = Mathf.Max(u, 0.02f);
            Vector3 du = BellPoint(Mathf.Min(us + 0.01f, 1f), a) - BellPoint(us - 0.01f, a);
            Vector3 da = BellPoint(us, a + 0.01f) - BellPoint(us, a - 0.01f);
            st.SetNormal(i == 0 ? Vector3.Up : da.Cross(du).Normalized());
            st.SetUV(new Vector2(u, (float)j / segments));
            st.SetUV2(new Vector2(point.X, point.Z) / BellRadius);
            st.AddVertex(point);
        }
        for (int i = 0; i < rings; i++)
        for (int j = 0; j < segments; j++)
        {
            int a = i * (segments + 1) + j, b = a + segments + 1;
            st.AddIndex(a);
            st.AddIndex(b);
            st.AddIndex(b + 1);
            st.AddIndex(a);
            st.AddIndex(b + 1);
            st.AddIndex(a + 1);
        }
        return st.Commit();
    }

    // ───────────────────────── the strands ─────────────────────────

    Vector3 AnchorOf(Strand s, Transform3D body) => body * (s.Arm
        ? new Vector3(Mathf.Cos(s.Angle) * 0.08f, 0.12f + 0.05f * _pulse, Mathf.Sin(s.Angle) * 0.08f)
        : MarginAt(s.Angle, _pulse));

    void StepStrands(float dt, float pulseRate, bool hit)
    {
        var body = _body.GlobalTransform;
        Vector3 axis = body.Basis.Y.Normalized();
        foreach (var s in _strands)
        {
            s.LastAnchor = s.Anchor;
            s.Anchor = AnchorOf(s, body);
        }
        // First frame, or a jump (a new room): lay the strands out at rest.
        if (!_strandsReady || body.Origin.DistanceTo(_lastCentre) > 3f)
        {
            foreach (var s in _strands)
            {
                Vector3 dir = (body.Basis * s.Rest).Normalized();
                for (int i = 0; i < s.Pos.Length; i++) s.Pos[i] = s.Prev[i] = s.Anchor + dir * s.Seg * i;
                s.LastAnchor = s.Anchor;
            }
            _strandsReady = true;
        }
        _lastCentre = body.Origin;
        if (hit)
        {
            // Hurt: the tentacles whip outward.
            foreach (var s in _strands)
            {
                Vector3 out_ = (s.Anchor - body.Origin).Normalized();
                for (int i = 1; i < s.Pos.Length; i++) s.Prev[i] -= out_ * 0.05f * i / (s.Pos.Length - 1);
            }
        }

        _accum += dt;
        int steps = Mathf.Min((int)(_accum / Step), 8);
        _accum = steps == 8 ? 0f : _accum - steps * Step;
        float drag = Mathf.Exp(-5f * Step);
        for (int n = 0; n < steps; n++)
        {
            _strandTime += Step;
            float k = (n + 1f) / steps;
            for (int si = 0; si < _strands.Count; si++)
            {
                var s = _strands[si];
                Vector3 anchor = s.LastAnchor.Lerp(s.Anchor, k);
                Vector3 rest = (body.Basis * s.Rest).Normalized();
                int count = s.Pos.Length;
                float seg = s.Seg;
                s.Pos[0] = s.Prev[0] = anchor;
                for (int i = 1; i < count; i++)
                {
                    float f = (float)i / (count - 1);
                    // Each strand relaxes toward its resting curve (out, then drooping toward the tip)...
                    Vector3 shape = (rest - axis * f * 0.6f).Normalized();
                    Vector3 acc = (anchor + shape * seg * i - s.Pos[i]) * s.Stiffness * _calm;
                    // ...sways in a slow current, more toward the tip...
                    float t = _strandTime;
                    acc += new Vector3(
                        Mathf.Sin(t * 0.7f + i * 0.4f + si * 1.7f),
                        0.35f * Mathf.Sin(t * 1.1f + si * 2.3f + i * 0.2f),
                        Mathf.Cos(t * 0.55f + i * 0.33f + si * 0.9f)) * 0.7f * f * (0.4f + 0.6f * _calm);
                    // ...resists kinking (a strand bends in long curves, never folds)...
                    if (i >= 2) acc += (s.Pos[i - 1] * 2f - s.Pos[i - 2] - s.Pos[i]) * 20f;
                    // ...and each stroke pushes water out behind the bell, carrying the strands with it.
                    acc -= axis * pulseRate * 0.6f * f;
                    // Streaming behind her, each strand ripples in a travelling S-wave of its own.
                    acc += _across * Mathf.Sin(t * 3.2f - i * 0.55f + si * 0.8f) * 2.4f * f * (1f - _calm);
                    Vector3 v = (s.Pos[i] - s.Prev[i]) * drag;
                    s.Prev[i] = s.Pos[i];
                    s.Pos[i] += v + acc * Step * Step;
                }
                // Keep the length: each node follows the one before. Some of the correction's speed is kept, so the
                // strands swing through turns instead of snapping.
                for (int i = 1; i < count; i++)
                {
                    Vector3 d = s.Pos[i] - s.Pos[i - 1];
                    float len = d.Length();
                    Vector3 want = s.Pos[i - 1] + (len > 1e-6f ? d / len : rest) * seg;
                    s.Prev[i] += (want - s.Pos[i]) * 0.9f;
                    s.Pos[i] = want;
                }
            }
        }
    }

    /// <summary>Each strand as a ribbon turned to face the camera, oral arms first so the tentacles lie over them.</summary>
    void DrawStrands(Vector3 eye)
    {
        _strandMesh.ClearSurfaces();
        _strandMesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, _strandMaterial);
        foreach (var s in _strands)
        {
            int count = s.Pos.Length;
            var color = new Color(1f, 1f, 1f, s.Arm ? 0.9f : 1f);
            var kind = new Vector2(s.Arm ? 1f : 0f, 0f);
            Vector3 lastL = default, lastR = default;
            for (int i = 0; i < count; i++)
            {
                float f = (float)i / (count - 1);
                Vector3 tangent = s.Pos[Mathf.Min(i + 1, count - 1)] - s.Pos[Mathf.Max(i - 1, 0)];
                Vector3 side = tangent.Cross(eye - s.Pos[i]);
                side = side.LengthSquared() > 1e-10f ? side.Normalized() : Vector3.Right;
                float width = Mathf.Lerp(s.RootWidth, s.TipWidth, f);
                Vector3 l = s.Pos[i] - side * width * 0.5f, r = s.Pos[i] + side * width * 0.5f;
                if (i > 0)
                {
                    float f0 = (float)(i - 1) / (count - 1);
                    Vertex(lastL, f0, 0f);
                    Vertex(lastR, f0, 1f);
                    Vertex(r, f, 1f);
                    Vertex(lastL, f0, 0f);
                    Vertex(r, f, 1f);
                    Vertex(l, f, 0f);
                }
                lastL = l;
                lastR = r;
            }

            void Vertex(Vector3 at, float along, float across)
            {
                _strandMesh.SurfaceSetColor(color);
                _strandMesh.SurfaceSetUV(new Vector2(along, across));
                _strandMesh.SurfaceSetUV2(kind);
                _strandMesh.SurfaceAddVertex(at);
            }
        }
        _strandMesh.SurfaceEnd();
    }

    // ───────────────────────── ink ─────────────────────────

    void SyncInk(PlaneWorld world)
    {
        while (_ink.Count < world.Clouds.Count)
        {
            var m = new MeshInstance3D { Mesh = new SphereMesh { Radius = 1f, Height = 1.2f }, MaterialOverride = _inkMaterial };
            GetParent().AddChild(m);
            _ink.Add(m);
        }
        for (int i = 0; i < _ink.Count; i++)
        {
            bool used = i < world.Clouds.Count;
            _ink[i].Visible = used;
            if (!used) continue;
            var c = world.Clouds[i];
            float k = c.Life / c.MaxLife;
            _ink[i].GlobalPosition = new Vector3(c.Position.X, LevelMap.SwimBand, c.Position.Y);
            _ink[i].Scale = Vector3.One * c.Radius * (1.3f - 0.3f * k);
        }
    }
}
