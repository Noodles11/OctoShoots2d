using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using OctoShoots.Core.Saves;

namespace OctoShoots.Game.Title;

/// <summary>
/// The glossary (proposal §4.4): two tabs, Pearls and Creatures, each a grid of tiles with a detail pane for the
/// selected one: what it is, what it does, and its record against Clementine.
/// </summary>
public partial class SeaPediaCard : PanelContainer
{
    /// <summary>The creatures on the plane today: name, field-guide line, plain notes, and whether it is a boss.</summary>
    public static readonly IReadOnlyDictionary<string, (string Name, string Line, string Notes, bool Boss)> Creatures =
        new Dictionary<string, (string, string, string, bool)>
        {
            [PlaneProfileRecorder.MobId] = ("Pufferling",
                "Round, spotty and easily offended. The Leak dyed this one red and clipped a price tag to its tail.",
                "Notices you within 10 m and drifts closer, facing you. Within 8 m it blows up into a spiny ball (0.8 s), fires eight needles in a ring, turned differently every time (8 each; hot pink, they pop your bubbles), and stays round for a moment. Then it backs off and shrinks for 3 s: your window. Its raised spines sting on touch (4). 60 HP. Freed, it swims off in its own sand-and-olive colours.",
                false),
            [PlaneProfileRecorder.QueenClamId] = ("Queen Clam",
                "Sits on the rift. Hates visitors. Loves pearls, as long as they are hers.",
                "Hurt only while her shell is open. Rings and walls of pearls; at half health a spinning helix, a slow homing royal pearl, and a snap if you come within 7 m. Any bubble pops a pearl. 800 HP.",
                true),
        };

    readonly Profile _profile;
    readonly ItemCatalog? _catalog;
    readonly Action _close;
    GridContainer _grid = null!;
    VBoxContainer _detail = null!;
    Button _pearlTab = null!, _creatureTab = null!;
    bool _pearls = true;
    readonly List<ShaderMaterial> _spinning = new();
    float _time;

    public SeaPediaCard(Profile profile, ItemCatalog? catalog, Action close)
    {
        _profile = profile;
        _catalog = catalog;
        _close = close;
    }

    public static int PearlsFound(Profile p) => PlaneRun.PortedPearls.Count(id => p.SeenItems.Contains(id));
    public static int CreaturesMet(Profile p) => Creatures.Keys.Count(id => p.SeenCreatures.Contains(id) || p.SeenBosses.Contains(id));

    public override void _Ready()
    {
        var style = TitleStyle.Box(TitleStyle.Pearl, TitleStyle.Coral, 3, 18, 28);
        style.ShadowColor = new Color(0.04f, 0.1f, 0.12f, 0.45f);
        style.ShadowSize = 24;
        style.ShadowOffset = new Vector2(0f, 10f);
        AddThemeStyleboxOverride("panel", style);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 16);
        AddChild(box);

        var head = new HBoxContainer();
        box.AddChild(head);
        var title = TitleStyle.Text("Sea-pedia", 40, TitleStyle.Ink, TitleStyle.Display);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        head.AddChild(title);
        var close = TitleStyle.Pill("Close  (Esc · B)", primary: false, size: 15);
        close.Pressed += _close;
        head.AddChild(close);

        var tabs = new HBoxContainer();
        tabs.AddThemeConstantOverride("separation", 10);
        box.AddChild(tabs);
        _pearlTab = TitleStyle.Pill($"Pearls  {PearlsFound(_profile)} / {PlaneRun.PortedPearls.Length}", size: 16);
        _creatureTab = TitleStyle.Pill($"Creatures  {CreaturesMet(_profile)} / {Creatures.Count}", primary: false, size: 16);
        _pearlTab.Pressed += () => ShowTab(true);
        _creatureTab.Pressed += () => ShowTab(false);
        tabs.AddChild(_pearlTab);
        tabs.AddChild(_creatureTab);

