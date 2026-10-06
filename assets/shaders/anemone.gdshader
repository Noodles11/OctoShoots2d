// Sea anemones: soft tentacles that sway in the current, glowing at the tips.
// Per instance (MultiMesh custom data): rgb = body colour, a = 1 for a nest anemone.
shader_type spatial;
render_mode cull_disabled;

uniform float sway = 0.16;
uniform vec3 tip_color : source_color = vec3(1.0, 0.85, 0.95);

varying vec3 body_color;
varying float is_nest;

void vertex() {
	float t = COLOR.r;
	vec3 origin = MODEL_MATRIX[3].xyz;
	float phase = origin.x * 0.37 + origin.z * 0.53;
	float scale = length(MODEL_MATRIX[0].xyz);
	float w1 = sin(TIME * 1.2 + phase + VERTEX.y * 2.5);
	float w2 = sin(TIME * 0.9 + phase * 1.3 + VERTEX.y * 2.0 + 1.7);
	VERTEX.x += w1 * sway * t * t;
	VERTEX.z += w2 * sway * t * t;
	body_color = INSTANCE_CUSTOM.rgb;
	is_nest = INSTANCE_CUSTOM.a;
}

void fragment() {
	float part = COLOR.b;
	float glow = COLOR.g;
	vec3 col = mix(body_color, tip_color, smoothstep(0.25, 1.0, glow) * (0.55 + 0.25 * is_nest));
	if (part > 0.9) {
		col = vec3(0.02, 0.0, 0.03);
	} else if (part > 0.4) {
		col = body_color * 0.45;
	}
	ALBEDO = col;
	ROUGHNESS = 0.55;
	SPECULAR = 0.3;
	EMISSION = col * (0.12 + (0.9 + 0.6 * is_nest) * glow * glow) * (part > 0.4 ? 0.0 : 1.0);
}
