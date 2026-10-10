using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

namespace OctoShoots.Game.Controls;

/// <summary>
/// The bindings (§5): every action in the InputMap, with one keyboard/mouse binding and one controller binding each.
/// Both are rebindable in Settings and saved to user://bindings.json. Controller defaults: left stick swims, right stick
/// aims, RT shoots, LT dashes, X dives, Y uses the active pearl, View (Back) holds the level map, Start pauses (Start or
/// B closes it). Menus: the D-pad moves, A confirms, B goes back (rebindable too). The debug keys (F1, F3, R) and typing
/// a seed are keyboard only.
/// </summary>
public static class InputSetup
{
	public const string Forward = "move_forward";
	public const string Back = "move_back";
	public const string Left = "move_left";
	public const string Right = "move_right";
	public const string AimUp = "aim_up";
	public const string AimDown = "aim_down";
	public const string AimLeft = "aim_left";
	public const string AimRight = "aim_right";
	public const string Fire = "fire";
	public const string Dash = "dash";
	public const string Dive = "dive";
	public const string Active = "use_active";
	public const string Bomb = "ink_bomb";
	public const string Review = "review_pearls";
	public const string Pause = "pause";
	public const string Restart = "restart";
	public const string DebugPanel = "debug_panel";
	/// <summary>Menus on a controller: A confirms (a splash, a prompt), B goes back (closes the pause menu, a card).</summary>
	public const string MenuAccept = "menu_accept";
	public const string MenuBack = "menu_back";
	/// <summary>Menus on a controller: moving the focus (the D-pad by default; the keyboard keeps its arrows).</summary>
	public const string MenuUp = "menu_up";
	public const string MenuDown = "menu_down";
	public const string MenuLeft = "menu_left";
	public const string MenuRight = "menu_right";

	/// <summary>A rebindable action: its name in Settings, its default keyboard/mouse and controller bindings.</summary>
	public sealed record Spec(string Action, string Name, string Key, string Pad, bool KeyLocked = false);

	public static readonly Spec[] Bindable =
	{
		new(Forward, "Swim up", "key:W", "axis:LeftY:-"),
		new(Back, "Swim down", "key:S", "axis:LeftY:+"),
		new(Left, "Swim left", "key:A", "axis:LeftX:-"),
		new(Right, "Swim right", "key:D", "axis:LeftX:+"),
		new(AimUp, "Aim up", "key:Up", "axis:RightY:-"),
		new(AimDown, "Aim down", "key:Down", "axis:RightY:+"),
		new(AimLeft, "Aim left", "key:Left", "axis:RightX:-"),
		new(AimRight, "Aim right", "key:Right", "axis:RightX:+"),
		new(Fire, "Shoot (hold)", "mouse:Left", "axis:TriggerRight:+"),
		new(Dash, "Dash", "key:Space", "axis:TriggerLeft:+"),
		new(Dive, "Dive down the shaft", "key:Shift", "button:X"),
		new(Active, "Use the active pearl", "key:F", "button:Y"),
		new(Review, "Level map (hold)", "key:Tab", "button:Back"),
		// Esc always pauses (and cancels a rebind), so its key stays.
		new(Pause, "Pause", "key:Escape", "button:Start", KeyLocked: true),
		// The menus on a controller (the keyboard's are fixed: arrows, Enter, Esc). Rebindable for a pad Godot does not
		// recognise (an Xbox controller over Bluetooth can arrive with its buttons numbered differently).
		new(MenuAccept, "Menu: confirm", "", "button:A", KeyLocked: true),
		new(MenuBack, "Menu: back", "", "button:B", KeyLocked: true),
		new(MenuUp, "Menu: up", "", "button:DpadUp", KeyLocked: true),
		new(MenuDown, "Menu: down", "", "button:DpadDown", KeyLocked: true),
		new(MenuLeft, "Menu: left", "", "button:DpadLeft", KeyLocked: true),
		new(MenuRight, "Menu: right", "", "button:DpadRight", KeyLocked: true),
	};

	/// <summary>The menu actions and the Godot ui action each one drives.</summary>
	public static readonly (string Menu, string Ui)[] MenuActions =
	{
		(MenuAccept, "ui_accept"), (MenuUp, "ui_up"), (MenuDown, "ui_down"), (MenuLeft, "ui_left"), (MenuRight, "ui_right"),
	};

	/// <summary>A controller's last press, for Settings (what the pad really sends).</summary>
	public static string? LastPad { get; set; }

	const string FilePath = "user://bindings.json";
	static readonly Dictionary<string, (string? Key, string? Pad)> Current = new();

