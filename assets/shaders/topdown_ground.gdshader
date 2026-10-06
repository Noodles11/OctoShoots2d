// Top-down ground: vertex colour by height band (chosen by the level view). Walls between the camera and Clementine
// are not cut away: she is drawn on top of them, and the depth focus pass blurs high rock into soft silhouettes.
shader_type spatial;
render_mode cull_back;

uniform bool debug_normals = false;

void fragment() {
	// Vertex colours are authored in sRGB; shaders light in linear space.
	ALBEDO = pow(COLOR.rgb, vec3(2.2));
	if (debug_normals) { ALBEDO = vec3(0.0); EMISSION = (NORMAL * 0.5 + 0.5) * 0.6; }
	ROUGHNESS = 0.92;
	SPECULAR = 0.2;
}
