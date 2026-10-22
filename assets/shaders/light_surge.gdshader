// Firefly Squid's surge (SurgeView): a continuous shaft of her light. Drawn as ribbons in world space on the swim band.
// UV.x runs along it (m from her), UV.y across it (−1 → 1). COLOR: r its brightness, g the hot core's share of the
// ribbon's half-width, b 1 for the cone's filled fan, a its fade (in at her, out at the tip).
// A white-hot core, her light glowing round it, pulses of brightness racing outward along it, a faint shimmer - laid
// like paint: its edge wanders and tears, bristle streaks flow outward along it, the cone is a dry-brushed sheet.
shader_type spatial;
render_mode blend_add, unshaded, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;

// Her light (HerColor.cs, data/clementine_color.cfg).
global uniform vec3 her_light;

float hash(float n) { return fract(sin(n) * 43758.5453); }
float noise(float x) {
	float i = floor(x);
	float f = fract(x);
	return mix(hash(i), hash(i + 1.0), f * f * (3.0 - 2.0 * f));
}

float hash2(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float noise2(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash2(i), hash2(i + vec2(1.0, 0.0)), f.x), mix(hash2(i + vec2(0.0, 1.0)), hash2(i + vec2(1.0, 1.0)), f.x), f.y);
}

void fragment() {
	float s = UV.x;
	float across = abs(UV.y);
	// Pulses racing outward, and a shimmer that never settles.
	float race = 0.78 + 0.22 * sin(s * 5.0 - TIME * 26.0) * (0.6 + 0.4 * sin(s * 1.7 - TIME * 9.0));
	float shimmer = 0.85 + 0.3 * (noise(s * 3.0 - TIME * 14.0) - 0.5);
	vec3 col;
	float a;
	if (COLOR.b > 0.5) {
		// The cone: an even sheet of light, soft at its sides, fading with distance, its middle a little brighter.
		float side = 1.0 - smoothstep(0.7, 1.0, across);
		float mid = exp(-across * across * 6.0);
		// Dry-brushed: streaks fanning out from her, flowing outward.
		float streak = 0.55 + 0.7 * noise2(vec2(UV.y * 9.0, s * 0.6 - TIME * 3.0));
		float body = side * (0.3 + 0.25 * mid) * streak;
		col = her_light * 1.3 + vec3(1.0, 0.97, 0.9) * mid * 0.4;
		a = body * race * shimmer;
	} else {
		// Its edge wanders and tears along it, the tearing flowing outward.
		float edge = 0.78 + 0.22 * noise2(vec2(s * 2.2 - TIME * 7.0, 3.0)) + 0.08 * noise2(vec2(s * 8.0 - TIME * 12.0, 1.0));
		float ac = across / edge;
		float x = ac / max(COLOR.g, 0.04);
		// The core is a loaded brush: hottest where it pressed, a hint thinner where it lifted.
		float press = 0.8 + 0.35 * noise2(vec2(s * 1.4 - TIME * 10.0, 7.0));
		float core = exp(-x * x * 1.6) * press;
		// Bristle streaks dragged through the glow, flowing outward.
		float bristle = 0.6 + 0.6 * noise2(vec2(UV.y * 11.0, s * 0.8 - TIME * 8.0));
		float glow = (exp(-ac * ac * 3.5) * 0.5 + exp(-ac * 2.2) * 0.12) * bristle;
		col = vec3(1.0, 0.98, 0.94) * core * 2.2 + her_light * glow * 1.5 + her_light * vec3(1.1, 0.8, 0.5) * glow * (1.0 - bristle) * 0.4;
		a = (core + glow) * race * shimmer;
		a *= 1.0 - smoothstep(0.8, 1.0, ac);
	}
	ALBEDO = col;
	ALPHA = clamp(a * COLOR.r * COLOR.a, 0.0, 1.0);
}
