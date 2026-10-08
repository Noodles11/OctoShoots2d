// Clementine's bell (THEME-BIBLE §5, §6.3): a translucent tangerine dome lit from within. Through it show the four
// gold gonad horseshoes round the stomach, the radial canals and the ring canal, warts of stinging cells, and eight rim
// organs in the notches of the scalloped margin. A light wave runs base→rim on every contraction. Unshaded: she is
// her own light and never changes hue with depth.
//   UV: x runs apex (0) → margin (1), y is the angle (0..1). UV2: the bell's plan, unit disk.
// The contraction (`pulse`) is matched on the CPU by BellView.MarginAt, which anchors the tentacles to the margin.
shader_type spatial;
render_mode unshaded, cull_back, blend_mix, depth_draw_never, depth_test_disabled;

uniform float pulse = 0.0;      // contraction, about −0.15 (flared) to 1 (fully contracted)
uniform float wave = 1.0;       // the light wave's position along the bell, 0 apex → 1 margin
uniform float hurt = 0.0;       // magenta flash, 1 → 0
uniform float ink = 0.0;        // the ink dash's ghosting, 0..1
uniform float glow = 1.0;
uniform vec3 body : source_color = vec3(1.0, 0.549, 0.259);   // tangerine #FF8C42
uniform vec3 gold : source_color = vec3(1.0, 0.769, 0.420);   // warm gold #FFC46B
uniform vec3 flash : source_color = vec3(1.0, 0.239, 0.498);  // hurt magenta #FF3D7F

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

	// The four gonads: gold horseshoes round the stomach, open toward the centre.
	float gonad = 0.0, gonad_fill = 0.0;
	for (int k = 0; k < 4; k++) {
		float ka = 0.7853982 + float(k) * 1.5707963;
		vec2 c = vec2(cos(ka), sin(ka)) * 0.33;
		vec2 d = q - c;
		float open = 1.0 - smoothstep(0.35, 0.75, dot(normalize(d + 1e-5), -normalize(c)));
		gonad = max(gonad, line(length(d) - 0.14, 0.05) * open);
		gonad_fill = max(gonad_fill, 1.0 - smoothstep(0.08, 0.15, length(d)));
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
	col = mix(col, gold, gonad_fill * 0.25 + stomach * 0.5);
	col = mix(col, mix(gold, vec3(1.0, 0.93, 0.8), 0.35), gonad);
	col = mix(col, gold * 1.05, canals * 0.7 + ring * 0.6);
	col = mix(col, vec3(1.0, 0.85, 0.7), warts * 0.45);
	col = mix(col, body * 1.15 + 0.08, margin * 0.5);
	vec3 organ = mix(gold * 1.4, flash * 1.6, hurt);
	col = mix(col, organ, rho * (0.75 + 0.25 * glow));
	col += gold * swell * 0.55;
	// The silhouette: a deeper tangerine edge, so she reads against pale sand as well as in the dark.
	col = mix(col, vec3(0.62, 0.12, 0.02), smoothstep(0.15, 0.7, fres) * 0.7);
	// Hurt: the whole bell flushes magenta for a beat.
	col = mix(col, flash * 1.2, hurt * 0.55);
	// The ink dash: a dark violet ghost with a bright rim.
	col = mix(col, vec3(0.16, 0.08, 0.26) + fres * gold * 0.8, ink * 0.55);
	ALBEDO = col * glow;

	float a = 0.6 + 0.2 * r * r + fres * 0.45 + gonad * 0.35 + canals * 0.2 + ring * 0.2 + margin * 0.15 + rho * 0.5 + warts * 0.15 + swell * 0.2;
	ALPHA = clamp(a * (1.0 - ink * 0.4) + hurt * 0.2, 0.0, 0.96);
}