	/// <summary>Sets up every action: the saved bindings (or the defaults), the keyboard-only keys and the menu buttons.</summary>
	public static void Register()
	{
		Load();
		foreach (var spec in Bindable) Apply(spec.Action);
		KeyOnly(Bomb, Godot.Key.E);
		KeyOnly(Restart, Godot.Key.R);
		KeyOnly(DebugPanel, Godot.Key.F1);
		LoadMappings();
		// Godot's own menu navigation: the D-pad moves focus, A presses, B cancels.
		AddIfMissing("ui_up", new InputEventJoypadButton { ButtonIndex = JoyButton.DpadUp, Device = -1 });
		AddIfMissing("ui_down", new InputEventJoypadButton { ButtonIndex = JoyButton.DpadDown, Device = -1 });
		AddIfMissing("ui_left", new InputEventJoypadButton { ButtonIndex = JoyButton.DpadLeft, Device = -1 });
		AddIfMissing("ui_right", new InputEventJoypadButton { ButtonIndex = JoyButton.DpadRight, Device = -1 });
		AddIfMissing("ui_accept", new InputEventJoypadButton { ButtonIndex = JoyButton.A, Device = -1 });
		AddIfMissing("ui_cancel", new InputEventJoypadButton { ButtonIndex = JoyButton.B, Device = -1 });
	}

	/// <summary>
	/// Extra controller mappings: user://gamecontrollerdb.txt, one SDL mapping per line (SDL_GameControllerDB format),
	/// for a pad Godot's own list does not know.
	/// </summary>
	static void LoadMappings()
	{
		const string path = "user://gamecontrollerdb.txt";
		if (!FileAccess.FileExists(path)) return;
		using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		foreach (string line in (file?.GetAsText() ?? "").Split('\n'))
		{
			string mapping = line.Trim();
			if (mapping.Length > 0 && !mapping.StartsWith('#')) Input.AddJoyMapping(mapping, updateExisting: true);
		}
	}

	/// <summary>The connected controllers: name, whether Godot recognises its layout, and its GUID.</summary>
	public static IEnumerable<string> PadReport()
	{
		foreach (int device in Input.GetConnectedJoypads())
			yield return $"{Input.GetJoyName(device)} — {(Input.IsJoyKnown(device) ? "recognised" : "NOT recognised: its buttons may be numbered differently, rebind them below")} · GUID {Input.GetJoyGuid(device)}";
	}

	public static Spec SpecOf(string action) => Bindable.First(s => s.Action == action);

	/// <summary>The binding in use (null: none).</summary>
	public static string? Get(string action, bool pad)
	{
		var (key, padCode) = Current.TryGetValue(action, out var b) ? b : (SpecOf(action).Key, SpecOf(action).Pad);
		return pad ? padCode : key;
	}

	/// <summary>Rebinds one slot (null clears it), applies it at once and saves.</summary>
	public static void Set(string action, bool pad, string? code)
	{
		var (key, padCode) = Current.TryGetValue(action, out var b) ? b : (SpecOf(action).Key, SpecOf(action).Pad);
		if (pad) padCode = code;
		else if (!SpecOf(action).KeyLocked) key = code;
		Current[action] = (key, padCode);
		Apply(action);
		Save();
	}

	public static void ResetAll()
	{
		Current.Clear();
		foreach (var spec in Bindable) Apply(spec.Action);
		Save();
	}

	static void Apply(string action)
	{
		bool stick = action is Forward or Back or Left or Right or AimUp or AimDown or AimLeft or AimRight;
		Ensure(action, stick ? 0.2f : 0.35f);
		foreach (string? code in new[] { Get(action, pad: false), Get(action, pad: true) })
			if (ToEvent(code) is { } e) InputMap.ActionAddEvent(action, e);
	}

	// ───────────── the binding codes: "key:W", "mouse:Left", "button:X", "axis:LeftY:-" ─────────────

	public static InputEvent? ToEvent(string? code)
	{
		if (string.IsNullOrEmpty(code)) return null;
		var parts = code.Split(':');
		try
		{
			return parts[0] switch
			{
				"key" => new InputEventKey { PhysicalKeycode = Enum.Parse<Key>(parts[1]) },
				"mouse" => new InputEventMouseButton { ButtonIndex = Enum.Parse<MouseButton>(parts[1]) },
				"button" => new InputEventJoypadButton { ButtonIndex = Enum.Parse<JoyButton>(parts[1]), Device = -1 },
				"axis" => new InputEventJoypadMotion { Axis = Enum.Parse<JoyAxis>(parts[1]), AxisValue = parts[2] == "-" ? -1f : 1f, Device = -1 },
				_ => null,
			};
		}
		catch (Exception)
		{
			GD.PushWarning($"Ignoring unknown binding '{code}'");
			return null;
		}
	}

