// The collectible shell (NautilusShell): a round nautilus coated in silver. Fine growth lines run across the whorls,
// wavy, tarnished dark in their grooves and bright on their crests; the older, inner whorls a little duller. The metal
// mirrors a fixed surround of its own (a bright sky, a dark horizon band, warm sand below, a sharp glint), so it reads
// as polished silver in the shallows and in the dark deeps alike, and the ambient water alone never leaves it grey.
// Lying in the reef (shimmer 1; the HUD's counter 0), a band of light sweeps over it now and then and its pearly sheen
// breathes, so it catches the eye from across the screen.
shader_type spatial;
render_mode cull_disabled;

// Growth lines per turn of the coil.
uniform float lines = 64.0;
uniform vec3 silver : source_color = vec3(0.82, 0.82, 0.85);
uniform vec3 tarnish : source_color = vec3(0.36, 0.34, 0.32);
instance uniform float shimmer = 0.0;
instance uniform float shimmer_seed = 0.0;

varying vec3 local_pos;

void vertex() {
	local_pos = VERTEX;
}

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

void fragment() {
	float turn = UV.x;
	float round_ = UV.y;
	// The lines sweep across the tube in a gentle S, as a nautilus's do, and wander a little.
	float sweep = 0.06 * sin(round_ * 6.2831) + 0.015 * noise(vec2(turn * 30.0, round_ * 8.0));
	float g = sin((turn + sweep) * lines * 6.2831);
	float groove = smoothstep(0.7, 1.0, g);
	float crest = smoothstep(0.6, 1.0, -g);
	// A few deeper growth checks, and a fine hairline grain.
	float check = smoothstep(0.92, 1.0, noise(vec2(turn * 7.0, 0.5)));
	float grain = noise(vec2(turn * 220.0, round_ * 40.0));
	float dark = clamp(groove * 0.32 + check * 0.4, 0.0, 1.0);
	vec3 col = mix(silver, tarnish, dark) * (0.9 + 0.12 * grain + 0.08 * crest);
	// The inner whorls (the navel's coil) duller and darker.
	col *= mix(0.65, 1.0, smoothstep(0.6, 2.4, turn));
	// Polished metal shows what it mirrors: a bright sky above, a dark band at the horizon and the warm sand below
	// (a fixed surround, as seen from the eye), so it reads as silver in the shallows and in the dark deeps alike.
	vec3 r = reflect(-VIEW, NORMAL);
	float sky = smoothstep(-0.05, 0.55, r.y);
	float band = 1.0 - smoothstep(0.0, 0.18, abs(r.y + 0.05));
	vec3 env = mix(vec3(0.42, 0.37, 0.32), vec3(1.05, 1.05, 1.08), sky) * (1.0 - 0.55 * band);
	// A sharp glint where it mirrors the sky's brightest part.
	env += vec3(1.2) * smoothstep(0.9, 0.97, r.y) * (1.0 - dark);
	ALBEDO = col * 0.45;
	METALLIC = 0.6;
	ROUGHNESS = 0.2 + 0.3 * dark + 0.06 * grain;
	SPECULAR = 0.8;
	EMISSION = col * env * 0.62 * (1.0 - 0.55 * dark);
	if (shimmer > 0.0) {
		// A band of light sweeping across it every couple of seconds, and a pearly sheen breathing over it.
		float t = fract(TIME * 0.45 + shimmer_seed);
		float band = 1.0 - smoothstep(0.0, 0.12, abs(dot(local_pos, normalize(vec3(1.0, 0.6, 0.3))) * 1.4 - (t * 3.0 - 1.5)));
		float breathe = 0.5 + 0.5 * sin(TIME * 2.6 + shimmer_seed * 6.28);
		vec3 pearl = mix(vec3(1.0, 0.93, 0.78), vec3(0.85, 0.92, 1.0), 0.5 + 0.5 * sin(TIME * 1.3 + local_pos.y * 8.0));
		EMISSION += shimmer * (pearl * (0.25 + 0.2 * breathe) + vec3(1.2, 1.15, 1.05) * band * 1.6 * (1.0 - dark));
	}
}
