// The level's ground: the reef's skin (reef_surface.gdshaderinc) on the heightfield. Walls between the camera and
// Clementine are not cut away: she is drawn on top of them, and the depth focus pass blurs high rock into soft
// silhouettes.
shader_type spatial;
render_mode cull_back;

#include "res://assets/shaders/reef_surface.gdshaderinc"

uniform bool debug_normals = false;

varying vec3 world;
varying vec3 world_normal;

void vertex() {
	world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}


// An art direction under study (reef_art.gdshaderinc): the reef painted another way.
void art(inout ReefSkin s, vec3 local, float hw, float px) {
	if (reef_art <= 0) return;
	vec3 w = lf_pattern(local);
	s.albedo = art_paint(s.albedo, w, local, s.normal, reef_water_deep, reef_rock, reef_sand);
	s.albedo = mix(s.albedo, s.albedo * 0.4 + vec3(0.85, 0.82, 0.74) * 0.6, art_rim(local, s.normal, hw));
	vec2 ink = art_ink(w, local, s.normal, hw, px);
	s.albedo = mix(s.albedo, art_beyond(reef_water_deep), ink.y);
	s.albedo = mix(s.albedo, art_ink_colour(reef_water_deep), ink.x);
	s.caustic = art_caustic(s.caustic);
}

void fragment() {
	if (lf_portal_cut(world, FRAGCOORD.xy) || lf_see_cut(world, CAMERA_POSITION_WORLD, FRAGCOORD.xy)) discard;
	vec3 local = lf_local(world);
	float px = length(fwidth(world.xz));
	ReefSkin s = reef_surface(local, normalize(world_normal), TIME, px);
	art(s, local, fwidth(local.y), px);
	// The caustics ride to light() in BACKLIGHT (this shader does its own lighting, so nothing else reads it).
	BACKLIGHT = vec3(s.caustic * 1.5);
	ALBEDO = s.albedo;
	EMISSION = s.emission;
	NORMAL = normalize((VIEW_MATRIX * vec4(s.normal, 0.0)).xyz);
	ROUGHNESS = s.rough;
	// Matte: next to no reflection of the water's light.
	SPECULAR = 0.06;
	if (debug_normals) { ALBEDO = vec3(0.0); EMISSION = (s.normal * 0.5 + 0.5) * 0.6; }
}

void light() {
	float ndl = art_light(clamp(dot(NORMAL, LIGHT), 0.0, 1.0));
	// The sun also brings the caustics; other lights (her glow) light the reef plainly.
	float extra = LIGHT_IS_DIRECTIONAL ? BACKLIGHT.x : 0.0;
	DIFFUSE_LIGHT += (ndl + extra) * ATTENUATION * LIGHT_COLOR / PI;
	// Her light (and other near lights) leaves only a faint dull sheen on the matte rock and sand.
	if (!LIGHT_IS_DIRECTIONAL) {
		vec3 hv = normalize(LIGHT + VIEW);
		float spec = pow(clamp(dot(NORMAL, hv), 0.0, 1.0), 10.0) * (1.0 - ROUGHNESS) * ndl;
		SPECULAR_LIGHT += spec * ATTENUATION * LIGHT_COLOR * 0.4;
	}
}
