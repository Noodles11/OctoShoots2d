using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OctoShoots.Core.Plane;
using OctoShoots.Core.Saves;

namespace OctoShoots.Game.Title;

/// <summary>
/// The statistics (proposal §4.3): four headline numbers, then runs, combat, treasure and records in label/value rows
/// (records marked "best"), and a bar per cause of death. Seeded runs are counted too.
/// </summary>
public partial class StatsCard : PanelContainer
{
    readonly Profile _profile;
    readonly Func<string, string> _pearlName;
    readonly Action _close;

    public StatsCard(Profile profile, Func<string, string> pearlName, Action close)
    {
        _profile = profile;
        _pearlName = pearlName;
        _close = close;
    }

    /// <summary>How each cause of death reads.</summary>
    public static string CauseName(string id) => id switch
    {
        "mob_shot" => "Pellet Dot's pellets",
        "boss_pearl" => "Queen Clam's pearls",
        "royal_pearl" => "Queen Clam's royal pearl",
        "boss_snap" => "Queen Clam's snap",
        "boss_contact" => "Queen Clam's shell",
        _ => "Something unseen",
    };

    public static string Reach(ProfileStats s) => s.BestDepth > 0 ? $"Depth {s.BestDepth} · Room {s.BestRoom}" : "—";

    public override void _Ready()
    {
        var style = TitleStyle.Box(TitleStyle.Pearl, TitleStyle.Coral, 3, 18, 28);
        style.ShadowColor = new Color(0.04f, 0.1f, 0.12f, 0.45f);
        style.ShadowSize = 24;
        style.ShadowOffset = new Vector2(0f, 10f);
        AddThemeStyleboxOverride("panel", style);
        var s = _profile.Stats;

        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        AddChild(scroll);
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddThemeConstantOverride("separation", 18);
        scroll.AddChild(box);

        var head = new HBoxContainer();
        box.AddChild(head);
        var title = TitleStyle.Text("Statistics", 40, TitleStyle.Ink, TitleStyle.Display);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        head.AddChild(title);
        var close = TitleStyle.Pill("Close  (Esc)", primary: false, size: 15);
        close.Pressed += _close;
        head.AddChild(close);

        // Headline tiles.
        var tiles = new HBoxContainer();
        tiles.AddThemeConstantOverride("separation", 14);
        box.AddChild(tiles);
        foreach (var (label, value) in new[]
                 {
                     ("Runs", s.Runs.ToString("N0")),
                     ("Best reach", Reach(s)),
                     ("Foes defeated", s.Kills.ToString("N0")),
                     ("Time in the sea", TitleStyle.Hours(s.PlaySeconds)),
                 })
        {
            var tile = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var tb = TitleStyle.Box(new Color(1f, 0.94f, 0.9f), new Color(1f, 0.86f, 0.82f), 2, 14, 14);
            tile.AddThemeStyleboxOverride("panel", tb);
            var col = new VBoxContainer();
            col.AddThemeConstantOverride("separation", 0);
            col.AddChild(TitleStyle.Text(value, value.Length > 10 ? 30 : 44, TitleStyle.Ink, TitleStyle.Display));
            col.AddChild(TitleStyle.Text(label, 14, TitleStyle.InkSoft, TitleStyle.BodyBold));
            tile.AddChild(col);
            tiles.AddChild(tile);
        }

        // Groups, two columns.
        var groups = new GridContainer { Columns = 2 };
        groups.AddThemeConstantOverride("h_separation", 40);
        groups.AddThemeConstantOverride("v_separation", 20);
        box.AddChild(groups);
        groups.AddChild(Group("Runs", new()
        {
            ("Runs started", s.Runs.ToString("N0"), false),
            ("Seeded runs", s.SeededRuns.ToString("N0"), false),
            ("Rooms cleared", s.RoomsCleared.ToString("N0"), false),
            ("Deaths", s.Deaths.ToString("N0"), false),
            ("Runs abandoned", s.Abandoned.ToString("N0"), false),
            ("Longest run", s.LongestRunSeconds > 0 ? TitleStyle.Clock(s.LongestRunSeconds) : "—", s.LongestRunSeconds > 0),
            ("Fastest room", s.FastestRoomSeconds > 0 ? TitleStyle.Clock(s.FastestRoomSeconds) : "—", s.FastestRoomSeconds > 0),
        }));
        double bossBest = _profile.Creatures.GetValueOrDefault(PlaneProfileRecorder.QueenClamId)?.BestSeconds ?? 0;
        groups.AddChild(Group("Combat", new()
        {
            ("Bubbles thrown", s.BubblesThrown.ToString("N0"), false),
            ("Full bubbles made (15 merged)", s.FullBubbles.ToString("N0"), false),
            ("Biggest volley", s.BiggestVolley > 0 ? $"{s.BiggestVolley} bubbles" : "—", s.BiggestVolley > 0),
            ("Damage dealt", s.DamageDealt.ToString("N0"), false),
            ("Damage taken", s.DamageTaken.ToString("N0"), false),
            ("Bosses freed", s.BossesFreed.ToString("N0"), false),
            ("Fastest boss", bossBest > 0 ? $"{TitleStyle.Clock(bossBest)} (Queen Clam)" : "—", bossBest > 0),
        }));
        string favourite = _profile.Pearls.Where(kv => kv.Value.Runs > 0).OrderByDescending(kv => kv.Value.Runs).ThenBy(kv => kv.Key)
            .Select(kv => $"{_pearlName(kv.Key)} ({kv.Value.Runs} runs)").FirstOrDefault() ?? "—";
        groups.AddChild(Group("Treasure", new()
        {
            ("Shells collected", s.ShellsCollected.ToString("N0"), false),
            ("Shells spent", s.ShellsSpent.ToString("N0"), false),
            ("Pearls absorbed", s.PearlsAbsorbed.ToString("N0"), false),
            ("Favourite pearl", favourite, false),
        }));
        groups.AddChild(Group("Feats", new()
        {
            ("Achievements earned", $"{_profile.Achievements.Count}", false),
            ("Most shells in a room", s.MostShellsInRoom.ToString("N0"), s.MostShellsInRoom > 0),
            ("Rooms without a scratch", s.UntouchableRooms.ToString("N0"), false),
        }));

        // How runs ended.
        box.AddChild(TitleStyle.Text("How runs ended", 22, TitleStyle.Ink, TitleStyle.Display));
        if (s.DeathsByCause.Count == 0)
        {
            box.AddChild(TitleStyle.Text("No run has ended yet. Keep it that way.", 16, TitleStyle.InkSoft));
            return;
        }
        int most = s.DeathsByCause.Values.Max();
        foreach (var (cause, count) in s.DeathsByCause.OrderByDescending(kv => kv.Value))
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 12);
            var name = TitleStyle.Text(CauseName(cause), 15, TitleStyle.Ink, TitleStyle.BodyBold);
            name.CustomMinimumSize = new Vector2(240f, 0f);
            row.AddChild(name);
            row.AddChild(new Bar(count / (float)most) { CustomMinimumSize = new Vector2(520f, 18f), SizeFlagsVertical = SizeFlags.ShrinkCenter });
            row.AddChild(TitleStyle.Text(count.ToString("N0"), 15, TitleStyle.Ink, TitleStyle.BodyBold));
            box.AddChild(row);
        }
    }

    static Control Group(string title, List<(string Label, string Value, bool Best)> rows)
    {
        var col = new VBoxContainer { CustomMinimumSize = new Vector2(450f, 0f) };
        col.AddThemeConstantOverride("separation", 6);
        col.AddChild(TitleStyle.Text(title.ToUpperInvariant(), 13, TitleStyle.Coral, TitleStyle.BodyBold));
        foreach (var (label, value, best) in rows)
        {
            var row = new HBoxContainer();
            var l = TitleStyle.Text(label, 16, TitleStyle.InkSoft);
            l.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(l);
            if (best)
            {
                var chip = new PanelContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
                var cb = TitleStyle.Box(TitleStyle.Butter, TitleStyle.Butter, 0, 999);
                cb.ContentMarginLeft = cb.ContentMarginRight = 7;
                chip.AddThemeStyleboxOverride("panel", cb);
                chip.AddChild(TitleStyle.Text("BEST", 10, TitleStyle.Ink, TitleStyle.BodyBold));
                row.AddChild(chip);
            }
            row.AddChild(TitleStyle.Text(value, 16, TitleStyle.Ink, TitleStyle.BodyBold));
            col.AddChild(row);
            col.AddChild(new ColorRect { Color = TitleStyle.Rule, CustomMinimumSize = new Vector2(0f, 1f), MouseFilter = MouseFilterEnum.Ignore });
        }
        return col;
    }

    /// <summary>A coral bar on a pale track.</summary>
    partial class Bar : Control
    {
        readonly float _k;
        public Bar(float k) => _k = Mathf.Clamp(k, 0f, 1f);

        public override void _Draw()
        {
            DrawRect(new Rect2(Vector2.Zero, Size), new Color(1f, 0.9f, 0.87f));
            DrawRect(new Rect2(Vector2.Zero, new Vector2(Size.X * _k, Size.Y)), TitleStyle.Coral);
        }
    }
}
