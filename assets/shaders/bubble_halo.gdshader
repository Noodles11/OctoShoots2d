// A light bubble's glow in the water: a soft additive disc that always faces the camera, drawn round each of
// Clementine's bubbles (docs/LIGHT-BUBBLES.md). Its size is fixed by the quad (about 0.8 m radius, never grown by
// merging); `strength` (≤ 0.4 of her halo) and `color` carry the bubble's brightness and warmth.
shader_type spatial;
render_mode blend_add, unshaded, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;

instance uniform float strength = 0.0;
instance uniform vec3 color = vec3(1.0, 0.769, 0.420);

void vertex() {
	// Billboard, keeping the node's scale.
	MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
	MODELVIEW_MATRIX = MODELVIEW_MATRIX * mat4(
		vec4(length(MODEL_MATRIX[0].xyz), 0.0, 0.0, 0.0),
		vec4(0.0, length(MODEL_MATRIX[1].xyz), 0.0, 0.0),
		vec4(0.0, 0.0, length(MODEL_MATRIX[2].xyz), 0.0),
		vec4(0.0, 0.0, 0.0, 1.0));
}

void fragment() {
	float d = length(UV - 0.5) * 2.0;
	// A Gaussian glow with a slightly brighter inner bloom, fading to nothing at the quad's edge.
	float a = (exp(-d * d * 5.0) + 0.35 * exp(-d * d * 18.0)) * (1.0 - smoothstep(0.75, 1.0, d));
	ALBEDO = color;
	ALPHA = clamp(a * strength, 0.0, 1.0);
}
