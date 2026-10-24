// A clam room's pearl lighting the room round it (ClamRoomView): the light leaving the pearl in each direction takes the
// pearl's own colours in its own pattern (the eight of pearl_common: bands, swirl, spots, marble, rings, stripes,
// speckle, halo), so the room's walls, floor and clam carry its look and the room shows from afar what it holds. A shader
// includes this, sets glow_world in its vertex() and adds pearl_glow(glow_world) to what it shows.

uniform vec3 glow_pos = vec3(0.0);
uniform vec3 glow_c0 : source_color = vec3(1.0);
uniform vec3 glow_c1 : source_color = vec3(1.0);
uniform vec3 glow_c2 : source_color = vec3(1.0);
uniform int glow_pattern = 7;
uniform float glow_seed = 0.0;
// 0 dark (no pearl, or taken) → its full light.
uniform float glow_strength = 0.0;

varying vec3 glow_world;

float glow_hash(vec3 p) {
	p = fract(p * 0.3183099 + 0.1);
	p *= 17.0;
	return fract(p.x * p.y * p.z * (p.x + p.y + p.z));
}

float glow_noise(vec3 x) {
	vec3 i = floor(x);
	vec3 f = fract(x);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(mix(glow_hash(i), glow_hash(i + vec3(1, 0, 0)), f.x), mix(glow_hash(i + vec3(0, 1, 0)), glow_hash(i + vec3(1, 1, 0)), f.x), f.y),
	           mix(mix(glow_hash(i + vec3(0, 0, 1)), glow_hash(i + vec3(1, 0, 1)), f.x), mix(glow_hash(i + vec3(0, 1, 1)), glow_hash(i + vec3(1, 1, 1)), f.x), f.y), f.z);
}

vec3 pearl_glow(vec3 at) {
	if (glow_strength <= 0.0) return vec3(0.0);
	vec3 off = at - glow_pos;
	float d = length(off);
	vec3 n = off / max(d, 1e-4);
	// The pearl's pattern on the sphere of directions round it (scaled up: on a wall the pattern's bands are wide).
	float t;
	if (glow_pattern == 0) t = 0.5 + 0.5 * sin(n.y * 9.0);
	else if (glow_pattern == 1) t = 0.5 + 0.5 * sin(atan(n.z, n.x) * 3.0 + n.y * 8.0);
	else if (glow_pattern == 2) t = smoothstep(0.55, 0.75, glow_noise(n * 5.0 + glow_seed));
	else if (glow_pattern == 3) t = 0.5 + 0.5 * sin(n.x * 6.0 + 4.0 * glow_noise(n * 3.0 + glow_seed));
	else if (glow_pattern == 4) t = 0.5 + 0.5 * cos(acos(clamp(n.y, -1.0, 1.0)) * 10.0);
	else if (glow_pattern == 5) t = mix(step(0.0, sin(atan(n.z, n.x) * 6.0)), 0.5, 0.15);
	else if (glow_pattern == 6) { t = glow_noise(n * 14.0 + glow_seed); t = t * t; }
	else t = 1.0 - abs(n.y);
	t = clamp(t, 0.0, 1.0);
	vec3 col = t < 0.5 ? mix(glow_c0, glow_c1, t * 2.0) : mix(glow_c1, glow_c2, t * 2.0 - 1.0);
	// Strong contrast between the pattern's parts, its dark parts near dark, so it reads; falling off gently with the distance from the pearl, so
	// it reaches across the whole chamber (6.5 m) to the wall.
	// (Held flat over the first 2 m, so the clam right round the pearl doesn't burn white.)
	float near = max(d, 2.0);
	float fall = 1.0 / (1.0 + near * near * 0.03) * (1.0 - smoothstep(8.0, 12.0, d));
	return col * (0.06 + 1.1 * t) * fall * glow_strength;
}
