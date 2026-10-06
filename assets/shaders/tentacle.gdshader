// Clementine's tentacle skin, drawn on top of the world like the rest of the viewmodel.
// UV.x = metres along the arm, UV.y = around the arm (0 = middle of the sucker side, 0.5 = the back).
// UV2.x = 0 at the base → 1 at the tip, UV2.y = the arm's radius there (so spots keep their size as it tapers).
shader_type spatial;
render_mode depth_test_disabled, cull_back;

uniform vec3 back_color : source_color = vec3(0.78, 0.2, 0.11);
uniform vec3 back_dark : source_color = vec3(0.38, 0.05, 0.08);
uniform vec3 belly_color : source_color = vec3(0.98, 0.66, 0.56);
uniform vec3 tip_color : source_color = vec3(1.0, 0.6, 0.47);
uniform vec3 sheen_color : source_color = vec3(0.45, 0.85, 1.0);
uniform float spot_density = 72.0;
/// Hurt: the skin flushes deep red for a moment.
uniform float flush = 0.0;
/// A throw: the arm warms briefly.
uniform float glow = 0.0;

varying float v_s;
varying float v_radius;

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }

float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

// Distance to the nearest cell point, and that cell's random id: chromatophore spots.
vec2 cells(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	float best = 8.0, id = 0.0;
	for (int y = -1; y <= 1; y++)
	for (int x = -1; x <= 1; x++) {
		vec2 g = vec2(float(x), float(y));
		vec2 o = vec2(hash(i + g), hash(i + g + 19.1)) * 0.8 + 0.1;
		float d = length(g + o - f);
		if (d < best) { best = d; id = hash(i + g + 7.7); }
	}
	return vec2(best, id);
}

void vertex() {
	v_s = UV2.x;
	v_radius = UV2.y;
}

// Height of the skin: soft bumps plus slightly raised spots.
float skin_height(vec2 q, float back) {
	float bumps = noise(q * 0.85) * 0.6 + noise(q * 2.1) * 0.25;
	float spot = 1.0 - smoothstep(0.15, 0.45, cells(q).x);
	return bumps + spot * 0.5 * back;
}

void fragment() {
	float around = cos(UV.y * TAU);                       // 1 on the sucker side, -1 on the back
	float belly = smoothstep(0.3, 0.8, around);
	float back = 1.0 - belly;
	vec2 q = vec2(UV.x, UV.y * TAU * v_radius) * spot_density;

	// The back: coral red, mottled, dotted with dark chromatophores and a few pale flecks.
	vec2 c = cells(q);
	float spot = 1.0 - smoothstep(0.16, 0.44, c.x);
	float fleck = (1.0 - smoothstep(0.04, 0.14, cells(q * 1.9 + 3.1).x)) * step(0.72, c.y);
	float mottle = noise(q * 0.12 + 4.0);
	vec3 top = mix(back_color, back_dark, spot * (0.5 + 0.5 * c.y));
	top = mix(top, back_color * vec3(1.2, 1.1, 1.05), mottle * 0.35);
	top += vec3(1.0, 0.85, 0.75) * fleck * 0.35;

	// The sucker side: pale peach, faintly blushing toward the edges.
	vec3 under = mix(belly_color, belly_color * vec3(0.95, 0.72, 0.7), smoothstep(0.95, 0.45, around));

	// Fine transverse creases on the soft underside.
	under *= 1.0 - 0.14 * smoothstep(0.4, 1.0, sin(UV.x * 260.0 + noise(q * 0.3) * 3.0));
	// Blotchy, uneven flesh: paler and pinker patches, faint darker freckles.
	under *= 0.86 + 0.26 * noise(q * 0.18 + 2.0);
	under = mix(under, under * vec3(1.0, 0.82, 0.82), smoothstep(0.55, 0.8, noise(q * 0.09 + 5.0)) * 0.6);
	under *= 1.0 - 0.25 * (1.0 - smoothstep(0.08, 0.2, cells(q * 1.3 + 1.7).x)) * step(0.6, cells(q * 1.3 + 1.7).y);
	vec3 col = mix(top, under, belly);
	// An opal sheen along the side where back meets belly.
	float lateral = exp(-pow((around - 0.1) * 6.0, 2.0));
	col += sheen_color * lateral * 0.16 * (0.6 + 0.4 * sin(UV.x * 40.0 + TIME * 0.7));
	// Paler and more translucent toward the tip.
	col = mix(col, tip_color, smoothstep(0.65, 1.0, v_s) * 0.55);
	col = mix(col, vec3(0.5, 0.03, 0.06), flush * 0.55 * (1.0 - belly * 0.5));
	ALBEDO = col;

	// Wet, bumpy skin.
	float e = 0.06;
	float h0 = skin_height(q, back);
	float hx = skin_height(q + vec2(e, 0.0), back);
	float hy = skin_height(q + vec2(0.0, e), back);
	float strength = mix(1.3, 0.6, belly);
	NORMAL_MAP = normalize(vec3((h0 - hx) / e * 0.12 * strength, (h0 - hy) / e * 0.12 * strength, 1.0)) * 0.5 + 0.5;
	NORMAL_MAP_DEPTH = 1.0;

	// Soft living skin: mostly matte, with only a faint, broken wet sheen (no lacquer).
	float wet = smoothstep(0.45, 0.75, noise(q * 0.35 + 9.0));
	ROUGHNESS = mix(0.72, 0.5, wet) + 0.08 * spot * back;
	SPECULAR = 0.3;
	CLEARCOAT = 0.12 * wet;
	CLEARCOAT_ROUGHNESS = 0.45;
	RIM = 0.25;
	RIM_TINT = 0.8;
	// Light glowing through thin flesh.
	// Light glowing through the flesh, strongest where the arm is thin.
	BACKLIGHT = vec3(1.0, 0.35, 0.2) * (0.3 + 0.45 * v_s);
	// Soft self-shadow toward the silhouette so the arm reads round.
	float facing = clamp(dot(normalize(NORMAL), normalize(VIEW)), 0.0, 1.0);
	ALBEDO *= mix(0.55, 1.0, smoothstep(0.0, 0.6, facing));
	EMISSION = ALBEDO * (0.07 + 0.05 * belly + 0.08 * v_s) + vec3(1.0, 0.45, 0.22) * glow * 0.3;
}
