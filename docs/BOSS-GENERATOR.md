# The boss generator

How Ink Deep's bosses are grown: every depth's boss is assembled from a seed — body, mutations, pearl, senses and
fight — so that each run meets bosses no one has met, yet every one of them looks and plays like a creature that was
designed on purpose. The generator lives in `src/Core/Bosses` (deterministic, engine-free, xUnit-tested: a seed code
like KELP7Q2Z reproduces the whole run, bosses included). The drawing lives in `src/Game` (one rig per body plan).

The design brief: **a boss the creator hasn't met either — that still looks deliberately made.** Surprise comes from
how authored pieces combine, never from noise.

## 0. Rules above every roll

- **Every boss holds a corrupted pearl.** Its only weak spot. On the freeing the pearl is cleansed (white), left
  exactly where it sat, and the freed creature moves away from under it as true colour floods back over the arena.
- **Creatures look real, not cartoonish.** No faces, brows, crowns, eyes that glare or mouths that grin. Behaviour
  reads from the body: a shell opening, a bell contracting, a body creeping, an ink sac swelling.
- **Corruption shows as mutation** (THEME-BIBLE §6.4): the Leak rewrites bodies — extra limbs, fused parts, ink
  sacs, shells overgrowing the animal, parts grown inside-out. Nothing is worn; nothing man-made appears.
- **Freed, not killed** (THEME-BIBLE §6.5): the mutations melt away with the ink and leave the creature's true body,
  smaller and whole, which leaves the arena slowly and is gone once off screen. It never helps her.
- **Fair before surprising.** A roll that cannot be read, dodged or won is regenerated (§9). Every danger has a tell
  that comes from the body; every fight has a weak-spot window she can learn.

## 1. Where bosses come from

- **Generated:** the boss of every real depth's boss level — Depths 1–5 (Kelp Jungle → Abyss).
- **Authored:** only **The Hand** (Depth 6, the Tank), never generated (not built yet; until it is, Depth 6 rolls a
  generated boss like the others).
- **None:** Depth 0, the tutorial (its open rift has no boss).
- **Seed:** `bossSeed = hash(runSeed, cycle, depth)`. Each loop meets new bosses. Nothing else feeds the roll: no
  clock, no playthrough state.
- **Queen Clam** is no longer fixed: she is one hand-tuned result of the Bivalve plan's *The Lid* concept (§4), which
  keeps her exact fight and look as a reference the generator is tested against.

## 2. The pipeline

Each step picks from authored tables, filtered by what is already chosen:

1. **Band** from the depth: *early* (Depths 1–2), *middle* (3), *deep* (4–6). Menace (0 at Depth 1 → 1 at Depth 6)
   drives every continuous number.