	/// <summary>A pressed key or mouse button (keyboard slot), or a pressed pad button or a pushed stick/trigger (pad slot).</summary>
	public static string? FromEvent(InputEvent e, bool pad) => (e, pad) switch
	{
		(InputEventKey { Pressed: true, Echo: false } k, false) => $"key:{(k.PhysicalKeycode != Key.None ? k.PhysicalKeycode : k.Keycode)}",
		(InputEventMouseButton { Pressed: true } m, false) when m.ButtonIndex is MouseButton.Left or MouseButton.Right or MouseButton.Middle
			or MouseButton.Xbutton1 or MouseButton.Xbutton2 => $"mouse:{m.ButtonIndex}",
		(InputEventJoypadButton { Pressed: true } b, true) => $"button:{b.ButtonIndex}",
		(InputEventJoypadMotion m, true) when Mathf.Abs(m.AxisValue) > 0.6f => $"axis:{m.Axis}:{(m.AxisValue < 0f ? "-" : "+")}",
		_ => null,
	};

	/// <summary>How a binding reads in Settings.</summary>
	public static string Describe(string? code)
	{
		if (string.IsNullOrEmpty(code)) return "—";
		var p = code.Split(':');
		return p[0] switch
		{
			"key" => Enum.TryParse<Key>(p[1], out var key) ? OS.GetKeycodeString(key) : p[1],
			"mouse" => p[1] switch { "Left" => "Left click", "Right" => "Right click", "Middle" => "Middle click", _ => "Mouse " + p[1] },
			"button" => p[1] switch
			{
				"Back" => "View (Back)", "Start" => "Start (Menu)", "Guide" => "Guide", "LeftShoulder" => "LB", "RightShoulder" => "RB",
				"LeftStick" => "LS (press)", "RightStick" => "RS (press)", "DpadUp" => "D-pad up", "DpadDown" => "D-pad down",
				"DpadLeft" => "D-pad left", "DpadRight" => "D-pad right",
				_ => int.TryParse(p[1], out int raw) ? $"Button {raw}" : p[1],
			},
			"axis" => p[1] switch
			{
				"TriggerLeft" => "LT", "TriggerRight" => "RT",
				"LeftX" => p[2] == "-" ? "Left stick ←" : "Left stick →",
				"LeftY" => p[2] == "-" ? "Left stick ↑" : "Left stick ↓",
				"RightX" => p[2] == "-" ? "Right stick ←" : "Right stick →",
				"RightY" => p[2] == "-" ? "Right stick ↑" : "Right stick ↓",
				_ => int.TryParse(p[1], out int raw) ? $"Axis {raw} {p[2]}" : code,
			},
			_ => code,
		};
	}

	// ───────────── saving ─────────────

	static void Load()
	{
		Current.Clear();
		if (!FileAccess.FileExists(FilePath)) return;
		try
		{
			using var file = FileAccess.Open(FilePath, FileAccess.ModeFlags.Read);
			var saved = JsonSerializer.Deserialize<Dictionary<string, string?[]>>(file?.GetAsText() ?? "{}");
			if (saved is null) return;
			foreach (var (action, slots) in saved)
				if (Bindable.Any(s => s.Action == action) && slots.Length == 2)
					Current[action] = (SpecOf(action).KeyLocked ? SpecOf(action).Key : slots[0], slots[1]);
		}
		catch (Exception e)
		{
			GD.PushWarning($"Ignoring unreadable {FilePath}: {e.Message}");
			Current.Clear();
		}
	}

	static void Save()
	{
		var data = Bindable.ToDictionary(s => s.Action, s => new[] { Get(s.Action, pad: false), Get(s.Action, pad: true) });
		using var file = FileAccess.Open(FilePath, FileAccess.ModeFlags.Write);
		file?.StoreString(JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
	}

	// ───────────── the InputMap ─────────────

	static void KeyOnly(string action, Key key)
	{
		Ensure(action, 0.5f);
		InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
	}

	static void AddIfMissing(string action, InputEvent e)
	{
		if (!InputMap.HasAction(action)) InputMap.AddAction(action);
		if (!InputMap.ActionHasEvent(action, e)) InputMap.ActionAddEvent(action, e);
	}

	static void Ensure(string action, float deadzone)
	{
		if (InputMap.HasAction(action)) InputMap.EraseAction(action);
		InputMap.AddAction(action, deadzone);
	}
}
