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
/// missed. The shells are drawn in 3D over the HUD's own drawing (a transparent view of their own). A pearl's name
/// and tagline show for a moment when she takes it. Over the shaft, a prompt: Shift to dive, or why she cannot yet.
/// The top of the screen stays clear at rest: a level's title shows there for a moment as she arrives (ShowTitle), the
/// boss's bar while its fight runs, and messages (<see cref="Say"/>) as a banner that slides down from the top edge,
/// floats there (under the title or the boss's bar when one shows) for a while, then eases away up; a queued one
/// follows. The run's clock shows only with its setting on (ShowClock), small and faint at the top left.
/// Everything is laid out on a 1080-line screen and scaled with the window (TopDownMain's HUD root).
/// Her health and her active pearl's charge are not here: they are on her bell (BellView, DESIGN-TOPDOWN §2.5).
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
        float rest = Margin + (_bossShown > 0.01f ? 50f : _titleShown > 0.01f ? 76f : 6f), hidden = rest - 22f;
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
        float drift = (1f - a) * -6f;
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
        PlaceShell(_counterShell, ShellIcon, CounterSize * (1f + 0.3f * k2), CounterLie);
        foreach (var f in _flights)
        {
            float e = FlightEase(f.T);
            float pop = Mathf.Clamp(f.T / 0.1f, 0f, 1f);
            float size = Mathf.Lerp(f.Size, CounterSize * 0.85f, e) * (1f + 0.25f * Mathf.Sin(Mathf.Pi * pop));
            var tumble = new Quaternion(new Vector3(0.3f, 1f, 0.2f).Normalized(), Mathf.Tau * e * f.Side);
            var lie = f.Lie.Slerp(CounterLie, e * e * (3f - 2f * e)) * tumble;
            PlaceShell(f.Mesh, FlightAt(f.From, f.Side, e), size, lie.Normalized());
        }
    }

    /// <summary>The middle of the counter's shell icon.</summary>
    Vector2 ShellIcon => new(Margin + 12f, Size.Y - Margin - 22f);

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

    public override void _Draw()
    {
        var font = ThemeDB.FallbackFont;
        // The message banner first: it slides in from the top edge.
        DrawBanner(font);
        // The bottom-left corner the pearls and shells stack up from.
        var at = new Vector2(Margin, Size.Y - Margin - 4f);

        DrawTitle(font);
        // The run's clock, with its setting on: small and faint, top left.
        if (ShowClock) Outlined(font, new Vector2(Margin, Margin + 16f), Clock(Elapsed), 16, new Color(1f, 1f, 1f, 0.7f));

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

        // Shells, bottom left (her pearls circle her bell instead: PearlOrbitView): brightens and swells with each one
        // collected.
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
            string key = "SHIFT";
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
