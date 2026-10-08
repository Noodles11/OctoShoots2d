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
| Levels | **Static 150×150 m seeded squares**, reef-ringed; free swimming inside; rift arena + the Crack; caves as special rooms (§4). |
| Level gen | **POIs → 3–5 intersecting paths → topographic reef**; mountains never on paths; arches over paths; caves & passes in ridges (§4.1). |
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
- **Static footprint: 150×150 m on every level, every depth** (1 m grid).
  Difficulty comes from content, menace and light — never from map size.
  (The earlier "+12 m per depth" idea is dropped: the square is a constant.)
- The square is **ringed by an impassable reef wall** — cliffs and coral
  ramparts; the rim is scenery and collision, never a soft boundary.
- **Two outcomes per level: descend through the rift, or perish.** No return
  to shallower levels within a run.
- **Pacing contract (the master knobs):** rushing start→boss takes **1–2 min**;
  looking under every rock takes **~10 min**. Every number in §4.5 exists to
  hit that pair, and the generation pipeline below is built around it.

### 4.1 Generation pipeline (one run seed; independent sub-stream per stage)

**The feel:** swimming through a labyrinth of canyon channels — but under
water. Heights are measured from the **swim level (0)**: Clementine floats on
it. Anything above 0 is an obstacle; anything at or below 0 is open water she
floats over, however deep the floor lies beneath.

**Stage 0 — The seabed.** Every attempt starts from a bare seabed at
**level −1** (1 m under the swim level). Everything else is placed on it,
and the reef is raised out of it afterwards.

**Stage 1 — POIs.** The named places come first, scattered on the bare
seabed by the rules below.
- **Start** — on the sunlit border zone.
- **Item spawn point** — one guaranteed major pearl/loot cache at 40–60% of
  the start→rift axis, preferentially *off* the main corridors (rewards
  leaving the path).
- **Rift (boss arena)** — hard constraint: geodesic distance from start
  **≥ 130 m** (it usually lands in the far corner quarter). A round, level
  arena at level −1, ~30 m across — room for the fight — with the rift, a
  crack 14–18 m long across its middle, cut to **−7 m: the deepest point of
  the level**.
- **Optional POIs** (seeded presence): shop ×1 (always), secret rooms ×1–2,
  curse den ×0–1, treasure cave ×1, ambushes ×2–4 (all off-path). The
  shop, secrets, treasure and curse den sit in caves (Stage 4); ambushes and
  the item cache are open clearings.
- **Scatter rules:** pairwise distance between optional POIs ≥ 25 m; only the
  rift inside its ~40 m exclusion zone; exactly one POI may sit "early"
  (20–35 m from start — usually the shop, to teach the economy).

**Stage 2 — Route canyons.** 3–5 canyons from start to rift, laid out
between the places, before any rock exists:
- Deliberately varied routes: one near-direct, wide arcs left and right, an
  S-curve through the middle, and (deeper depths) a trench route.
  Canyon width **6–10 m**, floor at the seabed (level −1).
- Corridors **intersect 2–4 times** at seeded crossing nodes; each crossing
  opens into a small plaza (~14 m) — natural fight arenas and orientation
  landmarks.
- **Routes keep apart:** away from the start, the rift and their crossings,
  routes do not run side by side — they spread to leave a ridge between
  them. Two routes may run closer than 16 m (centre to centre) for at most
  30 m; otherwise the layout regenerates.
- **Connectivity guarantee:** every POI joins the network via a spur corridor
  (≤ 15–20 m). Validation: flood-fill from start must reach every POI and the
  rift; otherwise regenerate from the next sub-seed (inherited retry rule).

**Stage 3 — The canyon labyrinth.**
- **Side canyons** (4–6.5 m wide) branch off the route canyons and off each
  other, turning sharply every 8–16 m, mostly into dead ends; now and then one
  breaks through into the canyon it meets, closing a loop. Every canyon keeps
  at least 3.5 m of rock between itself and the next, and none comes near a
  place (caves keep their rock). At least 4 per level; they grow until about
  63% of the interior is open.
- **Walls** rise out of the seabed everywhere else: from the edge of the open
  ground they climb over 3.5 m with the reef prototype's cosine brush to a
  top 4–14 m high, taken from the prototype's terrain (`reef_generator.html`,
  reproduced exactly for the seed text), so plateaus roll rather than lie flat.
