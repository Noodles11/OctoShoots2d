// The column of light a spring sends up through the water (docs/CORRUPTION.md): an open cylinder, brightest at its
// foot, fading upward, with soft streaks rising. bathing brightens it.
shader_type spatial;
render_mode blend_add, unshaded, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;

instance uniform float bathing = 0.0;
// Spent (its one washing used): it fades away, 1 → 0.
instance uniform float fade = 1.0;
uniform vec3 colour : source_color = vec3(0.6, 0.95, 1.0);

void fragment() {
	float up = 1.0 - UV.y;
	float streaks = 0.6 + 0.4 * sin(UV.x * 37.7 + sin(UV.x * 12.0) * 2.0) * sin(up * 6.0 - TIME * 2.2 + UV.x * 9.0);
	float edge = pow(1.0 - abs(dot(NORMAL, VIEW)), 1.5);
	float a = (1.0 - smoothstep(0.0, 1.0, up)) * streaks * (0.25 + 0.75 * edge) * (0.22 + 0.3 * bathing);
	ALBEDO = mix(colour, vec3(1.0, 0.88, 0.6), 0.35 + 0.3 * bathing) * a;
	ALPHA = fade;
}
