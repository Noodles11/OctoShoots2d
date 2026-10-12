using System;
using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Queen Clam and her arena, drawn from the sim. She is a real giant clam (art rules in CLAUDE.md: no face, nothing
/// cartoonish): two heavy ribbed valves, ash-drained with ink in their grooves, the lid opening and closing on its hinge
/// (creaking on the telegraph, rattling in the stagger, slamming on the snap), a dark corrupted mantle, and in her
/// middle the black pearl her light must strike. She never turns. Freed, the pearl bursts into white light and stays
/// white, her colour floods back, and she leaps aside and lies open. Also the shadow before she drops, the landing wave
/// and sand puff, the snap's warning rim, and the mud cloud standing around the sealed arena.
/// </summary>
public partial class BossView : Node3D
{
    const float R = PlaneBossTuning.BodyRadius;
    /// <summary>How far she falls from (metres above her seat) when she drops in.</summary>
    const float DropHeight = 16f;
    const float OpenDeg = 52f;

    Node3D _clam = null!, _hinge = null!;
    MeshInstance3D _bottom = null!, _top = null!, _shadow = null!, _wave = null!, _snapRim = null!, _pearl = null!;
    readonly MeshInstance3D[] _puffs = new MeshInstance3D[10];
    StandardMaterial3D _waveMat = null!, _snapMat = null!, _puffMat = null!, _shadowMat = null!, _mantleMat = null!, _whitePearl = null!;
    ShaderMaterial _blackPearl = null!;
    OmniLight3D _burstLight = null!;
    OctoShoots.Game.Fx.FxParticles _motes = null!;
    bool _burst;
    ShaderMaterial _shell = null!;
    readonly ShaderMaterial[] _mud = new ShaderMaterial[4];
    Node3D _mudRoot = null!;
    float _time, _drained = 1f, _snapSlam = 1f, _landedAt = -10f, _freedAt = -10f;
    BossStage _lastStage;

    public override void _Ready()
    {
        _shell = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/clam_shell.gdshader") };
        _clam = new Node3D();
        AddChild(_clam);

        // The bottom valve: a flattened bowl; the top one hinged at the back (local −X), opening upward.
        var dome = new SphereMesh { Radius = 1f, Height = 1f, IsHemisphere = true, RadialSegments = 40, Rings = 12 };
        _bottom = new MeshInstance3D { Mesh = dome, MaterialOverride = _shell, Scale = new Vector3(R, 0.75f, R * 0.88f), RotationDegrees = new Vector3(180f, 0f, 0f) };
        _clam.AddChild(_bottom);

        // The mantle inside, seen when she opens: dark, corrupted flesh (warm again once she is freed).
        _mantleMat = new StandardMaterial3D { AlbedoColor = new Color(0.1f, 0.07f, 0.09f), Roughness = 0.35f, Metallic = 0f };
        var mantle = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 1f, BottomRadius = 1f, Height = 0.04f, RadialSegments = 40 },
            Scale = new Vector3(R * 0.94f, 1f, R * 0.82f),
            Position = new Vector3(0f, 0.02f, 0f),
            MaterialOverride = _mantleMat,
        };
        _clam.AddChild(mantle);
        // The black pearl in her middle: corruption, glossy and drinking the light, a cold glint deep in it when she
        // opens. Her light must strike it. Freed, it is white.
        _blackPearl = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/corruption_orb.gdshader") };
        _blackPearl.SetShaderParameter("ember_color", new Color(0.55f, 0.3f, 0.85f));
        _whitePearl = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.97f, 0.96f, 0.93f), Roughness = 0.08f, Metallic = 0.25f, RimEnabled = true, Rim = 1f,
            EmissionEnabled = true, Emission = new Color(1f, 0.95f, 0.85f), EmissionEnergyMultiplier = 0.6f,
        };
        _pearl = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 32, Rings = 16 },
            MaterialOverride = _blackPearl,
            // Sits on the mantle, just under the shut lid.
            Scale = Vector3.One * PlaneBossTuning.CoreRadius * 0.55f,
            Position = new Vector3(0f, 0.36f, 0f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        _pearl.SetInstanceShaderParameter("wobble", 0.05f);
        _pearl.SetInstanceShaderParameter("ember", 0.1f);
        _clam.AddChild(_pearl);
        _burstLight = new OmniLight3D { LightColor = new Color(1f, 0.93f, 0.78f), LightEnergy = 0f, OmniRange = 30f, ShadowEnabled = false, Position = new Vector3(0f, 1.5f, 0f) };
        _clam.AddChild(_burstLight);
        _motes = new OctoShoots.Game.Fx.FxParticles(600, additive: true);
        AddChild(_motes);

        _hinge = new Node3D { Position = new Vector3(-R, 0f, 0f) };
        _clam.AddChild(_hinge);
        _top = new MeshInstance3D { Mesh = dome, MaterialOverride = _shell, Scale = new Vector3(R, 0.95f, R * 0.88f), Position = new Vector3(R, 0f, 0f) };
        _hinge.AddChild(_top);

        // Her shadow as she falls.
        _shadowMat = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(0f, 0f, 0f, 0.5f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha };
        _shadow = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 1f, BottomRadius = 1f, Height = 0.02f, RadialSegments = 40 }, MaterialOverride = _shadowMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_shadow);

        // The landing wave: a bright ring running out to the rim.
        _waveMat = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(0.95f, 0.88f, 0.7f, 0.8f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, NoDepthTest = false };
        _wave = new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.93f, OuterRadius = 1f, Rings = 64, RingSegments = 6 }, MaterialOverride = _waveMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_wave);

        // Sand puffing out where she lands.
        _puffMat = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(0.62f, 0.54f, 0.4f, 0.5f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha };
        var puffMesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 12, Rings = 6 };
        for (int i = 0; i < _puffs.Length; i++)
        {
            _puffs[i] = new MeshInstance3D { Mesh = puffMesh, MaterialOverride = _puffMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Visible = false };
            AddChild(_puffs[i]);
        }

        // The snap's warning: a red rim on the floor at her reach.
        _snapMat = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(1f, 0.2f, 0.2f, 0.6f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha };
        _snapRim = new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.96f, OuterRadius = 1f, Rings = 64, RingSegments = 4 }, MaterialOverride = _snapMat, Scale = new Vector3(PlaneBossTuning.SnapRange, 0.05f, PlaneBossTuning.SnapRange), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_snapRim);

        // The mud cloud: stacked layers of haze around the arena, the higher ones over the walls.
        _mudRoot = new Node3D();
        AddChild(_mudRoot);
        var shader = GD.Load<Shader>("res://assets/shaders/mud_cloud.gdshader");
        float[] heights = { 0.4f, 2.5f, 5.5f, 9.5f };
        for (int i = 0; i < _mud.Length; i++)
        {
            _mud[i] = new ShaderMaterial { Shader = shader };
            _mud[i].SetShaderParameter("layer", (float)i);
            _mudRoot.AddChild(new MeshInstance3D
            {
                Mesh = new PlaneMesh { Size = new Vector2(120f, 120f) },
                MaterialOverride = _mud[i],
                Position = new Vector3(0f, LevelMap.SwimBand + heights[i], 0f),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
        }
        Visible = false;
    }

    /// <summary>A new level: her arena around its rift.</summary>
    // The corruption's crust (docs/CORRUPTION.md): glossy ink lumps over her top valve, one fifth of them per layer; each
    // layer cleansed from the arena breaks its lumps off, tumbling away in specks.
    readonly List<(MeshInstance3D Mesh, int Layer)> _crust = new();
    readonly List<(MeshInstance3D Mesh, Vector3 Velocity, float Age)> _falling = new();
    ShaderMaterial? _crustMat;

    void BuildCrust()
    {
        foreach (var (mesh, _) in _crust)
            if (IsInstanceValid(mesh)) mesh.QueueFree();
        foreach (var (mesh, _, _) in _falling) mesh.QueueFree();
        // Lumps already broken off were moved to the view itself (and freed when they finished falling).
        _crust.Clear();
        _falling.Clear();
        _crustMat ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/corruption_orb.gdshader") };
        var lump = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 14, Rings = 7 };
        var rng = new RandomNumberGenerator { Seed = 7 };
        for (int i = 0; i < 25; i++)
        {
            // Over the dome (unit radius, half as high), spread so every layer covers all of it.
            float a = i * 2.39996f, rho = Mathf.Sqrt((i + 0.5f) / 25f) * 0.86f;
            var at = new Vector3(Mathf.Cos(a) * rho, 0.5f * Mathf.Sqrt(Mathf.Max(0f, 1f - rho * rho)) + 0.02f, Mathf.Sin(a) * rho);
            float size = rng.RandfRange(0.28f, 0.5f);
            var mesh = new MeshInstance3D
            {
                Mesh = lump,
                MaterialOverride = _crustMat,
                Position = at,
                // The valve is scaled (R, 0.95, 0.88 R): undo it so the lumps stay round, and flatten them onto the shell.
                Scale = new Vector3(size / R, size * 0.55f / 0.95f, size / (R * 0.88f)),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            mesh.SetInstanceShaderParameter("ember", 0.06f);
            mesh.SetInstanceShaderParameter("wobble", 0.15f);
            mesh.SetInstanceShaderParameter("seed", i * 1.3f);
            _top.AddChild(mesh);
            _crust.Add((mesh, i % 5));
        }
    }

    void SyncCrust(PlaneWorld world, PlaneBoss boss, float dt)
    {
        int crust = world.Corruption is null ? 0 : boss.Crust;
        for (int i = 0; i < _crust.Count; i++)
        {
            var (mesh, layer) = _crust[i];
            if (!mesh.Visible || mesh.GetParent() != _top) continue;
            if (layer < crust) continue;
            // This layer is cleansed away: the lump breaks off and tumbles.
            var at = mesh.GlobalTransform;
            _top.RemoveChild(mesh);
            AddChild(mesh);
            mesh.GlobalTransform = at;
            var out_ = (at.Origin - _clam.GlobalPosition) with { Y = 0f };
            _falling.Add((mesh, out_.Normalized() * 3f + Vector3.Up * 2.5f, 0f));
        }
        for (int i = _falling.Count - 1; i >= 0; i--)
        {
            var (mesh, v, age) = _falling[i];
            age += dt;
            v += Vector3.Down * 6f * dt;
            mesh.Position += v * dt;
            mesh.Rotation += new Vector3(3f, 2f, 1f) * dt;
            mesh.SetInstanceShaderParameter("fade", 1f - age / 0.9f);
            if (age >= 0.9f)
            {
                mesh.QueueFree();
                _falling.RemoveAt(i);
                continue;
            }
            _falling[i] = (mesh, v, age);
        }
    }

    public void Show(PlaneWorld world)
    {
        BuildCrust();
        var c = world.ArenaCenter;
        _mudRoot.Position = new Vector3(c.X, 0f, c.Y);
        foreach (var m in _mud)
        {
            m.SetShaderParameter("center", new Vector2(c.X, c.Y));
            m.SetShaderParameter("rim", world.ArenaRadius);
            m.SetShaderParameter("amount", 0f);
        }
        _drained = 1f;
        _landedAt = _freedAt = -10f;
        _burst = false;
        _pearl.MaterialOverride = _blackPearl;
        _burstLight.LightEnergy = 0f;
        _motes.Clear();
        _lastStage = BossStage.Waiting;
        Visible = world.Boss is not null;
    }

    public void Sync(PlaneWorld world, float dt)
    {
        var boss = world.Boss;
        if (boss is null) return;
        _time += dt;
        if (boss.Stage != _lastStage)
        {
            if (boss.Stage == BossStage.Fight) _landedAt = _time;
            if (boss.Stage == BossStage.Freed) _freedAt = _time;
            _lastStage = boss.Stage;
        }

        var c = world.ArenaCenter;
        float seat = LevelMap.SwimBand - 0.55f;
        _clam.Visible = boss.Stage is BossStage.Drop or BossStage.Fight or BossStage.Freed;
        float y = seat;
        _shadow.Visible = boss.Stage == BossStage.Drop;
        if (boss.Stage == BossStage.Drop)
        {
            float k = Mathf.Clamp(boss.StageTime / PlaneBossTuning.ShadowSeconds, 0f, 1f);
            y = seat + DropHeight * (1f - k * k);
            _shadow.Position = new Vector3(boss.Position.X, LevelMap.SwimBand - 0.6f, boss.Position.Y);
            _shadow.Scale = new Vector3(R * (0.3f + 0.7f * k), 1f, R * 0.88f * (0.3f + 0.7f * k));
            _shadowMat.AlbedoColor = new Color(0f, 0f, 0f, 0.15f + 0.4f * k);
        }

        // Facing, plus the shakes: a creak on the telegraph, a rattle in the stagger, a shudder when freed.
        float shake = 0f;
        if (boss.Stage == BossStage.Fight && boss.Act == ClamAct.Opening) shake = 0.04f;
        if (boss.Stage == BossStage.Fight && boss.Act == ClamAct.Stagger) shake = 0.12f;
        if (boss.Stage == BossStage.Freed && boss.StageTime < PlaneBossTuning.FreedShudder) shake = 0.15f;
        Vector3 jitter = shake > 0f ? new Vector3(Mathf.Sin(_time * 71f), 0f, Mathf.Cos(_time * 57f)) * shake : Vector3.Zero;
        // A squash on landing.
        float sinceLand = _time - _landedAt;
        float squash = sinceLand < 0.35f ? 1f - 0.25f * Mathf.Sin(sinceLand / 0.35f * Mathf.Pi) : 1f;
        // Freed, she leaps aside in one arc.
        if (boss.Stage == BossStage.Freed && boss.Leap is > 0f and < 1f) y += 3.5f * Mathf.Sin(boss.Leap * Mathf.Pi);
        _clam.Position = new Vector3(boss.Position.X, y, boss.Position.Y) + jitter;
        _clam.Scale = new Vector3(1f / Mathf.Sqrt(squash), squash, 1f / Mathf.Sqrt(squash));
        _clam.Rotation = new Vector3(0f, -Mathf.Atan2(boss.Facing.Y, boss.Facing.X), 0f);

        // The shell: open while she fires, opening on the telegraph, slammed shut by the snap.
        float open = 0f;
        if (boss.Stage == BossStage.Fight)
        {
            open = boss.Act switch
            {
                ClamAct.Opening => Mathf.SmoothStep(0f, 1f, boss.ActTime / PlaneBossTuning.OpenTelegraph) * 0.85f,
                ClamAct.Volleys or ClamAct.Helix => 1f,
                _ => 0f,
            };
            if (boss.SnapTimer >= 0f) open = 1f + 0.15f * Mathf.Sin(_time * 40f);
        }
        if (world.Events.Exists(e => e.Type == PlaneEventType.BossSnap)) _snapSlam = 0f;
        _snapSlam = Mathf.MoveToward(_snapSlam, 1f, dt / 0.35f);
        open *= _snapSlam;
        // Freed, she lies wide open (shut only for the shudder and the leap).
        if (boss.Stage == BossStage.Freed) open = boss.Leap >= 1f ? Mathf.MoveToward(_hinge.Rotation.Z / Mathf.DegToRad(OpenDeg), 1f, dt * 1.5f) : 0.2f;
        _hinge.Rotation = new Vector3(0f, 0f, Mathf.DegToRad(OpenDeg) * open);

        // The black pearl: a cold glint stirs deep in it while she is open.
        if (!_burst)
        {
            float glint = boss.Stage == BossStage.Fight && boss.Act is ClamAct.Opening or ClamAct.Volleys or ClamAct.Helix ? 0.35f + 0.15f * Mathf.Sin(_time * 5f) : 0.08f;
            _pearl.SetInstanceShaderParameter("ember", glint);
            _pearl.SetInstanceShaderParameter("fade", 1f);
        }
        // Freed: the pearl bursts into white light, washing the arena, and stays white.
        if (boss.Stage == BossStage.Freed && !_burst)
        {
            _burst = true;
            _pearl.MaterialOverride = _whitePearl;
            _burstLight.LightEnergy = 16f;
            var at = _pearl.GlobalPosition;
            for (int i = 0; i < 160; i++)
                _motes.Emit(at, _motes.RandDir() * _motes.RandRange(3f, 14f) + Vector3.Up * 1.5f, _motes.RandRange(0.8f, 1.8f), _motes.RandRange(0.12f, 0.3f), 0.02f,
                    new Color(1.6f, 1.5f, 1.3f), new Color(1f, 0.7f, 0.3f, 0f), drag: 1.4f, buoyancy: 0.3f);
        }
        if (_burst)
        {
            _burstLight.LightEnergy = Mathf.MoveToward(_burstLight.LightEnergy, 1.2f, dt * 10f);
            _whitePearl.EmissionEnergyMultiplier = 0.6f + 0.15f * Mathf.Sin(_time * 1.6f);
        }
        if (GetViewport()?.GetCamera3D() is { } cam) _motes.Tick(dt, cam.GlobalBasis);
        _mantleMat.AlbedoColor = new Color(0.1f, 0.07f, 0.09f).Lerp(new Color(0.78f, 0.55f, 0.48f), 1f - _drained);

        // Corrupted colours wash back in once she is freed; hits flash her white.
        if (boss.Stage == BossStage.Freed) _drained = Mathf.MoveToward(_drained, 0f, dt / 1.2f);
        foreach (var valve in new[] { _bottom, _top })
        {
            valve.SetInstanceShaderParameter("drained", _drained);
            valve.SetInstanceShaderParameter("flash", boss.HitFlash > 0f ? 0.7f : 0f);
        }
        SyncCrust(world, boss, dt);

        // The landing wave and the sand puff.
        _wave.Visible = boss.Wave >= 0f;
        if (_wave.Visible)
        {
            float w = Mathf.Max(boss.Wave, 0.5f);
            _wave.Position = new Vector3(c.X, LevelMap.SwimBand - 0.3f, c.Y);
            _wave.Scale = new Vector3(w, 0.6f, w);
            _waveMat.AlbedoColor = new Color(0.95f, 0.88f, 0.7f, 0.85f * (1f - boss.Wave / world.ArenaRadius));
        }
        float puff = sinceLand / 0.9f;
        for (int i = 0; i < _puffs.Length; i++)
        {
            _puffs[i].Visible = puff is >= 0f and < 1f;
            if (!_puffs[i].Visible) continue;
            float a = i * Mathf.Tau / _puffs.Length + 0.3f;
            var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            _puffs[i].Position = new Vector3(boss.Position.X, LevelMap.SwimBand - 0.4f, boss.Position.Y) + dir * (R + 3.5f * Mathf.Sqrt(puff));
            _puffs[i].Scale = Vector3.One * (0.8f + 1.4f * puff);
        }
        _puffMat.AlbedoColor = new Color(0.62f, 0.54f, 0.4f, 0.5f * (1f - Mathf.Clamp(puff, 0f, 1f)));

        // The snap's warning rim.
        _snapRim.Visible = boss.SnapTimer >= 0f;
        if (_snapRim.Visible)
        {
            _snapRim.Position = new Vector3(boss.Position.X, LevelMap.SwimBand - 0.5f, boss.Position.Y);
            _snapMat.AlbedoColor = new Color(1f, 0.2f, 0.2f, 0.35f + 0.35f * Mathf.Sin(_time * 30f));
        }

        foreach (var m in _mud) m.SetShaderParameter("amount", boss.Cloud);
        _mudRoot.Visible = boss.Cloud > 0.001f;
    }
}
