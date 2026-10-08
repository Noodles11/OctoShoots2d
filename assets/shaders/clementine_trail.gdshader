// Clementine's tentacles and oral arms: camera-facing ribbons along verlet chains (BellView). Marginal tentacles are
// thin glowing threads beaded with stinging cells; oral arms are wider, paler, ruffled curtains.
//   UV: x along the strand (0 at the bell → 1 at the tip), y across (0..1). UV2.x: 0 tentacle, 1 oral arm.
//   COLOR.a: the strand's own opacity (hurt and ink are applied through uniforms).
shader_type spatial;
render_mode unshaded, cull_disabled, blend_mix, depth_draw_never, depth_test_disabled;

uniform float hurt = 0.0;
uniform float ink = 0.0;
uniform float glow = 1.0;
uniform vec3 body : source_color = vec3(1.0, 0.549, 0.259);
uniform vec3 gold : source_color = vec3(1.0, 0.769, 0.420);
uniform vec3 flash : source_color = vec3(1.0, 0.239, 0.498);

void fragment() {
	float t = UV.x;
	float s = abs(UV.y * 2.0 - 1.0);
	bool arm = UV2.x > 0.5;
	vec3 col;
	float a;
	if (arm) {
		// A ruffled curtain: frilled edges that ripple along the arm, a brighter seam down the middle.
		float frill = 0.75 + 0.25 * sin(t * 46.0 - TIME * 2.0);
		a = (1.0 - smoothstep(0.55 * frill, frill, s)) * 0.62;
		float seam = 1.0 - smoothstep(0.0, 0.25, s);
		col = mix(mix(body, vec3(1.0, 0.8, 0.68), 0.45), gold, seam * 0.6);
		a *= 1.0 - smoothstep(0.75, 1.0, t);
	} else {
		// A thread: a bright core inside a deeper orange edge (it reads on pale sand and in the dark alike), with
		// sparse beads of stinging cells, thinning out toward the tip.
		float bead = (1.0 - smoothstep(0.12, 0.3, abs(fract(t * 11.0 + 0.3) - 0.5))) * (1.0 - smoothstep(0.3, 0.6, s));
		a = 1.0 - smoothstep(0.6, 1.0, s);
		col = mix(vec3(0.42, 0.08, 0.02), body * 1.1, 1.0 - smoothstep(0.45, 0.95, s));
		col = mix(col, gold * 1.25, bead * (1.0 - 0.6 * t));
		a *= 1.0 - 0.6 * t * t;
	}
	col = mix(col, flash * 1.3, hurt * 0.6);
	col = mix(col, vec3(0.2, 0.1, 0.3), ink * 0.5);
	ALBEDO = col * glow;
	ALPHA = clamp(a * COLOR.a, 0.0, 1.0);
}
