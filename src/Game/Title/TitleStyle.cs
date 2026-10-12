using System;
using Godot;

namespace OctoShoots.Game.Title;

/// <summary>
/// The title screen's look (docs/TITLE-MENU-PROPOSAL.md §3): pearl-white cards with a coral rim over the live sea, mint
/// for good things, butter for records; Baloo 2 ExtraBold for display, Nunito for the UI (THEME-BIBLE §6.8), IBM Plex
/// Mono for seeds and codes — bundled in assets/fonts (SIL Open Font License), with system fonts behind them for any
/// glyph they lack. Nunito is also the game's default font (InstallDefaultFont), so every screen speaks it. Plus the
/// surface and dive motions shared by its cards.
/// </summary>
public static class TitleStyle
{
    public static readonly Color Pearl = new(1f, 0.98f, 0.957f);
    public static readonly Color Coral = new(1f, 0.478f, 0.42f);
    public static readonly Color Mint = new(0.435f, 0.89f, 0.757f);
    public static readonly Color Butter = new(1f, 0.82f, 0.4f);
    public static readonly Color Ink = new(0.231f, 0.122f, 0.169f);
    public static readonly Color Deep = new(0.055f, 0.196f, 0.216f);
    /// <summary>Muted ink on a card, and muted pearl on the water.</summary>
    public static readonly Color InkSoft = new(0.54f, 0.35f, 0.4f);
    public static readonly Color WaterSoft = new(0.78f, 0.88f, 0.88f);
    public static readonly Color Rule = new(1f, 0.85f, 0.82f);

    const string Fonts = "res://assets/fonts/";
    static Font? _display, _body, _bold, _italic, _mono, _symbols;

    /// <summary>Baloo 2 ExtraBold: the logo, screen titles, big numbers, banners.</summary>
    public static Font Display => _display ??= Weighted("Baloo2-Variable.ttf", 800, "Arial Rounded MT Bold", "Segoe UI Black");
    /// <summary>Nunito SemiBold: menu items' details, labels, body text, captions.</summary>
    public static Font Body => _body ??= Weighted("Nunito-Variable.ttf", 600, "Segoe UI", "Arial");
    /// <summary>Nunito ExtraBold: menu items, names, emphasis.</summary>
    public static Font BodyBold => _bold ??= Weighted("Nunito-Variable.ttf", 800, "Segoe UI", "Arial");
    /// <summary>Nunito Italic: the logo's line, taglines.</summary>
    public static Font Italic => _italic ??= Weighted("Nunito-Italic-Variable.ttf", 600, "Segoe UI", "Arial");
    /// <summary>IBM Plex Mono Medium: seeds and save codes.</summary>
    public static Font Mono => _mono ??= Weighted("IBMPlexMono-Medium.ttf", 0, "Cascadia Mono", "Consolas");

    /// <summary>Makes Nunito the default font of every control and every drawn string that names none.</summary>
    public static void InstallDefaultFont() => ThemeDB.FallbackFont = Body;

    /// <summary>
    /// A bundled font at a weight (variable fonts; 0 leaves a static font as it is), backed by the named system fonts and
    /// a symbol font for any glyph it lacks. If the file is missing, the system fonts alone.
    /// </summary>
    static Font Weighted(string file, int weight, params string[] system)
    {
        var fallback = new SystemFont { FontNames = system, FontWeight = weight > 0 ? weight : 500 };
        var symbols = _symbols ??= new SystemFont { FontNames = new[] { "Segoe UI Symbol", "Segoe UI Emoji", "Arial Unicode MS" } };
        fallback.Fallbacks = new Godot.Collections.Array<Font> { symbols };
        var data = ResourceLoader.Exists(Fonts + file) ? GD.Load<FontFile>(Fonts + file) : null;
        if (data is null) return fallback;
        var font = new FontVariation { BaseFont = data, Fallbacks = new Godot.Collections.Array<Font> { fallback } };
        if (weight > 0) font.VariationOpentype = new Godot.Collections.Dictionary { { TextServerManager.GetPrimaryInterface().NameToTag("wght"), weight } };
        return font;
    }

