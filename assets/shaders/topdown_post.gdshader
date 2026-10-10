// The water (THEME-BIBLE Â§6.1 depth focus, Â§6.2 light, Â§6.9 post stack), drawn over the reef as one full-screen pass.
// Everything is judged by its height against the swim level (0), where Clementine floats; that layer stays sharp.
//   Below it the world blurs and sinks into the depth's water colour, turquoise in the Shallows, murk further down.
//   Above it rock blurs into soft foreground silhouettes, tinted by the water rather than blackened.
//   God rays rake across the view: shafts of sunlight marched along each view ray through the water column, parallel
//   to the sun, ending in the caustics they make below. Never into the lens: the camera looks down.
//   A faint refraction wobble, her warm halo, a grade (saturation, cool shadows, sunlit highlights) and a vignette
//   tinted by the water, all deepening with Menace.
// A full-screen quad in the transparent pass, drawn first, so Clementine and the other glowing things land on top sharp.
shader_type spatial;
render_mode unshaded, depth_test_disabled, depth_draw_never, cull_disabled, fog_disabled, shadows_disabled;

#include "res://assets/shaders/sunlight.gdshaderinc"

global uniform vec3 reef_water_shallow;
global uniform vec3 reef_water_deep;
global uniform vec3 reef_murk;
global uniform vec3 reef_sun;
global uniform float reef_rays;
global uniform float reef_menace;
global uniform float reef_saturation;
global uniform float reef_vignette;
global uniform float reef_fog;
global uniform vec4 reef_crack;
/// Clementine's bell in world space and her glow pool's radius; how strong her glow on the floor is (BellView).
global uniform vec4 her_glow;
global uniform float her_glow_strength;

uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
uniform sampler2D depth_tex : hint_depth_texture, filter_nearest;
uniform float swim_level = 0.0;
/// Heights within this band of the swim level stay in focus (the bell floats ~0.7 m above it).
uniform float focus_above = 1.6;
uniform float focus_below = 0.6;
uniform float below_blur = 5.2;
uniform float above_blur = 3.4;
uniform float deep_fog = 0.06;
/// Clementine's glow scattering in the water around her: a warm halo on the swim layer, the scene's lantern.
uniform vec3 player_pos = vec3(0.0);
uniform vec3 halo_color : source_color = vec3(1.0, 0.77, 0.42);
uniform float halo_radius = 8.0;
uniform float halo_strength = 0.16;
/// Light bubbles (docs/LIGHT-BUBBLES.md): up to 16 small halos on the swim layer, from her bubbles and the blooms they
/// leave where they pop: x, z (world), radius (metres), strength (a share of her halo's, ≤ 0.4).
uniform vec4 bubble_lights[16];
uniform int bubble_count = 0;
uniform vec3 bubble_color : source_color = vec3(1.0, 0.769, 0.420);
/// A big-merge kill: a white lift at the screen's edges only, 1 → 0 over 60 ms.
uniform float edge_flash = 0.0;
/// The faint refraction wobble (0 with reduced motion).
/// The absolute position (xz) of the world's origin: the god rays never jump after a dive.
uniform vec2 world_origin = vec2(0.0);
uniform float wobble = 1.0;

void vertex() {
	POSITION = vec4(VERTEX.xy, 1.0, 1.0);
}

// A soft blur: rings of taps spread wide over a moderately coarse mip (fine mips smoothed by distance, never the
// coarsest ones, whose big texels show as blocky steps). Fixed tap pattern, so nothing crawls over the screen.
vec3 blurred(vec2 uv, float amount) {
	if (amount < 0.05) return textureLod(screen_tex, uv, 0.0).rgb;
	vec2 px = 1.0 / vec2(textureSize(screen_tex, 0));
	float lod = min(amount, 3.0);
	float r = exp2(amount) * 0.7;
	vec3 sum = textureLod(screen_tex, uv, lod).rgb;
	float w = 1.0;
	for (int ring = 1; ring <= 3; ring++) {
		float rr = r * float(ring) / 3.0;
		float k = 1.0 - float(ring) * 0.22;
		for (int i = 0; i < 8; i++) {
			float a = float(i) * 0.785398 + float(ring) * 0.39;
			sum += textureLod(screen_tex, uv + vec2(cos(a), sin(a)) * rr * px, lod).rgb * k;
			w += k;
		}
	}
	return sum / w;
}

