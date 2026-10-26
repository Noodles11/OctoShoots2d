// An ink dot in the water (AdventureView): a matte drop of ink at her swim plane, brushed in dark bands, its rim laid as
// a dry-brushed violet stroke (as a murkling's), bobbing faintly on its own beat. Drawn in one MultiMesh.
shader_type spatial;
render_mode cull_back, shadows_disabled;

varying vec3 v_world;
varying vec3 v_local;

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

void vertex() {
	vec3 origin = MODEL_MATRIX[3].xyz;
	float seed = hash2(origin.xz * 0.731);
	// Afloat: a slow bob up and down (in metres, whatever the dot's size), each on its own beat, and a soft wobble out of round.
	float size = length(MODEL_MATRIX[0].xyz);
	VERTEX *= 1.0 + 0.06 * sin(TIME * 3.1 + seed * 9.0 + dot(normalize(VERTEX), vec3(3.0, 2.0, 4.0)));
	v_local = normalize(VERTEX);
	VERTEX.y += (sin(TIME * 1.4 + seed * 6.28) * 0.06 + sin(TIME * 2.3 + seed * 11.0) * 0.025) / max(size, 1e-3);
	v_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	float facing = clamp(dot(NORMAL, VIEW), 0.0, 1.0);
	vec3 n = (INV_VIEW_MATRIX * vec4(NORMAL, 0.0)).xyz;
	float around = atan(n.z, n.x);
	float band = noise2(vec2(atan(v_local.z, v_local.x) * 2.0 + v_world.x, v_local.y * 5.0 + TIME * 0.2));
	ALBEDO = vec3(0.014, 0.01, 0.024) + vec3(0.03, 0.012, 0.045) * band;
	ROUGHNESS = 0.95;
	SPECULAR = 0.05;
	float edge = 0.45 + 0.15 * noise2(vec2(around * 5.0 + v_world.z, TIME * 0.4));
	float stroke = smoothstep(edge, edge + 0.15, 1.0 - facing);
	float dry = 0.6 + 0.5 * noise2(vec2(around * 14.0 + v_world.x * 3.0, (1.0 - facing) * 5.0));
	EMISSION = vec3(0.36, 0.1, 0.62) * stroke * dry * 0.9;
}
