using System;
using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The boss and its arena, drawn from the sim (docs/BOSS-GENERATOR.md §13). The body is a rig built from the boss's
/// spec — a clam, a star or urchin, a jelly or siphonophore, a sea hare or snail — posed every frame; this view keeps
/// what every boss shares: the pearl (black and glinting while corrupted; white once freed, left where it sat), the
/// crust's lumps on its back, the ink drip lifting off its mutation sites (gold in the freeing), its pulse rings, the
/// sacs bursting, the freeing itself (the mutations melt, true colours flood back, the true body — smaller — moves off),
/// and the arena: the shadow before it drops, the landing wave and sand puff and the snap's warning rim.
/// </summary>
public partial class BossView : Node3D
{
    /// <summary>How far it falls from (metres above its seat) when it drops in.</summary>
    const float DropHeight = 16f;

    Node3D _root = null!;
    OctoShoots.Game.TopDown.Bosses.BossRig? _rig;
    MeshInstance3D _shadow = null!, _wave = null!, _snapRim = null!, _pearl = null!;
    readonly MeshInstance3D[] _puffs = new MeshInstance3D[10];
    StandardMaterial3D _waveMat = null!, _snapMat = null!, _puffMat = null!, _shadowMat = null!, _whitePearl = null!;
    ShaderMaterial _blackPearl = null!;
    StandardMaterial3D _ringMat = null!;
    OmniLight3D _burstLight = null!;
    OctoShoots.Game.Fx.FxParticles _motes = null!, _drips = null!;
    readonly List<MeshInstance3D> _rings = new();
    bool _burst;
    float _time, _drained = 1f, _snapSlam = 1f, _landedAt = -10f, _freedAt = -10f, _drip, _surge, _scale = 1f;
    BossStage _lastStage;

