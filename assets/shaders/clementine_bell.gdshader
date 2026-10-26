// Clementine's bell (THEME-BIBLE §5, §6.3): a dome of living glass, nearly clear — only a faint warm light at its heart
// and its edge catching the light. What glows is hers: the four gonad horseshoes (bioluminescent, clear inside, a soft
// halo round each line), the rim — a bright line round the margin and eight rim organs in its notches — and a scatter
// of tiny twinkling specks. A light wave runs base→rim on every contraction. Unshaded: she is her own light and never
// changes hue with depth.
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

	// The four gonads: horseshoes round her middle, open toward the centre — her health, a quarter each. Ring k holds
	// the quarter from k/4 up: always warm gold, breathing with her pulse, and as opaque as its quarter is full — a
	// draining ring fades, an empty one is gone into the bell. When only the first is left its light pulses slowly in
	// and out: a dying light at the centre of her. The first is a little larger and hotter than the rest: it leads — she
	// turns to keep it toward her aim (BellView), so it is her pointer.
	float gonad = 0.0, lead = 0.0, halo = 0.0;
	float critical = 1.0 - step(0.25, health);
	vec3 gonad_col = mix(gold, vec3(1.0, 0.93, 0.8), 0.2) * (0.92 + 0.1 * pulse);
	float dying = 0.25 + 0.75 * (0.5 + 0.5 * sin(TIME * 2.4));
	for (int k = 0; k < 4; k++) {
		float ka = 0.7853982 + float(k) * 1.5707963;
		vec2 c = vec2(cos(ka), sin(ka)) * 0.43;
		vec2 d = q - c;
		float open = 1.0 - smoothstep(0.35, 0.75, dot(normalize(d + 1e-5), -normalize(c)));
		float size = k == 0 ? 0.2 : 0.17;
		float shape = line(length(d) - size, k == 0 ? 0.07 : 0.06) * open;
		float lit = clamp(health * 4.0 - float(k), 0.0, 1.0);
		if (k == 0) lit *= mix(1.0, dying, critical);
		gonad = max(gonad, shape * lit);
		halo = max(halo, line(length(d) - size, 0.22) * open * lit);
		if (k == 0) lead = shape * lit;
	}
	// (No stomach drawn: her middle is clear.)


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

	// Bioluminescent specks scattered over the bell, each twinkling on its own beat.
	vec2 cell = floor(q * 15.0);
	vec2 inside = fract(q * 15.0) - 0.5;
	vec2 jit = vec2(hash(cell), hash(cell + 17.0)) - 0.5;
	float twinkle = 0.35 + 0.65 * pow(0.5 + 0.5 * sin(TIME * (1.5 + 2.0 * hash(cell + 5.0)) + hash(cell + 9.0) * 40.0), 3.0);
	float specks = step(0.8, hash(cell + 3.0)) * (1.0 - smoothstep(0.03, 0.09, length(inside - jit * 0.5))) * twinkle;

	// Rim organs in eight of the margin's notches.
	float rho = 0.0;
	for (int k = 0; k < 8; k++) {
		float ka = 0.1963495 + float(k) * 0.7853982;
		rho = max(rho, 1.0 - smoothstep(0.025, 0.055, distance(q, vec2(cos(ka), sin(ka)) * 0.95)));
	}
	// The contraction's light wave, base → rim.
	float swell = exp(-pow((u - wave) / 0.11, 2.0)) * (1.0 - wave * 0.6);

	// The glass: nearly clear, a faint warm light at her heart, her own colour toward the rim, its edge catching the
	// light (fresnel). Barely there: the water shows through her.
	vec3 heart = mix(gold, vec3(1.0, 0.94, 0.88), 0.35);
	vec3 col = mix(heart, mix(body, vec3(1.0), 0.25), smoothstep(0.0, 0.85, r));
	float a = 0.04 + 0.16 * (1.0 - smoothstep(0.0, 0.6, r)) * (0.85 + 0.3 * pulse) + 0.3 * fres;
	col = mix(col, mix(body, vec3(1.0, 0.95, 0.9), 0.55), smoothstep(0.2, 0.8, fres) * 0.6);
	// The contraction's light wave, base → rim.
	col += gold * swell * 0.35;
	a += swell * 0.1;
	// Specks of light.
	col = mix(col, vec3(1.0, 0.96, 0.9), specks);
	a = max(a, specks * 0.7);
	// The gonads: her health, bioluminescent lines with a soft halo round each.
	col = mix(col, gonad_col * 1.15, clamp(halo * 0.5 + gonad, 0.0, 1.0));
	a = max(a, halo * 0.22);
	col = mix(col, mix(gonad_col, vec3(1.0, 0.98, 0.92), 0.35) * 1.25, gonad * 0.7);
	a = max(a, gonad * 0.95);
	col = mix(col, vec3(1.0, 0.98, 0.92), lead * 0.25);
	// The rim: a bright line round the margin, its organs glowing in its notches.
	float rimline = smoothstep(0.86, 0.94, u) * (1.0 - smoothstep(0.975, 1.0, u));
	vec3 rim_glow = mix(body, vec3(1.0, 0.95, 0.88), 0.3) * 1.3;
	col = mix(col, rim_glow, rimline * 0.85);
	a = max(a, rimline * (0.55 + 0.1 * pulse));
	vec3 organ = mix(gold * 1.4, flash * 1.6, hurt);
	col = mix(col, organ, rho * (0.75 + 0.25 * glow));
	a = max(a, rho * 0.9);
	// The rim gauge (her active pearl's charge) over the margin, a faint track and its light.
	col = mix(col, body * 0.5, rim_track * 0.4);
	a = max(a, rim_track * 0.2);
	col = mix(col, rim_col * 1.15, clamp(rim_lit, 0.0, 1.0));
	a = max(a, clamp(rim_lit, 0.0, 1.0) * 0.95);
	// Hurt: the whole bell flushes magenta for a beat.
	col = mix(col, flash * 1.2, hurt * 0.6);
	a += hurt * 0.35;
	// The ink dash: a dark violet ghost with a bright rim.
	col = mix(col, vec3(0.16, 0.08, 0.26) + fres * gold * 0.8, ink * 0.55);
	// The ritual: her colours give way to the pearl's look, glowing as the pearl does (the pattern fills the glass);
	// the burst whitens her for a beat as her own colours flood back.
	vec3 worn = mimic_ramp(mimic_t(q, fres)) * (1.1 + 0.3 * fres);
	col = mix(col, worn, clamp(mimic, 0.0, 1.0) * 0.88);
	a = mix(a, max(a, 0.55), clamp(mimic, 0.0, 1.0));
	col = mix(col, vec3(1.0, 0.95, 0.85) * 1.6, clamp(burst, 0.0, 1.0) * 0.7);
	a += clamp(burst, 0.0, 1.0) * 0.4;
	ALBEDO = col * glow * breath;
	ALPHA = clamp(a * (1.0 - ink * 0.3), 0.0, 0.97);
}
