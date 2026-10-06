// Moon jellies: a translucent, softly glowing bell that contracts with `pulse`; arms and tentacles trail and sway.
// Vertex alpha: 0.7 bell, 0.85 glowing core, 0.5 trailing parts.
shader_type spatial;
render_mode cull_disabled, blend_mix, depth_draw_opaque;

instance uniform float phase = 0.0;
instance uniform float swim = 0.0;
instance uniform float pulse = 0.0;
instance uniform float glow = 0.0;
instance uniform float wag = 0.0;
instance uniform float quiver = 0.0;
instance uniform float flash = 0.0;
instance uniform vec3 tint : source_color = vec3(1.0);
instance uniform float tint_amount = 0.0;
// Corrupted jellies cycle slowly through aquarium-LED colours; healthy ones are pale and steady.
instance uniform float corrupt = 0.0;

void vertex() {
	float a = COLOR.a;
	if (a > 0.6 && a < 0.8) {
		VERTEX.xz *= 1.0 - 0.3 * pulse;
		VERTEX.y *= 1.0 + 0.22 * pulse;
	} else if (a > 0.4 && a < 0.6) {
		float k = 0.05 * (0.6 + swim) * clamp(-VERTEX.y * 1.5, 0.0, 1.0);
		VERTEX.x += sin(TIME * 2.5 + phase + VERTEX.y * 5.0) * k;
		VERTEX.z += cos(TIME * 2.1 + phase * 1.3 + VERTEX.y * 4.0) * k;
		VERTEX.xz *= 1.0 - 0.2 * pulse;
	}
}

void fragment() {
	float a = COLOR.a;
	vec3 col = mix(COLOR.rgb, tint, tint_amount);
	vec3 led = 0.55 + 0.45 * cos(6.2831 * (TIME * 0.12 + phase * 0.1 + vec3(0.0, 0.33, 0.67)));
	col = mix(col, led * (0.6 + 0.6 * dot(COLOR.rgb, vec3(0.33))), corrupt * 0.85);
	float fres = pow(1.0 - clamp(dot(normalize(NORMAL), normalize(VIEW)), 0.0, 1.0), 2.0);
	ALBEDO = col;
	ROUGHNESS = 0.2;
	float base = (a > 0.8) ? 0.9 : (a > 0.6 ? 0.34 : 0.55);
	ALPHA = clamp(base + fres * 0.4 + flash * 0.4, 0.0, 1.0);
	EMISSION = col * (0.55 + 0.6 * glow + fres * 0.8) + vec3(flash) + (a > 0.8 ? col * 1.5 : vec3(0.0));
}
