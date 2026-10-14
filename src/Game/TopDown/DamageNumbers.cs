using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Floating damage numbers: each hit or heal shows its amount above who took it, rising and fading over about a second.
/// The game has only two forces, so only two colours: light (bright gold) for her hits on anything and for any
/// cleansing or healing of her, and corruption (dark violet ink) for whatever corrupts her. Hers are a little larger.
/// Billboards drawn over everything.
/// </summary>
public partial class DamageNumbers : Node3D
{
    const float Life = 0.9f, Rise = 1.6f, Height = 1.4f;

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
        _live.Add((label, 0f, _rng.RandfRange(-0.5f, 0.5f)));
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
            // A quick pop, then an eased rise and a late fade.
            label.Position += new Vector3(drift, Rise * (1f - t) * 1.8f, 0f) * dt;
            float pop = t < 0.12f ? 1f + 0.5f * (1f - t / 0.12f) : 1f;
            label.Scale = Vector3.One * pop;
            float alpha = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            label.Modulate = label.Modulate with { A = alpha };
            label.OutlineModulate = label.OutlineModulate with { A = 0.92f * alpha };
            _live[i] = (label, age, drift);
        }
    }

    /// <summary>A new room: no numbers carry over.</summary>
    public void Clear()
    {
        foreach (var (label, _, _) in _live) label.QueueFree();
        _live.Clear();
    }
}
