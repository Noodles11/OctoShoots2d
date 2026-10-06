// The top-down look (after Below): everything is judged by its height against the swim level (0), where Clementine
// floats. That layer stays sharp. Below it the world blurs, fogs and darkens with depth until the deeps are a soft
// blue-green murk — the layer below reads as a soft floor, with only shapes of what lies beneath; above it rock
// blurs into dark foreground silhouettes. Then a heavy vignette and a cool grade.
// A full-screen quad in the transparent pass, drawn first, so Clementine and the other glowing things land on top sharp.
shader_type spatial;
render_mode unshaded, depth_test_disabled, depth_draw_never, cull_disabled, fog_disabled, shadows_disabled;

uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
uniform sampler2D depth_tex : hint_depth_texture, filter_nearest;
uniform float swim_level = 0.0;
/// Heights within this band of the swim level stay in focus (the bell floats ~0.7 m above it).
uniform float focus_above = 1.6;
uniform float focus_below = 0.6;
uniform float below_blur = 5.2;
uniform float above_blur = 3.4;
uniform vec3 deep_color : source_color = vec3(0.08, 0.22, 0.25);
uniform vec3 shade_color : source_color = vec3(0.05, 0.1, 0.11);
uniform float deep_fog = 0.06;
uniform float vignette = 0.66;
/// Clementine's glow scattering in the water around her: a warm halo on the swim layer, the scene's lantern.
uniform vec3 player_pos = vec3(0.0);
uniform vec3 halo_color : source_color = vec3(1.0, 0.68, 0.45);
uniform float halo_radius = 8.0;
uniform float halo_strength = 0.16;

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

	// Below the layer the blur sets in at once and deepens; a veil of murk covers it all, so the layer reads as a floor
	// with only shapes beneath it.
	float lod = below_blur * (below > 0.0 ? 0.55 + 0.45 * smoothstep(0.0, 10.0, below) : 0.0) * smoothstep(0.0, 0.8, below)
		+ above_blur * smoothstep(0.0, 9.0, above);
	vec3 col = blurred(uv, lod);

	// The deeper, the more fog and the darker, over a veil that starts just under the layer.
	float fog = smoothstep(0.0, 1.5, below) * (0.42 + 0.48 * (1.0 - exp(-below * deep_fog)));
	col = mix(col, deep_color, fog);
	// Her glow scatters in the water between her layer and the floor (not on high rock, which stands in front of it).
	// Measured where this pixel's view ray crosses her layer, so the halo surrounds her on screen.
	vec3 cam = CAMERA_POSITION_WORLD;
	vec3 ray = normalize(world - cam);
	vec3 on_layer = cam + ray * ((player_pos.y - cam.y) / min(ray.y, -1e-3));
	vec2 off = on_layer.xz - player_pos.xz;
	float halo = exp(-dot(off, off) / (halo_radius * halo_radius)) * (0.35 + 0.65 * fog) * (1.0 - smoothstep(0.0, 4.0, above));
	col += halo_color * halo * halo_strength;
	// Rock rising toward the camera goes dark, a foreground silhouette.
	col = mix(col, shade_color, smoothstep(0.0, 10.0, above) * 0.38);

	// Grade: a little desaturated, cool in the shadows.
	float luma = dot(col, vec3(0.299, 0.587, 0.114));
	col = mix(vec3(luma), col, 0.82);
	col *= mix(vec3(0.8, 0.98, 1.08), vec3(1.0), smoothstep(0.0, 0.35, luma));

	// Heavy vignette, oval to the screen.
	vec2 d = (uv - 0.5) * vec2(VIEWPORT_SIZE.x / VIEWPORT_SIZE.y, 1.0);
	float v = smoothstep(0.3, 1.0, length(d) * 1.3);
	col *= 1.0 - vignette * v;

	ALBEDO = max(col, vec3(0.0));
}
