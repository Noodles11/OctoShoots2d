using Godot;

namespace OctoShoots.Game.Util;

public static class Conv
{
	public static Vector3 G(this System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);

	public static System.Numerics.Vector3 N(this Vector3 v) => new(v.X, v.Y, v.Z);

	public static Vector3 Lerp(System.Numerics.Vector3 a, System.Numerics.Vector3 b, float t) =>
		System.Numerics.Vector3.Lerp(a, b, t).G();

	/// <summary>A basis whose -Z points along dir (Godot's forward), robust to vertical directions.</summary>
	public static Basis LookAlong(Vector3 dir)
	{
		if (dir.LengthSquared() < 1e-8f) return Basis.Identity;
		dir = dir.Normalized();
		Vector3 up = Mathf.Abs(dir.Y) > 0.98f ? Vector3.Right : Vector3.Up;
		return Basis.LookingAt(dir, up);
	}

	/// <summary>A basis whose +Y points along the given normal, spun around it by angle.</summary>
	public static Basis AlignUp(Vector3 normal, float spin)
	{
		normal = normal.Normalized();
		Vector3 helper = Mathf.Abs(normal.Y) > 0.9f ? Vector3.Right : Vector3.Up;
		Vector3 x = helper.Cross(normal).Normalized();
		Vector3 z = x.Cross(normal).Normalized();
		return new Basis(x, normal, z).Rotated(normal, spin);
	}
}
