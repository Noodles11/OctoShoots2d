// The reef's skin (THEME-BIBLE §6.3, §7 Depth 1): one look for every solid surface of a level — the seabed, the walls,
// the arches, the cave roofs, boulders and weak rock — worked out in world space from height and slope, so pieces made
// separately meet seamlessly. Colours come from the depth's look (reef_* globals, set by ReefLook, linear).
//
//   Seabed (flat, under the swim level): rippled sand whose crests catch the sun, shell grit, soft drifts of algae.
//   Reef walls: limestone with strata and dark crevices, crusted with lilac and pink coralline algae, turf on ledges.
//   Reef tops: coral gardens, a mosaic of rounded heads in the depth's coral colours with polyp dots.
//   Light: sharp moving caustics on everything facing up, fading with depth; red light absorbed as the water deepens.
//   The Crack: an orange glow breathing up from the deepest fissure (reef_crack: x, z, radius, strength).

#include "res://assets/shaders/sunlight.gdshaderinc"
#include "res://assets/shaders/level_frame.gdshaderinc"

global uniform vec3 reef_water_shallow;
global uniform vec3 reef_water_deep;
global uniform vec3 reef_murk;
global uniform vec3 reef_sand;
global uniform vec3 reef_rock;
global uniform vec3 reef_coral;
global uniform vec3 reef_coral2;
global uniform vec3 reef_algae;
global uniform vec3 reef_sponge;
global uniform vec3 reef_butter;
global uniform vec3 reef_sun;
global uniform float reef_caustics;
global uniform float reef_menace;
global uniform vec4 reef_crack;
/// 0 on a level drawn below the current one: the Crack (a global) belongs to the current level.
uniform float crack_mask = 1.0;
// The corruption (docs/CORRUPTION.md): this level's field, R = ink density, G = the gold of corruption dying (set per
// material by LevelView; off on any level that is not the one she is on or diving into).
uniform sampler2D corruption_map : filter_linear, repeat_disable;
uniform float corruption_on = 0.0;

struct ReefSkin {
	vec3 albedo;
	vec3 emission;
	vec3 normal;    // world space, perturbed by sand ripples and coral heads
	float rough;
	float caustic;  // extra sunlight from the caustics (the shaders' light() adds it where the sun reaches)
};

float rs_hash3(vec3 p) {
	p = fract(p * vec3(0.1031, 0.1030, 0.0973));
	p += dot(p, p.yxz + 33.33);
	return fract((p.x + p.y) * p.z);
}

float rs_noise(vec3 p) {
	vec3 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(mix(rs_hash3(i), rs_hash3(i + vec3(1, 0, 0)), f.x), mix(rs_hash3(i + vec3(0, 1, 0)), rs_hash3(i + vec3(1, 1, 0)), f.x), f.y),
	           mix(mix(rs_hash3(i + vec3(0, 0, 1)), rs_hash3(i + vec3(1, 0, 1)), f.x), mix(rs_hash3(i + vec3(0, 1, 1)), rs_hash3(i + vec3(1, 1, 1)), f.x), f.y), f.z);
}

float rs_fbm(vec3 p) {
	float s = 0.5 * rs_noise(p);
	s += 0.25 * rs_noise(p * 2.03 + 17.1);
	s += 0.125 * rs_noise(p * 4.07 + 31.7);
	return s / 0.875;
}

// Cells on the plane: x = distance to the nearest seed, y = distance to the border with the next, z = the cell's id;
// to_seed is the offset from p to that seed.
vec3 rs_cells_to(vec2 p, out vec2 to_seed) {
	vec2 i = floor(p), f = fract(p);
	float d1 = 9.0, d2 = 9.0, id = 0.0;
	to_seed = vec2(0.0);
	for (int y = -1; y <= 1; y++)
	for (int x = -1; x <= 1; x++) {
		vec2 g = vec2(float(x), float(y));
		float h = rs_hash3(vec3(i + g, 4.7));
		vec2 o = g + vec2(h, rs_hash3(vec3(i + g, 9.2))) * 0.85 + 0.075 - f;
		float d = dot(o, o);
		if (d < d1) { d2 = d1; d1 = d; id = h; to_seed = o; }
		else if (d < d2) d2 = d;
	}
	return vec3(sqrt(d1), sqrt(d2) - sqrt(d1), id);
}

