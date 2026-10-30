// A clam room's pearl lighting the chamber (ClamRoomView): its own colours (pearl_glow.gdshaderinc)
// laid over the chamber's floor and the foot of its walls, added to them like light — only in a cone of 120° from the
// clam's mouth toward the chamber's entrance, softly edged; the clam's shell keeps every bit of it off what lies behind.
// A patch of the ground's own surface, lifted a hair off it, reaching well past the chamber's mouth; the light floods
// out through the mouth and fades away smoothly long before the patch ends (reach), so it is never cut off.
shader_type spatial;
render_mode blend_add, unshaded, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;

#include "res://assets/shaders/pearl_glow.gdshaderinc"

// The way the clam opens (world xz, unit), and the cone's half-angle and its soft edge (radians).
uniform vec2 facing = vec2(0.0, 1.0);
uniform float half_angle = 1.0472;
uniform float edge = 0.2;
// The patch's reach from the pearl (m): the light is gone by then, fading out over its outer part.
uniform float reach = 15.0;

void vertex() {
	glow_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec2 off = glow_world.xz - glow_pos.xz;
	float d = length(off);
	float ang = acos(clamp(dot(off / max(d, 1e-4), facing), -1.0, 1.0));
	float cone = 1.0 - smoothstep(half_angle - edge, half_angle + edge, ang);
	// Right at the clam the light leaves its lip, so the cone starts there.
	cone *= smoothstep(0.6, 1.4, d);
	cone *= 1.0 - smoothstep(reach * 0.45, reach * 0.95, d);
	ALBEDO = pearl_glow(glow_world) * cone * 0.9;
}
