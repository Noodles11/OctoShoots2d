// The pufferling (docs/PUFFERLING-PROPOSAL.md §2): one body for the healthy fish and the corrupted one. The mesh
// (PufferlingMesh) carries both its calm shape and the ball it blows up into; this shader blends them, swims it (the
// tail stem wiggling with the dorsal and anal fins, the pectorals fluttering, the tail sweeping and bending into
// turns), raises, fires and regrows the spines, and paints it.
//   Healthy: a sand-olive back with dark brown spots, a cream belly, gold stripes on the cheeks; dark eyes, gold rings.
//   Corrupted (THEME-BIBLE §6.4): starfire red with acid-lime spots in a too-even pattern, a plastic gloss, a slow warm
//   shimmer of the Leak, and a heavy brow over eyes ringed acid-green. `corrupt` blends between them: freeing washes
//   the tank colours out.
shader_type spatial;
render_mode cull_disabled;

#include "res://assets/shaders/sunlight.gdshaderinc"

instance uniform float inflate = 0.0;
instance uniform float spines = 1.0;
instance uniform float swim = 0.0;        // 0 hovering → 1 cruising (→ 1.5 hurrying)
instance uniform float tail_phase = 0.0;  // the swimming stroke
instance uniform float fin_phase = 0.0;   // the pectorals' flutter
instance uniform float turn = 0.0;        // −1 hard left … +1 hard right: the tail bends into it
instance uniform float corrupt = 0.0;
instance uniform float flash = 0.0;
instance uniform float pattern = 0.0;     // shifts the healthy spots, so no two look alike

varying vec3 skin;
varying float part;
varying float along;
varying vec2 eye_uv;
varying float sun;

vec3 rot_y(vec3 v, float a) { float c = cos(a), s = sin(a); return vec3(c * v.x + s * v.z, v.y, -s * v.x + c * v.z); }
vec3 rot_x(vec3 v, float a) { float c = cos(a), s = sin(a); return vec3(v.x, c * v.y - s * v.z, s * v.y + c * v.z); }

// The stem's sideways wiggle at a point along the body (none at the head, most at the tail).
float wiggle(float x) {
	float rear = 1.0 - smoothstep(-0.48, 0.15, x);
	return sin(tail_phase - x * 4.0) * 0.035 * rear * (0.35 + 0.65 * min(swim, 1.2)) * (1.0 - inflate);
}

void vertex() {
	part = CUSTOM0.w;
	along = CUSTOM3.w;
	skin = VERTEX;
	eye_uv = CUSTOM1.xy;
	float k = inflate;
	vec3 p = mix(VERTEX, CUSTOM0.xyz, k);
	vec3 n = normalize(mix(NORMAL, CUSTOM3.xyz, k));
	vec3 attach = mix(CUSTOM1.xyz, CUSTOM2.xyz, k);
	float f = CUSTOM1.w;
	float side = CUSTOM2.w;
	if (part > 2.5 && part < 3.5) {
		// Pectorals: a quick flutter about the root, fanning fore and aft.
		float a = side * (0.25 + (0.45 + 0.15 * swim) * sin(fin_phase + side * 0.8)) * f;
		p = attach + rot_y(p - attach, a);
		n = rot_y(n, a);
	} else if (part > 3.5 && part < 5.5) {
		// Dorsal and anal fins: fanning side to side together, in time with the stem — they push it along.
		float a = sin(tail_phase + 0.7) * 0.55 * (0.4 + 0.6 * min(swim, 1.2)) * f;
		p = attach + rot_x(p - attach, a);
		n = rot_x(n, a);
	} else if (part > 5.5 && part < 6.5) {
		// The tail fan: a slow sweep, bending into turns.
		float a = (sin(tail_phase - 1.2) * 0.32 * (0.35 + 0.65 * min(swim, 1.2)) + turn * 0.5) * (0.3 + 0.7 * f);
		p = attach + rot_y(p - attach, a);
		n = rot_y(n, a);
	} else if (part > 6.5) {
		// Spines: shrunk to their roots once fired, growing back after.
		p = attach + (p - attach) * spines;
	}
	if (part < 0.5 || part > 2.5) p.z += wiggle(attach.x * step(2.5, part) + p.x * (1.0 - step(2.5, part)));
	// Breathing: the gills pump, the body swells a hair.
	p *= 1.0 + 0.012 * sin(TIME * 2.3 + pattern * 7.0) * (1.0 - k);
	VERTEX = p;
	NORMAL = n;
	vec3 world = (MODEL_MATRIX * vec4(p, 1.0)).xyz;
	vec3 wn = normalize((MODEL_MATRIX * vec4(n, 0.0)).xyz);
	// The sun's caustics dapple its back.
	sun = sun_caustics(world, TIME) * clamp(wn.y, 0.0, 1.0) * light_strength;
}

