// The flounder (docs/ADVENTURE.md §4; its body baked by FlounderMesh): a flat fish of the sand. Its back is mottled sand
// in light and dark blotches, flecked pale, with orange spots ringed in cream, the lateral line arching over its pectoral
// and the gill cover's curve behind its eyes; its belly is cream. The fin fringe round its edges is thin and lit through,
// barred with rays, dark-spotted near its pale edge; the tail fan is rayed with a dark band. Its eyes are gold-ringed,
// their pupils dark slots. It moves as a flatfish does: the fringe ripples back along both sides; swimming, a wave runs
// down its body and tail, up and down; bend curls it sideways (its shimmy into the sand).
// Per instance: phase (its stroke), swim (0 lying still → 1 swimming), bend (its sideways curl, radians), ink (turning:
// 0 → 1, ink creeping in from its edges), seed.
shader_type spatial;
render_mode cull_disabled;

#include "res://assets/shaders/sunlight.gdshaderinc"

instance uniform float phase = 0.0;
instance uniform float swim = 0.0;
instance uniform float bend = 0.0;
instance uniform float ink = 0.0;
instance uniform float seed = 0.0;

varying float part;
varying float outw;
varying float round_;
varying float along;
varying vec2 rest;
varying float sun;

void vertex() {
	part = UV.x;
	outw = UV.y;
	rest = UV2;
	round_ = COLOR.r;
	along = COLOR.g;
	vec3 p = VERTEX;
	// The fringe ripples: waves running back along each side, stronger as it swims.
	if (part > 1.5 && part < 2.5) {
		float from_head = round_ < 0.5 ? round_ : 1.0 - round_;
		p.y += (0.014 + 0.03 * swim) * sin(phase * 1.3 - from_head * TAU * 3.0 + seed) * pow(outw, 1.3);
	}
	// Swimming: a wave down its body, up and down, growing toward the tail; the tail beats on it.
	float tail = smoothstep(0.3, 1.0, along);
	p.y += swim * 0.07 * sin(phase - along * 5.0) * tail;
	if (part > 2.5 && part < 3.5) p.y += (0.012 + 0.05 * swim) * sin(phase - 2.0) * outw;
	// Its shimmy: a sideways curl, nose and tail together.
	float x = p.x - 0.03;
	p.z += bend * x * abs(x) * 1.6;
	// Breathing.
	p.y += 0.004 * sin(TIME * 2.3 + seed * 7.0) * step(part, 0.5) * (1.0 - outw);
	VERTEX = p;
	vec3 world = (MODEL_MATRIX * vec4(p, 1.0)).xyz;
	vec3 wn = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
	sun = sun_caustics(world, TIME) * clamp(wn.y, 0.0, 1.0) * light_strength;
}

float hash2(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float noise2(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash2(i), hash2(i + vec2(1.0, 0.0)), f.x), mix(hash2(i + vec2(0.0, 1.0)), hash2(i + vec2(1.0, 1.0)), f.x), f.y);
}

float fbm(vec2 p) {
	return 0.55 * noise2(p) + 0.3 * noise2(p * 2.1 + 3.7) + 0.15 * noise2(p * 4.3 + 9.1);
}

// Its spots: a few orange spots ringed in cream, scattered over a loose grid (1 core, 0.5 ring, 0 none).
float spots(vec2 q) {
	vec2 cell = floor(q / 0.13);
	float best = 0.0;
	for (int i = -1; i <= 1; i++)
	for (int j = -1; j <= 1; j++) {
		vec2 c = cell + vec2(float(i), float(j));
		float h = hash2(c + seed);
		if (h < 0.7) continue;
		vec2 at = (c + 0.2 + 0.6 * vec2(hash2(c + 7.1 + seed), hash2(c + 3.3 + seed))) * 0.13;
		float r = 0.022 + 0.014 * hash2(c + 1.7);
		float d = length(q - at);
		best = max(best, d < r ? 1.0 : (d < r + 0.009 ? 0.5 : 0.0));
	}
	return best;
}

