# Ink Deep — Top-Down Redesign (v0.1, draft)

> A top-down action roguelite set in a darkening ocean. You are a small
> bioluminescent jellyfish, the last of your bloom, diving after your family
> through a reef poisoned by the Leak. Pearls you gather are consumed by your
> bell — each one a new glowing pattern on your body, each combination a new
> build, each dive a step into darker water.
>
> This document defines the new game. **Everything not explicitly changed here
> is inherited** from `DESIGN-3D.md` (current live rules) and, where noted,
> `DESIGN-2D.md`. Inherited systems are referenced by section, not repeated.
>
> **The theme guideline is [`THEME-BIBLE.md`](THEME-BIBLE.md):**
> - the pillars, the palettes, the corruption and freeing language, the audio identity, the tone of voice;
> - the theme tests (§12) that every new feature must pass.
>
> This document stays authoritative for rules and numbers.

---

## 0. Decisions

| Topic | Decision |
|---|---|
| View | **Top-down, tilted camera ~60°** (Below-style), perspective (not orthographic) for flora/parallax. |
| Depth handling | **Plane-locked**: the whole sim runs at one depth band per level; no free vertical movement. |
| Player | **Clementine**, a one-of-her-kind **absorbent jellyfish** — she absorbs the skills locked inside pearls (§2). |
| Pearls | **Consumed by the bell** — each pearl adds a permanent glowing pattern to the bell (§2.4, §7). |
| Shooting | **Bubbles**, mouse-aim (twin-stick optional), ammo visible as orbs under the bell. |
| Levels | **Static 125×125 m seeded squares**, reef-ringed; free swimming inside; one way down — a blue hole, or on a depth's boss level the Crack in the boss arena; caves as special rooms (§4). |
| Level gen | **Plan → POIs → 3–4 intersecting paths → topographic reef**; the rock around the start and the hole shared with the levels above and below; mountains never on paths; arches over paths; caves & passes in ridges (§4.1, §4.6). |
| Occlusion | Canopy layer (arch bodies, cave roofs) **blurs out and fades** while Clementine is underneath (§3, §4.5). |
| Lighting | **The core visual pillar.** Atmospheric, volumetric, per-depth darkening (§5). |
| Engine | **Godot 4 / C# retained** — plane-locked rework of the existing stack (§11). |
| Story | **"Ink Deep" lore inherited** (DESIGN-2D §1): the Leak, the Crack, corrupted-not-killed creatures, the Tank, The Hand. Clementine, the one-of-her-kind absorbent jellyfish, dives after her bloom (§1). |

---

## 1. Pitch & Lore — adapted

The premise is inherited unchanged: the Crack opened, the Leak bleeds tank-water
into the reef, corrupted creatures are **freed, not killed** (they burst back to
true colours), and every dive ends at the pipe into the **Tank** and **The Hand**.

What changes is the protagonist's skin:

- You are **Clementine**, a juvenile **absorbent jellyfish** — the only one of
  her kind, and for good reason: her bell is the reef's perfect counter-adaptation.
  Where the Leak rewrites creatures from the outside, Clementine takes things *in* —
  swallow a pearl and the **skill locked inside it is absorbed**, worn as living
  light on her bell. Her entire **bloom** — the drifting cloud of siblings she has
  never been apart from — was pulled down into the Crack in a single current.
  She follows, because she is the only one left who glows the family colour.
- **Why a jellyfish matters thematically:** a jellyfish drifts; she cannot grab,
  hold, or anchor. Everything she carries, she carries *inside* — and the absorbent
  gift makes that literal. Her family used to say a jellyfish's bell is her heart
  wearing a lantern.
- **The Tank twist lands harder:** a jellyfish in a fish tank is a decorative
  object. The horror of the reveal is not "I was watched" but "**I was kept**."
  The Hand fight is a pet reaching into the bowl.
- **Name:** Clementine — reclaimed from the octopus era; she kept the name and
  changed species.
- Foreshadowing, Menace curve, freed-creature poofs, comic-panel endings:
  all inherited (DESIGN-2D §1, §5.3).

---

## 2. The Player — Clementine the Absorbent Jellyfish

### 2.1 Body
- **Bell**: translucent tangerine dome, ~0.84 m across, with a scalloped
  margin of sixteen lappets. Seen through it: four gold gonad horseshoes round
  the stomach, radial and ring canals, warts of stinging cells, and eight rim
  organs in the margin's notches. Breathing rhythm at rest; every stroke is a
  contraction beat (quick squeeze, slow relax, a slight recoil flare) with a
  light wave running base→rim. The bell leans into the swim on a critically
  damped spring.
- **Tentacles**: sixteen thin marginal tentacles (one per notch, 1.35–1.95 m)
  and eight ruffled oral arms on **verlet chains** with a fixed 120 Hz step:
  they stream behind her in travelling S-waves while she swims, swing through
  turns, are pushed back by each stroke, and float out around her in a slow
  current when she stops. Drawn as camera-facing ribbons, always over the
  scene (never cut away). Presentation only: the sim never sees them.
- **Bioluminescence**: a soft point light carried at the bell; the player's glow
  is the primary light source in dark depths (inherited rule: *Clementine is
  always the warm light in the scene*).
- **The Absorbent:** one of her kind. A pearl swallowed is a skill absorbed —
  the bell dissolves it into light and keeps the pattern forever (§2.4).
- **No eyes on the UI — eyes on the body**: the bell's rim organs pulse when
  hurt (magenta flash), when an active item is ready (gold pulse), when a pearl
  is consumed (chromatic ripple running base→rim).

### 2.2 Movement
- Free 8-direction swimming on the locked plane. **Pulse propulsion** (inherited
  jellyfish feel, DESIGN-2D §11.2): movement plays a contraction pulse; a
  direction change applies recoil on the contraction beat, so motion always has
  that signature jellyfish *lurch-then-glide*. Responsive first: input overrides
  pulse timing within 0.1 s.
- **Idle drift**: with no input, Clementine slows, bell relaxes, tentacles float
  up around her. The old "slow sink" rule becomes a gentle ~1 m bob on the depth
  plane (no floor settling — there is no floor contact at her depth band).
- **Jet start** inherited (DESIGN-2D §20): first stroke from rest is a 1.8×
  burst over 0.38 s; sharp turns (>110°) re-trigger it.
- **Ink dash** inherited (DESIGN-2D §30): Space, ~3.4× speed, 0.32 s
  untouchable, 0.85 s cooldown, leaves a slowing ink cloud. (Yes, a jellyfish
  inks — the Leak changed her. The Sea-pedia entry says so.)
- Base swim speed 4.5 m/s equivalent, scaled by the `speed` stat.

### 2.3 Combat — bubbles
- **LMB** releases a bubble; hold for auto-fire at the fire-rate stat; hold-long
  for charge items (Pearl Diver, Sunbeam).
- **Bubbles are ammunition**: each bubble rests as a **glowing orb beneath the
  bell**, orbiting slowly between the oral arms — visible diegetic ammo. Base
  8; they **regrow** one per 0.35 s starting 0.6 s after the last shot (inherited
  timings, DESIGN-3D §4.1). Items modify capacity/regrowth/multithrow exactly as
  in the 3D doc (Bubble Gland, Anemone Pump, Twin Siphon).
- Bubbles are slow, physical, iridescent-rimmed orbs; they pop at end of range
  (9.75 m base); they inherit 30% of Clementine's velocity (inherited Isaac rule).
- **Mouse aim with aim assist** inherited (DESIGN-3D §4.2 tiers: Off/Low/Medium/
  High — magnetism cone, bullet bend, sensitivity slow).
- Shot modifiers in 2D: spiral → flat spiral; wave → counter-phase pair; bounce
  → reflects off reef/rock normals; split → ±25° in the aim plane; multishot →
  fan; orbit → ring around Clementine; boomerang → returns to her; rear fin →
  opposite the aim; Kraken Form → 8-way volley. Homing/pierce/spectral/status
  effects unchanged.

### 2.4 Pearls — absorbed by the bell (the signature system)
- Pearls are **not held in inventory — they are absorbed.** Clementine swims
  over a pearl in its clam; the bell draws it in with a soft suction, the pearl
  dissolves into light, and its glow spreads through her translucent tissue and
  **stabilizes as a permanent glowing pattern** on the bell. Each absorption is
  a visible gulp of light — the one animation the whole item economy hangs on.
- **The bell is the build sheet.** Every pearl adds one motif; motifs stack and
  layer. A player glancing at Clementine reads her build the way an Isaac player
  reads a character sprite covered in items. Streamers get body-readable runs
  for free.
- **Motif language** (pattern per item family, color per item pool):

