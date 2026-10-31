using System;
using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The plane HUD, bottom left: her shells (the pearls she carries circle her bell: PearlOrbitView), counted beside a
/// shell of their own — the same silver nautilus that lies in the reef (NautilusShell), still, its coil to the eye. Each shell she
/// picks up flies itself from where she took it to the counter — the very shell, turned as it lay, shrinking and righting
/// itself as it goes, a wisp of light behind it on a slight arc — and counts in there with a pulse, so a pickup is never
/// missed. The shells are drawn in 3D over the HUD's own drawing (a transparent view of their own). (The pearl she is
/// absorbing shows on its own card, PearlBanner.) Over the shaft, a prompt: the dive's key (or button) to dive, or why
/// she cannot yet.
/// The top of the screen stays clear at rest: a level's title shows there for a moment as she arrives (ShowTitle), the
/// boss's bar while its fight runs, and messages (<see cref="Say"/>) as a banner that slides down from the top edge,
/// floats there (under the title or the boss's bar when one shows) for a while, then eases away up; a queued one
/// follows. The run's clock shows only with its setting on (ShowClock), small and faint at the top left.
/// Everything is laid out on a 1080-line screen and scaled with the window (TopDownMain's HUD root).
/// Her health and her active pearl's charge are not here: they are on her bell (BellView, DESIGN-TOPDOWN §2.5).
/// <para>
/// The adventure (docs/ADVENTURE.md §6) puts her on it, top left: Clementine seen from above — her bell a disc of glass
/// with her four gonads and her glowing rim (hud_bell.gdshader), the active pearl's colours and pattern filling it when
/// she holds one — and her four oral arms hanging under it, one stat each, left to right by urgency: health, inner
/// light, the active pearl's charge, experience. Each arm is as long as its stat is full, over a faint track of its
/// whole length, with a small sign at its foot; a hit frays the lost piece off health's arm. Her shells beside the
/// bell (once she has any; the counter moves there). Top centre, a dot per vent: dark while it pours, gold once sealed.
/// </para>
/// </summary>
public partial class HudView : Control
{
    const float Margin = 18f;

    PlaneRun? _run;
    int _shells;
    /// <summary>1 when a shell has just reached the counter, easing back to 0: the counter brightens and swells.</summary>
    float _pulse;
    /// <summary>1 as a shell lands, easing to 0: a ring of light spreading from the counter.</summary>
    float _ring;
    /// <summary>
    /// Shells on their way to the counter: where each set off (on screen), which way its arc bows, and how far along it is
    /// (0 → 1). The counter shows them only once they land.
    /// </summary>
    readonly List<Flight> _flights = new();

    sealed class Flight
    {
        public Vector2 From;
        public float Side, T;
        /// <summary>Its size on the screen as it set off (pixels across), and how it was turned (seen from the camera).</summary>
        public float Size;
        public Quaternion Lie;
        public MeshInstance3D Mesh = null!;
    }

    /// <summary>The shells' own view over the HUD: transparent, its camera orthographic at one unit a pixel.</summary>
    SubViewport _shellView = null!;
    Camera3D _shellCamera = null!;
    MeshInstance3D _counterShell = null!;
    /// <summary>The counter's shell: its size (pixels across), and how it is turned (its coil to the eye, tipped a little).</summary>
    const float CounterSize = 40f;
    static readonly Quaternion CounterLie = new Basis(Vector3.Right, Mathf.Pi * 0.5f - 0.35f).GetRotationQuaternion();
    /// <summary>A flight's length (s): brief, so it does not get in the way, quick enough to read as a flick.</summary>
    const float FlightTime = 0.55f;
    int _side = 1;

    // ───────────────────────── the adventure's gonad gauge ─────────────────────────

