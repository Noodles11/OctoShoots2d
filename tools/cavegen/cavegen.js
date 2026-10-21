// Ink Deep cave generator (prototype for docs/CAVEGEN.md): organic cave levels in the manner of Watabou's Cave
// Generator, re-made from scratch for the game. A level is a chain of chambers from the start to the way down, with
// side branches and the odd loop, joined by winding tunnels; it grows until its reachable water covers the area rule
// (6000 m², ±15%). The result mirrors the game's LevelMap: a 1 m height grid (open water ≤ 0, rock above), places
// (Pois), chambers (Plazas), tunnels (Corridors) and the shaft.
//
// Written to be ported line for line to C# (src/Core): the same xoshiro256** generator as OctoShoots.Core.Rng, integer
// hash noise, and no library calls beyond basic math.
(function (root) {
  'use strict';

  // ─────────────────────────── tuning ───────────────────────────

  const T = {
    Size: 140,            // the container, metres (a technical bound only: the rule is the area)
    Rim: 8,               // impassable band round the container
    Area: 6000,           // reachable water, m²
    AreaSlack: 0.15,      // ±15%
    MinPath: 90,          // start → way down, metres along the water
    MinStraight: 78,      // start → way down, straight line
    StartRadius: 8, StartRadiusArena: 15,
    ExitRadius: 8, ArenaRadius: 15,
    Attempts: 60,
    // Chambers and tunnels.
    StepMain: [16, 22], StepBranch: [14, 21],
    RadiusMain: [6, 10], RadiusBranch: [5, 9], RadiusStart: [9, 11],
    WidthMain: [6, 9], WidthBranch: [5, 7], WidthLoop: [4.5, 6],
    LoopChance: 0.25, LoopReach: 26,
    // The craggy edge: two octaves of noise on the outline (metres).
    Crag: 1.6, CragFine: 0.6,
    // Places (validator rules carried over from LevelValidator).
    PlaceFromStart: 39, PlaceFromExit: 40, PlaceSpacing: 21, EarlyMin: 39, EarlyMax: 47, MaxPlaces: 5,
    // Terrain.
    Floor: -1, PeakMin: 8, PeakMax: 14, BlueHoleRadius: 6.5, ShaftBottom: -9,
  };

  const PoiKind = { Start: 'Start', ShellCache: 'ShellCache', Exit: 'Exit', Shop: 'Shop', Secret: 'Secret', CurseDen: 'CurseDen', TreasureCave: 'TreasureCave', Ambush: 'Ambush' };
  const PlaceRadius = { Shop: 6, TreasureCave: 7, Secret: 5, CurseDen: 7, ShellCache: 5, Ambush: 9 };

  // ─────────────────────────── random: xoshiro256** through SplitMix64 (OctoShoots.Core.Rng) ───────────────────────────

  const M64 = (1n << 64n) - 1n;
  function splitMix(state) {
    state.x = (state.x + 0x9E3779B97F4A7C15n) & M64;
    let z = state.x;
    z = ((z ^ (z >> 30n)) * 0xBF58476D1CE4E5B9n) & M64;
    z = ((z ^ (z >> 27n)) * 0x94D049BB133111EBn) & M64;
    return z ^ (z >> 31n);
  }
  const rotl = (x, k) => ((x << BigInt(k)) | (x >> BigInt(64 - k))) & M64;

  function fnv1a(s) {
    let h = 0xcbf29ce484222325n;
    for (let i = 0; i < s.length; i++) {
      h ^= BigInt(s.charCodeAt(i));
      h = (h * 0x100000001b3n) & M64;
    }
    return h;
  }

  class Rng {
    constructor(seed) {
      this.seed = BigInt.asUintN(64, BigInt(seed));
      const st = { x: this.seed };
      this.s0 = splitMix(st); this.s1 = splitMix(st); this.s2 = splitMix(st); this.s3 = splitMix(st);
    }
    stream(name) { return new Rng(this.seed ^ ((fnv1a(name) * 0x9E3779B97F4A7C15n) & M64)); }
    nextU64() {
      const result = (rotl((this.s1 * 5n) & M64, 7) * 9n) & M64;
      const t = (this.s1 << 17n) & M64;
      this.s2 ^= this.s0; this.s3 ^= this.s1; this.s1 ^= this.s2; this.s0 ^= this.s3;
      this.s2 ^= t;
      this.s3 = rotl(this.s3, 45);
      return result;
    }
    float() { return Number(this.nextU64() >> 40n) / 16777216; }
    range(a, b) { return a + (b - a) * this.float(); }
    pick(r) { return this.range(r[0], r[1]); }
    int(n) { return Number((this.nextU64() >> 33n) % BigInt(n)); }
  }

  // ─────────────────────────── integer hash noise (portable) ───────────────────────────

  function hash2(x, y, seed) {
    let h = (Math.imul(x, 374761393) + Math.imul(y, 668265263) + Math.imul(seed, 1442695041)) | 0;
    h = Math.imul(h ^ (h >>> 13), 1274126177);
    h ^= h >>> 16;
    return (h >>> 0) / 4294967296;
  }
  function vnoise(x, y, seed) {
    const xi = Math.floor(x), yi = Math.floor(y);
    let fx = x - xi, fy = y - yi;
    fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
    const a = hash2(xi, yi, seed), b = hash2(xi + 1, yi, seed), c = hash2(xi, yi + 1, seed), d = hash2(xi + 1, yi + 1, seed);
    return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
  }
  // Two octaves, centred on 0 (about −1..1).
  const fbm = (x, y, seed) => (vnoise(x, y, seed) * 0.66 + vnoise(x * 2.03, y * 2.03, seed + 7) * 0.34) * 2 - 1;

  // ─────────────────────────── geometry ───────────────────────────

  const len = (x, y) => Math.sqrt(x * x + y * y);
  function segDist(px, py, ax, ay, bx, by) {
    const dx = bx - ax, dy = by - ay, l2 = dx * dx + dy * dy;
    const t = l2 > 0 ? Math.max(0, Math.min(1, ((px - ax) * dx + (py - ay) * dy) / l2)) : 0;
    return len(px - ax - dx * t, py - ay - dy * t);
  }

  // ─────────────────────────── the plan: what a level holds ───────────────────────────

  /** What places a level holds (the game's LevelPlan rolls these; here a stand-in with the same caps). */
  function planPlaces(rng, boss) {
    const kinds = [];
    if (rng.float() < 0.5) kinds.push(PoiKind.Shop);
    if (rng.float() < 0.6) kinds.push(PoiKind.TreasureCave);
    if (rng.float() < 0.5) kinds.push(PoiKind.Secret);
    if (rng.float() < 0.3) kinds.push(PoiKind.CurseDen);
    for (let i = rng.int(3); i > 0; i--) kinds.push(PoiKind.ShellCache);
    for (let i = rng.int(3); i > 0; i--) kinds.push(PoiKind.Ambush);
    if (boss && kinds.length > 3) kinds.length = 3;
    // Over the cap: ambushes go first, then the curse den, the secret, a cache (LevelStocking).
    for (const drop of [PoiKind.Ambush, PoiKind.Ambush, PoiKind.CurseDen, PoiKind.Secret, PoiKind.ShellCache, PoiKind.ShellCache])
      if (kinds.length > T.MaxPlaces) { const i = kinds.lastIndexOf(drop); if (i >= 0) kinds.splice(i, 1); }
    return kinds;
  }

  // ─────────────────────────── the cave graph ───────────────────────────

  function inBounds(x, y, r) {
    const m = T.Rim + r + 4;
    return x >= m && y >= m && x <= T.Size - m && y <= T.Size - m;
  }

  /** A chamber's organic outline: its radius wavers round it (four low harmonics). */
  function makeChamber(rng, x, y, r, role) {
    const harm = [];
    for (let k = 2; k <= 5; k++) harm.push({ k, a: rng.range(0.03, 0.12) / Math.sqrt(k - 1), p: rng.range(0, Math.PI * 2) });
    return { x, y, r, role, harm, links: [] };
  }

  /** A tunnel: a gentle curve (a quadratic Bézier) from one chamber to another, sampled every ~2 m. */
  function makeTunnel(rng, a, b, width, kind) {
    const dx = b.x - a.x, dy = b.y - a.y, l = len(dx, dy);
    const bend = rng.range(-0.3, 0.3) * l;
    const cx = (a.x + b.x) / 2 - dy / l * bend, cy = (a.y + b.y) / 2 + dx / l * bend;
    const n = Math.max(4, Math.ceil(l / 2));
    const points = [];
    for (let i = 0; i <= n; i++) {
      const t = i / n, u = 1 - t;
      points.push({ x: u * u * a.x + 2 * u * t * cx + t * t * b.x, y: u * u * a.y + 2 * u * t * cy + t * t * b.y });
    }
    const t = { a, b, width, kind, points, phase: rng.range(0, 10) };
    a.links.push(b); b.links.push(a);
    return t;
  }

  /** Whether a straight run from a to b keeps clear of every other chamber. */
  function clearRun(chambers, a, b, skip) {
    for (const c of chambers) {
      if (c === a || c === b || c === skip) continue;
      if (segDist(c.x, c.y, a.x, a.y, b.x, b.y) < c.r + 3) return false;
    }
    return true;
  }

  /** The main chain: from the start on one side to the way down near the far side, wandering. */
  function mainChain(rng, boss) {
    const side = rng.int(4);
    const inset = 26, along = rng.range(0.3, 0.7) * T.Size, far = rng.range(0.2, 0.8) * T.Size;
    const P = (s, t, d) => s === 0 ? { x: t, y: d } : s === 1 ? { x: T.Size - d, y: t } : s === 2 ? { x: t, y: T.Size - d } : { x: d, y: t };
    const s = P(side, along, inset), goal = P((side + 2) % 4, far, inset);
    const start = makeChamber(rng, s.x, s.y, rng.pick(T.RadiusStart), 'start');
    const chambers = [start], tunnels = [];
    let cur = start, dx = goal.x - s.x, dy = goal.y - s.y, dl = len(dx, dy);
    dx /= dl; dy /= dl;
    for (let i = 0; i < 9; i++) {
      const toGoal = len(goal.x - cur.x, goal.y - cur.y);
      if (toGoal < 20) break;
      const wobble = rng.range(-0.65, 0.65);
      let nx = dx * Math.cos(wobble) - dy * Math.sin(wobble), ny = dx * Math.sin(wobble) + dy * Math.cos(wobble);
      nx = nx * 0.55 + (goal.x - cur.x) / toGoal * 0.45; ny = ny * 0.55 + (goal.y - cur.y) / toGoal * 0.45;
      const nl = len(nx, ny); nx /= nl; ny /= nl;
      const step = Math.min(rng.pick(T.StepMain), toGoal - 8);
      const r = rng.pick(T.RadiusMain);
      let x = cur.x + nx * step, y = cur.y + ny * step;
      const m = T.Rim + r + 4;
      x = Math.max(m, Math.min(T.Size - m, x)); y = Math.max(m, Math.min(T.Size - m, y));
      const next = makeChamber(rng, x, y, r, 'main');
      chambers.push(next);
      tunnels.push(makeTunnel(rng, cur, next, rng.pick(T.WidthMain), 'Main'));
      cur = next; dx = nx; dy = ny;
    }
    const er = boss ? T.ArenaRadius + 2 : T.ExitRadius + 2.5;
    const m = T.Rim + er + 4;
    const exit = makeChamber(rng, Math.max(m, Math.min(T.Size - m, goal.x)), Math.max(m, Math.min(T.Size - m, goal.y)), er, 'exit');
    if (boss) exit.harm.forEach(h => { h.a *= 0.3; });   // the arena stays round
    chambers.push(exit);
    tunnels.push(makeTunnel(rng, cur, exit, rng.pick(T.WidthMain), 'Main'));
    return { chambers, tunnels, start, exit };
  }

  /** One side branch off a chamber (and now and then a second tunnel making a loop). Null when it does not fit. */
  function branch(rng, g) {
    const options = g.chambers.filter(c => c.role !== 'exit');
    const parent = options[rng.int(options.length)];
    const a = rng.range(0, Math.PI * 2), step = rng.pick(T.StepBranch), r = rng.pick(T.RadiusBranch);
    const x = parent.x + Math.cos(a) * (parent.r + step), y = parent.y + Math.sin(a) * (parent.r + step);
    if (!inBounds(x, y, r)) return null;
    for (const c of g.chambers) if (len(c.x - x, c.y - y) < c.r + r + 3) return null;
    const c = makeChamber(rng, x, y, r, 'branch');
    if (!clearRun(g.chambers, parent, c, null)) return null;
    for (const t of g.tunnels) {
      if (t.a === parent || t.b === parent) continue;
      for (const p of t.points) if (len(p.x - x, p.y - y) < r + t.width / 2 + 1) return null;
    }
    const added = { chamber: c, tunnels: [makeTunnel(rng, parent, c, rng.pick(T.WidthBranch), 'Side')] };
    if (rng.float() < T.LoopChance) {
      let best = null, bd = T.LoopReach;
      for (const o of g.chambers) {
        if (o === parent || o.role === 'exit') continue;
        const d = len(o.x - x, o.y - y) - o.r - r;
        if (d < bd && clearRun(g.chambers, c, o, parent)) { bd = d; best = o; }
      }
      if (best) added.tunnels.push(makeTunnel(rng, c, best, rng.pick(T.WidthLoop), 'Pass'));
    }
    return added;
  }

  function unlink(added) {
    for (const t of added.tunnels) {
      t.a.links.splice(t.a.links.indexOf(t.b), 1);
      t.b.links.splice(t.b.links.indexOf(t.a), 1);
    }
  }

  // ─────────────────────────── rasterising: the water ───────────────────────────

  /**
   * The signed distance to the cave (negative inside), on the 1 m grid: chambers as wobbly blobs, tunnels as winding
   * tubes whose width breathes along them, all roughened by noise so the outline crags like a real cave.
   */
  function field(g, seed) {
    const N = T.Size + 1, sdf = new Float32Array(N * N).fill(1e9), core = new Uint8Array(N * N);
    for (const c of g.chambers) {
      const reach = c.r * 1.6 + 2;
      for (let y = Math.max(0, Math.floor(c.y - reach)); y <= Math.min(T.Size, Math.ceil(c.y + reach)); y++)
        for (let x = Math.max(0, Math.floor(c.x - reach)); x <= Math.min(T.Size, Math.ceil(c.x + reach)); x++) {
          const ang = Math.atan2(y - c.y, x - c.x);
          let k = 1;
          for (const h of c.harm) k += h.a * Math.cos(h.k * ang + h.p);
          const d = len(x - c.x, y - c.y) - c.r * k, i = y * N + x;
          if (d < sdf[i]) sdf[i] = d;
        }
    }
    for (const t of g.tunnels) {
      const pts = t.points, reach = t.width + 3;
      for (let s = 0; s < pts.length - 1; s++) {
        const a = pts[s], b = pts[s + 1];
        const x0 = Math.max(0, Math.floor(Math.min(a.x, b.x) - reach)), x1 = Math.min(T.Size, Math.ceil(Math.max(a.x, b.x) + reach));
        const y0 = Math.max(0, Math.floor(Math.min(a.y, b.y) - reach)), y1 = Math.min(T.Size, Math.ceil(Math.max(a.y, b.y) + reach));
        const half = t.width / 2 * (1 + 0.25 * Math.sin(t.phase + s * 0.55));
        for (let y = y0; y <= y1; y++)
          for (let x = x0; x <= x1; x++) {
            const along = segDist(x, y, a.x, a.y, b.x, b.y), d = along - half, i = y * N + x;
            if (d < sdf[i]) sdf[i] = d;
            // The tunnel's middle stays swimmable whatever the noise does to its walls.
            if (along < 1.6) core[i] = 1;
          }
      }
    }
    for (let y = 0; y < N; y++)
      for (let x = 0; x < N; x++) {
        const i = y * N + x;
        sdf[i] += fbm(x * 0.12, y * 0.12, seed) * T.Crag + fbm(x * 0.37, y * 0.37, seed + 31) * T.CragFine;
        if (core[i]) sdf[i] = Math.min(sdf[i], -1);
        // Never into the rim.
        const edge = Math.min(x, y, T.Size - x, T.Size - y);
        if (edge < T.Rim + 1) sdf[i] = Math.max(sdf[i], T.Rim + 1 - edge);
      }
    return sdf;
  }

  /** Open water: the field's inside, smoothed twice (a majority vote), keeping only what joins the start. */
  function water(sdf, start) {
    const N = T.Size + 1;
    let open = new Uint8Array(N * N);
    for (let i = 0; i < open.length; i++) open[i] = sdf[i] < 0 ? 1 : 0;
    for (let pass = 0; pass < 2; pass++) {
      const next = new Uint8Array(N * N);
      for (let y = 1; y < N - 1; y++)
        for (let x = 1; x < N - 1; x++) {
          let n = 0;
          for (let j = -1; j <= 1; j++) for (let k = -1; k <= 1; k++) n += open[(y + j) * N + x + k];
          next[y * N + x] = n >= 5 ? 1 : 0;
        }
      open = next;
    }
    // Keep what joins the start.
    const keep = new Uint8Array(N * N), si = Math.round(start.y) * N + Math.round(start.x);
    if (!open[si]) return keep;
    const queue = [si];
    keep[si] = 1;
    while (queue.length) {
      const i = queue.pop(), x = i % N, y = (i - x) / N;
      for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
        const nx = x + dx, ny = y + dy;
        if (nx < 0 || ny < 0 || nx >= N || ny >= N) continue;
        const j = ny * N + nx;
        if (open[j] && !keep[j]) { keep[j] = 1; queue.push(j); }
      }
    }
    return keep;
  }

  const count = (open) => { let n = 0; for (let i = 0; i < open.length; i++) n += open[i]; return n; };

  /** Distances through the water from a cell (8-neighbour Dijkstra; Infinity where it cannot be reached). */
  function geodesic(open, sx, sy) {
    const N = T.Size + 1, dist = new Float64Array(N * N).fill(Infinity);
    const s = Math.round(sy) * N + Math.round(sx);
    if (!open[s]) return dist;
    dist[s] = 0;
    // A binary heap of [distance, index].
    const heap = [[0, s]];
    const push = (e) => { heap.push(e); let i = heap.length - 1; while (i > 0) { const p = (i - 1) >> 1; if (heap[p][0] <= heap[i][0]) break; [heap[p], heap[i]] = [heap[i], heap[p]]; i = p; } };
    const pop = () => { const top = heap[0], last = heap.pop(); if (heap.length) { heap[0] = last; let i = 0; for (;;) { const l = i * 2 + 1, r = l + 1; let m = i; if (l < heap.length && heap[l][0] < heap[m][0]) m = l; if (r < heap.length && heap[r][0] < heap[m][0]) m = r; if (m === i) break; [heap[m], heap[i]] = [heap[i], heap[m]]; i = m; } } return top; };
    const steps = [[1, 0, 1], [-1, 0, 1], [0, 1, 1], [0, -1, 1], [1, 1, Math.SQRT2], [1, -1, Math.SQRT2], [-1, 1, Math.SQRT2], [-1, -1, Math.SQRT2]];
    while (heap.length) {
      const [d, i] = pop();
      if (d > dist[i]) continue;
      const x = i % N, y = (i - x) / N;
      for (const [dx, dy, w] of steps) {
        const nx = x + dx, ny = y + dy;
        if (nx < 0 || ny < 0 || nx >= N || ny >= N) continue;
        const j = ny * N + nx;
        if (!open[j]) continue;
        // No squeezing diagonally between two rock corners.
        if (dx && dy && (!open[y * N + nx] || !open[ny * N + x])) continue;
        if (d + w < dist[j]) { dist[j] = d + w; push([d + w, j]); }
      }
    }
    return dist;
  }

  /** Chamfer distance to the nearest cell of a kind (0 on it): to water from rock, or to rock from water. */
  function distanceTo(open, want) {
    const N = T.Size + 1, d = new Float32Array(N * N);
    for (let i = 0; i < d.length; i++) d[i] = open[i] === want ? 0 : 1e9;
    const D = Math.SQRT2;
    for (let y = 0; y < N; y++)
      for (let x = 0; x < N; x++) {
        const i = y * N + x;
        if (x > 0) d[i] = Math.min(d[i], d[i - 1] + 1);
        if (y > 0) d[i] = Math.min(d[i], d[i - N] + 1);
        if (x > 0 && y > 0) d[i] = Math.min(d[i], d[i - N - 1] + D);
        if (x < N - 1 && y > 0) d[i] = Math.min(d[i], d[i - N + 1] + D);
      }
    for (let y = N - 1; y >= 0; y--)
      for (let x = N - 1; x >= 0; x--) {
        const i = y * N + x;
        if (x < N - 1) d[i] = Math.min(d[i], d[i + 1] + 1);
        if (y < N - 1) d[i] = Math.min(d[i], d[i + N] + 1);
        if (x < N - 1 && y < N - 1) d[i] = Math.min(d[i], d[i + N + 1] + D);
        if (x > 0 && y < N - 1) d[i] = Math.min(d[i], d[i + N - 1] + D);
      }
    return d;
  }

  // ─────────────────────────── places ───────────────────────────

  function placePlaces(rng, g, kinds, open, fromStart, clearance) {
    const N = T.Size + 1;
    const at = (x, y) => Math.round(y) * N + Math.round(x);
    const spots = [];
    // Chambers first (dead ends before the rest), then points along the tunnels.
    const chambers = g.chambers.filter(c => c.role === 'branch' || c.role === 'main');
    chambers.sort((a, b) => (a.links.length - b.links.length) || (rng.float() - 0.5));
    for (const c of chambers) spots.push({ x: c.x, y: c.y, chamber: c, deadEnd: c.links.length === 1 });
    for (const t of g.tunnels) for (let i = 2; i < t.points.length - 2; i += 3) spots.push({ x: t.points[i].x, y: t.points[i].y, chamber: null, deadEnd: false });
    const pois = [];
    const fits = (s, r, lo, hi) => {
      const i = at(s.x, s.y);
      if (!open[i] || clearance[i] < Math.min(r, 6) * 0.8) return false;
      const d = fromStart[i];
      if (!(d >= lo && d <= hi)) return false;
      if (len(s.x - g.exit.x, s.y - g.exit.y) < T.PlaceFromExit + g.exit.r * 0) return false;
      return pois.every(p => len(p.x - s.x, p.y - s.y) >= T.PlaceSpacing);
    };
    // Exactly one early place, 39–47 m from the start along the water: the first kind that fits.
    let early = -1;
    for (let k = 0; k < kinds.length && early < 0; k++)
      for (const s of spots)
        if (fits(s, PlaceRadius[kinds[k]], T.EarlyMin, T.EarlyMax)) { pois.push({ kind: kinds[k], x: s.x, y: s.y, radius: PlaceRadius[kinds[k]], early: true }); early = k; s.used = true; break; }
    if (early < 0) return null;
    // The rest beyond the early band; rooms (shop, treasure, secret, curse) like dead ends.
    const rest = kinds.filter((_, i) => i !== early);
    for (const kind of rest) {
      const room = kind === PoiKind.Shop || kind === PoiKind.TreasureCave || kind === PoiKind.Secret || kind === PoiKind.CurseDen;
      const ordered = room ? spots.filter(s => s.deadEnd).concat(spots.filter(s => !s.deadEnd)) : spots;
      for (const s of ordered) {
        if (s.used || !fits(s, PlaceRadius[kind], T.EarlyMax + 0.01, Infinity)) continue;
        pois.push({ kind, x: s.x, y: s.y, radius: PlaceRadius[kind], early: false });
        s.used = true;
        break;
      }
    }
    return pois;
  }

  // ─────────────────────────── heights ───────────────────────────

  function heights(rng, open, toWater, seed, shaft) {
    const N = T.Size + 1, h = new Float32Array(N * N);
    const peak = rng.range(T.PeakMin, T.PeakMax);
    for (let y = 0; y < N; y++)
      for (let x = 0; x < N; x++) {
        const i = y * N + x;
        if (open[i]) h[i] = T.Floor + 0.2 * fbm(x * 0.2, y * 0.2, seed + 3);
        else {
          const d = toWater[i];
          h[i] = Math.min(peak, 0.4 + 1.5 * d) + 1.2 * fbm(x * 0.15, y * 0.15, seed + 5) * Math.min(1, d / 3);
          h[i] = Math.max(h[i], 0.3);
        }
      }
    // The shaft: a blue hole, or on a boss level the Crack (an ellipse).
    for (let y = 0; y < N; y++)
      for (let x = 0; x < N; x++) {
        const i = y * N + x;
        if (!open[i]) continue;
        const dx = x - shaft.x, dy = y - shaft.y;
        const u = (dx * Math.cos(shaft.angle) + dy * Math.sin(shaft.angle)) / shaft.halfLength;
        const v = (-dx * Math.sin(shaft.angle) + dy * Math.cos(shaft.angle)) / shaft.halfWidth;
        const q = u * u + v * v;
        if (q < 1) h[i] = T.Floor + (T.ShaftBottom - T.Floor) * Math.min(1, (1 - q) * 3);
      }
    return h;
  }

  // ─────────────────────────── the level ───────────────────────────

  /**
   * One level from a seed. boss: a boss level (its way down is the arena and the Crack). Returns a LevelMap-shaped
   * object (see the bottom of this file) or throws when no attempt meets the rules.
   */
  function generate(seed, opts) {
    const boss = !!(opts && opts.boss);
    const root = new Rng(seed);
    const kinds = planPlaces(root.stream('plan'), boss);
    const lo = T.Area * (1 - T.AreaSlack), hi = T.Area * (1 + T.AreaSlack);
    let last = null;
    for (let attempt = 0; attempt < T.Attempts; attempt++) {
      const rng = root.stream('attempt ' + attempt);
      const noiseSeed = rng.int(1 << 30);
      const g = mainChain(rng, boss);
      if (len(g.exit.x - g.start.x, g.exit.y - g.start.y) < T.MinStraight) { last = 'straight'; continue; }
      // Grow side branches until the water covers the area rule: first by estimate (cheap), then measured.
      const estimate = () => g.chambers.reduce((s, c) => s + Math.PI * c.r * c.r, 0)
        + g.tunnels.reduce((s, t) => s + Math.max(0, len(t.b.x - t.a.x, t.b.y - t.a.y) - t.a.r - t.b.r) * t.width, 0);
      let open = water(field(g, noiseSeed), g.start), area = count(open), misses = 0;
      const ratio = area / estimate();
      while (estimate() * ratio < T.Area && misses < 300) {
        const added = branch(rng, g);
        if (!added) { misses++; continue; }
        g.chambers.push(added.chamber);
        g.tunnels.push(...added.tunnels);
      }
      open = water(field(g, noiseSeed), g.start); area = count(open);
      misses = 0;
      while (area < T.Area && misses < 300) {
        const added = branch(rng, g);
        if (!added) { misses++; continue; }
        g.chambers.push(added.chamber);
        g.tunnels.push(...added.tunnels);
        const next = water(field(g, noiseSeed), g.start), a = count(next);
        if (a > hi) {
          g.chambers.pop();
          g.tunnels.length -= added.tunnels.length;
          unlink(added);
          misses++;
          continue;
        }
        open = next; area = a;
      }
      if (area < lo || area > hi) { last = 'area ' + area; continue; }
      const fromStart = geodesic(open, g.start.x, g.start.y);
      const N = T.Size + 1;
      const pathToExit = fromStart[Math.round(g.exit.y) * N + Math.round(g.exit.x)];
      if (!isFinite(pathToExit) || pathToExit < T.MinPath) { last = 'path ' + pathToExit; continue; }
      const toWater = distanceTo(open, 1), clearance = distanceTo(open, 0);
      const pois = placePlaces(rng, g, kinds, open, fromStart, clearance);
      if (!pois) { last = 'no early place'; continue; }
      const shaft = boss
        ? { x: g.exit.x, y: g.exit.y, angle: rng.range(0, Math.PI), halfLength: rng.range(7, 9), halfWidth: rng.range(2.5, 3.5), crack: true }
        : { x: g.exit.x, y: g.exit.y, angle: 0, halfLength: T.BlueHoleRadius, halfWidth: T.BlueHoleRadius, crack: false };
      const h = heights(rng, open, toWater, noiseSeed, shaft);
      const start = { kind: PoiKind.Start, x: g.start.x, y: g.start.y, radius: T.StartRadius, early: false };
      const exit = { kind: PoiKind.Exit, x: g.exit.x, y: g.exit.y, radius: boss ? T.ArenaRadius : T.ExitRadius, early: false };
      return {
        // The LevelMap contract.
        Size: T.Size, Samples: N, Rim: T.Rim, Heights: h, HasBoss: boss,
        Start: start, Exit: exit, Pois: [start, exit, ...pois],
        Plazas: g.chambers.map(c => ({ x: c.x, y: c.y, radius: c.r, role: c.role })),
        Corridors: g.tunnels.map(t => ({ kind: t.kind, width: t.width, points: t.points })),
        Shaft: shaft,
        // For the prototype page.
        Open: open, Area: area, PathToExit: pathToExit, Attempt: attempt, Planned: kinds,
      };
    }
    throw new Error('No level met the rules (' + last + ')');
  }

  const api = { generate, Rng, T, PoiKind, fbm, hash2 };
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  else root.CaveGen = api;
})(typeof window !== 'undefined' ? window : globalThis);
