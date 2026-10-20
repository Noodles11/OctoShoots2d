// The sand over a boss level's Crack (DESIGN-TOPDOWN §4.6): while the Crack is sealed it lies flush with the arena
// floor, painted by the reef's own skin (reef_surface.gdshaderinc) so it reads as more of the same ground. When the
// Crack opens, `sink` runs 0 → 1: the middle sags first, the sand turns slowly round as it drains, and the whole cover
// slides down into the dark, its rim last.
//   UV.x: how far out from the middle (0 → 1 at the rim); UV.y: unused.
shader_type spatial;
render_mode cull_back;

#include "res://assets/shaders/reef_surface.gdshaderinc"

uniform float sink = 0.0;
uniform vec2 cover_center = vec2(0.0);
// How far the sand falls (metres) once it has drained.
uniform float drop = 9.0;

varying vec3 world;
varying vec3 world_normal;
varying float gone;

void vertex() {
	float r = UV.x;
	// The middle goes first, the rim last.
	float k = smoothstep(0.0, 1.0, clamp((sink - 0.4 * r) / 0.6, 0.0, 1.0));
	// A sag before it falls: a funnel deepening toward the middle.
	float sag = sink * 1.4 * (1.0 - r) * (1.0 - r) * (1.0 - k);
	// It turns as it drains (more in the middle).
	float turn = sink * 0.9 * (1.0 - r);
	vec2 d = VERTEX.xz - cover_center;
	float c = cos(turn), s = sin(turn);
	VERTEX.xz = cover_center + vec2(c * d.x - s * d.y, s * d.x + c * d.y);
	VERTEX.y -= sag + k * drop;
	gone = k;
	world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

void fragment() {
	if (gone > 0.97 || lf_portal_cut(world, FRAGCOORD.xy)) discard;
	ReefSkin s = reef_surface(lf_local(world), normalize(world_normal), TIME, length(fwidth(world.xz)));
	BACKLIGHT = vec3(s.caustic * 1.5);
	ALBEDO = s.albedo;
	EMISSION = s.emission;
	NORMAL = normalize((VIEW_MATRIX * vec4(s.normal, 0.0)).xyz);
	ROUGHNESS = s.rough;
	// Matte: next to no reflection of the water's light.
	SPECULAR = 0.06;
}

void light() {
	float ndl = clamp(dot(NORMAL, LIGHT), 0.0, 1.0);
	float extra = LIGHT_IS_DIRECTIONAL ? BACKLIGHT.x : 0.0;
	DIFFUSE_LIGHT += (ndl + extra) * ATTENUATION * LIGHT_COLOR / PI;
}
