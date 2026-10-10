// The corruption's outgrowth (docs/CORRUPTION.md): ink blades on the floor, one MultiMesh instance each, drawn from
// the level's corruption field (R density, G the gold of dying). Thick ground grows tall glossy blades that sway in
// the current and recoil from her light; as the ground is cleansed they shrivel, curl, blacken to ash and dissolve,
// their tips burning gold as they go: light spreading, never erasing. Instance custom: (seed, u, v, unused).
shader_type spatial;
render_mode cull_disabled, world_vertex_coords, shadows_disabled;

uniform sampler2D corruption_map : filter_linear, repeat_disable;
/// Her bell (world), and how far her light pushes the blades back.
uniform vec3 her_pos = vec3(0.0, -1000.0, 0.0);
uniform float her_reach = 3.4;
uniform vec3 ink : source_color = vec3(0.014, 0.010, 0.022);
uniform vec3 sheen : source_color = vec3(0.32, 0.10, 0.55);
uniform vec3 gold : source_color = vec3(1.0, 0.72, 0.32);

varying float v_grow;
varying float v_burn;
varying float v_seed;
varying float v_t;

float hash(float n) { return fract(sin(n * 127.1) * 43758.5453); }

void vertex() {
	vec4 c = INSTANCE_CUSTOM;
	vec2 fr = textureLod(corruption_map, c.yz, 0.0).rg;
	float grow = smoothstep(0.03, 0.6, fr.r);
	v_grow = grow;
	v_burn = fr.g;
	v_seed = c.x;
	float t = UV.y;
	v_t = t;
	vec3 base = MODEL_MATRIX[3].xyz;
	vec3 rel = VERTEX - base;
	if (fr.r < 0.015 && fr.g < 0.02) {
		// Gone: collapsed to a point (no fragments).
		VERTEX = base;
	} else {
		// Shrivel: a dying blade shortens and thins, but keeps a stub while it burns.
		float h = mix(0.18, 1.0, grow);
		rel.y *= h;
		rel.xz *= mix(0.55, 1.0, grow);
		// Sway: the current, slow and broad, with a quicker shiver; each blade its own phase.
		float ph = c.x * 6.283;
		vec2 flow = vec2(sin(TIME * 0.9 + base.x * 0.21 + base.z * 0.13 + ph), cos(TIME * 0.7 + base.z * 0.19 + ph * 0.7)) * 0.16
			+ vec2(sin(TIME * 3.3 + ph * 3.0), cos(TIME * 2.9 + ph * 2.0)) * 0.03;
		// Recoil: blades bend away from her light, the nearer the harder.
		vec2 away = base.xz - her_pos.xz;
		float d = length(away);
		float push = (1.0 - smoothstep(0.0, her_reach, d)) * step(her_pos.y - 6.0, base.y + 0.01);
		flow += (d > 1e-3 ? away / d : vec2(0.0)) * push * push * 0.75;
		// Dying, they curl over and droop toward the floor.
		float curl = (1.0 - grow) * 0.9;
		float bend = t * t;
		vec2 dir = normalize(vec2(cos(ph), sin(ph)) + 1e-4);
		rel.xz += (flow + dir * curl) * bend * (0.6 + 0.4 * grow);
		rel.y -= curl * bend * 0.35;
		VERTEX = base + rel;
	}
}

void fragment() {
	// Dissolve: holes eat the blade as it dies, from the tip down.
	float n = hash(floor(UV.y * 9.0) + v_seed * 91.7) * 0.6 + hash(UV.x * 3.0 + v_seed * 13.1) * 0.4;
	float eat = (1.0 - v_grow) * (0.55 + 0.6 * v_t) - v_burn * 0.25;
	ALPHA = n > eat ? 1.0 : 0.0;
	ALPHA_SCISSOR_THRESHOLD = 0.5;

	vec3 nrm = FRONT_FACING ? NORMAL : -NORMAL;
	NORMAL = nrm;
	float facing = clamp(dot(nrm, VIEW), 0.0, 1.0);
	float fres = pow(1.0 - facing, 3.0);
	// Ink, slightly glossy; dying, it ashes to a warm brown before it goes.
	vec3 col = mix(ink, vec3(0.16, 0.09, 0.05), (1.0 - v_grow) * 0.6);
	ALBEDO = col;
	ROUGHNESS = mix(0.16, 0.5, 1.0 - v_grow);
	SPECULAR = 0.75;
	// Light-drinking: only a cold violet sheen at the rim and a faint pulse up the tips, readable from far.
	float pulse = 0.6 + 0.4 * sin(TIME * 1.3 + v_seed * 20.0);
	vec3 e = sheen * fres * 0.35 * v_grow + sheen * pow(v_t, 4.0) * 0.18 * pulse * v_grow;
	// The gold of dying, brightest at the tips.
	e += gold * v_burn * (0.35 + 1.8 * v_t * v_t);
	EMISSION = e;
}
