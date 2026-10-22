// The wash under her painted light (LightTrails, light_stroke.gdshader's first pass): a warm amber glaze laid over the
// scene, not added to it — so on the Shallows' pale sand the light still has a painted edge to stand against, and in the
// dark depths it glows amber. Its breadth breathes along the stroke and it is a little blotchy. Vertex COLOR: rgb the
// light's colour, a its strength. UV: x across (0 → 1), y along it in metres; UV2.y a seed.
shader_type spatial;
render_mode blend_mix, unshaded, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;

varying vec4 v_col;
varying float v_seed;

void vertex() {
	v_col = COLOR;
	v_seed = UV2.y;
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
	float wander = 0.7 + 0.3 * noise(vec2(s * 0.9 + v_seed, v_seed * 3.1));
	float r = abs(x) / wander;
	float blot = 0.7 + 0.6 * noise(vec2(s * 1.6 + v_seed * 2.0, x * 1.5));
	float a = exp(-r * r * 2.2) * blot;
	ALBEDO = mix(v_col.rgb, vec3(0.95, 0.5, 0.16), 0.55);
	ALPHA = clamp(a * v_col.a * 0.5, 0.0, 0.7);
}
