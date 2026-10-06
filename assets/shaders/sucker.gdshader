// A tentacle sucker: a raised pale ring around a rosy cup with a dark pit, drawn on top of the world.
// Vertex colour is the paint; COLOR.a is 1 on the ring's crest, fading to 0 off it (for the glint and the glow).
shader_type spatial;
render_mode depth_test_disabled, cull_back;

/// The bubble it holds (0 = empty): the cup flushes a little when it grips one.
instance uniform float holding = 0.0;
uniform float flush = 0.0;

void fragment() {
	vec3 col = COLOR.rgb;
	col = mix(col, vec3(0.5, 0.03, 0.06), flush * 0.4);
	ALBEDO = col;
	float crest = COLOR.a;
	// Soft flesh, not lacquer: only the crest keeps a faint wet sheen.
	ROUGHNESS = mix(0.62, 0.42, crest);
	SPECULAR = 0.3;
	CLEARCOAT = 0.15 * crest;
	CLEARCOAT_ROUGHNESS = 0.4;
	RIM = 0.25;
	RIM_TINT = 0.7;
	BACKLIGHT = vec3(1.0, 0.4, 0.3) * 0.4;
	// The ring reads clearly in murky water: its crest holds a soft glow.
	EMISSION = col * vec3(1.0, 0.85, 0.8) * (0.13 + 0.05 * crest) + vec3(1.0, 0.5, 0.45) * holding * 0.04;
}
