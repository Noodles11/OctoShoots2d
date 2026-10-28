// A light spring's god ray (docs/CORRUPTION.md): one cone of sunlight falling slantwise from the surface down to the
// seabed, as light finds its way down through deep water. An oblique cone, open at both ends (SpringView.RayMesh):
// UV.x round it, UV.y from the surface (0) to the floor (1). Soft as a volume: brightest where it is seen face-on,
// fading at its edges, a few broad shafts drifting round it and a slow shimmer running down. Faint at the top, where it
// comes out of the dark water, full low down. `glow` 0..1 brightens and warms it while it heals her; `store` (the
// healing it still holds, 0..1) dims it a little as it is used; `fade` 1 → 0 takes it away when it is spent.
shader_type spatial;
render_mode blend_add, unshaded, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;

instance uniform float glow = 0.0;
instance uniform float store = 1.0;
instance uniform float fade = 1.0;
instance uniform float seed = 0.0;
uniform vec3 white : source_color = vec3(0.78, 0.93, 1.0);
uniform vec3 warm : source_color = vec3(1.0, 0.88, 0.62);

void fragment() {
	float t = UV.y;
	float u = UV.x * 6.2831853;
	// A volume, not a skin: only where the cone is seen face-on does it hold light; its silhouette melts away.
	float facing = abs(dot(normalize(NORMAL), normalize(VIEW)));
	float soft = pow(smoothstep(0.0, 1.0, facing), 2.2);
	// Broad shafts round it, drifting slowly, and a shimmer running down it (the surface's ripples far above).
	float shafts = 0.55 + 0.45 * sin(u * 5.0 + TIME * 0.23 + seed * 7.0) * sin(u * 3.0 - TIME * 0.17 + seed * 3.0 + 1.7);
	float shimmer = 0.8 + 0.2 * sin(t * 14.0 - TIME * 1.3 + sin(u * 4.0 + TIME * 0.4) * 1.5);
	// Out of the dark water at the top, full low down, easing off just above the floor where its spot takes over.
	float along = smoothstep(0.0, 0.65, t) * (1.0 - 0.45 * smoothstep(0.86, 1.0, t));
	float strength = (0.16 + 0.3 * glow) * (0.6 + 0.4 * store);
	vec3 col = mix(white, warm, 0.45 * glow);
	ALBEDO = col * soft * shafts * shimmer * along * strength * fade;
	ALPHA = 1.0;
}
