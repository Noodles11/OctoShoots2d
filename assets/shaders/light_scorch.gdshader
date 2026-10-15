// Under Firefly Squid's burns (SurgeView): the seabed tinted warm where her light comes down on it, so the flame over
// it reads even on bright sand. It multiplies the ground toward ember orange in the middle and fades to no change at
// its edge, so it never draws a dark rim. Same layout as light_burn (a patch, or a strip when COLOR.b is 1); COLOR.r
// its strength.
shader_type spatial;
render_mode blend_mul, unshaded, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;

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
		n = noise(vec2(UV.x * 2.5 - TIME * 0.8, 3.0));
	} else {
		r = length(UV);
		n = noise(UV * 3.0 + vec2(7.0, TIME * 0.6));
	}
	// Strongest in the middle, smoothly gone well inside the edge.
	float warm = (1.0 - smoothstep(0.1, 0.85 + 0.1 * n, r)) * clamp(COLOR.r, 0.0, 1.0);
	ALBEDO = mix(vec3(1.0), vec3(1.0, 0.62, 0.32), warm * 0.85);
}
