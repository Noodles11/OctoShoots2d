// The clam room's shell (ClamRoomMeshes): inside, mother-of-pearl — a milky pink-white with a shifting rainbow sheen
// that slides as the view moves, glossy, glowing faintly in its own light; outside, weathered shell, ribbed and banded
// with growth lines. The ribs run round (UV.x) and are drawn as both shading and colour.
shader_type spatial;
render_mode cull_disabled;

uniform float ribs = 18.0;
uniform vec3 nacre_base : source_color = vec3(0.56, 0.5, 0.56);
uniform vec3 shell_outer : source_color = vec3(0.62, 0.52, 0.5);
// Its own faint glow (kept low: the room is lit by the pearl in it, ClamRoomView, whose pattern its light paints here
// and must read over the shell's own colour).
uniform float glow = 0.1;

#include "res://assets/shaders/pearl_glow.gdshaderinc"

varying vec3 v_normal;

void vertex() {
	glow_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
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
		// Glossy, but not a mirror: her lantern close by leaves a soft sheen, not a white blot over the pattern.
		ROUGHNESS = 0.32;
		SPECULAR = 0.45;
		CLEARCOAT = 0.4;
		CLEARCOAT_ROUGHNESS = 0.25;
		// Its own faint glow, and the pearl's light in its pattern over it.
		EMISSION = col * glow * (0.6 + 0.4 * fres) + pearl_glow(glow_world) * (0.45 + 0.55 * col);
	} else {
		// The outside: rough shell, ribbed, banded with growth lines toward the rim.
		float bands = 0.85 + 0.15 * sin(UV.y * 90.0);
		ALBEDO = shell_outer * bands * (1.0 - 0.3 * groove);
		ROUGHNESS = 0.85;
		NORMAL = -v_normal;
	}
}
