// A valve (docs/CORRUPTION.md): a wall of dense darkness across a neck. Smoke of ink rolling upward, dense in the
// middle, wisping at its edges and top, glinting violet; as it is worn down (integrity) it thins and its cracks
// burn gold. UV: x across the wall, y up.
shader_type spatial;
render_mode blend_mix, unshaded, depth_draw_never, cull_disabled, shadows_disabled;

instance uniform float integrity = 1.0;
instance uniform float fade = 1.0;
instance uniform float seed = 0.0;

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
float fbm(vec2 p) {
	float v = 0.0, a = 0.5;
	for (int i = 0; i < 5; i++) { v += a * noise(p); p = p * 2.03 + 11.7; a *= 0.5; }
	return v;
}

void fragment() {
	vec2 uv = UV;
	float t = TIME * 0.35;
	vec2 q = vec2(uv.x * 3.0 + seed, uv.y * 2.0 - t);
	float smoke = fbm(q + vec2(fbm(q * 1.7 + t), fbm(q * 1.3 - t)) * 1.2);
	float edge = smoothstep(0.0, 0.18, uv.x) * (1.0 - smoothstep(0.82, 1.0, uv.x)) * (1.0 - smoothstep(0.55, 1.0, uv.y)) * smoothstep(0.0, 0.08, uv.y);
	float dense = smoothstep(0.25, 0.75, smoke) * edge;
	float thin = mix(0.35, 1.0, integrity);
	vec3 col = vec3(0.012, 0.008, 0.02) + vec3(0.2, 0.06, 0.34) * pow(smoke, 4.0) * 0.6;
	// Worn down: its cracks burn gold.
	float crack = (1.0 - integrity) * smoothstep(0.62, 0.7, fbm(q * 3.0 + 4.0)) * edge;
	col += vec3(1.3, 0.85, 0.35) * crack * 1.6;
	ALBEDO = col;
	ALPHA = clamp(dense * 0.92 * thin + crack, 0.0, 1.0) * fade;
}
