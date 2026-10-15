// The clam's tongue (ClamRoomMeshes.Tongue): soft pink velvet the pearl lies on. Velvet is brightest at its edges (the
// pile catches the light sideways) and matte face-on; a fine fuzz breaks it up, a warmer flush lies in the folds, and a
// little light comes through it like flesh.
shader_type spatial;
render_mode cull_back;

uniform vec3 velvet : source_color = vec3(0.93, 0.48, 0.62);
uniform vec3 sheen : source_color = vec3(1.0, 0.8, 0.88);
uniform vec3 flush : source_color = vec3(0.78, 0.25, 0.42);

float hash(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

void fragment() {
	float facing = clamp(dot(NORMAL, VIEW), 0.0, 1.0);
	float edge = pow(1.0 - facing, 2.2);
	float fuzz = 0.9 + 0.2 * hash(floor(UV * vec2(420.0, 160.0)));
	// The folds of the frill run darker and warmer.
	float fold = 0.5 + 0.5 * sin(UV.x * 6.2831 * 9.0);
	vec3 col = mix(velvet, flush, 0.35 * fold * smoothstep(0.3, 0.6, UV.y)) * fuzz;
	ALBEDO = col;
	ROUGHNESS = 1.0;
	SPECULAR = 0.15;
	BACKLIGHT = col * 0.5;
	EMISSION = sheen * edge * 0.45 + col * 0.06;
}
