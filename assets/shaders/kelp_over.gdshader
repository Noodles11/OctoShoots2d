// Kelp blades over Clementine (KelpView): the parts above her bell and near her, drawn again after her (she is drawn
// after the water's pass, over everything), so a blade between her and the camera covers her.
shader_type spatial;
render_mode cull_disabled, blend_mix, depth_draw_never;

#define OVER
#include "res://assets/shaders/kelp.gdshaderinc"