| Family (effect class) | Motif | Example |
|---|---|---|
| Stat up | concentric ring added to bell | Coral Crown → one clean gold ring |
| Bubble modifier | radial ray from bell center | Mitosis → forking twin ray |
| Defensive | scalloped edge band | Barnacle Armor → crusted rim band |
| Economy/luck | scattered dots | Lucky Sea Glass → drifting sparkles |
| Mobility | spiral | Drift Pearl-style → slow swirl |
| Status (burn/poison/freeze) | wavy band | Fire Coral → flickering wave band |
| Active item | central halo glyph | one slot only — halo sits dead center |

  Color = pool of origin (treasure teal, boss gold, siren crimson, curse violet,
  secret white). A Motif × Color grid gives every item a unique bell signature
  with only 6×5 combinations.
- **Synergies and transformations rewrite the pattern.** Named synergies add a
  linking filament between their motifs (two rays joined by a glowing thread);
  **transformations re-theme the whole bell** — Kraken Form turns motifs into
  dark veins, Neon Rave makes all motifs cycle hue, Coral Reef makes them pulse
  softly like breathing coral. (Inherited synergy/transformation tables,
  DESIGN-2D §9.4, §28 tags.)
- **Capacity**: 16 motifs visible on the bell (inherited "8 pairs" spirit);
  pearls beyond 16 apply their effect but show as faint interior glow only.
- Absorption is permanent for the run (Isaac rule: no un-absorbing). Pearls are never dropped or transferred.

### 2.5 Stats & health
- Full Isaac-style stat block inherited (DESIGN-2D §4 table) with the 3D doc's
  bubble-economy mapping. **Numeric health (100 HP)** and the entire damage
  table inherited (DESIGN-2D §23), scaling ×(1 + 0.75·menace).
- Active item: one slot, **recharges over time** (inherited DESIGN-3D §4.4 — no
  rooms to clear; Glow Burst 10 s … Kraken Call 60 s; Brain Coral ×1.35; glow
  jelly +30%).

#### Diegetic vitals — the gonad rings and the bell rim
Clementine's body carries her two most important gauges; there is no HP bar and
no active-item slot on the HUD (only a quiet echo of her health on the minimap's
bezel, §9). Each uses a different part of her anatomy and a
different visual channel, so they can never be confused.

- **The gonad rings — health.** The four glowing cloverleaf rings at the centre
  of the bell are her heart, and her health: each holds one quarter of her max
  HP (25 HP each at the base 100).
  - *Intact:* a full ring glows a steady warm gold, breathing gently with her
    pulse.
  - *Damaged:* the rings never change colour; as quarters are lost they fade
    out one by one, ever more transparent (a ring holding part of its quarter is
    as opaque as that part), until an empty ring is gone into the bell. Healing
    brings them back in order. The rings follow her HP smoothly (draining at
    most 0.8 of her max a second, returning at 1.5), so a lost quarter visibly
    fades rather than blinking off.
  - *Critical:* at a quarter or less, the last ring's light pulses slowly in and
    out — a dying light at the centre of her body, readable in peripheral
    vision without looking away from the fight.
  - The centre of the bell is where the eye rests on a jellyfish, which is why
    health — the thing she must always know — lives there.
- **The bell rim — active-pearl charge.** The glowing marginal band at the
  outer edge of the bell is her readiness gauge, a fuse of light drawn on her
  body. With no active pearl it is plain margin.
  - *Charging:* a light sweeps clockwise round the rim from the top of the
    screen (her slow turn is undone, so the top stays the top), filling the
    circle in proportion to the recharge, a bright head at its front; the unlit
    part is a dim track. Fast pearls race round; slow ones creep.
  - *Ready:* the full ring settles into a slow gold breathing pulse — the bell
    itself says *now*.
  - *On use:* the gold whips off the rim counter-clockwise in one snuffed-fuse
    flash (0.35 s), and the sweep begins again.
  - The rim is a continuous circle at the silhouette's edge: visible from any
    camera angle, drawn over the silhouette's dark edge, and far from the
    gonad rings.

| | Gonad rings (health) | Bell rim (charge) |
|---|---|---|
| Position | Centre of bell | Outer edge |
| Structure | Four discrete circles | One continuous ring |
| Channel | Opacity (lit / faded) | Motion (sweeping fill) |
| Tempo | Changes only on hit or heal | Always moving |

The only overlap is the magenta hurt flash, which washes the whole bell for a
moment — on purpose: being hurt is an interrupt and should override everything
for a fraction of a second.

---

## 3. Camera & Presentation

- **Tilted top-down, ~55–65°**, perspective projection (narrow FOV ~35°) so tall
  flora, arches and reef walls parallax softly; keeps Below's "map feeling"
  without flatness.
- Gentle scroll smoothing; **no camera rotation** (north is always up; the
  minimap and world agree).
- Screen-space dressing: marine snow drifting *between camera and level*
  (parallax speed reference), faint refraction wobble, vignette that deepens
  with depth, subtle chromatic aberration on hurt.
- **Overhead occlusion rule.** Arch bodies, cave roofs and overhang ledges render
  on a **canopy layer** above the gameplay plane. The moment Clementine passes
  underneath — she enters a cave or swims through an arch — the canopy **blurs
  out and dissolves** around her and stays gone for as long as she remains
  underneath, restoring when she exits. This is what makes the reef read as
  layered and physical rather than painted flat (§4.5, §11).
- **Camera breaks (only two, ever):** boss-awakening dips (2 s low-angle swell
  toward the boss) and the **Tank transition** — the Crack sucks Clementine down
  through a vertical shaft in a short cutscene (the one time true verticality is
  shown), then the camera settles back to tilted top-down inside the tank.

---

## 4. The Level — one seeded square, generated from paths

### 4.0 The contract
- **Static footprint: 125×125 m on every level, every depth** (1 m grid).
  Difficulty comes from content, menace and light — never from map size.
- The square is **ringed by an impassable reef wall** — cliffs and coral
  ramparts; the rim is scenery and collision, never a soft boundary.
- **A depth is a chain of 4–5 levels** (§8). **Every level has exactly one way
  out:** a blue hole in its floor, or — on the depth's boss level — the Crack in
  the boss arena, which opens when the boss is freed. She dives down it (§4.6),
  or perishes. No return to shallower levels within a run.
- **Pacing contract (the master knobs):** rushing start→exit takes **~1–1.5
  min**; looking under every rock takes **~6–7 min**; a depth takes ~30 min.
  Every number in §4.5 exists to hit that, and the generation pipeline below is
  built around it.

### 4.1 Generation pipeline (one run seed; independent sub-stream per stage)

**The feel:** swimming through a labyrinth of canyon channels — but under
water. Heights are measured from the **swim level (0)**: Clementine floats on
it. Anything above 0 is an obstacle; anything at or below 0 is open water she
floats over, however deep the floor lies beneath.

**Stage 0 — The plan, the stamps, the seabed.** A level's plan is rolled
from the run seed before anything is shaped (`LevelPlan`): whether its boss
stands here, which places it holds (Stage 1), its Menace, and the seeds of its
two **stamps** — the rock around its start (shared with the hole of the level
above) and the rock around its exit (shared with the start of the level below;
§4.6). Every attempt starts from a bare seabed at **level −1** (1 m under the
swim level). Everything else is placed on it, and the reef is raised out of it
afterwards.

**Stage 1 — POIs.** The named places come first, scattered on the bare
seabed by the rules below.
- **Start** — on a border band (its centre 24 m in from the edge, so its stamp
  clears the rim), on a side where one of its stamp's canyons opens toward the
  exit. On whole metres, like the exit: two levels then sample their shared
  stamp at the very same points.
- **Exit** — far from the start, each end's stamp opening toward the other;
  hard constraint: geodesic distance from start **≥ 90 m**. On most levels a
  **blue hole**: an 8 m clearing with a 6.5 m shaft in its middle. On the
  depth's boss level the **boss arena**: a round, level arena at level −1,
  ~30 m across — room for the fight — with **the Crack** across its middle, a
  fissure 14–18 m long; the Crack is the shaft.
- **The level's places** come from its plan. At most **5** besides the start
  and the exit (the boss arena counts); over the cap they are dropped in the
  order ambushes, curse den, secret, shell cache (treasure rooms stay):

  | Place | Level 1 of a depth | Level 2+ |
  |---|---|---|
  | Treasure room | always 1 | at most 2 per depth: a 2nd at 50%, on one level from 2 to the boss level |
  | Secret room | 40% | 40% |
  | Curse den | 30% | 30% |
  | Shop | never | 45% |
  | Shell cache | never | 60% |
  | Ambush | 0–2 | 0–2 |
  | Boss arena | — | level 4 at 50%, else level 5 |

  The shop, secrets, treasure rooms and curse den sit in caves (Stage 4);
  ambushes and the shell cache are open clearings. The shell cache sits at
  40–60% of the start→exit axis, preferentially *off* the main corridors
  (rewards leaving the path).
- **Scatter rules:** places ≥ 21 m apart (caves ≥ 25 m), ≥ 40 m from the exit
  and ≥ 39 m from the start (their keep-outs never reach a stamp), and clear of
  the stamps' canyon lines; exactly one sits "early", 39–47 m from the start —
  the shop if the level has one (usually; to teach the economy), else a
  treasure room, else another place. The shell cache never sits early (it keeps
  to the middle of the way), so a level whose only place is the cache has no
  early place.

