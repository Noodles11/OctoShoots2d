// Topographic chart of a reef sector, grayscale: contour lines from the heightfield (sampled with a cubic B-spline, so
// the 1 m grid reads as smooth terrain) above and below the swim level alike. The swim level (0, the coastline of
// everything that blocks) is bold, a thin contour every `interval` metres, an index contour every 5 of them. Land is
// white paper with soft hill shading; water is a light grey that darkens with depth.
//
// The minimap and the Tab map (edges_only) are a hand-inked cave chart instead (docs/CAVEGEN.md): the water in the
// depth's own colour, the rock in its stone tone, a soft shadow along the walls, a wavering fineliner outline, and
// bundles of hatching in a band along the walls with the odd pebble past it. The heights texture carries the height
// (r), the distance into the rock from the water (g) and into the water from the rock (b), in metres.
shader_type canvas_item;

uniform sampler2D heights : filter_nearest;
uniform vec2 view_origin = vec2(0.0);   // metres at the top-left corner
uniform vec2 view_size = vec2(150.0);   // metres across the rect
uniform float samples = 151.0;
uniform float shore = 0.0;
uniform float interval = 2.0;
// The minimap and the Tab map: the hand-inked cave chart (see the top), under fog of war.
uniform bool edges_only = false;
uniform vec3 water_col : source_color = vec3(0.5, 0.86, 0.82);
uniform vec3 deep_col : source_color = vec3(0.09, 0.44, 0.48);
uniform vec3 rock_col : source_color = vec3(0.58, 0.6, 0.55);
uniform float ppm = 4.0;                // screen pixels per metre
uniform float line_px = 2.4;            // the fineliner's width
uniform float band_px = 14.0;           // the hatching band's depth into the rock
uniform bool hatching = true;
uniform bool use_fog = false;
uniform sampler2D fog : filter_linear;
// The mud cloud around a sealed boss arena, a ring about 15 m thick: centre (metres), rim, how risen (0–1).
uniform vec4 mud = vec4(0.0, 0.0, 15.0, 0.0);
const vec3 MUD = vec3(0.3, 0.25, 0.19);
const vec3 FOG = vec3(0.16, 0.19, 0.21);

const vec3 LAND = vec3(0.985, 0.983, 0.975);
const vec3 SHALLOW = vec3(0.9, 0.9, 0.895);
const vec3 DEEP = vec3(0.74, 0.74, 0.74);
const vec3 OUTSIDE = vec3(0.55, 0.55, 0.55);
const vec3 MINOR = vec3(0.42, 0.42, 0.42);
const vec3 INDEX = vec3(0.24, 0.24, 0.24);
const vec3 SHORE = vec3(0.12, 0.12, 0.12);
const vec3 LINE = vec3(0.11, 0.1, 0.1);

vec4 bspline(float t) {
	float t2 = t * t, t3 = t2 * t, u = 1.0 - t;
	return vec4(u * u * u, 3.0 * t3 - 6.0 * t2 + 4.0, -3.0 * t3 + 3.0 * t2 + 3.0 * t + 1.0, t3) / 6.0;
}

vec3 field_at(vec2 p) {
	vec2 i = floor(p);
	vec2 f = p - i;
	vec4 wx = bspline(f.x), wy = bspline(f.y);
	ivec2 hi = ivec2(int(samples) - 1);
	vec3 s = vec3(0.0);
	for (int y = 0; y < 4; y++) {
		vec3 row = vec3(0.0);
		for (int x = 0; x < 4; x++)
			row += texelFetch(heights, clamp(ivec2(i) + ivec2(x - 1, y - 1), ivec2(0), hi), 0).rgb * wx[x];
		s += row * wy[y];
	}
	return s;
}

float hash21(vec2 p) {
	p = fract(p * vec2(123.34, 456.21));
	p += dot(p, p + 45.32);
	return fract(p.x * p.y);
}

