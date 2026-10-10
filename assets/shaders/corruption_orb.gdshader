// Corrupted bodies (docs/CORRUPTION.md): a murkling, a Blightroot's ink sac, a murkling's throw, the boss's crust. A
// wobbling glossy membrane of darkness with a cold violet rim; inside, a dim stolen ember (ember 0 for none).
// swell inflates it (a sac's telegraph); fade dissolves it (a burst).
shader_type spatial;
render_mode cull_back, shadows_disabled;

instance uniform float ember = 0.4;
instance uniform float wobble = 1.0;
instance uniform float seed = 0.0;
instance uniform float swell = 0.0;
instance uniform float fade = 1.0;
uniform vec3 ember_color : source_color = vec3(1.0, 0.42, 0.16);
uniform vec3 rim_color : source_color = vec3(0.34, 0.09, 0.6);

varying vec3 v_dir;

void vertex() {
	v_dir = VERTEX;
	vec3 p = normalize(VERTEX);
	float t = TIME;
	float d = sin(t * 3.1 + seed * 5.1) * p.x * p.z * 1.1 + sin(t * 2.3 + seed * 2.7) * (p.x * p.x - p.z * p.z) * 0.8
		+ sin(t * 4.1 + seed * 1.3) * p.y * p.x * 0.7 + sin(t * 1.7 + seed) * (3.0 * p.y * p.y - 1.0) * 0.3;
	VERTEX += NORMAL * (d * 0.09 * wobble + swell * 0.18);
}

float hash3(vec3 p) { return fract(sin(dot(p, vec3(12.9898, 78.233, 37.719))) * 43758.5453); }

void fragment() {
	// A burst eats it away in specks.
	if (fade < 1.0 && hash3(floor(v_dir * 9.0) + seed) > fade) discard;
	float facing = clamp(dot(NORMAL, VIEW), 0.0, 1.0);
	float fres = pow(1.0 - facing, 2.4);
	vec3 n = (INV_VIEW_MATRIX * vec4(NORMAL, 0.0)).xyz;
	float swirl = 0.5 + 0.5 * sin(n.x * 7.0 + n.z * 5.0 + TIME * 1.6 + seed);
	ALBEDO = vec3(0.008, 0.006, 0.014) + vec3(0.02, 0.005, 0.03) * swirl;
	ROUGHNESS = 0.1;
	SPECULAR = 0.85;
	float flicker = 0.7 + 0.3 * sin(TIME * 7.0 + seed * 3.0) * sin(TIME * 4.3 + seed);
	EMISSION = rim_color * fres * (0.55 + 0.3 * swirl + swell * 0.8) + ember_color * pow(facing, 6.0) * ember * flicker * 1.4;
}
