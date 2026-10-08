// Free-standing reef rock in the reef's skin (reef_surface.gdshaderinc): arches, overhangs, cave roofs, boulders and
// weak rock. Lumped along its normal so primitive shapes read as rock, and dissolved with an ordered dither while
// Clementine is underneath (THEME-BIBLE §6.1: canopy dissolves in a dithered cutout). Dithering keeps the rock opaque
// in the depth buffer, so the depth focus blurs it like any other high rock.
shader_type spatial;
render_mode cull_back;

#include "res://assets/shaders/reef_surface.gdshaderinc"

/// 1 solid, 0 gone.
uniform float fade = 1.0;
/// How far the surface is lumped in or out along its normal (metres).
uniform float lumps = 0.0;
/// A colour wash (weak rock reads paler and cracked).
uniform vec3 tint : source_color = vec3(1.0);
uniform float cracks = 0.0;

varying vec3 world;
varying vec3 world_normal;

const float BAYER[16] = float[](0.0, 8.0, 2.0, 10.0, 12.0, 4.0, 14.0, 6.0, 3.0, 11.0, 1.0, 9.0, 15.0, 7.0, 13.0, 5.0);

void vertex() {
	vec3 wp = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	if (lumps > 0.0) VERTEX += NORMAL * (rs_fbm(wp * 0.45) - 0.5) * 2.0 * lumps;
	world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

void fragment() {
	if (fade < 0.999) {
		ivec2 p = ivec2(FRAGCOORD.xy) % 4;
		if ((BAYER[p.y * 4 + p.x] + 0.5) / 16.0 >= fade) discard;
	}
	ReefSkin s = reef_surface(world, normalize(world_normal), TIME, length(fwidth(world.xz)));
	// The caustics ride to light() in BACKLIGHT (this shader does its own lighting, so nothing else reads it).
	BACKLIGHT = vec3(s.caustic * 1.5);
	vec3 c = s.albedo * tint;
	if (cracks > 0.0) {
		// Weak rock: a web of dark fractures (one ink bomb will open it).
		vec3 cell = rs_cells(world.xz * 1.3 + world.y * 0.7);
		c = mix(c, vec3(0.06, 0.05, 0.06), (1.0 - smoothstep(0.02, 0.07, cell.y)) * cracks);
	}
	ALBEDO = c;
	EMISSION = s.emission;
	NORMAL = normalize((VIEW_MATRIX * vec4(s.normal, 0.0)).xyz);
	ROUGHNESS = s.rough;
	SPECULAR = 0.22;
}

void light() {
	float ndl = clamp(dot(NORMAL, LIGHT), 0.0, 1.0);
	// The sun also brings the caustics; other lights (her glow) light the reef plainly.
	float extra = LIGHT_IS_DIRECTIONAL ? BACKLIGHT.x : 0.0;
	DIFFUSE_LIGHT += (ndl + extra) * ATTENUATION * LIGHT_COLOR / PI;
}