**Stage 2 — Route canyons.** 3–4 canyons from start to exit, laid out
between the places, before any rock exists:
- Deliberately varied routes: one near-direct, wide arcs left and right, an
  S-curve through the middle. Canyon width **6–10 m**, floor at the seabed
  (level −1).
- **Through the stamps:** each route leaves the start and reaches the exit
  straight down one of the stamps' canyons (anchored every few metres, so the
  line stays inside the canyon); routes share those canyons; a stamp's other
  canyons become dead-end side canyons. Past its canyon a route keeps outside
  the stamp's rock.
- Corridors **intersect 1–4 times** at seeded crossing nodes; each crossing
  opens into a small plaza (~14 m) — natural fight arenas and orientation
  landmarks.
- **Routes keep apart:** away from the stamps and their crossings, routes do
  not run side by side — they spread to leave a ridge between them. Two routes
  may run closer than 16 m (centre to centre) for at most 30 m; otherwise the
  layout regenerates.
- **Places are passed on one side:** a route bends round each place (or each
  group of places too close together to pass between) on the side its first
  draft lies on.
- **Connectivity guarantee:** every POI joins the network via a spur corridor
  (≤ 24 m). Validation: flood-fill from start must reach every POI and the
  exit; otherwise regenerate from the next sub-seed (inherited retry rule).

**Stage 3 — The canyon labyrinth.**
- **Side canyons** (4–6.5 m wide) branch off the route canyons and off each
  other, turning sharply every 8–16 m, mostly into dead ends; now and then one
  breaks through into the canyon it meets, closing a loop. Every canyon keeps
  at least 3.5 m of rock between itself and the next, and none comes near a
  place or a stamp (caves and stamps keep their rock). At least 2 per level;
  they grow until about 63% of the interior is open.
- **Walls** rise out of the seabed everywhere else: from the edge of the open
  ground they climb over 3.5 m with the reef prototype's cosine brush to a
  top 4–14 m high, taken from the prototype's terrain (`reef_generator.html`,
  reproduced exactly for the seed text), so plateaus roll rather than lie flat.
- **The stamps are pinned in:** exactly within 14 m of the start and the exit,
  blending into the level's own rock by 20 m (the blend never raises the
  level's open water; the rim stays impassable through it). Pinned again after
  the caves are raised (Stage 4): the stamp wins.
- **At most 40%** stands above the swim level: where the walls would cover
  more, the cores of the biggest wall masses (farthest from any canyon, then
  lowest on the prototype's terrain) sink into shallow plateaus just under the
  swim level, so the canyon walls themselves stay; a stamp's rock counts but
  never sinks. At least 45% of the interior is water reachable from the start.
- **Hard rule: nothing above 0 on a path** — the canyon floors lie at level −1
  to −1.6 (gently rolling, never flat); the boss arena alone is level.
- **The shaft is the only deep water:** in the exit's floor the floor gives way
  to the next level's seabed, **9 m under the swim level** (the next level's
  swim plane lies 8 m below this one), over a rounded lip. No trenches.
- The impassable rim rises out of the walls with the same brush.

**Stage 4 — Arches, caves, passes:**
- **Arches ×0–5 (where flanks allow; none in a stamp):** peak pairs flanking a
  corridor get arch spans (10–20 m) across the path. Swimming under one triggers the canopy fade (§3). Where
  no flank stands high enough, a pair of short rock ridges is raised either
  side of a corridor to carry one.
- **Caves:** small dead-end pockets (×1–2) and larger multi-chamber caves
  (×1–2) carved into ridge flanks; mouths face a corridor or open water;
  ≥ 25 m apart. Cave interiors host the special rooms (§4.2); where the ridge
  behind is too thin for more chambers, a room settles for a single pocket.
- A cave is hollowed out *inside* a mountain: where the rock over it is too
  low, a peak (8–11 m) rises first as a cosine dome
  and kept off the spur to its mouth. The mountain's surface is kept as the
  cave's roof and drawn as intact rock until Clementine swims inside (canopy
  fade); the chambers beneath have a floor 3.5 m under the swim level — the
  only flat ground in a level.
- **Passes (0–3):** narrow cuts (3–5 m, floor 2.5 m under the swim level) punched
  through ridge saddles — **shortcuts
  that braid the network together**, so the routes read as one reef rather
  than parallel lanes.

**Stage 5 — Reef & decoration from the map:**
- The seabed (under the swim level): sand channels and grass meadows in the
  shallows; bommies/boulders on the deeper floor (below 4 m, never reaching
  the swim level); sea rods, fans and tube sponges on its slopes.
- The reef wall where it breaks the swim level (0 < h < 6 m): flora zonation
  by height + noise — sea rods, fans broadside to the current, tube sponges.
- Steep ground (h ≥ 6 m): reef-rock cliffs, encrusting corals, ledge
  overhangs (canopy); peaks get crown gardens of fans and glowing anemones.
- The shaft: open water all the way down; nothing grows in it. Buried-coin X
  marks and sealed pockets in the seabed under the paths (§4.2).

**Stage 6 — Mob spawn rules:**
- **Dens:** caves and ridge pockets hold themed groups from the depth pool
  (inherited den system).
- **Ambush:** burrowers/clingers seeded 8–12 m off corridor edges, biased to
  plazas and arch landings — where players naturally slow down.
- **Patrols:** 1–2 swimmer packs per level loop along corridor segments
  between intersections; the paths feel watched.
- **Guardians:** POI guards scale with reward — shop lightly guarded,
  treasure medium, shell cache heavy, secrets trap-heavy (mimics).
- **Light bias:** ambient spawns avoid landmark-light radii (beacons are
  breathing room) — except curse dens.
- **Budget:** creature count follows the menace budget (×1.0→1.8 over the
  depths, and on with every loop; §6.2); dormant beyond notice range;
  champions by depth (all inherited).

### 4.2 Special places (caves, inherited from DESIGN-3D §6.4)
- Treasure cave, **Barnaby's shop** (safe water, inherited §24), curse den,
  **secret cave** (plugged by weak rock — one ink bomb), Mermaid's Grotto
  portal after bosses.
- **Landmark light language** inherited: gold (treasure), green (shop),
  red (curse), violet (secret, **no beacon** — found, not advertised),
  orange (the Crack); and white (the start), teal (the shell cache), coral (ambushes; warm but not gold, so
  they never read as treasure). In dark depths these lights are the level's signage —
  Below's campfire principle.
- **Sealed pockets** (3–5/level) and **buried coins** (6–10/level, scratched
  X marks) inherited (DESIGN-2D §26, DESIGN-3D §6.3) — bomb the floor, loot
  below.
- **Destructible reef** inherited: ink bombs dig craters through the floor
  into pockets; beams burn rock; plain bubbles never dig.

### 4.3 The floor
- The floor Clementine swims over is **solid seabed**: nothing of the levels
  below shows through it. The only view down is the shaft (§4.6), through
  which she sees the level below in plain water.
- Depth is told by the light, the palette and the murk of each depth (§5.1),
  and by the dive itself.

### 4.4 Flow on a level (the open-floor pacing contract)
1. Arrive by diving in: she sinks down the shaft from the level above and
   settles at the start, among the same rock that ringed the hole (§4.6).
2. Orient by landmarks: cave beacons, the boss glow.
3. Engage den encounters (waking groups, inherited §19), raid caves, spend at
   the shop, hunt secrets.
4. Find the way down and dive: the blue hole, or on the boss level the arena →
   currents seal it (inherited) → boss → the Crack opens → rewards drop →
   **dive, or die trying.** The inherited Surface Bubble early-exit is **cut**:
   a level offers exactly two endings (§4.0). Progressive depth unlocks
   retained — the Crack stays sealed with a glowing "?" rune until the next
   depth is unlocked (inherited §5.1), and diving into a sealed Crack ends the
   run as a win.

### 4.5 Sizes & timing budget (the master knobs)

| Knob | Value | Why |
|---|---|---|
| Footprint | **125×125 m, static** | a 6–7-minute explore budget per level |
| View on screen | ~30×22 m | a corridor reads at a glance |
| Corridors | 6–10 m wide, under open water | rush speed; nothing blocks |
| Crossing plazas | ~14 m | natural fight arenas |
| Peaks | up to 14 m | occlude and cast shade, never block |
| The shaft | blue hole 6.5 m; the Crack 14–18 m; floor −9 m | the way down |
| Level drop | **8 m** swim plane to swim plane | a real dive that still reads through the fog (§4.6) |
| Stamps | pinned 14 m, blended out by 20 m | the cliffs around the hole carry on below |
| Arches | ×0–5, span 10–20 m | canopy-fade showcase |
| Passes | 3–5 m cuts | network braiding |
| Boss arena | ~30 m disc | room for 2D bullet rings |
| Cruise speed | **6 m/s** (jet 1.8× for 0.38 s; dash 3.4×, 0.85 s cd) | brisk — dash crosses a corridor in ~0.4 s |
| Rush time | **~1–1.5 min** | ~110–140 m route + partial fights |
| Full clear | **~6–7 min** | ≤ 5 places + encounters + caves/shop/secrets |

