// Clownfish (and the ninja's headband): the body wags from head to tail, ribbons flutter.
// Vertex colour is the paint; COLOR.a flags parts: 1 body, 0.75 headband (glows in a wind-up), 0.4 ribbons.
shader_type spatial;
render_mode cull_disabled;

instance uniform float phase = 0.0;
instance uniform float swim = 0.0;
instance uniform float glow = 0.0;
instance uniform float flash = 0.0;
instance uniform vec3 tint : source_color = vec3(1.0);
instance uniform float tint_amount = 0.0;

void vertex() {
	float t = clamp((VERTEX.z + 0.1) / 0.6, 0.0, 1.0);
	float rate = 5.0 + swim * 8.0;
	float amp = 0.015 + 0.05 * swim;
	VERTEX.x += sin(TIME * rate + phase - VERTEX.z * 9.0) * amp * t * t;
	// A little counter-sway at the head.
	VERTEX.x -= sin(TIME * rate + phase) * amp * 0.25 * (1.0 - t);
	if (COLOR.a < 0.6) {
		float k = max(VERTEX.z + 0.1, 0.0);
		VERTEX.x += sin(TIME * 7.0 + phase - VERTEX.z * 18.0) * 0.03 * k;
		VERTEX.y += sin(TIME * 5.0 + phase * 1.7 - VERTEX.z * 14.0) * 0.02 * k;
	}
}

void fragment() {
	vec3 col = mix(COLOR.rgb, tint, tint_amount);
	ALBEDO = col;
	ROUGHNESS = 0.4;
	SPECULAR = 0.45;
	RIM = 0.3;
	RIM_TINT = 0.4;
	// A little self-glow keeps the orange reading as orange in murky water.
	// (the headband and ribbons stay dark: only the body gets the glow)
	vec3 emit = vec3(flash) + col * (COLOR.a > 0.9 ? 0.28 : 0.03);
	if (COLOR.a < 0.9 && COLOR.a > 0.6) {
		emit += vec3(1.0, 0.18, 0.1) * glow * 2.5;
	}
	emit += tint * tint_amount * 0.25;
	EMISSION = emit;
}
