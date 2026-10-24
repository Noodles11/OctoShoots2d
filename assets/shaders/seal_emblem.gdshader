// A sealed wall's carving (DESIGN-TOPDOWN §4.2; SealView): an X carved faintly in the wall, easy to miss (shape 0: a
// secret's, a tunnel's), or a sword (shape 1: the way into the boss's arena), its crimson smouldering in the groove
// (ember). Her light striking it fills the carving from its heart outward (the X's crossing, the sword's guard); each
// strike flashes it. Drawn on a patch of the wall's own surface (SealView.Carve), laid out as seen from above; its
// vertex alpha keeps it to the rock (none on the seabed at the wall's foot).
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled;

// How much of the carving is filled with light (0..1), a strike's flash (1 → 0), and its fading as the wall falls.
uniform float fill = 0.0;
uniform float flash = 0.0;
uniform float fade = 1.0;
uniform vec3 light_color : source_color = vec3(1.0, 0.72, 0.38);
uniform int shape = 0;
uniform float ember = 0.0;

/// Distance to a segment from a to b.
float seg(vec2 p, vec2 a, vec2 b) {
	vec2 pa = p - a, ba = b - a;
	float h = clamp(dot(pa, ba) / dot(ba, ba), 0.0, 1.0);
	return length(pa - ba * h);
}

float stroke(vec2 p, vec2 dir, out float along) {
	along = abs(dot(p, dir));
	return abs(dot(p, vec2(-dir.y, dir.x)));
}

void sword(vec2 p, out float groove, out float lit_at) {
	// Blade up (a point at its tip, tapering), a crossguard, a grip, a pommel; carved, a little rough.
	float rough = 0.01 * sin(p.x * 41.0 + p.y * 29.0) + 0.008 * sin(p.y * 57.0 - p.x * 17.0);
	vec2 guard_c = vec2(0.0, -0.22);
	float taper = clamp((p.y + 0.18) / 0.98, 0.0, 1.0);
	float blade = abs(p.x) - mix(0.085, 0.0, taper * taper) - rough;
	blade = max(blade, max(-(p.y + 0.18), p.y - 0.8));
	float fuller = abs(p.x) - 0.02;
	float g_blade = 1.0 - smoothstep(0.0, 0.025, blade);
	float g_guard = 1.0 - smoothstep(0.045, 0.07, seg(p, vec2(-0.36, -0.22), vec2(0.36, -0.22)) - rough);
	float g_grip = 1.0 - smoothstep(0.04, 0.06, seg(p, vec2(0.0, -0.26), vec2(0.0, -0.56)) - rough);
	float g_pommel = 1.0 - smoothstep(0.06, 0.085, length(p - vec2(0.0, -0.63)) - rough);
	// The blade's groove is its edge and its fuller (cut lines, not a solid shape).
	float edge = g_blade * (1.0 - smoothstep(0.01, 0.03, -blade)) + g_blade * (1.0 - smoothstep(0.008, 0.02, abs(fuller))) * step(p.y, 0.62);
	groove = clamp(max(max(edge, g_guard), max(g_grip, g_pommel)), 0.0, 1.0);
	lit_at = length(p - guard_c);
}

void fragment() {
	vec2 p = UV * 2.0 - 1.0;
	if (shape == 1) {
		float groove, at;
		sword(p, groove, at);
		// Filled from the guard outward (to its tip, its pommel, its quillons).
		float f = fill * 1.05;
		float lit = groove * (1.0 - smoothstep(f - 0.06, f + 0.02, at)) * step(0.001, fill);
		// The crimson smoulders in the groove even unlit, breathing, so the way to the boss is marked.
		float smoulder = ember * groove * (0.22 + 0.12 * sin(TIME * 1.7));
		float shine = lit * (0.85 + 0.15 * sin(TIME * 3.0)) + flash * groove * 0.8;
		vec3 shade = vec3(0.04, 0.01, 0.015);
		ALBEDO = mix(shade, light_color * 1.7, clamp(shine + smoulder, 0.0, 1.0));
		float halo = (1.0 - smoothstep(0.0, 0.55, at)) * fill * 0.3 + smoulder * 0.15 + flash * 0.15 * (1.0 - smoothstep(0.0, 0.8, length(p)));
		ALPHA = clamp(groove * (0.4 + 0.6 * clamp(shine + smoulder, 0.0, 1.0)) + halo, 0.0, 1.0) * fade * COLOR.a;
	} else {
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
	ALPHA = clamp(groove * (0.32 + 0.68 * clamp(shine, 0.0, 1.0)) + halo, 0.0, 1.0) * fade * COLOR.a;
	}
}
