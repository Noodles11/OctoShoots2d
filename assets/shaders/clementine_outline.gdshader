// Clementine behind something (BellView.Covered): a thin warm-white outline round her bell — the same light as the line
// her hits draw round a corrupted creature (corruption_orb's hit) — along its silhouette and its margin, drawn over
// everything (after the kelp that covers her), so she can always be found. Her bell's own pulse (clementine_bell's
// vertex) shapes it, so the line hugs the bell as it contracts. shown fades it in and out.
shader_type spatial;
render_mode unshaded, cull_back, blend_mix, depth_draw_never, depth_test_disabled, shadows_disabled, fog_disabled;

uniform float pulse = 0.0;
uniform float shown = 0.0;
uniform vec3 line_color : source_color = vec3(1.0, 0.93, 0.75);

varying float along;

void vertex() {
	// As clementine_bell.gdshader shapes it.
	float u = UV.x;
	float a = UV.y * 6.2831853;
	VERTEX.xz *= 1.0 - pulse * (0.07 + 0.18 * u * u);
	VERTEX.y *= 1.0 + 0.25 * pulse;
	VERTEX.y -= 0.04 * pulse * u * u * u;
	VERTEX.y += 0.014 * sin(a * 16.0 - TIME * 3.1) * pow(u, 5.0);
	along = u;
}

void fragment() {
	float facing = clamp(dot(NORMAL, VIEW), 0.0, 1.0);
	// Its silhouette, and its margin (the bell's open edge, which no silhouette line follows where it faces the camera).
	float rim = smoothstep(0.66, 0.88, 1.0 - facing);
	float margin = smoothstep(0.9, 0.97, along);
	float line = max(rim, margin);
	ALBEDO = line_color;
	ALPHA = line * shown;
}