    public override void _Ready()
    {
        _root = new Node3D();
        AddChild(_root);
        // The pearl: corruption, glossy and drinking the light, a cold glint deep in it when it can be struck. Her light
        // must strike it. Freed, it is white.
        _blackPearl = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/corruption_orb.gdshader") };
        _blackPearl.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("ember_color"), new Color(0.55f, 0.3f, 0.85f));
        _whitePearl = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.97f, 0.96f, 0.93f), Roughness = 0.08f, Metallic = 0.25f, RimEnabled = true, Rim = 1f,
            EmissionEnabled = true, Emission = new Color(1f, 0.95f, 0.85f), EmissionEnergyMultiplier = 0.6f,
        };
        _pearl = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 32, Rings = 16 },
            MaterialOverride = _blackPearl,
            Scale = Vector3.One * PlaneBossTuning.CoreRadius * 0.55f,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
        };
        _pearl.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("wobble"), 0.05f);
        _pearl.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("ember"), 0.1f);
        AddChild(_pearl);
        _burstLight = new OmniLight3D { LightColor = new Color(1f, 0.93f, 0.78f), LightEnergy = 0f, OmniRange = 30f, ShadowEnabled = false };
        AddChild(_burstLight);
        _motes = new OctoShoots.Game.Fx.FxParticles(600, additive: true);
        AddChild(_motes);
        _drips = new OctoShoots.Game.Fx.FxParticles(400, additive: false);
        AddChild(_drips);
        // The pulse rings: dark ink rings running out over the floor.
        _ringMat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = new Color(0.06f, 0.02f, 0.1f, 0.85f), RimEnabled = false,
        };

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
        _snapRim = new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.96f, OuterRadius = 1f, Rings = 64, RingSegments = 4 }, MaterialOverride = _snapMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_snapRim);

        Visible = false;
    }

    // The corruption's crust (docs/CORRUPTION.md): glossy ink lumps over its back, one fifth of them per layer; each
    // layer cleansed from the arena breaks its lumps off, tumbling away in specks.
    readonly List<(MeshInstance3D Mesh, int Layer)> _crust = new();
    readonly List<(MeshInstance3D Mesh, Vector3 Velocity, float Age)> _falling = new();
    ShaderMaterial? _crustMat;

    void BuildCrust(OctoShoots.Game.TopDown.Bosses.BossRig rig, float radius)
    {
        foreach (var (mesh, _) in _crust)
            if (IsInstanceValid(mesh)) mesh.QueueFree();
        foreach (var (mesh, _, _) in _falling)
            if (IsInstanceValid(mesh)) mesh.QueueFree();
        _crust.Clear();
        _falling.Clear();
        _crustMat ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/corruption_orb.gdshader") };
        var lump = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 14, Rings = 7 };
        var rng = new RandomNumberGenerator { Seed = 7 };
        var parent = rig.CrustParent;
        // Lumps sit on the rig's surface, in its own frame; undo the parent's scale so they stay round.
        var undo = parent.Scale;
        // The crust gathers where the ink is thickest — on the mutation sites and over the pearl — two lumps a layer, so
        // it reads as the corruption's own growth, not a scatter; the stain on the skin deepens with it too.
        var spec = rig.SpecOf;
        var clusters = new List<OctoShoots.Core.Bosses.BodySite>(spec.Sites);
        clusters.Add(spec.Pearl with { Out = Mathf.Max(spec.Pearl.Out, 0.2f) });
        for (int i = 0; i < 10; i++)
        {
            var site = clusters[i % clusters.Count];
            float a = site.Angle + rng.RandfRange(-0.35f, 0.35f), rho = Mathf.Clamp(site.Out * 0.8f + rng.RandfRange(-0.1f, 0.05f), 0.1f, 0.7f);
            var at = parent == rig ? rig.Surface(a, rho) : parent.Transform.AffineInverse() * rig.Surface(a, rho);
            float size = rng.RandfRange(0.2f, 0.32f) * Mathf.Clamp(radius / PlaneBossTuning.BodyRadius, 0.8f, 1.3f);
            var mesh = new MeshInstance3D
            {
                Mesh = lump,
                MaterialOverride = _crustMat,
                Position = at,
                Scale = new Vector3(size / Mathf.Max(undo.X, 0.01f), size * 0.55f / Mathf.Max(undo.Y, 0.01f), size / Mathf.Max(undo.Z, 0.01f)),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
            };
            mesh.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("ember"), 0.06f);
            mesh.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("wobble"), 0.15f);
            mesh.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("seed"), i * 1.3f);
            parent.AddChild(mesh);
            _crust.Add((mesh, i % 5));
        }
    }

    void SyncCrust(PlaneWorld world, PlaneBoss boss, float dt)
    {
        int crust = world.Corruption is null ? 0 : boss.Crust;
        for (int i = _crust.Count - 1; i >= 0; i--)
        {
            var (mesh, layer) = _crust[i];
            if (!IsInstanceValid(mesh))
            {
                _crust.RemoveAt(i);
                continue;
            }
            if (layer < crust) continue;
            // This layer is cleansed away: the lump breaks off and tumbles (and is the falling list's from now on).
            _crust.RemoveAt(i);
            var at = mesh.GlobalTransform;
            mesh.GetParent().RemoveChild(mesh);
            AddChild(mesh);
            mesh.GlobalTransform = at;
            var out_ = (at.Origin - _root.GlobalPosition) with { Y = 0f };
            _falling.Add((mesh, out_.Normalized() * 3f + Vector3.Up * 2.5f, 0f));
        }
        for (int i = _falling.Count - 1; i >= 0; i--)
        {
            var (mesh, v, age) = _falling[i];
            age += dt;
            v += Vector3.Down * 6f * dt;
            mesh.Position += v * dt;
            mesh.Rotation += new Vector3(3f, 2f, 1f) * dt;
            mesh.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("fade"), 1f - age / 0.9f);
            if (age >= 0.9f)
            {
                mesh.QueueFree();
                _falling.RemoveAt(i);
                continue;
            }
            _falling[i] = (mesh, v, age);
        }
    }

    /// <summary>A new level: its boss's body (built from its spec) and the arena round its rift.</summary>
    public void Show(PlaneWorld world)
    {
        if (_rig is not null)
        {
            _rig.QueueFree();
            _rig = null;
        }
        _crust.Clear();
        foreach (var r in _rings) r.QueueFree();
        _rings.Clear();
        if (world.Boss is { } boss)
        {
            _rig = OctoShoots.Game.TopDown.Bosses.Rigs.For(boss.Spec);
            _root.AddChild(_rig);
            _rig.Build(boss.Spec);
            BuildCrust(_rig, boss.Radius);
            _snapRim.Scale = new Vector3(boss.Radius + PlaneBossTuning.SnapReach, 0.05f, boss.Radius + PlaneBossTuning.SnapReach);
            _pearl.Scale = Vector3.One * boss.Spec.PearlRadius * 0.55f;
        }
        _drained = 1f;
        _scale = 1f;
        _landedAt = _freedAt = -10f;
        _burst = false;
        _pearl.MaterialOverride = _blackPearl;
        _pearl.Visible = true;
        _burstLight.LightEnergy = 0f;
        _motes.Clear();
        _drips.Clear();
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
        var rig = _rig;
        if (rig is null) return;
        float seat = LevelMap.SwimBand - 0.55f;
        float R = boss.Radius;
        _root.Visible = boss.Landed || boss.Stage == BossStage.Drop;
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

        // The shakes: a creak on the telegraph, a rattle in the stagger, a shudder when freed.
        float shake = 0f;
        if (boss.Stage == BossStage.Fight && boss.Act == BossAct.Telegraph) shake = 0.04f;
        if (boss.Stage == BossStage.Fight && boss.Act == BossAct.Stagger) shake = 0.12f;
        if (boss.Stage == BossStage.Freed && boss.StageTime < PlaneBossTuning.FreedShudder) shake = 0.15f;
        Vector3 jitter = shake > 0f ? new Vector3(Mathf.Sin(_time * 71f), 0f, Mathf.Cos(_time * 57f)) * shake : Vector3.Zero;
        // A squash on landing.
        float sinceLand = _time - _landedAt;
        float squash = sinceLand < 0.35f ? 1f - 0.25f * Mathf.Sin(sinceLand / 0.35f * Mathf.Pi) : 1f;
        // Freed, a clam leaps aside in one arc.
        if (boss.Stage == BossStage.Freed && boss.Spec.Form == OctoShoots.Core.Bosses.BodyForm.GiantClam && boss.Leap is > 0f and < 1f)
            y += 3.5f * Mathf.Sin(boss.Leap * Mathf.Pi);
        // Freed, the mutations melt away and the body settles to its true size.
        float melt = boss.Stage == BossStage.Freed ? Mathf.Clamp((_time - _freedAt - PlaneBossTuning.FreedShudder) / 1.2f, 0f, 1f) : 0f;
        _scale = Mathf.Lerp(1f, rig.TrueScale, Mathf.SmoothStep(0f, 1f, Mathf.Clamp((_time - _freedAt - 0.6f) / 1.5f, 0f, 1f)) * (boss.Freed ? 1f : 0f));
        _root.Position = new Vector3(boss.Position.X, y, boss.Position.Y) + jitter;
        _root.Scale = new Vector3(_scale / Mathf.Sqrt(squash), _scale * squash, _scale / Mathf.Sqrt(squash));
        _root.Rotation = new Vector3(0f, -Mathf.Atan2(boss.Facing.Y, boss.Facing.X), 0f);

        if (world.Events.Exists(e => e.Type == PlaneEventType.BossSnap)) _snapSlam = 0f;
        _snapSlam = Mathf.MoveToward(_snapSlam, 1f, dt / 0.35f);
        bool fired = world.Events.Exists(e => e.Type == PlaneEventType.BossVolley);
        bool pulsed = world.Events.Exists(e => e.Type == PlaneEventType.BossPulse);
        float speed = boss.Freed ? (boss.Gone ? 0f : PlaneBossTuning.LeaveSpeed) : boss.Spec.Move == OctoShoots.Core.Bosses.BossMove.Creep ? boss.Spec.MoveRate : 0f;
        rig.Pose(new OctoShoots.Game.TopDown.Bosses.RigPose
        {
            Stage = boss.Stage, Act = boss.Act, ActTime = boss.ActTime, Telegraph = boss.Spec.Telegraph,
            Warn = boss.Act == BossAct.Telegraph ? Mathf.Clamp(boss.ActTime / Mathf.Max(boss.Spec.Telegraph, 0.01f), 0f, 1f) : 0f,
            Open = boss.Open, Fired = fired, Pulsed = pulsed, Melt = melt, SnapWarn = boss.SnapTimer >= 0f, SnapSlam = _snapSlam,
            Speed = speed, Time = _time, Dt = dt,
        });
        rig.SyncMutations(boss, melt, _time);
        if (world.Events.Exists(e => e.Type == PlaneEventType.BossMutates)) _surge = 1f;
        _surge = Mathf.MoveToward(_surge, 0f, dt / 1.5f);
        // Corrupted colours wash back in once it is freed; hits flash it white; it fades as it leaves.
        if (boss.Stage == BossStage.Freed) _drained = Mathf.MoveToward(_drained, 0f, dt / 1.2f);
        float fade = boss.Freed && boss.Spec.Form != OctoShoots.Core.Bosses.BodyForm.GiantClam ? 1f - Mathf.Clamp((boss.Leap - 0.6f) / 0.4f, 0f, 1f) : 1f;
        // A hit only brightens it a little (under steady fire the flashes overlap; it must never turn white).
        rig.SetSkin(_drained, boss.HitFlash > 0f ? 0.25f : 0f, _surge + (world.Corruption is null ? 0f : boss.Crust * 0.06f), fade);
        _root.Visible &= !boss.Gone && fade > 0.01f;

        // The pearl: where the sim has it (a moving window's travels round the body), at the rig's height.
        var pearlAt = boss.PearlAt;
        float pearlY = y + rig.PearlHeight * _scale;
        if (!_burst) _pearl.Position = new Vector3(pearlAt.X, pearlY, pearlAt.Y);
        if (!_burst)
        {
            float glint = boss.Stage == BossStage.Fight && boss.Open ? 0.4f + 0.15f * Mathf.Sin(_time * 5f) : boss.Act == BossAct.Telegraph ? 0.25f : 0.06f;
            _pearl.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("ember"), glint);
            _pearl.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("fade"), 1f);
            // A Coax-out pearl is buried until its sacs are burst; then it surfaces.
            float buried = boss.Spec.Exposure == OctoShoots.Core.Bosses.Exposure.CoaxOut && !boss.Open ? 0.35f : 1f;
            _pearl.Scale = Vector3.One * boss.Spec.PearlRadius * 0.55f * buried;
        }
        // Freed: the pearl bursts into white light, washing the arena, and stays white where it sat.
        if (boss.Stage == BossStage.Freed && !_burst)
        {
            _burst = true;
            _pearl.MaterialOverride = _whitePearl;
            _burstLight.Position = _pearl.Position + Vector3.Up * 1.5f;
            _burstLight.LightEnergy = 16f;
            var at = _pearl.GlobalPosition;
            for (int i = 0; i < 160; i++)
                _motes.Emit(at, _motes.RandDir() * _motes.RandRange(3f, 14f) + Vector3.Up * 1.5f, _motes.RandRange(0.8f, 1.8f), _motes.RandRange(0.12f, 0.3f), 0.02f,
                    new Color(1.6f, 1.5f, 1.3f), new Color(1f, 0.7f, 0.3f, 0f), drag: 1.4f, buoyancy: 0.3f);
        }
        // Released, its cleansed pearl is the reward: the world's pearl takes its place where it sat, and it moves away
        // from under it.
        _pearl.Visible = !(boss.Stage == BossStage.Freed && !world.BossBattle);
        if (_burst)
        {
            _burstLight.LightEnergy = Mathf.MoveToward(_burstLight.LightEnergy, 1.2f, dt * 10f);
            _whitePearl.EmissionEnergyMultiplier = 0.6f + 0.15f * Mathf.Sin(_time * 1.6f);
        }

        // The drip: ink welling up off the mutation sites and lifting away; in the freeing, drop by drop, gold.
        if (boss.Landed && melt < 1f)
        {
            _drip += dt * (4f + 10f * boss.Spec.Mutation) * (boss.Freed ? 2f : 1f);
            var sites = rig.Mutations.FindAll(m => m.Node.Visible);
            while (_drip >= 1f && sites.Count > 0)
            {
                _drip -= 1f;
                var node = sites[(int)_motes.RandRange(0f, sites.Count - 0.01f)].Node;
                var from = node.GlobalPosition + _drips.RandDir() * 0.2f;
                bool gold = boss.Freed && _drips.RandRange(0f, 1f) < melt + 0.2f;
                var fx = gold ? _motes : _drips;
                var c0 = gold ? new Color(1.2f, 0.95f, 0.5f) : new Color(0.03f, 0.015f, 0.05f, 0.9f);
                fx.Emit(from, new Vector3(_drips.RandRange(-0.15f, 0.15f), _drips.RandRange(0.3f, 0.8f), _drips.RandRange(-0.15f, 0.15f)), _drips.RandRange(0.9f, 1.6f),
                    _drips.RandRange(0.05f, 0.1f), 0.02f, c0, c0 with { A = 0f }, drag: 0.8f, buoyancy: 0.4f);
            }
        }
        // Burst sacs: a splash of ink.
        foreach (var e in world.Events)
            if (e.Type == PlaneEventType.BossSacBurst)
                _drips.Burst(new Vector3(e.Position.X, y + 0.6f, e.Position.Y), Vector3.Up, 24, 4f, 1f, 0.6f, 0.14f, 0.03f,
                    new Color(0.04f, 0.01f, 0.07f, 0.95f), new Color(0.3f, 0.1f, 0.5f, 0f), drag: 3f);
        if (GetViewport()?.GetCamera3D() is { } cam)
        {
            _motes.Tick(dt, cam.GlobalBasis);
            _drips.Tick(dt, cam.GlobalBasis);
        }

        // Its pulse rings, running out over the floor.
        while (_rings.Count < boss.Rings.Count)
        {
            var ring = new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.94f, OuterRadius = 1f, Rings = 72, RingSegments = 4 }, MaterialOverride = _ringMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            AddChild(ring);
            _rings.Add(ring);
        }
        for (int i = 0; i < _rings.Count; i++)
        {
            _rings[i].Visible = i < boss.Rings.Count;
            if (!_rings[i].Visible) continue;
            float r = boss.Rings[i].Radius;
            _rings[i].Position = new Vector3(boss.Position.X, LevelMap.SwimBand - 0.3f, boss.Position.Y);
            _rings[i].Scale = new Vector3(r, 0.5f, r);
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
    }
}
