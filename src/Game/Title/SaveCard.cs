using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using OctoShoots.Core.Saves;
using OctoShoots.Game.Settings;

namespace OctoShoots.Game.Title;

/// <summary>
/// Save &amp; load (proposal §4.5): the saved run (continue or abandon), a backup code to copy or import (with a preview
/// before anything is replaced), and erasing everything (guarded by typing ERASE).
/// </summary>
public partial class SaveCard : PanelContainer
{
    readonly ItemCatalog? _catalog;
    readonly Action _continue, _abandon, _changed, _close;
    VBoxContainer _box = null!;
    Label _toast = null!;
    float _toastTimer;

    public SaveCard(ItemCatalog? catalog, Action @continue, Action abandon, Action changed, Action close)
    {
        _catalog = catalog;
        _continue = @continue;
        _abandon = abandon;
        _changed = changed;
        _close = close;
    }

    public override void _Ready()
    {
        var style = TitleStyle.Box(TitleStyle.Pearl, TitleStyle.Coral, 3, 18, 28);
        style.ShadowColor = new Color(0.04f, 0.1f, 0.12f, 0.45f);
        style.ShadowSize = 24;
        style.ShadowOffset = new Vector2(0f, 10f);
        AddThemeStyleboxOverride("panel", style);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        AddChild(scroll);
        _box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _box.AddThemeConstantOverride("separation", 14);
        scroll.AddChild(_box);
        Build();
    }

