using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OctoShoots.Core.Run;

namespace OctoShoots.Game.Title;

/// <summary>
/// The seeded run's box (proposal §4.2), opened under the menu item: two groups of four from the seed alphabet. Typing
/// is forgiving (upper-cased, spaced for you, paste works); a character outside the alphabet is refused with a shake.
/// The dice rolls a random seed, the chips replay recent ones, Enter or Dive starts the run.
/// </summary>
public partial class SeedBox : PanelContainer
{
    const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    readonly IReadOnlyList<string> _recent;
    readonly Action<SeedCode> _dive;
    LineEdit _field = null!;
    Label _check = null!;
    Button _go = null!;
    bool _formatting;

    public SeedBox(IReadOnlyList<string> recent, Action<SeedCode> dive)
    {
        _recent = recent;
        _dive = dive;
    }

    public override void _Ready()
    {
        AddThemeStyleboxOverride("panel", TitleStyle.Box(TitleStyle.Pearl, TitleStyle.Coral, 3, 16, 18));
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 10);
        AddChild(box);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        box.AddChild(row);
        _field = TitleStyle.Field("KELP 7Q2Z", TitleStyle.Mono, 28);
        _field.MaxLength = 9;
        _field.CustomMinimumSize = new Vector2(250f, 0f);
        _field.TextChanged += Typed;
        _field.TextSubmitted += _ => Go();
        row.AddChild(_field);
        _check = TitleStyle.Text("", 26, new Color(0.1f, 0.55f, 0.43f), TitleStyle.BodyBold);
        _check.CustomMinimumSize = new Vector2(26f, 0f);
        _check.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        row.AddChild(_check);
        var dice = TitleStyle.Pill("⚄  Random", primary: false, size: 16);
        dice.TooltipText = "Roll a random seed";
        dice.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        dice.Pressed += () => Fill(SeedCode.NewRandom().ToString());
        row.AddChild(dice);
        _go = TitleStyle.Pill("Dive", size: 18);
        _go.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        _go.Pressed += Go;
        row.AddChild(_go);

        if (_recent.Count > 0)
        {
            var chips = new HBoxContainer();
            chips.AddThemeConstantOverride("separation", 8);
            chips.AddChild(TitleStyle.Text("Recent", 14, TitleStyle.InkSoft, TitleStyle.BodyBold));
            foreach (string seed in _recent.Take(5))
            {
                var chip = TitleStyle.Pill(seed, primary: false, size: 14);
                chip.AddThemeFontOverride("font", TitleStyle.Mono);
                chip.Pressed += () => Fill(seed);
                chips.AddChild(chip);
            }
            box.AddChild(chips);
        }
        box.AddChild(TitleStyle.Text("Seeded runs don't earn achievements or unlock pearls.", 14, TitleStyle.InkSoft));

        Fill(_recent.Count > 0 ? _recent[0] : "");
    }

    public void FocusField() => CallDeferred(MethodName.FocusLater);

    void FocusLater()
    {
        _field.GrabFocus();
        _field.CaretColumn = _field.Text.Length;
    }

    void Fill(string text)
    {
        _field.Text = text;
        Typed(text);
        _field.GrabFocus();
    }

    /// <summary>Upper-cases, drops anything outside the alphabet (with a shake), and spaces the two groups.</summary>
    void Typed(string text)
    {
        if (_formatting) return;
        string upper = text.ToUpperInvariant();
        var kept = new string(upper.Where(c => Alphabet.Contains(c)).Take(8).ToArray());
        bool refused = upper.Any(c => c != ' ' && !Alphabet.Contains(c));
        string formatted = kept.Length > 4 ? kept[..4] + " " + kept[4..] : kept;
        if (formatted != text)
        {
            _formatting = true;
            _field.Text = formatted;
            _field.CaretColumn = formatted.Length;
            _formatting = false;
        }
        if (refused) Shake();
        bool valid = kept.Length == 8 && SeedCode.TryParse(kept, out _);
        _check.Text = valid ? "✓" : "";
        _go.Disabled = !valid;
        var rim = valid ? TitleStyle.Mint : new Color(1f, 0.76f, 0.72f);
        var normal = TitleStyle.Box(Colors.White, rim, valid ? 3 : 2, 12);
        normal.ContentMarginLeft = normal.ContentMarginRight = 14;
        normal.ContentMarginTop = normal.ContentMarginBottom = 8;
        _field.AddThemeStyleboxOverride("normal", normal);
    }

    void Shake()
    {
        _field.PivotOffset = _field.Size * 0.5f;
        var t = _field.CreateTween();
        foreach (float a in new[] { 2.5f, -2.5f, 1.5f, -1f, 0f })
            t.TweenProperty(_field, "rotation_degrees", a, 0.04f);
        var flash = _field.CreateTween();
        _field.Modulate = new Color(1f, 0.7f, 0.68f);
        flash.TweenProperty(_field, "modulate", Colors.White, 0.3f);
    }

    void Go()
    {
        string raw = _field.Text.Replace(" ", "");
        if (raw.Length == 8 && SeedCode.TryParse(raw, out var seed)) _dive(seed);
        else Shake();
    }
}
