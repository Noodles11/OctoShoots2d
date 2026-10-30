// A pearl as a flat icon (the menus, the title's Sea-pedia): its gradient on a lit sphere with its engraving, swaying
// slowly, with a highlight and a rim. `state` 0 shows it in full, 1 dims it (seen, never taken), 2 makes it a dark
// silhouette (unknown).
shader_type canvas_item;

#include "res://assets/shaders/pearl_engraving.gdshaderinc"

uniform vec3 c0 : source_color = vec3(1.0);
uniform vec3 c1 : source_color = vec3(0.9);
uniform vec3 c2 : source_color = vec3(0.7);
uniform int icon = 0;          // PearlIcon
uniform int state = 0;
uniform float spin = 0.0;
uniform float glint = -1.0;    // 0..1 sweeps a glint across; < 0 none

void fragment() {
	vec2 p = UV * 2.0 - 1.0;
	float r = length(p);
	float edge = 1.0 - smoothstep(0.94, 1.0, r);
	vec3 n = vec3(p, sqrt(max(0.0, 1.0 - r * r)));
	vec3 base = mix(c0, c1, smoothstep(-0.8, 0.2, n.y));
	vec3 col = mix(base, c2, smoothstep(0.1, -0.9, n.y) * 0.8);
	// The engraving (UV runs down the screen, the icons up), swaying a little as it turns.
	vec2 q = vec2(p.x + sin(spin) * 0.05, -p.y) * 1.05;
	float d = pearl_engraving(q, icon);
	float px = max(fwidth(d), 0.001);
	float cut = 1.0 - smoothstep(-px, px, d);
	float lip = cut * smoothstep(-px, px, pearl_engraving(q + vec2(0.05, -0.05), icon));
	col = col * (1.0 - 0.72 * cut) + mix(col, vec3(1.0), 0.5) * lip * 0.7;
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
