// Soft additive light around a pearl clam: a shaft of light rising from an open clam (mode 0),
// a glowing pool on the sand (mode 1), or the glow seeping through the seam of a shut clam (mode 2).
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, fog_disabled;

uniform vec3 color : source_color = vec3(0.6, 0.9, 1.0);
uniform float intensity = 1.0;
uniform int mode = 0;
uniform float height = 3.5;

varying vec3 local;

void vertex() {
	local = VERTEX;
}

void fragment() {
	float a;
	if (mode == 0) {
		float up = clamp(local.y / height + 0.5, 0.0, 1.0);
		float edge = pow(abs(dot(NORMAL, VIEW)), 2.0);
		float flicker = 0.85 + 0.15 * sin(TIME * 1.7 + local.y * 3.0);
		a = edge * (1.0 - up) * (1.0 - up) * smoothstep(0.0, 0.08, up) * flicker * 0.22;
	} else if (mode == 1) {
		float r = length(UV - 0.5) * 2.0;
		a = pow(max(0.0, 1.0 - r), 2.2) * 0.6;
	} else {
		float r = length(UV - 0.5) * 2.0;
		a = smoothstep(1.0, 0.75, r) * smoothstep(0.4, 0.9, r) * (0.7 + 0.3 * sin(TIME * 2.3)) * 1.4;
	}
	ALBEDO = color * a * intensity;
}
