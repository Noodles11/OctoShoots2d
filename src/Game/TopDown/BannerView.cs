using Godot;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The boss title card, comic-cover style: "CLEMENTINE VS QUEEN CLAM" on a tilted panel. It punches in when the arena
/// seals, stands for BannerSeconds, then dissolves over DissolveSeconds (driven by the boss's stage clock).
/// </summary>
public partial class BannerView : Control
{
    ShaderMaterial _dissolve = null!;
    float _in, _progress;
    string _boss = PlaneBossTuning.Name.ToUpperInvariant();

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _dissolve = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/banner_dissolve.gdshader") };
        Material = _dissolve;
        Visible = false;
    }

    public void Sync(PlaneBoss? boss)
    {
        bool show = boss is { Stage: BossStage.Banner } && boss.StageTime < PlaneBossTuning.BannerSeconds + PlaneBossTuning.DissolveSeconds;
        Visible = show;
        if (!show) return;
        float t = boss!.StageTime;
        _in = Mathf.Clamp(t / 0.25f, 0f, 1f);
        _progress = Mathf.Clamp((t - PlaneBossTuning.BannerSeconds) / PlaneBossTuning.DissolveSeconds, 0f, 1f);
        _dissolve.SetShaderParameter("progress", _progress);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var font = ThemeDB.FallbackFont;
        var c = Size * 0.5f + new Vector2(0f, -Size.Y * 0.12f);
        // Punch in: from big and transparent to its size, with a little overshoot.
        float k = _in;
        float scale = 1f + 0.35f * (1f - k) * (1f - k) - 0.05f * Mathf.Sin(k * Mathf.Pi);
        float alpha = k;
        DrawSetTransform(c, Mathf.DegToRad(-4f), Vector2.One * scale);

        // The words, laid out side by side: CLEMENTINE  VS  QUEEN CLAM.
        const int nameSize = 52, vsSize = 72;
        const float gap = 26f, h = 170f;
        float wl = font.GetStringSize("CLEMENTINE", HorizontalAlignment.Left, -1, nameSize).X;
        float wv = font.GetStringSize("VS", HorizontalAlignment.Left, -1, vsSize).X;
        float wr = font.GetStringSize(_boss, HorizontalAlignment.Left, -1, nameSize).X;
        float total = wl + gap + wv + gap + wr;
        float x0 = -total / 2f, xv = x0 + wl + gap, x1 = xv + wv + gap;
        float w = total + 120f;

        // The panel: a slanted ink-edged card with a burst of rays behind, a yellow stripe behind the VS.
        var rays = new Color(1f, 0.85f, 0.3f, 0.22f * alpha);
        for (int i = 0; i < 16; i++)
        {
            float a0 = i * Mathf.Tau / 16f, a1 = a0 + Mathf.Tau / 48f;
            DrawColoredPolygon(new[] { Vector2.Zero, new Vector2(Mathf.Cos(a0), Mathf.Sin(a0) * 0.5f) * 560f, new Vector2(Mathf.Cos(a1), Mathf.Sin(a1) * 0.5f) * 560f }, rays);
        }
        Vector2[] Card(float grow) => new[]
        {
            new Vector2(-w / 2f - grow + 30f, -h / 2f - grow), new Vector2(w / 2f + grow + 30f, -h / 2f - grow),
            new Vector2(w / 2f + grow - 30f, h / 2f + grow), new Vector2(-w / 2f - grow - 30f, h / 2f + grow),
        };
        DrawColoredPolygon(Card(8f), new Color(0.08f, 0.05f, 0.1f, 0.95f * alpha));
        DrawColoredPolygon(Card(0f), new Color(0.98f, 0.36f, 0.32f, alpha));
        float vc = xv + wv / 2f;
        DrawColoredPolygon(new[] { new Vector2(vc - wv / 2f - 4f + 30f, -h / 2f), new Vector2(vc + wv / 2f + 16f + 30f, -h / 2f), new Vector2(vc + wv / 2f + 16f - 30f, h / 2f), new Vector2(vc - wv / 2f - 4f - 30f, h / 2f) }, new Color(1f, 0.82f, 0.25f, alpha));

        var ink = new Color(0.08f, 0.05f, 0.1f, alpha);
        void Word(string text, float x, float y, int size, Color fill)
        {
            DrawStringOutline(font, new Vector2(x, y), text, HorizontalAlignment.Left, -1, size, 14, ink);
            DrawString(font, new Vector2(x, y), text, HorizontalAlignment.Left, -1, size, fill);
        }
        var cream = new Color(1f, 0.97f, 0.88f, alpha);
        // Baselines: the cap height sits centred on the card.
        Word("CLEMENTINE", x0, nameSize * 0.36f, nameSize, cream);
        Word("VS", xv, vsSize * 0.36f, vsSize, new Color(0.15f, 0.08f, 0.2f, alpha));
        Word(_boss, x1, nameSize * 0.36f, nameSize, cream);
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }
}
