// Her light painted (LightTrails: a shot's afterlight, a strike's bolt; SurgeView: the lightning round Firefly Squid's
// surge): a stroke laid in passes like paint. The wash under it is light_wash.gdshader (a glaze, mixed over the scene);
// this draws the light added on top: a body with a ragged, wandering edge and dry-brush bristle streaks, then a broken
// white-hot core line. (Pass 0 here is a soft additive wash, kept for strokes that want one.) Vertex COLOR: rgb the
// light's colour, a its strength (fading with age). UV: x across the stroke (0 to 1), y along it in metres. UV2: x the
// pass (0 wash, 1 body, 2 core), y a seed for its brush.
shader_type spatial;
render_mode blend_add, unshaded, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;

varying vec4 v_col;
varying vec2 v_pass;

void vertex() {
	v_col = COLOR;
	v_pass = UV2;
}

float hash(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

void fragment() {
	float x = UV.x * 2.0 - 1.0;
	float s = UV.y;
	float seed = v_pass.y;
	float pass = v_pass.x;
	vec3 base = v_col.rgb;
	vec3 hot = vec3(1.0, 0.97, 0.9);
	vec3 col;
	float a;
	if (pass < 0.5) {
		// The wash: a soft warm bloom, its breadth breathing along the stroke, a little blotchy.
		float wander = 0.75 + 0.25 * noise(vec2(s * 0.9 + seed, seed * 3.1));
		float r = abs(x) / wander;
		a = exp(-r * r * 2.6) * (0.75 + 0.5 * noise(vec2(s * 1.6 + seed * 2.0, x * 1.5)));
		col = mix(base, base * vec3(1.15, 0.85, 0.6), 0.35);
		a *= 0.32;
	} else if (pass < 1.5) {
		// The body: one loaded stroke — its edge wanders and tears, bristles drag streaks along it, its paint thins
		// in places (dry brush).
		float edge = 0.62 + 0.3 * noise(vec2(s * 2.4 + seed * 1.3, seed)) + 0.12 * noise(vec2(s * 7.0 + seed, 2.0));
		float r = abs(x) / edge;
		float bristle = noise(vec2(x * 7.0 + seed * 4.0, s * 0.9 + seed));
		float streak = noise(vec2(x * 15.0 + seed * 9.0, s * 0.35));
		float load = 0.55 + 0.6 * noise(vec2(s * 0.7 + seed * 5.0, 0.5));
		float dry = smoothstep(0.2, 0.65, bristle * 0.7 + streak * 0.5 + load * 0.4 - r * 0.55);
		a = (1.0 - smoothstep(0.7, 1.0, r)) * dry;
		// Paler where it is laid thickest, its warm colour at the thin edges.
		col = mix(base * 1.1, mix(base, hot, 0.55), (1.0 - r) * load);
		a *= 0.75;
	} else {
		// The core: a white-hot thread, broken where the brush lifted, flaring where it pressed.
		float gate = smoothstep(0.3, 0.55, noise(vec2(s * 2.6 + seed * 7.0, 0.5)));
		float press = 0.7 + 0.6 * noise(vec2(s * 5.0 + seed * 3.0, 1.5));
		a = exp(-x * x * 9.0) * gate * press;
		col = hot * 1.6;
	}
	ALBEDO = col;
	ALPHA = clamp(a * v_col.a, 0.0, 1.0);
}
