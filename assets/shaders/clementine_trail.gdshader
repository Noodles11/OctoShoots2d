// Clementine's tentacles and oral arms: camera-facing ribbons along verlet chains (BellView). Marginal tentacles are
// thin glowing threads beaded with stinging cells; oral arms are a moon jelly's: long thick ribbons, a translucent
// body-coloured blade with a firm central rib, its margins folded into dense crinkled ruffles, coated olive-gold like a
// kelp blade, flecked with small nematocyst warts.
//   UV: x along the strand (0 at the bell → 1 at the tip), y across (0..1). UV2.x: 0 tentacle, 1 oral arm.
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

void fragment() {
	float t = UV.x;
	float s = abs(UV.y * 2.0 - 1.0);
	bool arm = UV2.x > 0.5;
	vec3 col;
	float a;
	if (arm) {
		// The margins: crinkled ruffles, fine folds on coarse ones, rippling down the arm as it swims.
		float coarse = sin(t * 38.0 - TIME * 1.6);
		float fine = sin(t * 140.0 + coarse * 2.0 - TIME * 3.0);
		float edge = 0.8 + 0.12 * coarse + 0.07 * fine;
		a = 1.0 - smoothstep(edge - 0.12, edge, s);
		// Each fold of the ruffle shades darker in its trough.
		float fold = 0.5 + 0.5 * fine;
		// The blade: her body colour, translucent, a firm brighter rib down its middle.
		float rib = 1.0 - smoothstep(0.0, 0.18, s);
		vec3 blade = mix(body, vec3(1.0), 0.3);
		// The kelp coat: olive-gold, thickest on the ruffles, mottled.
		vec3 kelp = vec3(0.5, 0.5, 0.15);
		float mottle = 0.5 + 0.5 * sin(t * 61.0 + s * 9.0) * sin(t * 23.0 - s * 13.0 + 1.3);
		float coat = smoothstep(0.3, 0.75, s) * (0.6 + 0.4 * mottle);
		col = mix(blade, kelp * (0.7 + 0.4 * fold), coat);
		col = mix(col, gold * 1.1, rib * 0.55);
		// Warts of stinging cells, small bright flecks along it.
		float wart = (1.0 - smoothstep(0.08, 0.22, length(vec2(fract(t * 24.0) - 0.5, (s - 0.45) * 2.2)))) * (1.0 - t);
		col = mix(col, gold * 1.3, wart * 0.6);
		// Twisted edge-on, the blade turns its darker side and the ruffles bunch.
		col *= mix(0.72, 1.0, COLOR.r);
		// Thinner and more see-through in the middle of the blade than at its coated margins; fading at the tip.
		a *= mix(0.6, 0.92, max(coat, rib));
		a *= 1.0 - smoothstep(0.82, 1.0, t);
	} else {
		// A thread: a bright core inside a deeper orange edge (it reads on pale sand and in the dark alike), with
		// sparse beads of stinging cells, thinning out toward the tip.
		float bead = (1.0 - smoothstep(0.12, 0.3, abs(fract(t * 11.0 + 0.3) - 0.5))) * (1.0 - smoothstep(0.3, 0.6, s));
		a = 1.0 - smoothstep(0.6, 1.0, s);
		col = mix(body * body * 0.42, body * 1.1, 1.0 - smoothstep(0.45, 0.95, s));
		col = mix(col, gold * 1.25, bead * (1.0 - 0.6 * t));
		a *= 1.0 - 0.6 * t * t;
	}
	col = mix(col, mix(mimic_c1, mimic_c0, t) * 1.15, clamp(mimic, 0.0, 1.0) * 0.8);
	col = mix(col, vec3(1.0, 0.95, 0.85) * 1.5, clamp(burst, 0.0, 1.0) * 0.6);
	col = mix(col, flash * 1.3, hurt * 0.6);
	col = mix(col, vec3(0.2, 0.1, 0.3), ink * 0.5);
	ALBEDO = col * glow;
	float under = 1.0 - smoothstep(bell_radius * 0.8, bell_radius * 1.05, length(v_world.xz - bell_centre.xz));
	ALPHA = clamp(a * COLOR.a * (1.0 - under), 0.0, 1.0);
}