Rush math: exit ≥ 90 m away by swimming → 20–25 s pure swim at cruise (less
with jet bursts), plus partial fights and maybe one cave. A depth is 4–5
levels: ~5–7 min rushed, ~30 min explored.

### 4.6 The way down — holes, stamps and the dive
- **One way down per level:** a blue hole, or the Crack on the boss level
  (Stage 1). Over it the floor gives way to a shaft; through it she sees the
  level below, through plain water (nothing marks what lies there).
- **The level below lies 8 m down** (swim plane to swim plane), moved so its
  start sits right under the hole. Map coordinates need not match: only the
  rock around the hole does.
- **The stamp:** the rock around a hole is a patch of terrain generated alone
  from its own seed — a clearing (the hole, the start) with 2–3 canyons leaving
  it, or for the Crack the arena's level floor. It is pinned into both levels at
  the same height above each level's swim plane: the mountain to the left of the
  hole and the hill below it are the same mountain and hill around her when she
  arrives, one level lower. Under the hole the level below has a floor (its
  hole is ≥ 90 m from its start, never in view down the shaft), so she can never
  cross two levels at once.
- **Each level depends only on seeds** (the run seed, its id, its two stamp
  seeds): any level can be generated on its own. Two levels live at once — the
  current one and the one below its hole. The one below is made off the main
  thread as soon as she arrives (on one thread, so the game keeps its frame
  rate), shown under the hole, and drawn only within the stamp's ring through
  the shaft; the level she leaves is freed when she has dived. Continue
  regenerates only the saved level and the one below.
- **The dive: Shift** over the shaft. Not in an active battle — while a
  creature that has noticed her is within 12 m, an ambush is fighting her or
  the arena is sealed, the prompt reads "Not while fighting"; the Crack opens
  only when the boss is freed ("The Crack is sealed").
- **The animation (1.6 s, input locked):** she gathers (a flare, a small rise);
  a hard stroke turns her head-first and she sinks the 8 m down the shaft in
  three pulses, tentacles streaming up, then rights herself and settles. The
  camera falls with her and pulls in (to about two-thirds of its distance at
  the middle of the dive), easing in and back out so it is still at the end;
  the level above opens like an iris from the hole outward — from nothing, to
  the shaft's width as she gathers, then sweeping out — revealing the level
  below inside the same ring; the depth focus and the sea surface sink
  with her; through the Crack, the next depth's look blends
  in. The level's life stays behind: its creatures, pickups, shots, damage
  numbers and currents (and Queen Clam) fade out as the iris's dissolving edge
  passes over them, the rest with the descent, casting no shadow once fading;
  the Crack's glow, light and rising bubbles fade as she sinks through it.
  Nothing is switched off in one frame. If the level below is not made yet,
  she holds at the lip of the shaft until it is.
- **Arrival — seamless, no cut:** the level below becomes the level and the
  whole world moves back to the origin in one frame — camera, marine snow, her
  bell and tentacles, her glow on the floor and her halo in the water, all
  moved and drawn at their new places in that same frame — so nothing on screen
  moves. Each level draws its patterns (sand, rock, caustics, flora sway, god
  rays, the specks' glitter) from a fixed absolute origin,
  and its sunlight from its true height under the sea surface (which sank with
  her), so they do not move either (`level_frame.gdshaderinc`). The run
  autosaves.

---

### 4.7 The reef director — the world acts without her
A level is not a stage that waits for the actor. A seeded **reef director**
(`ReefDirector`, Core/Plane) fires world events on its own timers, whether
Clementine is near or not. At level build it draws a schedule from the run's
`events/{cycle}/{depth}/{level}` stream into a priority queue of (time, event),
20 minutes ahead; the sim pops events as the level's clock reaches them.
Deterministic and cheap: the same level always schedules the same events.

- **Current surges** (the first event kind). The first comes 20–40 s into the
  level, each next one 25–50 s after the last has ended (never two at once).
  A surge runs through one canyon (a main route or a side passage; never a
  spur), one way along its course, for **20 s**: it begins slow and
  accelerates (eased over 5 s), holds its force, and decelerates over the last
  5 s. At full strength the flow is 3–4.5 m/s (her cruise is 6: she can swim
  against it, slowly). It changes a fight's geometry mid-encounter.
  - **Felt in the canyon:** inside the canyon's channel the flow pushes at full
    strength, fading to nothing 2 m past its walls. It carries everything not
    fixed to the reef — Clementine, pufferlings (mobs and healthy ones), every
    shot (her bubbles, needles, Queen Clam's pearls), shells, hearts, loose
    pearls, ink clouds — the swimmers sliding along rock as they go. Shop stands
    and Queen Clam hold fast.
  - **Seen beyond it:** pale streaks of moving water race down the canyon and
    spill up to 5 m past its walls, where the current is only cosmetic. They
    appear as the surge builds (the warning), race at full strength, and thin
    out as it eases off.

## 5. Lighting & Atmosphere — the core pillar

The Below principle: **small warm pools of light in overwhelming dark**, except
our "campfires" are bioluminescent and the dark is the ocean itself.

**Her glow lands on the floor.** In daylight bioluminescence does not read as
brightness; it reads as a warm tint on nearby surfaces and as shadow contrast.
So whatever lies below Clementine carries two marks, from the first minute:
- a broad **warm pool** (about 3 m; ×√glow, so Lantern Pearl widens it),
  tinting the floor toward her tangerine — a hue, not a glare, so it shows on
  pale sand too and the reef itself advertises her;
- a small soft **contact shadow** straight under the bell (about 1.2× its
  radius), the dark anchor her bright body needs to pop.

Both are laid on in the water pass (`topdown_post.gdshader`, from the
`her_glow` globals that BellView sets each frame) after the floor's haze and
blur, so neither is washed out; only on what lies below her (never rock over
her head), fading as the gap below her grows and as she turns head-down into
a dive. She is drawn after that pass, so the shadow never darkens her.

### 5.1 Per-depth light budget (drives palette, mood, and difficulty readability)

| Depth | Ambient | God rays | Player glow radius | Primary scenery light |
|---|---|---|---|---|
| 1 Shallows | High — sunlit turquoise | Strong, broad | Small (supplemental) | Sun + caustics |
| 2 Kelp | Dappled green | Broken by canopy | Small | Light shafts |
| 3 Galleon | Amber, dusty | Only through wreck holes | Medium | Lanternfish, loot glints |
| 4 Carnival | Neon but flickering | None — "stage lights" | Medium | Corrupted neon, bounce pads |
| 5 Trench | Near zero | None | **Large — she is the lantern** | Bioluminescent flora specks |
| 6 Abyss | Black | None | **Large, bloom-heavy** | Neon outlines + distant glowing eyes |
| 7 Tank | Flat fluorescent LED | None — *wrong* | Irrelevant — everything is lit | Harsh, shadowless, artificial |

- Menace drives fog density, saturation falloff, and how far the player glow
  carries (inherited "glow items feel precious" presentation rule).
- **Volumetric god rays** rake the floor at the sun's angle in lit depths;
  inherited raymarch water pass, re-aimed: rays now cross the view instead of
  the lens. Caustics play over upward faces and fade with depth (inherited).
- **Fog is also a fairness tool:** creature shots glow in their own color with
  short trails; telegraphs add rim-light flares — readability never drowns in
  mood (inherited §7.2 philosophy).

### 5.2 Living water (cosmetic, inherited §11.4 rules)
Low-res 2D velocity field on the plane; drives bubbles, marine snow, kelp/fan
sway, ink clouds, current streaks. Bosses and dashes inject swirls. Cosmetic
stream only — never touches gameplay physics. Quality presets inherited.

---

## 6. Enemies & Bosses

### 6.1 Classes (2D-native, inherited DESIGN-2D §12.1)
- **Swimmers** — free 2D pursuit on the flow field.
- **Walkers** — floor-bound (crabs, clams, urchin beds).
- **Clingers** — stuck to reef walls/rocks, shoot in patterns.
- **Burrowers** — hide in sand, burst up under Clementine.

### 6.2 Menace (inherited wholesale — DESIGN-2D §5.3)
Same archetypes, same parameter ramp: aggression 40→100%, speed ×1.0→1.5,
projectile speed/count up, telegraph 0.8→0.35 s (never lower), tactics
none→flanking, champion 0→25%, budget ×1.0→1.8. **Visual menace** in 2D:
round eyes → glowing slit eyes, smile → teeth, smooth → jagged silhouette,
desaturation + rim light. A Depth-1 gumdrop is a Depth-6 toothy shadow.
**Menace** is 0 at the first depth and 1 at the seventh, and keeps rising by the
same step through every loop (`LevelPlan.MenaceOf`); it changes only on a dive
through the Crack. In the sim (current pass, the corrupted pufferlings, capped at 3):
creatures ×(1 + 0.4·menace) per level, their speed ×(1 + 0.5·menace), their
needles ×(1 + 0.4·menace) faster, their cooldown ÷(1 + 0.6·menace), and the
telegraph up to 0.35 s shorter (never under 0.45 s); the spawn budget
×(1 + 0.8·menace).

### 6.3 Senses, aggro, fairness (inherited DESIGN-3D §7.2/§7.6)
Notice ranges halve while slow (<0.6 m/s), double for 2 s after shooting/dashing;
ink clouds hide completely; 0.6 s startle + call; leashing back to dens;
**no more than 3 attackers from off-screen** — enforced as "beyond the screen
edge" in top-down. The **clownfish ninja** spec carries over intact (headband
glow telegraph, decoy schoolmates, nest respawn) — it reads *better* top-down,
where hiding among identical fish is visual, not camera-based.

### 6.4 Bosses
- A depth's boss stands on its last level (level 4, or 5). Arenas: wide grottoes with the Crack across the floor —
  the level's only way down. On entry an invisible wall seals them and a mud cloud rises around them (the top-down
  stand-in for the inherited current walls; see Queen Clam in §12.2).
