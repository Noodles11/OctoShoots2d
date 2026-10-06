// Bubbles Clementine throws: clear in the middle, an iridescent thin-film rim, a soft wobble.
// Shared by bubbles in flight and the ones held on the tentacle's suckers (overlay).

uniform vec3 tint : source_color = vec3(0.75, 0.95, 1.0);
uniform float strength = 1.0;
uniform float wobble = 1.0;

void vertex() {
	float s = MODEL_MATRIX[3].x * 3.1 + MODEL_MATRIX[3].z * 1.7 + MODEL_MATRIX[3].y * 2.3;
	VERTEX += NORMAL * sin(TIME * 9.0 + VERTEX.y * 14.0 + s) * 0.035 * wobble;
}

void fragment() {
	float ndv = clamp(dot(NORMAL, VIEW), 0.0, 1.0);
	float fres = pow(1.0 - ndv, 2.5);
	// Thin-film colours shift with the viewing angle.
	vec3 film = 0.5 + 0.5 * cos(6.2831 * (vec3(0.0, 0.33, 0.67) + fres * 1.5 + 0.2));
	ALBEDO = mix(tint, film, 0.6);
	ROUGHNESS = 0.05;
	SPECULAR = 1.0;
	EMISSION = (film * 0.6 + tint * 0.4) * fres * 1.6 * strength;
	ALPHA = clamp(0.1 + 0.85 * fres, 0.0, 1.0) * strength;
}
