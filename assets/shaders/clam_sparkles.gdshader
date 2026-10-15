// The clam room's gold motes: camera-facing sparks spiralling round the pearl, each on its own climbing helix (wide
// at the bottom, drawing in as it rises, then back to the bottom), twinkling. `gone` (0 → 1) dissolves them when the
// pearl is taken: they fling outward and fade. Instance custom: (phase, speed, height, twinkle seed).
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled, world_vertex_coords;

uniform vec3 gold : source_color = vec3(1.0, 0.8, 0.38);
uniform float gone = 0.0;
uniform float spread = 1.6;

varying float v_alpha;

void vertex() {
	vec4 c = INSTANCE_CUSTOM;
	vec3 centre = MODEL_MATRIX[3].xyz;
	float size = length(MODEL_MATRIX[0].xyz);
	// Climbing: its place on the helix (0 bottom → 1 top), looping.
	float k = fract(TIME * c.y * 0.12 + c.x);
	float turn = c.x * 6.2831 + k * 6.2831 * 2.2 + TIME * 0.5;
	float r = spread * (1.0 - 0.65 * k) * (1.0 + 2.5 * gone * gone);
	float y = -0.5 + k * c.z + gone * 0.8;
	vec3 at = centre + vec3(cos(turn) * r, y, sin(turn) * r);
	// Facing the camera.
	vec3 right = INV_VIEW_MATRIX[0].xyz, up = INV_VIEW_MATRIX[1].xyz;
	vec2 corner = (UV - 0.5) * 2.0;
	float twinkle = 0.55 + 0.45 * sin(TIME * (6.0 + 6.0 * c.w) + c.w * 40.0);
	VERTEX = at + (right * corner.x + up * corner.y) * size * (0.6 + 0.5 * twinkle);
	// In and out of each loop, and fading as they dissolve.
	v_alpha = twinkle * smoothstep(0.0, 0.12, k) * (1.0 - smoothstep(0.85, 1.0, k)) * (1.0 - gone);
}

void fragment() {
	// A soft round spark with a hot core and a little four-point glint.
	vec2 q = (UV - 0.5) * 2.0;
	float d = length(q);
	float core = exp(-d * d * 9.0);
	float star = max(0.0, 1.0 - abs(q.x) * 6.0) * max(0.0, 1.0 - abs(q.y) * 1.2) + max(0.0, 1.0 - abs(q.y) * 6.0) * max(0.0, 1.0 - abs(q.x) * 1.2);
	float a = (core + star * 0.35) * v_alpha;
	if (a < 0.003) discard;
	ALBEDO = mix(gold, vec3(1.0, 0.97, 0.85), core) * 2.2;
	ALPHA = clamp(a, 0.0, 1.0);
}