2. **Body plan** (§3), weighted by band — never the body of the boss met just before (the last depth's, or at
   Depth 1 the previous loop's last), and preferring bodies that can carry a fight unlike that boss's.
3. **Concept** (§4): the fight's designed idea, chosen from those the plan supports, weighted by band and tier. Never
   the very same kind of fight as the boss before (its weak-spot rule and its signature both) while the body has
   another; one that shares either is much less likely. Two bosses in a row are never the same animal and almost
   never the same puzzle.
4. **Rarity tier** (§11): Common 70 % · Rare 25 % · Legendary 5 %. It unlocks concepts, extras and phases.
5. **Anatomy**: the plan's rig parameters (silhouette, surface, proportions) within the plan's authored ranges, then
   the transforms (§3.3).
6. **Mutations** (§5): one dominant mutation theme, its sites on the rig, density by Menace.
7. **Pearl** (§6): the mount and exposure rule the concept names, placed on a real site of the rig.
8. **Fight** (§7): the concept's signature attack, movement and tempo; one or two filler attacks chosen by tags from
   what the body can produce; phases.
9. **One spice** (§4.1): a single bounded twist that makes this one stranger.
10. **Name** (§10).
11. **Validator** (§9). On failure the generator re-rolls from step 5 with the next sub-seed (bounded attempts; the
    last fallback is the concept's reference roll, which always passes).
12. **True form**: the same body with every mutation rolled back, natural colours, 55–70 % of the size.

The output is a `BossSpec`: plain data the sim runs and the view draws.

## 3. Body plans

A body plan is an authored **rig**: a parametric body with named sockets (where a pearl, a mutation, an attack can
sit), the movements it can make, and its natural colours. All bosses are drawn at the game's scale (Clementine's
bell is about 0.8 m; the arena is 30 m across); bosses span 3.5–6 m mutated.

### 3.1 First pass (built now)

| Plan | Real model | Rig and sockets | Moves | True colours | Weight early / mid / deep |
|---|---|---|---|---|---|
| **Bivalve** | giant clam | two ribbed valves on a hinge, mantle, lid edge; sockets: *mantle centre*, *lid lip*, *hinge*, *valve flanks* | hold, turn in place | sand shell, blue-teal mantle | 30 / 15 / 5 |
| **Echinoderm** | sea star or urchin (sub-rolled) | star: a central disc with 5 arms (tube-foot undersides); urchin: a test with a spine coat; sockets: *disc centre*, *arm tips*, *between spines*, *underside* | hold, turn in place, creep | coral orange or plum purple | 30 / 20 / 10 |
| **Cnidarian** | moon jelly or siphonophore (sub-rolled) | a bell with oral arms and a fringe of tentacles, or a colony of small bells coiled in a ring (its pearl travels round the ring) trailing a curtain of tentacles; sockets: *heart of the bell*, *bell rim*, *tentacle roots* | drift, pulse-dash, circle slowly | milky translucent, faint pink gonads | 20 / 25 / 25 |
| **Gastropod** | sea hare or snail (sub-rolled) | a soft foot and mantle, parapodia flaps or a coiled shell, rhinophores (never eyes); sockets: *mantle flaps*, *under the shell lip*, *foot*, *shell apex* | creep, turn | sea hare brown-olive with pale rings, or snail cream shell over grey | 20 / 25 / 20 |

### 3.2 Later passes

Crustacean (walker, claws, leaps), Fish (charges, circling), Cephalopod (arms, ink, reach), Annelid (lunges from
holes). They bring their own movements (charge lines, leap between anchors, burrow and emerge) and attacks (lunge,
beam sweep). Until then their weight goes to the four built plans.

### 3.3 Transforms (bounded, always applied)

- **Scale** by depth (×0.9 at Depth 1 → ×1.35 at Depth 6) and ×0.92–1.08 jitter; one axis stretched at most ±15 %.
- **Proportions** within the plan's authored ranges only (arm count 5–7 for a star, bell depth, spine length, shell
  turns, flap size).
- **Asymmetry** (Rare and up, 40 %): one side grown out — an oversized arm, a lopsided bell, one valve overgrown.
  Always a mutation site, never random wobble.
- **Grafting** (Legendary, later pass): a second plan's part grown in.

## 4. Concepts — the designed core of each fight

A concept is an authored fight idea: what the player has to work out, which body plans can carry it, its pearl
exposure, its signature attack, how it moves and its tempo. Everything else rolls around it.

| Concept | Plans | The question | Pearl exposure | Signature | Moves / tempo | Tier |
|---|---|---|---|---|---|---|
| **The Lid** | Bivalve | "When does it open?" | open moments (the lid lifts on its cycle) | ring and wall volleys | hold · metronome | Common |
| **Snapping Gate** | Bivalve | "Get in close, but not too long" | open moments, shorter, with a snap punish | helix | turn slowly · duelist | Common |
| **Turning Star** | Echinoderm | "Stay with the open arc" | moving window (the pearl travels round under the skin) | spine ring (radial, a turning gap) | turn in place · metronome | Common |
| **Slam and Sigh** | Echinoderm | "Dodge the slam, punish the rest" | stagger (exposed after its slam) | ink pulse ring | creep · surge-and-rest | Common |
| **Pulse Bell** | Cnidarian | "Hit the heart between pulses" | open moments (the bell contracts open) | ring on every pulse | drift with sudden pulses · metronome | Common |
| **Trailing Curtain** | Cnidarian | "Find the gap in the curtain" | moving window along the bell chain | pearl wall from the tentacles | circle at range · relentless | Rare |
| **The Spreader** | Gastropod | "It paints the floor; when it rears, strike" | stagger (it rears after a mortar) | arc bombs, staining where they land | creep · surge-and-rest | Common |
| **Shy Shell** | Gastropod (snail) | "Don't overdo it" | open moments while it is out; hit too hard and it withdraws early | homing pearl | creep · duelist | Rare |
| **Boil Heart** | Gastropod, Cnidarian | "Burst the boils to reach the heart" | coax out (3–5 ink sacs, each a small meter; the pearl surfaces as they burst) | sac burst | creep or drift · territorial | Legendary |

