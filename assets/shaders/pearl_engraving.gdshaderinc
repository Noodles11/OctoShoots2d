// The icons engraved on a pearl (PearlIcon, in its order; docs/PEARLS.md, "Reading a pearl"): what kind of thing it does.
// pearl_engraving gives the distance from a point to the icon (below 0 inside it), on a face where the pearl's disc spans
// −1..1 and y is up; a shader cuts the pearl where that is below 0.

float eng_segment(vec2 p, vec2 a, vec2 b) {
	vec2 pa = p - a, ba = b - a;
	return length(pa - ba * clamp(dot(pa, ba) / dot(ba, ba), 0.0, 1.0));
}

// Up: a stat raised.
float eng_arrow(vec2 p) {
	float stem = eng_segment(p, vec2(0.0, -0.56), vec2(0.0, 0.5));
	float left = eng_segment(p, vec2(-0.36, 0.12), vec2(0.0, 0.54));
	float right = eng_segment(p, vec2(0.36, 0.12), vec2(0.0, 0.54));
	return min(stem, min(left, right)) - 0.1;
}

// Three dots in a chevron pointing up: a change to her shots.
float eng_chevron(vec2 p) {
	float d = length(p - vec2(0.0, 0.3));
	d = min(d, length(p - vec2(-0.42, -0.22)));
	d = min(d, length(p - vec2(0.42, -0.22)));
	return d - 0.2;
}

// A heater shield: defence.
float eng_shield(vec2 p) {
	float y = clamp(-p.y / 0.66, 0.0, 1.0);
	float w = 0.48 * sqrt(1.0 - y * y);
	return max(max(p.y - 0.54, abs(p.x) - w), -0.66 - p.y) * 0.8;
}

// A nautilus shell: a solid body stepping out at its opening, the one thin line of its coil left standing in it. Luck,
// shells and maps.
float eng_nautilus(vec2 p) {
	const float grow = 0.135;
	float r = length(p);
	float a = mod(atan(p.y, p.x) + 0.5, 6.2831853);
	float outer = 0.27 * exp(grow * a);
	float shell = r - outer;
	return max(shell, 0.03 - abs(r - outer * exp(-grow * 6.2831853)));
}

// A feather, leaning, its rachis left standing down the vane, a nick in its edge: mobility.
float eng_feather(vec2 p) {
	p = vec2(0.8 * p.x - 0.6 * p.y, 0.6 * p.x + 0.8 * p.y);
	float y = clamp((p.y - 0.08) / 0.56, -1.0, 1.0);
	float w = 0.28 * sqrt(1.0 - y * y) * (1.0 + 0.2 * y);
	float vane = max(abs(p.x) - w, abs(p.y - 0.08) - 0.56);
	vane = max(vane, 0.03 - abs(p.x));
	vane = max(vane, 0.03 - eng_segment(p, vec2(0.06, 0.14), vec2(0.34, 0.32)));
	return min(vane, eng_segment(p, vec2(0.0, -0.72), vec2(0.0, -0.3)) - 0.045);
}

// A thin swirl, two turns: a status.
float eng_swirl(vec2 p) {
	const float pitch = 0.05;
	float r = length(p);
	float a = mod(atan(p.y, p.x), 6.2831853);
	float inner = pitch * a, outer = pitch * (a + 6.2831853);
	return min(abs(r - inner), abs(r - outer)) - 0.075;
}

// A five-armed star: an active pearl.
float eng_star(vec2 p) {
	const float r = 0.68, rf = 0.46;
	const vec2 k1 = vec2(0.809016994375, -0.587785252292);
	const vec2 k2 = vec2(-0.809016994375, -0.587785252292);
	p.x = abs(p.x);
	p -= 2.0 * max(dot(k1, p), 0.0) * k1;
	p -= 2.0 * max(dot(k2, p), 0.0) * k2;
	p.x = abs(p.x);
	p.y -= r;
	vec2 ba = rf * vec2(-k1.y, k1.x) - vec2(0.0, 1.0);
	float h = clamp(dot(p, ba) / dot(ba, ba), 0.0, r);
	return length(p - ba * h) * sign(p.y * ba.x - p.x * ba.y);
}

float pearl_engraving(vec2 q, int icon) {
	if (icon == 0) return eng_arrow(q);
	if (icon == 1) return eng_chevron(q);
	if (icon == 2) return eng_shield(q);
	if (icon == 3) return eng_nautilus(q);
	if (icon == 4) return eng_feather(q);
	if (icon == 5) return eng_swirl(q);
	return eng_star(q);
}
