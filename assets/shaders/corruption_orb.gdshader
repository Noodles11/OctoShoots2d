// Corrupted bodies (docs/CORRUPTION.md): a murkling, a Blightroot's ink sac, a murkling's throw, the boss's crust. A
// wobbling membrane of darkness, matte and rubbery, tacky to look at (a soft broad sheen, no polish, a fine dimpled
// skin), with a soft cold violet rim (or its kind's own colour: skin); inside, a dim stolen ember (ember 0 for none).
// worn: her light's outline, kept round it while its corruption is not full (0 none → 1 all but freed), the thicker
// the less corruption it has left; hit flashes it brighter for a moment as her light strikes.
// swell inflates it (a sac's telegraph); fade dissolves it (a burst). A murkling's throw: pull bends it back away from
// aim like a drawn slingshot (−1), then lunges it forward (+1); bud swells the piece it throws out of its front.
// A throw itself: drip draws its back out into the neck it is pinching off from (1 → 0 as it parts).
shader_type spatial;
render_mode cull_back, shadows_disabled;

instance uniform float ember = 0.4;
instance uniform float wobble = 1.0;
instance uniform float seed = 0.0;
instance uniform float swell = 0.0;
instance uniform float fade = 1.0;
uniform vec3 ember_color : source_color = vec3(1.0, 0.42, 0.16);
// One body's own ember (alpha 1 to use it): a lesser murkling's carries its fish's old colour.
instance uniform vec4 ember_tint = vec4(0.0);
uniform vec3 rim_color : source_color = vec3(0.34, 0.09, 0.6);
// A kind's own base colour (alpha: how much of it): its skin under the ink and its rim (murklings stay ink-black).
instance uniform vec4 skin = vec4(0.0);
instance uniform vec3 aim = vec3(0.0, 0.0, 1.0);
instance uniform float pull = 0.0;
instance uniform float bud = 0.0;
instance uniform float drip = 0.0;
// Struck by her light (1 at the hit, fading): the outline flashes bright.
instance uniform float hit = 0.0;
// How far her light has worn it down (1 − its corruption left; 0 while full): the outline stays, thickening.
instance uniform float worn = 0.0;

varying vec3 v_dir;

void vertex() {
	v_dir = VERTEX;
	vec3 p = normalize(VERTEX);
	float t = TIME;
	float d = sin(t * 3.1 + seed * 5.1) * p.x * p.z * 1.1 + sin(t * 2.3 + seed * 2.7) * (p.x * p.x - p.z * p.z) * 0.8
		+ sin(t * 4.1 + seed * 1.3) * p.y * p.x * 0.7 + sin(t * 1.7 + seed) * (3.0 * p.y * p.y - 1.0) * 0.3;
	VERTEX += NORMAL * (d * 0.09 * wobble + swell * 0.18);
	if (pull != 0.0 || bud > 0.0 || drip > 0.0) {
		vec3 A = normalize(aim);
		float a = dot(p, A);
		vec3 lateral = VERTEX - A * dot(VERTEX, A);
		// The slingshot, bent in the water's plane: drawn back, it bows into a C (its middle hauled back, its tips left
		// reaching toward its aim), thinned along its aim, shivering with the strain; snapping, it whips through into the
		// opposite arc, its middle flung forward. P runs across the aim.
		vec3 P = normalize(vec3(-A.z, 0.0, A.x));
		float l = clamp(dot(VERTEX, P), -1.0, 1.0);
		float arch = 1.0 - l * l;
		float back = max(-pull, 0.0), fwd = max(pull, 0.0);
		VERTEX -= A * back * arch * (0.8 + 1.5 * max(a, 0.0));
		VERTEX -= A * dot(VERTEX, A) * back * 0.3;
		VERTEX += P * l * back * 0.3;
		VERTEX.y *= 1.0 - 0.15 * back;
		VERTEX += NORMAL * sin(TIME * 70.0 + seed * 9.0 + dot(p, vec3(5.0, 3.0, 4.0))) * 0.08 * back * back;
		VERTEX += A * fwd * arch * (0.8 + 1.4 * max(-a, 0.0));
		VERTEX -= A * dot(VERTEX, A) * fwd * 0.2;
		VERTEX += P * l * fwd * 0.12;
		// The piece of itself it throws, swelling out of its front: a knob on a narrowing neck.
		VERTEX += A * bud * 0.7 * smoothstep(0.55, 1.0, a);
		VERTEX -= lateral * bud * 0.45 * smoothstep(0.55, 0.85, a) * (1.0 - smoothstep(0.85, 1.0, a));
		// A throw parting from it: its back drawn out into a tapering neck, as a drop leaves a surface.
		float tail = smoothstep(0.2, 1.0, -a);
		VERTEX -= A * drip * 1.6 * tail * tail;
		VERTEX -= lateral * drip * 0.6 * tail;
	}
}