vec3 rs_cells(vec2 p) {
	vec2 unused;
	return rs_cells_to(p, unused);
}

// Rippled sand: long ripples whose heading wanders, crests lit and troughs shaded through the normal.
vec3 rs_sand(vec3 w, inout vec3 n, float px) {
	vec2 q = w.xz;
	float heading = (rs_noise(vec3(q * 0.025, 5.1)) - 0.5) * 2.6;
	vec2 dir = vec2(cos(heading), sin(heading));
	// Sinuous crests that wander, fork and fade: a strongly warped phase, its spacing drifting from place to place.
	float warp = rs_fbm(vec3(q * 0.07, 1.3)) * 11.0 + rs_noise(vec3(q * 0.35, 6.1)) * 1.6;
	float spacing = mix(1.9, 3.1, rs_noise(vec3(q * 0.03, 9.4)));
	float phase = dot(q, dir) * spacing + warp;
	float ripple = sin(phase);
	// Ripples are asymmetric: a gentle stoss side and a steep lee side.
	float slope = cos(phase) * (ripple > 0.0 ? 0.55 : 0.3);
	float field = smoothstep(0.42, 0.72, rs_fbm(vec3(q * 0.05, 3.3)));     // ripple fields, with flat sand between them
	field *= 1.0 - smoothstep(0.12, 0.4, px);                               // and none where a pixel spans a ripple
	n = normalize(n + vec3(dir.x, 0.0, dir.y) * slope * 0.26 * field);
	vec3 c = reef_sand * (0.95 + 0.07 * smoothstep(0.3, 1.0, ripple) * field - 0.07 * (1.0 - smoothstep(-1.0, -0.2, ripple)) * field);
	c *= 0.9 + 0.18 * rs_noise(vec3(q * 4.3, 2.0));                          // grain
	// Shell grit: pale chips scattered in the troughs.
	vec3 cell = rs_cells(q * 2.2);
	float grit = (1.0 - smoothstep(0.035, 0.07, cell.x)) * step(0.78, cell.z);
	c = mix(c, mix(vec3(1.0, 0.86, 0.86), vec3(0.96, 0.94, 0.86), step(0.9, cell.z)), grit * 0.75);
	// Soft drifts of darker sand and algae film.
	float drift = smoothstep(0.52, 0.78, rs_fbm(vec3(q * 0.045, 8.0)));
	c = mix(c, c * mix(vec3(1.0), reef_algae * 1.6, 0.55), drift * 0.35);
	return c;
}

// Bare reef rock, crusted with life.
vec3 rs_rock(vec3 w, vec3 n) {
	float f = rs_fbm(w * 0.32);
	float strata = 0.5 + 0.5 * sin(w.y * 2.6 + f * 5.0);
	vec3 c = reef_rock * (0.74 + 0.34 * f) * (0.88 + 0.14 * strata);
	// Coralline crust in lilac and pink patches over the faces.
	float crust = smoothstep(0.5, 0.66, rs_fbm(w * 0.42 + 11.0));
	vec3 crustCol = mix(reef_coral2, reef_coral, smoothstep(0.35, 0.65, rs_noise(w * 0.16 + 4.0)));
	c = mix(c, crustCol * (0.85 + 0.25 * rs_noise(w * 3.0)), crust * 0.8);
	// Turf on ledges and the faces near the swim level.
	float turf = smoothstep(0.25, 0.75, n.y) * smoothstep(0.42, 0.6, rs_fbm(w * 0.6 + 21.0));
	c = mix(c, reef_algae * (0.8 + 0.3 * rs_noise(w * 5.0)), turf * 0.65);
	// Dark, cool crevices.
	float crev = 1.0 - smoothstep(0.24, 0.42, rs_fbm(w * 0.85 + 3.0));
	c = mix(c, reef_murk * 0.55, crev * 0.6);
	return c;
}

