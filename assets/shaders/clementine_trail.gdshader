// Clementine's strands: camera-facing ribbons along verlet chains (BellView), three kinds, as a moon jelly's.
//   Fringe hairs: short fine pale hairs round the margin, barely there (an accent).
//   Oral arms: long ribbons of thin cloth twisting freely in the water — nearly see-through, their ruffled margins
//   glowing, a faint current down their middle with a brighter impulse now and then.
//   Fibre tentacles: long and hair-thin, like optical fibres — a glowing thread with beads of light running down it
//   to a tip twinkling in random colours.
//   UV: x along the strand (0 at the bell → 1 at the tip), y across (0..1). UV2.x: 0 fringe, 1 oral arm, 2 fibre;
//   UV2.y: the strand's own seed (0..1).
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
// Her bell (world): under it the strands show only faintly, through its glass.
uniform vec3 bell_centre = vec3(0.0, -1000.0, 0.0);
uniform float bell_radius = 0.42;
varying vec3 v_world;

void vertex() {
	v_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

vec3 hue(float h) {
	return clamp(abs(fract(h + vec3(0.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0) - 1.0, 0.0, 1.0);
}

float hash1(float n) { return fract(sin(n * 127.1) * 43758.5453); }

void fragment() {
	float t = UV.x;
	float s = abs(UV.y * 2.0 - 1.0);
	float kind = UV2.x;
	float seed = UV2.y;
	vec3 col;
	float a;
	if (kind > 1.5) {
		// A fibre: a glowing thread, beads of light running down it, a bright tip.
		a = 1.0 - smoothstep(0.15, 1.0, s);
		col = mix(body, vec3(1.0, 0.95, 0.92), 0.4) * 1.15;
		float run = fract(t * 2.2 - TIME * 0.55 + seed * 7.0);
		float bead = exp(-pow((run - 0.5) * 18.0, 2.0));
		float tip = smoothstep(0.88, 1.0, t);
		col = mix(col, vec3(1.0, 0.98, 0.95) * 1.5, bead * 0.8);
		// Its tip twinkles in colours, as an optical fibre's: a new random hue every moment, flaring and dimming.
		float beat = floor(TIME * 2.5 + seed * 13.0);
		vec3 shade = mix(hue(hash1(beat + seed * 91.0)), hue(hash1(beat + 1.0 + seed * 91.0)), smoothstep(0.6, 1.0, fract(TIME * 2.5 + seed * 13.0)));
		float flare = 0.55 + 0.45 * sin(TIME * (9.0 + 6.0 * seed) + seed * 30.0);
		col = mix(col, mix(shade, vec3(1.0), 0.25) * (1.4 + 0.6 * flare), tip);
		a *= 0.2 * (1.0 - 0.5 * t) + 0.45 * bead + tip * (0.5 + 0.45 * flare);
	} else if (kind > 0.5) {
		float twist = COLOR.r;
		// Thin cloth: ruffled margins (coarse folds and fine crinkles, rippling down it as it swims), see-through.
		float coarse = sin(t * 30.0 - TIME * 1.4 + seed * 6.0);
		float fine = sin(t * 110.0 + coarse * 2.0 - TIME * 2.6);
		float edge = 0.86 + 0.08 * coarse + 0.05 * fine;
		float cloth = 1.0 - smoothstep(edge - 0.08, edge, s);
		// Its margins glow (the cloth catches her light at its folds), its middle a faint veil.
		float rim = smoothstep(edge - 0.24, edge - 0.05, s) * cloth;
		vec3 veil = mix(body, vec3(1.0, 0.85, 0.9), 0.2);
		col = mix(veil * 0.9, mix(body, vec3(1.0, 0.95, 0.9), 0.35) * 1.35, rim);
		col *= mix(0.6, 1.0, twist);
		a = cloth * (0.12 + 0.55 * rim * (0.6 + 0.4 * (0.5 + 0.5 * fine)));
		// A faint current down its middle, a brighter impulse shooting down it now and then.
		float wander = 0.06 * sin(t * 19.0 + TIME * 1.1 + seed * 4.0);
		float core = 1.0 - smoothstep(0.0, 0.06, abs(UV.y * 2.0 - 1.0 + wander));
		float period = 3.1;
		float cycle = floor((TIME + seed * period) / period);
		float head = (mod(TIME + seed * period, period) - 0.3 * fract(cycle * 0.618)) / 0.5;
		float spark = exp(-pow((t - head) * 14.0, 2.0)) + 0.35 * exp(-(head - t) * 7.0) * step(t, head);
		spark *= step(-0.1, head) * (1.0 - step(1.25, head));
		col = mix(col, gold * 1.2 + vec3(0.2, 0.25, 0.3) * spark, core * clamp(0.2 + spark * 1.5, 0.0, 1.0));
		a = max(a, core * (0.12 + 0.45 * clamp(spark, 0.0, 1.0)));
		a *= 1.0 - smoothstep(0.78, 1.0, t);
	} else {
		// A hair: pale, near white with a breath of her colour, fading toward the tip.
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
	ALPHA = clamp(a * COLOR.a * (1.0 - 0.65 * under), 0.0, 1.0);
}
