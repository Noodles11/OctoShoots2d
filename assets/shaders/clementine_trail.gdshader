// Clementine's fringe and oral arm: camera-facing ribbons along verlet chains (BellView). The fringe is a moon jelly's:
// short fine pale hairs, barely there (an accent). The oral arm is a long thick ribbon like withered lace: a faint pale
// blade, its margins crinkled, torn and pierced with holes in a faded kelp tone, almost see-through; down its middle a
// thin wandering core alive with current, a bright impulse shooting down it from the bell to the tip now and then.
//   UV: x along the strand (0 at the bell → 1 at the tip), y across (0..1). UV2.x: 0 fringe hair, 1 oral arm.
//   COLOR.a: the strand's own opacity (hurt and ink are applied through uniforms); COLOR.r on an oral arm: how face-on
//   its twist is there (1 face-on, 0 edge-on).
shader_type spatial;
render_mode unshaded, cull_disabled, blend_mix, depth_draw_never, depth_test_disabled;

uniform float hurt = 0.0;
uniform float ink = 0.0;
uniform float glow = 1.0;
// Her colours (HerColor.cs, data/clementine_color.cfg): body is the bell and tentacles, gold her light.
global uniform vec3 her_body;
global uniform vec3 her_light;
#define body her_body
#define gold her_light
uniform vec3 flash : source_color = vec3(1.0, 0.239, 0.498);
// The absorbing ritual: her strands take on the pearl's colour with her bell (mimic), and flash with the burst.
uniform float mimic = 0.0;
uniform vec3 mimic_c0 : source_color = vec3(1.0);
uniform vec3 mimic_c1 : source_color = vec3(1.0);
uniform float burst = 0.0;
// Her bell (world): the strands are hidden under it, showing only once out past its rim, so none show through it.
uniform vec3 bell_centre = vec3(0.0, -1000.0, 0.0);
uniform float bell_radius = 0.42;
varying vec3 v_world;

void vertex() {
	v_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

float hash2(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float vnoise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash2(i), hash2(i + vec2(1.0, 0.0)), f.x), mix(hash2(i + vec2(0.0, 1.0)), hash2(i + vec2(1.0, 1.0)), f.x), f.y);
}

void fragment() {
	float t = UV.x;
	float s = abs(UV.y * 2.0 - 1.0);
	bool arm = UV2.x > 0.5;
	vec3 col;
	float a;
	if (arm) {
		float twist = COLOR.r;
		// The margins: withered lace — crinkled ruffles, torn ragged here and there, pierced through with holes.
		float coarse = sin(t * 38.0 - TIME * 1.6);
		float fine = sin(t * 140.0 + coarse * 2.0 - TIME * 3.0);
		float tear = smoothstep(0.55, 0.85, vnoise(vec2(t * 24.0, UV.y * 2.0) + 3.0));
		float edge = 0.82 + 0.1 * coarse + 0.05 * fine - 0.35 * tear;
		a = 1.0 - smoothstep(edge - 0.1, edge, s);
		vec2 g = vec2(t * 55.0, UV.y * 7.0);
		vec2 gi = floor(g), gf = fract(g) - 0.5;
		vec2 jit = vec2(hash2(gi), hash2(gi + 7.3)) - 0.5;
		float hole = smoothstep(0.18, 0.3, length(gf - jit * 0.4)) + step(0.55, hash2(gi + 2.1));
		float lace = smoothstep(0.25, 0.45, s);
		a *= mix(1.0, clamp(hole, 0.0, 1.0), lace);
		// The core: a thin line down its middle, wandering a little, alive with current.
		float wander = 0.07 * sin(t * 23.0 + TIME * 1.3) + 0.03 * sin(t * 71.0 - TIME * 2.1);
		float core = 1.0 - smoothstep(0.0, 0.07, abs(UV.y * 2.0 - 1.0 + wander));
		// The blade: her colour, pale and thin; the lace a withered kelp tone, faded, almost see-through.
		vec3 blade = mix(body, vec3(1.0), 0.45);
		vec3 kelp = vec3(0.56, 0.54, 0.38);
		float mottle = 0.5 + 0.5 * sin(t * 61.0 + s * 9.0) * sin(t * 23.0 - s * 13.0 + 1.3);
		col = mix(blade, kelp * (0.8 + 0.3 * mottle), lace);
		col *= mix(0.7, 1.0, twist);
		// Now and then an impulse: a bright spark shot down the core from the bell to the tip, a fading wake behind it,
		// the current flickering as it passes.
		float period = 2.3;
		float cycle = floor(TIME / period);
		float head = (mod(TIME, period) - 0.4 * hash2(vec2(cycle, 3.1))) / 0.42;
		float spark = exp(-pow((t - head) * 16.0, 2.0)) + 0.45 * exp(-(head - t) * 7.0) * step(t, head);
		spark *= step(-0.1, head) * (1.0 - step(1.25, head)) * (0.75 + 0.25 * sin(TIME * 90.0 + t * 40.0));
		float live = 0.35 + 0.15 * sin(TIME * 3.0 + t * 9.0) + spark * 2.2;
		col = mix(col, gold * 1.2 + vec3(0.25, 0.3, 0.35) * spark, core * clamp(live, 0.0, 1.0));
		col += vec3(1.0, 0.97, 0.88) * core * spark * 1.4;
		// See-through: the lace barely there, the blade faint, the core firm; fading at the tip.
		a *= mix(0.42, 0.28, lace);
		a = max(a, core * (0.55 + 0.45 * clamp(spark, 0.0, 1.0)) * (1.0 - smoothstep(0.85, 1.0, t)));
		a *= 1.0 - smoothstep(0.85, 1.0, t);
	} else {
		// A hair: pale, near white with a breath of her colour, a soft glow down its middle, fading toward the tip.
		a = 1.0 - smoothstep(0.35, 1.0, s);
		col = mix(mix(body, vec3(1.0), 0.65), vec3(1.0, 0.97, 0.92), 1.0 - smoothstep(0.0, 0.6, s));
		a *= 1.0 - 0.75 * t;
	}
	col = mix(col, mix(mimic_c1, mimic_c0, t) * 1.15, clamp(mimic, 0.0, 1.0) * 0.8);
	col = mix(col, vec3(1.0, 0.95, 0.85) * 1.5, clamp(burst, 0.0, 1.0) * 0.6);
	col = mix(col, flash * 1.3, hurt * 0.6);
	col = mix(col, vec3(0.2, 0.1, 0.3), ink * 0.5);
	ALBEDO = col * glow;
	float under = 1.0 - smoothstep(bell_radius * 0.8, bell_radius * 1.05, length(v_world.xz - bell_centre.xz));
	ALPHA = clamp(a * COLOR.a * (1.0 - under), 0.0, 1.0);
}