Every boss belongs to one concept; tags (§8) only choose the fillers and the spice. *The Lid* with Queen Clam's
numbers is the reference roll the tests hold every generated Bivalve to (same readability, same fairness).

### 4.1 The spice (one per boss)

A single bounded twist from a short list, so two bosses of one concept still differ: an extra filler attack, a
faster tempo with longer windows, a mutation that advances at the first phase change, an asymmetric body whose
grown-out side is also where the attacks come from, the pearl mounted at the second-best site. The validator checks
it like everything else.

## 5. Mutations — how the Leak shows

Three tells, always all three (the Read-at-Range test, THEME-BIBLE §12):

1. **Wrong anatomy** — one **dominant theme** per boss, repeated across its sites so it reads as one coherent
   corruption rather than clutter, plus at most one minor theme (Rare and up). Themes by plan:
   - Bivalve: *overgrown shell* (layers piling past the animal), *ink sacs* along the mantle, *extra lid* (a third
     valve forcing the hinge).
   - Echinoderm: *extra arms* (two to four too many, crowding), *inverted spines* (spines grown inward through the
     test), *ink sacs* on the disc.
   - Cnidarian: *fused bells* (two bells grown into one), *extra tentacle cluster*, *ink sacs* in the bell.
   - Gastropod: a snail's *overgrown shell* (a second crooked whorl swallowing the foot), a sea hare's *extra flaps*,
     *extra tentacles* growing off the back, *ink sacs* along the mantle.
   No mutation is a face: no second mouth, no split head, nothing that grins (the No-Villain test).
2. **Ink look** — the shared corruption shading: glossy, light-drinking black spreading from the mutation sites like
   a stain under the skin, a cold violet rim, the rest of the body drained toward ash. The pearl is the blackest
   point, with the cold glint; the stain is always densest round it.
3. **The drip** — ink droplets welling up from the sites and lifting off, denser where the mutation is thickest;
   during the freeing they turn gold, drop by drop.

**Menace** sets how far it has gone: at Depth 1 the mutation is small (one extra arm, a single sac) and the true
body clearly visible; at Depth 6 the mutation is most of the mass. Silhouette sharpens with depth, saturation drops,
the body darkens and the rim brightens — the same roll at Depth 1 and Depth 6 reads as a gumdrop and a shadow.

**Ink sacs** are both anatomy and telegraph: they swell before the attacks that come from them.

## 6. The pearl

- **Mount**: a socket of the rig, named by the concept (mantle centre, heart of the bell, disc centre, between the
  spines, under the shell lip, inside the largest sac). It sits on her plane: her light must be able to reach it.
- A Coax-out body is soft: her light passes over it to the sacs and, once they are burst, to the pearl. A body whose
  pearl is shut away (a closed lid, a stagger boss between windows) stops her shots and Firefly Squid's surge.
- **Exposure rule**:
  - *Open moments* — reachable only while the body opens (lid, bell, mantle) on its own cycle.
  - *Stagger* — exposed for 1.2–2 s after its big attack.
  - *Moving window* — the pearl slides along the body under the skin; only the exposed arc takes light.
  - *Coax out* — buried; it surfaces as the ink sacs are burst one by one.
  - *Always, but small* — no timing; a small, mobile pearl (Depth 1 Common only).
  - *Flip/leap* — later pass (needs leaping plans).
