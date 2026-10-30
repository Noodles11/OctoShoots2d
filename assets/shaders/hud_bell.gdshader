// The HUD's Clementine (HudView): her bell seen from above, a disc of living glass — her four gonad horseshoes (static,
// lit, open toward the middle as on her bell), her scalloped rim glowing, a few twinkling specks. Holding an active
// pearl, the pearl's colours and icon fill the glass as its background (as her bell takes them on in the ritual);
// without one, the glass is clear. `flash` 1 → 0 whitens her for a beat (her pearl just used).
shader_type canvas_item;

#include "res://assets/shaders/pearl_engraving.gdshaderinc"

global uniform vec3 her_body;
global uniform vec3 her_light;
uniform bool has_pearl = false;
uniform vec3 c0 : source_color = vec3(1.0);
uniform vec3 c1 : source_color = vec3(1.0);
uniform vec3 c2 : source_color = vec3(1.0);
uniform int icon = 0;
uniform float ready = 0.0;
uniform float flash = 0.0;
uniform float points = 0.0; // a skill point waits: her gonads pulse, swelling and flaring gold

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }

vec3 ramp(float t) {
	t = clamp(t, 0.0, 1.0);
	return t < 0.5 ? mix(c0, c1, t * 2.0) : mix(c1, c2, t * 2.0 - 1.0);
}

// The pearl over the disc, its icon cut into its colours (as clementine_bell's mimic_t).
float pearl_t(vec2 q) {
	float d = pearl_engraving(vec2(q.x, -q.y) * 1.2, icon);
	return mix(1.0, 0.2 + 0.4 * length(q), smoothstep(-0.02, 0.02, d));
}

float line(float d, float w) { return 1.0 - smoothstep(w * 0.5, w, abs(d)); }

void fragment() {
	vec2 q = (UV - 0.5) * 2.0;
	float r = length(q);
	float ang = atan(q.y, q.x);
	// The scalloped margin: sixteen lappets.
	float edge = 0.9 + 0.025 * cos(ang * 16.0);
	float inside = 1.0 - smoothstep(edge - 0.02, edge + 0.01, r);
	vec3 col = mix(mix(her_light, vec3(1.0, 0.95, 0.9), 0.35), her_body, smoothstep(0.0, 0.85, r));
	float a = (0.18 + 0.2 * smoothstep(0.4, 0.9, r)) * inside;
	// The pearl's look, filling the glass.
	if (has_pearl) {
		vec3 worn = ramp(pearl_t(q * 1.1)) * (1.0 + 0.25 * ready * (0.5 + 0.5 * sin(TIME * 4.0)));
		col = mix(col, worn, 0.85);
		a = max(a, 0.62 * inside);
	}
	// A soft dark backing so the disc reads on bright sand.
	float shade = (1.0 - smoothstep(edge, edge + 0.12, r)) * 0.35;
	// The rim: a bright line round the margin.
	float rim = line(r - (edge - 0.05), 0.09) * inside;
	col = mix(col, mix(her_body, vec3(1.0, 0.96, 0.9), 0.45) * 1.25, rim);
	a = max(a, rim);
	// Her four gonads, open toward the middle.
	float gonad = 0.0, glow = 0.0;
	float waiting = points * (0.5 + 0.5 * sin(TIME * 4.5));
	for (int k = 0; k < 4; k++) {
		float ka = 0.7853982 + float(k) * 1.5707963;
		vec2 c = vec2(cos(ka), sin(ka)) * 0.4;
		vec2 d = q - c;
		float open = 1.0 - smoothstep(0.35, 0.75, dot(normalize(d + 1e-5), -normalize(c)));
		gonad = max(gonad, line(length(d) - 0.17, 0.08 + 0.045 * waiting) * open);
		glow = max(glow, line(length(d) - 0.17, 0.24) * open);
	}
	col = mix(col, vec3(1.0, 0.8, 0.4) * 1.3, glow * waiting * 0.7);
	a = max(a, glow * waiting * 0.6);
	col = mix(col, mix(her_light, vec3(1.0, 0.98, 0.92), 0.5) * (1.2 + 0.9 * waiting), gonad);
	a = max(a, gonad * 0.95);
	// Specks of light, twinkling.
	vec2 cell = floor(q * 9.0);
	float speck = step(0.82, hash(cell + 3.0)) * (1.0 - smoothstep(0.05, 0.13, length(fract(q * 9.0) - 0.5))) * (0.4 + 0.6 * sin(TIME * 3.0 + hash(cell) * 30.0)) * inside;
	col = mix(col, vec3(1.0), max(speck, 0.0));
	a = max(a, speck * 0.8);
	col = mix(col, vec3(1.0, 0.97, 0.9), flash * 0.7);
	// The glass over its dark backing.
	float alpha = a + shade * (1.0 - a);
	COLOR = vec4(col * a / max(alpha, 1e-4), alpha);
}
