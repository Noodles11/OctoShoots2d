// Her low health on screen (DESIGN-TOPDOWN §2.5): a dark violet veil that closes in from the screen's edges as her
// health drains, and blurs what lies under it radially, streaked toward the middle of the screen. At danger 1 (critical)
// it covers 70% of the screen, leaving a clear disc round her. Its edge drifts a little (slow noise), so it reads as
// something in the water rather than a mask.
shader_type canvas_item;

uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
uniform float danger = 0.0;   // 0 none → 1 critical
uniform float breath = 0.0;   // −1…1: the slow pulse while she is critical (0 with reduced motion)
uniform vec3 tint : source_color = vec3(0.13, 0.02, 0.2);

float hash(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

void fragment() {
	vec2 uv = SCREEN_UV;
	float aspect = SCREEN_PIXEL_SIZE.y / SCREEN_PIXEL_SIZE.x;
	// Distance from the middle in screen heights (a circle, not the screen's oval).
	vec2 d = (uv - 0.5) * vec2(aspect, 1.0);
	float r = length(d);
	float corner = length(vec2(aspect, 1.0)) * 0.5;
	// The clear disc: just past the corners at no danger, down to 30% of the screen at critical
	// (π r² = 0.3 × aspect at 16:9 → r ≈ 0.41).
	float clear_r = sqrt(0.3 * aspect / 3.14159265);
	float edge = mix(corner + 0.05, clear_r, danger) * (1.0 + 0.035 * breath);
	// A long, gentle gradient: it starts well inside its edge and only thickens far past it.
	float soft_in = mix(0.16, 0.3, danger), soft_out = mix(0.2, 0.36, danger);
	// A slow drift along its edge.
	float a = atan(d.y, d.x);
	edge += (noise(vec2(a * 2.2, TIME * 0.25)) - 0.5) * 0.05 * danger;
	float m = smoothstep(edge - soft_in, edge + soft_out, r);
	m = m * m * (3.0 - 2.0 * m);
	if (m <= 0.002) {
		COLOR = vec4(0.0);
	} else {
		// Radial blur: taps along the line toward the middle, longer and softer further into the veil.
		vec2 toward = uv - 0.5;
		float reach = m * mix(0.025, 0.09, danger);
		float lod = m * mix(1.0, 3.0, danger);
		vec3 sum = vec3(0.0);
		float w = 0.0;
		for (int i = 0; i < 10; i++) {
			float t = float(i) / 9.0;
			float k = 1.0 - 0.6 * t;
			sum += textureLod(screen_tex, uv - toward * reach * t, lod).rgb * k;
			w += k;
		}
		vec3 col = sum / w;
		// The violet dark, thicker the lower her health and the deeper into the veil.
		// Never solid: even at critical the reef still shows faintly through it at the screen's edges.
		float thick = clamp(m * mix(0.4, 0.78, danger) * (1.0 + 0.06 * breath), 0.0, 0.8);
		// Dimmed under it as well, so it stays a dark violet over the bright Shallows too.
		col = mix(col * (1.0 - 0.45 * thick), tint, thick);
		COLOR = vec4(col, m);
	}
}
