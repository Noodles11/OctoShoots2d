// A corrupted creature's corruption meter (docs/CORRUPTION.md): a ring round it, facing the camera, drawn over
// everything. Filled clockwise from the top with ink and a cold violet edge for what corruption is left (fill); the
// chunk her last shots took off burns gold-white as it drains away (ghost ≥ fill); the rest is a faint track.
shader_type spatial;
render_mode blend_mix, unshaded, depth_draw_never, depth_test_disabled, cull_disabled, shadows_disabled, fog_disabled;

instance uniform float fill = 1.0;
instance uniform float ghost = 1.0;
instance uniform float alpha = 1.0;

void vertex() {
	MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
	MODELVIEW_MATRIX = MODELVIEW_MATRIX * mat4(
		vec4(length(MODEL_MATRIX[0].xyz), 0.0, 0.0, 0.0),
		vec4(0.0, length(MODEL_MATRIX[1].xyz), 0.0, 0.0),
		vec4(0.0, 0.0, length(MODEL_MATRIX[2].xyz), 0.0),
		vec4(0.0, 0.0, 0.0, 1.0));
}

void fragment() {
	vec2 p = (UV - 0.5) * 2.0;
	float r = length(p);
	// Clockwise from the top of the screen.
	float t = fract(atan(p.x, -p.y) / 6.2831853 + 1.0);
	float band = 1.0 - smoothstep(0.075, 0.1, abs(r - 0.8));
	float outline = (1.0 - smoothstep(0.1, 0.13, abs(r - 0.8))) - band;
	float aa = fwidth(t) * 1.5;
	float inFill = 1.0 - smoothstep(fill - aa, fill + aa, t);
	float inGhost = (1.0 - smoothstep(ghost - aa, ghost + aa, t)) * (1.0 - inFill);
	// The corruption left: ink with a violet sheen toward its outer edge.
	vec3 ink = mix(vec3(0.05, 0.02, 0.09), vec3(0.55, 0.22, 0.95), smoothstep(0.74, 0.88, r));
	vec3 col = mix(vec3(0.08, 0.06, 0.1), ink, inFill);
	col = mix(col, vec3(1.4, 1.15, 0.7), inGhost);
	float a = band * mix(0.35, 0.95, max(inFill, inGhost)) + outline * 0.7;
	col = mix(col, vec3(0.0), outline / max(a, 1e-3) * 0.7);
	ALBEDO = col;
	ALPHA = clamp(a, 0.0, 1.0) * alpha;
}
