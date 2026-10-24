// A light spring's emblem on the floor (docs/CORRUPTION.md): her own emblem in white light — the four gonad horseshoes
// of her bell, open toward the middle as hers are (at the same quarters), inside a white circle. Still: nothing moves in
// it. While she bathes (bathing 1) it shines brighter, a touch warmer. A flat disc (UV 0..1).
shader_type spatial;
render_mode blend_add, unshaded, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;

instance uniform float bathing = 0.0;
// Spent (its one washing used): it fades away, 1 → 0.
instance uniform float fade = 1.0;
uniform vec3 white : source_color = vec3(0.95, 0.98, 1.0);
uniform vec3 warm : source_color = vec3(1.0, 0.9, 0.7);

// A gonad: a horseshoe open toward the emblem's middle, as on her bell.
float horseshoe(vec2 q, vec2 centre) {
	vec2 d = q - centre;
	float ring = 1.0 - smoothstep(0.03, 0.05, abs(length(d) - 0.17));
	float open = 1.0 - smoothstep(0.35, 0.75, dot(normalize(d + 1e-4), -normalize(centre)));
	return ring * open;
}

void fragment() {
	vec2 p = (UV - 0.5) * 2.0;
	float r = length(p);
	if (r > 1.0) discard;
	float emblem = 0.0;
	for (int k = 0; k < 4; k++) {
		float a = 0.785398 + float(k) * 1.570796;
		emblem = max(emblem, horseshoe(p, vec2(cos(a), sin(a)) * 0.42));
	}
	float circle = 1.0 - smoothstep(0.015, 0.04, abs(r - 0.92));
	float shape = max(emblem, circle);
	vec3 col = mix(white, warm, 0.35 * bathing) * shape * (0.75 + 0.9 * bathing);
	ALBEDO = col;
	ALPHA = fade;
}
