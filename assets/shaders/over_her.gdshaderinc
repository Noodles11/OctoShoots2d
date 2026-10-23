// What may pass over Clementine (KelpView, BlightView.Octopus): she is drawn over the whole scene, so whatever really
// stands between her and the camera is drawn a second time after her — its *_over shader — and only where it covers
// her: near her (within OverRadius of her bell, on the map), and nearer the camera than her bell (what is behind her
// stays behind her). Also hazed by hand, as the water's pass would have hazed it (it is drawn after that pass).
// her_glow (BellView): her bell (xyz, world).
global uniform vec4 her_glow;
global uniform vec3 reef_water_deep;

const float OverRadius = 2.6;

/// How much of this fragment is drawn over her (0..1): world its world position, view_z its view-space depth (VERTEX.z).
float over_her(vec3 world, float view_z, mat4 view) {
	vec3 her = her_glow.xyz;
	float near = 1.0 - smoothstep(OverRadius - 0.9, OverRadius, distance(world.xz, her.xz));
	float her_depth = -(view * vec4(her, 1.0)).z;
	float in_front = smoothstep(0.05, 0.35, her_depth + view_z);
	return near * in_front;
}

/// A colour as the water's pass would have shown it (blued, a shade of the deep water's veil over it).
vec3 over_haze(vec3 c, float veil) {
	return mix(c * vec3(0.72, 0.92, 1.03), reef_water_deep * 0.7, veil);
}