float hash3(vec3 p) {
	p = fract(p * 0.3183099 + 0.1);
	p *= 17.0;
	return fract(p.x * p.y * p.z * (p.x + p.y + p.z));
}

float noise3(vec3 x) {
	vec3 i = floor(x), f = fract(x);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(mix(hash3(i), hash3(i + vec3(1, 0, 0)), f.x), mix(hash3(i + vec3(0, 1, 0)), hash3(i + vec3(1, 1, 0)), f.x), f.y),
	           mix(mix(hash3(i + vec3(0, 0, 1)), hash3(i + vec3(1, 0, 1)), f.x), mix(hash3(i + vec3(0, 1, 1)), hash3(i + vec3(1, 1, 1)), f.x), f.y), f.z);
}

// Round spots scattered over the skin (healthy): a cell pattern on the body's own surface.
float spots(vec3 q) {
	vec3 c = floor(q), f = fract(q);
	float d = 9.0;
	for (int z = -1; z <= 1; z++)
	for (int y = -1; y <= 1; y++)
	for (int x = -1; x <= 1; x++) {
		vec3 o = vec3(float(x), float(y), float(z));
		vec3 j = vec3(hash3(c + o), hash3(c + o + 7.1), hash3(c + o + 13.3));
		d = min(d, length(o + j * 0.8 + 0.1 - f) * (0.8 + 0.5 * hash3(c + o + 3.7)));
	}
	return 1.0 - smoothstep(0.22, 0.32, d);
}

