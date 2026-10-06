// Queen Clam's valves: a fan of ribs from the hinge (local x = -1 on the unit hemisphere) with growth bands.
// `drained` (per instance) washes her colours out while she is corrupted; `flash` whitens her on a hit.
shader_type spatial;
render_mode cull_disabled;

instance uniform float drained = 1.0;
instance uniform float flash = 0.0;
uniform vec3 color_a : source_color = vec3(0.32, 0.72, 0.68);
uniform vec3 color_b : source_color = vec3(0.13, 0.4, 0.46);
uniform float ribs = 8.0;

varying vec3 lp;

void vertex() {
	lp = VERTEX;
}

void fragment() {
	vec2 from_hinge = lp.xz - vec2(-1.0, 0.0);
	float ang = atan(from_hinge.y, from_hinge.x);
	float rib = 0.5 + 0.5 * cos(ang * ribs * 2.0);
	float band = fract(length(from_hinge) * 3.2);
	vec3 c = mix(color_b, color_a, smoothstep(0.2, 0.8, rib));
	c *= 0.82 + 0.18 * smoothstep(0.0, 0.25, band);
	// The lip: a paler scalloped rim.
	c = mix(c, vec3(0.85, 0.95, 0.9), smoothstep(0.82, 0.98, length(from_hinge) * 0.5) * 0.5);
	float grey = dot(c, vec3(0.3, 0.59, 0.11));
	c = mix(c, vec3(grey) * vec3(0.92, 0.93, 0.96), drained * 0.8);
	c = mix(c, vec3(1.0), clamp(flash, 0.0, 1.0));
	ALBEDO = c;
	ROUGHNESS = 0.5;
	SPECULAR = 0.45;
	EMISSION = vec3(flash * 0.6);
}
