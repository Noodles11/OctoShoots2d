using System.Collections.Generic;
using Godot;

namespace OctoShoots.Game.UI;

/// <summary>What the HUD shows this frame.</summary>
public struct HudState
{
    public float Hp;
    public float MaxHp;
    public float Foam;
    public float Dash01;
    public bool OnTarget;
    public float Charge01;
    public bool Shielded;
    public string? ActiveName;
    /// <summary>Seconds built up and seconds needed.</summary>
    public float ActiveCharge;
    public float ActiveMax;
    public int Coins;
    public int Bombs;
    public string Status;
    public string Items;
}

public enum BlipKind { Enemy, EnemyAlert, Pearl, Chest, Pickup, Landmark }

/// <summary>A minimap dot: offset in metres (right, forward) relative to Clementine's facing, height difference, colour hint.</summary>
public readonly record struct Blip(Vector2 Offset, float Dy, BlipKind Kind, Color? Tint = null);

/// <summary>A held pearl's label while reviewing the inventory tentacle.</summary>
public readonly record struct PearlLabel(Vector2 Screen, string Name, bool Selected);

/// <summary>
/// Centre dot (always on, §11), dash cooldown and charge rings, hit marker, health bar with foam
/// and drain trail (2D §23), active item charge, item captions (2D §29), synergy banners, and
/// danger markers at the screen edge for attacks from outside the view (§7.2).
/// </summary>
public partial class Hud : Control
{
    public readonly record struct DangerMarker(Vector2 Direction, float Intensity);

    sealed class Card
    {
        public required string Title;
        public required string Subtitle;
        public required List<string> Lines;
        public required Color Accent;
        public float Time;
        public float Duration;
    }

    const float BarHpPerPixel = 100f / 320f;

    readonly List<DangerMarker> _markers = new();
    readonly List<Blip> _blips = new();
    readonly List<PearlLabel> _pearlLabels = new();
    float _minimapRange = 32f;
    (string Title, string Subtitle, List<string> Lines, int Count, int Shown)? _review;
    readonly Queue<Card> _cards = new();
    HudState _s;
    float _drainHp = 100f, _drainDelay;
    float _hitMarker;
    float _activeFlash;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public void SetState(in HudState s)
    {
        if (s.Hp < _s.Hp) _drainDelay = 0.4f;
        if (s.Hp > _drainHp) _drainHp = s.Hp;
        _s = s;
    }

    public void SetMarkers(IEnumerable<DangerMarker> markers)
    {
        _markers.Clear();
        _markers.AddRange(markers);
    }

    public void SetMinimap(IEnumerable<Blip> blips, float range)
    {
        _blips.Clear();
        _blips.AddRange(blips);
        _minimapRange = range;
    }

    /// <summary>Inventory review: labels next to each held pearl and the selected pearl's description (null hides it).</summary>
    public void SetReview(IEnumerable<PearlLabel>? labels, (string Title, string Subtitle, List<string> Lines, int Count, int Shown)? card)
    {
        _pearlLabels.Clear();
        if (labels is not null) _pearlLabels.AddRange(labels);
        _review = card;
    }

    public void OnHit() => _hitMarker = 0.15f;

    public void OnActiveCharged() => _activeFlash = 1f;

    /// <summary>Item pickup caption: name, tagline and one line per effect.</summary>
    public void ShowItem(string name, string tagline, List<string> lines) =>
        _cards.Enqueue(new Card { Title = name, Subtitle = tagline, Lines = lines, Accent = new Color(1f, 0.8f, 0.5f), Duration = 4.5f });

    /// <summary>Synergy, transformation or notice banner.</summary>
    public void ShowBanner(string title, string subtitle, Color accent, float duration = 3.5f) =>
        _cards.Enqueue(new Card { Title = title, Subtitle = subtitle, Lines = new List<string>(), Accent = accent, Duration = duration });

    public void ClearCards() => _cards.Clear();