- **At most 40%** stands above the swim level: where the walls would cover
  more, the cores of the biggest wall masses (farthest from any canyon, then
  lowest on the prototype's terrain) sink into shallow plateaus just under the
  swim level, so the canyon walls themselves stay. At least half the interior
  is water reachable from the start.
- **Hard rule: nothing above 0 on a path** — the canyon floors lie at level −1
  to −1.6 (gently rolling, never flat); the boss arena alone is level.
- **Trenches (deeps):** 1–3 stretches (30–60 m) of route canyon, 8–14 m wide,
  cut **3.5–5.5 m below the swim level** — darker and quieter, with richer loot
  bias — always shallower than the rift.
- The impassable rim rises out of the walls with the same brush.

**Stage 4 — Arches, caves, passes:**
- **Arches ×1–5:** peak pairs flanking a corridor get arch spans (10–20 m)
  across the path. Swimming under one triggers the canopy fade (§3). Where
  no flank stands high enough, a pair of short rock ridges is raised either
  side of a corridor to carry one.
- **Caves:** small dead-end pockets (×1–2) and larger multi-chamber caves
  (×1–2) carved into ridge flanks; mouths face a corridor or open water;
  ≥ 30 m apart. Cave interiors host the special rooms (§4.2).
- A cave is hollowed out *inside* a mountain: where the rock over it is too
  low, a peak (8–11 m) rises first as a cosine dome
  and kept off the spur to its mouth. The mountain's surface is kept as the
  cave's roof and drawn as intact rock until Clementine swims inside (canopy
  fade); the chambers beneath have a floor 3.5 m under the swim level — the
  only flat ground in a level.
- **Passes:** narrow cuts (3–5 m, floor 2.5 m under the swim level) punched
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
- Trenches: shadowed walls, sponges, bioluminescent accents. Buried-coin X
  marks and sealed pockets in the seabed under the paths (§4.2).

**Stage 6 — Mob spawn rules:**
- **Dens:** caves and ridge pockets hold themed groups from the depth pool
  (inherited den system).
- **Ambush:** burrowers/clingers seeded 8–12 m off corridor edges, biased to
  plazas and arch landings — where players naturally slow down.
- **Patrols:** 1–2 swimmer packs per level loop along corridor segments
  between intersections; the paths feel watched.
- **Guardians:** POI guards scale with reward — shop lightly guarded,
  treasure medium, item spawn point heavy, secrets trap-heavy (mimics).
- **Light bias:** ambient spawns avoid landmark-light radii (beacons are
  breathing room) — except curse dens.
- **Budget:** creature count follows the menace budget (inherited ×1.0→1.8);
  dormant beyond notice range; champions by depth (all inherited).

### 4.2 Special places (caves, inherited from DESIGN-3D §6.4)
- Treasure cave, **Barnaby's shop** (safe water, inherited §24), curse den,
  **secret cave** (plugged by weak rock — one ink bomb), Mermaid's Grotto
  portal after bosses.
- **Landmark light language** inherited: gold (treasure), green (shop),
  red (curse), violet (secret, **no beacon** — found, not advertised),
  orange (the Crack); and white (the start), teal (the item cache), coral (ambushes; warm but not gold, so
  they never read as treasure). In dark depths these lights are the level's signage —
  Below's campfire principle.
- **Sealed pockets** (4–6/level) and **buried coins** (8–12/level, scratched
  X marks) inherited (DESIGN-2D §26, DESIGN-3D §6.3) — bomb the floor, loot
  below.
- **Destructible reef** inherited: ink bombs dig craters through the floor
  into pockets; beams burn rock; plain bubbles never dig.

### 4.3 The beneath-layer (the depth illusion)
- The floor Clementine swims over is **translucent water, not ground glass**:
  looking "down" you see, ~10 m below, a **dark, blurred, slow-moving shadow
  copy of the next depth** — its structures as vague silhouettes, and **the
  next biome's creatures as drifting shadows**.
- Rendered in the water pass (`beneath.gdshaderinc`), no geometry: each view
  ray is carried on to a plane ~13 m below the swim level (y −14), so the layer
  slides by with true parallax under the floor. There it reads a procedural
  shadow field: broad ridges of the deeper reef, swaying forests of the next
  biome, six large creatures gliding on long paths across the level, and a
  drifting fish school. Shadows darken the floor toward the next depth's murk
  (`reef_beneath_tint`); the strength is per depth (`ReefLook.Beneath`, the
  `reef_beneath` global) and rises where the floor itself drops away.