    bool _adventure;
    float _health = 1f, _healthShown = 1f, _xpShare, _charge = 1f, _hurt;
    bool _xpFull, _shellsSeen;
    int _xpLevel = 1;
    ItemDef? _active;
    /// <summary>The level's vents: sealed or not, in order.</summary>
    readonly List<bool> _vents = new();
    float _time;
    /// <summary>Her bell on the HUD (hud_bell.gdshader): its middle and radius; her arms' roots under it, their spacing,
    /// length and width (root → tip); the shell counter's size beside the bell.</summary>
    static readonly Vector2 BellAt = new(Margin + 64f, Margin + 50f);
    const float BellRadius = 42f, ArmTop = 30f, ArmGap = 17f, ArmLength = 150f, ArmRoot = 10f, ArmTip = 6.5f, GaugeShell = 22f;
    ColorRect _bell = null!;
    ShaderMaterial _bellLook = null!;
    float _lastCharge = 1f, _used;
    /// <summary>Health's arm frayed by a hit: the lost piece (as shares of its length) and how long it has been drifting off.</summary>
    readonly List<(float From, float To, float Age)> _frays = new();

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        // The shells' own 3D view, over the HUD's drawing: lit by a bright key from above and a cool fill, so the silver
        // shines the same in every depth.
        var holder = new SubViewportContainer { Stretch = true, MouseFilter = MouseFilterEnum.Ignore };
        holder.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(holder);
        _shellView = new SubViewport { TransparentBg = true, OwnWorld3D = true, Msaa3D = Viewport.Msaa.Msaa4X, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        holder.AddChild(_shellView);
        _shellCamera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Position = new Vector3(0f, 0f, 200f), Near = 1f, Far = 400f, Size = 720f };
        _shellView.AddChild(_shellCamera);
        _shellView.AddChild(new DirectionalLight3D { Rotation = new Vector3(-0.9f, 0.4f, 0f), LightEnergy = 1.4f });
        _shellView.AddChild(new DirectionalLight3D { Rotation = new Vector3(0.5f, -2.4f, 0f), LightEnergy = 0.45f, LightColor = new Color(0.75f, 0.85f, 1f) });
        _shellView.AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.ClearColor,
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.8f, 0.85f, 0.9f),
                AmbientLightEnergy = 0.6f,
            },
        });
        _counterShell = ShellMesh();
        _bellLook = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/hud_bell.gdshader") };
        _bell = new ColorRect { Material = _bellLook, MouseFilter = MouseFilterEnum.Ignore, Visible = false, Position = BellAt - Vector2.One * BellRadius, Size = Vector2.One * BellRadius * 2f };
        AddChild(_bell);
    }

    MeshInstance3D ShellMesh()
    {
        var m = new MeshInstance3D { Mesh = OctoShoots.Game.Fx.NautilusShell.Mesh, MaterialOverride = OctoShoots.Game.Fx.NautilusShell.Material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        _shellView.AddChild(m);
        return m;
    }

    /// <summary>A shell in the shells' view: at this point of the screen, this many pixels across, turned so.</summary>
    void PlaceShell(MeshInstance3D m, Vector2 at, float size, Quaternion lie) =>
        m.Transform = new Transform3D(new Basis(lie).Scaled(Vector3.One * size), new Vector3(at.X - Size.X * 0.5f, Size.Y * 0.5f - at.Y, 0f));

    // ───────────────────────── the message banner ─────────────────────────

    /// <summary>The banner's sliding in, its stay (shorter while another waits) and its easing away (seconds).</summary>
    const float BannerIn = 0.35f, BannerStay = 2.6f, BannerStayQueued = 1.2f, BannerOut = 0.45f;
    readonly Queue<string> _banners = new();
    string? _banner;
    float _bannerAge;

    /// <summary>A message for her: a banner under the clock (queued after the one showing; repeats of it are dropped).</summary>
    public void Say(string text)
    {
        if (text.Length == 0 || text == _banner || _banners.Contains(text)) return;
        if (_banners.Count >= 3) _banners.Dequeue();
        _banners.Enqueue(text);
    }

    public override void _Process(double delta)
    {
        if (_banner is null && _banners.Count > 0)
        {
            _banner = _banners.Dequeue();
            _bannerAge = 0f;
        }
        if (_banner is null) return;
        _bannerAge += (float)delta;
        float stay = _banners.Count > 0 ? BannerStayQueued : BannerStay;
        if (_bannerAge >= BannerIn + stay + BannerOut) _banner = null;
        QueueRedraw();
    }

    /// <summary>The banner: a dark band with a warm line under it, its text centred; it slides from behind the header to under the clock and away up.</summary>
    void DrawBanner(Font font)
    {
        if (_banner is null) return;
        const int fs = 18;
        var ts = font.GetStringSize(_banner, HorizontalAlignment.Left, -1, fs);
        var size = new Vector2(ts.X + 56f, 36f);
        // It drops in a short way to its place (never across the title or the boss's bar).
        float rest = Margin + (_bossShown > 0.01f ? 50f : _titleShown > 0.01f ? 76f : 6f) + (_adventure && _vents.Count > 0 ? 20f : 0f), hidden = rest - 22f;
        float stay = _banners.Count > 0 ? BannerStayQueued : BannerStay;
        float y, a;
        if (_bannerAge < BannerIn)
        {
            float t = _bannerAge / BannerIn, e = 1f - Mathf.Pow(1f - t, 3f);
            y = Mathf.Lerp(hidden, rest, e);
            a = Mathf.Clamp(t * 2.5f, 0f, 1f);
        }
        else if (_bannerAge < BannerIn + stay)
        {
            // Floating: a slow bob.
            y = rest + 1.5f * Mathf.Sin((_bannerAge - BannerIn) * 2.4f);
            a = 1f;
        }
        else
        {
            float t = Mathf.Clamp((_bannerAge - BannerIn - stay) / BannerOut, 0f, 1f), e = t * t * t;
            y = Mathf.Lerp(rest, hidden, e);
            a = 1f - t * t;
        }
        var p = new Vector2((Size.X - size.X) * 0.5f, y);
        DrawRect(new Rect2(p + new Vector2(0f, 3f), size), new Color(0f, 0f, 0f, 0.25f * a));
        DrawRect(new Rect2(p, size), new Color(0.03f, 0.05f, 0.08f, 0.78f * a));
        DrawRect(new Rect2(p + new Vector2(0f, size.Y - 2f), new Vector2(size.X, 2f)), new Color(1f, 0.85f, 0.55f, 0.85f * a));
        DrawString(font, p + new Vector2((size.X - ts.X) * 0.5f, size.Y * 0.5f + ts.Y * 0.32f), _banner, HorizontalAlignment.Left, -1, fs, new Color(1f, 0.95f, 0.86f, a));
    }

    /// <summary>The run's elapsed play time (it stands still while paused).</summary>
    public float Elapsed;
    /// <summary>The clock's setting: shown small at the top left (else only in the pause menu and on the Tab map).</summary>
    public bool ShowClock;

    /// <summary>The run's clock as text (m:ss, or h:mm:ss past an hour).</summary>
    public static string Clock(float seconds)
    {
        var t = TimeSpan.FromSeconds(seconds);
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes}:{t.Seconds:00}";
    }

    // ───────────────────────── the level's title ─────────────────────────

    /// <summary>The title card: its fading in, its stay and its fading out (seconds).</summary>
    const float TitleIn = 0.4f, TitleStay = 2.6f, TitleOut = 0.7f;
    string _title = "", _subtitle = "";
    float _titleAge = 99f, _titleShown;

    /// <summary>She has arrived on a level: its name (the big line) and where it is (the small one) show for a moment.</summary>
    public void ShowTitle(string title, string subtitle)
    {
        _title = title;
        _subtitle = subtitle;
        _titleAge = 0f;
    }

    /// <summary>The title card, top centre: the name large, where it is small above it, both outlined; no box.</summary>
    void DrawTitle(Font font)
    {
        if (_titleShown <= 0.01f) return;
        float a = _titleShown;
        float drift = (1f - a) * -6f + (_adventure && _vents.Count > 0 ? 18f : 0f);
        var ss = font.GetStringSize(_subtitle, HorizontalAlignment.Left, -1, 17);
        Outlined(font, new Vector2((Size.X - ss.X) * 0.5f, Margin + 20f + drift), _subtitle, 17, new Color(1f, 0.9f, 0.75f, 0.85f * a), a);
        var ts = font.GetStringSize(_title, HorizontalAlignment.Left, -1, 34);
        Outlined(font, new Vector2((Size.X - ts.X) * 0.5f, Margin + 58f + drift), _title, 34, new Color(1f, 0.96f, 0.9f, a), a);
    }

    /// <summary>The HUD's one text style: light text with a soft dark outline, readable on bright sand and in the deeps.</summary>
    void Outlined(Font font, Vector2 at, string text, int size, Color color, float alpha = 1f)
    {
        DrawStringOutline(font, at, text, HorizontalAlignment.Left, -1, size, Mathf.Max(4, size / 4), new Color(0f, 0f, 0f, 0.7f * alpha));
        DrawString(font, at, text, HorizontalAlignment.Left, -1, size, color);
    }

    float _bossHp, _bossTrail, _bossShown, _bossFlash, _bossHold;
    string _bossName = "";
    int _bossCrust;
    bool _bossFight;
    string _prompt = "";
    bool _promptReady;
    float _promptShown;

    public void Track(PlaneWorld world, ItemCatalog? catalog, float dt)
    {
        // Queen Clam's bar: shown while the fight runs, fading out once she is freed.
        var boss = world.Boss;
        if (boss is not null) _bossName = boss.Spec.Name.ToUpperInvariant();
        _bossFight = boss is { Stage: BossStage.Fight };
        _bossShown = Mathf.MoveToward(_bossShown, _bossFight ? 1f : 0f, dt * 2f);
        if (boss is not null)
        {
            float hp = boss.Hp / Mathf.Max(boss.MaxHp, 1f);
            // A hit flashes the bar; what it took stays pale for a moment (chip damage adds up where it can be seen),
            // then drains away.
            if (hp < _bossHp - 1e-5f)
            {
                _bossFlash = 1f;
                _bossHold = 0.7f;
            }
            _bossHp = hp;
            _bossHold -= dt;
            _bossTrail = _bossTrail < _bossHp ? _bossHp : _bossHold > 0f ? _bossTrail : Mathf.MoveToward(_bossTrail, _bossHp, dt * 0.25f);
            _bossFlash = Mathf.MoveToward(_bossFlash, 0f, dt * 5f);
            _bossCrust = boss.Crust;
        }
        _run = world.Run;
        _shells = world.Run.Shells;
        _time += dt;
        _adventure = world.Options.Adventure;
        if (_adventure)
        {
            float max = Mathf.Max(world.Run.MaxHp, 1f);
            float health = Mathf.Clamp(world.Player.Hp / max, 0f, 1f);
            if (health < _health - 1e-4f)
            {
                _hurt = 1f;
                // The lost piece frays off her arm.
                _frays.Add((health, _healthShown, 0f));
                _healthShown = health;
            }
            _health = health;
            _healthShown = Mathf.MoveToward(_healthShown, _health, dt * 0.8f);
            _hurt = Mathf.MoveToward(_hurt, 0f, dt * 2.5f);
            _xpFull = world.Run.Points > 0;
            _xpShare = _xpFull ? 1f : Mathf.Clamp(world.Run.Xp / (float)Mathf.Max(world.Run.XpNeeded, 1), 0f, 1f);
            _xpLevel = world.Run.XpLevel;
            _active = world.Run.Active?.Active is not null ? world.Run.Active : null;
            _charge = _active is null ? 0f : world.Run.ActiveCharge;
            if (_active is not null && _lastCharge >= 1f && _charge < 1f) _used = 1f;
            _lastCharge = _active is null ? 1f : _charge;
            _used = Mathf.MoveToward(_used, 0f, dt * 2.5f);
            for (int i = _frays.Count - 1; i >= 0; i--)
            {
                var f = _frays[i];
                f.Age += dt;
                if (f.Age > FrayTime) _frays.RemoveAt(i);
                else _frays[i] = f;
            }
            SyncBell();
            if (_shells > 0) _shellsSeen = true;
            _vents.Clear();
            int ventsSealed = world.Vents.Count - world.VentsOpen;
            for (int i = 0; i < world.Vents.Count; i++) _vents.Add(i < ventsSealed);
        }
        _titleAge += dt;
        _titleShown = _titleAge < TitleIn ? _titleAge / TitleIn : _titleAge < TitleIn + TitleStay ? 1f : Mathf.Max(0f, 1f - (_titleAge - TitleIn - TitleStay) / TitleOut);
        _pulse = Mathf.MoveToward(_pulse, 0f, dt * 2.5f);
        _ring = Mathf.MoveToward(_ring, 0f, dt * 3f);
        for (int i = _flights.Count - 1; i >= 0; i--)
        {
            var f = _flights[i];
            f.T += dt / FlightTime;
            if (f.T >= 1f)
            {
                // Landed: it counts in, with a pulse.
                f.Mesh.QueueFree();
                _flights.RemoveAt(i);
                _pulse = 1f;
                _ring = 1f;
            }
        }
        SyncShells(dt);
        // Over the shaft: how to dive, or why not yet.
        bool over = world.OverShaft && !world.Defeated && !Diving;
        if (over)
        {
            _promptReady = world.CanDive;
            _prompt = world.CanDive ? "dive" : !world.ExitOpen ? "The Crack is sealed" : "Not while fighting";
        }
        _promptShown = Mathf.MoveToward(_promptShown, over ? 1f : 0f, dt * 4f);
        QueueRedraw();
    }

    /// <summary>She is diving: no prompt.</summary>
    public bool Diving { get; set; }

    /// <summary>
    /// A shell was just collected at that point of the screen, this many pixels across, turned so (as the camera sees it;
    /// null: any way): it flies itself to the counter.
    /// </summary>
    public void CollectShell(Vector2 from, float size, Quaternion? lie)
    {
        // Alternate the arcs' bow, so a handful taken at once fans out instead of stacking up.
        var start = lie ?? new Basis(new Vector3(GD.Randf(), GD.Randf(), GD.Randf()).Normalized() + Vector3.Up * 0.01f, GD.Randf() * Mathf.Tau).GetRotationQuaternion();
        _flights.Add(new Flight { From = from, Side = _side * (0.7f + 0.3f * GD.Randf()), Size = Mathf.Max(size, 8f), Lie = start, Mesh = ShellMesh() });
        _side = -_side;
    }

    /// <summary>
    /// The shells' view this frame: the counter's shell, still (swelling with each that lands); each flying shell
    /// on its arc, from its size where it lay to the counter's, righting itself from how it lay to the counter's turn
    /// with a tumble on the way.
    /// </summary>
    void SyncShells(float dt)
    {
        if (_shellView is null) return;
        _shellCamera.Size = Mathf.Max(Size.Y, 1f);
        float k2 = _pulse * _pulse;
        float counter = _adventure ? GaugeShell : CounterSize;
        // The adventure's counter shows only once she has a shell (or one is on its way).
        _counterShell.Visible = !_adventure || _shellsSeen || _flights.Count > 0;
        PlaceShell(_counterShell, ShellIcon, counter * (1f + 0.3f * k2), CounterLie);
        foreach (var f in _flights)
        {
            float e = FlightEase(f.T);
            float pop = Mathf.Clamp(f.T / 0.1f, 0f, 1f);
            float size = Mathf.Lerp(f.Size, (_adventure ? GaugeShell : CounterSize) * 0.85f, e) * (1f + 0.25f * Mathf.Sin(Mathf.Pi * pop));
            var tumble = new Quaternion(new Vector3(0.3f, 1f, 0.2f).Normalized(), Mathf.Tau * e * f.Side);
            var lie = f.Lie.Slerp(CounterLie, e * e * (3f - 2f * e)) * tumble;
            PlaceShell(f.Mesh, FlightAt(f.From, f.Side, e), size, lie.Normalized());
        }
    }

    /// <summary>The middle of the counter's shell icon (in the adventure, beside her bell).</summary>
    Vector2 ShellIcon => _adventure ? BellAt + new Vector2(BellRadius + 26f, -4f) : new(Margin + 12f, Size.Y - Margin - 22f);

    /// <summary>Where a flight is at progress e (0 → 1): a quadratic arc from its start to the counter, bowed to one side.</summary>
    Vector2 FlightAt(Vector2 from, float side, float e)
    {
        var to = ShellIcon;
        var d = to - from;
        var bow = new Vector2(-d.Y, d.X) * 0.22f * side;
        var mid = (from + to) * 0.5f + bow;
        float u = 1f - e;
        return u * u * from + 2f * u * e * mid + e * e * to;
    }

    /// <summary>A flight's progress along its arc: it leaps off, then speeds in to the counter.</summary>
    static float FlightEase(float t) => t * t * (3f - 2f * t) * 0.6f + t * t * 0.4f;

    /// <summary>Her bell on the HUD: the active pearl's colours and pattern filling it (none: clear glass).</summary>
    void SyncBell()
    {
        _bell.Visible = _adventure;
        var look = _active?.Pearl;
        _bellLook.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("has_pearl"), look is not null);
        if (look is { Colors.Count: > 0 })
        {
            Color C(int i) => Color.FromHtml(look.Colors[Mathf.Min(i, look.Colors.Count - 1)]);
            _bellLook.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("c0"), C(0));
            _bellLook.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("c1"), C(1));
            _bellLook.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("c2"), C(2));
            _bellLook.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("icon"), (int)look.Icon);
        }
        _bellLook.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("ready"), _active is not null && _charge >= 1f ? 1f : 0f);
        _bellLook.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("flash"), _used);
        _bellLook.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("points"), _xpFull ? 1f : 0f);
    }

    /// <summary>How long a frayed piece of health's arm takes to drift off and fade.</summary>
    const float FrayTime = 0.7f;

    /// <summary>
    /// Her arms under her bell: health (warm white-gold, ragged and pulsing when low, a hit fraying the lost piece off),
    /// the active pearl's charge (its own colour, breathing when ready; curled up and dim without one), experience (amber,
    /// growing, pulsing with a point to spend; her level at its foot). Her shells beside the bell.
    /// </summary>
    void DrawGauge(Font font)
    {
        float pulse = 0.5f + 0.5f * Mathf.Sin(_time * 4f);
        bool low = _healthShown < 0.25f;
        var health = low ? new Color(1f, 0.88f, 0.62f).Lerp(new Color(1f, 0.42f, 0.58f), 0.4f + 0.4f * pulse) : new Color(1f, 0.88f, 0.62f);
        Arm(0, _healthShown, health, _hurt, low ? 1f : 0f);
        foreach (var (from, to, age) in _frays) Fray(0, from, to, age / FrayTime, health);
        if (_active is not null)
        {
            var pearl = OctoShoots.Game.Fx.PearlMaterials.GlowColor(_active);
            bool ready = _charge >= 1f;
            Arm(1, _charge, ready ? pearl.Lerp(Colors.White, 0.3f * pulse) : pearl * 0.85f, ready ? 0.25f * pulse : 0f, 0f);
        }
        else Curled(1);
        var xp = _xpFull ? new Color(1f, 0.85f, 0.45f).Lerp(Colors.White, 0.4f * pulse) : new Color(1f, 0.72f, 0.3f);
        Arm(2, _xpShare, xp, _xpFull ? 0.3f * pulse : 0f, 0f);

        // The signs at the arms' feet: a heart, a pearl, her level.
        SignHeart(Foot(0), health);
        SignPearl(Foot(1), _active is not null ? OctoShoots.Game.Fx.PearlMaterials.GlowColor(_active) : new Color(0.6f, 0.62f, 0.66f, 0.6f));
        var lv = _xpLevel.ToString();
        var ls = font.GetStringSize(lv, HorizontalAlignment.Left, -1, 15);
        Outlined(font, Foot(2) + new Vector2(-ls.X * 0.5f, 5f), lv, 15, xp);

        // Her shells beside the bell (the shell itself is drawn in 3D over this).
        if (_shellsSeen || _flights.Count > 0)
        {
            float k2 = _pulse * _pulse;
            var at = ShellIcon;
            if (k2 > 0.01f) DrawCircle(at, 14f * (1f + 0.35f * k2), new Color(1f, 0.8f, 0.55f, 0.4f * k2));
            if (_ring > 0.01f) DrawArc(at, 12f * (1.3f + 2.2f * (1f - _ring)), 0f, Mathf.Tau, 40, new Color(1f, 0.92f, 0.7f, 0.8f * _ring * _ring), 2.5f * _ring + 0.5f, true);
            string count = Mathf.Max(_shells - _flights.Count, 0).ToString();
            int fs = (int)(19f * (1f + 0.3f * k2));
            Outlined(font, at + new Vector2(16f, 7f + 2f * k2), count, fs, new Color(1f, 0.9f, 0.8f).Lerp(new Color(1f, 1f, 0.75f), k2));
        }
    }

    /// <summary>Where arm i hangs from (under the bell), and the point of it f (0 root → 1 its full length), swaying a little.</summary>
    static Vector2 ArmRootOf(int i) => BellAt + new Vector2((i - 1f) * ArmGap, ArmTop);

    Vector2 ArmAt(int i, float f) =>
        ArmRootOf(i) + new Vector2(Mathf.Sin(_time * 1.3f + i * 1.9f + f * 3.2f) * 3.2f * f, f * ArmLength);

    Vector2 Foot(int i) => ArmRootOf(i) + new Vector2(0f, ArmLength + 14f);

    static float ArmWidth(float f) => Mathf.Lerp(ArmRoot, ArmTip, f);

    /// <summary>
    /// One arm: its faint track the whole length, then its fill as long as the stat, a lighter core line down it, a
    /// rounded tip; a flash over it (a hit, a stat ready), ragged edges when it is low.
    /// </summary>
    void Arm(int i, float share, Color fill, float flash, float ragged)
    {
        const int N = 30;
        for (int k = 0; k < N; k++)
        {
            float f0 = k / (float)N, f1 = (k + 1f) / N;
            DrawLine(ArmAt(i, f0), ArmAt(i, f1), new Color(0f, 0f, 0f, 0.42f), ArmWidth(f0) + 3f, true);
            DrawLine(ArmAt(i, f0), ArmAt(i, f1), new Color(fill.R * 0.3f, fill.G * 0.3f, fill.B * 0.35f, 0.45f), ArmWidth(f0), true);
        }
        share = Mathf.Clamp(share, 0f, 1f);
        if (share > 0.004f)
        {
            int n = Mathf.Max(2, (int)(N * share));
            for (int k = 0; k < n; k++)
            {
                float f0 = share * k / n, f1 = share * (k + 1f) / n;
                float jag = ragged > 0f ? 1f + 0.35f * Mathf.Sin(f0 * 90f + _time * 20f) * ragged : 1f;
                DrawLine(ArmAt(i, f0), ArmAt(i, f1), fill, ArmWidth(f0) * jag, true);
                DrawLine(ArmAt(i, f0), ArmAt(i, f1), fill.Lerp(Colors.White, 0.55f) with { A = fill.A * 0.8f }, 2f, true);
            }
            DrawCircle(ArmAt(i, share), ArmWidth(share) * 0.5f, fill);
        }
        if (flash > 0.01f)
            for (int k = 0; k < N; k++)
            {
                float f0 = k / (float)N, f1 = (k + 1f) / N;
                DrawLine(ArmAt(i, f0), ArmAt(i, f1), new Color(1f, 0.85f, 1f, 0.55f * flash), ArmWidth(f0) + 4f, true);
            }
    }

    /// <summary>A piece of an arm frayed off by a hit: it drifts out sideways, breaking into flecks, and fades.</summary>
    void Fray(int i, float from, float to, float k, Color fill)
    {
        if (to <= from) return;
        float fade = 1f - k;
        var off = new Vector2(10f + 26f * k, 6f * k);
        const int n = 8;
        for (int j = 0; j < n; j++)
        {
            float f0 = Mathf.Lerp(from, to, j / (float)n), f1 = Mathf.Lerp(from, to, (j + 0.7f) / n);
            float scatter = (j % 2 == 0 ? 1f : -0.6f) * 10f * k;
            var a = ArmAt(i, f0) + off + new Vector2(scatter, 0f);
            var b = ArmAt(i, f1) + off + new Vector2(scatter, 0f);
            DrawLine(a, b, fill with { A = 0.85f * fade }, ArmWidth(f0) * (1f - 0.6f * k), true);
        }
    }

    /// <summary>No active pearl: its arm curled up short under the bell, dim.</summary>
    void Curled(int i)
    {
        var root = ArmRootOf(i);
        var col = new Color(0.6f, 0.62f, 0.66f, 0.45f);
        DrawLine(root, root + new Vector2(0f, 12f), col, ArmRoot * 0.8f, true);
        DrawArc(root + new Vector2(5f, 14f), 5f, Mathf.Pi * 0.2f, Mathf.Pi * 1.4f, 12, col, ArmTip * 0.8f, true);
        // Its track, very faint, so the place reads.
        for (int k = 0; k < 20; k++)
            DrawLine(ArmAt(i, k / 20f), ArmAt(i, (k + 1f) / 20f), new Color(0f, 0f, 0f, 0.18f), ArmWidth(k / 20f), true);
    }

    void SignHeart(Vector2 c, Color col)
    {
        DrawCircle(c + new Vector2(-2.6f, -1.5f), 3.2f, new Color(0f, 0f, 0f, 0.5f));
        DrawCircle(c + new Vector2(2.6f, -1.5f), 3.2f, new Color(0f, 0f, 0f, 0.5f));
        DrawCircle(c + new Vector2(-2.6f, -1.5f), 2.5f, col);
        DrawCircle(c + new Vector2(2.6f, -1.5f), 2.5f, col);
        DrawColoredPolygon(new[] { c + new Vector2(-5f, -0.5f), c + new Vector2(5f, -0.5f), c + new Vector2(0f, 5.5f) }, col);
    }

    void SignPearl(Vector2 c, Color col)
    {
        DrawCircle(c, 5.5f, new Color(0f, 0f, 0f, 0.5f));
        DrawCircle(c, 4.5f, col);
        DrawCircle(c + new Vector2(-1.4f, -1.4f), 1.4f, new Color(1f, 1f, 1f, 0.8f));
    }

    /// <summary>
    /// The vents, top centre: a dot each, dark with a violet glint while it pours, gold once sealed, filling from the left.
    /// The ones she must seal to go on are full size, the ones she may leave smaller.
    /// </summary>
    void DrawVents()
    {
        if (_vents.Count == 0) return;
        const float gap = 22f, big = 6.5f, small = 4.5f;
        float x0 = Size.X * 0.5f - (_vents.Count - 1) * gap * 0.5f;
        for (int i = 0; i < _vents.Count; i++)
        {
            var at = new Vector2(x0 + i * gap, Margin + 6f);
            float r = i < OctoShoots.Core.Plane.AdventureTuning.VentsToClear ? big : small;
            DrawCircle(at, r + 2.5f, new Color(0f, 0f, 0f, 0.5f));
            if (_vents[i])
            {
                DrawCircle(at, r, new Color(1f, 0.82f, 0.42f));
                DrawCircle(at + new Vector2(-1.5f, -1.5f), r * 0.35f, new Color(1f, 1f, 0.9f, 0.9f));
            }
            else
            {
                DrawCircle(at, r, new Color(0.06f, 0.02f, 0.1f));
                DrawArc(at, r - 1f, 0f, Mathf.Tau, 20, new Color(0.55f, 0.25f, 0.95f, 0.6f + 0.3f * Mathf.Sin(_time * 3f + i)), 1.6f, true);
            }
        }
    }

    public override void _Draw()
    {
        var font = ThemeDB.FallbackFont;
        // The message banner first: it slides in from the top edge.
        DrawBanner(font);
        // The bottom-left corner the pearls and shells stack up from.
        var at = new Vector2(Margin, Size.Y - Margin - 4f);

        DrawTitle(font);
        // The run's clock, with its setting on: small and faint, top left.
        if (ShowClock) Outlined(font, _adventure ? new Vector2(Margin, BellAt.Y + ArmTop + ArmLength + 46f) : new Vector2(Margin, Margin + 16f), Clock(Elapsed), 16, new Color(1f, 1f, 1f, 0.7f));
        if (_adventure) DrawVents();

        // The boss's health, top centre.
        if (_bossShown > 0.01f)
        {
            const float bw = 380f, bh = 12f;
            var b = new Vector2((Size.X - bw) * 0.5f, Margin + 26f);
            float a = _bossShown;
            string name = _bossName;
            var ns = font.GetStringSize(name, HorizontalAlignment.Left, -1, 16);
            Outlined(font, new Vector2((Size.X - ns.X) * 0.5f, b.Y - 6f), name, 16, new Color(1f, 0.93f, 0.85f, a), a);
            DrawRect(new Rect2(b - Vector2.One * 3f, new Vector2(bw + 6f, bh + 6f)), new Color(0f, 0f, 0f, 0.55f * a));
            DrawRect(new Rect2(b, new Vector2(bw, bh)), new Color(0.08f, 0.1f, 0.14f, 0.9f * a));
            DrawRect(new Rect2(b, new Vector2(bw * Mathf.Clamp(_bossTrail, 0f, 1f), bh)), new Color(1f, 0.9f, 0.7f, 0.8f * a));
            DrawRect(new Rect2(b, new Vector2(bw * Mathf.Clamp(_bossHp, 0f, 1f), bh)), new Color(0.35f, 0.8f, 0.78f, a));
            // Her crust (each layer soaks up a sixth of every hit until the arena is cleansed): an ink film over the bar,
            // and a pip per layer inside its right end.
            if (_bossCrust > 0)
            {
                DrawRect(new Rect2(b, new Vector2(bw * Mathf.Clamp(_bossHp, 0f, 1f), bh)), new Color(0.3f, 0.12f, 0.5f, 0.09f * _bossCrust * a));
                for (int i = 0; i < _bossCrust; i++)
                {
                    var pip = b + new Vector2(bw - 9f - i * 12f, bh * 0.5f);
                    DrawCircle(pip, 5f, new Color(0.05f, 0.02f, 0.09f, 0.95f * a));
                    DrawCircle(pip, 3.2f, new Color(0.55f, 0.3f, 0.85f, a));
                }
            }
            DrawRect(new Rect2(b, new Vector2(bw * Mathf.Clamp(_bossHp, 0f, 1f), bh)), new Color(1f, 1f, 1f, 0.55f * _bossFlash * a));
            DrawRect(new Rect2(b, new Vector2(bw, bh * 0.35f)), new Color(1f, 1f, 1f, 0.12f * a));
            // The phase mark at half.
            DrawRect(new Rect2(b + new Vector2(bw * 0.5f - 1f, -2f), new Vector2(2f, bh + 4f)), new Color(0f, 0f, 0f, 0.7f * a));
        }

        if (_adventure) DrawGauge(font);
        // Shells, bottom left (her pearls circle her bell instead: PearlOrbitView): brightens and swells with each one
        // collected.
        else
        {
            float k2 = _pulse * _pulse;
            var c = ShellIcon;
            float size = 14f * (1f + 0.35f * k2);
            // (The shell itself is drawn over this, in 3D: SyncShells.)
            if (k2 > 0.01f) DrawCircle(c, size * 1.5f, new Color(1f, 0.8f, 0.55f, 0.4f * k2));
            int fontSize = (int)(26f * (1f + 0.3f * k2));
            var col = new Color(1f, 0.9f, 0.8f).Lerp(new Color(1f, 1f, 0.75f), k2);
            if (_ring > 0.01f) DrawArc(c, size * (1.3f + 2.2f * (1f - _ring)), 0f, Mathf.Tau, 40, new Color(1f, 0.92f, 0.7f, 0.8f * _ring * _ring), 2.5f * _ring + 0.5f, true);
            // Shells still in flight are not counted yet.
            int shown = Mathf.Max(_shells - _flights.Count, 0);
            Outlined(font, c + new Vector2(26f, 8f + 3f * k2), shown.ToString(), fontSize, col);
        }

        // Shells in flight: a wisp of warm light with a short tail behind each (the shell itself is drawn over it, in 3D).
        foreach (var flight in _flights)
        {
            var (from, side, t) = (flight.From, flight.Side, flight.T);
            float e = FlightEase(t);
            const int tail = 16;
            for (int j = tail; j >= 1; j--)
            {
                float back = FlightEase(Mathf.Max(t - j * 0.02f, 0f));
                float fade = 1f - j / (float)(tail + 1);
                var pt = FlightAt(from, side, back);
                // A soft shade under the gold, so the wisp reads on bright sand as well as in the dark.
                DrawCircle(pt, 3f + 7f * fade, new Color(0.25f, 0.12f, 0f, 0.12f * fade));
                DrawCircle(pt, 2f + 5.5f * fade, new Color(1f, 0.72f, 0.25f, 0.65f * fade * fade));
            }
        }

        // The dive prompt, low in the middle: a key cap and a word.
        if (_promptShown > 0.01f)
        {
            float a = _promptShown;
            const int fs = 18;
            // The key cap: whatever dive is bound to on the device in use (RB on a controller, SHIFT on the keyboard…).
            string key = OctoShoots.Game.Controls.InputSetup.Hint(OctoShoots.Game.Controls.InputSetup.Dive);
            var ks = font.GetStringSize(key, HorizontalAlignment.Left, -1, 14);
            var ws = font.GetStringSize(_prompt, HorizontalAlignment.Left, -1, fs);
            float w = (_promptReady ? ks.X + 22f : 0f) + ws.X + 28f;
            var p = new Vector2((Size.X - w) * 0.5f, Size.Y * 0.7f);
            DrawRect(new Rect2(p, new Vector2(w, 34f)), new Color(0.02f, 0.05f, 0.08f, 0.6f * a));
            float x = p.X + 14f;
            if (_promptReady)
            {
                var cap = new Rect2(new Vector2(x - 4f, p.Y + 7f), new Vector2(ks.X + 8f, 20f));
                DrawRect(cap, new Color(1f, 0.93f, 0.82f, 0.9f * a));
                DrawString(font, new Vector2(x, p.Y + 22f), key, HorizontalAlignment.Left, -1, 14, new Color(0.15f, 0.1f, 0.08f, a));
                x += ks.X + 22f;
            }
            var col = _promptReady ? new Color(1f, 0.93f, 0.8f, a) : new Color(0.85f, 0.85f, 0.9f, 0.75f * a);
            DrawString(font, new Vector2(x, p.Y + 24f), _prompt, HorizontalAlignment.Left, -1, fs, col);
        }
    }
}
