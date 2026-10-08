// The beneath-layer (THEME-BIBLE §6.7): the floor is translucent water, not ground glass. About 13 m below it lies a
// blurred, slow-drifting shadow copy of the next depth: its structures as silhouettes, its creatures as shadows
// gliding underfoot. Seen through the floor along each view ray, so it moves with parallax beneath the reef.
// Cosmetic only (it never touches play); everything here is a function of position and time.
//   reef_beneath: x strength (per depth: faint in the Shallows, the scenery in the Trench and Abyss), y the layer's
//   height, z Menace. reef_beneath_tint: the next depth's murk, the colour of its shadows.

global uniform vec4 reef_beneath;
global uniform vec3 reef_beneath_tint;

float bn_hash(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float bn_noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(bn_hash(i), bn_hash(i + vec2(1.0, 0.0)), f.x), mix(bn_hash(i + vec2(0.0, 1.0)), bn_hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

float bn_fbm(vec2 p) {
	float s = 0.5 * bn_noise(p);
	s += 0.25 * bn_noise(p * 2.03 + 7.1);
	s += 0.125 * bn_noise(p * 4.11 + 3.3);
	return s / 0.875;
}

// A creature's shadow, nose along dir: an elongated body that undulates as it swims, and a forked tail. Soft-edged,
// as if seen through 13 m of water.
float bn_creature(vec2 q, vec2 dir, float len, float wid, float t) {
	vec2 side = vec2(-dir.y, dir.x);
	float along = dot(q, dir);
	float across = dot(q, side);
	across -= sin(along / len * 2.4 - t * 2.6) * wid * 0.22;
	float body = length(vec2(along / len, across / (wid * (1.0 - 0.35 * (1.0 - smoothstep(-1.0, -0.2, along / len))))));
	float shape = 1.0 - smoothstep(0.55, 1.2, body);
	// The tail: a fork behind the body.
	float back = -(along + len * 0.95);
	float fork = abs(abs(across) - back * 0.55);
	float tail = (step(0.0, back) - step(len * 0.6, back)) * (1.0 - smoothstep(0.0, wid * 0.55, fork));
	return max(shape, tail * 0.85);
}

// The shadow (x, 0 to 1) and the Crack's backlight (y) at a point of the beneath-layer.
vec2 beneath(vec2 q, float t, vec4 crack) {
	float shade = 0.0;
	// The deeper reef's ridges: broad dark silhouettes.
	shade = max(shade, smoothstep(0.56, 0.74, bn_fbm(q * 0.022 + 3.1)) * 0.7);
	// Forests of the next biome (kelp for the Shallows): clumps of fronds swaying in a slow surge.
	vec2 surge = vec2(sin(t * 0.55 + q.y * 0.04), cos(t * 0.45 + q.x * 0.05)) * 0.9;
	float forest = smoothstep(0.42, 0.62, bn_fbm(q * 0.028 + 9.0));
	float fronds = smoothstep(0.56, 0.7, bn_fbm((q + surge) * 0.16)) * (0.55 + 0.45 * bn_noise((q + surge) * 1.1));
	shade = max(shade, fronds * forest * 0.8);
	// Big shapes gliding on long, slow paths across the whole reef.
	for (int i = 0; i < 6; i++) {
		float fi = float(i);
		float a = fi * 2.39 + 0.7 + 0.25 * sin(t * 0.03 + fi);
		vec2 dir = vec2(cos(a), sin(a));
		vec2 c = vec2(fi * 41.0 + 20.0, fi * 67.0 + 35.0) + dir * t * (1.1 + fi * 0.3);
		c = mod(c + 20.0, 190.0) - 20.0;
		if (length(q - c) > 14.0) continue;
		shade = max(shade, bn_creature(q - c, dir, 3.4 + fi * 1.3, 1.3 + fi * 0.45, t + fi * 1.7) * 0.95);
	}
	// A school of small fish: a drifting cloud of darting specks.
	vec2 school = vec2(75.0, 75.0) + vec2(cos(t * 0.045), sin(t * 0.06)) * 52.0;
	vec2 sq = q - school;
	float cloud = 1.0 - smoothstep(3.5, 9.0, length(sq * vec2(1.0, 1.4)));
	float specks = step(0.8, bn_noise(sq * 1.7 + vec2(t * 0.9, t * 0.35)));
	shade = max(shade, cloud * specks * 0.65);
	// The Crack lights both layers: an orange glow spreading under the arena.
	vec2 dc = q - crack.xy;
	float glow = crack.w * exp(-dot(dc, dc) / 160.0);
	return vec2(shade, glow);
}
