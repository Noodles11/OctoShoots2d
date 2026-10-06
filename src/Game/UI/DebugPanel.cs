using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using OctoShoots.Core;
using OctoShoots.Core.Items;

namespace OctoShoots.Game.UI;

/// <summary>
/// F1 debug panel: live readout, buttons, the item picker (2D §27) and a live control for every
/// field marked [Tune], grouped by its group name.
/// </summary>
public partial class DebugPanel : PanelContainer
{
    readonly List<Action> _refreshers = new();
    VBoxContainer _list = null!;
    Label _readout = null!;

    /// <summary>Any tunable changed.</summary>
    public event Action? Changed;

    public void Build(string title)
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.RightWide);
        OffsetLeft = -480f;
        CustomMinimumSize = new Vector2(480f, 0f);
        AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.05f, 0.08f, 0.12f, 0.92f), ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 10, ContentMarginBottom = 10 });

        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        AddChild(scroll);
        _list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(_list);

        _list.AddChild(new Label { Text = title, Modulate = new Color(1f, 0.75f, 0.5f) });
        _readout = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _list.AddChild(_readout);
    }

    public void SetReadout(string text) => _readout.Text = text;

    /// <summary>Re-reads every control from its source (after Defaults, a reload or a restart).</summary>
    public void Refresh()
    {
        foreach (var r in _refreshers) r();
    }

    public void AddSection(string title)
    {
        _list.AddChild(new HSeparator());
        _list.AddChild(new Label { Text = title, Modulate = new Color(0.55f, 0.85f, 1f) });
    }

    public void AddButtons(params (string Text, Action OnPressed)[] buttons)
    {
        var row = new HBoxContainer();
        foreach (var (text, onPressed) in buttons)
        {
            var b = new Button { Text = text, SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipText = true };
            b.Pressed += onPressed;
            row.AddChild(b);
        }
        _list.AddChild(row);
    }

    /// <summary>A text field with an apply button.</summary>
    public void AddTextField(string label, Func<string> get, Action<string> apply, string button = "Apply")
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(90f, 0f) });
        var edit = new LineEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        edit.TextSubmitted += text => apply(text);
        row.AddChild(edit);
        var b = new Button { Text = button };
        b.Pressed += () => apply(edit.Text);
        row.AddChild(b);
        _refreshers.Add(() => edit.Text = get());
        _list.AddChild(row);
    }

    /// <summary>A dropdown bound to an index.</summary>
    public void AddOptions(string label, string[] options, Func<int> get, Action<int> set)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(90f, 0f) });
        var box = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var o in options) box.AddItem(o);
        box.ItemSelected += i => set((int)i);
        row.AddChild(box);
        _refreshers.Add(() => box.Select(get()));
        _list.AddChild(row);
    }

    /// <summary>Multi-select item picker: tick items, then Apply makes them exactly Clementine's set.</summary>
    public void AddItemPicker(IReadOnlyList<ItemDef> items, Func<IReadOnlyCollection<string>> owned, Action<List<string>> apply, Action random)
    {
        var boxes = new List<(ItemDef Item, CheckBox Box)>();
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var item in items.OrderBy(i => i.Kind).ThenBy(i => i.Name))
        {
            string caption = item.Tagline + "\n" + string.Join("\n", ItemCaption.Describe(item));
            var box = new CheckBox
            {
                Text = (item.Kind == ItemKind.Active ? "◆ " : "") + item.Name,
                TooltipText = caption,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                ClipText = true,
                CustomMinimumSize = new Vector2(200f, 0f),
            };
            boxes.Add((item, box));
            grid.AddChild(box);
        }
        AddButtons(
            ("Apply items", () => apply(boxes.Where(b => b.Box.ButtonPressed).Select(b => b.Item.Id).ToList())),
            ("Clear", () => apply(new List<string>())),
            ("Random treasure", random));
        _list.AddChild(grid);
        _refreshers.Add(() =>
        {
            var have = owned();
            foreach (var (item, box) in boxes) box.SetPressedNoSignal(have.Contains(item.Id));
        });
    }

    /// <summary>One control per [Tune] field of each target.</summary>
    public void AddTunables(params object[] targets)
    {
        foreach (var target in targets)
        {
            var fields = target.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Select(f => (Field: f, Tune: f.GetCustomAttribute<TuneAttribute>()))
                .Where(x => x.Tune is not null)
                .GroupBy(x => x.Tune!.Group);
            foreach (var group in fields)
            {
                AddSection(group.Key);
                foreach (var (field, tune) in group) _list.AddChild(Row(target, field, tune!));
            }
        }
    }

    /// <summary>One control per [Tune] field of the target, all under one heading.</summary>
    public void AddTunables(string heading, object target)
    {
        AddSection(heading);
        foreach (var field in target.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            if (field.GetCustomAttribute<TuneAttribute>() is { } tune) _list.AddChild(Row(target, field, tune));
    }

    Control Row(object target, FieldInfo field, TuneAttribute tune)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = field.Name, CustomMinimumSize = new Vector2(190f, 0f), ClipText = true });

        if (field.FieldType == typeof(bool))
        {
            var box = new CheckBox();
            box.Toggled += on =>
            {
                field.SetValue(target, on);
                Changed?.Invoke();
            };
            _refreshers.Add(() => box.SetPressedNoSignal((bool)field.GetValue(target)!));
            row.AddChild(box);
        }
        else if (field.FieldType.IsEnum)
        {
            var options = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var values = Enum.GetValues(field.FieldType);
            foreach (var v in values) options.AddItem(v.ToString());
            options.ItemSelected += i =>
            {
                field.SetValue(target, values.GetValue((int)i));
                Changed?.Invoke();
            };
            _refreshers.Add(() => options.Select(Array.IndexOf(values, field.GetValue(target))));
            row.AddChild(options);
        }
        else
        {
            bool isInt = field.FieldType == typeof(int);
            var slider = new HSlider
            {
                MinValue = tune.Min,
                MaxValue = tune.Max,
                Step = isInt ? 1 : (tune.Max - tune.Min) / 400.0,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
            };
            var value = new Label { CustomMinimumSize = new Vector2(60f, 0f), HorizontalAlignment = HorizontalAlignment.Right };
            slider.ValueChanged += v =>
            {
                if (isInt) field.SetValue(target, (int)Math.Round(v));
                else field.SetValue(target, (float)v);
                value.Text = isInt ? ((int)Math.Round(v)).ToString() : v.ToString("0.###");
                Changed?.Invoke();
            };
            _refreshers.Add(() =>
            {
                double v = isInt ? (int)field.GetValue(target)! : (float)field.GetValue(target)!;
                slider.SetValueNoSignal(v);
                value.Text = isInt ? ((int)v).ToString() : v.ToString("0.###");
            });
            row.AddChild(slider);
            row.AddChild(value);
        }
        return row;
    }
}
