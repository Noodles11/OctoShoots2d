// Art directions under study for the reef (docs/ART-LOOKS.md; `--look=N`, reef_art): not a filter laid over the picture
// but the reef painted another way, in its own surface shading (reef_surface.gdshaderinc's callers: the ground and the
// free rock). 0 is the game's own look, which takes the stippling of 4 and 5 (art_dots), its dots a deeper shade of
// the reef's own colour rather than ink.
//
//   1 Gouache layers: a muted palette; the reef stepped into flat painted layers by height (the deeper, the hazier and
//     cooler, as planes of atmosphere in concept art); broad brush strokes laid along the slopes instead of the fine
//     photographic detail; light in soft bands.
//   2 Felt tip and wash: the gouache layers, paler, with the minimap's hand: a wavering felt-tip line along every wall
//     where it meets her plane (a finer second pass beside it), faint contour strokes up the walls, and bundles of
//     hatching in the walls' shade.
//   3 Flat concept: the fewest, biggest shapes — the layers flat, almost no texture — two tones of light, and a pale rim
//     catching the light along the walls' tops.
//   4 Flat and felt tip: the reef drawn flat (each material one flat tone, no light or shade at all) and its depth stated
//     only by stippling, as in a pen drawing: the deeper or the more in shade, the closer the dots crowd. The walls
//     pocked like a sponge with holes and craters, shallow, deep or right through.
//   5 Felt tip over the reef's own colours: look 4's flat light, stippling and sponge, on the game's own textures and
//     colours, and the game's own blur and murk below her plane (topdown_post treats it as look 0).
//
// The caller (in fragment) works out what needs derivatives (hw: how much the height changes across a pixel) and calls
// art_paint, then art_ink; its light() bands the light through art_light.

// (Included by reef_surface.gdshaderinc, after sunlight.gdshaderinc: it reads sun_dir.)
global uniform int reef_art;

