using System.Linq;
using System;
using Godot;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// ESC: the game stops under a dimmed screen. Resume, restart the run (a new random seed, room 1, no pearls, full HP),
/// and every pearl Clementine has absorbed with its name, tagline and effects (the item caption lines).
/// </summary>
public partial class PauseMenu : Control
{
    public event Action? ResumePressed;
    public event Action? RestartPressed;
    public event Action? QuitPressed;

    VBoxContainer _pearls = null!;
    Label _summary = null!;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        AddChild(new ColorRect { Color = new Color(0.01f, 0.03f, 0.05f, 0.72f), MouseFilter = MouseFilterEnum.Ignore, AnchorRight = 1f, AnchorBottom = 1f });

        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(560f, 0f) };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.04f, 0.09f, 0.11f, 0.95f),
            BorderColor = new Color(1f, 0.75f, 0.55f, 0.5f),
            BorderWidthBottom = 2, BorderWidthTop = 2, BorderWidthLeft = 2, BorderWidthRight = 2,
            CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10, CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10,
            ContentMarginLeft = 24, ContentMarginRight = 24, ContentMarginTop = 20, ContentMarginBottom = 20,
        });
        center.AddChild(panel);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 12);
        panel.AddChild(box);

        var title = new Label { Text = "Paused", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 34);
        title.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.7f));
        box.AddChild(title);

        _summary = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _summary.AddThemeColorOverride("font_color", new Color(0.75f, 0.85f, 0.88f));
        box.AddChild(_summary);

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 16);
        box.AddChild(buttons);
        var resume = new Button { Text = "Resume  (Esc)", CustomMinimumSize = new Vector2(170f, 40f) };
        resume.Pressed += () => ResumePressed?.Invoke();
        buttons.AddChild(resume);
        var restart = new Button { Text = "Restart run", CustomMinimumSize = new Vector2(170f, 40f) };
        restart.Pressed += () => RestartPressed?.Invoke();
        buttons.AddChild(restart);
        var quit = new Button { Text = "Save & quit to title", CustomMinimumSize = new Vector2(190f, 40f), TooltipText = "You'll resume at the start of this room." };
        quit.Pressed += () => QuitPressed?.Invoke();
        buttons.AddChild(quit);

        box.AddChild(new HSeparator());
        var heading = new Label { Text = "Absorbed pearls" };
        heading.AddThemeFontSizeOverride("font_size", 20);
        heading.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.7f));
        box.AddChild(heading);

        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0f, 340f), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        box.AddChild(scroll);
        _pearls = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _pearls.AddThemeConstantOverride("separation", 14);
        scroll.AddChild(_pearls);
    }

    /// <summary>Fills the menu from the run and shows it.</summary>
    public void Open(PlaneRun run, ItemCatalog? catalog, string seed, int room, float hp)
    {
        _summary.Text = $"Seed {seed} · Room {room} · HP {Mathf.CeilToInt(hp)} / {Mathf.RoundToInt(run.MaxHp)} · {run.Items.Count} pearl{(run.Items.Count == 1 ? "" : "s")}";
        foreach (var child in _pearls.GetChildren()) child.QueueFree();
        if (run.Items.Count == 0 || catalog is null)
        {
            var none = new Label { Text = "None yet — treasure rooms hold them.", AutowrapMode = TextServer.AutowrapMode.WordSmart };
            none.AddThemeColorOverride("font_color", new Color(0.6f, 0.7f, 0.72f));
            _pearls.AddChild(none);
        }
        else
        {
            // Newest first.
            foreach (string id in Enumerable.Reverse(run.Items))
                if (catalog.TryGet(id, out var item)) _pearls.AddChild(Row(item));
        }
        Visible = true;
    }

    public void Close() => Visible = false;

    static Control Row(ItemDef item)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 14);
        row.AddChild(new PearlDot(item.Pearl));
        var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        text.AddThemeConstantOverride("separation", 2);
        row.AddChild(text);

        var name = new Label { Text = item.Name };
        name.AddThemeFontSizeOverride("font_size", 19);
        name.AddThemeColorOverride("font_color", new Color(1f, 0.93f, 0.85f));
        text.AddChild(name);
        if (!string.IsNullOrEmpty(item.Tagline))
        {
            var tagline = new Label { Text = item.Tagline };
            tagline.AddThemeColorOverride("font_color", new Color(0.62f, 0.72f, 0.75f));
            text.AddChild(tagline);
        }
        foreach (string line in ItemCaption.Describe(item))
        {
            var effect = new Label { Text = "• " + line, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            effect.AddThemeColorOverride("font_color", new Color(0.82f, 0.95f, 0.88f));
            text.AddChild(effect);
        }
        return row;
    }

    /// <summary>The pearl in its own colours, as on the HUD but larger.</summary>
    partial class PearlDot : Control
    {
        readonly PearlLook? _look;

        public PearlDot(PearlLook? look)
        {
            _look = look;
            CustomMinimumSize = new Vector2(44f, 44f);
            MouseFilter = MouseFilterEnum.Ignore;
        }

        public override void _Draw()
        {
            Color a = _look is null ? Colors.White : new Color(_look.Colors[0]);
            Color b = _look is null ? Colors.LightBlue : new Color(_look.Colors[^1]);
            var c = new Vector2(22f, 22f);
            DrawCircle(c, 19f, new Color(0f, 0f, 0f, 0.6f));
            DrawCircle(c, 17f, b);
            DrawCircle(c - new Vector2(3f, 3f), 12f, a);
            DrawCircle(c - new Vector2(7f, 7f), 4f, new Color(1f, 1f, 1f, 0.8f));
        }
    }
}
