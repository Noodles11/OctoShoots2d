// A pearl as a flat icon (the title's Sea-pedia): its gradient and pattern on a lit sphere, turning slowly, with a
// highlight and a rim. `state` 0 shows it in full, 1 dims it (seen, never taken), 2 makes it a dark silhouette (unknown).
shader_type canvas_item;

uniform vec3 c0 : source_color = vec3(1.0);
uniform vec3 c1 : source_color = vec3(0.9);
uniform vec3 c2 : source_color = vec3(0.7);
uniform int pattern = 0;       // PearlPattern: Bands, Swirl, Spots, Marble, Rings, Stripes, Speckle, Halo
uniform int state = 0;
uniform float spin = 0.0;
uniform float glint = -1.0;    // 0..1 sweeps a glint across; < 0 none

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1, 0)), u.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), u.x), u.y);
}

void fragment() {
	vec2 p = UV * 2.0 - 1.0;
	float r = length(p);
	float edge = 1.0 - smoothstep(0.94, 1.0, r);
	vec3 n = vec3(p, sqrt(max(0.0, 1.0 - r * r)));
	// Surface coordinates on the turning sphere.
	float lon = atan(n.x, n.z) + spin, lat = asin(clamp(n.y, -1.0, 1.0));
	vec2 s = vec2(lon, lat);
	float t = 0.0;
	if (pattern == 0) t = 0.5 + 0.5 * sin(lat * 9.0);
	else if (pattern == 1) t = 0.5 + 0.5 * sin(lon * 3.0 + lat * 6.0);
	else if (pattern == 2) t = step(0.72, noise(s * 3.2));
	else if (pattern == 3) t = smoothstep(0.35, 0.65, noise(s * 2.2 + noise(s * 4.0)));
	else if (pattern == 4) t = 0.5 + 0.5 * sin(length(vec2(lon * 0.6, lat)) * 14.0);
	else if (pattern == 5) t = step(0.5, fract(lon * 1.6));
	else if (pattern == 6) t = step(0.86, hash(floor(s * 9.0)));
	else t = smoothstep(0.2, 1.0, 1.0 - abs(n.z - 0.4));
	vec3 base = mix(c0, c1, smoothstep(-0.8, 0.9, n.y));
	vec3 col = mix(base, c2, t * 0.65);
	// Light from the upper left, a soft rim, and the highlight.
	float lit = clamp(dot(n, normalize(vec3(-0.5, 0.6, 0.65))), 0.0, 1.0);
	col *= 0.55 + 0.6 * lit;
	col += pow(1.0 - n.z, 3.0) * 0.35;
	col += pow(clamp(dot(n, normalize(vec3(-0.45, 0.55, 0.7))), 0.0, 1.0), 40.0) * 0.9;
	if (glint >= 0.0) col += (1.0 - smoothstep(0.0, 0.08, abs(p.x + p.y * 0.4 - (glint * 3.0 - 1.5)))) * 0.6;
	if (state == 1) col = mix(col, vec3(dot(col, vec3(0.33))), 0.5) * 0.85;
	if (state == 2) col = vec3(0.09, 0.12, 0.15) + pow(1.0 - n.z, 3.0) * 0.12;
	COLOR = vec4(col, edge * (state == 1 ? 0.75 : 1.0));
}