// A coral garden on the reef tops: rounded heads of many corals, each lit like a small dome, with its own texture
// (brain coral meanders, polyp-dotted mounds, ridged plates, soft fuzz) and dark seams between them.
vec3 rs_garden(vec3 w, inout vec3 n) {
	vec2 to_seed;
	vec2 q = w.xz * 1.9 + vec2(rs_fbm(w * 0.7), rs_fbm(w * 0.7 + 5.0)) * 1.1;
	vec3 cell = rs_cells_to(q, to_seed);
	// Colonies: neighbouring heads tend to share a colour (a slow field picks it, the cell nudges it).
	float id = fract(rs_fbm(w * 0.2 + 2.0) * 1.8 + cell.z * 0.3);
	vec3 c = id < 0.3 ? reef_coral : id < 0.5 ? reef_coral2 : id < 0.62 ? reef_butter : id < 0.8 ? reef_algae : reef_sponge;
	float r = clamp(cell.x / 0.62, 0.0, 1.0);
	// The dome: the normal leans away from the head's centre.
	n = normalize(n + vec3(-to_seed.x, 0.0, -to_seed.y) * 0.9 * r);
	vec2 lp = -to_seed * 9.0;
	float kind = fract(cell.z * 7.31);
	if (kind < 0.3) {
		// Brain coral: meandering valleys.
		float m = sin(lp.x * 1.3 + 2.2 * sin(lp.y * 0.9 + cell.z * 9.0)) * sin(lp.y * 1.2 - 1.8 * sin(lp.x * 0.8));
		c *= 0.78 + 0.32 * smoothstep(-0.2, 0.4, m);
	} else if (kind < 0.55) {
		// Polyps: bright cups all over the mound.
		vec3 pc = rs_cells(lp * 1.4);
		c = mix(c * 0.85, c * 1.3 + 0.03, 1.0 - smoothstep(0.08, 0.2, pc.x));
	} else if (kind < 0.75) {
		// A plate: ridges radiating from its middle.
		float ang = atan(to_seed.y, to_seed.x);
		c *= 0.82 + 0.25 * (0.5 + 0.5 * sin(ang * 22.0));
	} else {
		// Soft coral fuzz.
		c *= 0.86 + 0.24 * rs_noise(vec3(lp * 2.5, cell.z * 30.0));
	}
	c *= 1.05 - 0.35 * r * r;
	float seam = 1.0 - smoothstep(0.015, 0.07, cell.y);
	c = mix(c, reef_murk * 0.3, seam * 0.8);
	return c;
}

