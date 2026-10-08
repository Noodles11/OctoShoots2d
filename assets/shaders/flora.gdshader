// Reef flora drawn with MultiMesh. Vertex colour rgb = paint, a = how freely that part sways (0 base → 1 tip).
// Per instance (custom data): r = palette variant (0 or 1), g = brightness, b = unused, a = random phase.
// Recolouring keeps each mesh's light and dark structure but repaints it in the depth's palette (THEME-BIBLE §6.3):
// the paint's lightness picks a colour between the variant's low and high ends.
// Optional looks per species: a sea fan's lattice, a brain coral's meandering grooves, a coral's polyp dots.
shader_type spatial;
render_mode cull_disabled;

#include "res://assets/shaders/sunlight.gdshaderinc"

varying vec3 sun_world;
varying float sun_light;

uniform float sway = 0.08;
uniform float sway_speed = 1.0;
uniform float lattice = 0.0;
uniform float meander = 0.0;
uniform float polyps = 0.0;
uniform float rough = 0.7;
uniform float glow = 0.05;
uniform float backlight = 0.2;
uniform float recolor = 0.0;
uniform vec3 low_a : source_color = vec3(0.2);
uniform vec3 high_a : source_color = vec3(0.8);
uniform vec3 low_b : source_color = vec3(0.2);
uniform vec3 high_b : source_color = vec3(0.8);

varying vec3 tint;
varying vec3 local;

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

void vertex() {
	local = VERTEX;
	vec3 origin = MODEL_MATRIX[3].xyz;
	float ph = INSTANCE_CUSTOM.a * 6.2831 + origin.x * 0.21 + origin.z * 0.17;
	float w = COLOR.a;
	// The surge: everything leans back and forth together, a little out of step.
	float surge = sin(TIME * sway_speed * 0.6 + origin.x * 0.05 + origin.z * 0.03);
	VERTEX.x += (sin(TIME * sway_speed + ph + VERTEX.y * 2.0) * 0.6 + surge * 0.6) * sway * w;
	VERTEX.z += sin(TIME * sway_speed * 0.8 + ph * 1.3 + VERTEX.y * 1.7) * sway * 0.6 * w;
	tint = INSTANCE_CUSTOM.rgb;
	sun_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	// Small things: sunlight is worked out per vertex, which is plenty and far cheaper.
	vec3 sun_n = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
	float sun_face = 0.35 + 0.65 * clamp(dot(sun_n, -normalize(sun_dir)), 0.0, 1.0);
	sun_light = (sun_caustics(sun_world, TIME) * 0.55 + sun_shafts(sun_world, TIME) * 0.3) * sun_lit(sun_world) * sun_face * light_strength;
}

void fragment() {
	vec3 col = COLOR.rgb;
	if (recolor > 0.0) {
		float l = clamp(dot(col, vec3(0.299, 0.587, 0.114)) * 1.5, 0.0, 1.0);
		vec3 painted = tint.r > 0.5 ? mix(low_b, high_b, l) : mix(low_a, high_a, l);
		col = mix(col, painted, recolor);
	}
	col *= tint.g;
	col *= 0.85 + 0.3 * noise(local * 18.0);

	if (lattice > 0.0) {
		// A sea fan's net: veins radiating from the stalk, crossed by finer links.
		vec2 q = local.xy;
		float r = length(q);
		float ang = atan(q.x, q.y);
		float veins = abs(fract(ang * 11.0 + noise(local * 6.0) * 0.6) - 0.5);
		float links = abs(fract(r * 24.0 + sin(ang * 23.0) * 0.25 + noise(local * 9.0) * 0.5) - 0.5);
		if (veins > 0.11 && links > 0.13 && r > 0.08) discard;
	}
	if (meander > 0.0) {
		// Brain coral: meandering valleys between rounded ridges.
		// Two warped wave fields crossed, so the valleys wander and fork like a real brain coral's.
		vec3 q = local * 38.0;
		float warp = noise(local * 6.0) * 6.0;
		float m = sin(q.x * 0.7 + warp + 2.0 * sin(q.z * 0.35 + q.y * 0.2)) * sin(q.z * 0.7 - warp * 0.7 + 2.0 * sin(q.x * 0.3));
		float valley = 1.0 - smoothstep(0.0, 0.25, abs(m));
		float rim = smoothstep(0.25, 0.4, abs(m)) * (1.0 - smoothstep(0.4, 0.6, abs(m)));
		col = mix(col * 1.05, col * vec3(0.45, 0.5, 0.35), valley);
		col += rim * 0.06;
	}
	if (polyps > 0.0) {
		// Tiny corallite cups dotting the branches.
		float dots = step(0.8, noise(local * 70.0));
		col = mix(col, col * 1.35 + 0.05, dots * polyps);
	}

	ALBEDO = col;
	ROUGHNESS = rough;
	SPECULAR = 0.3;
	BACKLIGHT = col * backlight;
	EMISSION = col * glow;
	// Caustics and dappled sunlight play over it, except in the shade of rock.
	EMISSION += ALBEDO * sun_light;
}
