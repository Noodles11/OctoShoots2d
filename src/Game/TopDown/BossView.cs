using System;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Queen Clam and her arena, drawn from the sim: her valves (opening on the telegraph, rattling in the stagger, snapping),
/// the glowing heart-pearl, her pearl crown and grumpy eyes; the shadow before she drops, the landing wave and sand puff,
/// the snap's warning rim, and the mud cloud standing around the sealed arena.
/// </summary>
public partial class BossView : Node3D
{
    const float R = PlaneBossTuning.BodyRadius;
    /// <summary>How far she falls from (metres above her seat) when she drops in.</summary>
    const float DropHeight = 16f;
    const float OpenDeg = 52f;

    Node3D _clam = null!, _hinge = null!;
    MeshInstance3D _bottom = null!, _top = null!, _shadow = null!, _wave = null!, _snapRim = null!, _heart = null!;
    readonly MeshInstance3D[] _crown = new MeshInstance3D[5];
    readonly MeshInstance3D[] _puffs = new MeshInstance3D[10];
    StandardMaterial3D _heartMat = null!, _waveMat = null!, _snapMat = null!, _puffMat = null!, _shadowMat = null!;
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

        // The mantle and heart-pearl inside, seen when she opens.
        var mantle = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 1f, BottomRadius = 1f, Height = 0.04f, RadialSegments = 40 },
            Scale = new Vector3(R * 0.94f, 1f, R * 0.82f),
            Position = new Vector3(0f, 0.02f, 0f),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.3f, 0.45f, 0.75f), EmissionEnabled = true, Emission = new Color(0.25f, 0.55f, 0.75f), EmissionEnergyMultiplier = 0.35f, Roughness = 0.3f },
        };
        _clam.AddChild(mantle);
        _heartMat = new StandardMaterial3D { AlbedoColor = new Color(1f, 0.9f, 0.95f), EmissionEnabled = true, Emission = new Color(1f, 0.6f, 0.8f), EmissionEnergyMultiplier = 0.5f, Roughness = 0.1f, Metallic = 0.3f, RimEnabled = true };
        _heart = new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.5f, Height = 1f }, MaterialOverride = _heartMat, Position = new Vector3(R * 0.15f, 0.3f, 0f) };
        _clam.AddChild(_heart);

        _hinge = new Node3D { Position = new Vector3(-R, 0f, 0f) };
        _clam.AddChild(_hinge);
        _top = new MeshInstance3D { Mesh = dome, MaterialOverride = _shell, Scale = new Vector3(R, 0.95f, R * 0.88f), Position = new Vector3(R, 0f, 0f) };
        _hinge.AddChild(_top);
        // Grumpy eyes on the top valve, near the lip, with heavy brows.
        var white = new StandardMaterial3D { AlbedoColor = new Color(0.97f, 0.97f, 0.94f), Roughness = 0.3f };
        var black = new StandardMaterial3D { AlbedoColor = new Color(0.05f, 0.05f, 0.08f) };
        foreach (float side in new[] { -1f, 1f })
        {
            var eye = new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.42f, Height = 0.84f }, MaterialOverride = white, Position = new Vector3(R * 1.55f, 0.62f, side * 0.62f) };
            _hinge.AddChild(eye);
            eye.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.2f, Height = 0.4f }, MaterialOverride = black, Position = new Vector3(0.24f, 0.18f, -side * 0.05f) });
            _hinge.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.18f, 0.12f, 0.7f) },
                MaterialOverride = black,
                Position = new Vector3(R * 1.62f, 1.08f, side * 0.62f),
                RotationDegrees = new Vector3(side * 22f, 0f, 0f),
            });
        }
        // The pearl crown along the hinge.
        var pearl = new StandardMaterial3D { AlbedoColor = new Color(1f, 0.97f, 0.92f), Roughness = 0.12f, Metallic = 0.35f, RimEnabled = true, EmissionEnabled = true, Emission = new Color(0.9f, 0.85f, 1f), EmissionEnergyMultiplier = 0.25f };
        for (int i = 0; i < _crown.Length; i++)
        {
            float z = (i - 2) * 0.55f;
            _crown[i] = new MeshInstance3D { Mesh = new SphereMesh { Radius = i == 2 ? 0.34f : 0.25f, Height = i == 2 ? 0.68f : 0.5f }, MaterialOverride = pearl, Position = new Vector3(0.35f, 0.75f + (i == 2 ? 0.15f : 0f), z) };
            _hinge.AddChild(_crown[i]);
        }

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
    public void Show(PlaneWorld world)
    {
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
        if (boss.Stage == BossStage.Freed) open = 0.45f + 0.05f * Mathf.Sin(_time * 2f);
        _hinge.Rotation = new Vector3(0f, 0f, Mathf.DegToRad(OpenDeg) * open);

        // The heart-pearl glows up on the telegraph and while open.
        float glow = boss.Stage == BossStage.Fight && boss.Act is ClamAct.Opening or ClamAct.Volleys or ClamAct.Helix ? 1.5f + 0.8f * Mathf.Sin(_time * 8f) : 0.4f;
        _heartMat.EmissionEnergyMultiplier = Mathf.MoveToward(_heartMat.EmissionEnergyMultiplier, glow, dt * 6f);

        // Corrupted colours wash back in once she is freed; hits flash her white.
        if (boss.Stage == BossStage.Freed) _drained = Mathf.MoveToward(_drained, 0f, dt / 1.2f);
        foreach (var valve in new[] { _bottom, _top })
        {
            valve.SetInstanceShaderParameter("drained", _drained);
            valve.SetInstanceShaderParameter("flash", boss.HitFlash > 0f ? 0.7f : 0f);
        }
        // Her crown jiggles after a thump.
        float jig = Mathf.Exp(-sinceLand * 3f) * Mathf.Sin(sinceLand * 30f) * 0.12f;
        for (int i = 0; i < _crown.Length; i++)
            _crown[i].Scale = Vector3.One * (1f + jig * (i % 2 == 0 ? 1f : -1f));

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