void fragment() {
	float up = clamp(skin.y / 0.3 * 0.5 + 0.5, 0.0, 1.0);
	float ang = atan(skin.y, skin.z);
	vec3 col;
	float rough = mix(0.62, 0.22, corrupt);
	float spec = mix(0.35, 0.75, corrupt);
	vec3 glow = vec3(0.0);

	// The two skins, painted separately and blended by `corrupt`.
	vec3 back_h = vec3(0.50, 0.44, 0.27), belly_h = vec3(0.93, 0.88, 0.74);
	vec3 back_c = vec3(1.0, 0.13, 0.17), belly_c = vec3(1.0, 0.70, 0.74);
	float shade = smoothstep(0.32, 0.68, up);
	vec3 healthy = mix(belly_h, back_h, shade);
	vec3 tank = mix(belly_c, back_c, smoothstep(0.25, 0.6, up));

	if (part < 0.5) {
		// Healthy: dark spots on the back and flanks, gold cheek stripes, a scale grain.
		float sp = spots(skin * vec3(13.0, 14.0, 14.0) + pattern * 31.0) * smoothstep(0.25, 0.55, up);
		healthy = mix(healthy, vec3(0.22, 0.15, 0.08), sp * 0.85);
		float cheek = smoothstep(0.05, 0.22, skin.x) * (1.0 - smoothstep(0.3, 0.75, abs(skin.y + 0.02) / 0.3)) * step(0.12, abs(skin.z));
		float stripes = smoothstep(0.55, 0.85, sin(skin.y * 70.0 - skin.x * 22.0));
		healthy = mix(healthy, vec3(0.86, 0.64, 0.22), cheek * stripes * 0.75);
		healthy *= 0.92 + 0.12 * noise3(skin * 60.0);
		// Corrupted: big lime spots in rows and columns — too even to be a fish's own.
		vec2 g = vec2(along * 11.0, ang * 3.6);
		vec2 cell = fract(g) - 0.5;
		float dots = 1.0 - smoothstep(0.26, 0.31, length(cell * vec2(1.0, 1.15)));
		tank = mix(tank, vec3(0.38, 1.0, 0.18), dots * smoothstep(0.08, 0.95, along) * (1.0 - step(0.97, along)));
		// The Leak's heavy brow over each eye.
		float brow = 0.0;
		for (int s = -1; s <= 1; s += 2) {
			vec3 eye = vec3(0.205, 0.09, 0.2 * float(s));
			vec3 d = skin - eye;
			float r = length(d.xz * vec2(1.0, 1.4));
			// A short, thin arc just over the eye.
			brow = max(brow, (1.0 - smoothstep(0.008, 0.016, abs(r - 0.098))) * smoothstep(0.035, 0.06, d.y) * (1.0 - smoothstep(0.05, 0.09, abs(d.x))));
		}
		tank = mix(tank, vec3(0.25, 0.02, 0.05), brow);
		col = mix(healthy, tank, corrupt);
	} else if (part < 1.5) {
		// The eye: a dark pupil in a ring (gold when healthy, acid green when corrupted), a wet highlight.
		float r = length(eye_uv);
		vec3 ring = mix(vec3(0.95, 0.72, 0.25), vec3(0.45, 1.0, 0.2), corrupt);
		col = mix(vec3(0.02, 0.02, 0.03), ring, smoothstep(0.42, 0.5, r) * (1.0 - smoothstep(0.78, 0.86, r)));
		col = mix(col, vec3(0.08, 0.06, 0.04), smoothstep(0.86, 0.95, r));
		col += vec3(1.0) * (1.0 - smoothstep(0.08, 0.16, length(eye_uv - vec2(-0.25, 0.3)))) * 0.9;
		rough = 0.08;
		spec = 0.9;
	} else if (part < 2.5) {
		// The beak: pale plates with a dark seam.
		col = vec3(0.92, 0.88, 0.76) * (1.0 - 0.7 * (1.0 - smoothstep(0.004, 0.012, abs(skin.y + 0.035))));
		rough = 0.4;
	} else if (part < 6.5) {
		// Fins: thin and pale toward the edge, with rays; the corrupted ones lime with a red rim.
		vec3 fin_h = mix(back_h * 1.25, vec3(0.95, 0.9, 0.75), 0.45);
		vec3 fin_c = vec3(0.45, 1.0, 0.25);
		col = mix(fin_h, fin_c, corrupt);
		float rays = 0.85 + 0.15 * sin((skin.y + skin.z) * 140.0);
		col *= rays;
		rough = 0.5;
		// Thin tissue: the light comes through it (seen from above, the upright fins would otherwise sit in shade).
		BACKLIGHT = col * 0.8;
		glow += col * 0.28;
	} else {
		// Spines: lying flat they take the skin's colour (a faint speckle); raised they are bone, tipped dark (lime when
		// corrupted).
		vec3 raised = mix(vec3(0.92, 0.88, 0.74), vec3(0.95, 0.4, 0.45), corrupt * 0.6);
		raised = mix(raised, mix(vec3(0.35, 0.28, 0.2), vec3(0.4, 1.0, 0.2), corrupt), 0.35);
		vec3 flat_ = mix(mix(healthy, tank, corrupt), vec3(0.9, 0.85, 0.7), 0.25);
		col = mix(flat_, raised, smoothstep(0.1, 0.6, inflate));
		rough = 0.4;
	}

	// The Leak's slow warm shimmer: it reads through the mist from afar.
	glow += vec3(1.0, 0.42, 0.18) * corrupt * (0.12 + 0.1 * sin(TIME * 1.4 + pattern * 9.0));
	glow += col * sun * 0.18;
	glow += vec3(1.0, 0.95, 0.9) * flash;
	if (!FRONT_FACING) NORMAL = -NORMAL;
	ALBEDO = col;
	ROUGHNESS = rough;
	SPECULAR = spec;
	CLEARCOAT = corrupt * 0.6;
	CLEARCOAT_ROUGHNESS = 0.2;
	RIM = 0.35;
	RIM_TINT = 0.5;
	EMISSION = glow;
}
