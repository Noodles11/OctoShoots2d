// Clementine's bell (THEME-BIBLE §5, §6.3): a translucent tangerine dome lit from within. Through it show the four
// gold gonad horseshoes round the stomach, the radial canals and the ring canal, warts of stinging cells, and eight rim
// organs in the notches of the scalloped margin. A light wave runs base→rim on every contraction. Unshaded: she is
// her own light and never changes hue with depth.
// Her vitals live on the bell (DESIGN-TOPDOWN §2.5): the four gonad rings are her health, a quarter each (warm gold,
// fading out — ever more transparent — as their quarters go, the last one pulsing slowly when it is all she has
// left); the rim is her
// active pearl's charge, a light sweeping clockwise from the top of the screen, breathing gold when ready.
// She spends light to shoot (docs/LIGHT-BUBBLES.md): `breath` dims her a little as she throws bubbles and rekindles it
// as she rests. Never dark: she is always the warm light.
//   UV: x runs apex (0) → margin (1), y is the angle (0..1). UV2: the bell's plan, unit disk.
// The contraction (`pulse`) is matched on the CPU by BellView.MarginAt, which anchors the tentacles to the margin.
shader_type spatial;
render_mode unshaded, cull_back, blend_mix, depth_draw_never, depth_test_disabled;

uniform float pulse = 0.0;      // contraction, about −0.15 (flared) to 1 (fully contracted)
uniform float wave = 1.0;       // the light wave's position along the bell, 0 apex → 1 margin
uniform float hurt = 0.0;       // magenta flash, 1 → 0
uniform float ink = 0.0;        // the ink dash's ghosting, 0..1
uniform float glow = 1.0;
uniform float breath = 1.0;     // her light reserve: 0.85 (just spent a volley of light bubbles) → 1 (rekindled)
uniform float health = 1.0;     // her HP as a share of her max, 0..1 (eased on the CPU)
uniform float charge = -1.0;    // the active pearl's charge 0..1 (1 ready); below 0, she holds none
uniform float snuff = 0.0;      // just used: 1 → 0, the gold whipping off the rim counter-clockwise
uniform float spin = 0.0;       // the bell's turn about its axis (she faces her aim), undone so the rim's top stays at the screen's top
// Her colours (HerColor.cs, data/clementine_color.cfg): body is the bell and tentacles, gold her light.
global uniform vec3 her_body;
global uniform vec3 her_light;
#define body her_body
#define gold her_light
uniform vec3 flash : source_color = vec3(1.0, 0.239, 0.498);  // hurt magenta #FF3D7F
// The absorbing ritual (docs/PEARLS.md): `mimic` 0..1 is how far she has taken on the pearl's look (its three colours
// laid out in its pattern, mimic_pattern as PearlPattern), like a chameleon; `burst` 1 → 0 is the light pulse as she
// returns to her own colours.
uniform float mimic = 0.0;
uniform vec3 mimic_c0 : source_color = vec3(1.0);
uniform vec3 mimic_c1 : source_color = vec3(1.0);
uniform vec3 mimic_c2 : source_color = vec3(1.0);
uniform int mimic_pattern = 0;
uniform float burst = 0.0;

vec3 mimic_ramp(float t) {
	t = clamp(t, 0.0, 1.0);
	return t < 0.5 ? mix(mimic_c0, mimic_c1, t * 2.0) : mix(mimic_c1, mimic_c2, t * 2.0 - 1.0);
}

float mimic_hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }

// The pearl's pattern laid over the bell's plan (q, unit disk), each the bell's own version of the pearl's.
float mimic_t(vec2 q, float fres) {
	float r = length(q), a = atan(q.y, q.x);
	if (mimic_pattern == 0) return 0.5 + 0.5 * sin(r * 11.0);
	if (mimic_pattern == 1) return 0.5 + 0.5 * sin(a * 3.0 + r * 9.0);
	if (mimic_pattern == 2) return step(0.68, mimic_hash(floor(q * 6.0)));
	if (mimic_pattern == 3) return 0.5 + 0.5 * sin(q.x * 7.0 + 3.0 * sin(q.y * 5.0 + q.x * 3.0));
	if (mimic_pattern == 4) return 0.5 + 0.5 * cos(r * 18.0);
	if (mimic_pattern == 5) return mix(step(0.0, sin(a * 6.0)), 0.5, 0.15);
	if (mimic_pattern == 6) return step(0.82, mimic_hash(floor(q * 14.0)));
	return fres;
}