float hash12(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

void fragment() {
	vec2 uv = SCREEN_UV;
	float depth = texture(depth_tex, uv).r;
	vec4 view = INV_PROJECTION_MATRIX * vec4(uv * 2.0 - 1.0, depth, 1.0);
	view.xyz /= view.w;
	vec3 world = (INV_VIEW_MATRIX * vec4(view.xyz, 1.0)).xyz;
	float h = world.y - swim_level;
	// Nothing drawn (reverse-Z far plane): the open deep.
	bool open_deep = depth <= 1e-6;
	float below = open_deep ? 40.0 : max(-h - focus_below, 0.0);
	float above = open_deep ? 0.0 : max(h - focus_above, 0.0);
	// Soften the blur's edge where high rock meets lower ground: take the highest of a few nearby depths, so the
	// boundary is a smooth margin instead of the mesh's stair-stepped silhouette.
	{
		vec2 px = 3.0 / VIEWPORT_SIZE;
		for (int k = 0; k < 4; k++) {
			vec2 o = vec2(k == 0 ? px.x : k == 1 ? -px.x : 0.0, k == 2 ? px.y : k == 3 ? -px.y : 0.0);
			float dk = texture(depth_tex, uv + o).r;
			if (dk <= 1e-6) continue;
			vec4 vk = INV_PROJECTION_MATRIX * vec4((uv + o) * 2.0 - 1.0, dk, 1.0);
			vk.xyz /= vk.w;
			float hk = (INV_VIEW_MATRIX * vec4(vk.xyz, 1.0)).y - swim_level;
			above = max(above, max(hk - focus_above, 0.0) * 0.85);
		}
	}

	// Refraction: the picture swims very slightly, more for what lies deeper.
	vec2 wob = vec2(sin(uv.y * 38.0 + TIME * 1.3 + uv.x * 9.0), cos(uv.x * 31.0 + TIME * 1.1 - uv.y * 7.0));
	vec2 suv = uv + wob * wobble * (0.0006 + 0.0012 * smoothstep(0.0, 6.0, below));

	float lod = below_blur * (below > 0.0 ? 0.55 + 0.45 * smoothstep(0.0, 10.0, below) : 0.0) * smoothstep(0.0, 0.8, below)
		+ above_blur * smoothstep(0.0, 9.0, above);
	vec3 col = blurred(suv, lod);

	// Everything is seen through water: reds fade and a turquoise in-scatter rises with the water between the sea
	// surface and what this pixel shows (rock tops near the surface keep more of their colour).
	float column_in = clamp((sea_surface_y - (open_deep ? -40.0 : world.y)) / 20.0, 0.0, 1.5);
	col *= mix(vec3(1.0), vec3(0.64, 0.9, 1.05), column_in);
	col = mix(col, reef_water_deep * 0.7, 0.13 * column_in);

	// The deeps sink into the water: turquoise just under the layer, the depth's murk far below. Clear water in the
	// Shallows (a light veil), thick murk deeper down (reef_fog).
	vec3 water = mix(reef_water_deep, reef_murk, smoothstep(2.0, 16.0, below));
	float veil = mix(0.18, 0.5, reef_fog);
	float fog = smoothstep(0.0, 1.8, below) * (veil + (0.85 - veil) * (1.0 - exp(-below * deep_fog * (0.6 + reef_fog))));
	col = mix(col, water, fog);

	// Her glow lands on the floor (DESIGN-TOPDOWN §5): in daylight bioluminescence reads not as brightness but as a
	// warm tint on what is near, and as shadow contrast. A broad warm pool (her_glow.w metres, showing on pale sand
	// too) lets the reef advertise her; a small soft dark blob straight under the bell (about 1.2× its radius) gives her
	// bright body the dark anchor it needs to pop. Laid on after the water's veil so neither is washed out; only on what
	// lies below her (never rock over her head), fading as the gap below her grows. She herself is drawn after this
	// pass, so the shadow never darkens her.
	if (her_glow_strength > 0.0 && !open_deep) {
		float gap = her_glow.y - world.y;
		float d = distance(world.xz, her_glow.xz);
		float k = her_glow_strength * smoothstep(0.1, 0.5, gap) * (1.0 - smoothstep(5.0, 10.0, gap));
		float pool = exp(-pow(d / (her_glow.w * 0.55), 2.0)) * k;
		vec3 warm = vec3(1.0, 0.46, 0.18);
		col = mix(col, col * warm * 1.15 + warm * 0.06, pool * 0.65);
		float shadow = (1.0 - smoothstep(0.1, 0.62, d)) * k * (1.0 - smoothstep(2.0, 5.0, gap));
		col *= 1.0 - 0.6 * shadow;
	}

	vec3 cam = CAMERA_POSITION_WORLD;
	vec3 ray = normalize(world - cam);

	// The Crack shines up through the murk: an orange glow breathing out of the fissure, a doorway in the floor.
	if (reef_crack.w > 0.0 && !open_deep) {
		float dc = distance(world.xz, reef_crack.xy);
		float inside = (1.0 - smoothstep(reef_crack.z * 0.4, reef_crack.z, dc)) * (1.0 - smoothstep(-4.5, -1.8, h));
		float breathe = 0.7 + 0.3 * sin(TIME * 1.1 + dc * 0.5);
		vec3 ember = mix(vec3(1.0, 0.42, 0.1), vec3(1.0, 0.2, 0.03), 1.0 - smoothstep(-6.5, -3.0, h));
		col = mix(col, col * 0.35 + ember * 0.9, min(inside * breathe * reef_crack.w * 1.1, 1.0));
	}

	// God rays: march the view ray through the water column, from the sea surface down to what it hits, summing the
	// shaft density (parallel to the sun, drifting with the swell). Longer water paths gather more light.
	vec3 hit = open_deep ? cam + ray * 90.0 : world;
	float t0 = ray.y < -1e-3 ? max((sea_surface_y - cam.y) / ray.y, 0.0) : 0.0;
	float t1 = max(dot(hit - cam, ray), t0);
	float span = t1 - t0;
	float shafts = 0.0;
	if (reef_rays > 0.0 && span > 0.1) {
		float jitter = hash12(FRAGCOORD.xy + fract(TIME) * 61.0);
		for (int i = 0; i < 10; i++) {
			vec3 q = cam + ray * (t0 + span * (float(i) + jitter) / 10.0);
			shafts += sun_shafts(q + vec3(world_origin.x, 0.0, world_origin.y), TIME);
		}
		shafts /= 10.0;
	}
	float column = smoothstep(0.0, 26.0, span);
	col += reef_sun * shafts * column * reef_rays * light_strength * 0.14 * (1.0 - 0.5 * smoothstep(0.0, 8.0, above));

	// Her glow scatters in the water between her layer and the floor (not on high rock, which stands in front of it).
	// Measured where this pixel's view ray crosses her layer, so the halo surrounds her on screen.
	vec3 on_layer = cam + ray * ((player_pos.y - cam.y) / min(ray.y, -1e-3));
	vec2 off = on_layer.xz - player_pos.xz;
	float halo = exp(-dot(off, off) / (halo_radius * halo_radius)) * (0.35 + 0.65 * fog) * (1.0 - smoothstep(0.0, 4.0, above));
	col += halo_color * halo * halo_strength * (0.6 + 0.8 * reef_menace);
	// Her bubbles carry light: each a small halo of its own on the same layer, the same depth-darkness scaling as hers
	// (subtle in the bright Shallows, glorious in the deep).
	float bubbles = 0.0;
	for (int i = 0; i < bubble_count; i++) {
		vec4 b = bubble_lights[i];
		vec2 ob = on_layer.xz - b.xy;
		bubbles += exp(-dot(ob, ob) / (b.z * b.z)) * b.w;
	}
	col += bubble_color * bubbles * halo_strength * (0.35 + 0.65 * fog) * (1.0 - smoothstep(0.0, 4.0, above)) * (0.6 + 0.8 * reef_menace);

	// Rock rising toward the camera: a soft silhouette in the water's colour, darker as the depths darken.
	vec3 shade = mix(reef_murk * 0.45, vec3(0.01, 0.015, 0.02), reef_menace);
	col = mix(col, shade, smoothstep(0.0, 9.0, above) * mix(0.42, 0.6, reef_menace));

	// Grade: the depth's saturation; shadows cooled toward the water, highlights a touch of sun.
	float luma = dot(col, vec3(0.299, 0.587, 0.114));
	col = mix(vec3(luma), col, reef_saturation);
	// Shadows take the water's hue at their own brightness (cool, not lifted).
	float wl = max(dot(reef_water_deep, vec3(0.299, 0.587, 0.114)), 0.01);
	col = mix(col, reef_water_deep / wl * luma, (1.0 - smoothstep(0.0, 0.3, luma)) * 0.45);
	col = mix(col, col * (reef_sun * 0.2 + 0.82), smoothstep(0.5, 1.0, luma) * 0.3);

	// Vignette, oval to the screen, tinted by the deep water; heavier as the depths darken.
	vec2 d = (uv - 0.5) * vec2(VIEWPORT_SIZE.x / VIEWPORT_SIZE.y, 1.0);
	float v = smoothstep(0.32, 1.05, length(d) * 1.3);
	col = mix(col, reef_murk * 0.25, v * reef_vignette);
	// A big-merge kill lifts the screen's edges toward white for an instant (never its middle).
	col = mix(col, vec3(1.0, 0.97, 0.9), edge_flash * 0.55 * smoothstep(0.45, 1.15, length(d) * 1.3));

	ALBEDO = max(col, vec3(0.0));
}
