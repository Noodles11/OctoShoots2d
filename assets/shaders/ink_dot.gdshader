// One of an ink dot's three marks in the water (AdventureView): a fine-tip violet "×" that keeps twitching into a "+" and
// back, drawn as the ink's bracket marks are (magic_particle.gdshader: strokes tapering to their tips, a pale edge so they
// read on the dark). Each flips on its own uneven beat, jumping a hair aside as it does, and all of them bob slowly up and
// down. Camera-facing quads in one MultiMesh; the instance's scale is its size in metres across, INSTANCE_CUSTOM.x a seed.
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;

varying float v_turn;
varying vec3 v_ink;

float hash(float n) {
	return fract(sin(n * 91.3458) * 47453.5453);
}

void vertex() {
	float seed = INSTANCE_CUSTOM.x;
	// Its beat: 4–9 flips a second, never quite even.
	float clock = TIME * (4.0 + 5.0 * hash(seed)) + seed * 17.0 + 0.7 * sin(TIME * 2.3 + seed * 40.0);
	float tick = floor(clock);
	// A "×" and a "+" by turns, a little off true, each flip with a kick.
	v_turn = mod(tick, 2.0) * 0.785398 + (hash(tick + seed) - 0.5) * 0.24;
	float kick = 1.0 + 0.25 * exp(-fract(clock) * 6.0);
	vec2 jump = (vec2(hash(tick * 1.3 + seed), hash(tick * 1.7 - seed)) - 0.5) * 0.05;
	float bob = sin(TIME * 1.4 + seed * 6.28) * 0.06 + sin(TIME * 2.3 + seed * 11.0) * 0.025;
	v_ink = mix(vec3(0.42, 0.16, 0.72), vec3(0.26, 0.08, 0.48), step(0.5, hash(seed + 0.5)));
	vec3 at = MODEL_MATRIX[3].xyz + vec3(jump.x, bob, jump.y);
	float size = length(MODEL_MATRIX[0].xyz) * kick;
	MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], vec4(at, 1.0));
	MODELVIEW_MATRIX = MODELVIEW_MATRIX * mat4(
		vec4(size, 0.0, 0.0, 0.0),
		vec4(0.0, size, 0.0, 0.0),
		vec4(0.0, 0.0, size, 0.0),
		vec4(0.0, 0.0, 0.0, 1.0));
}

// A stroke through the middle at this angle, tapering toward both tips.
float stroke(vec2 p, float ang, float len, float w) {
	vec2 d = vec2(cos(ang), sin(ang));
	float t = clamp(dot(p, d), -len, len);
	float dist = length(p - d * t);
	float width = w * mix(1.0, 0.3, abs(t) / len);
	return 1.0 - smoothstep(width * 0.55, width, dist);
}

void fragment() {
	vec2 p = (UV - 0.5) * 2.0;
	float ink = 0.0, edge = 0.0;
	for (int i = 0; i < 2; i++) {
		float a = v_turn + float(i) * 1.570796;
		ink = max(ink, stroke(p, a, 0.8, 0.14));
		edge = max(edge, stroke(p, a, 0.86, 0.28));
	}
	ALBEDO = mix(vec3(0.75, 0.62, 0.95), v_ink, ink);
	ALPHA = max(ink, edge * 0.35);
}
