// A generated boss's body (docs/BOSS-GENERATOR.md §5, §13): its true colours drained toward ash while it is corrupted,
// and the Leak's ink spreading from the mutation sites like a stain under the skin — glossy, light-drinking black with a
// cold violet rim. The rig bakes per vertex: COLOR.r the stain (nearness to a mutation site or the pearl), COLOR.g the
// accent pattern (rings, bands, gonads), COLOR.b grooves (ribs, growth lines, furrows). `drained` 1 corrupted → 0 freed;
// `flash` whitens it on a hit; `shine` how wet its skin is (shell dry, slug wet).
shader_type spatial;
render_mode cull_disabled;

instance uniform float drained = 1.0;
instance uniform float flash = 0.0;
instance uniform float shine = 0.3;
// Extra stain while a mutation advances or sacs swell (0..1).
instance uniform float surge = 0.0;
uniform vec3 body_color : source_color = vec3(0.8, 0.7, 0.6);
uniform vec3 accent_color : source_color = vec3(0.9, 0.85, 0.75);
uniform vec3 ink : source_color = vec3(0.022, 0.016, 0.03);
uniform vec3 rim_color : source_color = vec3(0.5, 0.32, 0.85);

varying vec3 baked;
varying vec3 wpos;

float hash(vec3 p) { return fract(sin(dot(p, vec3(127.1, 311.7, 74.7))) * 43758.5453); }
float noise(vec3 p) {
	vec3 i = floor(p);
	vec3 f = fract(p);
	vec3 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(mix(hash(i), hash(i + vec3(1, 0, 0)), u.x), mix(hash(i + vec3(0, 1, 0)), hash(i + vec3(1, 1, 0)), u.x), u.y),
		mix(mix(hash(i + vec3(0, 0, 1)), hash(i + vec3(1, 0, 1)), u.x), mix(hash(i + vec3(0, 1, 1)), hash(i + vec3(1, 1, 1)), u.x), u.y), u.z);
}

void vertex() {
	baked = COLOR.rgb;
	wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	float stain = baked.r, accent = baked.g, groove = baked.b;
	vec3 c = mix(body_color, accent_color, clamp(accent, 0.0, 1.0));
	c *= 1.0 - 0.3 * groove;
	// Corrupted: the colour drained toward ash.
	float grey = dot(c, vec3(0.3, 0.59, 0.11));
	c = mix(c, vec3(grey * 0.72) * vec3(1.03, 1.0, 0.96), drained * 0.85);
	// The ink: a ragged-edged stain from the sites, veins of it reaching out along the grooves.
	float n = noise(wpos * 2.3) * 0.65 + noise(wpos * 6.1) * 0.35;
	float reach = stain + surge * 0.25 + groove * 0.12 * stain;
	float inked = smoothstep(0.32, 0.62, reach + (n - 0.5) * 0.35) * drained;
	c = mix(c, ink, inked);
	float fres = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0);
	c = mix(c, vec3(1.0), clamp(flash, 0.0, 1.0));
	ALBEDO = c;
	ROUGHNESS = mix(mix(0.85, 0.35, shine), 0.12, inked);
	SPECULAR = mix(0.3 + 0.4 * shine, 0.85, inked);
	// The cold violet rim on the ink only: never a glow to rival her light.
	EMISSION = rim_color * fres * inked * 0.45 + vec3(flash * 0.5);
}