    public override void _Process(double delta)
    {
        if (_toastTimer <= 0f) return;
        _toastTimer -= (float)delta;
        _toast.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(_toastTimer * 2f, 0f, 1f));
    }

    void Toast(string text)
    {
        _toast.Text = text;
        _toastTimer = 2.5f;
    }

    void Build()
    {
        foreach (var child in _box.GetChildren()) child.QueueFree();
        var save = GameSave.Current;

        var head = new HBoxContainer();
        _box.AddChild(head);
        var title = TitleStyle.Text("Save & load", 40, TitleStyle.Ink, TitleStyle.Display);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        head.AddChild(title);
        _toast = TitleStyle.Text("", 15, new Color(0.1f, 0.45f, 0.36f), TitleStyle.BodyBold);
        head.AddChild(_toast);
        var close = TitleStyle.Pill("Close  (Esc)", primary: false, size: 15);
        close.Pressed += _close;
        head.AddChild(close);

        if (SaveStore.LastLoadFailed)
            _box.AddChild(Notice("The save on disk could not be read, so a fresh profile was started. The old file was kept as save.bad.json."));

        // 1. The run.
        _box.AddChild(Heading("Your run"));
        if (save.Run is { } run) _box.AddChild(RunSlot(run));
        else _box.AddChild(Note("No saved run. A run is saved at the start of every room, and when you choose Save & quit in the pause menu."));

        // 2. Backup.
        _box.AddChild(Heading("Backup"));
        _box.AddChild(Note("The save code holds everything: your profile and your run. Keep it somewhere safe, or paste it on another computer."));
        var copy = TitleStyle.Pill("Copy save code", primary: false, size: 16);
        copy.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        copy.Pressed += () =>
        {
            DisplayServer.ClipboardSet(SaveCodec.Export(GameSave.Current));
            Toast("Save code copied");
        };
        _box.AddChild(copy);
        _box.AddChild(ImportRow());

        // 3. Profile.
        _box.AddChild(Heading("Profile"));
        _box.AddChild(EraseRow());
        var path = TitleStyle.Text($"Saved at {SaveStore.GlobalPath}", 12, TitleStyle.InkSoft, TitleStyle.Mono);
        path.AutowrapMode = TextServer.AutowrapMode.Arbitrary;
        _box.AddChild(path);
    }

    static Label Heading(string text) => TitleStyle.Text(text.ToUpperInvariant(), 13, TitleStyle.Coral, TitleStyle.BodyBold);

    static Label Note(string text)
    {
        var l = TitleStyle.Text(text, 15, TitleStyle.InkSoft);
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        l.CustomMinimumSize = new Vector2(860f, 0f);
        return l;
    }

    static Control Notice(string text)
    {
        var p = new PanelContainer();
        p.AddThemeStyleboxOverride("panel", TitleStyle.Box(new Color(1f, 0.9f, 0.87f), TitleStyle.Coral, 2, 12, 12));
        var l = TitleStyle.Text(text, 15, TitleStyle.Ink, TitleStyle.BodyBold);
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        p.AddChild(l);
        return p;
    }

    Control RunSlot(SuspendedRun run)
    {
        var slot = new PanelContainer();
        slot.AddThemeStyleboxOverride("panel", TitleStyle.Box(new Color(1f, 0.94f, 0.9f), new Color(1f, 0.82f, 0.78f), 2, 14, 18));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 24);
        slot.AddChild(row);

        var info = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        info.AddThemeConstantOverride("separation", 4);
        row.AddChild(info);
        info.AddChild(TitleStyle.Text($"Depth {run.Depth} · Room {run.Room}", 30, TitleStyle.Ink, TitleStyle.Display));
        info.AddChild(TitleStyle.Text($"{run.Seed}{(run.CustomSeed ? "  (seeded)" : "")}", 18, TitleStyle.Ink, TitleStyle.Mono));
        info.AddChild(TitleStyle.Text($"{Mathf.CeilToInt(run.Hp)} HP  ·  {run.Shells} shells  ·  {TitleStyle.Clock(run.Elapsed)} played  ·  saved {When(run.SavedAt)}", 15, TitleStyle.InkSoft));
        var pearls = new HBoxContainer();
        pearls.AddThemeConstantOverride("separation", 6);
        foreach (string id in run.Items)
        {
            ItemDef? item = null;
            _catalog?.TryGet(id, out item);
            pearls.AddChild(MiniPearl(item));
        }
        if (run.Items.Count == 0) pearls.AddChild(TitleStyle.Text("No pearls yet", 14, TitleStyle.InkSoft));
        info.AddChild(pearls);

        var buttons = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 10);
        row.AddChild(buttons);
        var go = TitleStyle.Pill("Continue", size: 18);
        go.Pressed += _continue;
        buttons.AddChild(go);
        var abandon = TitleStyle.Pill("Abandon run", primary: false, size: 15);
        buttons.AddChild(abandon);
        abandon.Pressed += () =>
        {
            abandon.Visible = false;
            var confirm = new HBoxContainer();
            confirm.AddThemeConstantOverride("separation", 8);
            var yes = TitleStyle.Pill("Abandon", size: 14);
            var no = TitleStyle.Pill("Keep it", primary: false, size: 14);
            yes.Pressed += () =>
            {
                _abandon();
                Build();
                _changed();
            };
            no.Pressed += () =>
            {
                confirm.QueueFree();
                abandon.Visible = true;
            };
            confirm.AddChild(yes);
            confirm.AddChild(no);
            buttons.AddChild(confirm);
            no.GrabFocus();
        };
        return slot;
    }

    static string When(string iso)
    {
        if (!DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t)) return "earlier";
        var today = DateTime.Today;
        string day = t.Date == today ? "today" : t.Date == today.AddDays(-1) ? "yesterday" : t.ToString("d MMM", CultureInfo.InvariantCulture);
        return $"{day}, {t:HH:mm}";
    }

    static ColorRect MiniPearl(ItemDef? item)
    {
        var mat = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/pearl_icon.gdshader") };
        var colors = item?.Pearl?.Colors ?? new List<string> { "#ffffff", "#dddddd" };
        mat.SetShaderParameter("c0", Color.FromHtml(colors[0]));
        mat.SetShaderParameter("c1", Color.FromHtml(colors[Math.Min(1, colors.Count - 1)]));
        mat.SetShaderParameter("c2", Color.FromHtml(colors[^1]));
        mat.SetShaderParameter("pattern", (int)(item?.Pearl?.Pattern ?? PearlPattern.Halo));
        return new ColorRect { CustomMinimumSize = new Vector2(26f, 26f), Material = mat, TooltipText = item?.Name ?? "" };
    }

    Control ImportRow()
    {
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 8);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        col.AddChild(row);
        var field = TitleStyle.Field("Paste a save code (INKD2:…)", TitleStyle.Mono, 15);
        field.CustomMinimumSize = new Vector2(640f, 0f);
        row.AddChild(field);
        var check = TitleStyle.Pill("Check code", primary: false, size: 15);
        row.AddChild(check);
        var result = new VBoxContainer();
        col.AddChild(result);
        void Check()
        {
            foreach (var c in result.GetChildren()) c.QueueFree();
            SaveFile incoming;
            try
            {
                incoming = SaveCodec.Import(field.Text);
            }
            catch (SaveException e)
            {
                result.AddChild(TitleStyle.Text($"That code can't be used: {e.Message}. Nothing was changed.", 15, TitleStyle.Coral, TitleStyle.BodyBold));
                return;
            }
            var s = incoming.Profile.Stats;
            int pearls = PlaneRun.ShotPearls.Count(id => incoming.Profile.SeenItems.Contains(id));
            string run = incoming.Run is { } r ? $"a saved run at Depth {r.Depth} · Room {r.Room}" : "no saved run";
            result.AddChild(TitleStyle.Text($"This save has {s.Runs} runs, {pearls} pearls found, {incoming.Profile.Achievements.Count} achievements and {run}.", 15, TitleStyle.Ink, TitleStyle.BodyBold));
            var replace = TitleStyle.Pill("Replace my save", size: 15);
            replace.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
            replace.Pressed += () =>
            {
                GameSave.Replace(incoming);
                Build();
                _changed();
                Toast("Save imported");
            };
            result.AddChild(replace);
        }
        check.Pressed += Check;
        field.TextSubmitted += _ => Check();
        return col;
    }

    Control EraseRow()
    {
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 8);
        col.AddChild(Note("Erasing clears your profile (statistics, Sea-pedia, achievements) and your saved run. It can't be undone. Copy a save code first if you might want it back."));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        col.AddChild(row);
        var field = TitleStyle.Field("Type ERASE to confirm", TitleStyle.Mono, 15);
        field.CustomMinimumSize = new Vector2(280f, 0f);
        row.AddChild(field);
        var erase = TitleStyle.Pill("Erase everything", size: 15);
        erase.Disabled = true;
        row.AddChild(erase);
        field.TextChanged += t => erase.Disabled = t.Trim() != "ERASE";
        erase.Pressed += () =>
        {
            GameSave.Erase();
            Build();
            _changed();
            Toast("Everything erased");
        };
        return col;
    }
}
