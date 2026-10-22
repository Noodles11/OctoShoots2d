using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Floating damage numbers: each hit or heal shows its amount above who took it, rising and fading over about a second.
/// The game has only two forces, so only two colours: light (bright gold) for her hits on anything and for any
/// cleansing or healing of her, and corruption (dark violet ink) for whatever corrupts her. Hers are a little larger.
/// Billboards drawn over everything, never over one another: a new number starts just above any it would cover, and
/// the live ones are kept apart as they rise (laid out across the camera's view, so they read apart on screen).
/// </summary>
public partial class DamageNumbers : Node3D
{
    const float Life = 0.9f, Rise = 1.5f, Height = 1.4f, Pop = 1.3f;

    /// <summary>Light: her shots, and her corruption cleansed. A bright gold on a dark outline.</summary>
    static readonly Color Light = new(1f, 0.9f, 0.5f);
    static readonly Color LightOutline = new(0.12f, 0.06f, 0.02f, 0.95f);
    /// <summary>Corruption: dark violet ink, edged with a pale violet glow so it reads on the dark sea.</summary>
    static readonly Color Ink = new(0.22f, 0.06f, 0.38f);
    static readonly Color InkOutline = new(0.78f, 0.6f, 1f, 0.9f);

    readonly List<(Label3D Label, float Age, float Drift)> _live = new();
    readonly RandomNumberGenerator _rng = new();

    /// <summary>One number per damage event this step (PlayerHit, MobHit, MobDefeated, BossHit).</summary>
    public void Show(in PlaneEvent e)
    {
        if (e.Type is not (PlaneEventType.PlayerHit or PlaneEventType.PlayerDrained or PlaneEventType.MobHit or PlaneEventType.MobDefeated or PlaneEventType.BossHit)) return;
        // A drain cut short (a tick or two as she leaves the dark) would read as a hit of 0.
        if (e.Type == PlaneEventType.PlayerDrained && e.Size < 0.5f) return;
        float lift = e.Type == PlaneEventType.BossHit ? Height * 2f : Height;
        Show(e.Position, -e.Size, hers: e.Type is PlaneEventType.PlayerHit or PlaneEventType.PlayerDrained, lift);
    }

    /// <summary>Corruption cleansed (any source: a spring, Whale Song, a healing pearl, the shop): a light −number above Clementine.</summary>
    public void ShowHeal(System.Numerics.Vector2 at, float amount) => Show(at, amount, hers: true, Height);

    /// <summary>A signed amount: negative is damage, positive healing.</summary>
    void Show(System.Numerics.Vector2 at, float amount, bool hers, float lift)
    {
        float size = Mathf.Abs(amount);
        if (size <= 0f) return;
        // Only a hit on her is corruption; everything else is light.
        bool corrupts = hers && amount < 0f;
        string number = size >= 1f ? Mathf.RoundToInt(size).ToString() : size.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        var label = new Label3D
        {
            // No sign: the colour says what it is, light (gold) or corruption (violet ink).
            Text = number,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            PixelSize = 0.022f,
            FontSize = hers ? 37 : 31,
            OutlineSize = 11,
            OutlineModulate = corrupts ? InkOutline : LightOutline,
            Modulate = corrupts ? Ink : Light,
            NoDepthTest = true,
            RenderPriority = 4,
            OutlineRenderPriority = 3,
            Position = new Vector3(at.X, LevelMap.SwimBand + lift, at.Y),
        };
        AddChild(label);
        // Above any live number it would cover (it rises faster than they do, being younger, so it stays clear).
        if (View() is { } view)
            for (int tries = 0; tries < 12 && Covering(label, view) is { } over; tries++)
                label.Position += view.Up * (Top(over, view) + Gap - Bottom(label, view));
        _live.Add((label, 0f, _rng.RandfRange(-0.5f, 0.5f)));
    }

    /// <summary>Space kept between two numbers (metres, across the view).</summary>
    const float Gap = 0.08f;

    /// <summary>The camera's right and up (numbers are laid out across its view), or null with no camera.</summary>
    (Vector3 Right, Vector3 Up)? View() => GetViewport().GetCamera3D() is { } cam ? (cam.GlobalBasis.X, cam.GlobalBasis.Y) : null;

    /// <summary>A number's half size across the view (metres): its text at its pixel size, room left for its pop.</summary>
    static Vector2 Extent(Label3D label)
    {
        float h = label.FontSize * label.PixelSize * Pop;
        return new Vector2(h * 0.32f * label.Text.Length + h * 0.15f, h * 0.42f);
    }

    static Vector2 Across(Label3D label, (Vector3 Right, Vector3 Up) view) => new(label.Position.Dot(view.Right), label.Position.Dot(view.Up));
    static float Top(Label3D label, (Vector3 Right, Vector3 Up) view) => Across(label, view).Y + Extent(label).Y;
    static float Bottom(Label3D label, (Vector3 Right, Vector3 Up) view) => Across(label, view).Y - Extent(label).Y;

    static bool Overlap(Label3D a, Label3D b, (Vector3 Right, Vector3 Up) view)
    {
        Vector2 d = Across(a, view) - Across(b, view), e = Extent(a) + Extent(b) + new Vector2(Gap, Gap);
        return Mathf.Abs(d.X) < e.X && Mathf.Abs(d.Y) < e.Y;
    }

    /// <summary>The highest live number this one covers, or null.</summary>
    Label3D? Covering(Label3D label, (Vector3 Right, Vector3 Up) view)
    {
        Label3D? over = null;
        foreach (var (other, _, _) in _live)
            if (Overlap(label, other, view) && (over is null || Top(other, view) > Top(over, view))) over = other;
        return over;
    }

    /// <summary>Rises and fades the live numbers; frozen while paused (not called).</summary>
    public void Tick(float dt)
    {
        for (int i = _live.Count - 1; i >= 0; i--)
        {
            var (label, age, drift) = _live[i];
            age += dt;
            if (age >= Life)
            {
                label.QueueFree();
                _live.RemoveAt(i);
                continue;
            }
            float t = age / Life;
            // A quick pop, then a steady rise (all at one speed, so none catches up with the one above it) and a late fade.
            label.Position += new Vector3(drift * (1f - t), Rise, 0f) * dt;
            float pop = t < 0.12f ? 1f + (Pop - 1f) * (1f - t / 0.12f) : 1f;
            label.Scale = Vector3.One * pop;
            float alpha = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            label.Modulate = label.Modulate with { A = alpha };
            label.OutlineModulate = label.OutlineModulate with { A = 0.92f * alpha };
            _live[i] = (label, age, drift);
        }
        // Kept apart: where two still cover each other (a pop, a drift), the younger is eased up over the older.
        if (View() is not { } view) return;
        for (int i = 0; i < _live.Count; i++)
        for (int j = i + 1; j < _live.Count; j++)
        {
            Label3D older = _live[i].Label, younger = _live[j].Label;
            if (!Overlap(older, younger, view)) continue;
            float need = Top(older, view) + Gap - Bottom(younger, view);
            if (need > 0f) younger.Position += view.Up * need * Mathf.Min(1f, dt * 20f);
        }
    }

    /// <summary>A new room: no numbers carry over.</summary>
    public void Clear()
    {
        foreach (var (label, _, _) in _live) label.QueueFree();
        _live.Clear();
    }
}