float hash3(vec3 p) { return fract(sin(dot(p, vec3(12.9898, 78.233, 37.719))) * 43758.5453); }

void fragment() {
	// A burst eats it away in specks.
	if (fade < 1.0 && hash3(floor(v_dir * 9.0) + seed) > fade) discard;
	float facing = clamp(dot(NORMAL, VIEW), 0.0, 1.0);
	float fres = pow(1.0 - facing, 2.4);
	vec3 n = (INV_VIEW_MATRIX * vec4(NORMAL, 0.0)).xyz;
	float swirl = 0.5 + 0.5 * sin(n.x * 7.0 + n.z * 5.0 + TIME * 1.6 + seed);
	ALBEDO = vec3(0.012, 0.01, 0.018) + vec3(0.02, 0.008, 0.026) * swirl;
	ALBEDO = mix(ALBEDO, skin.rgb * (0.16 + 0.1 * swirl), skin.a);
	vec3 rim = mix(rim_color, skin.rgb, skin.a);
	// Matte rubber: rough, a weak broad sheen; its skin finely dimpled, as if tacky.
	vec3 dimple = vec3(hash3(floor(v_dir * 26.0) + seed), hash3(floor(v_dir * 26.0) + seed + 3.1), hash3(floor(v_dir * 26.0) + seed + 7.7)) - 0.5;
	vec3 fine = vec3(hash3(floor(v_dir * 61.0) + seed + 1.3), hash3(floor(v_dir * 61.0) + seed + 5.9), hash3(floor(v_dir * 61.0) + seed + 9.2)) - 0.5;
	NORMAL = normalize(NORMAL + (dimple * 0.22 + fine * 0.1) * (1.0 - fres));
	ROUGHNESS = 0.72;
	SPECULAR = 0.22;
	float flicker = 0.7 + 0.3 * sin(TIME * 7.0 + seed * 3.0) * sin(TIME * 4.3 + seed);
	vec3 glow = ember_tint.a > 0.5 ? ember_tint.rgb : ember_color;
	// Its rim glows in its colour, brighter for a coloured kind, so it reads in the dark depths.
	// A soft rim (no hard polished edge).
	float soft_rim = pow(1.0 - facing, 1.7);
	EMISSION = rim * soft_rim * (0.32 + 0.15 * swirl + swell * 0.8) * (1.0 + 1.0 * skin.a) + rim * skin.a * 0.06 + glow * pow(facing, 6.0) * ember * flicker * 1.4;
	// Her light's outline along the silhouette: thin at first, thicker the less corruption is left (at most about a
	// fifth of its radius), flashing bright on each hit.
	if (worn > 0.0 || hit > 0.0) {
		float edge = mix(0.72, 0.42, clamp(worn, 0.0, 1.0));
		float line = smoothstep(edge, edge + 0.2, 1.0 - facing);
		float strength = (worn > 0.0 ? 1.1 + 0.9 * worn : 0.0) + hit * 3.0;
		EMISSION += vec3(1.0, 0.93, 0.75) * line * strength;
	}
}
