// A magic particle (MagicParticles): a tiny drawing in fine-tip white or ecru — a "+" or a "*" — as a comic panel marks a
// sparkle: thin strokes tapering to their tips, a hair of dark edge so they read on bright sand. Camera-facing quads;
// the instance colour is its ink, INSTANCE_CUSTOM.x its shape (0 "+", 1 "*").
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;

varying float v_shape;

void vertex() {
	v_shape = INSTANCE_CUSTOM.x;
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
	bool star = v_shape > 0.5;
	int arms = star ? 3 : 2;
	float len = star ? 0.78 : 0.88;
	float ink = 0.0, edge = 0.0;
	for (int i = 0; i < arms; i++) {
		float a = float(i) * 3.14159265 / float(arms);
		ink = max(ink, stroke(p, a, len, 0.12));
		edge = max(edge, stroke(p, a, len + 0.06, 0.24));
	}
	vec3 col = mix(vec3(0.08, 0.06, 0.05), COLOR.rgb, ink);
	ALBEDO = col;
	ALPHA = max(ink, edge * 0.45) * COLOR.a;
}