- Patterns translate from "rings, spheres, sweeping planes" to 2D: rings, fans,
  sweeping lines, rotating beams, spawning adds. Every boss keeps 2–3 phases,
  comic-cover title cards (inherited), and **freed-not-killed** poofs.
- Roster inherited: Gus, Queen Clam, Kelpie, Sir Urchin, Rusty Admiral, Treasure
  Mimic, Ringmaster Octo, Jester Jellies, Mama Otter, Captain Sawtooth, Punchy,
  Mother Angler, Giant Squid, Siphonophore, Frilled Shark, Sea Spider, Hollow Maw.

### 6.5 The Hand — restaged for top-down
The FP staging ("look up through the surface") is replaced:

- The arena is the tank floor seen from above: neon gravel, plastic plants that
  **don't sway** (inherited uncanny rule), ornaments as cover that breaks.
- **The Hand enters as weather, not a sprite at first:** a slow-moving
  **shadow** glides across the gravel (telegraph), then the hand **descends
  from the top of the screen** — slap (radial shockwave), poke (fast jab with
  ripple ring telegraph), grab (closing circle — dash out), glass tap
  (expanding ring walls), fish-food flakes (sink, heal if touched, grab-bait),
  net sweep (curved wall), gravel vacuum (pull zone), phase-3 prop chaos
  (pencil lines, ice cubes that slide, castle tip-over as a crushing rectangle).
- Vulnerability inherited: glowing fingertips/wrist windows after whiffed
  attacks; "OW! It stings!" ending inherited, comic-panel epilogue inherited.
- The beat of the fight: Clementine is a bug on a countertop. Everything the
  hand does is huge, slow, and telegraphed by shadow.

---

## 7. Items, Loot & Economy

- **Pearls** (items): clam-resident, gradient + pattern identity inherited
  (DESIGN-3D §6.6); on touch, Clementine absorbs them (§2.4). Item system
  architecture inherited — data + modifiers + triggers + transform tags
  (DESIGN-2D §9.1); the full item list, pools, qualities, synergies and
  transformations inherited unmodified.
- **Clams** inherited (Tridacna shells, breathing seam-light giveaway); shop
  clams gilded; shop stock/pricing inherited.
- **Pickups**: sand dollars, hearts/half hearts, foam hearts, ink bombs, glow
  jellies, sea snacks (per-seed identities), treasure chests (burst open when
  shot), loose seabed pickups, creature drop table — all inherited.
- **Economy**: sand dollars only; shops, no keys (inherited §27), beggars
  post-v1.
- **Sea-pedia, stats, achievements, Tide Pool hub**: inherited meta shell.
  The hub's rescued-octopus NPCs become rescued **jellyfish of the bloom**,
  each restored to its family colour.

---

## 8. World Structure & Progression (inherited, restated)

- **7 depths** (Shallows → Kelp → Galleon → Carnival → Trench → Abyss → Tank),
  each a chain of **4–5 levels**: the boss stands on level 4 (50%) or else
  level 5. HUD `Depth x · Level y`. After the Tank's boss **the run loops**
  endlessly: Depth 1 again, with Menace still rising (the cycle shows in the
  HUD from the second pass). Menace rises only through the Crack (§6.2).
- **Progressive dives** inherited (§5.1 spirit adapted to open floors): early
  runs end at Depth 1; each first boss kill deepens the Crack's reach. Admiral's
  Chart / unlock table inherited.
- **The Crack**: glowing fissure in the boss arena floor; boss guards it; on
  her freeing it splits open with updraft bubbles + light (inherited §5.2).
  Diving into it (§4.6) leads to the next depth's first level, which starts on
  the arena's level floor.
- **Endings:** per level, exactly two — dive down the way out, or perish
  (§4.0; the old Surface Bubble early-exit is cut). True ending at The Hand
  (with the endless loop, when it ends a run is to be revisited); Debug Dive
  inherited (`DEBUG` seed, item picker, no unlocks).
- **Menace** (§5.3) and the **sadness curve** (fewer healthy creatures, more
  empty dens, quieter reef) are systemic and inherited unchanged.
- **Achievements unlock pearls** (`data/achievements.json`, `PlaneAchievements`;
  the design is docs/ACHIEVEMENTS-PROPOSAL.md). A run starts with the **basic
  set**: every ported pearl without an `unlock` (DESIGN docs/PEARLS.md). Each of
  the five achievements unlocks one pearl, which treasure rooms, shops and
  Queen Clam offer from the moment it is earned — the rest of that run included
  — and in every run after. Earned once per profile and saved at once; **a run
  on a custom seed never earns one** (unlocks already earned still apply).

  | Achievement | Earned when | Unlocks |
  |---|---|---|
  | **Big Bubble Energy** | one hit from a full bubble (15 merged: Bubble Coral) frees a foe that was at full health | Starfish Arm |
  | **Bubble Bath** | 10 or more bubbles in one volley (Triple Tentacle + Hammerhead + Plankton Swarm + Double Helix) | Mitosis |
  | **Shucked in Fifteen** | a boss freed within 15 s of landing | Giant Squid Eye |
  | **Untouchable** | she dives on from a level without having taken damage on it | Lucky Sea Glass |
  | **Hermit Hoarder** | 100 shells collected on one level | Pirate's Doubloon |

  The unlocked pearls on the plane: **Mitosis** — a bubble that pops on a foe,
  rock or a pot splits into two half-damage bubbles 40° either side of its way
  on (they fly 4 m and never split again; the foe it popped on is spared);
  **Giant Squid Eye** — 10% of hits deal ×3 (seeded); **Lucky Sea Glass** — +2
  luck: every freed foe leaves 2 more shells; **Pirate's Doubloon** — +15 shells
  when taken, and shell caches hold 50% more. **Plankton Swarm** (basic) adds
  two smaller, half-damage bubbles to every volley. A pearl newly unlocked wears
  a mint **NEW** chip the first time a treasure room or shop offers it.

---

## 9. UI

