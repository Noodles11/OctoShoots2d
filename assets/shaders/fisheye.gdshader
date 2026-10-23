// A slight fisheye over the finished view (Fisheye.cs): the picture bends toward its edges, as through a dome port —
// barely in the middle, more toward the rim, the corners staying the corners (so nothing is lost off screen). Drawn over
// the whole 3D view (Clementine and her light included), under the HUD. Fisheye.Source does the same sums for the mouse.
shader_type canvas_item;

uniform sampler2D screen : hint_screen_texture, filter_linear_mipmap;
uniform float strength = 0.08;

void fragment() {
	vec2 size = 1.0 / SCREEN_PIXEL_SIZE;
	vec2 a = vec2(size.x / size.y, 1.0);
	vec2 r = (SCREEN_UV - 0.5) * a;
	float rmax2 = dot(a * 0.5, a * 0.5);
	float r2 = dot(r, r);
	// Quartic: it bends mostly toward the rim.
	vec2 src = r * (1.0 + strength * r2 * r2) / (1.0 + strength * rmax2 * rmax2);
	COLOR = vec4(textureLod(screen, src / a + 0.5, 0.0).rgb, 1.0);
}
