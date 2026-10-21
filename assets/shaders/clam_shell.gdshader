// Queen Clam's valves (docs/CORRUPTION.md, art rules in CLAUDE.md): a real giant clam's shell, no face. Heavy radial
// ribs fanning from the hinge (local x = -1 on the unit hemisphere), fine concentric growth lines, a wavy scalloped
// lip, calcified bone-cream between the ribs. `drained` (per instance) is the corruption in her: the colour drained to
// ash and ink settled into every groove; freed, it washes out. `flash` whitens her on a hit.
shader_type spatial;
render_mode cull_disabled;

instance uniform float drained = 1.0;
instance uniform float flash = 0.0;
// The shared boss skin's ink surge (boss_body.gdshader); a clam's shell does not show it.
instance uniform float surge = 0.0;
uniform vec3 color_a : source_color = vec3(0.86, 0.81, 0.72);
uniform vec3 color_b : source_color = vec3(0.55, 0.49, 0.43);
uniform vec3 ink : source_color = vec3(0.025, 0.02, 0.03);
uniform float ribs = 7.0;

varying vec3 lp;

void vertex() {
	lp = VERTEX;
	// The ribs stand proud of the shell, and the lip waves with them.
	vec2 h = lp.xz - vec2(-1.0, 0.0);
	float ang = atan(h.y, h.x);
	float rib = cos(ang * ribs * 2.0);
	float reach = length(h) * 0.5;
	VERTEX += NORMAL * rib * 0.05 * smoothstep(0.1, 0.6, reach);
}

void fragment() {
	vec2 h = lp.xz - vec2(-1.0, 0.0);
	float ang = atan(h.y, h.x);
	float reach = length(h) * 0.5;
	float rib = 0.5 + 0.5 * cos(ang * ribs * 2.0);
	// Fine growth lines, wavering a little round the shell.
	float growth = 0.5 + 0.5 * sin(reach * 70.0 + sin(ang * 9.0) * 0.6);
	vec3 c = mix(color_b, color_a, smoothstep(0.15, 0.85, rib));
	c *= 0.86 + 0.14 * growth;
	// The lip: thicker, paler calcite.
	c = mix(c, color_a * 1.08, smoothstep(0.84, 0.98, reach) * 0.6);
	// Corrupted: the colour drained to ash, and ink settled into the grooves and growth lines.
	float grey = dot(c, vec3(0.3, 0.59, 0.11));
	c = mix(c, vec3(grey * 0.8) * vec3(1.04, 1.0, 0.95), drained * 0.85);
	float groove = (1.0 - smoothstep(0.0, 0.35, rib)) * 0.8 + (1.0 - growth) * 0.25;
	c = mix(c, ink, clamp(groove * drained, 0.0, 0.85));
	c = mix(c, vec3(1.0), clamp(flash, 0.0, 1.0));
	ALBEDO = c;
	ROUGHNESS = mix(0.55, 0.75, drained);
	SPECULAR = 0.35;
	EMISSION = vec3(flash * 0.6);
}
