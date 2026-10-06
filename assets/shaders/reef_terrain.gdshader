// The reef's rock and seabed, painted the way a Caribbean fore-reef looks.
// Sand settles where the ground is low and flat (the seabed heightfield is passed in as a texture);
// everything else is old limestone reef rock: grey-brown, pitted, crusted with pink coralline algae,
// furred with green-brown turf algae on top in the shallows, dark and sponge-spotted under overhangs.
// Sunlight caustics dance on upward faces and fade with depth.
shader_type spatial;

#include "res://assets/shaders/sunlight.gdshaderinc"

uniform sampler2D seabed : filter_linear, repeat_disable;
uniform vec2 reef_size = vec2(224.0, 224.0);
uniform float has_seabed = 0.0;
uniform float surface_y = 90.0;
uniform float caustic_strength = 1.6;

varying vec3 world_pos;
varying vec3 world_normal;

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

float hash(vec3 p) {
	p = fract(p * 0.3183099 + 0.1);
	p *= 17.0;
	return fract(p.x * p.y * p.z * (p.x + p.y + p.z));
}

float noise(vec3 x) {
	vec3 i = floor(x), f = fract(x);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(mix(hash(i), hash(i + vec3(1, 0, 0)), f.x), mix(hash(i + vec3(0, 1, 0)), hash(i + vec3(1, 1, 0)), f.x), f.y),
	           mix(mix(hash(i + vec3(0, 0, 1)), hash(i + vec3(1, 0, 1)), f.x), mix(hash(i + vec3(0, 1, 1)), hash(i + vec3(1, 1, 1)), f.x), f.y), f.z);
}

float fbm(vec3 p) {
	return noise(p) * 0.62 + noise(p * 2.03) * 0.31;
}

// Height of the surface detail (for bump mapping): sand ripples or pitted rock.
float detail(vec3 p, float sand) {
	float ripple_dir = noise(p * 0.08) * 1.2;
	vec2 d = vec2(cos(ripple_dir), sin(ripple_dir));
	float ripples = sin(dot(p.xz, d) * 9.0 + noise(p * 0.6) * 4.0) * 0.5 + 0.5;
	ripples = ripples * ripples * (0.6 + 0.4 * noise(p * 0.3));
	float rock = noise(p * 2.2) * 0.6 + noise(p * 5.0) * 0.3;
	return mix(rock, ripples * 0.7, sand);
}

void fragment() {
	vec3 p = world_pos;
	vec3 n = world_normal;
	float up = n.y;
	float depth = max(surface_y - p.y, 0.0);
	float shallow = exp(-depth / 35.0);

	// Where is sand? Up-facing ground near the seabed heightfield (or any flat floor without one).
	float flat_ground = smoothstep(0.62, 0.88, up);
	float near_seabed = 1.0;
	if (has_seabed > 0.5) {
		float floor_h = texture(seabed, p.xz / reef_size).r;
		near_seabed = 1.0 - smoothstep(0.6, 1.8, p.y - floor_h);
	}
	float sand = flat_ground * near_seabed * smoothstep(0.25, 0.45, fbm(p * 0.12) + 0.15);

	// Sand: warm coral sand, paler and darker patches, specks of shell and rubble.
	vec3 sand_col = mix(vec3(0.6, 0.53, 0.4), vec3(0.72, 0.66, 0.53), fbm(p * 0.35));
	sand_col = mix(sand_col, vec3(0.55, 0.52, 0.45), smoothstep(0.55, 0.8, fbm(p * 0.07 + 5.0)) * 0.6);
	// Specks of shell: tiny round pale grains, a few darker bits of rubble.
	vec3 cell = p * 14.0;
	float grain_d = length(fract(cell) - 0.5);
	float pick = hash(floor(cell));
	sand_col = mix(sand_col, vec3(0.96, 0.94, 0.88), step(0.94, pick) * (1.0 - smoothstep(0.12, 0.2, grain_d)));
	sand_col = mix(sand_col, vec3(0.45, 0.4, 0.36), step(0.985, 1.0 - pick) * (1.0 - smoothstep(0.08, 0.14, grain_d)) * 0.6);

	// Rock: old reef limestone with crevices, coralline crusts, turf algae, sponge spots under overhangs.
	float grain = fbm(p * 1.6);
	vec3 rock_col = mix(vec3(0.36, 0.33, 0.3), vec3(0.52, 0.48, 0.42), grain);
	float crevice = smoothstep(0.55, 0.8, 1.0 - fbm(p * 0.9 + 11.0));
	rock_col *= 1.0 - 0.45 * crevice;
	float coralline = smoothstep(0.52, 0.68, fbm(p * 0.6 + 3.0)) * (0.4 + 0.6 * shallow) * smoothstep(-0.6, 0.3, up);
	rock_col = mix(rock_col, vec3(0.78, 0.46, 0.55), coralline * 0.85);
	float turf = smoothstep(0.35, 0.8, up) * shallow * smoothstep(0.45, 0.6, fbm(p * 0.4 + 7.0));
	rock_col = mix(rock_col, vec3(0.34, 0.4, 0.2), turf * 0.8);
	float under = smoothstep(0.0, -0.6, up);
	float sponge = smoothstep(0.6, 0.72, fbm(p * 0.8 + 19.0));
	vec3 sponge_col = mix(vec3(0.85, 0.42, 0.12), vec3(0.45, 0.18, 0.5), step(0.5, noise(p * 0.2 + 2.0)));
	rock_col = mix(rock_col, rock_col * 0.55, under);
	rock_col = mix(rock_col, sponge_col, sponge * (0.35 + 0.5 * under));

	vec3 col = mix(rock_col, sand_col, sand);
	// Deeper water: darker and bluer.
	col *= mix(vec3(0.55, 0.68, 0.8), vec3(1.0), shallow);

	// Bump mapping from the detail height, in world space.
	float e = 0.03;
	float h0 = detail(p, sand);
	vec3 grad = vec3(detail(p + vec3(e, 0, 0), sand) - h0, detail(p + vec3(0, e, 0), sand) - h0, detail(p + vec3(0, 0, e), sand) - h0) / e;
	vec3 bumped = normalize(n - (grad - n * dot(grad, n)) * mix(0.06, 0.035, sand));
	NORMAL = normalize((VIEW_MATRIX * vec4(bumped, 0.0)).xyz);

	ALBEDO = col;
	ROUGHNESS = mix(0.88, 0.95, sand);
	SPECULAR = 0.25;

	// Sunlight: rippling caustics on faces turned to the sun, only where no rock shades them, plus a soft
	// dappled brightening (the spots of light) so the ground glows in patches under the beams.
	float facing = smoothstep(0.05, 0.8, dot(bumped, -normalize(sun_dir)));
	float lit = sun_lit(p) * facing;
	float caustic = sun_caustics(p, TIME);
	float dapple = sun_shafts(p, TIME);
	EMISSION = col * (caustic * caustic_strength + dapple * 0.5) * lit * light_strength;
}
