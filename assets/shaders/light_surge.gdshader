// Firefly Squid's surge (SurgeView): a continuous shaft of her light. Drawn as ribbons in world space on the swim band.
// UV.x runs along it (m from her), UV.y across it (−1 → 1). COLOR: r its brightness, g the hot core's share of the
// ribbon's half-width, b 1 for the cone's filled fan, a its fade (in at her, out at the tip).
// A white-hot core, her light glowing round it, pulses of brightness racing outward along it, a faint shimmer.
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
		float body = side * (0.3 + 0.25 * mid);
		col = her_light * 1.3 + vec3(1.0, 0.97, 0.9) * mid * 0.4;
		a = body * race * shimmer;
	} else {
		float x = across / max(COLOR.g, 0.04);
		float core = exp(-x * x * 1.6);
		float glow = exp(-across * across * 3.5) * 0.5 + exp(-across * 2.2) * 0.12;
		col = vec3(1.0, 0.98, 0.94) * core * 2.2 + her_light * glow * 1.5;
		a = (core + glow) * race * shimmer;
		a *= 1.0 - smoothstep(0.85, 1.0, across);
	}
	ALBEDO = col;
	ALPHA = clamp(a * COLOR.r * COLOR.a, 0.0, 1.0);
}
