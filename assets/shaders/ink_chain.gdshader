// The corruption's chains (docs/CORRUPTION.md): links of glossy ink pinning a Blightroot's octopus to the floor, and the
// two that tear loose to lash at her (the gloomvines). Ink-black with the Blightroot's blue rim; per link (MultiMesh
// colour): r how far it has dissolved (it falls away in specks), g how taut it is (the rim brightens on the pulse and
// while coiling her).
shader_type spatial;
render_mode cull_disabled;

uniform vec3 rim_color : source_color = vec3(0.2, 0.48, 1.0);

varying float v_gone;
varying float v_taut;
varying vec3 v_local;

void vertex() {
	v_gone = COLOR.r;
	v_taut = COLOR.g;
	v_local = VERTEX;
}

float hash3(vec3 p) { return fract(sin(dot(p, vec3(12.9898, 78.233, 37.719))) * 43758.5453); }

void fragment() {
	if (v_gone > 0.0 && hash3(floor(v_local * 40.0)) < v_gone) discard;
	float facing = clamp(dot(NORMAL, VIEW), 0.0, 1.0);
	float fres = pow(1.0 - facing, 2.0);
	ALBEDO = vec3(0.012, 0.01, 0.028);
	ROUGHNESS = 0.12;
	SPECULAR = 0.85;
	EMISSION = rim_color * (fres * (0.7 + 1.4 * v_taut) + 0.05 + 0.25 * v_taut);
}