varying float along;

void vertex() {
	float u = UV.x;
	float a = UV.y * 6.2831853;
	// The contraction: the margin pulls in hard, the apex barely; the dome stands taller.
	VERTEX.xz *= 1.0 - pulse * (0.07 + 0.18 * u * u);
	VERTEX.y *= 1.0 + 0.25 * pulse;
	VERTEX.y -= 0.04 * pulse * u * u * u;
	// The lappets flutter, a wave running round the margin.
	VERTEX.y += 0.014 * sin(a * 16.0 - TIME * 3.1) * pow(u, 5.0);
	along = u;
}

float hash(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float line(float d, float w) { return 1.0 - smoothstep(w * 0.5, w, abs(d)); }

void fragment() {
	vec2 q = UV2;
	float r = length(q);
	float ang = atan(q.y, q.x);
	float u = along;
	float fres = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 2.0);

	// The four gonads: horseshoes round the stomach, open toward the centre — her health, a quarter each. Ring k holds
	// the quarter from k/4 up: always warm gold, breathing with her pulse, and as opaque as its quarter is full — a
	// draining ring fades, an empty one is gone into the bell. When only the first is left its light pulses slowly in
	// and out: a dying light at the centre of her. The first is a little larger and hotter than the rest: it leads — she
	// turns to keep it toward her aim (BellView), so it is her pointer.
	float gonad = 0.0, gonad_fill = 0.0, lead = 0.0;
	float critical = 1.0 - step(0.25, health);
	vec3 gonad_col = mix(gold, vec3(1.0, 0.93, 0.8), 0.2) * (0.92 + 0.1 * pulse);
	float dying = 0.25 + 0.75 * (0.5 + 0.5 * sin(TIME * 2.4));
	for (int k = 0; k < 4; k++) {
		float ka = 0.7853982 + float(k) * 1.5707963;
		vec2 c = vec2(cos(ka), sin(ka)) * 0.33;
		vec2 d = q - c;
		float open = 1.0 - smoothstep(0.35, 0.75, dot(normalize(d + 1e-5), -normalize(c)));
		float size = k == 0 ? 0.17 : 0.14;
		float shape = line(length(d) - size, k == 0 ? 0.06 : 0.05) * open;
		float fill = 1.0 - smoothstep(size * 0.55, size * 1.05, length(d));
		float lit = clamp(health * 4.0 - float(k), 0.0, 1.0);
		if (k == 0) lit *= mix(1.0, dying, critical);
		gonad = max(gonad, shape * lit);
		gonad_fill = max(gonad_fill, fill * lit);
		if (k == 0) lead = max(shape, fill * 0.6) * lit;
	}
	// The stomach: a soft cross at the centre.
	float stomach = (1.0 - smoothstep(0.05, 0.16, r)) * 0.8;

	// Radial canals: eight straight, eight meandering between them; the ring canal near the margin.
	float seg = 6.2831853 / 16.0;
	float k16 = ang / seg;
	float odd = mod(floor(k16 + 0.5), 2.0);
	float wig = odd * 0.18 * sin(r * 34.0);
	float canal_d = (fract(k16 + 0.5 + wig) - 0.5) * seg * r;
	float canals = line(canal_d, 0.026) * smoothstep(0.38, 0.5, r) * (1.0 - smoothstep(0.9, 0.95, r));
	float ring = line(r - 0.9, 0.03);

	// The rim: her active pearl's charge. Measured clockwise from the top of the screen (the bell's turn undone), lit
	// up to the charge with a bright head; full, it breathes gold; just used, the gold whips back counter-clockwise.
	float band = smoothstep(0.76, 0.82, r) * (1.0 - smoothstep(0.97, 1.0, r));
	float sweep = fract((atan(q.x, -q.y) - spin) / 6.2831853 + 1.0);
	float rim_lit = 0.0, rim_track = 0.0;
	vec3 rim_col = vec3(0.0);
	if (charge >= 0.0) {
		if (charge >= 1.0) {
			rim_lit = 0.8 + 0.2 * sin(TIME * 1.8);
			rim_col = gold;
		} else {
			// The unlit part is a dim track, so the sweep reads against it.
			float filled = 1.0 - step(charge, sweep);
			float head = exp(-pow((sweep - charge) * 40.0, 2.0)) * step(0.001, charge);
			rim_lit = filled * 0.85 + head;
			rim_track = 1.0 - filled;
			rim_col = mix(mix(gold, vec3(1.0), 0.5), vec3(1.0, 0.98, 0.9), head);
		}
		if (snuff > 0.0) {
			float tail = 1.0 - step(snuff, sweep);
			float edge = exp(-pow((sweep - snuff) * 30.0, 2.0));
			rim_lit = max(rim_lit, tail * 1.2 + edge * 1.5);
			rim_col = mix(rim_col, gold * 1.6, max(tail, edge));
		}
		rim_lit *= band;
		rim_track *= band;
	}

	// Warts of stinging cells, scattered over the outer bell.
	vec2 cell = floor(q * 13.0);
	vec2 inside = fract(q * 13.0) - 0.5;
	vec2 jit = vec2(hash(cell), hash(cell + 17.0)) - 0.5;
	float warts = step(0.62, hash(cell + 3.0)) * (1.0 - smoothstep(0.08, 0.16, length(inside - jit * 0.5))) * smoothstep(0.3, 0.6, r);

	// Rim organs in eight of the margin's notches.
	float rho = 0.0;
	for (int k = 0; k < 8; k++) {
		float ka = 0.1963495 + float(k) * 0.7853982;
		rho = max(rho, 1.0 - smoothstep(0.025, 0.055, distance(q, vec2(cos(ka), sin(ka)) * 0.95)));
	}
	// The margin: folded tissue, thicker and brighter.
	float margin = smoothstep(0.86, 1.0, u);

	// The contraction's light wave, base → rim.
	float swell = exp(-pow((u - wave) / 0.11, 2.0)) * (1.0 - wave * 0.6);

	vec3 col = body * (0.55 + 0.25 * (1.0 - r));
	col = mix(col, gold, stomach * 0.5);
	col = mix(col, gonad_col, gonad_fill * 0.3);
	col = mix(col, gonad_col, gonad);
	col = mix(col, vec3(1.0, 0.97, 0.9), lead * 0.35);
	col = mix(col, gold * 1.05, canals * 0.7 + ring * 0.6);
	col = mix(col, mix(body, vec3(1.0), 0.6), warts * 0.45);
	col = mix(col, body * 1.15 + 0.08, margin * 0.5);
	vec3 organ = mix(gold * 1.4, flash * 1.6, hurt);
	col = mix(col, organ, rho * (0.75 + 0.25 * glow));
	col += gold * swell * 0.55;
	// The silhouette: a deeper edge of her own colour, so she reads against pale sand as well as in the dark.
	col = mix(col, body * body * 0.62, smoothstep(0.15, 0.7, fres) * 0.7);
	// The rim gauge sits over the silhouette's edge so it reads from any angle.
	col = mix(col, body * body * 0.3, rim_track * 0.6);
	col = mix(col, rim_col, clamp(rim_lit, 0.0, 1.0));
	// Hurt: the whole bell flushes magenta for a beat.
	col = mix(col, flash * 1.2, hurt * 0.55);
	// The ink dash: a dark violet ghost with a bright rim.
	col = mix(col, vec3(0.16, 0.08, 0.26) + fres * gold * 0.8, ink * 0.55);
	// The ritual: her colours give way to the pearl's look, glowing as the pearl does; the burst whitens her for a
	// beat as her own colours flood back.
	vec3 worn = mimic_ramp(mimic_t(q, fres)) * (1.1 + 0.3 * fres);
	col = mix(col, worn, clamp(mimic, 0.0, 1.0) * 0.88);
	col = mix(col, vec3(1.0, 0.95, 0.85) * 1.6, clamp(burst, 0.0, 1.0) * 0.7);
	ALBEDO = col * glow * breath;

	float a = 0.6 + 0.2 * r * r + fres * 0.45 + gonad * 0.35 + rim_lit * 0.4 + canals * 0.2 + ring * 0.2 + margin * 0.15 + rho * 0.5 + warts * 0.15 + swell * 0.2;
	ALPHA = clamp(a * (1.0 - ink * 0.4) + hurt * 0.2, 0.0, 0.96);
}
