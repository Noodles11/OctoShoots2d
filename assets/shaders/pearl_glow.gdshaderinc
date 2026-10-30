// A clam room's pearl lighting the room round it (ClamRoomView): the light leaving the pearl in each direction takes the
// pearl's own colours, softly mottled, so the room's walls, floor and clam carry them and the room shows from afar what
// it holds. A shader
// includes this, sets glow_world in its vertex() and adds pearl_glow(glow_world) to what it shows.

uniform vec3 glow_pos = vec3(0.0);
uniform vec3 glow_c0 : source_color = vec3(1.0);
uniform vec3 glow_c1 : source_color = vec3(1.0);
uniform vec3 glow_c2 : source_color = vec3(1.0);
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
	// Its colours mottled over the sphere of directions round it (wide patches on a wall).
	float t = glow_noise(n * 3.0 + glow_seed);
	t = clamp(t, 0.0, 1.0);
	vec3 col = t < 0.5 ? mix(glow_c0, glow_c1, t * 2.0) : mix(glow_c1, glow_c2, t * 2.0 - 1.0);
	// Strong contrast between the patches, the dark ones near dark, so it reads; falling off gently with the distance from the pearl, so
	// it reaches across the whole chamber (6.5 m) to the wall.
	// (Held flat over the first 2 m, so the clam right round the pearl doesn't burn white.) Its own fade has no end:
	// whatever lays it on fades it out before its own edge.
	float near = max(d, 2.0);
	float fall = 1.0 / (1.0 + near * near * 0.03);
	return col * (0.06 + 1.1 * t) * fall * glow_strength;
}
