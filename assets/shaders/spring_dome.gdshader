// A light spring's dome (docs/CORRUPTION.md, SpringView): a force field like a great jellyfish bell lying over the
// emblem — clear and faint, no lines on it: only a soft sheen where it is seen edge-on, breathing slowly as a bell does
// (the margin drawing in, the dome rising). While she bathes it glows a little warmer.
shader_type spatial;
render_mode blend_add, unshaded, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;

instance uniform float bathing = 0.0;
// Spent (its one washing used): it fades away, 1 → 0.
instance uniform float fade = 1.0;
uniform vec3 colour : source_color = vec3(0.55, 0.92, 1.0);
uniform vec3 warm : source_color = vec3(1.0, 0.9, 0.7);

void vertex() {
	// UV.x: from the apex (0) to the margin (1).
	float breathe = 0.5 + 0.5 * sin(TIME * mix(1.1, 1.8, bathing));
	VERTEX.xz *= 1.0 - 0.035 * breathe * UV.x * UV.x;
	VERTEX.y *= 1.0 + 0.05 * breathe;
}

void fragment() {
	float fres = pow(1.0 - abs(dot(NORMAL, VIEW)), 3.0);
	// Softly gone at the margin, so it meets the sand without a line.
	float foot = 1.0 - smoothstep(0.8, 1.0, UV.x);
	float a = (0.015 + 0.22 * fres) * foot * (0.8 + 0.6 * bathing);
	ALBEDO = mix(colour, warm, 0.3 * bathing) * a;
	ALPHA = fade;
}
