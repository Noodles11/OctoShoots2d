// Kelp (KelpView, DESIGN-TOPDOWN §4.1 stage 5): long thick blades, one ribbon per strand, its points moved by the
// view's physics each frame. Vertex colour: r a per-strand seed, g its brightness, a how far along it (0 root → 1 tip).
// UV: x across the blade (0 → 1), y along it in metres. Darker and browner at the stipe, paler toward the tip, a pale
// midrib, ruffled margins and soft bands; the light comes through it (a leaf in the water), caustics play on it.
// OVER (kelp_over.gdshader): the same blades drawn again after Clementine, only where they pass over her
// (over_her.gdshaderinc), so a blade between her and the camera covers her as it should.

#include "res://assets/shaders/sunlight.gdshaderinc"
#include "res://assets/shaders/level_frame.gdshaderinc"

uniform vec3 base_col : source_color = vec3(0.2, 0.3, 0.15);
uniform vec3 tip_col : source_color = vec3(0.55, 0.7, 0.3);
#ifdef OVER
#include "res://assets/shaders/over_her.gdshaderinc"
#endif

varying vec3 world_pos;
varying float sun_light;

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	// The blades are turned to the camera (KelpView): their faces look at it, a little rounded across.
	NORMAL = normalize((inverse(MODEL_MATRIX) * vec4(INV_VIEW_MATRIX[2].xyz, 0.0)).xyz);
	vec3 sun_world = lf_pattern(lf_local(world_pos));
	sun_world.y = world_pos.y;
	vec3 n = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
	float face = 0.45 + 0.55 * abs(dot(n, -normalize(sun_dir)));
	sun_light = (sun_caustics(sun_world, TIME) * 0.5 + sun_shafts(sun_world, TIME) * 0.3) * sun_lit(sun_world) * face * light_strength;
}

void fragment() {
	if (lf_portal_cut(world_pos, FRAGCOORD.xy)) discard;
	float u = COLOR.a;
	float across = UV.x * 2.0 - 1.0;
	// Ruffled margins: the blade's edge waves in and out along it.
	float margin = 0.84 + 0.1 * sin(UV.y * 9.0 + COLOR.r * 40.0) + 0.05 * sin(UV.y * 23.0 + COLOR.r * 17.0);
	if (abs(across) > margin) discard;
	vec3 col = mix(base_col, tip_col, smoothstep(0.0, 0.9, u));
	col *= 0.78 + 0.32 * COLOR.g;
	// The midrib, paler; soft bands across the blade; the edge a shade darker.
	col = mix(col, col * 1.3 + 0.02, (1.0 - smoothstep(0.0, 0.1, abs(across))) * 0.55);
	col *= 0.9 + 0.12 * sin(UV.y * 5.5 + COLOR.r * 9.0 + across * 2.0);
	col *= 1.0 - 0.25 * smoothstep(0.6, 1.0, abs(across) / margin);
	ALBEDO = col;
	ROUGHNESS = 0.8;
	SPECULAR = 0.12;
	BACKLIGHT = col * 0.3;
	EMISSION = col * (sun_light + 0.03);
#ifdef OVER
	// Only over her (over_her.gdshaderinc).
	ALPHA = over_her(world_pos, VERTEX.z, VIEW_MATRIX);
	if (ALPHA < 0.01) discard;
	ALBEDO = over_haze(ALBEDO, 0.12);
	EMISSION = mix(EMISSION * vec3(0.72, 0.92, 1.03), vec3(0.0), 0.12);
#endif
}