        var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 28);
        box.AddChild(body);
        var gridScroll = new ScrollContainer { CustomMinimumSize = new Vector2(556f, 520f), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        body.AddChild(gridScroll);
        _grid = new GridContainer { Columns = 5 };
        _grid.AddThemeConstantOverride("h_separation", 12);
        _grid.AddThemeConstantOverride("v_separation", 12);
        gridScroll.AddChild(_grid);
        body.AddChild(new VSeparator());
        _detail = new VBoxContainer { CustomMinimumSize = new Vector2(380f, 0f), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _detail.ClipContents = true;
        _detail.AddThemeConstantOverride("separation", 10);
        body.AddChild(_detail);

        ShowTab(true);
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        foreach (var m in _spinning) m.SetShaderParameter("spin", _time * 0.6f);
    }

    /// <summary>Opens on the Creatures tab.</summary>
    public void ShowCreatures() => ShowTab(false);

    void ShowTab(bool pearls)
    {
        _pearls = pearls;
        Restyle(_pearlTab, pearls);
        Restyle(_creatureTab, !pearls);
        foreach (var child in _grid.GetChildren()) child.QueueFree();
        _spinning.Clear();
        Button? first = null;
        if (pearls)
        {
            foreach (string id in PlaneRun.PortedPearls)
            {
                var tile = PearlTile(id);
                _grid.AddChild(tile);
                first ??= tile;
            }
        }
        else
        {
            foreach (string id in Creatures.Keys)
            {
                var tile = CreatureTile(id);
                _grid.AddChild(tile);
                first ??= tile;
            }
        }
        first?.CallDeferred(Control.MethodName.GrabFocus);
        first?.EmitSignal(BaseButton.SignalName.Pressed);
    }

    static void Restyle(Button tab, bool active)
    {
        var fill = active ? TitleStyle.Coral : new Color(1f, 1f, 1f, 0f);
        var box = TitleStyle.Box(fill, TitleStyle.Coral, 2, 999);
        box.ContentMarginLeft = box.ContentMarginRight = 18;
        box.ContentMarginTop = box.ContentMarginBottom = 7;
        tab.AddThemeStyleboxOverride("normal", box);
        foreach (string state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" })
            tab.AddThemeColorOverride(state, active ? TitleStyle.Pearl : TitleStyle.Ink);
    }

    enum Known { Unknown, Seen, Absorbed }

    Known PearlState(string id) =>
        _profile.Pearls.TryGetValue(id, out var r) && r.Absorbed > 0 ? Known.Absorbed
        : _profile.SeenItems.Contains(id) ? Known.Seen : Known.Unknown;

    /// <summary>A flat button holding a pearl icon (or a creature portrait) and its name.</summary>
    static Button Tile(Control art, string caption, bool known)
    {
        var tile = new Button { CustomMinimumSize = new Vector2(100f, 124f), FocusMode = FocusModeEnum.All, MouseDefaultCursorShape = CursorShape.PointingHand };
        var normal = TitleStyle.Box(new Color(1f, 0.93f, 0.9f), new Color(1f, 0.85f, 0.8f), 2, 14);
        tile.AddThemeStyleboxOverride("normal", normal);
        tile.AddThemeStyleboxOverride("hover", TitleStyle.Box(new Color(1f, 0.9f, 0.86f), TitleStyle.Coral, 2, 14));
        tile.AddThemeStyleboxOverride("pressed", TitleStyle.Box(new Color(1f, 0.88f, 0.84f), TitleStyle.Coral, 3, 14));
        tile.AddThemeStyleboxOverride("focus", TitleStyle.Box(new Color(0, 0, 0, 0), TitleStyle.Mint, 3, 14));
        var col = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        col.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        col.AddThemeConstantOverride("separation", 4);
        tile.AddChild(col);
        var holder = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        holder.AddChild(art);
        col.AddChild(holder);
        var name = TitleStyle.Text(known ? caption : "?", 13, known ? TitleStyle.Ink : TitleStyle.InkSoft, TitleStyle.BodyBold, HorizontalAlignment.Center);
        name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        name.CustomMinimumSize = new Vector2(92f, 0f);
        col.AddChild(name);
        return tile;
    }

    ColorRect PearlIcon(ItemDef? item, Known state, float size)
    {
        var mat = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/pearl_icon.gdshader") };
        var look = item?.Pearl;
        var colors = look?.Colors ?? new List<string> { "#ffffff", "#dddddd" };
        mat.SetShaderParameter("c0", Color.FromHtml(colors[0]));
        mat.SetShaderParameter("c1", Color.FromHtml(colors[Math.Min(1, colors.Count - 1)]));
        mat.SetShaderParameter("c2", Color.FromHtml(colors[^1]));
        mat.SetShaderParameter("pattern", (int)(look?.Pattern ?? PearlPattern.Halo));
        mat.SetShaderParameter("state", state switch { Known.Absorbed => 0, Known.Seen => 1, _ => 2 });
        _spinning.Add(mat);
        return new ColorRect { CustomMinimumSize = new Vector2(size, size), Material = mat, MouseFilter = MouseFilterEnum.Ignore };
    }

    Button PearlTile(string id)
    {
        ItemDef? item = null;
        _catalog?.TryGet(id, out item);
        var state = PearlState(id);
        var tile = Tile(PearlIcon(item, state, 64f), item?.Name ?? id, state != Known.Unknown);
        tile.Pressed += () => ShowPearl(id, item, state);
        tile.FocusEntered += () => ShowPearl(id, item, state);
        return tile;
    }

    Button CreatureTile(string id)
    {
        bool known = _profile.SeenCreatures.Contains(id) || _profile.SeenBosses.Contains(id);
        var info = Creatures[id];
        var tile = Tile(new Portrait(id, known) { CustomMinimumSize = new Vector2(70f, 70f) }, info.Name, known);
        tile.Pressed += () => ShowCreature(id, known);
        tile.FocusEntered += () => ShowCreature(id, known);
        return tile;
    }

    void ClearDetail()
    {
        foreach (var child in _detail.GetChildren()) child.QueueFree();
    }

    void ShowPearl(string id, ItemDef? item, Known state)
    {
        ClearDetail();
        var top = new HBoxContainer();
        top.AddThemeConstantOverride("separation", 18);
        _detail.AddChild(top);
        top.AddChild(PearlIcon(item, state, 120f));
        var names = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
        top.AddChild(names);
        if (state == Known.Unknown || item is null)
        {
            names.AddChild(TitleStyle.Text("Not found yet", 28, TitleStyle.Ink, TitleStyle.Display));
            names.AddChild(Wrapped("Find it in a treasure room, a shop, or on a freed boss.", 15, TitleStyle.InkSoft, width: 230f));
            return;
        }
        names.AddChild(TitleStyle.Text(item.Name, 30, TitleStyle.Ink, TitleStyle.Display));
        names.AddChild(Wrapped(item.Tagline, 16, TitleStyle.InkSoft, width: 230f));
        if (state == Known.Seen) names.AddChild(Chip("SEEN, NOT TAKEN", new Color(1f, 0.9f, 0.86f), TitleStyle.InkSoft));
        foreach (string line in ItemCaption.Describe(item)) _detail.AddChild(Wrapped("+ " + line, 16, TitleStyle.Ink, TitleStyle.BodyBold));
        _detail.AddChild(Wrapped("Found in: " + string.Join(", ", item.Pools.Select(p => p switch { "treasure" => "treasure rooms", "shop" => "shops", "boss" => "bosses", _ => p })), 14, TitleStyle.InkSoft));
        _detail.AddChild(new HSeparator());
        var r = _profile.Pearls.GetValueOrDefault(id) ?? new PearlRecord();
        _detail.AddChild(Record(("Absorbed", r.Absorbed.ToString("N0")), ("Runs it was in", r.Runs.ToString("N0")),
            ("Runs lost holding it", r.RunsLost.ToString("N0")), ("Levels cleared with it", r.RoomsCleared.ToString("N0"))));
    }

    void ShowCreature(string id, bool known)
    {
        ClearDetail();
        var info = Creatures[id];
        var top = new HBoxContainer();
        top.AddThemeConstantOverride("separation", 18);
        _detail.AddChild(top);
        top.AddChild(new Portrait(id, known) { CustomMinimumSize = new Vector2(120f, 120f) });
        var names = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
        top.AddChild(names);
        if (!known)
        {
            names.AddChild(TitleStyle.Text("Not met yet", 28, TitleStyle.Ink, TitleStyle.Display));
            names.AddChild(Wrapped(info.Boss ? "Waits somewhere deeper." : "Lurks somewhere in the canyons.", 15, TitleStyle.InkSoft, width: 230f));
            return;
        }
        names.AddChild(TitleStyle.Text(info.Name, 30, TitleStyle.Ink, TitleStyle.Display));
        if (info.Boss) names.AddChild(Chip("BOSS", TitleStyle.Butter, TitleStyle.Ink));
        _detail.AddChild(Wrapped("“" + info.Line + "”", 17, TitleStyle.InkSoft));
        _detail.AddChild(Wrapped(info.Notes, 15, TitleStyle.Ink));
        _detail.AddChild(new HSeparator());
        var r = _profile.Creatures.GetValueOrDefault(id) ?? new CreatureRecord();
        var cells = new List<(string, string)>
        {
            (info.Boss ? "Freed" : "Defeated", r.Defeated.ToString("N0")),
            ("Defeated you", r.DefeatedYou.ToString("N0")),
            ("Encounters", r.Encounters.ToString("N0")),
        };
        if (info.Boss) cells.Add(("Best time", r.BestSeconds > 0 ? TitleStyle.Clock(r.BestSeconds) : "—"));
        _detail.AddChild(Record(cells.ToArray()));
    }

    static Label Wrapped(string text, int size, Color color, Font? font = null, float width = 380f)
    {
        var l = TitleStyle.Text(text, size, color, font);
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        l.CustomMinimumSize = new Vector2(width, 0f);
        return l;
    }

    static Control Chip(string text, Color fill, Color ink)
    {
        var p = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        var box = TitleStyle.Box(fill, fill, 0, 999);
        box.ContentMarginLeft = box.ContentMarginRight = 10;
        box.ContentMarginTop = box.ContentMarginBottom = 2;
        p.AddThemeStyleboxOverride("panel", box);
        p.AddChild(TitleStyle.Text(text, 12, ink, TitleStyle.BodyBold));
        return p;
    }

    /// <summary>A row of record cells: a big number over a small label.</summary>
    static Control Record(params (string Label, string Value)[] cells)
    {
        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 26);
        grid.AddThemeConstantOverride("v_separation", 12);
        foreach (var (label, value) in cells)
        {
            var cell = new VBoxContainer { CustomMinimumSize = new Vector2(170f, 0f) };
            cell.AddThemeConstantOverride("separation", 0);
            cell.AddChild(TitleStyle.Text(value, 32, TitleStyle.Ink, TitleStyle.Display));
            cell.AddChild(TitleStyle.Text(label, 14, TitleStyle.InkSoft));
            grid.AddChild(cell);
        }
        return grid;
    }

    /// <summary>A creature's portrait, drawn: the Pufferling as a red, lime-spotted ball of spines, Queen Clam as her crowned shell.</summary>
    public partial class Portrait : Control
    {
        readonly string _id;
        readonly bool _known;

        public Portrait(string id, bool known)
        {
            _id = id;
            _known = known;
            MouseFilter = MouseFilterEnum.Ignore;
        }

        public override void _Draw()
        {
            var c = Size * 0.5f;
            float r = Mathf.Min(Size.X, Size.Y) * 0.5f;
            var dark = new Color(0.12f, 0.15f, 0.18f);
            DrawCircle(c, r, _known ? new Color(0.86f, 0.95f, 0.93f) : new Color(0.8f, 0.78f, 0.78f));
            if (_id == PlaneProfileRecorder.MobId)
            {
                // Blown up: a red ball, spines all round, lime spots, a tail fin with its price tag.
                var body = _known ? new Color(0.95f, 0.25f, 0.3f) : dark;
                var at = c + new Vector2(-r * 0.05f, r * 0.04f);
                float ball = r * 0.5f;
                for (int i = 0; i < 18; i++)
                {
                    float a = i * Mathf.Tau / 18f;
                    var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    DrawLine(at + dir * ball * 0.9f, at + dir * ball * 1.32f, _known ? new Color(0.55f, 0.38f, 0.3f) : dark, r * 0.04f, true);
                }
                var tail = new[] { at + new Vector2(ball * 0.92f, 0f), at + new Vector2(ball * 1.35f, -ball * 0.38f), at + new Vector2(ball * 1.35f, ball * 0.38f) };
                DrawColoredPolygon(tail, _known ? new Color(0.45f, 1f, 0.25f) : dark);
                DrawCircle(at, ball, body);
                if (!_known) return;
                foreach (var (x, y) in new[] { (-0.45f, -0.3f), (0f, -0.55f), (0.4f, -0.25f), (-0.15f, 0.2f), (0.3f, 0.35f), (-0.5f, 0.45f), (0.55f, 0.05f) })
                    DrawCircle(at + new Vector2(x, y) * ball, ball * 0.11f, new Color(0.5f, 1f, 0.25f));
                DrawCircle(at + new Vector2(-ball * 0.45f, -ball * 0.05f), ball * 0.2f, new Color(0.45f, 1f, 0.2f));
                DrawCircle(at + new Vector2(-ball * 0.48f, -ball * 0.05f), ball * 0.11f, new Color(0.05f, 0.04f, 0.04f));
                DrawLine(at + new Vector2(-ball * 0.7f, -ball * 0.36f), at + new Vector2(-ball * 0.25f, -ball * 0.28f), new Color(0.3f, 0.02f, 0.05f), r * 0.045f, true);
                // The price tag on the tail: a cream card with a pink edge.
                var tag = new Rect2(at + new Vector2(ball * 1.2f, ball * 0.35f), new Vector2(r * 0.2f, r * 0.13f));
                DrawRect(tag, new Color(1f, 0.42f, 0.8f));
                DrawRect(tag.Grow(-r * 0.02f), new Color(0.98f, 0.95f, 0.86f));
            }
            else
            {
                // A shell fan, hinge down, ribbed, with a pearl crown on the hinge.
                var shell = _known ? new Color(0.25f, 0.62f, 0.6f) : dark;
                var hinge = c + new Vector2(0f, r * 0.45f);
                var pts = new List<Vector2> { hinge };
                for (int i = 0; i <= 16; i++)
                {
                    float a = Mathf.Pi * (1.08f + 0.84f * i / 16f);
                    float bump = 1f + 0.05f * Mathf.Cos(i * Mathf.Pi);
                    pts.Add(hinge + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r * 0.8f * bump);
                }
                DrawColoredPolygon(pts.ToArray(), shell);
                if (!_known) return;
                for (int i = 1; i < 8; i++)
                {
                    float a = Mathf.Pi * (1.08f + 0.84f * i / 8f);
                    DrawLine(hinge, hinge + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r * 0.78f, new Color(0.13f, 0.4f, 0.44f), 2f, true);
                }
                for (int i = -2; i <= 2; i++)
                    DrawCircle(hinge + new Vector2(i * r * 0.14f, r * 0.06f - (i == 0 ? r * 0.04f : 0f)), r * (i == 0 ? 0.1f : 0.075f), new Color(1f, 0.97f, 0.92f));
                DrawCircle(c + new Vector2(-r * 0.16f, -r * 0.05f), r * 0.07f, Colors.White);
                DrawCircle(c + new Vector2(r * 0.16f, -r * 0.05f), r * 0.07f, Colors.White);
                DrawCircle(c + new Vector2(-r * 0.15f, -r * 0.04f), r * 0.035f, Colors.Black);
                DrawCircle(c + new Vector2(r * 0.17f, -r * 0.04f), r * 0.035f, Colors.Black);
            }
        }
    }
}
