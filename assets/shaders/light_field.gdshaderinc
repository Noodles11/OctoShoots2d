// Clementine's light in the water (DESIGN-TOPDOWN §5.3), shared by every shader that shows where it reaches.
//
// Visibility: the reef is a heightfield, so her light reaches a point exactly when the slope from the light up to the
// point clears the steepest slope of the terrain on the way. HorizonMap.cs keeps that running steepest slope for 256
// directions × 32 distances round her light; her_vis() tests a point with a single lookup, soft at the edge.
//
// In-scatter: the water glows where her light passes through it. For a point light the light scattered toward the eye
// along a view ray is ∫ I / d(t)² dt, which has a closed form: (I / h) · (θb − θa), h the ray's closest approach to
// the light and θ the angle seen from the light. Samples spaced evenly in that angle (equiangular sampling) carry equal
// weight, so the unshadowed glow is exact and noise-free, and the few samples only have to find the shadows: the walls,
// boulders and rises that block her light cut dark shafts into the glowing water.
//
// Her light is a searchlight (BellView): a cone from her bell (her_spot: its direction, and the cosine of its half-angle;
// w below −1 for a lamp all round), softly edged, with a little glow round the bell itself.
//
// Globals (project.godot [shader_globals], set each frame by TopDownMain):
//   her_light_at   her light (world xyz) and how far it reaches (w)
//   her_spot       the searchlight's direction (xyz) and the cosine of its half-angle (w)
//   her_horizon    the horizon map (R: running steepest slope; x the direction, y the distance)
//   her_scatter    how strongly the water scatters her light (0 none)

global uniform vec4 her_light_at;
global uniform sampler2D her_horizon : filter_linear, repeat_enable;
global uniform float her_scatter;
global uniform vec4 her_spot;

// 1 inside her searchlight's beam, soft at its edge (and a little round the bell itself), 0 outside it.
float her_cone(vec3 p) {
	if (her_spot.w < -1.0) return 1.0;
	vec3 lq = p - her_light_at.xyz;
	float d = length(lq);
	float c = dot(lq / max(d, 1e-3), her_spot.xyz);
	float cone = smoothstep(her_spot.w - 0.045, her_spot.w + 0.02, c);
	float near = 1.0 - smoothstep(0.8, 2.2, d);
	return max(cone, near * 0.45);
}

// 1 where her light reaches p, 0 behind the reef, soft over the edge.
float her_vis(vec3 p) {
	vec2 d = p.xz - her_light_at.xz;
	float r = length(d);
	if (r < 0.9) return her_cone(p);
	float u = atan(d.y, d.x) / 6.2831853 + 0.5;
	// The terrain short of the point itself (its own column would shade it).
	float v = clamp((r - 0.8) / her_light_at.w - 0.5 / 32.0, 0.0, 1.0 - 0.5 / 32.0);
	float horizon = texture(her_horizon, vec2(u, v)).r;
	float slope = (p.y - her_light_at.y) / r;
	return smoothstep(-0.14, 0.04, slope - horizon) * her_cone(p);
}

// How strongly her light falls on p (0..1): its reach, softening with distance, and only where it gets to.
float her_reach(vec3 p) {
	if (her_light_at.w <= 0.0) return 0.0;
	float d = distance(p, her_light_at.xyz);
	float fall = clamp(1.0 - d * d / (her_light_at.w * her_light_at.w), 0.0, 1.0);
	return fall * fall / (1.0 + d * d * 0.05) * her_vis(p);
}

// Her searchlight's beam in the water: the view ray's stretch inside the cone, sampled evenly along it (the glow must
// carry down the whole shaft, not only round the bell), fading with distance from her and cut by what shades it.
float her_beam(vec3 o, vec3 dir, float t0, float t1, float jitter) {
	if (her_spot.w < -1.0 || her_scatter <= 0.0) return 0.0;
	vec3 l = her_light_at.xyz;
	float reach = her_light_at.w;
	float tc = dot(l - o, dir);
	float h = length(o + dir * tc - l);
	float disc = reach * reach - h * h;
	if (disc <= 0.0) return 0.0;
	float s = sqrt(disc);
	float ta = max(tc - s, t0), tb = min(tc + s, t1);
	if (tb <= ta) return 0.0;
	const int N = 14;
	float sum = 0.0;
	for (int i = 0; i < N; i++) {
		vec3 q = o + dir * mix(ta, tb, (float(i) + jitter) / float(N));
		vec3 lq = q - l;
		float d = length(lq);
		float c = dot(lq / max(d, 1e-3), her_spot.xyz);
		float cone = smoothstep(her_spot.w - 0.03, her_spot.w + 0.03, c);
		// Brightest along the beam's axis, a soft core.
		float axis = smoothstep(her_spot.w, 1.0, c);
		float fall = 1.0 - d / reach;
		sum += cone * (0.55 + 0.45 * axis) * fall * fall * her_vis(q);
	}
	return her_scatter * (tb - ta) * sum / float(N);
}

// Where her searchlight falls on p (0..1): inside its cone, within its reach, where it gets to.
float her_spot_on(vec3 p) {
	if (her_spot.w < -1.0 || her_light_at.w <= 0.0) return 0.0;
	vec3 lq = p - her_light_at.xyz;
	float d = length(lq);
	float c = dot(lq / max(d, 1e-3), her_spot.xyz);
	float fall = clamp(1.0 - d / her_light_at.w, 0.0, 1.0);
	return smoothstep(her_spot.w - 0.02, her_spot.w + 0.04, c) * fall * her_vis(p);
}

// Henyey-Greenstein, normalised so isotropic scattering is 1 (water scatters mostly forward).
float lf_phase(float cos_theta, float g) {
	float k = 1.0 + g * g - 2.0 * g * cos_theta;
	return (1.0 - g * g) / (k * sqrt(k));
}

// Her light scattered toward the eye along the ray o + t·dir for t in [t0, t1]: equiangular samples, jittered by
// `jitter` (0..1 per pixel). Returns a strength (multiply by her colour).
float her_inscatter(vec3 o, vec3 dir, float t0, float t1, float jitter) {
	if (her_scatter <= 0.0) return 0.0;
	vec3 l = her_light_at.xyz;
	float reach = her_light_at.w;
	float tc = dot(l - o, dir);
	float h = length(o + dir * tc - l);
	float disc = reach * reach - h * h;
	if (disc <= 0.0) return 0.0;
	float s = sqrt(disc);
	float ta = max(tc - s, t0), tb = min(tc + s, t1);
	if (tb <= ta) return 0.0;
	// Never closer than her bell (the bell itself is drawn there): the glow has a soft heart, not a burning point.
	h = max(h, 1.1);
	float a = atan((ta - tc) / h), b = atan((tb - tc) / h);
	const int N = 10;
	float sum = 0.0;
	for (int i = 0; i < N; i++) {
		float th = mix(a, b, (float(i) + jitter) / float(N));
		vec3 q = o + dir * (tc + h * tan(th));
		vec3 lq = q - l;
		float d = length(lq);
		float fall = 1.0 - d * d / (reach * reach);
		sum += her_vis(q) * fall * fall * lf_phase(dot(lq / max(d, 1e-3), -dir), 0.25);
	}
	return her_scatter * (b - a) / h * sum / float(N);
}
