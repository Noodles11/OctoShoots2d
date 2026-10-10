// Topographic chart of a reef sector, grayscale: contour lines from the heightfield (sampled with a cubic B-spline, so
// the 1 m grid reads as smooth terrain) above and below the swim level alike. The swim level (0, the coastline of
// everything that blocks) is bold, a thin contour every `interval` metres, an index contour every 5 of them. Land is
// white paper with soft hill shading; water is a light grey that darkens with depth.
shader_type canvas_item;

uniform sampler2D heights : filter_nearest;
uniform vec2 view_origin = vec2(0.0);   // metres at the top-left corner
uniform vec2 view_size = vec2(150.0);   // metres across the rect
uniform float samples = 151.0;
uniform float shore = 0.0;
uniform float interval = 2.0;
// The minimap: only the walls (a darker tone, outlined at the swim-level coastline) on plain paper, under fog of war.
uniform bool edges_only = false;
uniform bool use_fog = false;
uniform sampler2D fog : filter_linear;
// The mud cloud around a sealed boss arena, a ring about 15 m thick: centre (metres), rim, how risen (0–1).
uniform vec4 mud = vec4(0.0, 0.0, 15.0, 0.0);
// The corruption (docs/CORRUPTION.md): open water reads ink-violet where the floor is corrupted, its own colour where
// cleansed, so the map literally regains colour as she plays.
uniform sampler2D corruption_tex : filter_linear, repeat_disable;
uniform bool use_corruption = false;
const vec3 INK = vec3(0.16, 0.09, 0.22);
const vec3 MUD = vec3(0.3, 0.25, 0.19);
const vec3 PAPER = vec3(0.93, 0.93, 0.92);
const vec3 WALL = vec3(0.56, 0.57, 0.57);
const vec3 FOG = vec3(0.16, 0.19, 0.21);

const vec3 LAND = vec3(0.985, 0.983, 0.975);
const vec3 SHALLOW = vec3(0.9, 0.9, 0.895);
const vec3 DEEP = vec3(0.74, 0.74, 0.74);
const vec3 OUTSIDE = vec3(0.55, 0.55, 0.55);
const vec3 MINOR = vec3(0.42, 0.42, 0.42);
const vec3 INDEX = vec3(0.24, 0.24, 0.24);
const vec3 SHORE = vec3(0.12, 0.12, 0.12);

vec4 bspline(float t) {
	float t2 = t * t, t3 = t2 * t, u = 1.0 - t;
	return vec4(u * u * u, 3.0 * t3 - 6.0 * t2 + 4.0, -3.0 * t3 + 3.0 * t2 + 3.0 * t + 1.0, t3) / 6.0;
}

float height_at(vec2 p) {
	vec2 i = floor(p);
	vec2 f = p - i;
	vec4 wx = bspline(f.x), wy = bspline(f.y);
	ivec2 hi = ivec2(int(samples) - 1);
	float s = 0.0;
	for (int y = 0; y < 4; y++) {
		float row = 0.0;
		for (int x = 0; x < 4; x++)
			row += texelFetch(heights, clamp(ivec2(i) + ivec2(x - 1, y - 1), ivec2(0), hi), 0).r * wx[x];
		s += row * wy[y];
	}
	return s;
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
		float h = height_at(w);
		vec3 col;
		if (edges_only) {
			// Walls a darker tone than the open water, antialiased at the edge, then the edge line itself.
			float aa = max(fwidth(h), 1e-4);
			col = mix(PAPER, WALL, smoothstep(-aa, aa, h - shore));
			col = mix(col, SHORE, iso((h - shore) / 100.0, 1.8));
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
		if (use_corruption) {
			float cr = texture(corruption_tex, w / (samples - 1.0)).r;
			float aa2 = max(fwidth(h), 1e-4);
			col = mix(col, INK, smoothstep(0.03, 0.6, cr) * (1.0 - smoothstep(-aa2, aa2, h - shore)) * 0.85);
		}
		if (use_fog) col = mix(FOG, col, smoothstep(0.0, 1.0, texture(fog, (w + 0.5) / samples).r));
		if (use_fog && mud.w > 0.0) {
			float d = length(w - mud.xy);
			col = mix(col, MUD, mud.w * 0.92 * smoothstep(mud.z, mud.z + 3.0, d) * (1.0 - smoothstep(mud.z + 12.0, mud.z + 18.0, d)));
		}
		COLOR = vec4(col, 1.0);
	}
}
