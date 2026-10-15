// The clam room's shell (ClamRoomMeshes): inside, mother-of-pearl — a milky pink-white with a shifting rainbow sheen
// that slides as the view moves, glossy, glowing faintly in its own light; outside, weathered shell, ribbed and banded
// with growth lines. The ribs run round (UV.x) and are drawn as both shading and colour.
shader_type spatial;
render_mode cull_disabled;

uniform float ribs = 18.0;
uniform vec3 nacre_base : source_color = vec3(0.96, 0.88, 0.9);
uniform vec3 shell_outer : source_color = vec3(0.62, 0.52, 0.5);
uniform float glow = 0.8;

varying vec3 v_normal;

void vertex() {
	// The mesh's normals point to the nacre side; which side the camera sees decides the look.
	v_normal = normalize((MODELVIEW_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

void fragment() {
	float rib = sin(UV.x * 6.2831 * ribs);
	float groove = smoothstep(0.6, 1.0, -rib);
	if (dot(v_normal, VIEW) > 0.0) {
		NORMAL = v_normal;
		// Nacre: thin-film colours by the angle to the eye, banded a little by the ribs.
		float fres = 1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0);
		vec3 film = 0.5 + 0.5 * cos(6.2831 * (fres * 1.3 + UV.y * 0.6 + rib * 0.06 + vec3(0.0, 0.33, 0.67)));
		vec3 col = mix(nacre_base, film, 0.32 + 0.25 * fres);
		col *= 1.0 - 0.18 * groove;
		ALBEDO = col;
		ROUGHNESS = 0.12;
		SPECULAR = 0.9;
		CLEARCOAT = 1.0;
		CLEARCOAT_ROUGHNESS = 0.05;
		EMISSION = col * glow * (0.6 + 0.4 * fres);
	} else {
		// The outside: rough shell, ribbed, banded with growth lines toward the rim.
		float bands = 0.85 + 0.15 * sin(UV.y * 90.0);
		ALBEDO = shell_outer * bands * (1.0 - 0.3 * groove);
		ROUGHNESS = 0.85;
		NORMAL = -v_normal;
	}
}
