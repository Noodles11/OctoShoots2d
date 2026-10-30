using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The corruption's creatures and works on screen (docs/CORRUPTION.md), presentation only: Blightroots (a pole of ink
/// with a pulsing head, swaying like a boxing pole and hammering her, BlightView.Octopus.cs; its ring of ink), murklings
/// (liquid drops of ink with a stolen ember, budding from the floor, BlightView.Murklings.cs) and their throws. Everything
/// corrupted shares one look: ink-black, light-drinking, slightly glossy, with a cold violet rim. Every one of them,
/// freed, pops like a water balloon: the ink it was made of showers out glowing in its colour and evaporates
/// (InkVapour.Pop), never a flash of light.
/// </summary>
public partial class BlightView : Node3D
{
    static readonly Color Gold = new(1.25f, 0.85f, 0.38f);
    static readonly Color Ink = new(0.02f, 0.012f, 0.035f);
    /// <summary>A Blightroot's head: raised high on its pole over her plane (it stands tall, as kelp does).</summary>
    const float SacHeight = LevelMap.SwimBand + 1.9f;

    sealed class Ripple
    {
        public MeshInstance3D Mesh = null!;
        public float Age, Life, From, To;
        public Color Tint;
    }

    readonly List<Root> _roots = new();
    readonly List<MeshInstance3D> _murks = new(), _throws = new();
    readonly List<Ripple> _ripples = new();
    ShaderMaterial _orb = null!, _sacMat = null!, _ring = null!;
    /// <summary>The swells in the water (WaterSwells): a Blightroot's head coming down shoves the water out in rings.</summary>
    public OctoShoots.Game.Fx.WaterSwells? Swells { get; set; }
    readonly SphereMesh _sphere = new() { Radius = 1f, Height = 2f, RadialSegments = 24, Rings = 12 };
    /// <summary>A finer sphere for a spiked murkling (its spikes need the vertices).</summary>
    readonly SphereMesh _spikySphere = new() { Radius = 1f, Height = 2f, RadialSegments = 64, Rings = 32 };
    readonly QuadMesh _quad = new() { Size = new Vector2(2f, 2f), Orientation = PlaneMesh.OrientationEnum.Y };
    FxParticles _motes = null!, _droplets = null!;
    LevelMap? _map;
    PlaneWorld? _lastWorld;
    /// <summary>The ink's evaporation (shared with the floor's): a freed creature pops into it.</summary>
    public InkVapour? Vapour;

    /// <summary>A freed creature pops: its ink showers out, glowing in its colour, and evaporates.</summary>
    void Pop(Vector3 at, float size, Color glow) => Vapour?.Pop(at, size, glow);
    float _time;

