// Clementine's shot (docs/LIGHT-BUBBLES.md): light itself, no bubble. A small hot mote on a camera-facing quad: a
// white-hot core, a warm glow (her gold, whitening as it merges: warmth), and a faint twinkle that turns.
// lantern is its brightness (merging, pulses, dimming); ink (Ink Sac) sinks a violet heart into it.
// Its pearls show in it (CombatView): motion (xyz its heading in the world, w how far it is drawn out along it: Swordfish
// Bill's lance), twin (Mitosis: two cores circling each other), points (Starfish Arm: a five-point star, else four),
// spin (Boomerang Shrimp: it whirls), pearl (Pearl Diver's charge: a ring of nacre round the light, pink and gold).
shader_type spatial;
render_mode blend_add, unshaded, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;

instance uniform float lantern = 0.5;
instance uniform float warmth = 0.0;
instance uniform float ink = 0.0;
instance uniform float seed = 0.0;
instance uniform vec4 motion = vec4(1.0, 0.0, 0.0, 0.0);
instance uniform float twin = 0.0;
instance uniform float points = 4.0;
instance uniform float spin = 1.0;
instance uniform float pearl = 0.0;
// Her light (HerColor.cs, data/clementine_color.cfg).
global uniform vec3 her_light;
// Prism Pearl: a third of her split light, in its own colour (rgb; a > 0.5), instead of her own.
instance uniform vec4 prism_hue = vec4(0.0);
#define gold (prism_hue.a > 0.5 ? prism_hue.rgb : her_light)

varying vec2 v_dir;

void vertex() {
	// Its heading on screen; drawn out along it (the quad grows that way with it).
	vec2 d = (VIEW_MATRIX * vec4(motion.xyz, 0.0)).xy;
	v_dir = length(d) > 1e-4 ? normalize(d) : vec2(1.0, 0.0);
	vec2 q = VERTEX.xy;
	float along = dot(q, v_dir);
	VERTEX.xy = q + v_dir * along * motion.w;
	MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
	MODELVIEW_MATRIX = MODELVIEW_MATRIX * mat4(
		vec4(length(MODEL_MATRIX[0].xyz), 0.0, 0.0, 0.0),
		vec4(0.0, length(MODEL_MATRIX[1].xyz), 0.0, 0.0),
		vec4(0.0, 0.0, length(MODEL_MATRIX[2].xyz), 0.0),
		vec4(0.0, 0.0, 0.0, 1.0));
}

// A star of n rays at angle a: sharp along each ray, soft across it.
float star(vec2 p, float n, float a) {
	float ang = atan(p.y, p.x) - a;
	float r = length(p);
	float ray = pow(abs(cos(ang * n * 0.5)), n > 4.5 ? 14.0 : 24.0);
	return ray * exp(-r * 2.2) * smoothstep(0.0, 0.08, r);
}

void fragment() {
	vec2 p = (UV - 0.5) * 2.0;
	// Back to true distances (the quad is drawn out along its heading), so its glow stays round and a lance has room.
	vec2 d = vec2(v_dir.x, -v_dir.y);
	float al = dot(p, d), ac = dot(p, vec2(-d.y, d.x));
	al *= 1.0 + motion.w;
	vec2 lp = d * al + vec2(-d.y, d.x) * ac;
	float lance = motion.w;
	float r = length(lp);
	float flicker = 0.88 + 0.12 * sin(TIME * 31.0 + seed * 7.0) * sin(TIME * 17.0 + seed * 3.0);
	// The core: one, or two circling each other.
	float core;
	if (twin > 0.5) {
		float t = TIME * 7.0 + seed * 6.28;
		vec2 o = vec2(cos(t), sin(t)) * 0.3;
		core = exp(-dot(lp - o, lp - o) * 90.0) + exp(-dot(lp + o, lp + o) * 90.0);
		// The membrane still joining them, pinched in the middle like a cell dividing.
		vec2 ax = normalize(o);
		float along_o = dot(lp, ax), across_o = dot(lp, vec2(-ax.y, ax.x));
		core += exp(-along_o * along_o * 9.0 - across_o * across_o * (40.0 + 60.0 * exp(-along_o * along_o * 60.0))) * 0.35;
	} else {
		core = exp(-r * r * 60.0);
	}
	// A lance: a white-hot spear along its heading, a short sharp point ahead and a long tail behind.
	if (lance > 0.0) core += exp(-ac * ac * 260.0) * (al > 0.0 ? exp(-al * 7.0) : exp(al * 1.4 / (0.4 + lance))) * 1.1;
	float glow = exp(-r * r * 7.0) * 0.55 + exp(-r * 2.6) * 0.25;
	// The twinkle, turning (whirling for a boomerang).
	float a = TIME * 1.3 * spin + seed * 6.28;
	float tw = star(lp, points, a) * (points > 4.5 ? 1.35 : 0.65);
	vec3 hue = mix(gold, vec3(1.0, 0.95, 0.84), clamp(warmth, 0.0, 1.0));
	vec3 col = vec3(1.0, 0.98, 0.93) * core * 2.4 + hue * (glow + tw) * 1.4;
	float alpha = core + glow + tw;
	// A charged pearl: a ring of nacre round the light, its lustre turning pink to gold.
	if (pearl > 0.0) {
		float ring = exp(-pow((r - 0.42) / 0.07, 2.0));
		float ang = atan(lp.y, lp.x);
		vec3 nacre = mix(vec3(1.0, 0.78, 0.86), vec3(1.0, 0.9, 0.62), 0.5 + 0.5 * sin(ang * 2.0 + TIME * 2.0 + seed));
		col += nacre * ring * pearl * 1.3;
		alpha += ring * pearl * 0.8;
	}
	col = mix(col, col * vec3(0.55, 0.3, 0.9) * 0.6, ink);
	float edge = 1.0 - smoothstep(0.8, 1.0, length(p));
	ALBEDO = col;
	ALPHA = clamp(alpha * edge * lantern * 1.6 * flicker, 0.0, 1.0);
}
