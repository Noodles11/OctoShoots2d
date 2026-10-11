// The afterlight a shot of light leaves (docs/LIGHT-BUBBLES.md): a ribbon on the swim layer with a bright thread down
// its middle and a soft glow either side. Vertex COLOR: rgb its colour, a its strength (fading with age); UV.x across
// the ribbon (0..1).
shader_type spatial;
render_mode blend_add, unshaded, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;

varying vec4 v_col;

void vertex() { v_col = COLOR; }

void fragment() {
	float x = UV.x * 2.0 - 1.0;
	float thread = exp(-x * x * 40.0);
	float glow = exp(-x * x * 3.5) * 0.35;
	ALBEDO = v_col.rgb * (thread * 1.8 + glow) + vec3(1.0, 0.97, 0.9) * thread * v_col.a * 0.6;
	ALPHA = clamp((thread + glow) * v_col.a, 0.0, 1.0);
}
