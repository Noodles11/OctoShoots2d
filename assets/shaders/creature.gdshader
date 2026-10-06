// The den creatures: one shader, behaviour chosen by the vertex colour's alpha (the paint is in rgb):
//   1.0 body (wags like a fish when `wag` > 0)   0.85 glowing part (eyes, spots)   0.7 bell (contracts with `pulse`)
//   0.5 soft part (frills, gills, legs, tentacles: flutters; flares out with `pulse`)   0.3 spine (stands up with `pulse`, rattles with `quiver`)
shader_type spatial;
render_mode cull_disabled;

#include "res://assets/shaders/sunlight.gdshaderinc"

varying vec3 sun_world;
varying float sun_light;

instance uniform float phase = 0.0;
instance uniform float swim = 0.0;
instance uniform float wag = 0.0;
instance uniform float glow = 0.0;
instance uniform float pulse = 0.0;
instance uniform float quiver = 0.0;
instance uniform float flash = 0.0;
instance uniform vec3 tint : source_color = vec3(1.0);
instance uniform float tint_amount = 0.0;
// Corruption (DEPTH1-BESTIARY §8): tank colours in a pattern wrong for the species, a plastic gloss and the Leak's warm shimmer.
// corrupt 0 = healthy, 1 = corrupted (it fades while a creature is freed). Mode: 0 stripes, 1 spots, 2 violet spines with acid tips.
instance uniform float corrupt = 0.0;
instance uniform vec3 corrupt_a : source_color = vec3(1.0, 0.2, 0.6);
instance uniform vec3 corrupt_b : source_color = vec3(0.1, 0.5, 1.0);
instance uniform int corrupt_mode = 0;

uniform float half_len = 0.5;
uniform float spine_rest = 0.65;
uniform vec3 glow_color : source_color = vec3(1.0, 0.4, 0.15);
uniform float rough = 0.55;

varying vec3 local_pos;

void vertex() {
	local_pos = VERTEX;
	float a = COLOR.a;
	if (a > 0.95) {
		if (wag > 0.0) {
			float t = clamp(VERTEX.z / (half_len * 2.0) + 0.5, 0.0, 1.0);
			float rate = 4.0 + swim * 8.0;
			VERTEX.x += sin(TIME * rate + phase - VERTEX.z * 4.0 / max(half_len, 0.1)) * wag * (0.25 + 0.75 * swim) * half_len * 0.18 * t * t;
		}
		VERTEX.xy += vec2(sin(TIME * 90.0 + phase), cos(TIME * 77.0 + phase)) * 0.012 * quiver;
	} else if (a > 0.6 && a < 0.8) {
		VERTEX.xz *= 1.0 - 0.32 * pulse;
		VERTEX.y *= 1.0 + 0.22 * pulse;
	} else if (a > 0.4 && a < 0.6) {
		float k = 0.03 * (1.0 + 2.5 * pulse + swim);
		VERTEX.x += sin(TIME * 6.0 + phase + VERTEX.z * 8.0) * k;
		VERTEX.y += sin(TIME * 5.0 + phase * 1.3 + VERTEX.x * 7.0) * k;
		VERTEX.xz *= 1.0 + 0.25 * pulse;
	} else if (a < 0.4) {
		VERTEX *= mix(spine_rest, 1.0, pulse);
		VERTEX += vec3(sin(TIME * 70.0 + phase + VERTEX.y * 20.0), 0.0, cos(TIME * 63.0 + VERTEX.x * 20.0)) * 0.014 * quiver;
	}
	sun_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	// Small things: sunlight is worked out per vertex, which is plenty and far cheaper.
	vec3 sun_n = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
	float sun_face = 0.35 + 0.65 * clamp(dot(sun_n, -normalize(sun_dir)), 0.0, 1.0);
	sun_light = (sun_caustics(sun_world, TIME) * 0.55 + sun_shafts(sun_world, TIME) * 0.3) * sun_lit(sun_world) * sun_face * light_strength;
}

float cr_hash(vec3 p) {
	p = fract(p * 0.3183099 + 0.1);
	p *= 17.0;
	return fract(p.x * p.y * p.z * (p.x + p.y + p.z));
}

float cr_noise(vec3 x) {
	vec3 i = floor(x), f = fract(x);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(mix(cr_hash(i), cr_hash(i + vec3(1, 0, 0)), f.x), mix(cr_hash(i + vec3(0, 1, 0)), cr_hash(i + vec3(1, 1, 0)), f.x), f.y),
	           mix(mix(cr_hash(i + vec3(0, 0, 1)), cr_hash(i + vec3(1, 0, 1)), f.x), mix(cr_hash(i + vec3(0, 1, 1)), cr_hash(i + vec3(1, 1, 1)), f.x), f.y), f.z);
}

// The corrupted paint for this point: two tank colours laid out in a pattern, shaded by the original paint's lightness.
vec3 tank_colours(vec3 original) {
	float l = dot(original, vec3(0.299, 0.587, 0.114));
	float m;
	if (corrupt_mode == 0) {
		m = smoothstep(-0.2, 0.2, sin(local_pos.z * 22.0 + local_pos.y * 6.0));
	} else if (corrupt_mode == 1) {
		m = smoothstep(0.55, 0.65, cr_noise(local_pos * 9.0));
	} else {
		m = smoothstep(0.32, 0.55, length(local_pos));
	}
	return mix(corrupt_a, corrupt_b, m) * (0.5 + 0.75 * l);
}

void fragment() {
	vec3 col = mix(COLOR.rgb, tint, tint_amount);
	// Eyes and other glowing parts keep their own colours; everything else takes the tank colours.
	bool paintable = !(COLOR.a > 0.8 && COLOR.a < 0.9);
	if (corrupt > 0.001 && paintable) col = mix(col, tank_colours(col), corrupt);
	ALBEDO = col;
	ROUGHNESS = mix(rough, rough * 0.35, corrupt);
	SPECULAR = mix(0.4, 0.75, corrupt);
	RIM = 0.25;
	RIM_TINT = 0.4;
	float a = COLOR.a;
	vec3 emit = vec3(flash) + col * 0.1;
	if (a > 0.8 && a < 0.9) emit += col * (1.2 + glow * 2.0);
	if (a < 0.6) emit += glow_color * glow * 1.6;
	emit += tint * tint_amount * 0.25;
	// The Leak's warm shimmer, slow and gentle, so a corrupted creature reads through the mist.
	float shimmer = 0.55 + 0.45 * sin(TIME * 1.6 + phase);
	emit += (mix(corrupt_a, corrupt_b, 0.5) * 0.28 + vec3(1.0, 0.72, 0.4) * 0.1) * shimmer * corrupt;
	EMISSION = emit;
	// Caustics and dappled sunlight play over it, except in the shade of rock.
	EMISSION += ALBEDO * sun_light;
}
