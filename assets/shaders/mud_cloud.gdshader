// The mud cloud around a sealed boss arena: churning sand-brown haze from the arena's rim outward, thick enough to hide
// what lies beyond. One horizontal layer; several stack at different heights. `amount` raises and settles it.
// Where a layer meets rock it thins out over a couple of metres (against the depth already drawn), so no layer ever
// shows the hard, stepped line where its plane cuts the terrain.
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_disabled, unshaded, shadows_disabled;

uniform vec2 center = vec2(0.0);
uniform float rim = 15.0;
uniform float amount = 0.0;
uniform float layer = 0.0;
uniform vec3 mud_light : source_color = vec3(0.58, 0.49, 0.36);
uniform vec3 mud_dark : source_color = vec3(0.27, 0.22, 0.16);
uniform sampler2D depth_texture : hint_depth_texture, filter_linear, repeat_disable;
// How far in front of the rock (metres) a layer fades in from nothing.
uniform float soft_reach = 2.2;

varying vec3 world;

void vertex() {
	world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

float hash(vec2 p) {
	// Stable at large coordinates (no sin() precision blocks as time runs on).
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
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
	float t = TIME * 0.25 + layer * 3.1;
	// Billows swirl slowly around the arena: the pattern turns about its centre (faster near the rim) and drifts
	// outward. Built on the plane itself, never on the angle round the arena, so there is no seam where it wraps.
	float swirl = t * 0.12 + 6.0 / (d + 6.0);
	vec2 q = mat2(vec2(cos(swirl), sin(swirl)), vec2(-sin(swirl), cos(swirl))) * off * 0.2;
	q -= normalize(off + vec2(1e-4)) * t * 0.35;
	float n = fbm(q + layer * 5.0);
	float n2 = fbm(q * 2.4 - vec2(t * 0.6, t * 0.2));
	// Thin at the rim (a ragged inner edge), thick a few metres out, everything beyond hidden.
	float edge = rim + 0.6 + (n - 0.5) * 3.0;
	float body = smoothstep(edge, edge + 3.5, d);
	float alpha = body * mix(0.55, 0.97, smoothstep(edge + 2.0, edge + 9.0, d)) * (0.75 + 0.35 * n2);
	ALBEDO = mix(mud_dark, mud_light, clamp(n * 1.2 + n2 * 0.3 - 0.2, 0.0, 1.0));
	// Soft where it meets the rock: the scene's depth behind this point, in metres from the camera.
	float depth = texture(depth_texture, SCREEN_UV).r;
	vec4 behind = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, depth, 1.0);
	float gap = -behind.z / behind.w - (-VERTEX.z);
	float soft = smoothstep(0.0, soft_reach, gap);
	ALPHA = clamp(alpha * amount * soft, 0.0, 1.0);
}
