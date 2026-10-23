// A Blightroot's pole (BlightView.Octopus): a stalk of ink standing from the seabed up to its head, painted as the
// murklings are (murkling.gdshader) — its ink brushed in long strokes running up it and flowing, a ragged dry-brushed
// stroke of its blue for a rim, its knots swelling it here and there. On the pulse (pulse 0 → 1) a swell of ink runs up
// it into the head. Freed (melt 0 → 1) it runs away from the top down into drips. A unit cylinder (y −0.5 → 0.5).

instance uniform vec4 skin = vec4(0.0);
instance uniform float seed = 0.0;
instance uniform float pulse = 0.0;
instance uniform float melt = 0.0;
uniform vec3 rim_color : source_color = vec3(0.2, 0.48, 1.0);

#ifdef OVER
#include "res://assets/shaders/over_her.gdshaderinc"
varying vec3 v_world;
#endif
varying float v_h;
varying float v_ang;

float hash2(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float noise2(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash2(i), hash2(i + vec2(1.0, 0.0)), f.x), mix(hash2(i + vec2(0.0, 1.0)), hash2(i + vec2(1.0, 1.0)), f.x), f.y);
}

void vertex() {
	float h = VERTEX.y + 0.5;
	v_h = h;
	v_ang = atan(VERTEX.z, VERTEX.x);
	// Knots along it, and the pulse's swell climbing it into the head.
	float knots = 1.0 + 0.14 * sin(h * 13.0 + seed * 3.0) + 0.08 * sin(h * 29.0 + seed);
	float climb = fract(TIME * 1.4 + seed);
	float swell = pulse * 0.6 * exp(-pow((h - climb) * 5.0, 2.0)) + pulse * 0.25;
	VERTEX.xz *= knots + swell;
	// It sways, more toward the top (it carries the head).
	VERTEX.x += sin(TIME * 1.1 + seed) * 0.12 * h * h;
	VERTEX.z += sin(TIME * 0.8 + seed * 1.7) * 0.1 * h * h;
#ifdef OVER
	v_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
#endif
}

void fragment() {
	// Freed: it runs away from the top down, its edge dripping.
	if (melt > 0.0 && v_h > 1.0 - melt * 1.15 + 0.12 * noise2(vec2(v_ang * 3.0 + seed, 2.0))) discard;
	float facing = clamp(dot(NORMAL, VIEW), 0.0, 1.0);
	// Long strokes running up it, the ink flowing up into the head.
	float stroke = noise2(vec2(v_ang * 4.0 + seed * 2.0, v_h * 2.2 - TIME * 0.6));
	float bristle = noise2(vec2(v_ang * 16.0 + seed, v_h * 6.0 - TIME * 1.2));
	ALBEDO = vec3(0.012, 0.01, 0.022) + vec3(0.025, 0.015, 0.045) * (stroke * 0.7 + bristle * 0.3);
	ALBEDO = mix(ALBEDO, skin.rgb * (0.2 + 0.15 * stroke), skin.a);
	ROUGHNESS = 0.8;
	SPECULAR = 0.18;
	vec3 rim = mix(rim_color, skin.rgb, skin.a);
	float edge = 0.45 + 0.15 * noise2(vec2(v_h * 5.0 + seed, v_ang * 2.0));
	float line = smoothstep(edge, edge + 0.15, 1.0 - facing);
	float dry = 0.55 + 0.6 * noise2(vec2(v_h * 14.0 + seed * 3.0, v_ang * 5.0));
	EMISSION = rim * line * dry * (0.6 + 0.9 * pulse);
#ifdef OVER
	ALPHA = over_her(v_world, VERTEX.z, VIEW_MATRIX);
	if (ALPHA < 0.01) discard;
	ROUGHNESS = 1.0;
	SPECULAR = 0.0;
	ALBEDO = over_haze(ALBEDO, 0.3);
	EMISSION *= vec3(0.72, 0.92, 1.03);
#endif
}