// local: the point in its level's own coordinates (level_frame.gdshaderinc); patterns are drawn at its absolute
// position, so a level looks the same wherever it is placed. px: how many metres one pixel spans here (from the
// caller's derivatives), to keep fine patterns from aliasing.
ReefSkin reef_surface(vec3 local, vec3 n, float time, float px) {
	ReefSkin s;
	vec3 w = lf_pattern(local);
	float up = n.y;
	// Height relative to the swim level (0): the seabed lies at about -1, the walls rise above 0.
	float h = local.y;

	// Sand on the flat seabed (and a little way up the foot of the walls); gardens on flat tops; rock elsewhere.
	// The weights come first, and each look is worked out only where it shows (most pixels need just one): the
	// three noise stacks are the reef's main cost on a big screen.
	float sandK = smoothstep(0.72, 0.92, up) * (1.0 - smoothstep(-0.4, 0.5 + rs_noise(w * 0.3) * 0.8, h));
	float gardenK = smoothstep(0.55, 0.85, up) * smoothstep(1.2, 2.6, h);
	if (gardenK > 0.0 && sandK < 0.999) gardenK *= smoothstep(0.3, 0.5, rs_fbm(w * 0.18 + 7.0));
	else gardenK = 0.0;
	vec3 nn = n;
	vec3 sand = sandK > 0.001 ? rs_sand(w, nn, px) : vec3(0.0);
	vec3 rock = (1.0 - gardenK) * (1.0 - sandK) > 0.001 ? rs_rock(w, n) : vec3(0.0);
	vec3 gn = n;
	vec3 garden = gardenK * (1.0 - sandK) > 0.001 ? rs_garden(w, gn) : vec3(0.0);
	vec3 c = mix(rock, garden, gardenK);
	float apron = (1.0 - smoothstep(0.8, 0.97, up)) * sandK;
	sand = mix(sand, sand * mix(vec3(0.72), reef_algae * 1.1, 0.4), apron);
	c = mix(c, sand, sandK);
	s.normal = normalize(mix(mix(n, gn, gardenK), nn, sandK));
	s.rough = mix(mix(0.82, 0.62, gardenK), 0.96, sandK);

	// The impassable reef wall around the square: darker and cooler toward the edge of the world.
	float edge = min(min(local.x, local.z), min(reef_extent.x - local.x, reef_extent.y - local.z));
	float rim = 1.0 - smoothstep(0.0, 9.0, edge);
	c = mix(c, c * 0.55 + reef_murk * 0.2, rim * 0.6);

	// Water steals red first: the deeper the floor, the bluer and dimmer it is.
	float deep = clamp((-h - 1.0) / 8.0, 0.0, 1.0);
	c = mix(c, c * vec3(0.6, 0.86, 0.95), deep);
	c = mix(c, reef_water_deep * 0.5, deep * 0.25);

	s.albedo = c;

	// Caustics on everything facing up (sharp near the surface, softer and fainter below). They are sunlight, so the
	// light() pass adds them only where the sun reaches: the walls' shade cuts them off.
	float face = pow(clamp(s.normal.y, 0.0, 1.0), 1.4);
	// The light's path is measured from the surface at the point's true height (the level's own height plus where the
	// level sits now): during the dive the sea surface sinks with her, so the light on the level below is already what
	// it will be once that level becomes the level — nothing jumps when the world is moved back.
	vec3 lit_at = vec3(w.x, local.y + level_offset.y, w.z);
	s.caustic = sun_caustics(lit_at, time) * reef_caustics * light_strength * face;
	s.emission = vec3(0.0);

	// The Crack: an orange glow breathing up out of the deepest fissure (only on the level it belongs to).
	if (reef_crack.w > 0.0 && crack_mask > 0.0) {
		float d = distance(local.xz, reef_crack.xy);
		float inside = (1.0 - smoothstep(reef_crack.z * 0.4, reef_crack.z, d)) * (1.0 - smoothstep(-4.5, -1.6, h));
		float breathe = 0.72 + 0.28 * sin(time * 1.1 + d * 0.4);
		float flicker = 0.75 + 0.5 * rs_noise(vec3(w.xz * 0.9, time * 0.35));
		vec3 ember = mix(vec3(1.0, 0.16, 0.02), vec3(1.0, 0.36, 0.06), smoothstep(-7.0, -3.0, h));
		s.emission += ember * inside * breathe * flicker * reef_crack.w * crack_mask * 0.9;
		s.albedo = mix(s.albedo, vec3(0.05, 0.03, 0.03), inside * 0.8);
	}
	// The corruption on the floor: ink-black, glossy, drinking the caustics, threaded with faint violet veins. Where it
	// dies it burns gold (light spreading, never erasing); its retreating edge keeps a warm fringe. Walls above the
	// floor stay clean, the ink only licking their feet.
	if (corruption_on > 0.0) {
		vec2 cr = texture(corruption_map, local.xz / reef_extent).rg;
		float floor_k = 1.0 - smoothstep(-0.2, 0.9, h);
		float ink = smoothstep(0.03, 0.7, cr.r) * floor_k * corruption_on;
		float vein = 1.0 - smoothstep(0.0, 0.07, abs(rs_noise(vec3(w.xz * 0.55, time * 0.05)) - 0.5));
		vein *= 0.55 + 0.45 * sin(time * 0.9 + w.x * 0.31 + w.z * 0.23);
		vec3 ink_col = vec3(0.016, 0.011, 0.026) + vec3(0.06, 0.012, 0.11) * vein;
		s.albedo = mix(s.albedo, ink_col, ink * 0.95);
		s.rough = mix(s.rough, 0.2, ink);
		s.caustic *= 1.0 - ink * 0.9;
		s.emission += vec3(0.14, 0.03, 0.24) * vein * ink * 0.22;
		float fringe = smoothstep(0.02, 0.16, cr.r) * (1.0 - smoothstep(0.16, 0.42, cr.r));
		s.emission += (vec3(1.0, 0.7, 0.3) * cr.g * 1.5 + vec3(1.0, 0.62, 0.26) * fringe * 0.07) * floor_k * corruption_on;
	}
	return s;
}
