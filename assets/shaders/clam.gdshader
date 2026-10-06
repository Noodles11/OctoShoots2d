// The giant clam that holds an item pearl. Vertex alpha picks the part:
//   1.0 outer shell (rgb = paint), 0.0 mother-of-pearl inside, 0.5 the living mantle along the lip.
// On the inside, COLOR.r = distance from the middle (0) to the lip (1) and COLOR.g = angle around (0–1).
shader_type spatial;
render_mode cull_back;

uniform float open = 0.0;
uniform vec3 glow_color : source_color = vec3(0.6, 0.9, 1.0);
/// Shop clams are gilded.
uniform float gold = 0.0;

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }

float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

void vertex() {
	// The mantle breathes while the clam is open.
	if (COLOR.a > 0.3 && COLOR.a < 0.7) {
		float t = COLOR.r;
		float th = COLOR.g * TAU;
		VERTEX += NORMAL * (0.012 + 0.018 * sin(TIME * 1.6 + th * 9.0)) * open * smoothstep(0.66, 0.8, t);
	}
}

void fragment() {
	float a = COLOR.a;
	float fres = 1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0);
	if (a > 0.9) {
		// Ribbed outer shell: chalky cream with growth bands, or gilded for the shop.
		vec3 col = mix(COLOR.rgb, COLOR.rgb * vec3(1.15, 0.85, 0.35) + vec3(0.25, 0.15, 0.0), gold);
		ALBEDO = col;
		ROUGHNESS = mix(0.7, 0.22, gold);
		METALLIC = gold * 0.85;
		SPECULAR = 0.45;
		RIM = 0.35;
		RIM_TINT = 0.5;
		EMISSION = col * 0.04;
	} else if (a < 0.2) {
		// Mother-of-pearl: white with a rainbow sheen that shifts with the angle.
		float t = COLOR.r;
		vec3 irid = 0.5 + 0.5 * cos(TAU * (fres * 1.3 + t * 0.9 + noise(vec2(COLOR.g * 40.0, t * 8.0)) * 0.4 + vec3(0.0, 0.33, 0.67)));
		vec3 col = mix(vec3(0.84, 0.82, 0.88), irid, 0.45);
		col = mix(col, vec3(1.0, 0.85, 0.45), gold * 0.3);
		ALBEDO = col;
		METALLIC = 0.3;
		ROUGHNESS = 0.1;
		CLEARCOAT = 1.0;
		CLEARCOAT_ROUGHNESS = 0.05;
		// The pearl lights its bed.
		EMISSION = col * 0.05 + glow_color * open * 0.12 * (1.0 - t * 0.6);
	} else {
		// The living mantle of a giant clam: electric blue and turquoise waves, violet edge, bright eye-spots.
		float t = COLOR.r;
		float th = COLOR.g * TAU;
		vec2 p = vec2(th * 22.0, t * 20.0);
		float waves = sin(p.x + sin(p.y * 1.3 + TIME * 0.5) * 1.6 + p.y * 0.7 + noise(p * 0.5) * 2.0);
		vec3 deep = vec3(0.01, 0.08, 0.38);
		vec3 bright = vec3(0.0, 0.7, 0.85);
		vec3 violet = vec3(0.38, 0.08, 0.8);
		vec3 col = mix(deep, bright, smoothstep(-0.3, 0.7, waves));
		col = mix(col, violet, smoothstep(0.86, 0.98, t) * 0.7);
		vec2 g = fract(p * 0.55) - 0.5;
		float spot = (1.0 - smoothstep(0.07, 0.13, length(g))) * step(0.55, hash(floor(p * 0.55)));
		col = mix(col, vec3(0.85, 1.0, 0.55), spot);
		col += vec3(0.4, 0.2, 0.9) * pow(fres, 3.0) * 0.25;
		ALBEDO = col;
		ROUGHNESS = 0.45;
		SPECULAR = 0.3;
		CLEARCOAT = 0.3;
		float shimmer = 0.75 + 0.25 * sin(TIME * 2.0 + th * 6.0);
		EMISSION = col * (0.15 + 0.45 * open * shimmer) + vec3(0.85, 1.0, 0.55) * spot * (0.25 + 0.5 * open);
	}
}