float vnoise(vec2 p) {
	vec2 i = floor(p), f = p - i;
	f = f * f * (3.0 - 2.0 * f);
	float a = hash21(i), b = hash21(i + vec2(1.0, 0.0)), c = hash21(i + vec2(0.0, 1.0)), d = hash21(i + vec2(1.0));
	return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

// Ink in the rock band along the walls: bundles of short parallel strokes, each bundle (a cell ~10 px across) at its
// own angle, a few cells left bare; past the band the odd pebble. `d_px`: pixels into the rock from the water.
float hatch_ink(vec2 w, float d_px) {
	float mpp = 1.0 / ppm;
	float band = band_px * (1.0 + 0.28 * sin(w.x * 0.21 + w.y * 0.13) + 0.18 * sin(w.y * 0.37 - w.x * 0.11));
	if (d_px > band + 12.0) return 0.0;
	if (d_px > band + 1.5) {
		// Pebbles: small rings, sparse.
		vec2 g = w / (7.0 * mpp), id = floor(g);
		if (hash21(id + 5.3) > 0.12) return 0.0;
		vec2 c = (id + 0.3 + 0.4 * vec2(hash21(id + 1.7), hash21(id + 8.2))) * 7.0 * mpp;
		float r = (1.1 + 0.9 * hash21(id + 2.9));
		float at = length(w - c) * ppm;
		return 1.0 - smoothstep(0.35, 0.9, abs(at - r));
	}
	float cell = 10.0 * mpp;
	vec2 g = w / cell, gi = floor(g);
	float best = 1e9;
	vec2 seed = vec2(0.0), id = vec2(0.0);
	for (int y = -1; y <= 1; y++)
	for (int x = -1; x <= 1; x++) {
		vec2 n = gi + vec2(float(x), float(y));
		vec2 s = n + 0.15 + 0.7 * vec2(hash21(n), hash21(n + 17.1));
		float d = length(g - s);
		if (d < best) { best = d; seed = s; id = n; }
	}
	if (hash21(id + 9.1) < 0.1) return 0.0;
	float ang = hash21(id + 3.7) * 3.14159;
	vec2 dir = vec2(cos(ang), sin(ang));
	float spacing = 2.9 * mpp;
	float u = dot(w - seed * cell, dir) / spacing;
	float off_px = abs(fract(u) - 0.5) * 2.9;
	float stroke = 1.0 - smoothstep(0.3, 0.85, off_px);
	// Each bundle stops at its own depth, so the band's inner edge is ragged.
	float reach = band * (0.7 + 0.3 * hash21(id + 1.3));
	return stroke * (1.0 - smoothstep(reach - 1.0, reach + 0.5, d_px));
}

float height_at(vec2 p) {
	return field_at(p).x;
}

// Coverage of an iso line where f crosses an integer, `px` pixels wide, antialiased. Flat ground (a pinned floor or a
// clamped plateau) has no contour.
float iso(float f, float px) {
	float aa = fwidth(f);
	if (aa < 1e-5) return 0.0;
	float d = abs(fract(f - 0.5) - 0.5) / aa;
	return 1.0 - smoothstep(px * 0.5 - 0.5, px * 0.5 + 0.5, d);
}

void fragment() {
	vec2 w = view_origin + UV * view_size;
	if (w.x < 0.0 || w.y < 0.0 || w.x > samples - 1.0 || w.y > samples - 1.0) {
		COLOR = vec4(use_fog ? FOG : OUTSIDE, 1.0);
	} else {
		vec3 fld = field_at(w);
		float h = fld.x;
		vec3 col;
		if (edges_only) {
			float aa = max(fwidth(h), 1e-4);
			float rock = smoothstep(-aa, aa, h - shore);
			// The water in the depth's colour, deepening into the shaft, with a soft shadow along the walls.
			vec3 water = mix(water_col, deep_col, clamp((shore - 1.6 - h) / 6.0, 0.0, 1.0));
			water *= 1.0 - 0.3 * (1.0 - smoothstep(0.0, 7.0, fld.z * ppm));
			col = mix(water, rock_col, rock);
			if (hatching) col = mix(col, LINE, 0.85 * rock * hatch_ink(w, fld.y * ppm));
			// The fineliner: a wavering bold line, and a finer second pass beside it.
			float wob = vnoise(w * 0.45);
			col = mix(col, LINE, iso((h - shore) / 100.0, line_px * (0.75 + 0.5 * wob)));
			col = mix(col, LINE, 0.8 * iso((h - shore - 0.35 * (vnoise(w * 0.9 + 7.0) - 0.5)) / 100.0, line_px * 0.4));
		} else {
			col = h > shore ? LAND : mix(SHALLOW, DEEP, clamp((shore - h) / 20.0, 0.0, 1.0));
			// Soft hill shading, lit from the north-west, on land and sea floor alike.
			vec2 metres_per_px = view_size / (vec2(1.0) / vec2(dFdx(UV.x), dFdy(UV.y)));
			vec3 nrm = normalize(vec3(-dFdx(h) / metres_per_px.x, -dFdy(h) / metres_per_px.y, 2.5));
			float lit = dot(nrm, normalize(vec3(-1.0, -1.0, 1.6)));
			col *= mix(0.86, 1.03, clamp(lit, 0.0, 1.0));
			float minor = iso((h - shore) / interval, 1.0);
			float index = iso((h - shore) / (interval * 5.0), 1.7);
			float edge = iso((h - shore) / 100.0, 2.2);
			col = mix(col, MINOR, minor * (h < shore ? 0.55 : 0.75));
			col = mix(col, INDEX, index);
			col = mix(col, SHORE, edge);
		}
		if (use_fog) col = mix(FOG, col, smoothstep(0.0, 1.0, texture(fog, (w + 0.5) / samples).r));
		if (use_fog && mud.w > 0.0) {
			float d = length(w - mud.xy);
			col = mix(col, MUD, mud.w * 0.92 * smoothstep(mud.z, mud.z + 3.0, d) * (1.0 - smoothstep(mud.z + 12.0, mud.z + 18.0, d)));
		}
		COLOR = vec4(col, 1.0);
	}
}
