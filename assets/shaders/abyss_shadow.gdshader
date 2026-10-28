// The abyss's big fish (AbyssView, docs/ADVENTURE.md §5): dark shapes cruising far below her plane, seen only as
// shadows through the water. Each is a flat quad (x along it, head at +x; z across), drawn opaque so the water's pass
// blurs and sinks it by its depth like the reef; its outline worked out here, its edge and its fade dithered (the blur
// softens both). A fish swings its tail; a ray beats its wings.
// INSTANCE_CUSTOM: x its kind (0 a fish, 1 a ray), y a phase, z how much it is there (0–1), w its beat (radians a second).
shader_type spatial;
render_mode unshaded, cull_disabled, shadows_disabled;

uniform vec3 body_col : source_color = vec3(0.02, 0.04, 0.05);

varying vec2 shape;
varying vec4 custom;

const float BAYER[16] = float[](0.0, 8.0, 2.0, 10.0, 12.0, 4.0, 14.0, 6.0, 3.0, 11.0, 1.0, 9.0, 15.0, 7.0, 13.0, 5.0);

void vertex() {
	custom = INSTANCE_CUSTOM;
	// 0 its tail → 1 its head; −1 → 1 across.
	shape = vec2(VERTEX.x + 0.5, VERTEX.z * 2.0);
	float beat = custom.y + TIME * custom.w;
	if (custom.x < 0.5) {
		// The swing runs from the head back, the tail most.
		float k = 1.0 - shape.x;
		VERTEX.z += sin(beat - shape.x * 4.0) * 0.14 * k * k;
	} else {
		// The wings' tips rise and fall, the beat running back along them.
		float w = abs(shape.y);
		VERTEX.y += sin(beat - shape.x * 1.5) * 0.16 * w * w;
	}
}

// How far inside the outline a point is (in the quad's half widths; under 0 outside).
float fish(vec2 p) {
	float x = p.x, a = abs(p.y);
	// The body: long and round-nosed, thinning to the tail's stalk.
	float e = (x - 0.6) / 0.42;
	float body = 0.48 * sqrt(max(0.0, 1.0 - e * e));
	float stalk = x > 0.1 && x < 0.4 ? 0.09 : 0.0;
	float d = max(body, stalk) - a;
	// The tail, forked.
	if (x < 0.22) {
		float outer = 0.1 + (0.22 - x) * 3.4;
		float notch = max(0.0, 0.13 - x) * 4.0;
		d = max(d, min(outer - a, a - notch));
	}
	// Its pectoral fins, swept back.
	vec2 f = vec2((x - 0.5) / 0.11, (a - 0.55) / 0.3);
	d = max(d, (1.0 - length(f)) * 0.12);
	return d;
}

float ray(vec2 p) {
	float x = p.x, a = abs(p.y);
	// The wings: broad at the shoulders, swept to a point at either tip, rounding into the head.
	float span = smoothstep(0.12, 0.58, x) * (1.0 - 0.8 * smoothstep(0.58, 1.0, x));
	float d = span * (1.0 - 0.15 * a) - a;
	// The head's lobes, and the whip of a tail.
	vec2 h = vec2((x - 0.93) / 0.08, (a - 0.12) / 0.06);
	d = max(d, (1.0 - length(h)) * 0.05);
	if (x < 0.25) d = max(d, 0.025 - a);
	return d;
}

void fragment() {
	float d = custom.x < 0.5 ? fish(shape) : ray(shape);
	float inside = smoothstep(-0.02, 0.03, d);
	ivec2 q = ivec2(FRAGCOORD.xy) % 4;
	if (inside * custom.z <= (BAYER[q.y * 4 + q.x] + 0.5) / 16.0) discard;
	// A shade lighter down its back.
	ALBEDO = body_col * (1.0 + 0.35 * smoothstep(0.0, 0.3, d));
}
