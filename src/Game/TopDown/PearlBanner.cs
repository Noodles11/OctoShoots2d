using Godot;
using OctoShoots.Core.Items;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The pearl she is absorbing, on the HUD (docs/PEARLS.md): as the ritual begins a card rises at the bottom centre with
/// the pearl as the pause menu lists it — the pearl in its colours, its name, its tagline and its effects — under an
/// "Absorbing" that turns to "Absorbed" as the pearl becomes hers. It holds a few seconds more to be read, then sinks
/// away. Over an achievement card it stands above it. It runs on game time (the game steps it).
/// </summary>
public partial class PearlBanner : Control
{
    const float Width = 560f, Bottom = 64f, Above = 250f, Rise = 28f, InTime = 0.3f, OutTime = 0.45f, ReadTime = 3.5f;

    PanelContainer _card = null!;
    Label _eyebrow = null!;
    VBoxContainer _body = null!;
    string? _id;
    bool _absorbed;
    float _shown, _hold;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _card = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore, Visible = false, CustomMinimumSize = new Vector2(Width, 0f) };
        _card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.02f, 0.05f, 0.08f, 0.82f),
            BorderColor = new Color(1f, 0.82f, 0.55f, 0.55f),
            BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 14, CornerRadiusTopRight = 14, CornerRadiusBottomLeft = 14, CornerRadiusBottomRight = 14,
            ContentMarginLeft = 20, ContentMarginRight = 20, ContentMarginTop = 12, ContentMarginBottom = 14,
        });
        AddChild(_card);
        _body = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _body.AddThemeConstantOverride("separation", 6);
        _card.AddChild(_body);
        _eyebrow = new Label();
        _eyebrow.AddThemeFontSizeOverride("font_size", 13);
        _eyebrow.AddThemeColorOverride("font_color", new Color(1f, 0.78f, 0.45f));
        _body.AddChild(_eyebrow);
    }

    /// <summary>
    /// Each frame: the pearl she is absorbing now (null when none). A new one shows its card; once it is hers the card
    /// holds ReadTime, then leaves. <paramref name="raised"/>: an achievement card is showing, so it stands above it.
    /// </summary>
    public void Step(float dt, ItemDef? absorbing, bool raised)
    {
        if (absorbing is not null && absorbing.Id != _id) Show(absorbing);
        if (_id is null) return;
        if (absorbing is null && !_absorbed)
        {
            // The pearl is hers.
            _absorbed = true;
            _eyebrow.Text = "ABSORBED";
            _hold = ReadTime;
        }
        if (_absorbed) _hold -= dt;
        _shown = _absorbed && _hold <= 0f ? Mathf.MoveToward(_shown, 0f, dt / OutTime) : Mathf.MoveToward(_shown, 1f, dt / InTime);
        if (_absorbed && _hold <= 0f && _shown <= 0f)
        {
            _id = null;
            _card.Visible = false;
            return;
        }
        float ease = 1f - (1f - _shown) * (1f - _shown);
        _card.Modulate = new Color(1f, 1f, 1f, ease);
        _card.Size = new Vector2(Width, 0f);
        _card.ResetSize();
        var size = _card.Size;
        _card.Position = new Vector2((Size.X - size.X) * 0.5f, Size.Y - size.Y - (raised ? Above : Bottom) + Rise * (1f - ease));
    }

    void Show(ItemDef item)
    {
        _id = item.Id;
        _absorbed = false;
        _shown = 0f;
        _eyebrow.Text = "ABSORBING";
        // The pearl as the pause menu lists it.
        for (int i = _body.GetChildCount() - 1; i >= 1; i--) _body.GetChild(i).QueueFree();
        _body.AddChild(PauseMenu.Row(item));
        _card.Visible = true;
    }
}