float art_hash(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float art_noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(art_hash(i), art_hash(i + vec2(1.0, 0.0)), f.x), mix(art_hash(i + vec2(0.0, 1.0)), art_hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

// The painted layers: the reef's height cut into bands (metres apart), each a flat plane of value, their edges soft and
// a little ragged (a brush's edge, not a ruler's).
float art_layer(vec3 w, float h, float step_m) {
	float ragged = (art_noise(w.xz * 0.35) - 0.5) * step_m * 0.5;
	float x = (h + ragged) / step_m;
	float i = floor(x), f = fract(x);
	return i + smoothstep(0.82, 1.0, f);
}

// Broad strokes laid across the slope (along the contours), long and soft: the brush's texture, at a painter's scale.
float art_strokes(vec3 w, vec3 n) {
	vec2 along = normalize(vec2(-n.z, n.x) + vec2(1e-4, 0.0));
	vec2 p = vec2(dot(w.xz, along), w.y * 1.6 + dot(w.xz, vec2(along.y, -along.x)) * (n.y > 0.8 ? 1.0 : 0.25));
	return art_noise(vec2(p.x * 0.55, p.y * 3.2)) * 0.6 + art_noise(vec2(p.x * 1.4 + 7.0, p.y * 7.5)) * 0.4;
}

// The palette held back: toward its own grey, and toward the depth's water (one harmony over the whole reef).
vec3 art_mute(vec3 c, float keep, vec3 water) {
	float l = dot(c, vec3(0.299, 0.587, 0.114));
	vec3 grey = vec3(l);
	vec3 harm = water / max(dot(water, vec3(0.299, 0.587, 0.114)), 0.02) * l;
	return mix(mix(grey, harm, 0.35), c, keep);
}

// The painted albedo: the reef's own colour (c) reworked by the look. local.y is the height over her plane.
vec3 art_paint(vec3 c, vec3 w, vec3 local, vec3 n, vec3 water, vec3 rock, vec3 sand) {
	if (reef_art <= 0 || reef_art == 5) return c;
	float h = local.y;
	if (reef_art == 4) {
		// Flat: rock and sand each one tone (a whisper of their own colour left), nothing else.
		float up = smoothstep(0.7, 0.92, n.y) * (1.0 - smoothstep(-0.4, 0.6, h));
		c = mix(mix(rock, sand, up), c, 0.12);
		return art_mute(c, 0.6, water) * 1.08;
	}
	if (reef_art == 3) {
		// Flat concept: the reef's two materials as flat tones (a whisper of its own colour left), layered by height.
		float up = smoothstep(0.7, 0.92, n.y) * (1.0 - smoothstep(-0.4, 0.6, h));
		vec3 flat_c = mix(rock, sand, up);
		c = mix(flat_c, c, 0.18);
		c = art_mute(c, 0.45, water);
		float layer = art_layer(w, h, 3.5);
		c *= 1.0 + 0.14 * clamp(layer, -3.0, 2.0);
		c = mix(c, water * 0.55, clamp(-layer * 0.2, 0.0, 0.7));
		c *= 0.94 + 0.08 * art_strokes(w, n);
		return c;
	}
	// Gouache (and the felt tip's wash, paler).
	c = art_mute(c, reef_art == 2 ? 0.45 : 0.55, water);
	float layer = art_layer(w, h, 1.8);
	// Higher layers (nearer the eye) a touch richer, deeper ones hazed toward the water and cooled.
	c *= 1.0 + 0.1 * clamp(layer, -5.0, 3.0);
	c = mix(c, water * 0.6, clamp(-layer * 0.12, 0.0, 0.6));
	// The brush: broad soft strokes instead of fine grain.
	c *= 0.78 + 0.44 * art_strokes(w, n);
	if (reef_art == 2) c = mix(c, vec3(dot(c, vec3(0.333))) * 1.15 + water * 0.05, 0.25);
	return c;
}

// How far the painted look smooths the reef's fine surface detail away (the sand's ripples, the coral heads' bumps):
// 0 keep it, 1 the plain shape.
float art_flatten() {
	return reef_art >= 3 ? 1.0 : reef_art > 0 ? 0.7 : 0.0;
}

vec3 art_hash3(vec3 p) {
	p = fract(p * vec3(0.1031, 0.103, 0.0973));
	p += dot(p, p.yxz + 33.33);
	return fract((p.xxy + p.yxx) * p.zyx);
}

// Stippling (looks 4 and 5): small dots of ink, one at most in each cell of a fine grid laid on the camera's picture
// plane (east and up the screen, so the dots keep one spacing on screen and never swim over the reef), each a little
// off its cell's middle. Each dot has its own share of the tone asked for: the darker the tone, the more of them are
// drawn (and a touch fatter), so the shade is stated by how close the dots crowd. How much ink lies here, 0..1.
float art_stipple(vec2 q, float px, float tone) {
	const float CELL = 0.13;
	if (tone <= 0.0) return 0.0;
	vec2 g = floor(q / CELL);
	float ink = 0.0;
	for (int j = -1; j <= 1; j++) {
		for (int i = -1; i <= 1; i++) {
			vec2 c = g + vec2(float(i), float(j));
			if (art_hash(c + 41.7) >= pow(tone, 1.25)) continue;
			vec2 at = (c + 0.2 + 0.6 * vec2(art_hash(c), art_hash(c + 17.3))) * CELL;
			// Small, fatter where the tone is dark, but never under a pixel across (it would flicker).
			float r = max(CELL * (0.1 + 0.2 * tone) * (0.85 + 0.3 * art_hash(c + 5.1)), px * 0.6);
			float d = length(q - at);
			ink = max(ink, 1.0 - smoothstep(r - px * 0.5, r + px * 0.5, d));
		}
	}
	return ink;
}

// The sponge (looks 4 and 5): the walls pocked with holes and craters of many sizes and depths, drawn as a pen would:
// a fine lip round each, on the side toward the sun (where the lip throws its shade inward), and inside it what the
// eye sees down the hole: in a shallow dish its side and its pale bottom; down a deep pit its side, stippled darker
// the further down, going solid black where the eye loses it; through a hole that goes all the way through the wall,
// its side and then the light water beyond. Each hole is a round shaft straight into the wall, its mouth where the wall cuts a ball hidden in the
// rock (two sizes of them, scattered in 3D round the wall); looked down from the camera's fixed angle, so no geometry
// and no extra passes. px: metres a pixel spans on the wall. x: the tone it asks of the stippling; y: ink drawn
// outright; z: how much of the water beyond shows.
const vec3 ART_VIEW = vec3(0.0, -0.866, -0.5);

vec3 art_sponge(vec3 w, vec3 n, float px) {
	float steep = 1.0 - smoothstep(0.4, 0.65, n.y);
	if (steep <= 0.0) return vec3(0.0);
	// A hole is there or not: only its lip thins out as the wall lies back.
	float firm = smoothstep(0.3, 0.7, steep);
	vec3 sun = normalize(sun_dir);
	// Into the wall along the view: how fast the eye goes down a hole, and how far across it.
	float vn = max(-dot(ART_VIEW, n), 0.0);
	vec3 vt = ART_VIEW + n * vn;
	vec3 res = vec3(0.0);
	for (int k = 0; k < 2; k++) {
		float S = k == 0 ? 2.6 : 1.1;
		vec3 g = floor(w / S);
		for (int z = -1; z <= 1; z++) for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++) {
			vec3 cell = g + vec3(float(x), float(y), float(z));
			vec3 c = cell + float(k) * 37.0;
			vec3 h = art_hash3(c);
			if (h.x > 0.5) continue;
			vec3 at = (cell + 0.2 + 0.6 * art_hash3(c + 9.7)) * S;
			float r = S * (0.16 + 0.24 * h.y);
			vec3 v = w - at;
			float d = length(v);
			if (d > r + px * 2.0) continue;
			// Across the wall from the mouth's middle; the side toward the sun is under the lip.
			vec3 flat_v = v - n * dot(v, n);
			float side = dot(flat_v, sun) / max(length(flat_v), 1e-4);
			float under = smoothstep(0.2, -0.6, side);
			float lip = 1.0 - smoothstep(px * 0.5, px * 1.4, abs(d - r));
			res.y = max(res.y, steep * lip * smoothstep(0.0, 0.7, under));
			if (d >= r) continue;
			// The mouth's radius, and the hole: one in six right through the wall (this thick), a third deep pits, the
			// rest shallow dishes (this deep).
			float e = dot(v, n);
			float rho = sqrt(max(r * r - e * e, 1e-6));
			float kind = art_hash(c.xy * 3.1 + c.z * 1.7);
			float roll = art_hash(c.yz * 2.3 + c.x * 5.9);
			bool through = kind < 0.17, pit = !through && kind < 0.5;
			float deep = through ? rho * (0.4 + 0.5 * roll) : pit ? 1e4 : rho * (0.2 + 0.5 * roll);
			// Where the eye, going down the hole, meets its side.
			float A = dot(vt, vt), B = dot(flat_v, vt), C = dot(flat_v, flat_v) - rho * rho;
			float t = A > 1e-6 ? (-B + sqrt(max(B * B - A * C, 0.0))) / A : 1e4;
			float down = vn * t;
			if (down < deep) {
				// Its side: the further down, the darker, and darker still where the lip shades it (the side toward the
				// sun); down a pit, black where the eye loses it.
				vec3 hit = flat_v + vt * t;
				float shaded = smoothstep(0.2, -0.6, dot(hit, sun) / rho);
				float fall = smoothstep(0.0, pit ? 0.6 * rho : max(deep, 0.1 * rho), down);
				res.x = max(res.x, firm * clamp(0.35 + 0.45 * fall + 0.35 * shaded, 0.0, 1.0));
				if (pit) res.y = max(res.y, firm * smoothstep(0.45 * rho, 0.8 * rho, down));
			} else if (through) {
				res.z = max(res.z, firm);
			} else {
				// A dish's bottom: pale, a shade darker the deeper and under the lip.
				res.x = max(res.x, firm * clamp(0.15 + 0.4 * deep / rho + 0.3 * under, 0.0, 1.0));
			}
		}
	}
	return res;
}

// The shade the stippling states here (0..1): how deep under her plane, how far turned from the sun, in a wall's
// shadow.
float art_tone(vec3 w, vec3 local, vec3 n) {
	float h = local.y;
	float below = max(-h - 0.3, 0.0);
	// Under her plane: sparse dots near it, crowding the deeper it goes.
	float tone = below > 0.2 ? 0.25 + 0.65 * smoothstep(1.0, 18.0, below) : 0.0;
	// Turned from the sun, or in a wall's shadow: the same, the darker the shade.
	float turned = smoothstep(0.35, 0.95, 1.0 - clamp(dot(n, -normalize(sun_dir)), 0.0, 1.0));
	float cast = 1.0 - sun_lit(vec3(w.x, h, w.z));
	return max(tone, max(turned * 0.8, cast * 0.5));
}

// The dots' grid: the camera's picture plane (east and up the screen).
vec2 art_picture(vec3 w) {
	return vec2(w.x, dot(w, vec3(0.0, 0.5, -0.866)));
}

// The game's own look (0): its shade stippled too, the dots not ink but a deeper shade of the reef's own colour
// (art_dot_colour), laid into its albedo so the light falls on them like the rest. How much of a dot lies here, 0..1.
float art_dots(vec3 w, vec3 local, vec3 n) {
	vec2 q = art_picture(w);
	return art_stipple(q, length(fwidth(q)) * 0.7071, art_tone(w, local, n));
}

// Looks 4 and 5's shade: the tone it must state here (art_tone, and the sponge's holes), drawn in stippled dots, and
// the sponge's lips and black pits in solid ink. x: the ink; y: how much of the water beyond a hole shows.
vec2 art_shading(vec3 w, vec3 local, vec3 n) {
	// Metres a pixel spans on the picture plane (the dots' scale) and on the reef's own surface (the lips').
	vec2 q = art_picture(w);
	float px = length(fwidth(q)) * 0.7071;
	float px_wall = length(fwidth(w)) * 0.7071;
	float tone = art_tone(w, local, n);
	vec3 sponge = art_sponge(w, n, px_wall);
	// Through a hole, only the water beyond: none of the wall's own shade.
	tone = mix(max(tone, sponge.x), 0.0, sponge.z);
	return vec2(max(0.85 * art_stipple(q, px, tone), 0.9 * sponge.y), sponge.z);
}

// The felt tip (looks 2-5, a light touch of it in 3): x how much ink lies here, 0..1; y how much of the water beyond a
// hole through the wall shows (looks 4 and 5). hw: the height's change across a pixel (fwidth); px: metres per pixel.
vec2 art_ink(vec3 w, vec3 local, vec3 n, float hw, float px) {
	if (reef_art < 2 || reef_art > 5) return vec2(0.0);
	if (reef_art >= 4) return art_shading(w, local, n);
	float h = local.y;
	float ink = 0.0;
	float wob = art_noise(w.xz * 0.45);
	// The wall's edge at her plane (the minimap's coastline): a bold wavering line, and a finer pass beside it.
	float shore = 0.25;
	float d = abs(h - shore) / max(hw, 1e-4);
	float wide = reef_art == 2 ? 3.2 : 1.8;
	ink = max(ink, 1.0 - smoothstep(wide * (0.6 + 0.6 * wob) * 0.5, wide * (0.6 + 0.6 * wob) * 0.5 + 1.0, d));
	if (reef_art == 2) {
		float d2 = abs(h - shore - 0.18 - 0.25 * (art_noise(w.xz * 0.9 + 7.0) - 0.5)) / max(hw, 1e-4);
		ink = max(ink, 0.75 * (1.0 - smoothstep(0.35, 1.2, d2)));
		// Contour strokes up the walls, faint and broken (a pen lifting off the page).
		if (h > shore + 0.5) {
			float c = abs(fract((h - shore) / 1.5 + 0.5) - 0.5) * 1.5 / max(hw, 1e-4);
			float broken = step(0.35, art_noise(w.xz * 0.6 + h));
			ink = max(ink, 0.45 * broken * (1.0 - smoothstep(0.3, 1.1, c)));
		}
		// Hatching in the walls' shade, near her plane: bundles of short parallel strokes, each bundle its own angle.
		float shade = 1.0 - clamp(dot(n, normalize(sun_dir)), 0.0, 1.0);
		float steep = 1.0 - smoothstep(0.35, 0.75, n.y);
		float band = (1.0 - smoothstep(0.0, 4.0, h)) * step(-1.5, h);
		if (shade * steep * band > 0.2) {
			vec2 along = normalize(vec2(-n.z, n.x) + vec2(1e-4, 0.0));
			vec2 p = vec2(dot(w.xz, along), w.y) / max(px, 1e-4);
			vec2 cell = floor(p / 12.0);
			if (art_hash(cell + 3.1) > 0.3) {
				float a = art_hash(cell + 7.7) * 3.1416;
				vec2 q = p - (cell + 0.5) * 12.0;
				float across = dot(q, vec2(cos(a), sin(a)));
				float len = dot(q, vec2(-sin(a), cos(a)));
				float line = 1.0 - smoothstep(0.35, 0.95, abs(fract(across / 3.2) - 0.5) * 3.2);
				line *= 1.0 - smoothstep(3.5, 5.5, abs(len));
				ink = max(ink, line * 0.7 * smoothstep(0.2, 0.6, shade * steep * band));
			}
		}
	}
	return vec2(clamp(ink, 0.0, 1.0), 0.0);
}

// The felt tip's ink: near black, a little of the depth's water in it.
vec3 art_ink_colour(vec3 water) {
	return mix(vec3(0.07, 0.07, 0.09), water * 0.2, 0.3);
}

// A dot's colour in the game's own look: the reef's colour (c) deepened, a touch of the felt tip's ink in it.
vec3 art_dot_colour(vec3 c, vec3 water) {
	return mix(c * 0.45, art_ink_colour(water), 0.2);
}

// The water seen through a hole in the wall: lit, paler than the rock round it.
vec3 art_beyond(vec3 water) {
	return mix(water, vec3(0.78, 0.92, 0.9), 0.55);
}

// A pale rim catching the light along the walls' tops (look 3): where a steep wall turns over into a top.
float art_rim(vec3 local, vec3 n, float hw) {
	if (reef_art != 3) return 0.0;
	float top = smoothstep(0.45, 0.7, n.y) * (1.0 - smoothstep(0.75, 0.95, n.y)) * step(0.3, local.y);
	return top * 0.5;
}

// Light in bands (looks 1–3): soft-edged steps, as a painter states light and shade, not a smooth photographic ramp.
float art_light(float ndl) {
	if (reef_art <= 0) return ndl;
	if (reef_art >= 4) return 0.85;
	if (reef_art == 3) return mix(0.3, 1.0, smoothstep(0.18, 0.3, ndl));
	float b = 0.22 + 0.4 * smoothstep(0.1, 0.22, ndl) + 0.38 * smoothstep(0.5, 0.62, ndl);
	return b;
}

// The caustics, painted: fewer, softer dapples (none in the flat look).
float art_caustic(float caustic) {
	if (reef_art <= 0) return caustic;
	if (reef_art >= 3) return 0.0;
	return smoothstep(0.25, 0.6, caustic) * 0.35;
}
