// The reef fish (docs/REEF-FISH-PROPOSAL.md §4): one shader for the three kinds' bodies (ReefFishMeshes), drawn as a
// MultiMesh per kind. It swims them — a body wave growing toward the tail, the tail sweeping and bending into turns,
// the pectorals rowing, the dorsal and anal fins rippling — and paints each species' natural colours:
//   0 Clownfish:  warm orange, three white bars edged in black, black-rimmed fins.
//   1 Blue tang:  royal blue, the black "palette" from the eye to the tail, a yellow tail edged in black.
//   2 Chromis:    pale blue-green with a shifting sheen, clear pale fins, a forked tail.
// Per instance (MultiMesh custom data): x the stroke's phase, y the pectorals' phase, z how hard it swims (0 hovering →
// 1 cruising → 1.5 hurrying), w its turn (−1 hard left … +1 hard right).
shader_type spatial;
render_mode cull_disabled;

#include "res://assets/shaders/sunlight.gdshaderinc"

uniform int kind = 0;
uniform float body_length = 0.45;
uniform float waves = 0.8;      // body waves along its length
uniform float wave_amp = 0.06;  // the wave's sideways reach at the tail, in body lengths
uniform float tail_amp = 0.35;  // the tail's sweep (radians)
uniform float fin_amp = 0.5;    // the pectorals' stroke (radians)

varying float part;
varying float u;
varying float f;
varying float fin_t;
varying float side;
varying vec2 eye_uv;
varying float v;
varying float sun;
varying float seed;
// A murkling's throw turning it (0 → 1, from the instance colour's red): ink creeps over it from the tail.
varying float ink;

vec3 rot_y(vec3 p, float a) { float c = cos(a), s = sin(a); return vec3(c * p.x + s * p.z, p.y, -s * p.x + c * p.z); }
vec3 rot_x(vec3 p, float a) { float c = cos(a), s = sin(a); return vec3(p.x, c * p.y - s * p.z, s * p.y + c * p.z); }

float lateral(float x, float phase, float swim, float turn) {
	float drive = 0.3 + 0.7 * min(swim, 1.4);
	return (sin(phase - x * waves * TAU) * wave_amp * pow(x, 1.6) * drive + turn * 0.12 * x * x) * body_length;
}

void vertex() {
	part = CUSTOM0.w;
	vec3 attach = CUSTOM0.xyz;
	f = CUSTOM1.x;
	side = CUSTOM1.y;
	u = CUSTOM1.z;
	fin_t = CUSTOM1.w;
	eye_uv = CUSTOM2.xy;
	v = part < 0.5 ? CUSTOM2.z : (part > 3.5 && part < 4.5 ? 1.0 : (part > 4.5 && part < 5.5 ? -1.0 : 0.0));
	seed = fract(float(INSTANCE_ID) * 0.6180339);
	ink = COLOR.r;
	float phase = INSTANCE_CUSTOM.x, fin_phase = INSTANCE_CUSTOM.y, swim = INSTANCE_CUSTOM.z, turn = INSTANCE_CUSTOM.w;
	float drive = 0.3 + 0.7 * min(swim, 1.4);
	vec3 p = VERTEX;
	vec3 n = NORMAL;
	if (part > 2.5 && part < 3.5) {
		// Pectorals: rowing about the root, fanning out and back.
		float a = side * (0.3 + fin_amp * (0.6 + 0.4 * drive) * sin(fin_phase + side * 0.6)) * f;
		p = attach + rot_y(p - attach, a);
		n = rot_y(n, a);
	} else if (part > 3.5 && part < 5.5) {
		// Dorsal and anal: a ripple running back along the fin.
		float a = sin(phase * 1.0 - fin_t * 4.0) * 0.2 * (0.4 + 0.6 * drive) * f;
		p = attach + rot_x(p - attach, a);
		n = rot_x(n, a);
	} else if (part > 5.5 && part < 6.5) {
		// The tail: sweeping a beat behind the body, bending into turns, flexing toward its edge.
		float a = (cos(phase - waves * TAU) * tail_amp * drive + turn * 0.45) * (0.45 + 0.55 * f);
		p = attach + rot_y(p - attach, a);
		n = rot_y(n, a);
	} else if (part > 6.5) {
		// Pelvics: a small quick flutter.
		float a = side * (0.15 + 0.2 * sin(fin_phase * 1.3 + 1.0)) * f;
		p = attach + rot_y(p - attach, a);
		n = rot_y(n, a);
	}
	p.z += lateral(u, phase, swim, turn);
	// Breathing: the gills pump.
	p *= 1.0 + 0.01 * sin(TIME * 3.1 + seed * 20.0) * (1.0 - smoothstep(0.1, 0.4, u)) * step(part, 0.5);
	VERTEX = p;
	NORMAL = n;
	vec3 world = (MODEL_MATRIX * vec4(p, 1.0)).xyz;
	vec3 wn = normalize((MODEL_MATRIX * vec4(n, 0.0)).xyz);
	sun = sun_caustics(world, TIME) * clamp(wn.y, 0.0, 1.0) * light_strength;
}

