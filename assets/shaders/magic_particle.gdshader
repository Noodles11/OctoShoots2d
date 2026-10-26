// A magic particle (MagicParticles): a tiny drawing in fine-tip white or ecru — a "+" or a "*" — as a comic panel marks a
// sparkle: thin strokes tapering to their tips, a hair of dark edge so they read on bright sand. Camera-facing quads;
// the instance colour is its ink, INSTANCE_CUSTOM.x its shape (0 "+", 1 "*"; the ink's own dark marks: 2 "(", 3 ")",
// 4 "[", 5 "]", violet with a pale edge so they read on the dark).
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

// A stroke between two points, of even width.
float seg(vec2 p, vec2 a, vec2 b, float w) {
	vec2 ab = b - a;
	float t = clamp(dot(p - a, ab) / dot(ab, ab), 0.0, 1.0);
	return 1.0 - smoothstep(w * 0.55, w, length(p - a - ab * t));
}

// A bracket: "(" / "[" (mirrored for ")" / "]"), w its stroke.
float bracket(vec2 p, bool round, float w) {
	if (round) {
		vec2 c = vec2(0.75, 0.0);
		vec2 d = p - c;
		float ang = abs(atan(d.y, -d.x));
		float on = 1.0 - smoothstep(w * 0.55, w, abs(length(d) - 0.95));
		return on * (1.0 - smoothstep(0.95, 1.05, ang)) * mix(1.0, 0.6, ang);
	}
	return max(seg(p, vec2(-0.3, -0.8), vec2(-0.3, 0.8), w), max(seg(p, vec2(-0.3, -0.8), vec2(0.25, -0.8), w), seg(p, vec2(-0.3, 0.8), vec2(0.25, 0.8), w)));
}

void fragment() {
	vec2 p = (UV - 0.5) * 2.0;
	int shape = int(v_shape + 0.5);
	if (shape >= 2) {
		vec2 q = shape == 3 || shape == 5 ? vec2(-p.x, p.y) : p;
		bool round = shape <= 3;
		float inkb = bracket(q, round, 0.14);
		float edgeb = bracket(q, round, 0.28);
		ALBEDO = mix(vec3(0.75, 0.62, 0.95), COLOR.rgb, inkb);
		ALPHA = max(inkb, edgeb * 0.35) * COLOR.a;
	} else {
		bool star = shape == 1;
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
}
