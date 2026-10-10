// Gloomvines (docs/CORRUPTION.md): thick ink vines, glossy, a slow violet pulse running from the root to the tip.
// Vertex COLOR: r along the vine (0 root → 1 tip), g dissolving (0..1), b coiling.
shader_type spatial;
render_mode cull_back, shadows_disabled;

varying vec4 v_col;

void vertex() { v_col = COLOR; }

float hash(vec2 p) { return fract(sin(dot(p, vec2(41.3, 289.1))) * 43758.5453); }

void fragment() {
	float along = v_col.r, gone = v_col.g, coil = v_col.b;
	if (gone > 0.0 && hash(floor(UV * vec2(14.0, 40.0))) < gone) discard;
	float facing = clamp(dot(NORMAL, VIEW), 0.0, 1.0);
	float fres = pow(1.0 - facing, 2.6);
	float pulse = smoothstep(0.85, 1.0, sin(along * 9.0 - TIME * 3.0) * 0.5 + 0.5);
	ALBEDO = vec3(0.012, 0.009, 0.018);
	ROUGHNESS = 0.14;
	SPECULAR = 0.8;
	EMISSION = vec3(0.3, 0.08, 0.52) * (fres * 0.4 + pulse * 0.5 + coil * 0.6) + vec3(1.0, 0.7, 0.3) * gone * 1.4;
}
