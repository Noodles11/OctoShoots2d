// Cuts the minimap's square render into a disc with a soft antialiased edge.
shader_type canvas_item;

void fragment() {
	vec4 c = texture(TEXTURE, UV);
	float r = length(UV - vec2(0.5)) * 2.0;
	float a = 1.0 - smoothstep(1.0 - fwidth(r) * 1.5, 1.0, r);
	COLOR = vec4(c.rgb, c.a * a);
}
