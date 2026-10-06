// The water itself, as a full-screen pass that rides with the camera (drawn before other transparent objects).
// 1. Limited vision: past mist_start the view blurs and fades into the water colour, gone by mist_end (a soft fog of war).
// 2. The water colour depends on where you look: bright and turquoise toward the surface, a glow toward the sun,
//    deep blue looking down; and everything darkens the deeper Clementine swims.
// 3. God rays: the view ray is marched through the water, gathering light wherever it crosses a sunbeam that is not
//    shaded by rock (sunlight.gdshaderinc). Beams glow brightest looking toward the sun, and fade into the mist.
shader_type spatial;
render_mode unshaded, depth_test_disabled, depth_draw_never, cull_disabled, fog_disabled;

#include "res://assets/shaders/sunlight.gdshaderinc"

uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
uniform sampler2D depth_tex : hint_depth_texture, filter_nearest;
uniform float mist_start = 10.0;
uniform float mist_end = 32.0;
uniform float max_blur = 5.0;
uniform vec3 mist_color : source_color = vec3(0.05, 0.16, 0.22);
uniform vec3 deep_color : source_color = vec3(0.02, 0.09, 0.16);
uniform vec3 surface_color : source_color = vec3(0.3, 0.68, 0.74);
uniform vec3 ray_color : source_color = vec3(0.72, 0.95, 1.0);
uniform float ray_strength = 0.9;
uniform float ray_distance = 48.0;
uniform int ray_steps = 14;

void vertex() {
	POSITION = vec4(VERTEX.xy, 1.0, 1.0);
}

float ign(vec2 frag) {
	// Interleaved gradient noise: spreads the ray-march start so no banding shows (it changes every frame, so it reads as soft grain).
	return fract(52.9829189 * fract(dot(frag, vec2(0.06711056, 0.00583715))));
}

void fragment() {
	float depth = texture(depth_tex, SCREEN_UV).r;
	vec4 view = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, depth, 1.0);
	vec3 vpos = view.xyz / view.w;
	float dist = length(vpos);
	vec3 cam = INV_VIEW_MATRIX[3].xyz;
	vec3 dir = normalize((INV_VIEW_MATRIX * vec4(vpos, 0.0)).xyz);
	vec3 to_sun = -normalize(sun_dir);

	// The colour of the water in this direction.
	float cam_depth = max(sea_surface_y - cam.y, 0.0);
	float toward = max(dot(dir, to_sun), 0.0);
	vec3 water = mix(deep_color, mist_color, smoothstep(-0.85, 0.15, dir.y));
	water = mix(water, surface_color, smoothstep(0.25, 1.0, dir.y) * 0.65);
	water += ray_color * pow(toward, 8.0) * 0.3 * light_strength;
	water *= mix(0.6, 1.05, exp(-cam_depth / 55.0));

	float f = smoothstep(mist_start, mist_end, dist);
	vec3 blurred = textureLod(screen_tex, SCREEN_UV, f * max_blur).rgb;
	vec3 col = mix(blurred, water, f * 0.9);

	// God rays.
	float end = min(dist, ray_distance);
	float step_len = end / float(ray_steps);
	float jitter = ign(FRAGCOORD.xy + vec2(TIME * 37.0, TIME * 11.0));
	float light = 0.0;
	for (int i = 0; i < ray_steps; i++) {
		float s = (float(i) + jitter) * step_len;
		vec3 p = cam + dir * s;
		if (p.y > sea_surface_y) break;
		light += sun_shafts(p, TIME) * sun_lit(p) * exp(-s / 30.0);
	}
	light *= step_len / 12.0;
	float phase = 0.35 + 1.6 * pow(toward, 3.0) + 0.25 * max(dir.y, 0.0);
	col += ray_color * light * phase * ray_strength * light_strength;

	ALBEDO = col;
}
