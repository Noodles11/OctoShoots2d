// Item pearls: a 2–3 colour gradient glowing from within and brighter at the rim, the icon of what it does engraved on the
// face turned to the camera (pearl_engraving.gdshaderinc), cut dark with a lit lip. Shared by the world pearl and the
// tentacle (overlay) pearl.

#include "res://assets/shaders/pearl_engraving.gdshaderinc"

uniform vec3 c0 : source_color = vec3(1.0);
uniform vec3 c1 : source_color = vec3(0.8);
uniform vec3 c2 : source_color = vec3(0.6);
uniform int icon = 0;
uniform float glow = 1.0;
uniform float seed = 0.0;
uniform float highlight = 0.0;
// The absorbing ritual: as she takes its look, the pearl pales to an empty, milky vessel (1).
uniform float pale = 0.0;

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
	// Its colours run down the face, stirred a little.
	float t = 0.5 - 0.45 * NORMAL.y + 0.3 * (vnoise(n * 2.5 + seed) - 0.5);
	vec3 col = ramp(t);
	// The engraving, always toward the camera: dark in the cut, its far wall catching the light.
	vec2 q = NORMAL.xy * 1.05;
	float d = pearl_engraving(q, icon);
	float px = max(fwidth(d), 0.001);
	float cut = (1.0 - smoothstep(-px, px, d)) * (1.0 - pale);
	float lip = cut * smoothstep(-px, px, pearl_engraving(q + vec2(0.04, -0.04), icon));
	col = col * (1.0 - 0.72 * cut) + mix(col, vec3(1.0), 0.5) * lip * 0.7;
	col = mix(col, vec3(0.88, 0.89, 0.9), pale * 0.85);
	ALBEDO = col;
	ROUGHNESS = 0.18;
	METALLIC = 0.15;
	SPECULAR = 0.8;
	EMISSION = (col * glow * (0.35 + 0.65 * fres) * (1.0 - 0.75 * cut) + vec3(fres * 0.4)) * (1.0 - 0.8 * pale) + vec3(highlight * 0.6);
}
