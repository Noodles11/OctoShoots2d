using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Clementine (THEME-BIBLE §5.1, DESIGN-TOPDOWN §2.1), presentation only: the sim's position, nothing more.
/// A bell of living glass (clementine_bell.gdshader), nearly clear, only her gonads and her rim bioluminescent, with a
/// short, nearly invisible fringe of fine hairs round its margin, eight long hair-thin fibre tentacles with light
/// running down them, and four oral arms — long ribbons of thin cloth twisting freely, their ruffled margins glowing,
/// growing with her skills (PlaneWorld.ArmLength) — on verlet chains (clementine_trail.gdshader). Swimming, she tips
/// almost onto her side, apex first; her bell always beats a little. Every stroke is a contraction beat — quick squeeze, slow relax,
/// a little recoil — paced by her speed; a jet or dash starts one at once. The bell leans into the swim on a soft
/// spring, the strands trail behind and swing through turns, and when she stops they float out around her while she
/// bobs. A hit flushes her magenta and whips the tentacles out; the ink dash turns her into a dark ghost.
/// Ink clouds left by dashes are drawn here too.
/// </summary>
public partial class BellView : Node3D
{
    const float BellRadius = 0.546f, BellHeight = 0.39f;
    /// <summary>Her fringe: this many fine hairs round the bell's margin; her long fibre tentacles; her oral arms.</summary>
    const int Hairs = 140, Fibers = 8, OralArms = 4;
    /// <summary>The strands' fixed step: steady motion at any frame rate.</summary>
    const float Step = 1f / 120f;
    /// <summary>Her glow in the water (HerColor: derived from her body colour).</summary>
    static Color Glow => OctoShoots.Game.Fx.HerColor.Glow;

    sealed class Strand
    {
        public Vector3[] Pos = null!, Prev = null!;
        public float Angle, Length, RootWidth, TipWidth, Stiffness;
        public bool Arm, Fiber;
        /// <summary>Its own seed (0..1), for its shader's beat.</summary>
        public float Seed;
        /// <summary>Which way the strand floats when nothing moves it, in the bell's frame.</summary>
        public Vector3 Rest;
        public Vector3 Anchor, LastAnchor;
        public float Seg => Length / (Pos.Length - 1);
    }

    Node3D _body = null!;
    ShaderMaterial _bellMaterial = null!, _strandMaterial = null!, _outlineMaterial = null!;
    /// <summary>Whether something is drawn over her now (kelp in front of her): a thin outline then shows round her bell.</summary>
    public bool Covered { get; set; }
    float _outline;
    ImmediateMesh _strandMesh = null!;
    /// <summary>Her searchlight (a beam from her bell, down and a little ahead), and a faint glow round the bell itself.</summary>
    SpotLight3D _light = null!;
    OmniLight3D _halo = null!;
    /// <summary>The beam's half-angle (degrees) and how far it reaches (before Lantern and glow).</summary>
    const float BeamAngle = 17f, BeamRange = 26f;
    /// <summary>The searchlight's strength (before Lantern, glow, her breath and pulse).</summary>
    const float BeamEnergy = 2.38f;
    Vector3 _beam = Vector3.Down;
    /// <summary>Which way her searchlight points (world, unit), and the cosine of its half-angle.</summary>
    public Vector3 BeamDirection => _beam;
    public float BeamCos => Mathf.Cos(Mathf.DegToRad(_light.SpotAngle));
    /// <summary>How much of her light gets through (1 all; the curse den's dark fades it).</summary>
    public float Dim = 1f;
    /// <summary>The depth's lantern (ReefLook.Lantern): her light's strength and reach against the dark.</summary>
    public float Lantern = 1f;
    float _dbgEnergy = 1f;
    bool _dbgFalloff;
    /// <summary>Her light: where it is (world), how far it reaches and how strong it is now.</summary>
    public Vector3 LightPosition => _light.GlobalPosition;
    public float LightRange => _light.SpotRange;
    public float LightEnergy => _light.Visible ? _light.LightEnergy : 0f;
    /// <summary>Her light's heart, over the bell's origin (inside the dome).</summary>
    public const float LightHeight = 1.2f;
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
    /// <summary>How fast she turns to face her aim (an easing rate, per second).</summary>
    const float AimTurn = 14f;
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

