// The mud cloud around a sealed boss arena: churning sand-brown haze from the arena's rim outward, thick enough to hide
// what lies beyond. One horizontal layer; several stack at different heights. `amount` raises and settles it.
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_disabled, unshaded, shadows_disabled;

uniform vec2 center = vec2(0.0);
uniform float rim = 15.0;
uniform float amount = 0.0;
uniform float layer = 0.0;
uniform vec3 mud_light : source_color = vec3(0.58, 0.49, 0.36);
uniform vec3 mud_dark : source_color = vec3(0.27, 0.22, 0.16);

varying vec3 world;

void vertex() {
	world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
}

float fbm(vec2 p) {
	float s = 0.0, a = 0.5;
	for (int i = 0; i < 5; i++) {
		s += a * noise(p);
		p = p * 2.03 + vec2(1.7, 9.2);
		a *= 0.5;
	}
	return s;
}

void fragment() {
	vec2 off = world.xz - center;
	float d = length(off);
	float ang = atan(off.y, off.x);
	float t = TIME * 0.25 + layer * 3.1;
	// Billows swirl slowly around the arena.
	vec2 q = vec2(ang * rim * 0.35 + t * 2.0, d * 0.45 - t);
	float n = fbm(q * 0.55 + layer * 5.0);
	float n2 = fbm(q * 1.3 - vec2(t * 1.5, 0.0));
	// Thin at the rim (a ragged inner edge), thick a few metres out, everything beyond hidden.
	float edge = rim + 0.6 + (n - 0.5) * 3.0;
	float body = smoothstep(edge, edge + 3.5, d);
	float alpha = body * mix(0.55, 0.97, smoothstep(edge + 2.0, edge + 9.0, d)) * (0.75 + 0.35 * n2);
	ALBEDO = mix(mud_dark, mud_light, clamp(n * 1.2 + n2 * 0.3 - 0.2, 0.0, 1.0));
	ALPHA = clamp(alpha * amount, 0.0, 1.0);
}
