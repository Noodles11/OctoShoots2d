// The sea surface seen from below: bright, rippling sunlight.
shader_type spatial;
render_mode unshaded, cull_disabled, blend_mix, depth_draw_never;

uniform vec3 deep : source_color = vec3(0.25, 0.65, 0.75);
uniform vec3 sun : source_color = vec3(0.85, 1.0, 1.0);
uniform vec3 mist_color : source_color = vec3(0.1, 0.32, 0.38);
uniform float mist_start = 14.0;
uniform float mist_end = 60.0;

varying vec3 world_pos;

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec2 p = world_pos.xz * 0.18;
	float w = sin(p.x * 2.1 + TIME * 0.9) * sin(p.y * 1.7 - TIME * 0.7)
	        + 0.5 * sin((p.x + p.y) * 3.3 + TIME * 1.3);
	float glint = smoothstep(0.6, 1.4, w);
	// Drawn after the mist pass, so it fades into the mist itself (a little further out: it is bright).
	float f = smoothstep(mist_start, mist_end, length(VERTEX)); // VERTEX is view-space here
	ALBEDO = mix(mix(deep, sun, 0.25 + 0.5 * glint), mist_color, f);
	ALPHA = 0.85;
}