- **Escalates with depth:** in the Sunlit Shallows it's almost invisible
  (bright water hides it). In the Twilight Trench and Abyss, ambient light is
  near zero and **the beneath-layer becomes the scenery** — giant slow shapes
  crossing under your feet, eyes opening and closing below. The deep ocean
  should feel inhabited from below.
- **Gameplay-readable tells:** sealed pockets glow faintly through the floor;
  the Crack **backlights both layers** around the arena; the Tank's LED light
  bleeds down into the Abyss below in the finale, inverted — the artificial
  light above is what reaches *down*.
- Shadow mobs are cosmetic: a function of position and time on the GPU, no
  AI, no RNG — the sim never sees them (inherited §10 rule). Nothing in the beneath-layer ever reaches up through the floor.

### 4.4 Flow on a level (the open-floor pacing contract)
1. Enter over the Crack's light-shaft from above (inherited start-room motif).
2. Orient by landmarks: cave beacons, the boss glow, the beneath-layer shadows.
3. Engage den encounters (waking groups, inherited §19), raid caves, spend at
   the shop, hunt secrets.
4. Boss arena → currents seal the tunnels (inherited) → boss → Crack opens →
   rewards drop → **descend through the rift, or die trying.** The inherited
   Surface Bubble early-exit is **cut**: a level offers exactly two endings
   (§4.0). Progressive depth unlocks retained — the rift stays sealed with a
   glowing "?" rune until the next depth is unlocked (inherited §5.1), and
   entering a sealed rift ends the run as a win.

### 4.5 Sizes & timing budget (the master knobs)

| Knob | Value | Why |
|---|---|---|
| Footprint | **150×150 m, static** | the 10-minute explore budget |
| View on screen | ~30×22 m | a corridor reads at a glance |
| Corridors | 6–10 m wide, under open water | rush speed; nothing blocks |
| Crossing plazas | ~14 m | natural fight arenas |
| Peaks | up to 14 m | occlude and cast shade, never block |
| Trenches | 8–14 m wide, −4…−8 m | mood + alternate routes |
| Arches | ×1–5, span 10–20 m | canopy-fade showcase |
| Passes | 3–5 m cuts | network braiding |
| Boss arena | ~30 m disc | room for 2D bullet rings |
| Cruise speed | **6 m/s** (jet 1.8× for 0.38 s; dash 3.4×, 0.85 s cd) | brisk — dash crosses a corridor in ~0.4 s |
| Rush time | **1–2 min** | ~200 m route + 2–4 partial fights |
| Full clear | **8–12 min (target ~10)** | ~1 km coverage + 10–14 encounters + caves/shop/secrets |

Rush math: rift ≥ 130 m away, route winding ×1.4–1.6 → 30–40 s pure swim at
cruise (less with jet bursts), plus partial fights and maybe one cave.
Explore math: the POI tour is 800 m–1 km of swimming plus encounters, shop
time and secret hunting.

---

## 5. Lighting & Atmosphere — the core pillar

The Below principle: **small warm pools of light in overwhelming dark**, except
our "campfires" are bioluminescent and the dark is the ocean itself.

### 5.1 Per-depth light budget (drives palette, mood, and difficulty readability)

| Depth | Ambient | God rays | Player glow radius | Primary scenery light |
|---|---|---|---|---|
| 1 Shallows | High — sunlit turquoise | Strong, broad | Small (supplemental) | Sun + caustics |
| 2 Kelp | Dappled green | Broken by canopy | Small | Light shafts |
| 3 Galleon | Amber, dusty | Only through wreck holes | Medium | Lanternfish, loot glints |
| 4 Carnival | Neon but flickering | None — "stage lights" | Medium | Corrupted neon, bounce pads |
| 5 Trench | Near zero | None | **Large — she is the lantern** | Bioluminescent flora specks |
| 6 Abyss | Black | None | **Large, bloom-heavy** | Neon outlines + beneath-layer eyes |
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

