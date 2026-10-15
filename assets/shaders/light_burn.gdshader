// Where Firefly Squid's surge comes down on the seabed it burns (SurgeView): a patch (UV −1 → 1 both ways) or, along a
// cone's far edge, a strip (COLOR.b 1: UV.x along it in metres, UV.y across it −1 → 1). A white-hot heart in her
// light, a ragged rim of flame licking and flickering round it, tongues that never sit still. COLOR.r its strength.
shader_type spatial;
render_mode blend_add, unshaded, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;

// Her light (HerColor.cs, data/clementine_color.cfg).
global uniform vec3 her_light;

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
}

void fragment() {
	float r;
	float n;
	if (COLOR.b > 0.5) {
		r = abs(UV.y);
		n = noise(vec2(UV.x * 3.0, TIME * 6.0)) * 0.6 + noise(vec2(UV.x * 7.0 - TIME * 2.0, TIME * 11.0)) * 0.4;
	} else {
		r = length(UV);
		float a = atan(UV.y, UV.x);
		// Tongues round the rim, licking outward.
		n = noise(vec2(a * 2.6 + 10.0, TIME * 5.0 - r * 3.0)) * 0.6 + noise(vec2(a * 6.0, TIME * 9.0)) * 0.4;
	}
	float edge = 0.62 + 0.3 * n;
	float body = 1.0 - smoothstep(edge - 0.3, edge, r);
	float rim = exp(-pow((r - edge * 0.78) / 0.16, 2.0));
	// A strip is a line of fire, not a hot spot: its middle stays flame-coloured.
	float core = exp(-r * r * 7.0) * (COLOR.b > 0.5 ? 0.35 : 1.0);
	// Deep orange to amber flame; only a small hot heart goes toward white.
	vec3 flame = mix(vec3(1.0, 0.22, 0.02), vec3(1.0, 0.6, 0.12), n);
	vec3 col = mix(flame, vec3(1.0, 0.85, 0.55), core * 0.7) * (0.9 + 0.6 * core) + her_light * body * 0.15;
	float flicker = 0.8 + 0.2 * sin(TIME * 23.0 + n * 6.0);
	// Tongues: the body is broken up by the noise, so it flickers in licks rather than glowing evenly.
	float licks = smoothstep(0.35, 0.75, noise(vec2(r * 5.0 - TIME * 4.0, n * 4.0 + TIME * 2.0)));
	ALBEDO = col;
	ALPHA = clamp((core * 0.5 + body * (0.15 + 0.35 * licks) + rim * 1.0) * flicker * COLOR.r, 0.0, 1.0);
}