- **HUD**: health and the active pearl's charge are diegetic on the bell (§2.5:
  gonad rings and rim), bubble orbs are diegetic under the bell (no ammo
  counter needed); the HUD keeps sand dollars, ink bombs, the minimap. At the
  top centre: the run's clock, and beside it, always on, where the run stands
  ("Depth 1 · Level 2"; a depth's first level names the depth too: "Sunlit
  Shallows · Depth 1 · Level 1"). Moving between levels shows no other message.
- **The minimap's bezel is a quiet health bar**: four warm-gold segments just
  inside its rim, parted at north, east, south and west — one for each gonad
  ring — each as opaque as its quarter of her HP is full, over a faint track
  (subtle, but there). They empty clockwise from the top-left and the last one
  left pulses when she is critical, eased like the rings.
- **Achievement banner** (`AchievementBanner`, ACHIEVEMENTS-PROPOSAL §4): a
  ticket at the bottom centre, above the HUD and below the pause menu — the main
  card (a coral ACHIEVEMENT eyebrow with a mint diamond, the title, the
  achievement's line, on pearl white with a coral rim and confetti) joined by a
  perforated seam to a stub holding the pearl it unlocked, live in a white
  socket with pastel rays turning behind it, a mint NEW PEARL chip, its name,
  tagline and effects. A bubble rises and pops into it (a ring and sparkles);
  the card springs in, the eyebrow slides in unskewing, the title drops in
  letter by letter, the line fades up, the stub flips in and the pearl drops
  into its socket with a squash; it holds about 3.75 s while a timer line
  drains, then leaves in a puff of pastel smoke as the pearl rises out in a
  bubble and pops. A pop and a rising four-note chime on the way in, a whoosh
  and a small pop on the way out. Banners queue (300 ms apart; with three or
  more waiting each holds 3 s), run on game time (they wait while paused and
  under the death splash) and, with reduced motion, simply cross-fade.
- **Pause menu**: resume, restart, save & quit, camera zoom; two tabs — the
  pearls she has absorbed, and the **achievements** (earned ones with their line
  and the pearl they unlocked; unearned ones as a dark pearl silhouette, the
  title and how to earn it).
- **Minimap**: circular, top-right, rotates so forward is up (inherited); fog of
  war; landmark icons (inherited §27); the map shows your floor only.
- **Tab map**: full floor, legend, crater-accurate (inherited §26).
- **Item captions**: name, tagline, one line per effect with numbers (inherited
  §29). No comic onomatopoeia (retired with the comic look); neon event
  language inherited (§11.3 glow events as light, not text).
- Accessibility: shake toggle, reduced-flash, reduced ambient motion,
  colorblind-safe shot outlines, FOV/mist options (inherited).

---

## 10. Audio

- Synth-style SFX + **positional audio as darkness telegraph** inherited
  (DESIGN-3D §7.2); in Trench/Abyss, sound *is* the off-screen warning system.
- SFX pitch lowers with depth (inherited presentation shift); muffled glass
  thumps from Depth 4+; filter-intake hum in the Tank.
- Music: post-v1 (inherited decision).

---

## 11. Technical Approach (rework, not rewrite)

Same `Core` (pure C#, fixed-step, seeded, engine-free) / `Game` (Godot) split.
The pivot is mostly **deletion and constraint**:

| Area | Action |
|---|---|
| Core sim | **Keep.** Constrain to a depth band: positions become 2D + fixed z; buoyancy, rise/descend, slow-sink, surface rules deleted. Collision: circle-vs-SDF-slice (inherit sphere-vs-SDF, z clamped). |
| Terrain | **Drive the inherited SDF toolkit from the topographic map** (§4.1): corridors kept under the swim level, ridges raised, the shaft cut, arches/caves/passes booleaned in. Mesh floors, walls, reef rim and the canopy layer only — ceilings are never meshed. Craters/pockets inherit (dig through the floor plane into the beneath-volume). |
| Canopy fade | New: arch bodies, cave roofs and overhangs render on a separate canopy layer; a blur+fade shader keyed by a player-under-volume test dissolves it while Clementine is underneath and restores it on exit. Cosmetic, camera-locked. |
| Navigation | 3D flow field → 2D grid flow field (strict simplification). Walkers: floor contour following. |
| Camera | New rig: tilted perspective, scroll smoothing, the two sanctioned breaks. |
| Player presentation | New: bell soft-body (reuse verlet tooling), pulse propulsion, beneath-bell ammo orbs, pearl-consumption animation, bell pattern composer (motif×color instancing on the bell shader). |
| Lighting | Re-aim inherited work: god rays cross-view, caustics on floor, pooled point lights (glows), fog by depth. Sun shadow map optional at top depths only. |
| Water field | 2D version of inherited grid. |
| Enemies/bosses | Behaviors/AI inherited; **presentation and bullet patterns reworked** to 2D. Hand fight rebuilt per §6.5. |
| FP systems | Viewmodel arm, aim-assist lens logic, FP comfort options: **cut** (aim assist re-implemented as 2D magnetism). |
| Data | items.json / creatures.json / pools.json **unchanged** — full content port. |
| Tests/CI | xUnit determinism suite inherited; add plane-lock invariants. |

**Performance target**: 60 FPS @1080p on GTX 1660 class with 300 projectiles,
40 active creatures, full water field. No-ceiling meshing keeps the
frame budget; the light budget per §5.1 is the real constraint — flora fades
at mist distance (inherited fade rule).

### 11.1 Delivery plan (rework order)
1. **Plane-lock spike (1 week):** Depth-1 basin, tilted camera, pulse swim,
   dash, bubble shooting with beneath-bell orbs. Fun gate — go/no-go.
2. **Core constraint pass:** 2D positions, flow field, walker nav, save schema.
3. **Look pass:** light budget per depth, god rays re-aim, caustics, marine
   snow, fog ramp, player glow. *This is the trailer milestone.*
4. **Bell system:** consumption animation, motif×color composer, synergy
   filaments, transformation re-themes.
5. **Enemies:** four classes + ninja ported; 2D patterns.
6. **Content:** three Depth-1 reefs, Gus/Queen Clam/Kelpie bosses, shop,
   caves, secrets, pockets, coins.
7. **Meta:** hub, unlocks, Sea-pedia, seeds, debug dive.
9. **Depths 2–6 + Tank + The Hand.**
10. **Polish:** presets, accessibility, balance, exports.

---

## 12. Open questions
- Hub format: the title screen is a menu over the live sea for now; whether a free-swim Tide Pool replaces it
  later is still open.

### 12.1 Conflicts found while implementing (unresolved — need a decision)
1. **Cruise speed:** §2.2 says base swim speed "4.5 m/s equivalent"; §4.5 says cruise **6 m/s**. Built with 6 m/s
   (`Tuning.PlaneCruiseSpeed`); the first-person `SwimSpeed` stays 4.5.
2. **Minimap orientation:** §3 says north is always up and "the minimap and world agree"; §9 says the minimap
   "rotates so forward is up (inherited)". Built north-up, matching the fixed camera. The minimap (90 m across, a thin
   outline) shows only the wall edges (the swim-level coastline) under fog of war: Clementine reveals the water within
   15 m of her (a third of its radius, with a soft 10 m gradient centred on that edge), and it stays revealed for the level. An unvisited place shows as
   "?" once she has come within 22.5 m (half its radius), above the fog; a visited one as a dot in its landmark
   colour. The shop and treasure rooms show their icons — a green "$" coin and a gold trophy — as soon as the fog
   over them is lifted (on the Tab map too). Her arrow is large and warm-coloured. The Tab map keeps the full chart, without fog. Entering a place
   names it under the minimap (all but the start); there are no beacons.
3. **Ninja respawn:** §6.3 carries the ninja over "intact (… nest respawn)"; the inherited Ink Deep rules
   (DESIGN-2D §1, DEPTH1-BESTIARY §8) free a defeated ninja into its school for good, and the current Core does that.
4. **Cave count vs special rooms:** Stage 4 asks for 1–2 pockets and 1–2 multi-chamber caves (2–4 caves), while
   §4.2 puts every special room in a cave: shop, treasure, 1–2 secrets and 0–1 curse den (3–5 rooms). Built: each
   special room gets its own cave (treasure and curse den multi-chamber, shop and secrets pockets), so a level
   can have 3 pockets and 5 caves.
5. **Secret cave vs reachability:** the secret cave is plugged by weak rock, but every POI must be flood-fill
   reachable. Built: the heightfield reaches it; the plug is a separate `WeakRock` that blocks swimming until it
   is broken (tested separately).
6. **Arches on "peaks":** an arch spans 10–20 m and stands on "peak pairs", but peaks are 10–16 m and rise well
   beyond 5–10 m from a corridor's centre line. Built: footings at the first ground ≥ 6 m high (falling back to
   5 m, then 4 m), with steeper ridge walls (k = 0.9, a value the doc leaves open). Most levels get 1–2 arches.
7. **Secret guards "trap-heavy (mimics)":** no mimic exists below Depth 3 (Treasure Mimic is a boss). Spawn rows
    are marked as traps with urchin and moray placeholders.
8. **Ambushers "8–12 m off corridor edges":** at that distance the ground is a ridge wall. Clingers fit, but
    burrowers are meant to hide in sand.
9. **CI:** the brief asks to keep the existing GitHub Actions (tests + headless export), but the repository has no
    workflow and no export presets.

### 12.2 Implementation assumptions (where the doc is silent)
- The bell is drawn floating **0.7 m** above the swim level; collision is against terrain above 0.
- Every open place keeps its floor **1.5 m** past its edge before the canyon wall starts to rise.
- Crossings closer than **16 m** merge into one plaza (plazas are ~14 m across).
- Routes are relaxed together: away from where they meet, each is pushed sideways from any route running alongside
  it until their centre lines are **22 m** apart, never into a place it bends around and only gently near the places
  it serves (so spurs stay short).
- A layout that fails validation retries with the next seeded attempt, up to **100** attempts.
- **1–3 passes** per level where they fit, chosen at the lowest saddles between corridors 14–34 m apart that are
  far apart on the network.
- Boulders and bommies are decoration only, never collision.
- Ridge walls between the camera and Clementine dissolve in a dithered cutout (a presentation rule for walls;
  the canopy layer has its own fade). A canopy fading over Clementine is drawn unblurred while it fades.
- **Combat** (`PlaneCombat.cs`; the Depth-1 bestiary is being ported onto the plane, the Pufferling first): Clementine has
  100 HP (shown by the gonad rings on her bell, §2.5; the run's play time, stopped while paused, top centre) and shoots toward the mouse (held) or the
  arrow keys — 10 damage, 4–5 volleys a second. Her shots are translucent bubbles, and a bubble never stops dead:
  thrown at 17 m/s, it slows steadily at first, then — once the water's drag (2.2 × its speed, per second) is the
  gentler — eases off exponentially, the two meeting smoothly at about 6.5 m/s. Slower than 0.9 m/s it hovers, still
  drifting and slowing (about 0.1 m/s left at the end), each bubble for its own time (0.8–1.2 s, rolled as it is
  thrown, times her **bubble hover** stat, which pearls can change: Bubble Coral ×1.5); then it pops, about 11.5 m
  from her. It only ever comes to rest by popping. They always pop on a mob (piercing and boomerang bubbles fly on through mobs). A bubble that bumps into rock
  pops 40% of the time and otherwise bounces off (Mirror Scale's bounces are sure). Two of her bubbles that touch
  (bubbles of one volley never do) bounce off each other: pushed apart, the bigger moving less, and turned off each
  other like two balls (80% of the meeting speed kept; each keeps its own easing-out speed, so a bounce turns it, and
  a bubble at rest is only nudged aside); each pops 40% of the time on the bump. A bubble is a film, not an orb: its
  surface wobbles in a few slow modes, livelier as it moves, and it weaves a hair off its line as it goes (drawn
  only). It pops like a real one, slowed so it reads: the film tears open where it was struck (at a random spot
  when it simply gives out) and the hole's bright rim sweeps across it in 0.09 s, the film breaking off the rim in
  droplets that fling on outward, one after another from the struck side to the far side, and fade in 0.26 s.
  **With Bubble Coral** (a pearl) they merge into one instead: it keeps the
  faster one's speed and heading (the two momenta added), the longer life, and the summed damage, and its radius
  grows by half the base per bubble merged in, so three bubbles make one twice the size and three times the damage.
  Growth stops at 15 bubbles: a full bubble still merges (taking the faster speed and heading and the longer life)
  but grows no bigger or stronger, and shines with a rainbow sheen. A mob flashes for 0.12 s on each hit. **The mobs are corrupted Pufferlings**
  (docs/PUFFERLING-PROPOSAL.md, DEPTH1-BESTIARY): 60 HP; they wander about a home spot; one that sees her within 10 m
  turns to face her and drifts closer; within 8 m it blows up (0.8 s), fires 8 needles in a ring turned at random (big
  hot-pink spikes, 20 m/s, 14 m, 8 damage; a needle pops any of her bubbles it meets and flies on), stays round 0.5 s (its spines sting
  for 4 on touch), then for 3 s backs away, shrinks and regrows its spines; past 16 m (or 2 s out of sight) it lets
  her go. Up to 16 per level (more with Menace, §6.2), 10 m apart and 30 m clear of the start: the spawn table's
  spots first, then canyon water she can reach (seeded order). Freed (THEME-BIBLE §6.5), a pufferling shudders as the
  tank colours wash out, bubbles burst round it, its price tag sinks, and it swims off as a healthy one, gone once it
  is out of sight; it never comes back as a mob. **8–12 healthy pufferlings** live on each level, in ones, twos and
  threes by the plazas and dens: never targets, unbothered by her. Running out of HP ends the run (see the death
  splash below).
- **Ambushes:** the first time she enters an ambush clearing, 3–4 pufferlings appear in a ring about 5.5 m around
  her (closer in where rock is near), already facing her; the first may blow up after 0.9 s, the others 0.6 s apart. They are ordinary
  mobs from then on: she may swim away, and they stay. An ambush springs once.
- **Pearls (first pass):** each treasure room holds one pearl she does not have yet, from the 19 ported to the
  plane (`PlaneRun.PortedPearls`; docs/PEARLS.md marks them). They stack through the item loadout (items.json); the
  pearls she carries show as dots bottom left, and taking one shows its name and tagline.
  - **Shots:** Triple Tentacle (3-shot fan), Hammerhead (5-shot cone), Anglerfish Lure (homing), Swordfish Bill
    (pierce), Mirror Scale (2 bounces off rock), Boomerang Shrimp (returns to her), Double Helix (2 weaving shots).
  - **Pearl Diver:** holding fire charges the next throw instead of firing (full in 1 s); letting go throws it as a
    pearl, up to ×3 damage and ×2.2 size at full charge (a tap throws a plain bubble). A pearl swells in front of her
    while she charges.
  - **Starfish Arm:** a bubble grows as it flies, to ×2 its size and damage at the end of its range.
  - **Ink Sac:** her bubbles are dark with ink; wherever one pops (on a mob, on rock, at rest, or popped by a needle)
    it bursts into a violet ink cloud that hurts everything within 1.8 m of its edge for 60% of its damage, on top of
    the hit itself (Queen Clam too, while her shell is open). It does not hurt her.
  - **Stats:** her damage stat scales every bubble (Shark Tooth +0.5, Coral Crown +0.3, on a base of 3.5). Coral Crown
    and Moon Jelly Heart add 20 max HP and heal 20 when taken (a pearl that raises max HP heals only what it says).
  - **Captain's Hook:** every hit shoves a mob along the shot (1.5 m/s, easing off at 6/s, so about 0.25 m); the hook
    makes it ×2.5. Queen Clam is never shoved.
  - **Remora Sucker:** shells drift to her from 7.5 m instead of 2.5 m.
  - **Bubble Coral:** her bubbles that touch merge into one, bigger and stronger, instead of bouncing apart, and they
    hover ×1.5 as long (see Combat above).
  - **Lantern Pearl:** her glow (its light) reaches ×1.8 as far and shines brighter, and she reveals the minimap's fog
    ×1.4 as far around her.
  - **Active pearls (F):** she holds one at a time (the last taken; a new one comes charged). Used, it empties and
    recharges over its listed time; its charge is the rim of her bell (§2.5). The charge carries down the shaft and
    into a saved run. **Bubble Shield** (20 s): a big iridescent bubble round her for 3 s; nothing hurts her inside it (needles
    and spines are turned away; it wobbles when struck and flickers before it goes). **Whale Song** (40 s): heals 35 HP,
    three rings of sound swelling out from her; not usable at full HP ("Already at full health").
- **Sunken amphorae (`PlaneVases.cs`, `VaseView`):** breakable pots on the seabed. Each level has 5–7 groups of
  1–3, seeded, by the walls where the seabed lies 0.3–1.6 m under her swim plane (so they stand up into it), clear of
  the start (20 m), the way down and the places: a tall two-handled amphora, a round jar or a squat pot, terracotta
  with dark glaze bands and a painted cream shoulder band, algae creeping up the foot, each leaning a little. They are
  solid: she and the creatures swim round them, and needles and Queen Clam's pearls stop on them.
  - **A plain bubble is not enough:** it rocks the pot on its foot, and pops on the bump (40%) or bounces off, as on
    rock. **What breaks one:** a bubble with an impact of 18 or more (merged, grown by Starfish Arm, or with the
    damage stats), a Pearl Diver throw charged half way or more, any bubble with Captain's Hook's knockback, an Ink
    Sac blast that reaches it, her ink dash into it — and, if she is lucky, a current: in a surging canyon a standing
    pot may topple and break (4% a second at full flow, less in a gentler one; about one in three over a surge).
  - **Broken,** it shatters: shards burst out along the blow, tumble down through the water and lie on the sand a
    while before fading, and a puff of silt rises. It leaves **2–3 shells** (50%), **a heart** (20%) or nothing.
- **Shells (the currency, first pass, `PlaneEconomy.cs`):** small shells lie in caches at the shell cache (6–9),
  and secret rooms (8–12); they drift to her from 2.5 m. A freed mob leaves a **heart** (6%: it heals 20 HP; she
  takes it only when hurt, otherwise it waits where it fell, a small red heart), else **shells** (30%: one, or two
  a quarter of the time), else nothing — about one heart and six shells a level from mobs. The shop
  has three stand slots: a pearl she does not have on half the visits (30 shells), always a heart (a health top-up:
  +25 HP, 5 shells, only when hurt), and a third slot kept empty for wares to come. The
  goods float on their own (pearls as in a treasure room, the top-up as a red heart), each with a small price tag on a
  string (the price and a shell), shown only while she is inside the shop; she buys by touching one. The HUD counter (a shell icon and the number) sits above the pearls and brightens and
  swells with every shell collected. Shells carry down with her pearls and HP.
- **Queen Clam (the Depth 1 boss, `PlaneBoss.cs`, adapted from DEPTH1-BESTIARY §5):** she guards the Crack on every
  depth's boss level for now (the other depths' bosses are not built). The arena is the exit's flat 15 m disc.
  - **Entering.** The first time Clementine is wholly inside the disc, the arena seals: an invisible wall on the rim
    stops her (her bubbles pop on it). Mobs cannot come in, and shots cannot cross it either way. A mud cloud of
    churning sand-brown haze rises outside the rim over 1.5 s and hides what lies beyond; on the minimap it is a brown
    ring about 15 m thick. A comic title card, **CLEMENTINE VS QUEEN CLAM**, punches in, stands for 2.5 s and
    dissolves over 0.6 s.
  - **The drop.** 0.5 s later her shadow grows on the Crack for 0.8 s and she drops onto it, facing Clementine. The
    thump shakes the camera, puffs sand, and sends a wave out to the rim in 0.5 s: it sweeps every mob in the disc out
    ahead of it and sets them down just outside the wall, unhurt, and nudges Clementine outward.
  - **Her body.** A giant ribbed clam 5 m across with a pearl crown and grumpy eyes, her colours drained. Her shell
    is solid; touching it while she fights costs 18 and bounces Clementine off. She turns toward Clementine at 30°/s.
    She has 800 HP and is hurt only while open; closed, bubbles pop harmlessly on her shell. Her pearls (12 damage,
    7 m/s) can be shot: a bubble that meets one pops, like on a mob, and pops the pearl.
  - **Phase 1.** She opens (a 0.9 s telegraph: the shell creaks and the heart-pearl glows), then fires four volleys
    1.2 s apart, alternating between a ring of 18 pearls with a 3-pearl gap at a random angle, and a 120° wall of 15
    pearls aimed at Clementine with a 2-pearl hole. Then she shuts for 3 s.
  - **Stagger.** At half HP she slams shut and rattles for 1.5 s, unhurt.
  - **Phase 2.** The second volley of each opening is a helix instead: three arms spiralling out for 2.5 s. The
    first comes with a royal pearl: big, slow (3.5 m/s) and homing for 6 s. It takes two bubbles (or one bubble
    merged from two), and if it hits her it costs 14 and slows her to 60% for 2 s. While open, she snaps once per
    opening if Clementine comes within 7 m: a 0.7 s warning (a red rim at her reach), then 18 damage within it.
    The pause shut is 2.2 s.
  - **Freed.** At 0 HP her pearls in the water burst, and she shudders for 0.3 s. Then the wall lifts, the mud
    settles over 2 s, her colours come back, and she shuffles 6.5 m off the Crack as a harmless clam. One pearl (a
    ported one she does not have, from the boss pool when one is) floats down beside the Crack, and the Crack opens.
    Her HP bar sits under the clock while the fight runs.
- **Death:** at 0 HP the run is over. The world stops, and a splash of the same kind fades in with how far the run
  got (the level reached, time, foes defeated, shells collected, pearls absorbed, seed). Enter or a click starts a
  clean new run: a new random seed, the first level, no pearls or shells, full HP. Esc goes back to the title
  screen. (The same kind of splash covers the first level's making when a run starts or resumes; between levels
  there is none — she dives.)
- **Pause (Esc):** the sea stands still under a dimmed screen. The menu:
  - resumes;
  - restarts the run (a new random seed, the first level, no pearls, full HP; the run left behind counts as
    abandoned);
  - **saves and quits to the title** (she resumes at the start of the level);
  - lists every absorbed pearl, newest first, with its name, tagline and effects.
- **Levels:** each level is generated from the run seed and its id (cycle, depth, level); diving down its shaft
  leads to the next (§4.6). She keeps her pearls, shells and HP through it; a new seed starts a fresh run. A new run
  starts on a random seed unless she types one on the title screen.
- **Title screen** (docs/TITLE-MENU-PROPOSAL.md has the full layout and motion):
  - **The backdrop.** The game boots into a menu over the live sea. Clementine swims slowly back and forth along a
    canyon of a fixed seed (KELP 7Q2Z), in the right third of the screen.
  - **The menu:** Continue (only with a saved run), New run, Seeded run, Sea-pedia, Statistics, Save & load,
    Settings, Quit. Each item has a detail line.
  - **Seeded run** opens a seed box under the item. Typing is forgiving and paste works; there is a random dice and
    the last five seeds as chips. Seeded runs never earn achievements.
  - **Sea-pedia:**
    - **Pearls:** each ported pearl is absorbed, seen (offered but never taken) or unknown. Its record: times
      absorbed, runs it was in, runs lost holding it, levels cleared with it.
    - **Creatures:** the Pufferling and Queen Clam. Each record: defeated (bosses: freed), defeated you,
      encounters, and best time for bosses.
  - **Statistics:**
    - four headline numbers (runs, best reach, foes defeated, time in the sea);
    - runs, combat, treasure and feats rows, records marked "best";
    - a bar per cause of death.
    - Seeded runs are counted, and also counted on their own.
  - **Save & load:**
    - the saved run (continue or abandon);
    - a backup save code to copy, or to import after a preview;
    - erasing everything (type ERASE).
  - **Settings:** frame-rate cap, sunlight, camera shake, reduced motion; the controls, read-only.
  - **Keeping the GPU cool on big screens:** the 3D scene renders with at most a 1080p screen's worth of pixels
    (view option MaxRenderHeight, 0 = native) and FSR upscales it to the window; the HUD draws at full size. The
    reef's skin works out only the looks a pixel shows (sand, rock or coral garden), not all three. Measured on a
    3840-wide window: about 12–25 ms of GPU a frame before, 3–5 ms after.
  - **Motion.** Sub-screens are pearl cards that surface from the bottom right and dive away, and starting a run
    fades into the loading splash.
  - **The interface** is laid out on a 1600×900 page and scaled to the window.
- **Saves:**
  - **One profile and one run** (the save file, version 2, written atomically).
  - **The run is saved at the start of every room.** Continuing regenerates that room from the seed with what she
    carried in: pearls, HP, shells, the run clock and totals. Death clears the saved run.
  - **The profile records:**
    - the statistics;
    - each pearl's and creature's record;
    - what dealt the killing blow (every hit on her names its source).
  - **Runs started straight from the game scene** with verification flags record and save nothing.
- **The look of a depth** (THEME-BIBLE §6.2–6.3, §6.9). One look per depth (`ReefLook`) sets every reef shader at once:
  - its water, sand, rock, coral, algae and sponge colours;
  - its sun (pale gold, never Clementine's tangerine) and ambient light;
  - its caustic and god-ray strength, and its saturation, vignette and murk;
  - its Menace (0 in the Shallows → 1 at the Tank).
  Depth 1 (Sunlit Shallows) is tuned; the other six carry the bible's palettes, ready to be tuned as they are built.
- **The reef's skin** (one shader for the ground, arches, cave roofs, boulders and weak rock, worked out in world
  space, so separate pieces meet seamlessly):
  - **Seabed:** rippled sand in patchy fields with flat sand between, sinuous crests, shell grit, soft algae drifts,
    and a shaded apron at the feet of walls.
  - **Walls:** limestone with strata and dark crevices, crusted with lilac and pink coralline algae, with turf on
    ledges.
  - **Tops:** coral gardens of domed heads in colonies (brain, polyp, plate and soft textures).
  - **Depth:** water steals red as the floor deepens; the rim wall darkens toward the edge of the world.
  - **Caustics:** on everything facing up, sharp near the surface, fading with depth. They are sunlight, so they
    fall only where the sun reaches; walls throw real shade across them.
- **Canopy rock.** Arches are stone bridges springing from the ground, and overhangs are lumpy shelves. Canopy pieces
  (arches, overhangs, cave roofs) dissolve with an ordered dither while she is underneath, staying opaque, so the
  depth focus still blurs them.
- **Life.** The generator's decoration is joined by cosmetic ground cover: seagrass meadows, crowds of small coral
  and sponges at the feet of walls, lone coral heads and rubble. It is deterministic from the seed and touches no
  play. Flora is repainted in the depth's palette, and soft things sway.
- **The Crack** glows orange from inside its fissure, through the murk; warm light spills over the arena floor and
  bubbles rise from it.
- **Depth focus** (the look, after Below):
  - **The focal plane** is the swim level. Within about 0.6 m below and 1.6 m above stays sharp.
  - **Below** that, the world blurs and sinks into the depth's water: clear turquoise in the Shallows, murk further
    down.
  - **Above** it, rock blurs into soft foreground silhouettes tinted by the water. Their edges are softened, so the
    mesh's steps never show.
- **The water over everything:**
  - Everything is seen through water: red fades and turquoise is scattered in with the water column.
  - God rays rake across the view, marched along each view ray parallel to the sun and drifting with the swell.
  - Marine snow drifts between the camera and the reef, glittering in the beams.
  - A faint refraction wobble (off with reduced motion).
  - Clementine's warm halo.
  - A grade: the depth's saturation, shadows cooled toward the water's hue.
  - A vignette tinted by the deep water, heavier as Menace rises.
- **Drawn after the pass, always sharp:** Clementine, the gateway, pearls, shells and ink. Places are not marked on
  the level itself (no rings or beacons); a shop's price tags show only while she is inside it.

