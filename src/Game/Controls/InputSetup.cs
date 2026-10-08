using Godot;

namespace OctoShoots.Game.Controls;

/// <summary>Registers the default bindings (§5). Actions live in the InputMap so they can be rebound later.</summary>
public static class InputSetup
{
	public const string Forward = "move_forward";
	public const string Back = "move_back";
	public const string Left = "move_left";
	public const string Right = "move_right";
	public const string Fire = "fire";
	public const string Dash = "dash";
	public const string Active = "use_active";
	public const string Bomb = "ink_bomb";
	public const string Review = "review_pearls";
	public const string Pause = "pause";
	public const string Restart = "restart";
	public const string DebugPanel = "debug_panel";

	public static void Register()
	{
		Key(Forward, Godot.Key.W);
		Key(Back, Godot.Key.S);
		Key(Left, Godot.Key.A);
		Key(Right, Godot.Key.D);
		Key(Dash, Godot.Key.Space);
		Key(Active, Godot.Key.F);
		Key(Bomb, Godot.Key.E);
		Key(Review, Godot.Key.Tab);
		Key(Pause, Godot.Key.Escape);
		Key(Restart, Godot.Key.R);
		Key(DebugPanel, Godot.Key.F1);
		Mouse(Fire, MouseButton.Left);
	}

	static void Key(string action, Key key)
	{
		Ensure(action);
		InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
	}

	static void Mouse(string action, MouseButton button)
	{
		Ensure(action);
		InputMap.ActionAddEvent(action, new InputEventMouseButton { ButtonIndex = button });
	}

	static void Ensure(string action)
	{
		if (InputMap.HasAction(action)) InputMap.EraseAction(action);
		InputMap.AddAction(action, 0.2f);
	}
}