### 6.3 Senses, aggro, fairness (inherited DESIGN-3D §7.2/§7.6)
Notice ranges halve while slow (<0.6 m/s), double for 2 s after shooting/dashing;
ink clouds hide completely; 0.6 s startle + call; leashing back to dens;
**no more than 3 attackers from off-screen** — enforced as "beyond the screen
edge" in top-down. The **clownfish ninja** spec carries over intact (headband
glow telegraph, decoy schoolmates, nest respawn) — it reads *better* top-down,
where hiding among identical fish is visual, not camera-based.

### 6.4 Bosses
- Arenas: wide grottoes with the Crack across the floor. On entry an invisible wall seals them and a mud cloud
  rises around them (the top-down stand-in for the inherited current walls; see Queen Clam in §12.2).
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
  three reefs each (Tank single), `DEPTH x · REEF y/3` HUD, menace ramp.
- **Progressive dives** inherited (§5.1 spirit adapted to open floors): early
  runs end at Depth 1; each first boss kill deepens the Crack's reach. Admiral's
  Chart / unlock table inherited.
- **The Crack**: glowing fissure in the boss arena floor; boss guards it; on
  death it splits open with updraft bubbles + light (inherited §5.2). Entering
  it: Clementine is **sucked down** (a jellyfish can't grab rock — she goes
  whether she wants to or not), comic-panel tunnel, next reef entered through a
  light shaft.
- **Endings:** per level, exactly two — descend through the rift, or perish
  (§4.0; the old Surface Bubble early-exit is cut). True ending at The Hand;
  Debug Dive inherited (`DEBUG` seed, item picker, no unlocks).
- **Menace** (§5.3) and the **sadness curve** (fewer healthy creatures, more
  empty dens, quieter reef) are systemic and inherited unchanged.

---

## 9. UI

- **HUD**: health bar (inherited numeric + drain trail), bubble orbs are
  diegetic under the bell (no ammo counter needed), active-item slot with
  recharge bar, sand dollars, ink bombs, minimap.
- **Minimap**: circular, top-right, rotates so forward is up (inherited); fog of
  war; landmark icons (inherited §27); beneath-layer shadows *do not* appear on
  it — the map shows your floor only.
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
| Terrain | **Drive the inherited SDF toolkit from the topographic map** (§4.1): corridors kept under the swim level, ridges raised, trenches carved, arches/caves/passes booleaned in. Mesh floors, walls, reef rim and the canopy layer only — ceilings are never meshed. Craters/pockets inherit (dig through the floor plane into the beneath-volume). |
| Canopy fade | New: arch bodies, cave roofs and overhangs render on a separate canopy layer; a blur+fade shader keyed by a player-under-volume test dissolves it while Clementine is underneath and restores it on exit. Cosmetic, camera-locked. |
| Navigation | 3D flow field → 2D grid flow field (strict simplification). Walkers: floor contour following. |
| Camera | New rig: tilted perspective, scroll smoothing, the two sanctioned breaks. |
| Player presentation | New: bell soft-body (reuse verlet tooling), pulse propulsion, beneath-bell ammo orbs, pearl-consumption animation, bell pattern composer (motif×color instancing on the bell shader). |
| Beneath-layer | New but cheap: a procedural shadow plane in the water pass, parallax by view ray, strength by depth; shadow mobs are pure functions of time. |
| Lighting | Re-aim inherited work: god rays cross-view, caustics on floor, pooled point lights (glows), fog by depth. Sun shadow map optional at top depths only. |
| Water field | 2D version of inherited grid. |
| Enemies/bosses | Behaviors/AI inherited; **presentation and bullet patterns reworked** to 2D. Hand fight rebuilt per §6.5. |
| FP systems | Viewmodel arm, aim-assist lens logic, FP comfort options: **cut** (aim assist re-implemented as 2D magnetism). |
| Data | items.json / creatures.json / pools.json **unchanged** — full content port. |
| Tests/CI | xUnit determinism suite inherited; add plane-lock invariants. |

**Performance target**: 60 FPS @1080p on GTX 1660 class with 300 projectiles,
40 active creatures, full water field. No-ceiling meshing + beneath-layer LOD
keep the frame budget; the light budget per §5.1 is the real constraint —
flora and shadow mobs fade at mist distance (inherited fade rule).

### 11.1 Delivery plan (rework order)
1. **Plane-lock spike (1 week):** Depth-1 basin, tilted camera, pulse swim,
   dash, bubble shooting with beneath-bell orbs. Fun gate — go/no-go.
