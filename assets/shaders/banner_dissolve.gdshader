// The boss title card dissolving: blocks of it vanish in a noisy order, a bright rim glowing on what is about to go.
shader_type canvas_item;

uniform float progress = 0.0;
uniform float block = 5.0;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

void fragment() {
	vec4 c = texture(TEXTURE, UV) * COLOR;
	vec2 cell = floor(FRAGCOORD.xy / block);
	float h = hash(cell) * 0.75 + hash(floor(cell / 6.0)) * 0.25;
	float cut = progress * 1.15;
	float a = step(cut, h);
	float glow = (1.0 - smoothstep(0.0, 0.08, h - cut)) * step(0.001, progress);
	c.rgb = mix(c.rgb, vec3(1.0, 0.95, 0.8), glow * 0.8);
	c.a *= a;
	COLOR = c;
}