- Outside its window, shots break on the body harmlessly, as on Queen Clam's shell.

## 7. The fight

The sim runs every generated boss through one cycle machine: **telegraph → act → window → recover**, its timings
from the concept, tempo and Menace. The arena (seal, banner, drop, landing wave, mud cloud, crust and condensation,
easing by the level cleansed, the freeing) is the one Queen Clam has today, shared by all.

### 7.1 Attacks (first pass)

| Attack | From (anatomy) | Tell | Notes |
|---|---|---|---|
| Pearl ring | radial bodies, open mouths | lid/bell opens, glint | always a gap ≥ 3 pearls wide |
| Pearl wall | lid lip, tentacle curtain | the edge lifts and quivers | always a hole ≥ 2 wide |
| Helix | mantle centre | a slow swirl of ink | arms by Menace |
| Homing pearl | mantle, shell lip | the pearl pulses violet | slow, can be popped (2 hits), slows her |
| Snap | lid, closing body | rim flashes, creak | only within 7 m, 0.7 s warn |
| Arc bombs | foot, disc, tentacle roots | the body rears, sacs swell | the Blotter's mortar and landing ring; stains the floor |
| Ink pulse ring | disc, bell rim | the body clenches | Blightroot's ring: stains, snuffs her shots, one hit per ring |
| Sac burst | ink sacs (only if rolled) | the sac swells and darkens | short radial burst; a burst sac is gone |

Shots are ink (`BossPearl` class): her light cannot clear them; they stop at the sealed rim.

### 7.2 Movement and facing

Hold · turn in place (≤ 1.5 rad/s, so flank windows stay real) · creep toward her (≤ 1.6 m/s) · drift with sudden
pulses · circle at range. The plan decides which are possible; the concept picks one. A boss never leaves the arena
and never pins her against the rim (it keeps 3 m from it).

**Senses**: *blind to her* (attacks on its own cycle, never aims — the fight is positioning), *water* (aims at her,
no line of sight needed) or *vibration* (aims where she was when she last dashed or fired). Rolled per concept.

### 7.3 Phases

Count: Depths 1–2: 1–2 · Depth 3: 2 · Depths 4–6: 2–3 (Legendary: 3). Triggered by the pearl's meter (50 %, or
33 % and 66 %), never by time. Each change is one of: a new attack joins, the tempo quickens, the window shortens,
the mutation advances (a sac splits open, an extra arm unfolds — visible on the body; a Coax-out boss's sacs grow
back, one fewer each time). A phase change always comes with a short stagger (1.5 s without attacks), as Queen Clam's
does.

### 7.4 Budgets (what keeps it fair and fun)

| | Depth 1 | Depth 6 |
|---|---|---|
| Pearl meter (HP) | 800 | 1600 (eased by up to 30 % with the level cleansed) |
| Telegraph | ≥ 0.8 s | ≥ 0.35 s |
| Window per cycle | ≥ 1.2 s and ≥ 30 % of the cycle | ≥ 1.0 s and ≥ 20 % |
| Hit damage | 12 | 18 |
| Pearls in flight | ≤ 45 | ≤ 75 |
| Fight length, played well | 60–90 s | 90–150 s |

The generator scales each concept's numbers into these and the validator checks them.

## 8. Tags

Every plan, concept, attack and mutation carries 2–3 tags (`radial`, `armoured`, `soft`, `drifting`, `sac-heavy`,
`spreader`, `close`, `ranged`, `stationary`). Fillers and the spice prefer the tags the concept already has, so a
boss's parts agree with each other; no pick ignores the tags.

## 9. The validator

A roll is re-rolled if it fails any of:

- **Winnable**: the pearl is on her plane and reachable in some window; windows meet the budget; every ring and
  wall has its gap; contact damage always has a recover window; the fight ends within the length budget against a
  simulated steady attacker.
- **Readable**: telegraphs at or above the floor; every attack comes from anatomy the body has (no sac burst
  without sacs); at most two attacks can be live at once.
