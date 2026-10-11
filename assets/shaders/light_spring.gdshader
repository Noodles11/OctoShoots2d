// A light spring (docs/CORRUPTION.md): a pool of clear, luminous water on the floor. Caustics ripple across it and
// under the water her emblem flows: the four gold gonad horseshoes of her bell, in a ring, turning slowly and wavering
// with the water. While she bathes (bathing 1) it brightens and the emblem turns faster. A flat disc (UV 0..1).
shader_type spatial;
render_mode blend_add, unshaded, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;

instance uniform float bathing = 0.0;
uniform vec3 water : source_color = vec3(0.38, 0.95, 1.0);
uniform vec3 gold : source_color = vec3(1.0, 0.769, 0.420);

float horseshoe(vec2 q, vec2 centre) {
	vec2 d = q - centre;
	float ring = 1.0 - smoothstep(0.035, 0.06, abs(length(d) - 0.16));
	// Open toward the rim, like the gonads round her stomach.
	float open = smoothstep(0.45, 0.75, dot(normalize(d + 1e-4), normalize(centre)));
	return ring * (1.0 - open);
}

void fragment() {
	vec2 p = (UV - 0.5) * 2.0;
	float r = length(p);
	if (r > 1.0) discard;
	float t = TIME;
	// Caustics: two crossing interference patterns, drifting.
	float c = abs(sin(p.x * 9.0 + t * 1.3 + sin(p.y * 6.0 + t * 0.9)) * sin(p.y * 8.0 - t * 1.1 + sin(p.x * 5.0 - t * 0.7)));
	c = pow(c, 0.6);
	// The emblem, under the water: turning, and wavering with the ripples.
	float spin = t * mix(0.18, 0.7, bathing);
	vec2 q = mat2(vec2(cos(spin), sin(spin)), vec2(-sin(spin), cos(spin))) * p;
	q += 0.035 * vec2(sin(q.y * 11.0 + t * 2.1), cos(q.x * 10.0 + t * 1.8));
	float emblem = 0.0;
	for (int k = 0; k < 4; k++) {
		float a = 0.785398 + float(k) * 1.570796;
		emblem += horseshoe(q, vec2(cos(a), sin(a)) * 0.44);
	}
	// The stomach at its heart, and a soft gold breath through it.
	emblem += (1.0 - smoothstep(0.08, 0.11, length(q))) * 0.7;
	float breathe = 0.75 + 0.25 * sin(t * 1.6);
	float body = 1.0 - smoothstep(0.7, 1.0, r);
	float rim = 1.0 - smoothstep(0.0, 0.07, abs(r - 0.93));
	float glow = 1.0 + 0.8 * bathing;
	vec3 col = water * (0.16 + 0.32 * c) * body + water * rim * 0.45 + gold * emblem * (1.1 + 0.9 * bathing) * breathe * body;
	ALBEDO = col * glow;
	ALPHA = 1.0;
}
