// Free-standing reef rock in the reef's skin (reef_surface.gdshaderinc): arches, overhangs, cave roofs, boulders and
// the rubble of a secret's seal. Lumped along its normal so primitive shapes read as rock, and dissolved with an ordered dither while
// Clementine is underneath (THEME-BIBLE §6.1: canopy dissolves in a dithered cutout). Dithering keeps the rock opaque
// in the depth buffer, so the depth focus blurs it like any other high rock.
shader_type spatial;
render_mode cull_back;

#include "res://assets/shaders/reef_surface.gdshaderinc"

/// 1 solid, 0 gone.
uniform float fade = 1.0;
/// How far the surface is lumped in or out along its normal (metres).
uniform float lumps = 0.0;
/// A colour wash.
uniform vec3 tint : source_color = vec3(1.0);

varying vec3 world;
varying vec3 world_normal;

void vertex() {
	vec3 wp = lf_pattern(lf_local((MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz));
	if (lumps > 0.0) VERTEX += NORMAL * (rs_fbm(wp * 0.45) - 0.5) * 2.0 * lumps;
	world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

void fragment() {
	if (fade < 0.999 && lf_dither(FRAGCOORD.xy) >= fade) discard;
	if (lf_portal_cut(world, FRAGCOORD.xy)) discard;
	vec3 local = lf_local(world);
	ReefSkin s = reef_surface(local, normalize(world_normal), TIME, length(fwidth(world.xz)));
	// The caustics ride to light() in BACKLIGHT (this shader does its own lighting, so nothing else reads it).
	BACKLIGHT = vec3(s.caustic * 1.5);
	vec3 c = s.albedo * tint;
	ALBEDO = c;
	EMISSION = s.emission;
	NORMAL = normalize((VIEW_MATRIX * vec4(s.normal, 0.0)).xyz);
	ROUGHNESS = s.rough;
	// Matte: next to no reflection of the water's light.
	SPECULAR = 0.06;
}

void light() {
	float ndl = clamp(dot(NORMAL, LIGHT), 0.0, 1.0);
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