float hash2(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

// A clownfish bar centred at c (bulging by `bulge` at the flank's middle) of half-width w: 1 white, 0.5 its black edge.
float bar(float uu, float vv, float c, float bulge, float w, float edge) {
	float d = abs(uu - (c - bulge * (1.0 - vv * vv)));
	return d < w ? 1.0 : (d < w + edge ? 0.5 : 0.0);
}

// The scales: a fine diamond lattice on the body, darker in its seams.
float scales(float uu, float vv) {
	float a = sin(uu * 150.0 + vv * 26.0), b = sin(uu * 150.0 - vv * 26.0);
	return 1.0 - 0.08 * smoothstep(0.75, 1.0, max(a, b));
}

void fragment() {
	vec3 col;
	float rough = 0.45;
	float spec = 0.45;
	vec3 glow = vec3(0.0);
	bool fin = part > 2.5;

	if (part > 0.5 && part < 1.5) {
		// The eye: a dark pupil in its iris ring, a wet highlight.
		float r = length(eye_uv);
		vec3 iris = kind == 0 ? vec3(0.9, 0.45, 0.08) : (kind == 1 ? vec3(0.12, 0.1, 0.12) : vec3(0.55, 0.72, 0.78));
		col = mix(vec3(0.015), iris, smoothstep(0.42, 0.52, r));
		col = mix(col, vec3(0.03), smoothstep(0.82, 0.95, r));
		col += vec3(1.0) * (1.0 - smoothstep(0.08, 0.17, length(eye_uv - vec2(-0.25, 0.32)))) * 0.95;
		rough = 0.06;
		spec = 0.9;
	} else if (kind == 0) {
		// Clownfish.
		vec3 orange = mix(vec3(1.0, 0.52, 0.12), vec3(0.95, 0.36, 0.04), smoothstep(-0.4, 0.8, v));
		vec3 white = vec3(0.98, 0.97, 0.93), black = vec3(0.03, 0.02, 0.02);
		float b = max(bar(u, v, 0.25, -0.035, 0.036, 0.014), max(bar(u, v, 0.53, 0.05, 0.04 + 0.015 * max(v, 0.0), 0.014), bar(u, v, 0.905, 0.0, 0.028, 0.012)));
		if (part < 0.5) {
			col = b > 0.75 ? white : (b > 0.25 ? black : orange);
			col *= scales(u, v);
			// The lips: a darker line round the mouth.
			col *= 1.0 - 0.5 * (1.0 - smoothstep(0.02, 0.035, u)) * (1.0 - smoothstep(0.05, 0.12, abs(v + 0.12)));
		} else if (part < 6.5 && part > 5.5) {
			col = f > 0.93 ? white : (f > 0.76 ? black : orange);
		} else {
			// Fins: orange, rimmed in black; the middle bar runs up into the dorsal.
			col = orange * 1.05;
			if (part > 3.5 && part < 4.5 && b > 0.75 && f < 0.6) col = white;
			col = mix(col, black, smoothstep(0.7, 0.82, f));
		}
	} else if (kind == 1) {
		// Blue tang.
		vec3 blue = mix(vec3(0.3, 0.55, 1.0), vec3(0.05, 0.24, 0.9), smoothstep(-0.7, 0.5, v));
		vec3 black = vec3(0.02, 0.03, 0.07), yellow = vec3(1.0, 0.82, 0.08);
		if (part < 0.5) {
			// The palette: a black band from behind the eye along the upper flank to the tail stem, and a loop below
			// it enclosing a blue window.
			float vu = 0.62 - 0.62 * smoothstep(0.08, 0.96, u);
			float hw = 0.2 * (1.0 - 0.45 * u);
			float upper = step(abs(v - vu), hw) * step(0.1, u) * step(u, 0.97);
			float k = clamp((u - 0.28) / 0.46, 0.0, 1.0);
			float vl = vu - hw - 0.48 * sin(PI * k);
			float lower = step(abs(v - vl), 0.11 * (0.5 + 0.5 * sin(PI * k))) * step(0.28, u) * step(u, 0.74);
			float back = smoothstep(0.86, 0.95, v) * step(0.12, u);
			col = mix(blue, black, max(max(upper, lower), back));
			// The yellow wedge on the tail stem.
			col = mix(col, yellow, smoothstep(0.86, 0.93, u) * (1.0 - smoothstep(0.35, 0.6, abs(v))));
			col *= scales(u, v);
			// The gill cover's edge.
			col *= 1.0 - 0.25 * (1.0 - smoothstep(0.004, 0.01, abs(u - (0.24 - 0.04 * v * v))));
		} else if (part > 5.5 && part < 6.5) {
			float edge = abs(fin_t * 2.0 - 1.0);
			col = mix(yellow, black, smoothstep(0.72, 0.8, edge) * smoothstep(0.15, 0.35, f));
		} else if (part > 2.5 && part < 3.5) {
			col = mix(blue, yellow, smoothstep(0.45, 0.7, f));
		} else {
			// Dorsal, anal and pelvics: black, edged in bright blue.
			col = mix(black, vec3(0.2, 0.55, 1.0), smoothstep(0.78, 0.88, f));
		}
	} else {
		// Chromis: counter-shaded blue-green, with a sheen that shifts as it turns in the light.
		vec3 back = vec3(0.08, 0.42, 0.5), flank = vec3(0.3, 0.78, 0.72), belly = vec3(0.8, 0.96, 0.9);
		col = mix(belly, flank, smoothstep(-0.7, -0.1, v));
		col = mix(col, back, smoothstep(0.1, 0.7, v));
		float sheen = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 2.0);
		col += vec3(0.05, 0.25, 0.35) * sheen;
		if (part < 0.5) col *= scales(u, v);
		if (fin) {
			col = mix(vec3(0.6, 0.88, 0.82), vec3(0.75, 0.95, 0.9), f);
			if (part > 5.5 && part < 6.5) col *= 1.0 - 0.3 * smoothstep(0.6, 0.95, abs(fin_t * 2.0 - 1.0)) * smoothstep(0.5, 1.0, f);
		}
		spec = 0.6;
		rough = 0.3;
	}
	if (fin) {
		// Thin tissue: rays, and light through it.
		col *= 0.9 + 0.1 * sin(fin_t * 90.0);
		BACKLIGHT = col * 0.75;
		glow += col * 0.18;
		rough = 0.5;
	}
	// A hair of grain so flat colour reads as skin.
	col *= 0.95 + 0.07 * hash2(floor(vec2(u * 260.0, v * 40.0)) + seed * 13.0);
	glow += col * sun * 0.2;
	if (!FRONT_FACING) NORMAL = -NORMAL;
	if (ink > 0.0) {
		// Turning: ink-black creeping up from the tail, a cold violet sheen over it.
		float k = smoothstep(0.0, 0.2, ink * 1.4 - (1.0 - u) * 0.4);
		col = mix(col, vec3(0.01, 0.006, 0.02), k);
		glow = mix(glow, vec3(0.3, 0.09, 0.6) * 0.5, k);
	}
	ALBEDO = col;
	ROUGHNESS = rough;
	SPECULAR = spec;
	RIM = 0.3;
	RIM_TINT = 0.5;
	EMISSION = glow;
}