void fragment() {
	vec3 sand_dark = vec3(0.22, 0.15, 0.08), sand = vec3(0.5, 0.37, 0.2), sand_light = vec3(0.72, 0.57, 0.34);
	vec3 cream = vec3(0.95, 0.88, 0.7), orange = vec3(1.0, 0.42, 0.06);
	vec2 q = rest + vec2(seed * 0.37, seed * 0.11);
	float blot = fbm(q * 8.0);
	vec3 skin = mix(sand_dark, sand_light, smoothstep(0.3, 0.72, blot));
	skin = mix(skin, sand, 0.25);
	// Pale flecks.
	skin = mix(skin, sand_light * 1.1, step(0.86, hash2(floor(q * 95.0))) * 0.6);
	vec3 col = skin;
	float rough = 0.6, spec = 0.3;
	vec3 glow = vec3(0.0);
	bool fin = part > 1.5 && part < 3.5;
	if (part < 0.5) {
		// Its back.
		float s = spots(rest);
		col = s > 0.75 ? orange : (s > 0.25 ? cream : col);
		// The lateral line, arching over its pectoral; the gill cover's curve behind the eyes.
		float arch = 0.06 * exp(-pow((rest.x - 0.14) / 0.11, 2.0));
		float line_ = (1.0 - smoothstep(0.002, 0.006, abs(rest.y - arch))) * step(-0.38, rest.x) * step(rest.x, 0.3);
		float gill = (1.0 - smoothstep(0.003, 0.007, abs(length((rest - vec2(0.36, 0.0)) * vec2(1.0, 0.75)) - 0.16))) * step(rest.x, 0.3);
		col *= 1.0 - 0.3 * max(line_, gill);
		// The mouth: a short dark curve at the nose.
		col *= 1.0 - 0.6 * (1.0 - smoothstep(0.004, 0.008, abs(length(rest - vec2(0.42, -0.06)) - 0.035))) * step(0.43, rest.x);
		// Darker toward its edge, where the dome meets the fringe.
		col *= mix(1.0, 0.82, smoothstep(0.7, 1.0, outw));
	} else if (part < 1.5) {
		// Its belly.
		col = cream * (0.92 + 0.08 * fbm(q * 12.0));
	} else if (part < 2.5) {
		// The fringe: paler, rayed, dark spots near its pale edge.
		col = mix(skin, sand_light, 0.3);
		col *= 1.0 - 0.2 * smoothstep(0.55, 1.0, sin(round_ * TAU * 70.0));
		float sp = step(0.62, hash2(vec2(floor(round_ * 24.0), seed))) * (1.0 - smoothstep(0.1, 0.18, abs(fract(round_ * 24.0) - 0.5) * 0.3 + abs(outw - 0.62) * 0.5));
		col = mix(col, sand_dark * 0.7, sp);
		// A dark line just in from its pale edge, so its outline reads on the sand.
		col = mix(col, sand_dark * 0.6, (smoothstep(0.7, 0.78, outw) - smoothstep(0.84, 0.9, outw)) * 0.7);
		col = mix(col, cream, smoothstep(0.88, 1.0, outw) * 0.7);
	} else if (part < 3.5) {
		// The tail: rays fanning out, a dark band, a pale edge.
		float ray = atan(rest.y, -(rest.x + 0.3));
		col = mix(skin, sand_light, 0.25);
		col *= 1.0 - 0.2 * smoothstep(0.5, 1.0, sin(ray * 70.0));
		col = mix(col, sand_dark * 0.75, (smoothstep(0.62, 0.7, outw) - smoothstep(0.8, 0.88, outw)) * 0.8);
		col = mix(col, cream, smoothstep(0.9, 1.0, outw) * 0.6);
	} else {
		// An eye: a gold ring round a dark slot of a pupil, a wet highlight.
		vec2 e = rest;
		float r = length(e);
		vec3 iris = vec3(0.86, 0.64, 0.22);
		col = mix(iris, sand_dark, smoothstep(0.82, 0.98, r));
		float pupil = 1.0 - smoothstep(0.3, 0.36, length(e * vec2(0.55, 1.0)));
		col = mix(col, vec3(0.02), pupil);
		col += vec3(1.0) * (1.0 - smoothstep(0.06, 0.14, length(e - vec2(-0.2, -0.25)))) * 0.9;
		rough = 0.08;
		spec = 0.9;
	}
	if (fin) {
		// Thin tissue: light through it.
		BACKLIGHT = col * 0.7;
		glow += col * 0.12;
		rough = 0.5;
	}
	// A hair of grain so flat colour reads as skin.
	col *= 0.95 + 0.07 * hash2(floor(rest * 300.0) + seed * 13.0);
	glow += col * sun * 0.2;
	if (!FRONT_FACING) NORMAL = -NORMAL;
	if (ink > 0.0) {
		// Turning: ink-black creeping in from its edges, a cold violet sheen over it.
		float edge = part < 1.5 ? outw : 1.0;
		float k = smoothstep(0.0, 0.2, ink * 1.4 - (1.0 - edge) * 0.4);
		col = mix(col, vec3(0.01, 0.006, 0.02), k);
		glow = mix(glow, vec3(0.3, 0.09, 0.6) * 0.5, k);
	}
	ALBEDO = col;
	ROUGHNESS = rough;
	SPECULAR = spec;
	RIM = 0.25;
	RIM_TINT = 0.5;
	EMISSION = glow;
}