    public override void _Ready()
    {
        _orb = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/corruption_orb.gdshader") };
        _sacMat = new ShaderMaterial { Shader = _orb.Shader };
        _sacMat.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("ember_color"), new Color(0.9f, 0.25f, 0.55f));
        _ring = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/ink_ring.gdshader"), RenderPriority = 1 };
        _motes = new FxParticles(768, additive: true);
        AddChild(_motes);
        _droplets = new FxParticles(512, additive: false);
        AddChild(_droplets);
    }

    float Floor(System.Numerics.Vector2 p) => _map is null ? -1f : Mathf.Min(_map.HeightAt(p), LevelMap.BlockHeight);

    /// <summary>A new level: its Blightroots.</summary>
    public void Show(PlaneWorld world)
    {
        _lastWorld = world;
        _map = world.Map;
        foreach (var r in _roots) r.Node.QueueFree();
        foreach (var r in _ripples) r.Mesh.QueueFree();
        _roots.Clear();
        _ripples.Clear();
        ClearHermits();
        ClearBombs();
        ClearThumps();
        ClearWardens();
        ClearLashjaws();
        ClearFlounders();
        ClearMurkBodies();
        _motes.Clear();
        _droplets.Clear();
        foreach (var root in world.Blightroots) _roots.Add(MakeRoot(root));
    }

    public void Sync(PlaneWorld world, float dt, Camera3D? camera)
    {
        _time += dt;
        UnFlinch();
        SyncRoots(world, dt);
        SyncMurklings(world, dt);
        SyncHermits(world, dt);
        SyncRunaways(world, dt, camera);
        SyncBombs(world);
        SyncThumps(world, dt);
        _lastWorld = world;
        SyncWardens(world, dt, camera);
        SyncLashjaws(world, dt, camera);
        SyncFlounders(world, dt);
        SyncKinLights(world);
        SyncOutlines(world);
        // (Her light's hit outline, SyncHitGlows, is off for now.)
        StepRipples(dt);
        StepGathers(dt);
        StepChunks(dt);
        ForgetBodies();
        FlinchAll(world);
        if (camera is not null)
        {
            _motes.Tick(dt, camera.GlobalBasis);
            _droplets.Tick(dt, camera.GlobalBasis);
        }
    }

    void SyncMurklings(PlaneWorld world, float dt)
    {
        while (_murks.Count < world.Murklings.Count)
        {
            var m = new MeshInstance3D { Mesh = _sphere, MaterialOverride = _orb, CastShadow = GeometryInstance3D.ShadowCastingSetting.On };
            AddChild(m);
            _murks.Add(m);
        }
        for (int i = 0; i < _murks.Count; i++)
        {
            var view = _murks[i];
            view.Visible = i < world.Murklings.Count;
            if (!view.Visible) continue;
            var m = world.Murklings[i];
            // Its own liquid body (BlightView.Murklings): its material carries its shape and its holes.
            var body = BodyOf(m);
            view.MaterialOverride = body.Material;
            StepBody(m, body, dt);
            float y = LevelMap.SwimBand + 0.08f * Mathf.Sin(_time * 2.3f + m.Seed);
            float scale = m.Radius;
            // A lesser one carries a dim ember in its fish's old colour, so it shows what it was.
            float ember = m.Lesser ? 0.7f : m.Prime ? 0.75f : 0.45f;
            view.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("ember_tint"), m.Fish is { } fish ? FishEmber(fish.Kind) : m.Prime ? Kin.Tint(Kin.PrimeCore) : new Color(0f, 0f, 0f, 0f));
            if (m.Budding)
            {
                // Budding: a blob swelling low over the floor as the ink is sucked into it (StartGather's dots, on the
                // same clock); once it is all in, it rises to her plane.
                float k = 1f - m.Bud / m.BudTotal;
                float grow = BlobGrowth(k);
                float rise = Mathf.Clamp((k - GatherIn) / (1f - GatherIn), 0f, 1f);
                float e = rise * rise * (3f - 2f * rise);
                y = Mathf.Lerp(BlobHeight(Floor(m.Position), m.Radius, k), y, e);
                scale *= grow;
                ember = 0.05f + 0.4f * e;
            }
            else if (m.Clinging)
            {
                // Clinging: it drinks her light, its stolen ember brightening.
                scale *= 0.75f;
                ember = 0.9f + 0.3f * Mathf.Sin(_time * 9f + m.Seed);
            }
            // Throwing: the slingshot bend back, the snap forward with the piece swelling out of its front, and once it
            // has flown, a wobble back to round.
            float pull = 0f, bud = 0f;
            if (!m.Budding && !m.Clinging)
            {
                if (m.Windup > 0f)
                {
                    float draw = Mathf.Max(m.WindupTotal - BlightTuning.SnapSeconds, 1e-3f);
                    if (m.Windup > BlightTuning.SnapSeconds)
                    {
                        float k = Mathf.Clamp((m.WindupTotal - m.Windup) / draw, 0f, 1f);
                        pull = -(1f - (1f - k) * (1f - k));
                        bud = 0.25f * k;
                    }
                    else
                    {
                        float s = 1f - m.Windup / BlightTuning.SnapSeconds;
                        pull = Mathf.Lerp(-1f, 1f, s * s);
                        bud = 0.25f + 0.75f * s;
                    }
                }
                else if (m.ThrowAge < 0.7f)
                {
                    float t = m.ThrowAge;
                    pull = Mathf.Exp(-7f * t) * Mathf.Cos(24f * t);
                    bud = 0.6f * (1f - Mathf.SmoothStep(0f, 0.08f, t));
                }
            }
            view.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("aim"), new Vector3(m.Aim.X, 0f, m.Aim.Y));
            // The adventure's kinds: a spiked one (a blue tang), a tadpole (a clownfish) with its tail out behind.
            bool adventure = world.Options.Adventure && m.Fish is not null;
            bool spiked = adventure && m.Fish!.Kind == ReefFishKind.BlueTang, tadpole = adventure && m.Fish!.Kind == ReefFishKind.Clownfish;
            view.Mesh = spiked ? _spikySphere : _sphere;
            view.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("spikes"), spiked ? 1f : 0f);
            view.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("tadpole"), tadpole && !m.Clinging ? 1f : 0f);
            if (m.Velocity.LengthSquared() > 0.04f)
                view.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("swim"), new Vector3(m.Velocity.X, 0f, m.Velocity.Y).Normalized());
            view.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("pull"), pull);
            view.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("bud"), bud);
            view.Position = new Vector3(m.Position.X, y, m.Position.Y);
            // Drawing the bend is an effort: the whole body trembles across its aim.
            if (pull < 0f) view.Position += new Vector3(-m.Aim.Y, 0f, m.Aim.X) * Mathf.Sin(_time * 55f + m.Seed) * 0.035f * -pull;
            view.Scale = Vector3.One * scale;
            view.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("seed"), m.Seed);
            view.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("ember"), ember);
            view.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("fade"), 1f);
        }

        while (_throws.Count < world.MurkShots.Count)
        {
            var t = new MeshInstance3D { Mesh = _sphere, MaterialOverride = _orb, CastShadow = GeometryInstance3D.ShadowCastingSetting.On };
            t.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("ember"), 0f);
            t.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("wobble"), 0.5f);
            // It flies along its local −Z (Basis.LookingAt): its neck trails behind.
            t.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("aim"), new Vector3(0f, 0f, -1f));
            AddChild(t);
            _throws.Add(t);
        }
        for (int i = 0; i < _throws.Count; i++)
        {
            var view = _throws[i];
            view.Visible = i < world.MurkShots.Count;
            if (!view.Visible) continue;
            var s = world.MurkShots[i];
            // Flown its range, it drops to the sand, falling faster as it goes.
            float drop = s.Airborne ? 0f : Mathf.Clamp(s.Fall / BlightTuning.ThrowFallSeconds, 0f, 1f);
            // A Hearthguard's drop leaves its flank on the shelf below and arcs up to her plane over its first metre.
            float rise = s.Lobbed ? 1f - Mathf.Clamp(System.Numerics.Vector2.Distance(s.Position, s.From) / 1f, 0f, 1f) : 0f;
            float flying = Mathf.Lerp(LevelMap.SwimBand, FlounderBed(s.From) + 0.1f, rise * rise);
            float y = Mathf.Lerp(flying, Floor(s.Position) + s.Radius * 0.5f, drop * drop);
            view.Position = new Vector3(s.Position.X, y, s.Position.Y);
            var along = new Vector3(s.Velocity.X, -drop * 6f + rise * 5f, s.Velocity.Y);
            // Stretched along its flight, a smear of dark.
            view.Basis = along.LengthSquared() > 1e-4f ? Basis.LookingAt(along.Normalized(), Vector3.Up).Scaled(new Vector3(1f, 1f, 1.8f)) : Basis.Identity;
            // Just thrown, it pinches off from the murkling's body on a neck that thins and snaps, then wobbles into its
            // flying drop.
            float age = BlightTuning.ThrowLife - s.Life;
            view.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("drip"), 1f - Mathf.SmoothStep(0f, 0.12f, age));
            // Pinched off, it rings as a drop does, and keeps a slow wobble as it flies.
            float jiggle = 1f + 0.3f * Mathf.Exp(-9f * age) * Mathf.Sin(30f * age) + 0.07f * Mathf.Sin(14f * age + i * 1.3f);
            view.Scale = new Vector3(s.Radius * 1.2f / Mathf.Sqrt(jiggle), s.Radius * 1.2f / Mathf.Sqrt(jiggle), s.Radius * 2.2f * jiggle);
            // An Inkwarden's spike: a thin dark spine along its flight, no drip.
            if (s.Spike)
            {
                view.Scale = new Vector3(s.Radius * 0.7f, s.Radius * 0.7f, s.Radius * 4.5f);
                view.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("drip"), 0f);
                continue;
            }
            if (((int)(_time * 60f) + i) % 3 == 0)
                _droplets.Emit(view.Position, Vector3.Up * 0.2f, 0.35f, 0.16f, 0.04f, new Color(0.03f, 0.01f, 0.06f, 0.7f), new Color(0.05f, 0.02f, 0.1f, 0f), drag: 1f);
        }
    }

    /// <summary>The vines: tubes from the floor at their root rising to her plane, tapering to the tip; a coil wraps her.</summary>
    // ───────────────────────── her light's outline ─────────────────────────

    /// <summary>How far her light has worn a creature down (0 while its corruption is full, or once freed → 1).</summary>
    static float Worn(CorruptionMeter meter) => meter.Shown ? 1f - meter.Fraction : 0f;

    /// <summary>
    /// No meters (docs/CORRUPTION.md): her light's outline stays round every corrupted creature whose corruption is not
    /// full, the thicker the less it has left (corruption_orb's worn).
    /// </summary>
    void SyncOutlines(PlaneWorld world)
    {
        for (int i = 0; i < _murks.Count && i < world.Murklings.Count; i++) Outline(_murks[i], Worn(world.Murklings[i].Corruption));
        // A freed octopus swims off healthy: no outline.
        for (int i = 0; i < _roots.Count && i < world.Blightroots.Count; i++)
            Outline(_roots[i].Node, world.Blightroots[i].Alive ? Worn(world.Blightroots[i].Corruption) : 0f);
        foreach (var (b, h) in _hermits) Outline(h.Node, Worn(b.Corruption));
        foreach (var (t, tube) in _tubes) Outline(tube.Node, Worn(t.Corruption));
        foreach (var (w, view) in _wardens) Outline(view.Node, Worn(w.Corruption));
        foreach (var (l, jaw) in _jaws) Outline(jaw.Node, Worn(l.Corruption));
    }

    void Outline(Node node, float k) => SetOrb(node, WornParam, WornMeta, k);

    static readonly StringName WornParam = "worn", WornMeta = "worn_outline";

    // ───────────────────────── a hit's give ─────────────────────────

    /// <summary>
    /// Her light strikes a corrupted body (the way it went, from her): a murkling's liquid body dents where it struck and
    /// rings back, a Blightroot's head the same and pushed back on its pole — the harder, the more of its meter the hit took.
    /// </summary>
    public void Struck(PlaneWorld world, System.Numerics.Vector2 at, System.Numerics.Vector2 dir)
    {
        var back = new Vector3(-dir.X, 0f, -dir.Y).Normalized();
        static float Share(CorruptionMeter meter) => Mathf.Clamp(meter.LastHit / Mathf.Max(meter.Max, 1f), 0.05f, 1f);
        foreach (var mk in world.Murklings)
            if (mk.Alive && System.Numerics.Vector2.Distance(mk.Position, at) < 2f)
            {
                Dent(BodyOf(mk), back, Share(mk.Corruption));
                return;
            }
        foreach (var root in world.Blightroots)
            if (root.Alive && System.Numerics.Vector2.Distance(root.Position, at) < 1.6f)
            {
                DentRoot(root, back, Share(root.Corruption));
                return;
            }
    }

    /// <summary>A drop of the ink: dark violet with the sheen of the creatures' own rims (it reads on dark ground too), thinning to their glow.</summary>
    static readonly Color InkDrop = new(0.2f, 0.1f, 0.34f, 1f), InkDropFade = new(0.5f, 0.32f, 0.85f, 0f);

    // ───────────────────────── events ─────────────────────────

    /// <summary>The war's moments: a bud's ripple, a murkling bursting (and its ember blooming), a sac drinking a bubble.</summary>
    public void OnEvent(in PlaneEvent e)
    {
        var at = new Vector3(e.Position.X, LevelMap.SwimBand, e.Position.Y);
        switch (e.Type)
        {
            case PlaneEventType.BombThrown:
                // A gob of ink spat up from its mouth.
                _droplets.Burst(at + Vector3.Up * 1.2f, (Vector3.Up + new Vector3(e.Direction.X, 0f, e.Direction.Y).Normalized() * 0.4f).Normalized(), 10, 3f, 0.5f, 0.5f, 0.12f, 0.03f, new Color(0.03f, 0.01f, 0.05f, 0.9f), new Color(0.2f, 0.06f, 0.35f, 0f), drag: 2f);
                break;
            case PlaneEventType.BombSplash:
            {
                // It lands with a splash: ink flung up and out, and a dark ring spreading over the ground.
                float floor = Floor(e.Position);
                var from = new Vector3(e.Position.X, floor + 0.15f, e.Position.Y);
                AddRipple(e.Position, floor + 0.07f, 0.3f, e.Size * 1.2f, 0.6f, new Color(Ink.R, Ink.G, Ink.B, 0.9f));
                _droplets.Burst(from, Vector3.Up, 34, 5f, 0.85f, 0.7f, 0.2f, 0.04f, new Color(0.03f, 0.01f, 0.05f, 0.95f), new Color(0.2f, 0.06f, 0.35f, 0f), drag: 2.5f);
                break;
            }
            case PlaneEventType.MurkShotLanded:
            {
                // A throw that fell: it splats on the sand, a small dark ring spreading where it stains.
                float floor = Floor(e.Position);
                var from = new Vector3(e.Position.X, floor + 0.08f, e.Position.Y);
                AddRipple(e.Position, floor + 0.06f, 0.15f, e.Size * 1.3f, 0.45f, new Color(Ink.R, Ink.G, Ink.B, 0.85f));
                _droplets.Burst(from, Vector3.Up, 12, 2.2f, 0.9f, 0.45f, 0.09f, 0.02f, new Color(0.03f, 0.01f, 0.05f, 0.95f), new Color(0.2f, 0.06f, 0.35f, 0f), drag: 3f);
                break;
            }
            case PlaneEventType.FishStruck:
            case PlaneEventType.MurkShotBurst:
            {
                // Struck in the air, it bursts like a water balloon: a quick spray of ink droplets flung out all round
                // (mostly on along its flight), a puff of violet mist, and nothing left behind.
                var on = new Vector3(e.Direction.X, 0f, e.Direction.Y);
                _droplets.Burst(at, Vector3.Up, 16, 3.6f, 1f, 0.32f, 0.08f, 0.015f, new Color(0.04f, 0.01f, 0.07f, 0.95f), new Color(0.3f, 0.1f, 0.5f, 0f), drag: 4f);
                if (on.LengthSquared() > 1e-4f)
                    _droplets.Burst(at, on.Normalized(), 8, 3f, 0.6f, 0.3f, 0.07f, 0.015f, new Color(0.04f, 0.01f, 0.07f, 0.95f), new Color(0.3f, 0.1f, 0.5f, 0f), drag: 4f);
                _droplets.Burst(at, Vector3.Up, 5, 0.6f, 1f, 0.5f, 0.28f, 0.45f, new Color(0.25f, 0.1f, 0.4f, 0.35f), new Color(0.35f, 0.15f, 0.55f, 0f), drag: 2f);
                break;
            }
            case PlaneEventType.GuardiansWoke:
                // Struck, it calls its guardians: a dark blue ripple runs out through its chains over the floor.
                AddRipple(e.Position, Floor(e.Position) + 0.07f, 0.6f, BlightTuning.GuardFar, 0.8f, new Color(Kin.Blightroot.R * 0.4f, Kin.Blightroot.G * 0.4f, Kin.Blightroot.B * 0.7f, 0.9f));
                break;
            case PlaneEventType.BlotterBorn:
                // The ink it is made of gathers in as it rises.
                if (e.Direction.X > 0f)
                {
                    var bornAt = e.Position;
                    StartGather(e.Position, e.Direction.X, e.Direction.Y, e.Size, true,
                        _lastWorld?.Blotters.Find(b => b.Alive && b.Rise > 0f && b.Position == bornAt)?.Draw, BlotterTuning.Radius);
                }
                AddRipple(e.Position, Floor(e.Position) + 0.06f, 0.4f, 4f, e.Size, new Color(Ink.R, Ink.G, Ink.B, 0.95f));
                _droplets.Burst(new Vector3(e.Position.X, Floor(e.Position) + 0.2f, e.Position.Y), Vector3.Up, 30, 3.5f, 0.6f, 0.8f, 0.2f, 0.05f, new Color(0.02f, 0.01f, 0.04f, 0.95f), new Color(0.2f, 0.06f, 0.35f, 0f), drag: 2f);
                break;
            case PlaneEventType.BlotterBurst:
                // Cleansed: its shell pops, and the bare crab runs.
                FreeHermit(e.Position);
                break;
            case PlaneEventType.FishTurned:
                // The ink closes over it: a puff of violet mist.
                _droplets.Burst(at, Vector3.Up, 6, 0.5f, 0.9f, 0.45f, 0.18f, 0.3f, new Color(0.25f, 0.1f, 0.4f, 0.4f), new Color(0.35f, 0.15f, 0.55f, 0f), drag: 2f);
                break;
            case PlaneEventType.FishFreed:
            {
                // A fish again: its ink cracks off in flakes, and a little burst of its own colour and light.
                var tint = FishEmber(e.Size < 0.2f ? ReefFishKind.Chromis : e.Size < 0.28f ? ReefFishKind.Clownfish : ReefFishKind.BlueTang);
                _droplets.Burst(at, Vector3.Up, 8, 2f, 0.7f, 0.35f, 0.06f, 0.015f, new Color(0.03f, 0.01f, 0.05f, 0.9f), new Color(0.2f, 0.06f, 0.35f, 0f), drag: 4f);
                for (int i = 0; i < 12; i++)
                    _motes.Emit(at, _motes.RandDir() * _motes.RandRange(0.5f, 1.8f) + Vector3.Up * 0.6f, _motes.RandRange(0.4f, 0.8f), _motes.RandRange(0.05f, 0.11f), 0.02f, new Color(tint.R, tint.G, tint.B, 1f), new Color(tint.R, tint.G, tint.B, 0f), drag: 2f, buoyancy: 0.5f);
                break;
            }
            case PlaneEventType.LashCatch:
                // The tongue strikes home: a splash of ink where it caught.
                _droplets.Burst(new Vector3(e.Position.X, LevelMap.SwimBand + 0.2f, e.Position.Y), Vector3.Up, 12, 2.5f, 1f, 0.4f, 0.1f, 0.02f, new Color(0.05f, 0.02f, 0.09f, 0.95f), new Color(0.3f, 0.1f, 0.5f, 0f), drag: 3f);
                break;
            case PlaneEventType.LashBite:
            {
                // The jaws snap shut on her: ink and her light flung out.
                var mouth = new Vector3(e.Position.X, LevelMap.SwimBand + 0.2f, e.Position.Y);
                _droplets.Burst(mouth, new Vector3(e.Direction.X, 0.4f, e.Direction.Y).Normalized(), 26, 4f, 0.8f, 0.5f, 0.14f, 0.03f, new Color(0.03f, 0.01f, 0.06f, 0.95f), new Color(0.2f, 0.06f, 0.35f, 0f), drag: 3f);
                break;
            }
            case PlaneEventType.FlounderBite:
                // The needle strikes home: ink and her light flung out along it.
                _droplets.Burst(new Vector3(e.Position.X, LevelMap.SwimBand + 0.1f, e.Position.Y), new Vector3(e.Direction.X, 0.3f, e.Direction.Y).Normalized(), 18, 3.5f, 0.7f, 0.45f, 0.1f, 0.02f, new Color(0.04f, 0.02f, 0.07f, 0.95f), new Color(0.3f, 0.1f, 0.5f, 0f), drag: 3f);
                break;
            case PlaneEventType.LashStun:
                // Its meter broken the first time: it reels, sparks of her light in its head.
                for (int i = 0; i < 24; i++)
                    _motes.Emit(at + Vector3.Up * 0.3f + _motes.RandDir() * 0.3f, _motes.RandDir() * _motes.RandRange(0.6f, 2f) + Vector3.Up * 0.6f, _motes.RandRange(0.5f, 1f), _motes.RandRange(0.06f, 0.14f), 0.02f, Gold, new Color(1f, 0.6f, 0.2f, 0f), drag: 2f, buoyancy: 0.4f);
                break;
            case PlaneEventType.LashFreed:
                // Freed: it pops, its ink showering out crimson and evaporating.
                Pop(at, 1f, Kin.Lashjaw);
                break;
            case PlaneEventType.Warded:
                if (_lastWorld is not null) OnWarded(_lastWorld, e);
                break;
            case PlaneEventType.WardenFreed:
                // Freed: its ink pops off it, showering out violet and evaporating, and a healthy anemone is left.
                Pop(at, 0.8f, Kin.Inkwarden);
                // (The adventure's Inkwarden was a pufferfish: it swims off as one, PufferlingView.)
                if (_lastWorld?.Options.Adventure != true) LeaveFreedAnemone(e.Position);
                break;
            case PlaneEventType.ThumpLeap:
                // It springs off: a puff of ink where it stood.
                _droplets.Burst(new Vector3(e.Position.X, TubeBase(e.Position) + 0.2f, e.Position.Y), Vector3.Up, 14, 2.5f, 0.9f, 0.45f, 0.14f, 0.03f, new Color(0.02f, 0.01f, 0.04f, 0.9f), new Color(0.2f, 0.06f, 0.35f, 0f), drag: 3f);
                break;
            case PlaneEventType.ThumpLand:
            {
                // It lands: ink flung out and a dark ring running out over the floor (the knockback wave).
                float floor = Floor(e.Position);
                AddRipple(e.Position, floor + 0.08f, 0.4f, e.Size, e.Size / ThumpTuning.RingSpeed + 0.2f, new Color(0.08f, 0.22f, 0.02f, 0.95f));
                AddRipple(e.Position, LevelMap.SwimBand - 0.2f, 0.4f, e.Size, e.Size / ThumpTuning.RingSpeed + 0.1f, new Color(Kin.Thumpsquirt.R, Kin.Thumpsquirt.G, Kin.Thumpsquirt.B, 0.6f));
                _droplets.Burst(new Vector3(e.Position.X, TubeBase(e.Position) + 0.15f, e.Position.Y), Vector3.Up, 30, 4.5f, 0.75f, 0.6f, 0.18f, 0.04f, new Color(0.02f, 0.01f, 0.04f, 0.95f), new Color(0.2f, 0.06f, 0.35f, 0f), drag: 2.5f);
                break;
            }
            case PlaneEventType.ThumpBurst:
            {
                // Freed: its ink pops off it, showering out green and evaporating, and a pale sea squirt is left settled there.
                Pop(at, 0.7f, Kin.Thumpsquirt);
                LeaveFreedSquirt(e.Position);
                break;
            }
            case PlaneEventType.MurklingBud:
            {
                // The ink it drinks is sucked into it from the very cells it drinks (a prime's bud is the long one).
                bool prime = e.Size > BlightTuning.BudSeconds + 0.1f;
                var budAt = e.Position;
                var bud = _lastWorld?.Murklings.Find(m => m.Alive && m.Budding && m.Position == budAt);
                StartGather(e.Position, e.Direction.X, e.Direction.Y, e.Size, prime, bud?.Draw,
                    BlightTuning.MurkRadius * (prime ? BlightTuning.PrimeScale : 1f));
                break;
            }
            case PlaneEventType.MurklingBurst:
                // Freed: it pops like a water balloon, its ink showering out in its core's colour and evaporating (a
                // prime's a great shower, Drippus Prime's the greatest: Direction.Y its size). Where its ember blooms,
                // the floor's own cleansing shows it.
                if (e.Direction.X > 0.5f) Pop(at, e.Direction.Y > 0f ? e.Direction.Y : BlightTuning.MurkRadius * BlightTuning.PrimeScale, Kin.PrimeCore);
                else Pop(at, BlightTuning.MurkRadius, Kin.MurklingCore);
                break;
        }
    }

    /// <summary>A reef fish's colour, for the ember a lesser murkling carries and the burst when it is freed.</summary>
    static Color FishEmber(ReefFishKind kind) => kind switch
    {
        ReefFishKind.Chromis => new Color(0.82f, 0.88f, 1f, 1f),
        ReefFishKind.Clownfish => new Color(1f, 0.5f, 0.15f, 1f),
        _ => new Color(0.3f, 0.55f, 1f, 1f),
    };

    void AddRipple(System.Numerics.Vector2 at, float y, float from, float to, float life, Color tint)
    {
        var mesh = new MeshInstance3D { Mesh = _quad, MaterialOverride = _ring, Position = new Vector3(at.X, y, at.Y), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(mesh);
        _ripples.Add(new Ripple { Mesh = mesh, Life = life, From = from, To = to, Tint = tint });
    }

    void StepRipples(float dt)
    {
        for (int i = _ripples.Count - 1; i >= 0; i--)
        {
            var r = _ripples[i];
            r.Age += dt;
            float k = r.Age / r.Life;
            if (k >= 1f)
            {
                r.Mesh.QueueFree();
                _ripples.RemoveAt(i);
                continue;
            }
            float e = 1f - (1f - k) * (1f - k);
            float radius = Mathf.Lerp(r.From, r.To, e);
            r.Mesh.Scale = Vector3.One * (radius / 0.8f);
            r.Mesh.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("tint"), new Color(r.Tint.R, r.Tint.G, r.Tint.B, r.Tint.A * (1f - k)));
            r.Mesh.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("width"), 0.1f + 0.25f / Mathf.Max(radius, 0.2f));
        }
    }

    /// <summary>How long the outline her light's hit draws round a creature lasts (seconds).</summary>
    const float HitGlowSeconds = 0.2f;

    /// <summary>The hit outline now: full as her light strikes, fading over HitGlowSeconds (none before any hit, nor once freed: an emptied meter stops counting).</summary>
    static float HitGlow(CorruptionMeter meter) =>
        !meter.Empty && meter.LastHit > 0f && meter.Since < HitGlowSeconds ? 1f - meter.Since / HitGlowSeconds : 0f;

    /// <summary>Every corrupted creature struck by her light lights a short outline round its body (corruption_orb's hit). Off for now.</summary>
    void SyncHitGlows(PlaneWorld world)
    {
        for (int i = 0; i < _murks.Count && i < world.Murklings.Count; i++) Glow(_murks[i], HitGlow(world.Murklings[i].Corruption));
        for (int i = 0; i < _roots.Count && i < world.Blightroots.Count; i++) Glow(_roots[i].Node, HitGlow(world.Blightroots[i].Corruption));
        foreach (var (b, h) in _hermits) Glow(h.Node, HitGlow(b.Corruption));
        foreach (var (t, tube) in _tubes) Glow(tube.Node, HitGlow(t.Corruption));
        foreach (var (w, view) in _wardens) Glow(view.Node, HitGlow(w.Corruption));
        foreach (var (l, jaw) in _jaws) Glow(jaw.Node, HitGlow(l.Corruption));
    }

    /// <summary>Sets the hit outline on a creature's corrupted parts (those drawn with corruption_orb), only when it changes.</summary>
    void Glow(Node node, float k) => SetOrb(node, HitParam, HitMeta, k);

    /// <summary>Sets one of corruption_orb's instance values on every part of a creature drawn with it, only when it changes.</summary>
    void SetOrb(Node node, StringName param, StringName meta, float k)
    {
        if (node is GeometryInstance3D g && g.MaterialOverride is ShaderMaterial m && (m.Shader == _orb.Shader || m.Shader == _murkShader || m.Shader == _murkOverShader))
        {
            if (!g.HasMeta(meta) || (float)g.GetMeta(meta) != k)
            {
                g.SetMeta(meta, k);
                g.SetInstanceShaderParameter(param, k);
            }
        }
        foreach (var child in node.GetChildren()) SetOrb(child, param, meta, k);
    }

    static readonly StringName HitParam = "hit", HitMeta = "hit_glow";
}