- **Coherent**: one dominant mutation theme; attacks, pearl and mutation sites on the same part of the body where
  the concept says so; the size within the plan's range.
- **Theme tests** (THEME-BIBLE §12): it never out-glows her (Warmth); all three mutation tells present
  (Read-at-Range); no face or grin (No-Villain); its freeing melts, never shatters (Freeing); it sways like water
  (Sway); no camera move but the boss-awakening dip (Two-Breaks).
- **Determinism**: derived from `bossSeed` only.

## 10. Names

The boss intro (DESIGN-TOPDOWN §6.4) names it **CLEMENTINE VS ___**. Storybook names, `[Epithet] [Noun]`, the tone ramping with depth — big and
warm early, thin and cold deep. The noun follows the body (a star is never "Clam"); the epithet may name what the Leak
did, never the Leak itself, and never imply intent.

| Band | Epithets | Nouns by plan |
|---|---|---|
| Depths 1–2 | Old, Big, Sleepy, Grumbly, Queen, King, Mama, Papa, Lumpy, Three-Arm | Clam, Star, Urchin, Bell, Jelly, Slug, Snail, Gus-style given names |
| Depth 3 | Gilded, Twisted, Many-Armed, Swollen, Overgrown, Crooked | Clam, Star, Urchin, Bell, Curtain, Hare, Whelk |
| Depths 4–6 | Mother, The Hollow, The Pale, The Patient, Drowned, The Unmade, Half-Grown | Clam, Star, Spines, Bell, Siphonophore, Hare, Whelk |

"Queen Clam" is reserved for the reference roll.

## 11. Tiers

| Tier | Weight | Gives |
|---|---|---|
| Common | 70 % | a Common concept, 1 filler attack, the dominant mutation theme, phases by depth |
| Rare | 25 % | Rare concepts open, 2 fillers, asymmetry, a minor mutation theme |
| Legendary | 5 % | Legendary concepts open (Boil Heart), an advancing mutation, 3 phases |

Wildcards (twin bloom, molting, the stolen light, regret, twice-taken), arena modifiers (ink walls, Blightroot ring,
budding, shifting safe disc, dimming) and the murkling-budding attack join in a later pass, Legendary and Depth 3+
only (budding waits on the sealed arena letting murklings in).

## 12. The freeing

Shudder (0.3 s) → the pearl bursts white and stays where it sat → the drip turns gold, the ink stain drains out of
the body, the mutations shrink back into it and melt (1.2 s) → true colours flood back over the arena → the true body
(55–70 % of the size) moves away in its own manner — a jelly drifts up and off, a star creeps off, a slug slides away,
a clam shuffles aside and lies open — and is gone once off screen. The Crack opens as today.

## 13. Drawing (src/Game)

- One **rig** per body plan (`IBossRig`): builds its meshes from the `BossSpec` (parametric, built once per fight),
  exposes its sockets, and animates from the sim's state (open, telegraph, pulse, creep, stagger, freeing).
- One **boss shader** for all rigs: true colour ↔ ash by `drained`, the ink stain mask from the mutation sites and the
  pearl (glossy black, violet rim), shell/skin/jelly variants by plan.
- Shared pieces stay in `BossView`: the pearl (where the sim has it), the crust (lumps gathered on the mutation sites
  and over the pearl, two a layer; the stain deepens with the crust too), the drip, the pulse rings, the sacs bursting,
  the freeing, and the arena (shadow, landing wave, mud cloud, snap rim). A hit only brightens the body a little.
- Verification: `--boss=<Concept>` (or `queen`) names the boss, `--boss-level` starts on the depth's boss level,
  `--at=boss-near` and `--dbg-follow=boss` frame it.

## 14. Tests

- Same seed, same boss (every field of the spec); different cycles or depths, different bosses.
- 2000 seeds × Depths 1–5: every roll passes the validator within its attempts; every concept and plan occurs;
  tier frequencies near their weights.
- The reference roll reproduces Queen Clam's fight numbers.
- The fight sim: a scripted attacker frees every concept's boss within the length budget, and a passive player is
  never hit by an attack with no tell.
