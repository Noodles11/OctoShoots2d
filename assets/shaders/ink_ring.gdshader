// A ring on the water (docs/CORRUPTION.md): a Blightroot's pulse of ink, a murkling budding, a burst's wave of
// cleansing (gold). A flat quad; the ring is a soft band at radius 0.8 of it, torn into wisps; tint.rgb its colour
// (over 1 to bloom), tint.a its strength. width: the band's width as a share of the radius.
shader_type spatial;
render_mode blend_mix, unshaded, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;

instance uniform vec4 tint = vec4(0.02, 0.01, 0.04, 0.8);
instance uniform float width = 0.18;
instance uniform float seed = 0.0;

void fragment() {
	vec2 p = (UV - 0.5) * 2.0;
	float r = length(p);
	float a = atan(p.y, p.x);
	float wisp = 0.7 + 0.3 * sin(a * 9.0 + seed * 5.0 + TIME * 1.5) * sin(a * 4.0 - TIME * 0.9 + seed);
	float band = 1.0 - smoothstep(0.0, width, abs(r - 0.8));
	float inner = smoothstep(0.0, 0.8, r) * 0.18;
	ALBEDO = tint.rgb;
	ALPHA = clamp((band * wisp + inner * band) * tint.a, 0.0, 1.0) * (1.0 - smoothstep(0.92, 1.0, r));
}
