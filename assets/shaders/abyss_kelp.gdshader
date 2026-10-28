// The abyss's kelp (AbyssView, docs/ADVENTURE.md §5): tall strands rising from the abyss floor far below her plane,
// drawn with the reef, so the water's pass blurs and sinks them into the deep like the rest. One ribbon per strand,
// still in its mesh: here it sways, a slow swell rolling through the field (neighbours lean together) and each strand
// a little out of step, the more toward its tip. Its outline is fronds, alternating up either side.
// Vertex colour: r a per-strand seed, g its brightness, a how far along it (0 root → 1 tip). UV: x across (0 → 1),
// y along it in metres. UV2: x the strand's length.
shader_type spatial;
render_mode cull_disabled;

uniform vec3 base_col : source_color = vec3(0.12, 0.14, 0.08);
uniform vec3 tip_col : source_color = vec3(0.3, 0.36, 0.16);

varying float along;

void vertex() {
	along = COLOR.a;
	float seed = COLOR.r;
	float bend = along * along * UV2.x;
	vec2 at = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz;
	float swell = sin(TIME * 0.3 + dot(at, vec2(0.11, 0.07)) + seed * 0.8);
	float cross_swell = cos(TIME * 0.24 + dot(at, vec2(0.05, 0.12)) + seed * 1.7);
	float own = sin(TIME * 0.65 + seed * 6.283 + along * 2.4);
	VERTEX.x += (swell * 0.09 + own * 0.03) * bend;
	VERTEX.z += (cross_swell * 0.06 + own * 0.02) * bend;
	// The blades face the camera, a little rounded across.
	NORMAL = normalize((inverse(MODEL_MATRIX) * vec4(INV_VIEW_MATRIX[2].xyz, 0.0)).xyz);
}

void fragment() {
	float across = UV.x * 2.0 - 1.0;
	// Fronds: lobes up either side, out of step with each other.
	float side = across > 0.0 ? 1.7 : 0.0;
	float frond = 0.5 + 0.5 * sin(UV.y * 3.4 + 0.8 * sin(UV.y * 1.3 + COLOR.r * 7.0) + COLOR.r * 40.0 + side);
	float margin = 0.42 + 0.5 * frond * smoothstep(0.04, 0.2, along);
	if (abs(across) > margin) discard;
	vec3 col = mix(base_col, tip_col, smoothstep(0.1, 1.0, along));
	col *= 0.78 + 0.36 * COLOR.g;
	// The stipe down its middle a shade darker, the fronds paler toward their tips.
	col *= 0.85 + 0.25 * smoothstep(0.1, 0.8, abs(across));
	ALBEDO = col;
	ROUGHNESS = 0.85;
	SPECULAR = 0.1;
	BACKLIGHT = col * 0.4;
}
