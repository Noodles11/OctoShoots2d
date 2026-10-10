// A pufferling's needle: a big hot-pink spike, lime at the point, saturated and bright so it reads (and
// can be dodged) at play distance on pale sand and in the dark alike. The tank's own colours (THEME-BIBLE §6.4).
shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, depth_draw_opaque;

instance uniform float fade = 1.0;

void fragment() {
	// UV.y runs along the cylinder: 0 at the point (its top), 1 at the base.
	float tip = 1.0 - UV.y;
	vec3 pink = vec3(1.0, 0.18, 0.62);
	vec3 lime = vec3(0.55, 1.0, 0.2);
	vec3 col = mix(pink, lime, smoothstep(0.62, 0.9, tip));
	// A pale hot core down the middle of each face, a deeper edge: it reads as a solid spike, not a smear.
	float core = 1.0 - smoothstep(0.0, 0.5, abs(fract(UV.x * 8.0) - 0.5));
	col = mix(col * 0.7, mix(col, vec3(1.0, 0.85, 0.95), 0.12), core);
	ALBEDO = col * 1.1;
	ALPHA = clamp(fade, 0.0, 1.0);
}