    public void Tick(float dt)
    {
        _hitMarker = Mathf.Max(0f, _hitMarker - dt);
        _activeFlash = Mathf.Max(0f, _activeFlash - dt);
        _drainDelay -= dt;
        if (_drainDelay <= 0f) _drainHp = Mathf.MoveToward(_drainHp, _s.Hp, dt * 40f);
        if (_cards.Count > 0)
        {
            var card = _cards.Peek();
            // Hurry when more cards are waiting.
            card.Time += dt * (_cards.Count > 2 ? 2.5f : 1f);
            if (card.Time >= card.Duration) _cards.Dequeue();
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        Vector2 size = GetViewportRect().Size;
        Vector2 c = size / 2f;
        float scale = size.Y / 1080f;
        var font = ThemeDB.FallbackFont;
        int fontSize = Mathf.Max(8, Mathf.RoundToInt(16f * scale));

        // Centre dot.
        Color dot = _s.OnTarget ? new Color(1f, 0.45f, 0.8f) : Colors.White;
        DrawCircle(c, 3.2f * scale, new Color(0, 0, 0, 0.6f));
        DrawCircle(c, 2.2f * scale, dot);

        if (_s.Dash01 < 1f)
            DrawArc(c, 15f * scale, -Mathf.Pi / 2f, -Mathf.Pi / 2f + Mathf.Tau * _s.Dash01, 32, new Color(0.7f, 0.6f, 1f, 0.7f), 2f * scale, true);
        if (_s.Charge01 > 0f)
        {
            Color chargeColor = _s.Charge01 >= 1f ? new Color(1f, 1f, 0.8f) : new Color(1f, 0.85f, 0.4f, 0.85f);
            DrawArc(c, 22f * scale, -Mathf.Pi / 2f, -Mathf.Pi / 2f + Mathf.Tau * _s.Charge01, 48, chargeColor, 3f * scale, true);
        }
        if (_s.Shielded) DrawArc(c, 30f * scale, 0f, Mathf.Tau, 48, new Color(0.5f, 0.95f, 1f, 0.6f), 2f * scale, true);

        if (_hitMarker > 0f)
        {
            float a = _hitMarker / 0.15f;
            float r0 = 7f * scale, r1 = 13f * scale;
            var col = new Color(1f, 1f, 1f, a);
            foreach (var d in new[] { new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(-1, -1) })
            {
                Vector2 n = d.Normalized();
                DrawLine(c + n * r0, c + n * r1, col, 2f * scale, true);
            }
        }

        // Danger markers on an ellipse near the screen edge.
        Vector2 radii = new(size.X * 0.42f, size.Y * 0.40f);
        foreach (var m in _markers)
        {
            Vector2 dir = m.Direction.Normalized();
            Vector2 p = c + new Vector2(dir.X * radii.X, dir.Y * radii.Y);
            Vector2 side = new(-dir.Y, dir.X);
            float s = (16f + 10f * m.Intensity) * scale;
            var points = new[] { p + dir * s, p - dir * s * 0.4f + side * s * 0.8f, p - dir * s * 0.4f - side * s * 0.8f };
            DrawColoredPolygon(points, new Color(1f, 0.25f, 0.3f, 0.45f + 0.55f * m.Intensity));
        }

        DrawHealth(size, scale, font, fontSize);
        DrawActive(size, scale, font, fontSize);

        if (!string.IsNullOrEmpty(_s.Status))
            DrawString(font, new Vector2(20f * scale, 30f * scale), _s.Status, HorizontalAlignment.Left, -1, fontSize, new Color(1, 1, 1, 0.75f));
        if (!string.IsNullOrEmpty(_s.Items))
            DrawString(font, new Vector2(20f * scale, 54f * scale), _s.Items, HorizontalAlignment.Left, size.X * 0.6f, Mathf.Max(8, fontSize - 3), new Color(1f, 0.85f, 0.6f, 0.7f));

        DrawMinimap(size, scale, font);
        if (_review is { } review) DrawReview(review, size, scale, font);
        else if (_cards.Count > 0) DrawCard(_cards.Peek(), size, scale, font);
    }

    /// <summary>Circular top-down map of the area around Clementine, turning with her (forward is up).</summary>
    void DrawMinimap(Vector2 size, float scale, Font font)
    {
        float radius = 110f * scale;
        Vector2 c = new(size.X - radius - 28f * scale, radius + 28f * scale);
        DrawCircle(c, radius + 3f * scale, new Color(0f, 0f, 0f, 0.45f));
        DrawCircle(c, radius, new Color(0.03f, 0.12f, 0.17f, 0.75f));
        DrawArc(c, radius * 0.5f, 0f, Mathf.Tau, 48, new Color(0.5f, 0.85f, 1f, 0.12f), 1f * scale, true);
        DrawArc(c, radius, 0f, Mathf.Tau, 64, new Color(0.5f, 0.85f, 1f, 0.55f), 2f * scale, true);

        float pxPerM = radius / _minimapRange;
        foreach (var b in _blips)
        {
            Vector2 p = new(b.Offset.X, -b.Offset.Y);
            float len = p.Length() * pxPerM;
            bool beyond = len > radius - 4f * scale;
            if (beyond) p = p.Normalized() * (radius - 4f * scale) / pxPerM;
            Vector2 at = c + p * pxPerM;
            // Fainter the further above or below her.
            float alpha = Mathf.Clamp(1f - Mathf.Abs(b.Dy) / 20f, 0.35f, 1f);
            switch (b.Kind)
            {
                case BlipKind.Enemy:
                case BlipKind.EnemyAlert:
                    if (beyond)
                    {
                        // Out of range: an arrow on the rim pointing at it.
                        Vector2 outward = (at - c).Normalized();
                        Vector2 side = new(-outward.Y, outward.X);
                        float s = (b.Kind == BlipKind.EnemyAlert ? 9f : 7f) * scale;
                        DrawColoredPolygon(new[] { at + outward * s * 0.7f, at - outward * s * 0.5f + side * s * 0.7f, at - outward * s * 0.5f - side * s * 0.7f },
                            new Color(1f, b.Kind == BlipKind.EnemyAlert ? 0.9f : 0.25f, 0.25f, 0.95f));
                        break;
                    }
                    float r = (b.Kind == BlipKind.EnemyAlert ? 5f : 4f) * scale;
                    DrawCircle(at, r, new Color(1f, b.Kind == BlipKind.EnemyAlert ? 0.9f : 0.25f, 0.25f, alpha));
                    // Above or below: a little tick.
                    if (Mathf.Abs(b.Dy) > 3f) DrawLine(at, at + new Vector2(0f, b.Dy > 0 ? -7f : 7f) * scale, new Color(1f, 0.4f, 0.4f, alpha), 1.5f * scale);
                    break;
                case BlipKind.Pearl:
                    DrawCircle(at, 4.5f * scale, new Color(b.Tint ?? new Color(1f, 0.95f, 0.8f), alpha));
                    DrawArc(at, 6f * scale, 0f, Mathf.Tau, 16, new Color(1f, 1f, 1f, alpha * 0.8f), 1f * scale, true);
                    break;
                case BlipKind.Chest:
                    DrawRect(new Rect2(at - Vector2.One * 3.5f * scale, Vector2.One * 7f * scale), new Color(0.75f, 0.5f, 0.25f, alpha));
                    break;
                case BlipKind.Pickup:
                    DrawCircle(at, 2f * scale, new Color(1f, 0.85f, 0.4f, alpha * 0.8f));
                    break;
                case BlipKind.Landmark:
                    var tint = b.Tint ?? Colors.White;
                    var d = new[] { at + new Vector2(0f, -6f) * scale, at + new Vector2(5f, 0f) * scale, at + new Vector2(0f, 6f) * scale, at + new Vector2(-5f, 0f) * scale };
                    DrawColoredPolygon(d, new Color(tint, 0.9f));
                    break;
            }
        }

        // Clementine: an arrow pointing up (her facing).
        var arrow = new[] { c + new Vector2(0f, -9f) * scale, c + new Vector2(6f, 7f) * scale, c + new Vector2(0f, 3f) * scale, c + new Vector2(-6f, 7f) * scale };
        DrawColoredPolygon(arrow, new Color(1f, 0.6f, 0.3f));
    }

    /// <summary>Inventory review: each held pearl named beside its sucker, the selected one described.</summary>
    void DrawReview((string Title, string Subtitle, List<string> Lines, int Count, int Shown) review, Vector2 size, float scale, Font font)
    {
        int small = Mathf.Max(8, Mathf.RoundToInt(15f * scale));
        foreach (var label in _pearlLabels)
        {
            Vector2 from = label.Screen + new Vector2(14f, 0f) * scale;
            Vector2 to = from + new Vector2(26f, 0f) * scale;
            var col = label.Selected ? new Color(1f, 0.9f, 0.55f) : new Color(1f, 1f, 1f, 0.7f);
            DrawLine(from, to, col, 1.5f * scale, true);
            DrawString(font, to + new Vector2(6f, 5f) * scale, label.Name, HorizontalAlignment.Left, -1, label.Selected ? small + 3 : small, col);
        }

        float x = size.X * 0.3f, y = size.Y * 0.3f, w = 440f * scale;
        float h = (90f + review.Lines.Count * 24f) * scale;
        DrawRect(new Rect2(x, y, w, h), new Color(0.02f, 0.05f, 0.09f, 0.78f));
        DrawRect(new Rect2(x, y, w, 3f * scale), new Color(1f, 0.85f, 0.5f));
        int titleSize = Mathf.RoundToInt(28f * scale), body = Mathf.RoundToInt(17f * scale);
        float ty = y + 34f * scale;
        DrawString(font, new Vector2(x + 16f * scale, ty), review.Title, HorizontalAlignment.Left, -1, titleSize, new Color(1f, 0.85f, 0.5f));
        ty += 26f * scale;
        DrawString(font, new Vector2(x + 16f * scale, ty), review.Subtitle, HorizontalAlignment.Left, -1, body, new Color(0.85f, 0.9f, 1f, 0.8f));
        foreach (var line in review.Lines)
        {
            ty += 24f * scale;
            DrawString(font, new Vector2(x + 16f * scale, ty), line, HorizontalAlignment.Left, -1, body, Colors.White);
        }
        string footer = review.Count > review.Shown
            ? $"Mouse wheel: next pearl · {review.Shown} of {review.Count} on this tentacle (the rest are held but not drawn)"
            : "Mouse wheel: next pearl · release Tab to swim on";
        DrawString(font, new Vector2(x, y + h + 22f * scale), footer, HorizontalAlignment.Left, -1, Mathf.Max(8, body - 3), new Color(1f, 1f, 1f, 0.6f));
    }

    /// <summary>Health bar: length grows with max HP, foam extends it in pale blue, ticks every 25 HP.</summary>
    void DrawHealth(Vector2 size, float scale, Font font, int fontSize)
    {
        float total = _s.MaxHp + _s.Foam;
        float pxPerHp = scale / BarHpPerPixel;
        Vector2 barPos = new(40f * scale, size.Y - 60f * scale);
        float height = 16f * scale;
        DrawRect(new Rect2(barPos - Vector2.One * 2f * scale, new Vector2(total * pxPerHp, height) + Vector2.One * 4f * scale), new Color(0, 0, 0, 0.55f));
        DrawRect(new Rect2(barPos, new Vector2(Mathf.Min(_drainHp, _s.MaxHp) * pxPerHp, height)), new Color(1f, 0.9f, 0.9f, 0.8f));
        DrawRect(new Rect2(barPos, new Vector2(Mathf.Max(0f, _s.Hp) * pxPerHp, height)), new Color(1f, 0.45f, 0.35f));
        if (_s.Foam > 0f)
            DrawRect(new Rect2(barPos + new Vector2(_s.MaxHp * pxPerHp, 0f), new Vector2(_s.Foam * pxPerHp, height)), new Color(0.75f, 0.92f, 1f));
        for (float t = 25f; t < total; t += 25f)
        {
            float x = barPos.X + t * pxPerHp;
            DrawLine(new Vector2(x, barPos.Y), new Vector2(x, barPos.Y + height), new Color(0, 0, 0, 0.6f), 2f * scale);
        }
        string text = $"{Mathf.CeilToInt(_s.Hp)} / {Mathf.RoundToInt(_s.MaxHp)}" + (_s.Foam > 0f ? $"  +{Mathf.CeilToInt(_s.Foam)} foam" : "");
        text += $"     ◎ {_s.Coins}   ● {_s.Bombs}";
        DrawString(font, barPos + new Vector2(0f, -8f * scale), text, HorizontalAlignment.Left, -1, fontSize, Colors.White);
    }

    void DrawActive(Vector2 size, float scale, Font font, int fontSize)
    {
        if (_s.ActiveName is null) return;
        Vector2 at = new(size.X - 340f * scale, size.Y - 60f * scale);
        bool ready = _s.ActiveCharge >= _s.ActiveMax;
        Color text = ready ? new Color(1f, 0.95f, 0.6f) : new Color(1f, 1f, 1f, 0.7f);
        DrawString(font, at + new Vector2(0f, -8f * scale), $"[F] {_s.ActiveName}" + (ready ? "  READY" : ""), HorizontalAlignment.Left, -1, fontSize, text);
        // A bar filling over the recharge time.
        var bar = new Rect2(at, new Vector2(300f * scale, 16f * scale));
        DrawRect(bar, new Color(0, 0, 0, 0.55f));
        float fill = Mathf.Clamp(_s.ActiveCharge / Mathf.Max(_s.ActiveMax, 0.001f), 0f, 1f);
        DrawRect(new Rect2(bar.Position + Vector2.One * 2f * scale, new Vector2((bar.Size.X - 4f * scale) * fill, bar.Size.Y - 4f * scale)),
            ready ? new Color(1f, 0.9f, 0.4f, 0.6f + 0.4f * _activeFlash) : new Color(0.6f, 0.85f, 1f));
        if (!ready)
            DrawString(font, at + new Vector2(bar.Size.X + 8f * scale, 13f * scale), $"{Mathf.CeilToInt(_s.ActiveMax - _s.ActiveCharge)}s", HorizontalAlignment.Left, -1, fontSize, new Color(1f, 1f, 1f, 0.7f));
    }

    void DrawCard(Card card, Vector2 size, float scale, Font font)
    {
        float fade = Mathf.Min(1f, Mathf.Min(card.Time / 0.2f, (card.Duration - card.Time) / 0.4f));
        int titleSize = Mathf.RoundToInt(34f * scale);
        int bodySize = Mathf.RoundToInt(18f * scale);
        float width = 640f * scale;
        float height = (86f + card.Lines.Count * 24f) * scale;
        var rect = new Rect2(new Vector2((size.X - width) / 2f, size.Y * 0.16f), new Vector2(width, height));
        DrawRect(rect, new Color(0.02f, 0.05f, 0.09f, 0.75f * fade));
        DrawRect(new Rect2(rect.Position, new Vector2(width, 3f * scale)), new Color(card.Accent, fade));

        float y = rect.Position.Y + 40f * scale;
        DrawString(font, new Vector2(rect.Position.X, y), card.Title, HorizontalAlignment.Center, width, titleSize, new Color(card.Accent, fade));
        y += 28f * scale;
        DrawString(font, new Vector2(rect.Position.X, y), card.Subtitle, HorizontalAlignment.Center, width, bodySize, new Color(0.85f, 0.9f, 1f, 0.8f * fade));
        foreach (var line in card.Lines)
        {
            y += 24f * scale;
            DrawString(font, new Vector2(rect.Position.X, y), line, HorizontalAlignment.Center, width, bodySize, new Color(1f, 1f, 1f, fade));
        }
    }
}