2. **Core constraint pass:** 2D positions, flow field, walker nav, save schema.
3. **Look pass:** light budget per depth, god rays re-aim, caustics, marine
   snow, fog ramp, player glow. *This is the trailer milestone.*
4. **Beneath-layer:** shadow geometry + shadow mobs + pocket glow-through.
5. **Bell system:** consumption animation, motif×color composer, synergy
   filaments, transformation re-themes.
6. **Enemies:** four classes + ninja ported; 2D patterns.
7. **Content:** three Depth-1 reefs, Gus/Queen Clam/Kelpie bosses, shop,
   caves, secrets, pockets, coins.
8. **Meta:** hub, unlocks, Sea-pedia, seeds, debug dive.
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
7. **Trench route** "at deeper depths": the depth it starts at is not given; not built (Depth 1 only so far).
8. **Secret guards "trap-heavy (mimics)":** no mimic exists below Depth 3 (Treasure Mimic is a boss). Spawn rows
    are marked as traps with urchin and moray placeholders.
9. **Ambushers "8–12 m off corridor edges":** at that distance the ground is a ridge wall. Clingers fit, but
    burrowers are meant to hide in sand.
10. **CI:** the brief asks to keep the existing GitHub Actions (tests + headless export), but the repository has no
    workflow and no export presets.

### 12.2 Implementation assumptions (where the doc is silent)
- The bell is drawn floating **0.7 m** above the swim level; collision is against terrain above 0.
- Every open place keeps its floor **1.5 m** past its edge before the canyon wall starts to rise.
- Crossings closer than **16 m** merge into one plaza (plazas are ~14 m across).
- Routes are relaxed together: away from where they meet, each is pushed sideways from any route running alongside
  it until their centre lines are **26 m** apart, never into a place it bends around and only gently near the places
  it serves (so spurs stay short).
- A layout that fails validation retries with the next seeded attempt, up to **100** attempts.
- **1–3 passes** per level, chosen at the lowest saddles between corridors 14–34 m apart that are far apart on the
  network.
- Trenches are random walks of **50–110 m**.
- Boulders and bommies are decoration only, never collision.
- Ridge walls between the camera and Clementine dissolve in a dithered cutout (a presentation rule for walls;
  the canopy layer has its own fade). A canopy fading over Clementine is drawn unblurred while it fades.
- **Placeholder combat** (until the Depth-1 bestiary is ported onto the plane, `PlaneCombat.cs`): Clementine has
  100 HP (an HP bar bottom left, with a trail showing damage just taken; the run's play time, stopped while
  paused, top centre) and shoots toward the mouse (held) or the
  arrow keys — 10 damage, 4–5 volleys a second. Her shots are translucent bubbles: thrown at 17 m/s, they slow
  steadily (constant deceleration) to a stop about 11 m out, hang still there for 0.3 s and pop; they pop on a mob or
  on rock first if they meet one (piercing and boomerang bubbles fly on through mobs). Two of her bubbles that touch
  merge into one (bubbles of one volley never do): it keeps the faster one's speed and heading (the two momenta
  added), the longer life, and the summed damage, and its radius grows by half the base per bubble merged in, so
  three bubbles make one twice the size and three times the damage. Growth stops at 15 bubbles: a full bubble
  still merges (taking the faster speed and heading and the longer life) but grows no bigger or stronger, and
  shines with a rainbow sheen. A mob flashes for 0.12 s on each hit. One mob, a shooting dot (30 HP): it notices her within
  14 m, follows to about 5 m, loses her past 20 m, and fires 10-damage shots every 1.4 s when it has a clear line of
  sight. Up to 22 per level, 10 m apart and 30 m clear of the start: the spawn table's spots first, then canyon
  water she can reach (seeded order); a defeated mob never respawns. Running out of HP ends the run (see the death
  splash below).
- **Ambushes:** the first time she enters an ambush clearing, 5–7 mobs appear in a ring about 5.5 m around her
  (closer in where rock is near), already hunting her, their first shots staggered from 0.9 s. They are ordinary
  mobs from then on: she may swim away, and they stay. An ambush springs once.
