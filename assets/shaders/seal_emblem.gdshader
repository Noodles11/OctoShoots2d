// A secret room's seal (DESIGN-TOPDOWN §4.2): an X carved faintly in the wall, easy to miss. Her light striking it
// fills the carving from its crossing outward along both strokes; each strike flashes it. Drawn on a quad laid on the
// seal's face (SealView).
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled;

// How much of the carving is filled with light (0..1), a strike's flash (1 → 0), and its fading as the wall falls.
uniform float fill = 0.0;
uniform float flash = 0.0;
uniform float fade = 1.0;
uniform vec3 light_color : source_color = vec3(1.0, 0.72, 0.38);

float stroke(vec2 p, vec2 dir, out float along) {
	along = abs(dot(p, dir));
	return abs(dot(p, vec2(-dir.y, dir.x)));
}

void fragment() {
	vec2 p = UV * 2.0 - 1.0;
	float a1, a2;
	float d1 = stroke(p, normalize(vec2(1.0, 1.0)), a1);
	float d2 = stroke(p, normalize(vec2(1.0, -1.0)), a2);
	float len = 0.78;
	// Each stroke's groove, tapering to its ends; a little roughness so it reads as cut stone, not paint.
	float rough = 0.012 * sin(p.x * 37.0 + p.y * 23.0) + 0.01 * sin(p.y * 51.0 - p.x * 13.0);
	float w1 = mix(0.11, 0.05, a1 / len) + rough, w2 = mix(0.11, 0.05, a2 / len) + rough;
	float g1 = (1.0 - smoothstep(w1 * 0.6, w1, d1)) * (1.0 - smoothstep(len - 0.06, len, a1));
	float g2 = (1.0 - smoothstep(w2 * 0.6, w2, d2)) * (1.0 - smoothstep(len - 0.06, len, a2));
	float groove = max(g1, g2);
	// Filled from the crossing outward: a stroke is lit up to fill × its length, its front soft.
	float f = fill * len;
	float lit = max(g1 * (1.0 - smoothstep(f - 0.05, f + 0.02, a1)), g2 * (1.0 - smoothstep(f - 0.05, f + 0.02, a2))) * step(0.001, fill);
	// The carving itself: a faint shade in the stone (not too visible), and the light in it.
	vec3 shade = vec3(0.02, 0.05, 0.06);
	float shine = lit * (0.85 + 0.15 * sin(TIME * 3.0)) + flash * groove * 0.8;
	ALBEDO = mix(shade, light_color * 1.6, clamp(shine, 0.0, 1.0));
	// A soft halo round the lit carving.
	float halo = (1.0 - smoothstep(0.0, 0.35, min(d1, d2))) * fill * 0.25 + flash * 0.15 * (1.0 - smoothstep(0.0, 0.6, length(p)));
	ALPHA = clamp(groove * (0.32 + 0.68 * clamp(shine, 0.0, 1.0)) + halo, 0.0, 1.0) * fade;
}
