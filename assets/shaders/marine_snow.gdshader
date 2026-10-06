// Drifting specks of marine snow. Faint in the shade; they glitter where they cross a sunbeam, which makes the
// beams feel like real light in real water.
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, fog_disabled;

#include "res://assets/shaders/sunlight.gdshaderinc"

uniform sampler2D speck : source_color, filter_linear;
uniform vec3 color : source_color = vec3(0.75, 0.9, 1.0);
uniform float fade_far = 9.0;

varying float brightness;
varying float fade;

void vertex() {
	vec3 origin = MODEL_MATRIX[3].xyz;
	float beam = sun_shafts(origin, TIME) * sun_lit(origin);
	float twinkle = 0.7 + 0.3 * sin(TIME * 3.0 + origin.x * 7.0 + origin.z * 5.0);
	brightness = (0.22 + 3.0 * beam * light_strength) * twinkle;
	float d = length((VIEW_MATRIX * vec4(origin, 1.0)).xyz);
	fade = smoothstep(0.2, 0.6, d) * (1.0 - smoothstep(fade_far * 0.5, fade_far, d));
	// Specks in a beam look a little bigger, as they catch the light.
	VERTEX *= 1.0 + beam * 0.8;
}

void fragment() {
	float a = texture(speck, UV).a;
	ALBEDO = color * a * brightness * fade * 0.4;
}
