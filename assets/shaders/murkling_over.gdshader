// A Blightroot's head drawn again after Clementine, only where it covers her (over_her.gdshaderinc), as kelp is.
shader_type spatial;
render_mode cull_back, shadows_disabled, depth_test_disabled, depth_draw_never;

#define OVER
#include "res://assets/shaders/murkling.gdshaderinc"
