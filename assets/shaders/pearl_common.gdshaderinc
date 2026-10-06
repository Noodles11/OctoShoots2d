// Item pearls: a hand-picked 2–3 colour gradient laid out in one of eight patterns, glowing from within
// and brighter at the rim. Shared by the world pearl and the tentacle (overlay) pearl.

uniform vec3 c0 : source_color = vec3(1.0);
uniform vec3 c1 : source_color = vec3(0.8);
uniform vec3 c2 : source_color = vec3(0.6);
uniform int pattern = 0;
uniform float glow = 1.0;
uniform float seed = 0.0;
uniform float highlight = 0.0;

varying vec3 local_n;

void vertex() {
	local_n = normalize(VERTEX);
}

float hash3(vec3 p) {
	p = fract(p * 0.3183099 + 0.1);
	p *= 17.0;
	return fract(p.x * p.y * p.z * (p.x + p.y + p.z));
}

float vnoise(vec3 x) {
	vec3 i = floor(x);
	vec3 f = fract(x);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(mix(hash3(i), hash3(i + vec3(1, 0, 0)), f.x), mix(hash3(i + vec3(0, 1, 0)), hash3(i + vec3(1, 1, 0)), f.x), f.y),
	           mix(mix(hash3(i + vec3(0, 0, 1)), hash3(i + vec3(1, 0, 1)), f.x), mix(hash3(i + vec3(0, 1, 1)), hash3(i + vec3(1, 1, 1)), f.x), f.y), f.z);
}

vec3 ramp(float t) {
	t = clamp(t, 0.0, 1.0);
	return t < 0.5 ? mix(c0, c1, t * 2.0) : mix(c1, c2, t * 2.0 - 1.0);
}

void fragment() {
	vec3 n = local_n;
	float fres = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0);
	float t;
	if (pattern == 0) {          // bands
		t = 0.5 + 0.5 * sin(n.y * 9.0);
	} else if (pattern == 1) {   // swirl
		t = 0.5 + 0.5 * sin(atan(n.z, n.x) * 3.0 + n.y * 8.0);
	} else if (pattern == 2) {   // spots
		t = smoothstep(0.55, 0.75, vnoise(n * 5.0 + seed));
	} else if (pattern == 3) {   // marble
		t = 0.5 + 0.5 * sin(n.x * 6.0 + 4.0 * vnoise(n * 3.0 + seed));
	} else if (pattern == 4) {   // rings
		t = 0.5 + 0.5 * cos(acos(clamp(n.y, -1.0, 1.0)) * 10.0);
	} else if (pattern == 5) {   // stripes
		t = mix(step(0.0, sin(atan(n.z, n.x) * 6.0)), 0.5, 0.15);
	} else if (pattern == 6) {   // speckle
		t = vnoise(n * 14.0 + seed);
		t = t * t;
	} else {                     // halo
		t = fres;
	}
	vec3 col = ramp(t);
	ALBEDO = col;
	ROUGHNESS = 0.18;
	METALLIC = 0.15;
	SPECULAR = 0.8;
	EMISSION = col * glow * (0.35 + 0.65 * fres) + vec3(fres * 0.4) + vec3(highlight * 0.6);
}
