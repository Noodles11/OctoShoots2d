// Clementine's bubbles: nearly clear in the middle, bright and faintly iridescent at the rim (fresnel), with a small
// specular glint. `fade` (per instance) fades a popping bubble's shell and droplets out; `rainbow` (a bubble merged
// to the cap) sweeps bright spectral bands over it.
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_back, unshaded, shadows_disabled;

instance uniform float fade = 1.0;
instance uniform float rainbow = 0.0;
uniform vec3 tint : source_color = vec3(0.86, 0.95, 1.0);

void fragment() {
	float rim = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 2.2);
	vec3 irid = 0.5 + 0.5 * cos(6.2831 * (rim * 1.4 + vec3(0.0, 0.33, 0.67)));
	float glint = smoothstep(0.9, 0.97, dot(NORMAL, normalize(vec3(-0.45, 0.55, 0.7))));
	vec3 n = (INV_VIEW_MATRIX * vec4(NORMAL, 0.0)).xyz;
	vec3 spectrum = 0.5 + 0.5 * cos(6.2831 * (n.x * 0.6 + n.z * 0.4 + rim * 0.8 - TIME * 0.6 + vec3(0.0, 0.33, 0.67)));
	vec3 base = mix(tint, irid, 0.3 * rim);
	ALBEDO = mix(base, spectrum * 1.25, rainbow * (0.45 + 0.5 * rim)) + vec3(glint);
	ALPHA = clamp((0.07 + 0.8 * rim + glint + rainbow * 0.22) * fade, 0.0, 1.0);
}
