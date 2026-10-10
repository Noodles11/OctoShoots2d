// A level's frame (DESIGN-TOPDOWN §4.6). The level below sits under the current one, 8 m down and moved so its start
// lies under the hole; when Clementine has dived, the whole world is moved back to the origin in one frame. So every
// level shader works in its level's own coordinates:
//   level_offset  where the level sits in the world right now (its node's translation)
//   level_origin  a fixed absolute position (xz) of the level's own origin, for patterns: they never jump when the
//                 level is moved
//   portal        the dive's opening: world xz centre, radius, and mode (0 off, 1 this level is cut away inside it,
//                 2 only the inside is drawn) — the level above opens like an iris onto the level below
// Instance-free uniforms: set per material by LevelView.

uniform vec3 level_offset = vec3(0.0);
uniform vec2 level_origin = vec2(0.0);
uniform vec4 portal = vec4(0.0);

const float LF_BAYER[16] = float[](0.0, 8.0, 2.0, 10.0, 12.0, 4.0, 14.0, 6.0, 3.0, 11.0, 1.0, 9.0, 15.0, 7.0, 13.0, 5.0);

// The ordered-dither threshold at a pixel (0..1).
float lf_dither(vec2 frag) {
	ivec2 p = ivec2(frag) % 4;
	return (LF_BAYER[p.y * 4 + p.x] + 0.5) / 16.0;
}

// A world position in this level's own coordinates.
vec3 lf_local(vec3 world) { return world - level_offset; }

// A local position where patterns are drawn: absolute across the dive.
vec3 lf_pattern(vec3 local) { return vec3(local.x + level_origin.x, local.y, local.z + level_origin.y); }

// True where the portal hides this pixel; its edge dissolves over a few metres.
bool lf_portal_cut(vec3 world, vec2 frag) {
	if (portal.w < 0.5) return false;
	float inside = 1.0 - smoothstep(-3.0, 3.0, distance(world.xz, portal.xy) - portal.z);
	float t = lf_dither(frag);
	return portal.w < 1.5 ? inside > t : 1.0 - inside > t;
}
