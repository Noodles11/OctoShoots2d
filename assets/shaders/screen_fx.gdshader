// Full-screen pass: vignette, magenta hurt pulse at the edges and a short chromatic wobble (§3.1).
shader_type canvas_item;

uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
uniform float hurt = 0.0;
uniform float aberration = 0.0;
uniform float vignette = 0.35;
uniform vec3 hurt_color : source_color = vec3(1.0, 0.1, 0.65);
// Edge tint for status: cyan while shielded, ink-dark while hidden.
uniform vec3 tint_color : source_color = vec3(0.4, 0.95, 1.0);
uniform float tint = 0.0;

void fragment() {
	vec2 uv = SCREEN_UV;
	vec2 d = uv - 0.5;
	float r = length(d * vec2(1.4, 1.0));
	vec2 off = d * aberration * 0.025 * (1.0 + 0.5 * sin(TIME * 40.0));

	vec3 col;
	col.r = texture(screen_tex, uv + off).r;
	col.g = texture(screen_tex, uv).g;
	col.b = texture(screen_tex, uv - off).b;

	float edge = smoothstep(0.35, 0.9, r);
	col *= 1.0 - edge * vignette;
	col = mix(col, tint_color, edge * clamp(tint, 0.0, 1.0) * 0.6);
	col = mix(col, hurt_color, edge * clamp(hurt, 0.0, 1.0) * 0.8);
	COLOR = vec4(col, 1.0);
}
