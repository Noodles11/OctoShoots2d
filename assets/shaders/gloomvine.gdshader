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
	ALBEDO = vec3(0.01, 0.025, 0.07);
	ROUGHNESS = 0.14;
	SPECULAR = 0.8;
	// The Blightroot's blue, as its root (docs/CORRUPTION.md, each kind its own colour).
	EMISSION = vec3(0.12, 0.32, 0.95) * (fres * 0.55 + pulse * 0.6 + coil * 0.7) + vec3(1.0, 0.7, 0.3) * gone * 1.4;
}