- **Pearls (first pass):** each treasure room holds one pearl she does not have yet, from the ones ported to the
  plane — those that change her shots the most: Triple Tentacle (3-shot fan), Hammerhead (5-shot cone), Anglerfish
  Lure (homing), Swordfish Bill (pierce), Mirror Scale (2 bounces off rock), Boomerang Shrimp (returns to her) and
  Double Helix (2 weaving shots). They stack through the item loadout (items.json); the pearls she carries show as
  dots above the HP bar, and taking one shows its name and tagline.
- **Shells (the currency, first pass, `PlaneEconomy.cs`):** small shells lie in caches at the item cache (6–9),
  and secret rooms (8–12), and every defeated mob drops 1–2; they drift to her from 2.5 m. The shop
  sells two pearls she does not have (15 shells each) and a health top-up (+25 HP, 5 shells, only when hurt): the
  goods float on their own (pearls as in a treasure room, the top-up as a red heart), each with a small price tag on a
  string ("15" and a shell), shown only while she is inside the shop; she buys by touching one. The HUD counter (a shell icon and the number) sits above the pearls and brightens and
  swells with every shell collected. Shells carry through the rift with her pearls and HP.
- **Queen Clam (the Depth 1 boss, `PlaneBoss.cs`, adapted from DEPTH1-BESTIARY §5):** she guards the rift in every
  room for now. The arena is the rift's flat 15 m disc.
  - **Entering.** The first time Clementine is wholly inside the disc, the arena seals: an invisible wall on the rim
    stops her (her bubbles pop on it). Mobs cannot come in, and shots cannot cross it either way. A mud cloud of
    churning sand-brown haze rises outside the rim over 1.5 s and hides what lies beyond; on the minimap it is a brown
    ring about 15 m thick. A comic title card, **CLEMENTINE VS QUEEN CLAM**, punches in, stands for 2.5 s and
    dissolves over 0.6 s.
  - **The drop.** 0.5 s later her shadow grows on the rift for 0.8 s and she drops onto it, facing Clementine. The
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
    settles over 2 s, her colours come back, and she shuffles 6.5 m off the rift as a harmless clam. One pearl (a
    ported one she does not have, from the boss pool when one is) floats down beside the rift, and the gateway opens.
    Her HP bar sits under the clock while the fight runs.
- **The rift's splash:** entering the gateway fades (0.35 s) to a dark splash with the room just cleared — time,
  foes defeated, shells collected (and spent), pearls found, places visited, damage taken, and what she carries — and
  the next room ("Depth 1 · Room N"). The next room is generated off the main thread meanwhile; once it is ready the
  splash waits for Enter or a click (not before), then fades out over the new room.
- **Death:** at 0 HP the run is over. The world stops, and a splash of the same kind fades in with how far the run
  got (depth and room reached, rooms cleared, time, foes defeated, shells collected, pearls absorbed, seed). Enter
  or a click starts a clean new run: a new random seed, room 1, no pearls or shells, full HP. Esc goes back to the
  title screen.
- **Pause (Esc):** the sea stands still under a dimmed screen. The menu:
  - resumes;
  - restarts the run (a new random seed, room 1, no pearls, full HP; the run left behind counts as abandoned);
  - **saves and quits to the title** (she resumes at the start of the room);
  - lists every absorbed pearl, newest first, with its name, tagline and effects.
- **Rooms:** a gateway at the rift's centre leads to the next room, a fresh level of the same seed (room N is
  generated as level N). She keeps her pearls and her HP through it; a new seed starts a fresh run. A new run starts
  on a random seed unless she types one on the title screen. The gateway is shut until the room's boss is freed.
- **Title screen** (docs/TITLE-MENU-PROPOSAL.md has the full layout and motion):
  - **The backdrop.** The game boots into a menu over the live sea. Clementine swims slowly back and forth along a
    canyon of a fixed seed (KELP 7Q2Z), in the right third of the screen.
  - **The menu:** Continue (only with a saved run), New run, Seeded run, Sea-pedia, Statistics, Save & load,
    Settings, Quit. Each item has a detail line.
  - **Seeded run** opens a seed box under the item. Typing is forgiving and paste works; there is a random dice and
    the last five seeds as chips. Seeded runs never earn achievements.
  - **Sea-pedia:**
    - **Pearls:** each ported pearl is absorbed, seen (offered but never taken) or unknown. Its record: times
      absorbed, runs it was in, runs lost holding it, rooms cleared with it.
    - **Creatures:** the Pellet Dot and Queen Clam. Each record: defeated (bosses: freed), defeated you,
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

