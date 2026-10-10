using Godot;

namespace OctoShoots.Game.Controls;

/// <summary>
/// A controller in the menus, the same way the keyboard works them. While a control has focus, the menu actions
/// (InputSetup: confirm, up, down, left, right; A and the D-pad by default, rebindable for a pad Godot does not recognise)
/// and the left stick become Godot's own ui actions (ui_accept, ui_up/down/left/right): buttons press, sliders and spin
/// boxes change, check boxes toggle and focus moves in all four directions, through exactly the path the keyboard takes.
/// The pad's own events are kept from the GUI so nothing happens twice. B (back) is left to the screens. Nothing happens
/// without a focused control, so play is never touched. Every pad press is also noted for Settings (InputSetup.LastPad).
/// </summary>
public partial class PadMenus : Node
{
	/// <summary>Settings is capturing a new binding: the pad is left alone.</summary>
	public static bool Capturing { get; set; }

	/// <summary>The left stick moves focus once per push (pushed past Press, re-armed below Release).</summary>
	const float Press = 0.6f, Release = 0.3f;
	bool _stickX, _stickY;

	public override void _Ready() => ProcessMode = ProcessModeEnum.Always;

	public override void _Input(InputEvent e)
	{
		if (e is not (InputEventJoypadButton or InputEventJoypadMotion)) return;
		if (InputSetup.FromEvent(e, pad: true) is { } code)
		{
			InputSetup.LastPad = code;
			if (e is InputEventJoypadButton { Pressed: true } b)
				GD.Print($"Pad {b.Device} ({Input.GetJoyName(b.Device)}, {(Input.IsJoyKnown(b.Device) ? "recognised" : "not recognised")}): {code}");
		}
		if (Capturing || !HasFocus()) return;
		// The menu's pad input is ours: the GUI only sees the ui actions sent from _Process.
		foreach (var (menu, _) in InputSetup.MenuActions)
			if (e.IsAction(menu))
			{
				GetViewport().SetInputAsHandled();
				return;
			}
		if (e is InputEventJoypadMotion { Axis: JoyAxis.LeftX or JoyAxis.LeftY } m)
		{
			bool x = m.Axis == JoyAxis.LeftX;
			float v = m.AxisValue;
			bool held = x ? _stickX : _stickY;
			if (!held && Mathf.Abs(v) > Press)
			{
				held = true;
				Tap(x ? (v < 0f ? "ui_left" : "ui_right") : (v < 0f ? "ui_up" : "ui_down"));
			}
			else if (held && Mathf.Abs(v) < Release) held = false;
			if (x) _stickX = held;
			else _stickY = held;
			GetViewport().SetInputAsHandled();
		}
	}

	public override void _Process(double delta)
	{
		if (Capturing || !HasFocus()) return;
		foreach (var (menu, ui) in InputSetup.MenuActions)
			if (Input.IsActionJustPressed(menu)) Tap(ui);
	}

	bool HasFocus() => GetViewport().GuiGetFocusOwner() is { } focus && focus.IsVisibleInTree();

	/// <summary>A ui action pressed and released, sent after this frame's events, as the keyboard would send it.</summary>
	static void Tap(string action) => Callable.From(() =>
	{
		Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = true, Strength = 1f });
		Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = false });
	}).CallDeferred();
}
