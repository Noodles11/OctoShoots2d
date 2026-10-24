// The holy light a light spring bursts up from its emblem while she bathes in it (docs/CORRUPTION.md): a burst of god
// rays fanning up and out from the emblem — many shafts of uneven width and strength round an open cone widening as it
// climbs, blazing at their foot and thinning to nothing high up, each flaring and dimming on its own while the light
// streams up them. Seen from above it is a sunburst of rays round her. Unlit while she is outside it (bathing 0).
shader_type spatial;
render_mode blend_add, unshaded, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;

instance uniform float bathing = 0.0;
// Spent (its one washing used): it fades away, 1 → 0.
instance uniform float fade = 1.0;
// How many shafts round it, and how bright it blazes (HDR: it blooms).
uniform float shafts = 22.0;
uniform float power = 2.2;
uniform vec3 colour : source_color = vec3(1.0, 0.97, 0.88);
uniform vec3 warm : source_color = vec3(1.0, 0.84, 0.55);

float hash(float n) { return fract(sin(n * 127.1) * 43758.5453); }

void fragment() {
	// UV.y runs from the top (0) to the foot (1); UV.x round it.
	float up = 1.0 - UV.y;
	float t = TIME;
	// The shafts: each its own width, strength and flicker.
	float s = UV.x * shafts;
	float id = floor(s);
	float f = fract(s);
	float width = mix(0.12, 0.42, hash(id));
	float shaft = 1.0 - smoothstep(width * 0.4, width, abs(f - 0.5));
	float strength = mix(0.35, 1.0, hash(id + 17.0)) * (0.6 + 0.4 * sin(t * mix(2.0, 5.0, hash(id + 5.0)) + id * 3.1));
	// A softer glow between the shafts, so the burst reads as light, not stripes.
	float haze = 0.18;
	// The light streaming up them.
	float stream = 0.75 + 0.25 * sin((up * 4.0 - t * 2.4 + hash(id + 9.0) * 6.0) * 6.2831);
	// Blazing at the foot, thinning to nothing high up.
	float rise = pow(1.0 - smoothstep(0.0, 1.0, up), 1.6);
	float a = (shaft * strength + haze) * stream * rise * power * bathing;
	ALBEDO = mix(colour, warm, 0.35 * (1.0 - up)) * a;
	ALPHA = fade;
}