    /// <summary>
    /// The absorbing ritual (docs/PEARLS.md): how far she wears the pearl's look (0..1), the burst of light as she
    /// returns to her own (1 → 0), and where her strands are drawn (toward a pearl, the nearer the stronger).
    /// </summary>
    float _mimic, _burst, _pull;
    Vector3 _pullAt;
    string? _wearing;

    /// <summary>Where a pearl being absorbed hovers: circling her slowly, a little above her bell.</summary>
    public static Vector3 RitualAt(Vector3 her, float ritual)
    {
        float a = ritual * 2.2f + 0.6f;
        float reach = 1.05f - 0.25f * Mathf.Clamp(ritual / PearlRitual.ShatterAt, 0f, 1f);
        return her + new Vector3(Mathf.Cos(a) * reach, 0.45f + 0.1f * Mathf.Sin(ritual * 5f), Mathf.Sin(a) * reach);
    }

    /// <summary>The ritual's climax: the pearl is hers. A strong pulse of light, and her own colours flood back.</summary>
    public void Burst()
    {
        _burst = 1f;
        _wearing = null;
    }

    /// <summary>Pearl Diver: the pearl swelling in front of her while she charges it.</summary>
    MeshInstance3D _chargeOrb = null!;
    ShaderMaterial _chargeMaterial = null!;
    /// <summary>
    /// Light bubbles: the light she has to spend, 0..1, presentation only. Each volley spends a little (the bell dims to
    /// 0.85 when it is gone, never dark) and it rekindles while she holds her fire.
    /// </summary>
    float _reserve = 1f, _breath = 1f, _points;
    const float SpendPerBubble = 0.07f, Rekindle = 0.45f;

