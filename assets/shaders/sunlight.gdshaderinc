// Sunlight under water, shared by everything that catches it (rock, sand, flora, creatures, drifting specks, the god rays).
// The sea surface focuses sunlight into moving bright filaments. Every pattern here lives on a plane facing the sun and is
// carried down along the sun's direction, so a shaft of light in the water always ends in the bright spot it makes below.
//
// Globals (project.godot [shader_globals], set by the game at level load):
//   sun_dir         direction the light travels (pointing down into the water)
//   sea_surface_y   height of the sea surface
//   reef_extent     size of the reef in x and z (the shadow map covers it)
//   sun_shadow      per surface point: the height where its light ray first hits rock (R channel)
//   sun_has_shadow  0 when there is no shadow map (the grey-box cave)
//   light_strength  overall strength of shafts and caustics (an option)

global uniform vec3 sun_dir;
global uniform float sea_surface_y;
global uniform vec2 reef_extent;
global uniform float sun_has_shadow;
global uniform sampler2D sun_shadow : filter_linear, repeat_disable;
global uniform float light_strength;

float sl_hash(vec2 p) {
	// Stable at large coordinates (pixel positions in the thousands).
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float sl_noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(sl_hash(i), sl_hash(i + vec2(1.0, 0.0)), f.x), mix(sl_hash(i + vec2(0.0, 1.0)), sl_hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

// Where along the surface this point's sunlight came in, in the sun's own plane (metres).
vec2 sun_plane(vec3 p) {
	vec3 l = normalize(sun_dir);
	float back = (sea_surface_y - p.y) / max(-l.y, 0.2);
	vec3 entry = p - l * back;
	return entry.xz;
}

// How far below the surface the light has travelled, in metres along its ray.
float sun_path(vec3 p) {
	return max(sea_surface_y - p.y, 0.0) / max(-normalize(sun_dir).y, 0.2);
}

// 1 where the sun reaches this point, 0 in the shadow of rock above it (softened over about a metre).
float sun_lit(vec3 p) {
	if (sun_has_shadow < 0.5) return 1.0;
	vec2 uv = sun_plane(p) / reef_extent;
	if (uv.x < 0.0 || uv.y < 0.0 || uv.x > 1.0 || uv.y > 1.0) return 1.0;
	// The surface a ray first meets is lit (the ray ends on it); anything more than about a metre below is in shade.
	// The bias also covers the map's 1 m cells on slopes.
	float hit = texture(sun_shadow, uv).r;
	return smoothstep(hit - 1.8, hit - 0.8, p.y);
}

// Tileable water caustics: bright, wobbling filaments.
float sl_caustic_layer(vec2 uv, float time) {
	vec2 p = mod(uv * 6.2831853, 6.2831853) - 250.0;
	vec2 i = p;
	float c = 1.0;
	float inten = 0.005;
	for (int n = 0; n < 4; n++) {
		float t = time * (1.0 - (3.5 / float(n + 1)));
		i = p + vec2(cos(t - i.x) + sin(t + i.y), sin(t - i.y) + cos(t + i.x));
		c += 1.0 / length(vec2(p.x / (sin(i.x + t) / inten), p.y / (cos(i.y + t) / inten)));
	}
	c /= 4.0;
	c = 1.17 - pow(c, 1.4);
	return pow(abs(c), 8.0);
}

// The caustic light falling on a surface at p (0 to about 2). Sharper near the surface, softer and fainter deeper down.
float sun_caustics(vec3 p, float time) {
	vec2 q = sun_plane(p);
	// Real-world metres: the reef's 60 m water column stands for about 27 m (as for the flora's zonation).
	float depth = sun_path(p) * 0.45;
	float blur = clamp(depth / 60.0, 0.0, 1.0);
	// Two layers at different scales: broad bright webs (they survive the painterly pass) and finer threads.
	float a = sl_caustic_layer(q * 0.085 + vec2(sl_noise(q * 0.04)) * 0.25, time * 0.38);
	float b = sl_caustic_layer(q * 0.15 + 3.7, time * 0.31 + 1.3);
	float c = mix(a * 1.2 + b * 0.5, (a + b) * 0.45 + 0.3, blur);
	// Big slow swells of the surface make whole patches brighten and dim.
	float swell = 0.65 + 0.55 * sl_noise(q * 0.025 + vec2(time * 0.05, time * 0.03));
	return c * swell * exp(-depth / 45.0);
}

// The density of light shafts in the water at p: soft beams that sway with the surface and fade with depth.
float sun_shafts(vec3 p, float time) {
	vec2 q = sun_plane(p);
	float depth = sun_path(p);
	// Beams: elongated bright cells in the sun plane, drifting with the swell.
	// Beams of every width: broad soft shafts, narrower bright ones inside them, a few thin needles.
	vec2 drift = vec2(time * 0.22, time * 0.14);
	float broad = smoothstep(0.45, 0.85, sl_noise(q * 0.07 + drift * 0.4));
	float mid = smoothstep(0.55, 0.9, sl_noise(q * 0.19 + drift) * 0.65 + sl_noise(q * 0.41 - drift * 1.3) * 0.35);
	float needle = smoothstep(0.82, 0.97, sl_noise(q * 0.9 + drift * 2.0));
	float beams = broad * 0.45 + mid * (0.35 + 0.65 * broad) + needle * 0.35 * broad;
	// The whole field breathes slowly as the swell passes overhead.
	beams *= 0.75 + 0.35 * sin(time * 0.4 + q.x * 0.03 + q.y * 0.02);
	return beams * exp(-depth / 50.0);
}
