// Clementine's shot (docs/LIGHT-BUBBLES.md): light itself, no bubble. A small hot mote on a camera-facing quad: a
// white-hot core, a warm glow (her gold, whitening as it merges: warmth), and a faint four-point twinkle that turns.
// lantern is its brightness (merging, pulses, dimming); ink (Ink Sac) sinks a violet heart into it.
shader_type spatial;
render_mode blend_add, unshaded, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;

instance uniform float lantern = 0.5;
instance uniform float warmth = 0.0;
instance uniform float ink = 0.0;
instance uniform float seed = 0.0;
uniform vec3 gold : source_color = vec3(1.0, 0.769, 0.420);

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
	float flicker = 0.88 + 0.12 * sin(TIME * 31.0 + seed * 7.0) * sin(TIME * 17.0 + seed * 3.0);
	float core = exp(-r * r * 60.0);
	float glow = exp(-r * r * 7.0) * 0.55 + exp(-r * 2.6) * 0.25;
	// A slowly turning four-point twinkle.
	float a = TIME * 1.3 + seed * 6.28;
	vec2 q = mat2(vec2(cos(a), sin(a)), vec2(-sin(a), cos(a))) * p;
	float star = (exp(-abs(q.x) * 28.0) * exp(-abs(q.y) * 3.2) + exp(-abs(q.y) * 28.0) * exp(-abs(q.x) * 3.2)) * 0.45;
	vec3 hue = mix(gold, vec3(1.0, 0.95, 0.84), clamp(warmth, 0.0, 1.0));
	vec3 col = vec3(1.0, 0.98, 0.93) * core * 2.4 + hue * (glow + star) * 1.4;
	col = mix(col, col * vec3(0.55, 0.3, 0.9) * 0.6, ink);
	float edge = 1.0 - smoothstep(0.8, 1.0, r);
	ALBEDO = col;
	ALPHA = clamp((core + glow + star) * edge * lantern * 1.6 * flicker, 0.0, 1.0);
}