    public override void _Ready()
    {
        _body = new Node3D();
        AddChild(_body);
        _bellMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/clementine_bell.gdshader"), RenderPriority = 2 };
        var bell = BakeBell();
        _body.AddChild(new MeshInstance3D { Mesh = bell, MaterialOverride = _bellMaterial, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        // Her outline when something covers her: over everything, the kelp over her included (it draws at 3).
        _outlineMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/clementine_outline.gdshader"), RenderPriority = 5 };
        _body.AddChild(new MeshInstance3D { Mesh = bell, MaterialOverride = _outlineMaterial, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });

        _strandMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/clementine_trail.gdshader"), RenderPriority = 1 };
        _strandMesh = new ImmediateMesh();
        // The strands live in world space: they trail where she has been, not where she is.
        AddChild(new MeshInstance3D { Mesh = _strandMesh, TopLevel = true, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 8f });
        SetArm(AdventureTuning.ArmBase);
        for (int k = 0; k < Hairs; k++)
        {
            // All round the margin, a little uneven, as a real fringe is.
            float a = (k + 0.6f * Hash(k + 300)) * Mathf.Tau / Hairs;
            _strands.Add(new Strand
            {
                Angle = a,
                Length = 0.08f + 0.065f * Hash(k),
                RootWidth = 0.02f,
                TipWidth = 0.007f,
                Stiffness = 2.4f,
                Rest = new Vector3(Mathf.Cos(a), -0.3f, Mathf.Sin(a)).Normalized(),
                Pos = new Vector3[4],
                Prev = new Vector3[4],
            });
        }
        for (int k = 0; k < Fibers; k++)
        {
            // Long hair-thin tentacles from the margin, between the arms: optical fibres trailing far behind her.
            float a = (k + 0.5f) * Mathf.Tau / Fibers + 0.2f * Hash(k + 500);
            _strands.Add(new Strand
            {
                Fiber = true,
                Angle = a,
                Seed = Hash(k + 520),
                Length = 1.8f + 0.8f * Hash(k + 540),
                RootWidth = 0.014f,
                TipWidth = 0.008f,
                Stiffness = 1.4f,
                Rest = new Vector3(Mathf.Cos(a) * 0.3f, -1f, Mathf.Sin(a) * 0.3f).Normalized(),
                Pos = new Vector3[32],
                Prev = new Vector3[32],
            });
        }

        // Her light is the lantern of the scene: a searchlight from her bell, a focused beam down through the water and a
        // little ahead of her, swinging after her as she swims, as a helicopter's does. It casts shadows (soft, from a
        // light the size of her bell's heart), so the reef's walls, rocks, kelp and creatures stand out against it and
        // what lies behind them stays dark; her own bell and strands cast none. A faint glow round the bell keeps what
        // swims right beside her from going black.
        _light = new SpotLight3D
        {
            LightColor = Glow, LightEnergy = BeamEnergy, SpotRange = BeamRange, SpotAngle = BeamAngle, SpotAngleAttenuation = 0.6f, SpotAttenuation = 0.9f,
            ShadowEnabled = OctoShoots.Game.Settings.ViewOptions.Shadows, ShadowBias = 0.06f, ShadowNormalBias = 1.2f,
            LightSize = 0.3f, ShadowBlur = 1f, LightSpecular = 0.8f, TopLevel = true,
        };
        _halo = new OmniLight3D { LightColor = Glow, LightEnergy = 0.5f, OmniRange = 3f, OmniAttenuation = 1.6f, ShadowEnabled = false, Position = new Vector3(0f, LightHeight, 0f) };
        AddChild(_halo);
        foreach (string arg in OS.GetCmdlineUserArgs())
            if (arg.StartsWith("--dbg-lantern="))
            {
                // Verification: her light's strength factor and falloff ("6,0.8").
                var parts = arg["--dbg-lantern=".Length..].Split(',');
                _dbgEnergy = float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
                if (parts.Length > 1)
                {
                    _light.SpotAttenuation = float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
                    _dbgFalloff = true;
                }
            }
        AddChild(_light);
        var sphere = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 32, Rings = 16 };
        var shieldMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/plane_bubble.gdshader"), RenderPriority = 3 };
        shieldMaterial.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("tint"), new Color(0.75f, 0.92f, 1f));
        _shield = new MeshInstance3D { Mesh = sphere, MaterialOverride = shieldMaterial, Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_shield);
        // Pearl Diver's charge: the light she gathers to throw, the same lantern as her bubbles (one substance).
        _chargeMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/light_shot.gdshader"), RenderPriority = 3 };
        _chargeOrb = new MeshInstance3D { Mesh = new QuadMesh { Size = Vector2.One }, MaterialOverride = _chargeMaterial, Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
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
        RenderingServer.GlobalShaderParameterSet(OctoShoots.Game.Fx.SN.Of("her_glow"), new Vector4(GlobalPosition.X, GlobalPosition.Y, GlobalPosition.Z, _glowRadius));
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

    /// <summary>
    /// Prism Pearl (PlanePrism.cs): which of the red and the blue beside her this view draws (0 the red, 1 the blue;
    /// −1: herself). Those are shown only while her light is split, smaller, in their colour, their searchlight unshadowed
    /// and the floor's glow left to her.
    /// </summary>
    public int Part { get; init; } = -1;
    /// <summary>How far she is split now (0 whole → 1), and the colour this one wears (herself: the green).</summary>
    float _prism;
    Color PrismColour => Part switch { 0 => CombatView.LightOf(PrismHue.Red), 1 => CombatView.LightOf(PrismHue.Blue), _ => CombatView.LightOf(PrismHue.Green) };

    public void Sync(PlaneWorld world, float alpha, float dt)
    {
        _time += dt;
        float h = Mathf.Min(dt, 0.05f);
        var p = world.Player;
        SetArm(world.Options.Adventure ? world.ArmLength : AdventureTuning.ArmBase);
        // A new level's body: its jet, dash and hurt timers start from zero, which must not read as a stroke or a hit.
        if (!ReferenceEquals(p, _drawnFor))
        {
            _drawnFor = p;
            _lastJet = p.JetTimer;
            _lastDash = p.DashTimer;
            _lastHurt = p.HurtTimer;
        }
        var at = System.Numerics.Vector2.Lerp(p.PrevPosition, p.Position, alpha);
        _prism = world.PrismOpen;
        if (Part >= 0)
        {
            // The red or the blue: only while her light is split, where it swims, growing out of her as she splits.
            Visible = world.Prism;
            if (!Visible)
            {
                _strandsReady = false;
                return;
            }
            var part = world.PrismParts[Part];
            at = System.Numerics.Vector2.Lerp(part.PrevPosition, part.Position, alpha);
        }
        else if (world.Prism)
        {
            // Herself, split: the green, at the triangle's front corner.
            at = System.Numerics.Vector2.Lerp(world.PrismLead.PrevPosition, world.PrismLead.Position, alpha);
        }
        // Split, all three are smaller; the red and the blue come out of her (from nothing to their size).
        float size = Mathf.Lerp(1f, PrismTuning.Size, _prism);
        Scale = Vector3.One * (Part >= 0 ? PrismTuning.Size * Mathf.Clamp(_prism * 1.6f, 0.05f, 1f) : size);
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
        float ampTarget = burst ? 1f : 0.42f + 0.45f * Mathf.Min(pace, 1f);
        _amp = Mathf.Lerp(_amp, ampTarget, 1f - Mathf.Exp(-4f * h));
        _phase = Mathf.PosMod(_phase + h * (burst ? 1.8f : 0.6f + 0.8f * Mathf.Min(pace, 1.3f)), 1f);
        float before = _pulse;
        _pulse = Mathf.Lerp(_pulse, Contraction(_phase) * _amp, 1f - Mathf.Exp(-25f * h));
        // Tide Pulse: her bell draws in hard before each of its waves and snaps open as the wave leaves.
        _pulse = Mathf.Max(_pulse, OctoShoots.Core.Plane.PushTuning.Contraction(p.PushTime, p.PushLength));
        float pulseRate = h > 0f ? (_pulse - before) / h : 0f;
        _hurt = hit ? 1f : Mathf.MoveToward(_hurt, 0f, h * 2.2f);
        _inkGhost = Mathf.MoveToward(_inkGhost, p.DashInvulnerableTimer > 0f ? 1f : 0f, h * 10f);

        // The lean: the apex leads into the swim on a critically damped spring; at rest she tips a little toward where
        // she aims (curiosity).
        // Swimming she tips almost onto her side (about 70° at her cruise), apex first, as a jelly swims.
        Vector2 leanTarget = speed > 0.05f ? vel / speed * Mathf.Min(Mathf.Sqrt(pace) * 1.22f, 1.38f) : new Vector2(p.Aim.X, p.Aim.Y) * 0.07f;
        const float w = 6f;
        _leanVel += ((leanTarget - _lean) * w * w - _leanVel * 2f * w) * h;
        _lean += _leanVel * h;

        // Idle drift: once she settles she bobs gently (~1 m) on the swim plane; each stroke lifts her a touch.
        _idle = Mathf.MoveToward(_idle, speed < 0.6f ? 1f : 0f, h * 1.2f);
        float ease = _idle * _idle * (3f - 2f * _idle);
        float bob = 0.5f * Mathf.Sin(_time * 1.1f) * ease + 0.06f * _pulse;
        Position = new Vector3(at.X, LevelMap.SwimBand + bob * (1f - DiveTurn) - DiveDepth, at.Y);
        // She turns to face her aim, her first gonad toward it (a turn about her axis: gonad 0 sits at 45° in the bell's
        // own frame), quickly but not in a snap.
        if (p.Aim.LengthSquared() > 1e-6f)
        {
            float target = Mathf.Pi * 0.25f - Mathf.Atan2(p.Aim.Y, p.Aim.X);
            _spin = Mathf.LerpAngle(_spin, target, 1f - Mathf.Exp(-AimTurn * h));
        }
        float lean = _lean.Length();
        var tilt = lean > 1e-4f ? new Basis(new Vector3(_lean.Y, 0f, -_lean.X) / lean, lean) : Basis.Identity;
        _body.Basis = tilt * new Basis(Vector3.Up, _spin);
        // Bounced off Drippus Prime: squashed flat against it for a moment, then stretched along her flight, she flips
        // once as she tumbles away.
        if (p.TumbleTimer > 0f)
        {
            float k = 1f - p.TumbleTimer / DrippusTuning.Tumble;
            var along = new Vector3(p.TumbleDir.X, 0f, p.TumbleDir.Y);
            var across = new Vector3(-along.Z, 0f, along.X);
            float stretch = k < 0.15f ? 1f - 0.4f * Mathf.Sin(k / 0.15f * Mathf.Pi) : 1f + 0.3f * Mathf.Sin((k - 0.15f) / 0.85f * Mathf.Pi);
            var frame = new Basis(along, Vector3.Up, across);
            var squash = frame * Basis.FromScale(new Vector3(stretch, 1f / Mathf.Sqrt(stretch), 1f / Mathf.Sqrt(stretch))) * frame.Transposed();
            _body.Basis = new Basis(across, Mathf.Tau * Mathf.SmoothStep(0f, 1f, k)) * squash * _body.Basis;
        }
        // Diving, she turns head-first (apex down), leaning south so the camera sees her turn.
        if (DiveTurn > 0f) _body.Basis = new Basis(Vector3.Right, -DiveTurn * Mathf.Pi * 0.85f) * _body.Basis;

        _bellMaterial.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("pulse"), _pulse);
        // Covered, the outline comes quickly; uncovered, it lingers a moment.
        _outline = Mathf.MoveToward(_outline, Covered ? 1f : 0f, h * (Covered ? 8f : 3f));
        _outlineMaterial.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("pulse"), _pulse);
        _outlineMaterial.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("shown"), _outline * _outline * (3f - 2f * _outline));
        _bellMaterial.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("wave"), _phase < 0.55f ? _phase / 0.55f : 1.3f);
        _bellMaterial.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("hurt"), _hurt);
        _bellMaterial.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("ink"), _inkGhost);
        _reserve = Mathf.Min(1f, _reserve + Rekindle * h);
        // Clinging murklings drink her glow (each a little); never dark.
        float drunk = 1f - 0.1f * world.Clingers - 0.03f * world.LesserClingers;
        _breath = Mathf.Lerp(_breath, (0.85f + 0.15f * _reserve) * Mathf.Max(drunk, 0.65f), 1f - Mathf.Exp(-8f * h));
        _bellMaterial.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("breath"), _breath);
        SyncVitals(world, h);
        _strandMaterial.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("hurt"), _hurt);
        _strandMaterial.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("ink"), _inkGhost);
        // Lantern Pearl: her glow reaches farther and shines brighter.
        float glow = world.Run.Loadout.Stats[OctoShoots.Core.Items.Stat.Glow];
        // Farther and brighter in the dark depths (the lantern), and with Lantern Pearl: in the Shallows a supplement to
        // the sun, in the Trench and the Abyss about three times as strong and falling off more gently, so it reaches the
        // floor of the canyons she swims over.
        float deep = Mathf.Clamp((Lantern - 1f) / 0.85f, 0f, 1f);
        _light.SpotRange = BeamRange * glow * Mathf.Sqrt(Lantern);
        if (!_dbgFalloff) _light.SpotAttenuation = Mathf.Lerp(1f, 0.6f, deep);
        _light.LightEnergy = BeamEnergy * _dbgEnergy * Mathf.Pow(Lantern, 1.8f) * (1f + 0.4f * (glow - 1f)) * (0.94f + 0.12f * _pulse) * (0.7f + 0.3f * _breath);
        _light.LightColor = Glow.Lerp(PrismColour, _prism).Lerp(new Color(1f, 0.3f, 0.55f), _hurt * 0.5f);
        // The red and the blue: lighter lanterns, unshadowed (her own casts the reef's shadows).
        if (Part >= 0)
        {
            _light.ShadowEnabled = false;
            _light.LightEnergy *= 0.5f;
        }
        _halo.LightColor = _light.LightColor;
        // The beam: she casts it from inside her bell, out of its opening — the way the inside of her bell points. At
        // rest that is down; swimming (tipped apex first) it streams out behind her.
        var axis = _body.GlobalBasis.Y.Normalized();
        _beam = _beam.Lerp(-axis, 1f - Mathf.Exp(-12f * h)).Normalized();
        var from = _body.GlobalPosition + axis * 0.12f;
        _light.GlobalTransform = new Transform3D(Basis.LookingAt(_beam, Vector3.Back), from);

        if (speed > 0.3f) _across = new Vector3(-vel.Y, 0f, vel.X) / speed;
        _calm = Mathf.Lerp(_calm, 1f - 0.85f * Mathf.Min(pace, 1f), 1f - Mathf.Exp(-3f * h));
        SyncRitual(world, h);
        StepStrands(h, pulseRate, hit);
        var camera = GetViewport().GetCamera3D();
        if (camera != null) DrawStrands(camera.GlobalPosition);
        SyncInk(world);
        SyncShield(p, h);
        SyncCharge(p);
        if (Part < 0) SyncGlowPool(world);
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
        RenderingServer.GlobalShaderParameterSet(OctoShoots.Game.Fx.SN.Of("her_glow"), new Vector4(at.X, at.Y, at.Z, _glowRadius));
        RenderingServer.GlobalShaderParameterSet(OctoShoots.Game.Fx.SN.Of("her_glow_strength"), (1f - DiveTurn) * (0.9f + 0.1f * _pulse));
    }

    float _glowRadius = 3f;

    /// <summary>
    /// The ritual on her: she takes on the touched pearl's colours, pattern and glow like a chameleon, holds them, and
    /// lets them go in the burst. Her strands reach for any pearl near her (the nearer the harder) and, through the
    /// ritual, for the pearl circling her, until it shatters.
    /// </summary>
    void SyncRitual(PlaneWorld world, float h)
    {
        PlanePearl? ritual = null;
        PlanePearl? nearest = null;
        float best = 6f;
        var her = new System.Numerics.Vector2(GlobalPosition.X, GlobalPosition.Z);
        foreach (var q in world.Pearls)
        {
            if (q.Taken) continue;
            if (q.Absorbing) ritual = q;
            else
            {
                float d = System.Numerics.Vector2.Distance(q.Position, her);
                if (d < best)
                {
                    best = d;
                    nearest = q;
                }
            }
        }
        float pull = 0f;
        if (ritual is not null)
        {
            if (_wearing != ritual.ItemId && world.Run.Catalog is { } catalog && catalog.TryGet(ritual.ItemId, out var item) && item.Pearl is { } look)
            {
                _wearing = ritual.ItemId;
                foreach (var (name, i) in new[] { ("mimic_c0", 0), ("mimic_c1", 1), ("mimic_c2", 2) })
                {
                    var c = Color.FromHtml(look.Colors[Mathf.Min(i, look.Colors.Count - 1)]);
                    _bellMaterial.SetShaderParameter(name, c);
                    if (i < 2) _strandMaterial.SetShaderParameter(name, c);
                }
                _bellMaterial.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("mimic_icon"), (int)look.Icon);
            }
            float k = Mathf.Clamp(ritual.Ritual / PearlRitual.Mimic, 0f, 1f);
            _mimic = k * k * (3f - 2f * k);
            if (ritual.Ritual < PearlRitual.ShatterAt)
            {
                pull = 1f;
                _pullAt = RitualAt(GlobalPosition, ritual.Ritual);
            }
        }
        else
        {
            if (_wearing is null) _mimic = Mathf.MoveToward(_mimic, 0f, h / 0.35f);
            if (nearest is not null)
            {
                float k = 1f - best / 6f;
                pull = 0.7f * k * k;
                _pullAt = new Vector3(nearest.Position.X, LevelMap.SwimBand + 0.25f, nearest.Position.Y);
            }
        }
        _pull = Mathf.MoveToward(_pull, pull, h * (pull > _pull ? 3f : 6f));
        _burst = Mathf.MoveToward(_burst, 0f, h / 0.45f);
        // Prism Pearl: split, each wears its own colour of her light (the pearl's look, worn as in the ritual).
        float mimic = _mimic;
        if (_prism > 0f)
        {
            var c = PrismColour;
            _bellMaterial.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("mimic_c0"), c.Darkened(0.35f));
            _bellMaterial.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("mimic_c1"), c);
            _bellMaterial.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("mimic_c2"), c.Lightened(0.45f));
            _bellMaterial.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("mimic_pattern"), 7);
            _strandMaterial.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("mimic_c0"), c.Darkened(0.2f));
            _strandMaterial.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("mimic_c1"), c.Lightened(0.3f));
            mimic = Mathf.Max(mimic, _prism);
            // The ritual's colours are put back when it next wears a pearl.
            _wearing = null;
        }
        _bellMaterial.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("mimic"), mimic);
        _bellMaterial.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("burst"), _burst);
        _strandMaterial.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("mimic"), mimic);
        _strandMaterial.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("burst"), _burst);
        // The pulse of light: her glow flares far out and falls back.
        _light.LightEnergy *= (1f + 4f * _burst + 0.4f * _mimic) * Dim;
        _light.SpotRange *= (1f + 0.8f * _burst) * (0.4f + 0.6f * Dim);
        _halo.LightEnergy = 0.5f * Mathf.Pow(Lantern, 1.2f) * (1f + 2f * _burst) * Dim;
    }

    public override void _ExitTree() => RenderingServer.GlobalShaderParameterSet(OctoShoots.Game.Fx.SN.Of("her_glow_strength"), 0f);

    /// <summary>
    /// Health into the gonad rings (draining at most 0.8 of her max a second, healing back faster) and the active pearl's
    /// charge into the rim; when the charge drops from ready, the rim snuffs out.
    /// </summary>
    void SyncVitals(PlaneWorld world, float h)
    {
        // A skill point waits: her gonads pulse until it is spent.
        _points = Mathf.MoveToward(_points, world.Run.Points > 0 ? 1f : 0f, h * 3f);
        _bellMaterial.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("points"), _points);
        // The adventure shows her health and her pearl's charge on the HUD's gonad gauge: her gonads and her rim are
        // only hers then, always whole, never a gauge.
        if (world.Options.Adventure)
        {
            _bellMaterial.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("health"), 1f);
            _bellMaterial.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("charge"), -1f);
            _bellMaterial.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("snuff"), 0f);
            return;
        }
        float target = world.Run.MaxHp > 0f ? Mathf.Clamp(world.Player.Hp / world.Run.MaxHp, 0f, 1f) : 0f;
        _health = Mathf.MoveToward(_health, target, h * (target < _health ? 0.8f : 1.5f));
        bool holds = world.Run.Active?.Active is not null;
        float charge = world.Run.ActiveCharge;
        if (holds && _lastCharge >= 1f && charge < 1f) _snuff = 1f;
        _lastCharge = holds ? charge : 1f;
        _snuff = Mathf.MoveToward(_snuff, 0f, h / SnuffSeconds);
        _bellMaterial.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("health"), _health);
        _bellMaterial.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("charge"), holds ? charge : -1f);
        _bellMaterial.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("snuff"), _snuff);
        _bellMaterial.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("spin"), _spin);
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
        _shield.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("fade"), _shieldShown * flicker);
        _shield.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("rainbow"), 0.35f + 0.4f * _ripple);
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
        // The mote's glow reaches well past its core: a quad several times the throw's size.
        _chargeOrb.Scale = Vector3.One * size * 4f;
        _chargeOrb.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("lantern"), 0.3f + 0.6f * k * k + (full ? 0.15f * Mathf.Sin(_time * 14f) : 0f));
        _chargeOrb.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("warmth"), full ? 0.6f : 0.3f * k);
        _chargeOrb.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("seed"), 1.7f);
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

    /// <summary>
    /// Her oral arms: four long ribbons of thin cloth from round her mouth, one between each pair of gonads, a little
    /// different in length about the sim's arm (it grows with every skill point she spends). A new length is taken up
    /// over a few strokes, the chains easing out to it.
    /// </summary>
    void SetArm(float length)
    {
        if (!_strands.Exists(s => s.Arm))
        {
            for (int k = 0; k < OralArms; k++)
            {
                float a = k * Mathf.Tau / OralArms;
                _strands.Insert(k, new Strand
                {
                    Arm = true,
                    Angle = a,
                    Seed = Hash(k + 60),
                    RootWidth = 0.34f,
                    TipWidth = 0.12f,
                    Stiffness = 2.2f,
                    Rest = new Vector3(Mathf.Cos(a) * 0.35f, -1f, Mathf.Sin(a) * 0.35f).Normalized(),
                    Pos = new Vector3[40],
                    Prev = new Vector3[40],
                });
            }
            _strandsReady = false;
        }
        int i = 0;
        foreach (var s in _strands)
            if (s.Arm) s.Length = length * ArmShare[i++ % ArmShare.Length];
    }

    /// <summary>Each oral arm's share of the sim's arm length.</summary>
    static readonly float[] ArmShare = { 1f, 0.82f, 0.94f, 0.76f };

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
                    // A pearl near her draws her strands toward it, like a magnet, the nearer the harder.
                    if (_pull > 0.001f)
                    {
                        Vector3 to = _pullAt - s.Pos[i];
                        float d = to.Length();
                        if (d > 0.05f) acc += to / d * _pull * 16f * f * Mathf.Min(d, 1.5f);
                    }
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
        // Under her bell the strands are hidden (only where they come out past its rim do they show).
        var bell = _body.GlobalTransform;
        _strandMaterial.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("bell_centre"), bell.Origin);
        _strandMaterial.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("bell_radius"), BellRadius * bell.Basis.Scale.X);
        _strandMesh.ClearSurfaces();
        _strandMesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, _strandMaterial);
        foreach (var s in _strands)
        {
            int count = s.Pos.Length;
            // The fringe barely there (an accent), the arm as its shader has it.
            var color = new Color(1f, 1f, 1f, s.Arm || s.Fiber ? 1f : 0.16f);
            var kind = new Vector2(s.Arm ? 1f : s.Fiber ? 2f : 0f, s.Seed);
            Vector3 lastL = default, lastR = default;
            float turn = 0f;
            for (int i = 0; i < count; i++)
            {
                float f = (float)i / (count - 1);
                // The oral arm twists along its length (about two and a half turns, unevenly, drifting slowly): the
                // ribbon pinches as it turns edge-on and opens wide again face-on.
                turn = s.Arm ? Mathf.Pow(Mathf.Abs(Mathf.Cos(f * Mathf.Pi * 4f + 0.9f * Mathf.Sin(f * 5f + _time * 0.7f + s.Seed * 6f) + _time * 0.6f + s.Angle * 2f)), 0.6f) : 1f;
                Vector3 tangent = s.Pos[Mathf.Min(i + 1, count - 1)] - s.Pos[Mathf.Max(i - 1, 0)];
                Vector3 side = tangent.Cross(eye - s.Pos[i]);
                side = side.LengthSquared() > 1e-10f ? side.Normalized() : Vector3.Right;
                float width = Mathf.Lerp(s.RootWidth, s.TipWidth, f) * (s.Arm ? 0.15f + 0.85f * turn : 1f);
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
                // (Red carries how face-on the twist is there: the shader shades the turned-away part.)
                _strandMesh.SurfaceSetColor(new Color(turn, color.G, color.B, color.A));
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
            // The dash leaves no ink now: her light bursts instead (CombatView.DashBurst). The cloud stays in the sim.
            bool used = false;
            _ink[i].Visible = used;
            if (!used) continue;
            var c = world.Clouds[i];
            float k = c.Life / c.MaxLife;
            _ink[i].GlobalPosition = new Vector3(c.Position.X, LevelMap.SwimBand, c.Position.Y);
            _ink[i].Scale = Vector3.One * c.Radius * (1.3f - 0.3f * k);
        }
    }
}
