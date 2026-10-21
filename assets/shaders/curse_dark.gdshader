// The curse den's dark (DESIGN-TOPDOWN §4.2): a dense black fog over the whole screen while she is in the tunnel,
// thinning only a little round her — her light is faded too — so she finds her way by the walls within a couple of
// metres. The fog's edge drifts and curls (slow noise), so the dark looks like something in the water, not a mask.
shader_type canvas_item;

uniform float darkness = 0.0;     // 0 none → 1 the full dark
uniform vec2 size = vec2(1920.0, 1080.0);
uniform vec2 centre = vec2(960.0, 540.0);  // her, in pixels
uniform float inner = 60.0;       // clear-ish within this (pixels)…
uniform float outer = 180.0;      // …black past this
uniform float veil = 0.5;         // how much of the dark lies even over her

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

float fbm(vec2 p) {
	float v = 0.0, a = 0.5;
	for (int i = 0; i < 4; i++) {
		v += a * noise(p);
		p = p * 2.03 + vec2(1.7, 9.2);
		a *= 0.5;
	}
	return v;
}

void fragment() {
	if (darkness <= 0.001) discard;
	vec2 px = UV * size;
	vec2 q = px / 220.0;
	float n = fbm(q + vec2(TIME * 0.05, -TIME * 0.035) + fbm(q * 0.7 - TIME * 0.02));
	float d = distance(px, centre) + (n - 0.5) * outer * 0.6;
	float hole = smoothstep(inner, outer, d);
	float a = mix(veil, 1.0, hole) * darkness;
	// Not flat black: the faintest bruised violet in the fog's folds.
	COLOR = vec4(vec3(0.015, 0.005, 0.03) * (0.4 + n), a);
}