    public static Label Text(string text, int size, Color color, Font? font = null, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var label = new Label { Text = text, HorizontalAlignment = align, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontOverride("font", font ?? Body);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    public static StyleBoxFlat Box(Color fill, Color rim, int rimWidth, int radius, int margin = 0) => new()
    {
        BgColor = fill,
        BorderColor = rim,
        BorderWidthBottom = rimWidth, BorderWidthTop = rimWidth, BorderWidthLeft = rimWidth, BorderWidthRight = rimWidth,
        CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius, CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius,
        ContentMarginLeft = margin, ContentMarginRight = margin, ContentMarginTop = margin, ContentMarginBottom = margin,
        AntiAliasing = true,
    };

    /// <summary>A pill button: coral (primary) or outlined (secondary), Nunito ExtraBold.</summary>
    public static Button Pill(string text, bool primary = true, int size = 17)
    {
        var b = new Button { Text = text, FocusMode = Control.FocusModeEnum.All, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        b.AddThemeFontOverride("font", BodyBold);
        b.AddThemeFontSizeOverride("font_size", size);
        var fill = primary ? Coral : new Color(1f, 1f, 1f, 0f);
        var text_ = primary ? Pearl : Ink;
        b.AddThemeStyleboxOverride("normal", Pad(Box(fill, Coral, 2, 999)));
        b.AddThemeStyleboxOverride("hover", Pad(Box(primary ? Coral.Lightened(0.12f) : new Color(1f, 0.478f, 0.42f, 0.12f), Coral, 2, 999)));
        b.AddThemeStyleboxOverride("pressed", Pad(Box(primary ? Coral.Darkened(0.12f) : new Color(1f, 0.478f, 0.42f, 0.22f), Coral, 2, 999)));
        b.AddThemeStyleboxOverride("focus", Pad(Box(new Color(0, 0, 0, 0), Mint, 3, 999)));
        b.AddThemeStyleboxOverride("disabled", Pad(Box(new Color(0.85f, 0.82f, 0.8f), new Color(0.85f, 0.82f, 0.8f), 2, 999)));
        foreach (string state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" })
            b.AddThemeColorOverride(state, text_);
        b.AddThemeColorOverride("font_disabled_color", new Color(0.55f, 0.5f, 0.5f));
        return b;
    }

    static StyleBoxFlat Pad(StyleBoxFlat box)
    {
        box.ContentMarginLeft = box.ContentMarginRight = 18;
        box.ContentMarginTop = box.ContentMarginBottom = 7;
        return box;
    }

    /// <summary>A text field on a card: pearl fill, coral rim, mint when focused.</summary>
    public static LineEdit Field(string placeholder, Font? font = null, int size = 17)
    {
        var f = new LineEdit { PlaceholderText = placeholder };
        f.AddThemeFontOverride("font", font ?? Body);
        f.AddThemeFontSizeOverride("font_size", size);
        f.AddThemeColorOverride("font_color", Ink);
        f.AddThemeColorOverride("font_placeholder_color", new Color(0.6f, 0.5f, 0.52f));
        f.AddThemeColorOverride("caret_color", Coral);
        f.AddThemeColorOverride("selection_color", new Color(1f, 0.478f, 0.42f, 0.3f));
        var normal = Box(Colors.White, new Color(1f, 0.76f, 0.72f), 2, 12);
        normal.ContentMarginLeft = normal.ContentMarginRight = 14;
        normal.ContentMarginTop = normal.ContentMarginBottom = 8;
        var focus = Box(new Color(0, 0, 0, 0), Mint, 3, 12);
        f.AddThemeStyleboxOverride("normal", normal);
        f.AddThemeStyleboxOverride("focus", focus);
        return f;
    }

    /// <summary>The card a sub-screen sits on: pearl face, coral rim, 18 px corners and a soft shadow.</summary>
    public static PanelContainer Card(Vector2 size)
    {
        var card = new PanelContainer { CustomMinimumSize = size };
        var box = Box(Pearl, Coral, 3, 18, 28);
        box.ShadowColor = new Color(0.04f, 0.1f, 0.12f, 0.45f);
        box.ShadowSize = 24;
        box.ShadowOffset = new Vector2(0f, 10f);
        card.AddThemeStyleboxOverride("panel", box);
        return card;
    }

    /// <summary>Rises into place from <paramref name="drop"/> pixels below, with a little overshoot (or just fades).</summary>
    public static Tween Surface(Control node, float drop = 60f, float seconds = 0.42f, float delay = 0f, bool reduced = false)
    {
        var rest = node.Position;
        var t = node.CreateTween();
        node.Modulate = new Color(1f, 1f, 1f, 0f);
        if (reduced)
        {
            t.TweenProperty(node, "modulate:a", 1f, 0.2f).SetDelay(delay);
            return t;
        }
        node.Position = rest + new Vector2(0f, drop);
        t.SetParallel();
        t.TweenProperty(node, "position", rest, seconds).SetDelay(delay).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        t.TweenProperty(node, "modulate:a", 1f, seconds * 0.5f).SetDelay(delay);
        return t;
    }

    /// <summary>
    /// For things a container places (menu rows, the logo): fades in while growing from a little below full size and
    /// rising through its pivot, so the container's layout is never fought (or just fades).
    /// </summary>
    public static Tween Bloom(Control node, float delay = 0f, bool reduced = false)
    {
        var t = node.CreateTween();
        node.Modulate = new Color(1f, 1f, 1f, 0f);
        if (reduced)
        {
            t.TweenProperty(node, "modulate:a", 1f, 0.2f).SetDelay(delay);
            return t;
        }
        node.PivotOffset = new Vector2(0f, node.Size.Y);
        node.Scale = new Vector2(0.94f, 0.94f);
        t.SetParallel();
        t.TweenProperty(node, "scale", Vector2.One, 0.42f).SetDelay(delay).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        t.TweenProperty(node, "modulate:a", 1f, 0.22f).SetDelay(delay);
        return t;
    }

    /// <summary>A small hop, then down and out (or just fades); calls <paramref name="done"/> at the end.</summary>
    public static Tween Dive(Control node, Action done, float seconds = 0.38f, bool reduced = false)
    {
        var t = node.CreateTween();
        if (reduced)
        {
            t.TweenProperty(node, "modulate:a", 0f, 0.18f);
        }
        else
        {
            var rest = node.Position;
            t.TweenProperty(node, "position", rest + new Vector2(0f, -10f), seconds * 0.25f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            t.SetParallel(false);
            t.TweenProperty(node, "position", rest + new Vector2(0f, 120f), seconds * 0.75f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
            t.Parallel().TweenProperty(node, "modulate:a", 0f, seconds * 0.75f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        }
        t.TweenCallback(Callable.From(done));
        return t;
    }

    public static string Clock(double seconds)
    {
        var t = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes}:{t.Seconds:00}";
    }

    public static string Hours(double seconds)
    {
        var t = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours} h {t.Minutes} m" : $"{t.Minutes} m {t.Seconds} s";
    }
}
