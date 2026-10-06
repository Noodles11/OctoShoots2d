# Ink Deep — Game Design Document (v0.1, draft for approval)

> A twin-stick roguelite dungeon crawler in the spirit of *The Binding of Isaac*,
> set in a funky, comic-book underwater realm. You play **Clementine**, a small
> bioluminescent orange octopus, the last of her kind, diving after her family to the bottom of the ocean.

Status: **v1 vertical slice implemented** (see §17 for what shipped and what differs).
Decisions are recorded in §0. Sections marked *(post-v1)* are future content.

---

## 0. Decisions (from Q&A)

| Topic | Decision |
|---|---|
| v1 scope | **Full dive**: Depths 1–6 plus The Tank (7), 60 items, 12 bosses incl. The Hand, 25 enemies, 14 synergies, 5 transformations (§28). |
| Run length | **Progressive, Isaac-style.** First runs end after Depth 1. Each first boss kill / goal unlocks the next depth (§5.1). |
| Resolution | **Smooth HD 960×540** logical, scaled to window. |
| Camera | **Side view — "fish tank"** (§11.1). Free 8-direction swimming, no platforming. Floor at the bottom, water surface/ceiling at the top. |
| Buoyancy | Clementine **very slowly sinks** when no input is held (§4). |
| Descent mood | The deeper she dives, the **darker, sadder and lonelier the world and the more ruthless the mobs** (§5.3). |
| Story | **"Ink Deep"**: the last octopus of a reef corrupted by the Leak, diving after her family (§1). No villain; corrupted creatures are freed, not killed. |
| Finale twist | Below the Abyss, a **pipe** in the Crack sucks Clementine up into the **human's fish tank** that broke and leaked into the sea. Final boss: **The Hand** (§5.4, §12.3). |
| Descending | The next depth is reached through **The Crack** — a fissure in the boss room floor, guarded by the boss (§5.2). |
| Clementine | **Bioluminescent octopus**: breathing mantle + eight physically simulated arms (§20; was a jellyfish, §11.2). |
| Glow | **Neon bloom** on shots, damage, pickups, synergies (§11.3). |
| Water | **Living water**: cosmetic fluid sim drives bubbles, fish, plants, currents (§11.4). |
| Renderer | **PixiJS v8 (WebGL2)** + custom shaders — needed for bloom, refraction, caustics at 60 FPS (§14). |
| Familiars / trinkets | **Cut from the plan.** No familiars, no trinkets. |
| Audio | **Synth SFX only** (WebAudio). Music post-v1. |
| Meta | **Pure unlocks** (achievements). No Pearls / hub currency. |
| Characters | **Clementine only** in v1. Roster post-v1. |
| Seeded runs | Custom seeds **do not** grant unlocks (Isaac rule). |
| Saves | **1 profile** + copyable export/import code. |
| Input | **Keyboard only.** |
| Deploy | **GitHub Pages** via GitHub Actions. |

---

## 1. Pitch & Lore — "Ink Deep"

One day **the Crack** opened in the sea floor, and the reef has never been the same.
Out of it came fish in colours no reef had ever seen (neon, glossy, *wrong*), and from
above, warm, too-bright water began to seep down into the reef: **the Leak**. Whatever the
Leak touches changes. Familiar neighbours turn strange colours and lash out at anything
that moves. And one by one, every octopus on the reef has been dragged down into the Crack.

**Clementine**, a small tangerine octopus who glows a little too much, is **the last of her
kind** on the reef. She dives after her family.

- **No villain.** The sea life she fights is not evil: it is **corrupted** by the Leak.
  Defeating a creature washes the corruption out of it: it bursts free in a "poof" of
  bubbles, its true colours return, and it swims on, healed (DEPTH1-BESTIARY §8). She is not
  killing her neighbours; she is setting them free on her way down. The only real adversary
  is met at the very end: **The Hand** (§12.3).
- **The truth (revealed at the end).** High above the sea stands a human's home **aquarium**,
  joined to the ocean by a long pipe whose mouth lies at the bottom of the Crack. The tank
  broke in one place: some of its fish found their way back to the ocean, but the tank had
  already changed them, and its water bleeds down into the reef as the Leak. The pipe still
  draws creatures up into the tank; the octopuses were taken that way. Clementine is pulled up
  after them and must face **The Hand** (§5.4).
- **Tone arc: the sadness grows with depth.** Depth 1 is bright and playful, with an ache
  underneath: empty octopus dens, healthy fish still everywhere. Each depth down there are
  fewer healthy neighbours and more corrupted ones, the light fades, and the reef grows quiet
  and lonely. The finale snaps into absurd, bright, *artificial* comedy-horror.
- **Foreshadowing:** the corrupted creatures carry bits of the tank with them (neon colours,
  aquarium gravel, plastic plant scraps, DEPTH1-BESTIARY §8). From Depth 4 on, odder things
  appear: straight pipes in the far background, a plastic castle shard, a fish-food flake,
  muffled *thump… thump…* (glass tapping) in the Abyss.
- **Clementine's power:** she throws **bubbles** from her tentacle and leaves **ink** when she
  dashes. Items are "gifts of the sea" that change her glow, body and tentacles.
- **Meta-lore:** the **Tide Pool Hub** between runs fills with the octopuses she rescues and
  the creatures she frees (unlocks), each adding NPCs, decorations and dialogue.

## 2. Core Loop

```
Title / Tide Pool ──> new run (random or seed) ──> Run: unlocked depths only
      ^                                               │
      │      achievements → unlocks (meta)            │ die or win
      └───────────────────────────────────────────┘
```

**Run:** clear rooms → collect pickups & items → find boss room → beat boss →
descend. Death is permanent for the run; meta progress persists.

## 3. Controls

| Action | Keyboard | Gamepad (stretch) |
|---|---|---|
| Move | **W A S D** | — |
| Shoot (4-dir, like Isaac) | **Arrow keys** | — |
| Use active item | **Space** | — |
| Drop Ink Bomb | **E** | — |
| Use consumable (Shell card/pill) | **Q** | — |
| Map (hold) | **Tab** | — |
| Pause | **Esc / P** | — |
| Restart run (hold) | **R** | — |

Gamepad column intentionally empty: keyboard only in v1.

Shots inherit a portion of movement velocity (Isaac-style). Diagonal shooting via
two arrows is **off by default** (faithful to Isaac) — toggle in options.

## 4. Player: Clementine

Isaac-style stat block (all modified by items):

| Stat | Base | Notes |
|---|---|---|
| Health | 3 hearts (6 half-hearts) | Heart types below |
| Damage | 3.5 | |
| Fire rate | 2.7 shots/s | "Tears" → **Bubbles** |
| Shot speed | 1.0 | |
| Range | 6.5 tiles | Bubbles pop at end of range |
| Speed | 1.0 | Floaty acceleration — she's a jellyfish |
| Luck | 0 | Proc chances |

**Movement feel:** free swimming in all 8 directions. Slight inertia and a
"pulse" squash-and-stretch on every direction change (jellyfish propulsion).
Not slippery — responsive first. Floor and rock surfaces are solid collision.

**Slow sink:** with no movement input, Clementine **drifts down very slowly**
(~0.15 tiles/s, eased in over ~0.5 s so it never fights the player), bell
relaxed and tentacles floating up around her like a real resting jellyfish.
She settles softly on the floor with a tiny sand puff. Any input cancels the
sink instantly. Items can change it (e.g. Whale Lung = floaty, zero sink).

**Side-view physics for objects (gameplay, deterministic):**
- **Pickups sink** slowly and rest on the floor/ledges (hearts, coins, keys);
  **Foam hearts float** up and rest under the ceiling — a readable rule.
- **Ink Bombs sink** with a slow arc, can roll down slopes, explode on a fuse.
- **Clementine's bubbles** fly straight (unaffected by buoyancy) unless an item changes that.

### Health types
- **Coral Hearts** (red) — normal, refillable containers.
- **Foam Hearts** (blue/white) — temporary shield hearts, lost when depleted.
- **Abyss Hearts** (black/purple) — temporary; on loss, deal damage to all enemies in room.
- **Golden Scale** — overlays a heart; drops coins on hit.

### Unlockable characters *(post-v1)*
1. **Clementine** — balanced.
2. **Barnaby the Hermit Crab** — low speed, high HP, starts with a shell-shield active.
3. **Pip the Seahorse** — fast, fragile, shots curve.
4. **Nori the Octopus** — shoots ink that slows, 8-way shooting.
5. **The Glowless** (hard mode Clementine) — 1 heart, all hearts are Foam.

## 5. World Structure

6 **Depths** (floors) plus the secret finale **The Tank**, each with a biome,
palette, enemy pool, and boss pool. Each depth is **one** generated floor.
v1 ships Depths 1–3; 4–6 and The Tank are post-v1.

| # | Depth | Palette / vibe | Hazards |
|---|---|---|---|
| 1 | **Sunlit Shallows** | Turquoise, sand, pink coral | Sea urchins (spike tiles) |
| 2 | **Kelp Jungle** | Greens, yellow light shafts | Tangling kelp (slow tiles) |
| 3 | **Sunken Galleon** | Browns, gold, rust | Cannonball turrets, loose barrels |
| 4 | **Coral Carnival** | Hot pink, purple, neon | Bounce anemones |
| 5 | **Twilight Trench** | Deep blue, glowing dots | Darkness (limited light radius around Clementine) |
| 6 | **The Abyss** | Black + neon outlines | Currents that push you |
| 7 | **The Tank** *(twist)* | Fluorescent LED white, neon-blue gravel, plastic colors | The Hand, net, glass taps, filter suction |

Beating **The Hollow Maw** (Depth 6) reveals the pipe → **The Tank** (§5.4).
Beating **The Hand** = true ending. Post-win unlock: **Alt path /
"Volcanic Vents"** loop for harder runs (post-v1).

### 5.1 Progressive depth unlocks ("The Dive Gets Deeper")

Like Isaac's Mom → Womb → Cathedral progression, the run's end point moves
deeper as the player proves themselves:

| Unlock | Requirement | Effect on runs |
|---|---|---|
| Start | — | Run = Depth 1 only. Boss kill → "Clementine surfaces" ending, run won. |
| **Dive 2** | Beat any Depth 1 boss once | Boss room's **Crack** now opens down to Kelp Jungle. Run ends at Depth 2 boss. |
| **Dive 3** | Beat any Depth 2 boss once | Crack opens to Sunken Galleon. Run ends at Depth 3 boss. |
| **The Admiral's Key** | Beat Rusty Admiral 3× | Adds a Key item; post-v1 it opens Depth 4. |
| *Dive 4–6 (post-v1)* | Boss kills + item gates | e.g. carry **The Lantern Pearl** into Depth 5 to light the Trench. |
| *The Tank (post-v1)* | Beat The Hollow Maw once | First kill: the pipe appears and the run ends on a cliffhanger panel. From then on the pipe pulls you into The Tank. |

After each new unlock: a short comic-panel cutscene (3–4 panels, procedurally
composed from existing sprites + captions) — "The current pulls deeper…".

Each ending also unlocks content (items into pools), so early short runs
already feed meta progress. The **surface choice**: when the next depth is
unlocked, beating a boss shows both the Crack (continue) and a **Surface
Bubble** rising at the top of the room (end run as a win now) — like Isaac's
chest vs. trapdoor. While the next depth is still locked, the Crack stays
sealed with a glowing "?" rune — a visible promise of what's below.

### 5.2 The Crack (descending)
- Every floor's boss room is a wide arena with **The Crack** — a glowing
  fissure in the sea floor — at the bottom center.
- **The boss guards it:** it sits on/over the Crack or circles it. Boss intros
  show it rising out of the fissure.
- On the boss's death: screen shake, the boss's color returns, the rock splits
  open with light pouring up, a strong updraft of bubbles, and the reward item
  + Heart Container float down from above.
- Clementine swims into the Crack → comic-panel transition of her sinking
  through a dark tunnel → next depth's start room (she enters from the top).
- Each depth start room has a light shaft from the crack above her, so the
  descent reads visually across floors.

### 5.3 The Descent Curve — darker, sadder and more ruthless

One global parameter, **Menace** (0.0 at Depth 1 → 1.0 at Depth 6), drives
mood, visuals and enemy behavior, so the tone shift is systemic, not just
new art.

| Depth | Light & mood | Ambient life | Mobs |
|---|---|---|---|
| 1 Shallows | Bright sun, god rays, saturated, surface shimmer | Big friendly fish schools, lots of bubbles | Cute, round eyes, wander a lot, slow, long telegraphs |
| 2 Kelp | Dappled green, fewer rays | Fish shyer | Chase more, small groups |
| 3 Galleon | Amber, dusty, rays only through holes | Few fish, drifting debris | Ambushes, projectiles faster |
| 4 Carnival | Neon but "off" — flickering lights | Fish hide in coral | Grins with teeth, coordinated flanking |
| 5 Trench | Dark; light only from glows (radius around Clementine) | Bioluminescent specks, no schools | Stalkers, strike from darkness |
| 6 Abyss | Near-black, neon outlines, heavy marine snow | Only distant glowing eyes | Ruthless: relentless chase, fast, minimal telegraphs |

**Visual menace (procedural, per enemy):** the same archetype is drawn with
parameters that shift with Menace — round eyes → slit pupils with glowing
irises, smile → teeth (count scales), smooth outline → spiky/jagged,
saturation ↓, darker body with bright rim-light. A Depth 1 Blubber Blob is a
grinning gumdrop; a Depth 6 one is a toothy shadow with red eyes.

**Behavioral menace:**
| Parameter | Depth 1 → Depth 6 |
|---|---|
| Aggression (chase vs wander) | 40% → 100% |
| Move speed | ×1.0 → ×1.5 |
| Projectile speed / count | ×1.0 → ×1.4 / +0–2 per volley |
| Telegraph time | 0.8 s → 0.35 s (never lower — stays fair) |
| Group tactics | none → flanking, surrounding, baiting |
| Champion chance | 0% → 25% |
| Room enemy budget | ×1.0 → ×1.8 |

**Sadness:** the share of corrupted creatures rises with depth (Depth 1: most of the life around is healthy; Depth 6: almost none is), empty octopus dens become more common, and the reef grows quieter. Freed creatures still burst back into colour at every depth: small bright moments that matter more the darker it gets.

**Presentation shifts:** comic panel borders go from clean to torn/inked;
onomatopoeia turns from bubbly ("BLUB!") to jagged ("KRSSH!"); SFX pitch
lowers; Clementine's own glow becomes the main light source, making glow
items feel precious. Clementine herself never gets scary — she's the
warm spot of color in the dark.

### 5.4 The Twist — The Tank

1. **The Hollow Maw** (Depth 6), the oldest and most corrupted of the tank's escapees, is
   fought over the Crack as usual. Freed, it spits out a **rusty metal grate**: the Crack is a pipe.
2. Cutscene (comic panels): the grate rattles, a **roar of suction**, all
   particles, fish and bubbles stream toward it, Clementine is pulled in —
   *"SHLUUUURP!"* — tumbling through a pipe with light at the end.
3. **The Tank:** a short final floor (boss room + 2–4 small rooms, fixed
   layout variants). The look **flips**: flat fluorescent LED light, blue
   neon gravel, fake plastic plants that **don't sway** (deliberately
   uncanny against the living-water sim), a bubbling treasure-chest
   ornament, a plastic diver, a castle. Behind the glass: a giant blurry
   living room — lamp, TV glow, a cat's eye passing by.
4. **The octopuses** of Clementine's reef float listlessly here, among other captured creatures:
   rescued on victory.
5. **Final boss: The Hand** (§12.3). Afterwards, the true ending (§12.3).

## 6. Procedural Generation

Everything derived from a single **run seed** (see §10).

### 6.1 Floor layout (Isaac-style grid, seen from the side)
- Floor = 13×13 grid of room slots, where **x = left/right** and
  **y = up/down in the water column**. Start room in the top row area
  (you just came down), boss room placed in a low row where possible
  (the dive goes *down*).
- **Connections:** left/right neighbours via **side passages** in the walls;
  up/down neighbours via **wide vertical shafts** (open gaps in the ceiling /
  floor). Normal shafts are plain rock openings — visually distinct from
  the glowing boss Crack so they're never confused.
- Room count: `min(20, round(3.33 * depth + rand(5,6)))`.
- Breadth-first expansion from start: a neighbor cell is added only if it has
  ≤1 filled neighbor (keeps a branching, maze-like shape), with random rejection.
- **Dead ends** (1 neighbor) are assigned special rooms, farthest first:
  **Boss** (farthest), **Treasure**, **Shop**, then optional **Secret**, **Curse**,
  **Challenge**, **Sacrifice**, **Library (Shipwreck Archive)**.
- **Secret room:** placed in an empty cell with the most (≥3) room neighbors;
  opened by bombing a cracked wall, ceiling, or floor (sinking bombs make floor secrets natural).
- **Big rooms** from depth 2+: wide caverns (2×1), **tall shafts (1×2)** with
  vertical dives, 2×2 grottoes, L-shapes. Camera scrolls within big rooms.
- Regenerate if constraints fail (not enough dead ends) — deterministic retry with sub-seed.

### 6.2 Room interiors (side-view "tank")
- Room = one screen, **20×11 tiles of 48px** (960×528). Big rooms are multiples.
- Anatomy, bottom → top:
  - **Sea floor:** a procedural heightline (noise, smoothed) with sand, rocks,
    slopes and dips. Most decoration lives here: kelp, coral, anemones, shells,
    shipwreck debris, treasure pots.
  - **Mid water:** mostly open for combat; occasional **floating rock islands,
    coral arches, hanging stalactites, sunken masts, bubble vents**.
  - **Ceiling:** rock overhang in deep biomes, or the **shimmering water
    surface** in Sunlit Shallows (you can't leave it — the light is too bright).
- **Generation:** hand-authored obstacle templates (JSON: rock islands, arches,
  pits, spike clusters, enemy slots) are stamped into a procedural terrain
  (floor heightline + ceiling line), with mirroring (horizontal only — keeps
  "floor is down"), biome variant swap and decoration scatter.
- A **procedural room generator** makes fully new layouts from noise.
  Validation: flood fill guarantees every door/shaft is reachable with a
  Clementine-sized clearance, no enclosed pockets with enemies.
- Pickup rest points are precomputed (floor tops / ledge tops) so sinking loot
  never gets stuck in unreachable spots.
- Enemy slots are filled from the depth's weighted **enemy pool** with a
  difficulty budget per room.

### 6.3 Everything else procedural
- Item pools per room type, shop inventory/prices, chest contents, pickup drops
  on room clear (Isaac-like reward table with luck bonus), boss choice, curses.
- **Art is procedural too:** all sprites drawn at runtime with Canvas vector
  shapes (see §11) — no bitmap assets needed.
- **SFX** generated with WebAudio synth. Music post-v1.

## 7. Rooms & Special Rooms

| Room | Icon | Content |
|---|---|---|
| Normal | — | Enemies, reward on clear |
| Treasure | 🏆 | 1 item from Treasure pool (Shells-key locked from depth 2) |
| Shop | 💰 | 2–5 items/pickups, paid with **Sand Dollars** |
| Boss | 💀 | Arena with **The Crack**. Boss → item + Heart Container; Crack opens down (§5.2) |
| Secret | ❓ | Bombable; rare items / big pickups |
| Curse (Urchin Den) | ☠ | Enter costs damage; Abyss item pool |
| Sacrifice (Anemone Altar) | ⚱ | Step on spikes for escalating rewards |
| Challenge (Arena) | ⚔ | Waves of enemies for an item |
| Shipwreck Archive | 📜 | Scrolls (consumables) |
| **Mermaid's Grotto** (Devil/Angel equivalent) | 🧜 | After boss, chance-based: trade heart containers for powerful items (**Siren deal**) or free holy item (**Whale Song**) |

## 8. Pickups & Economy

| Isaac | Clementine's Quest | Use |
|---|---|---|
| Coins | **Sand Dollars** (1/5/10) | Shops, beggars |
| Bombs | **Ink Bombs** | Break rocks, secret walls, damage |
| Pills | **Sea Snacks** (unidentified until eaten) | Random effect, randomized per seed |
| Cards/Runes | **Tarot Shells** | One-use effects |
| Batteries | **Glow Jellies** | Recharge active item |
| Chests | **Clams** (normal/golden/cursed) | Loot |

Per-seed randomized **Sea Snack** identities (e.g. "Purple Krill" = +speed this
run) — a nice seed-memory mechanic.

## 9. Items

### 9.1 Item system architecture (important for synergies)
Items are **data + modifiers**, not bespoke code:
- **Stat modifiers** (flat/multiplier, applied in fixed order).
- **Bubble (projectile) modifiers** — composable flags & behaviors:
  `homing, piercing, spectral, splitting, bouncing, orbiting, boomerang, chain,
  explosive, poisoning, freezing, charm, burn, growing, wave-motion, trail,
  multishot(n), charge-shot, laser, bomb-shot`.
- **Triggers** — `onHit, onKill, onRoomClear, onDamaged, onShoot, onPickup, onFloorStart`.
- **Transform tags** — collecting 3 items with the same tag grants a
  **Transformation** (Isaac-style, e.g. "Guppy").

Because shot behaviors compose, most synergies emerge naturally. A small
**explicit synergy table** adds bespoke visuals/bonuses on top (§9.4).

### 9.2 Item pools
Treasure · Shop · Boss · Secret · Mermaid (Siren) · Whale Song (Angel) · Curse ·
Golden Clam · Beggar *(post-v1)*. Each item has `quality 0–4`, weight, pool list, unlock condition.

### 9.3 Item list (full target ~60, then 100+)

**v1 vertical slice ships ~30:** marked ★ below; rest post-v1.

**Passive — stats**
| Item | Effect | Lore flavor |
|---|---|---|
| ★ Coral Crown | +1 heart, +0.3 dmg | A reef princess's lost tiara |
| ★ Squid Ink Espresso | +0.3 speed, +fire rate | "The Galleon's cook swore by it" |
| ★ Whale Lung | +1 range, bubbles are bigger, no idle sink | Deep breath! |
| ★ Pufferfish Pout | ×1.5 damage, −shot speed | Angry and proud of it |
| ★ Lucky Sea Glass | +2 luck | Found only on moonlit tides |
| ★ Plankton Swarm | +0.5 fire rate ×3 small bubbles | Tiny friends |
| ★ Barnacle Armor | +2 Foam hearts, −0.1 speed | Clingy but protective |

**Passive — bubble modifiers**
| Item | Effect |
|---|---|
| ★ Electric Eel Tail | Bubbles **chain lightning** to 2 nearby enemies |
| ★ Nautilus Spiral | Bubbles **spiral outward** |
| ★ Mirror Scale | Bubbles **bounce** off walls |
| ★ Anglerfish Lure | Bubbles **home** onto enemies |
| ★ Swordfish Bill | Bubbles **pierce** enemies |
| ★ Ghost Jelly | Bubbles are **spectral** (pass rocks) |
| ★ Mitosis | Bubbles **split in 2** on hit |
| ★ Frost Kelp | Chance to **freeze**; frozen enemies shatter into shards |
| ★ Fire Coral | Bubbles **burn** |
| Sea Nettle Sting | **Poison** damage over time |
| ★ Boomerang Shrimp | Bubbles **return** to Clementine |
| ★ Pearl Diver | **Charge shot** — hold to fire a large pearl |
| ★ Sunbeam | Replaces bubbles with a **laser beam** (charge) |
| ★ Double Helix | **Wave motion** + 2 shots |
| ★ Triple Tentacle | **Triple shot** (−damage) |
| ★ Starfish Arm | Bubbles **grow** with distance |
| ★ Ink Sac | Bubbles become **ink bombs** (explosive) |
| ★ Siren Song | Chance to **charm** enemies |

**Active items (Space; charge by room clears)**
| Item | Charge | Effect |
|---|---|---|
| ★ Conch Horn | 3 | Stun + knock back all enemies |
| ★ Bubble Shield | 2 | Invulnerable bubble for 3s |
| Tidal Wave | 4 | Pushes & damages everything in a direction |
| ★ Treasure Map | 6 | Reveal floor + secret rooms |
| ★ Mimic Clam | 6 | Reroll room pedestals (Isaac's D6) |
| ★ Glow Burst | 1 | Your next 5s bubbles triple; screen flash |
| Sea Dice | 3 | Re-roll pickups in room |
| Kraken Summon | 6 | Tentacles slam random enemies |

### 9.4 Synergies (explicit, with special visuals)

Emergent combos + named synergies with unique effects:

| Combo | Name | Result |
|---|---|---|
| Electric Eel Tail + Mirror Scale | **Pinball Storm** | Each wall bounce emits a lightning arc |
| Sunbeam + Nautilus Spiral | **Lighthouse** | Laser sweeps in a rotating spiral |
| Mitosis + Starfish Arm | **Cell Bloom** | Split bubbles also grow — screen fills with glowing orbs |
| Ink Sac + Anglerfish Lure | **Guided Inkfish** | Homing bombs; explosions leave slowing ink puddles |
| Frost Kelp + Fire Coral | **Steam Vent** | Frozen enemies hit with fire explode into steam clouds (AoE, blinds) |
| Boomerang Shrimp + Swordfish Bill | **Tuna Rang** | Piercing boomerang that hits twice & grows on return |
| Pearl Diver + Mitosis | **Pearl Necklace** | Charged pearl bursts into a ring of 8 pearls |
| Sunbeam + Pearl Diver | **Prism Pearl** | Charged pearl fires lasers in 4 directions when it pops |
| Tidal Wave + Ink Sac | **Black Tide** | Wave drags ink bombs along and detonates them |
| Pufferfish Pout + Starfish Arm | **Big Mad Puff** | Bubbles inflate with spikes & shotgun burst on pop |
| Ghost Jelly + Anglerfish Lure | **Will-o'-Wisp** | Spectral homing wisps; phase through everything |
| Sea Nettle Sting + Mitosis | **Toxic Bloom** | Split ink carries a double dose of poison |
| Shark Tooth + Lamprey Mouth | **Feeding Frenzy** | Every kill sends you into a fast-firing frenzy |
| Cannonball + Rear Fin | **Broadside** | Shots out of the back explode |

**Transformations (3 items with the same tag; all five ship):**
- **Kraken Form** (tag: tentacle) — 8-way shooting, ink trail.
- **Neon Rave** (tag: glow) — bubbles cycle colors, +damage, screen pulses to music.
- **Shark Mode** (tag: predator) — speed and damage up, heal 2 HP per kill.
- **Coral Reef** (tag: coral) — the reef mends you: slowly regain health.
- **Pirate** (tag: galleon) — +1 damage, +2 luck, foes drop more coins.

## 10. Seeded Runs

- **Seed format:** 8 characters, Isaac-style e.g. `KELP 7Q2Z` (A–Z, 0–9, no
  ambiguous chars). Shown on pause screen; copyable.
- **PRNG:** `sfc32` / `mulberry32` — fast, deterministic, pure JS.
- **Stream separation:** master seed → independent sub-streams:
  `layout[depth]`, `rooms[depth][room]`, `items`, `pickups`, `enemyAI`, `cosmetic`.
  Player actions (e.g. killing enemies in a different order) must not change
  what the next treasure room contains → item pool draws are seeded per room.
- Same seed + same choices ⇒ identical run layout & items.
- **Custom-seeded runs don't grant unlocks/achievements** (Isaac rule). The run
  still uses the player's currently-unlocked depths and item pools, so a seed
  reproduces identically for players with the same unlock state.
- **Daily Dive** *(post-v1)*: date-derived seed, one attempt per day, local best.
- **Special seeds** (easter eggs): e.g. `BIGG JELL` = Clementine is huge.

## 11. Visual Style — "Funky Comic Reef, Living Water"

### 11.1 Look & camera
- **Side view, like looking into a fish tank.** Floor at the bottom, surface /
  ceiling at the top, parallax depth layers behind the play plane.
  Fixed 960×540 logical resolution, scaled to window, letterboxed.
- **Tank framing:** the comic panel border doubles as the aquarium frame;
  faint glass reflection streak and slight edge vignette (toggleable).
- **Light gradient:** brighter at the top, darker toward the floor; each depth
  starts darker overall. The floor is lit by caustics from above.
- Mobs are drawn in profile — fish, crabs, eels, octopi read far better from
  the side, with clear silhouettes and faces.
- **Comic look:** thick ink outlines (3px), flat bold fills, halftone dot
  shading, 1 highlight per shape, saturated tropical palette.
- All characters are **procedurally drawn vector shapes**. No art pipeline.
  Items visually alter Clementine (e.g. Coral Crown sits on her bell).
- **Style rule:** comic outlines on gameplay objects (always readable);
  soft, painterly, glowing treatment on the water/ambient layers (never
  competes with gameplay). Ambient ≤ ~30% contrast of gameplay layer.

### 11.2 Clementine — soft-body jellyfish
- **Orientation:** seen from the side — dome-shaped bell on top, tentacles
  hanging below. The bell **tilts toward the swim direction** (up to ~60°);
  tentacles stream out behind/under her. Swimming up = classic jellyfish
  pulse; swimming down = bell tips forward, tentacles lift and stream upward.
- **Bell:** a deformable ring of ~16 control points. Each movement impulse
  plays a **pulse cycle**: bell contracts (narrow + taller), then relaxes
  (wide + flat). Propulsion force is applied on the contraction, so motion
  matches animation — just like a real jellyfish.
- **Tentacles:** 4 long oral arms + 8 thin marginal tentacles. Each is a
  **verlet chain** (8–12 points) anchored to the bell rim, with:
  - water drag + slight buoyancy → **delayed, fluid trailing** behind movement,
  - distance & bending constraints → no stretching, soft curls,
  - sampled by the water velocity field (§11.4) → they sway with currents
    and ripple when she turns or stops,
  - idle: slow sine drift so they never look frozen.
- Rendered as tapered ribbons with translucent gradient + inner glow; the bell
  is semi-translucent (tentacle bases visible through it).
- **Shooting:** bell squeezes toward the shot direction, a small recoil pulse
  and a neon flash at the bell rim.
- **Hurt:** bell squashes, tentacles whip outward, magenta neon rim flash,
  brief chromatic-aberration wobble.
- Enemies reuse the same soft-body toolkit (jelly swarm, squid tentacles,
  anemone fronds, the Siphonophore chain boss).

### 11.3 Neon glow (bloom)
A dedicated **glow layer**: anything drawn there is blurred and added on top
(additive bloom). Glow is an event language, color-coded:

| Event | Glow |
|---|---|
| Firing | Rim flash on Clementine, bubble core glow, short light trail |
| Projectile hit / pop | Expanding neon ring + light burst lighting nearby floor |
| Player damage | Magenta rim flash, screen-edge pulse, brief hit-stop |
| Enemy damage | White-hot flash on the enemy outline |
| Pickups / item pedestal | Slow breathing glow, color by rarity |
| Synergy / transformation | Big chromatic ripple + unique color signature |
| Explosions (Ink Bombs) | Bright core → dark ink cloud (inverse glow) |

- Projectiles are **light sources**: they softly light the sand and
  decorations as they pass (cheap 2D lighting via additive light sprites).
- Twilight Trench: darkness layer where only glows reveal the room.
- Reduced-flash option dims bloom intensity and removes screen pulses.

### 11.4 Living water (cosmetic simulation)
Goal: the water feels alive and physical but **ambient** — never distracting.

**Water velocity field** — a low-res 2D fluid grid (~64×36 cells per room),
simplified *Stable Fluids* (advect → diffuse → decay), updated each frame.
- **Everything that moves injects impulses**: Clementine's bell pulse (a ring
  push behind her), projectiles (thin wakes), enemies, explosions (radial
  blast), doors opening, bosses (large swirls).
- A gentle **biome base current** + slow curl-noise gives constant drift.
- Everything ambient samples this field, so one push ripples through the scene.

**What the field drives**
| Element | Behavior |
|---|---|
| **Bubbles** | **Rise upward** with buoyancy, wobble, slightly grow, merge when touching, pop at the surface/ceiling (tiny ring, collect under rock overhangs as shimmering pockets). Spawned from floor vents, Clementine's pulses, hits, sand, dying enemies. Pushed sideways by currents. |
| **Marine snow / plankton** | Hundreds of tiny particles drifting; swirl visibly in wakes — the main way currents become *visible*. |
| **Current streaks** | Very faint, stretched light streaks along strong flow; fade fast. |
| **Floating fish** | Background **boids** schools on 2 parallax depth layers (smaller + bluer when farther): cohesion/alignment/separation, flee from Clementine, projectiles & explosions, reform afterwards. Non-interactive. |
| **Kelp, sea grass, anemones** | Verlet chains / springs anchored to floor; sway with field, bend away when Clementine swims through, wobble after explosions. |
| **Coral, shells, rocks** | Subtle squash-wobble (spring) on nearby impacts. |
| **Sand** | Puffs of sediment on impacts and fast movement near the floor; settles back down slowly. Sinking pickups kick up a small puff when they land. |
| **Light** | Slanted **god rays** from the surface, slowly swaying; animated **caustics** on the sea floor and rock faces; surface shimmer line in shallow rooms. Intensity drops with depth. |
| **Refraction** | Full-screen shader distortion, driven by the velocity field — tiny heat-haze wobble in wakes and blast rings. |

**Layer stack (back → front)**
1. Far background: vertical depth gradient (light top → dark bottom), fog, far reef/wreck silhouettes, far fish schools (parallax).
2. Mid background: back rock wall, near fish schools, god rays.
3. Terrain: sea floor, rocks, ceiling/surface + caustics + projectile light.
4. Decorations: kelp, coral, anemones (simulated), mostly along the floor.
5. Gameplay: pickups, enemies, Clementine, projectiles (comic outlines).
6. Glow layer (bloom).
7. Foreground: bubbles, marine snow, out-of-focus kelp/coral silhouettes at the bottom edge (in front of the glass).
8. Post: refraction, color grade per biome, vignette, chromatic aberration on hit.

**Rules**
- **Cosmetic only.** The fluid never changes gameplay physics, so runs stay
  deterministic and fair for seeds. (Design-level hazard currents in the Abyss
  are separate, deterministic gameplay forces — and they also push the fluid.)
- Uses the `cosmetic` RNG stream only.
- **Quality presets** (Low / Medium / High / Auto): fluid grid size, particle
  counts, fish count, bloom resolution, refraction on/off. Auto drops quality
  if frame time > 16.6 ms for a few seconds.

### 11.5 Comic FX, UI, accessibility
- **Comic FX:** onomatopoeia pop-ups ("BLUB!", "ZAP!", "SPLOOSH!", "POP!") on hits/kills,
  speed-line bursts, screen shake, hit-stop (2–3 frames) on heavy hits,
  panel-style room transitions (wipe with a comic panel border).
- **Boss intros:** comic-cover splash screen ("ISSUE #3: THE RUSTY ADMIRAL!").
- **UI:** chunky rounded font (Google Font e.g. *Bangers* for titles, *Fredoka*
  for UI), speech-bubble tooltips for item pickups ("PUFFERFISH POUT — Big mad energy").
- Accessibility: color-blind-safe projectile outlines, screen-shake toggle,
  reduced-flash toggle, ambient-motion toggle (reduces water motion).

## 12. Enemies & Bosses

### 12.1 Enemy archetypes (~25 at launch)
The side view gives four **movement classes**, which makes rooms read clearly:
**Swimmers** (free 2D), **Walkers** (floor/ledges only), **Clingers** (stuck
to floor, walls or ceiling), **Burrowers** (hide in sand/holes).

| Enemy | Class | Behavior |
|---|---|---|
| Blubber Blob | Swimmer | Slow chase |
| Sea Urchin | Clinger | Stationary on any surface, shoots 8-way |
| Crabby | Walker | Scuttles along the floor, leaps up when you're above it |
| Pufferling | Swimmer | Inflates when close, bursts into spikes |
| Moray Pop-up | Burrower | Hides in wall holes, lunges out |
| Barracuda | Swimmer | Dashes in straight lines |
| Jelly Swarm | Swimmer | Tiny, erratic, in groups |
| Lanternfish | Swimmer | Shoots homing light orbs (darkness levels) |
| Cannon Crab | Walker | Galleon: lobbed cannonballs that sink in arcs |
| Clown Anemone | Clinger | Carnival: spawns bouncing balls |
| Ghost Shrimp | Swimmer | Invisible until close (Trench) |
| Mimic Clam | Walker | Sits on the floor, looks like a chest |
| Splitter Slime | Swimmer | Splits into 2 smaller |
| Sand Flounder | Burrower | Flat in the sand, bursts up when you pass over |

Champion variants (colored, with modifier) from depth 2+.

### 12.2 Bosses (2 per depth, pick 1 by seed)
| Depth | Bosses |
|---|---|
| 1 | **Big Barnacle Bill** (spawns barnacles) · **Queen Clam** (pearl barrage) |
| 2 | **Kelpie the Tangler** (vines across room) · **Sir Urchin** |
| 3 | **The Rusty Admiral** (crab in a cannon hat) · **Treasure Mimic** |
| 4 | **Ringmaster Octo** (juggles enemies) · **Jester Jellies** (trio) |
| 5 | **Mother Angler** (light/dark phases) · **The Siphonophore** (long chain enemy) |
| 6 | **The Hollow Maw** (3 phases; the tank's oldest escapee, guards the pipe) |
| 7 | **The Hand** (final, 3 phases) — §12.3 |

Boss structure: 2–3 phases with telegraphed bullet patterns (colorful, readable).
Every boss is a **corrupted** creature, freed (not killed) when it falls, and **guards The Crack** (§5.2) and its patterns use the side view:
floor slams, sinking/rising projectiles, ceiling drops, sweeping from wall to wall.
Boss designs also follow the Descent Curve: Depth 1 bosses are goofy,
Depth 6 is genuinely menacing.

### 12.3 Final boss — The Hand

A giant human hand (comic style, huge, fills the top of the screen), reaching
down from the water surface. Slightly silly details keep the tone:
a cartoon bandage, a smiley sticker, chewed nails. **Fingertips and the
wrist glow red when vulnerable**; Clementine's stings hurt it (*"OUCH!"*).

| Phase | Mood | Attacks |
|---|---|---|
| 1 — **Curious** | Pokes and plays | **Finger poke** (shadow + ripple telegraph, fast jab); **Glass tap** (shockwave rings cross the tank, bubbles and gravel jump); **Fish-food sprinkle** (flakes rain down and sink — eating one heals ½ heart but a grab follows) |
| 2 — **Catch!** | Tries to take her | **Green fish net** sweeps in arcs (caught = mash to escape, lose ½ heart); **Grab** (hand closes on her position, shadow warning); **Gravel vacuum** (siphon tube pulls everything toward it) |
| 3 — **Frustrated** | Throws everything in | Pokes with **pencil, chopstick, toy shark, rubber duck**; drops **ice cubes** (sink, freeze on touch); tips the **plastic castle** over; **slaps the glass** so the whole tank sloshes — a strong gameplay current swings left↔right |

**Arena:** tank interior, gravel floor, ornaments act as cover (and get
destroyed), the filter intake pipe on one side (hazard + suction), LED strip
at the top. The glass walls show the giant room outside; in phase 3 a
huge blurry face leans in.

**Defeat:** Clementine delivers a final big sting — the Hand jerks back,
the net falls in, a panicked voice (*"OW! It stings!"*). Ending panels:
the aquarium is carried to the shore and **tipped back into the sea**;
Clementine, her family and every rescued creature pour out; the pipe is sealed, the
**Leak stops**, and true colour floods back into the reef. Credits over the Tide Pool,
full of octopuses again.

**Why it works:** after six depths of creeping darkness, the finale is a
sudden bright, artificial, absurd-scary scale shift — the player realizes
the "fish tank" framing was literal all along.

## 13. Save System & Meta Progression

### 13.1 Saving
- **Storage:** `localStorage` (JSON, versioned schema with migrations).
  Optional **export/import save** as a text code for backup / transferring.
- **Mid-run save:** "Save & Quit" from pause; on continue, restore at the start
  of the current room (seed + floor state + player state). One suspended run slot.
- **Profile:** 1 profile. **Export/Import** via a base64 save string in Options.

### 13.2 Meta progression
Pure Isaac-style: **achievements → unlocks**. No currency. e.g.:
  - Beat any Depth 1 / Depth 2 boss → unlock next Dive (§5.1).
  - Beat Rusty Admiral → *Admiral's Key* (post-v1: Barnaby the Hermit Crab).
  - Get 3 synergies in one run → *Mitosis* added to item pool.
  - Win without taking damage on a floor → *Golden Scale* hearts can drop.
  - Beat Hollow Maw → Abyss alternate endings / hard mode.
- The **Tide Pool** title screen visibly fills with rescued, re-colored
  bosses as trophies (visual progress, no currency).
- **Collection Log ("Sea-pedia")** — every item/enemy/boss seen, with lore text.
- **Stats:** runs, wins, deaths by cause, best time, favorite item.

## 14. Technical Architecture

| Concern | Choice |
|---|---|
| Language | **TypeScript** |
| Build | **Vite** (dev server + static build) |
| Rendering | **PixiJS v8 (WebGL2)** — batched vector/mesh drawing + custom GLSL filters: bloom, refraction, caustics, god rays, color grade. Tentacles/kelp as dynamic meshes. |
| Water sim | Custom CPU stable-fluids grid (Float32Array), later optionally moved to a GPU shader |
| Audio | WebAudio synth SFX (music post-v1) |
| Physics | Gameplay: custom AABB/circle collision, tile grid (deterministic). Cosmetic: verlet chains, springs, boids, fluid field. |
| Tests | **Vitest** for PRNG, generation determinism, item modifiers, save migration |
| Deploy | Static site → GitHub Pages via Actions workflow |

```
src/
  core/       loop, input, rng, events, time
  gen/        floor layout, room templates, pools, seed
  world/      room, tiles, doors, hazards
  entities/   player, enemies, bosses, projectiles, pickups
  items/      item defs (data), modifiers, synergies, transformations
  render/     draw primitives, comic FX, sprites (procedural), camera, UI, shaders
  ambient/    fluid field, bubbles, particles, boids, softbody (verlet), plants
  audio/      synth, sfx, music
  meta/       save, unlocks, stats, hub
  scenes/     title, hub, run, pause, gameover, codex
```

Performance target: 60 FPS on mid-range laptops with ~300 projectiles, full
water sim, ~600 particles and ~40 ambient fish on the High preset.

## 15. v1 Delivery Plan (after approval)

1. **Skeleton** — Vite + TS, game loop, input, scaling canvas, scene manager, seeded RNG, Vitest, Pages workflow.
2. **Water & render core** — Pixi layers, bloom, caustics, god rays, refraction, fluid field, particles.
3. **Clementine** — soft-body bell, verlet tentacles, pulse propulsion, 4-dir bubbles, neon feedback.
4. **Floor gen** — layout, room templates + procedural rooms, doors, minimap, transitions.
5. **Combat & ambience** — 12 enemies (Depths 1–3), hit/knockback, kelp/fish/decor per biome, pickups, room-clear rewards.
6. **Items** — modifier system, ~28 items, pools, pedestals, shop, actives.
7. **Synergies & transformations** — explicit table + FX.
8. **Bosses** — 6 bosses with comic-cover intros.
9. **Meta** — save/continue, unlocks, progressive dives, Sea-pedia, export code.
10. **Juice** — comic FX, halftones, onomatopoeia, SFX, options, quality presets.
11. **Polish & balance** — seed determinism tests, perf pass, deploy.

## 16. Remaining minor assumptions
- Diagonal shooting off by default (toggle in Options).
- No mobile/touch support.
- Difficulty: Normal only in v1; "Riptide" hard mode post-v1.

## 17. v1 Implementation Notes

**Shipped:** Depths 1–3 with progressive unlocks and the Crack; 12 enemies with
Menace-driven looks/behavior and champion variants (Depth 2+); 6 bosses with comic-cover
intros; 29 items (7 stat, 17 bubble modifiers, 5 actives); 10 synergies; 2
transformations; treasure, shop, boss, secret, curse (spiked door) and Mermaid Grotto
rooms; big rooms (2×1, 1×2); sea snacks with per-seed identities; clams, glow jellies,
foam hearts; seeded runs + special seeds (`HUGEJELL`, `TEENYJEL`, `DARKDEEP`,
`PARTYFSH`); autosave/continue, export/import codes; achievements, Sea-pedia, stats;
title screen shows rescued bosses as restored-color trophies; living water (fluid
field, refraction, caustics, god rays, bubbles, marine snow, current streaks, fish
boids, verlet kelp/chains, wobbling decor); neon bloom and lightmap; synth SFX;
quality presets with auto-downgrade; accessibility toggles; GitHub Pages deploy.

**Changed from the draft:**
- Golden Scale hearts are not in v1; the *Untouchable* achievement unlocks Lucky Sea Glass instead.
- Challenge, Sacrifice and Shipwreck Archive rooms, Tarot Shells, enemy flanking tactics,
  torn panel borders and Daily Dive are deferred.
- Beams (Sunbeam) pass through rocks; Ink Sac explosions do not hurt Clementine.

## 18. Art Direction v2 — "Underwater Camera" (supersedes the comic look)

The game is now presented as documentary footage from an underwater camera:
- **Lens post-process:** barrel distortion, edge chromatic aberration, per-depth color grade
  (reds absorbed first, shadows lifted toward blue), filmic highlight roll-off, vignette, sensor grain.
- **Camera:** gentle handheld/ROV drift, depth-of-field blur on distant reef and fish, murky haze
  that thickens with depth, fades through black between rooms.
- **Cinematic lighting:** surface caustics and volumetric god rays as the key light; Clementine is the
  moving practical light.
- **Clementine is bioluminescent:** translucent gelatinous bell, glowing gonads, luminous rim organs,
  and light pulses that travel down her tentacles with every propulsion stroke; her glow scatters in the water.
- **Materials:** comic ink outlines replaced by faint contact edges; bodies are top-lit with soft gradients;
  textured rock with settled silt, shadowed overhangs, encrusting growth; realistic fish eyes.
- **UI:** camera OSD (REC, timecode, depth in metres, water temperature), viewfinder brackets,
  documentary lower-third captions, letterboxed specimen title cards for bosses, serif/sans typography.
  Comic onomatopoeia removed; only informative floating text remains.


## 19. World Structure v3 — "One Reef per Depth" (supersedes §6.1–6.2 and §7 room flow)

Inspired by Hollow Knight: each depth is a single, large, continuous level instead of a grid of rooms.

- **Macro maze:** a grid of chambers (Depth 1: 5×4, Depth 2: 6×5, Depth 3: 7×5; 24×18 tiles
  each) linked by a randomized depth-first spanning tree plus ~12% extra loops. The start is on
  the top row (Depth 1 opens into a sunlit lagoon under the surface). The **boss arena** is the
  bottom-row chamber farthest from the start, reachable by a single tunnel.
- **Carving:** chambers are noise-perturbed blobs, tunnels are wandering curves; cellular-automata
  smoothing, rock islands, and a connectivity pass keep it organic and fully traversable.
- **Caves (dead ends):** treasure, Barnaby's shop, curse den (item among urchins), secret cave
  (sealed by bombable weak rock), and side pockets with clams.
- **Encounters:** each chamber has a seeded group of creatures placed on floors, walls, ceilings
  or in open water. Groups wake when Clementine approaches; clearing one rolls a reward and
  charges the active item. Far creatures stay dormant.
- **Boss:** entering the arena starts the fight; strong inflowing currents seal every tunnel until
  the boss falls. Then the Crack opens (next depth), the surface bubble ends the run, and the
  Mermaid's Grotto portal may appear (a separate small area).
- **Growth & cover:** dense kelp, grass, coral, fans, anemones, boulders and wall sponges (densest
  at Depth 1); ~28% grow in front of the action and simply cover whatever swims behind.
- **Rendering:** tiles are only the collision skeleton. Rock is drawn as smooth contours
  (marching squares over a noisy half-tile field), textured, darkening toward its core, with silt
  on top faces, shadowed overhangs, crusts, and a faint rim light; chunked and culled to the view.
  Water simulation runs in a window that follows the camera; light falls off with depth.
- **Map:** fog-of-war minimap (top right) and a full map on **Tab**, revealing tiles as they are seen.
- **Saving:** per-area state (cleared encounters, pickups, pedestals, broken rock, explored map,
  position) is autosaved every 30 s and on key events; Continue resumes where you were.

## 20. Clementine v3 — a bioluminescent octopus (supersedes the jellyfish in §11.2/§18)

- **Body:** egg-shaped mantle and head that breathes slowly, chromatophore spots that flicker,
  a raised eye with a golden iris and a horizontal slit pupil that stays level whatever the body
  does, a siphon under the mantle edge, glowing sucker rims (like real bioluminescent octopods).
- **Arms:** eight verlet chains pulled toward a muscular pose and pushed by the water; a web joins
  the arm bases; far arms are drawn behind the body, near arms in front.
- **Hovering:** mantle upright, arms spread like an umbrella, slowly reaching, curling at the tips.
- **Setting off:** one jet — the mantle squeezes, water shoots from the siphon, arms flare then
  sweep together in a power stroke and she darts forward (~1.8× speed, 0.38 s). A sharp turn
  (>110°) takes another jet.
- **Cruising:** smooth continuous movement, mantle first, arms trailing in a loose rippling bundle.
- **Title screen & dive intro** use the same swimming: hover upright, one jet to set off, then a
  smooth mantle-first cruise (the intro is one jet and a head-first dive into the opening).

## 21. Ink shots (replaces bubbles)

- Clementine squirts **ink**: dark wobbling blobs with a smeared tail and a thin ink trail; the
  item's colour shows only as a sheen and a faint luminescence. Hitting a foe or running out of
  range dissolves the blob into an ink cloud.
- Where ink hits rock it leaves a **stain**: a blob flattened along the drawn rock face, spatter
  along the surface and drips running down walls and hanging from ceilings (newest 260 kept per area).

## 22. Destructible reef (Worms-style)

- Rock can be blown away in round craters. A fine 8 px mask on top of the tiles decides collision;
  the renderer subtracts the craters from the rock field so walls show clean round bites with
  silt settling on the new floors.
- **Who digs:** ink bombs (r ≈ 62 px), explosive ink (small), well-charged pearl shots (small) and
  beams (slowly burn a small hole where they meet rock). Plain ink never digs.
- Plants, urchins and ink stains inside a crater are destroyed. The level's outer shell (2 tiles)
  can't be breached. Craters are saved per area.

## 23. Numeric health

- Clementine has **100 HP** shown as a health bar (ticks every 25 HP, a drain trail for recent hits).
- Heart +15 HP, half heart +8 HP, heart container +20 max HP, foam = +15 bonus HP that soaks hits
  first (pale-blue bar extension). Max HP + foam ≤ 300. Siren deals cost 20 max HP per heart shown.
- Damage at Depth 1: jelly 6, squidling 8, blob/splitter 10, crab/cannon crab/urchin 12,
  pufferling/flounder 14, barracuda 16, mimic/moray 18, creature shots 10, urchin beds 8,
  own ink bomb 20, Kelpie's vines 15; bosses 18/22/25 contact and 12/14/16 per shot by depth.
  Creature damage scales ×(1 + 0.75·menace) deeper down; champions hit 25% harder.

## 24. Aggro, safe waters and leashing

- Creatures start hunting Clementine within ~720 px and give up beyond ~1050 px; then they stop
  attacking and drift back to where they spawned (morays retreat into their burrows).
- **Barnaby's shop is safe water:** creatures can't swim into it (they are turned back at its edge),
  their shots fizzle there, and nothing hurts Clementine inside. A "Safe waters" note shows on entry.
- **Boss arena:** creatures from the rest of the reef don't follow Clementine in and can't enter during
  the fight; only the boss and the minions it summons fight inside.

## 25. End of a dive & debug dives

- After a boss falls there is no "back to the surface" bubble any more: only the rewards and the
  **rift**. If the next depth is unlocked the rift leads down; otherwise entering it ends the run
  with the summary screen ("The dive is over") and a single button back to the title screen.
- **Debug dive:** type `DEBUG` as the seed. A normal (random) level, but 999 HP, 99 coins/bombs/keys,
  a snack that never runs out (all snacks identified), all three depths open, and an item picker
  (press **`** or use the pause menu) that grants any item. Debug dives never unlock anything.

## 26. True map, sealed pockets, buried coins

- **Map:** the minimap and Tab map sample the rock exactly as it is drawn (3 samples per tile),
  including every crater blown during the run, under the fog of war.
- **Sealed pockets:** 4–6 per level (more deeper), small round chambers sealed one tile of rock below
  a corridor floor. Visible through the rock but unreachable; one ink bomb (crater r 72 px) on the floor
  above opens them. The first holds a treasure item, the rest clams or a handful of collectibles.
- **Buried coins:** 8–12 per level under faint scratched X marks on reef floors. Blow a hole there and
  2–3 coins (or a 5-coin) pop out. Dug spots are saved.

## 27. Keys removed; map landmarks; debug picker

- **No keys.** Gold clams open like ordinary clams (with a richer haul); keys are gone from drops,
  shops, the HUD and debug stock. The Admiral achievement is now "The Admiral's Chart".
- **Map:** seen rock is drawn as a solid silhouette with lit edges; whole chambers are marked once seen —
  shop (coin pouch), treasure (gem), secret (purple), curse den (red triangle), boss (skull), and the rift
  after the boss falls — instead of individual items. The Tab map shows a legend.
- **Debug picker:** multi-select. Owned items start highlighted; click to pick or unpick (one active at a
  time), then Apply to make Clementine's items exactly that set.

## 28. Depths 4–7, new mobs and items (familiars and trinkets cut)

**Familiars and trinkets are cut from the plan.** Their synergies (Choir, Krill Coil, Royal Guard) are gone.

**Depths.** Each first boss kill unlocks the next dive. The Hollow Maw unlocks The Tank.

| # | Biome | Feature | Mobs | Bosses |
|---|---|---|---|---|
| 4 | Coral Carnival | Bounce pads ("BOING!") | Clown Anemone, Seahorse, Nettle, Stingray | Ringmaster Octo · The Jester Jellies |
| 5 | Twilight Trench | Dark; Clementine's glow carries further | Lanternfish, Ghost Shrimp, Anglerling, Hatchetfish | Mother Angler (dims the arena) · The Siphonophore |
| 6 | The Abyss | Deep currents push along tunnels | Viperfish, Gulper (inhales), Isopod (armored, rolls) | The Hollow Maw; its crack is a pipe grate |
| 7 | The Tank | Glass box in a living room; filter intake pulls and hurts | Toy Diver, Snail (shell blocks shots) | **The Hand** — poke, jab, grab, tap, food, net, drop, slosh |

Mob and boss damage keep scaling with depth. Ghost Shrimp can only be hit while visible.
Beating The Hand plays the true ending. The run ends; no rewards.

**New passives (25).** Moon Jelly Heart, Shark Tooth, Remora Sucker (magnet), Manta Shawl,
Cuttlebone, Giant Squid Eye (crits ×3), Coral Polyp, Brain Coral, Pirate's Doubloon (+15 coins),
Cannonball, Captain's Hook (knockback), Sea Nettle Sting (poison), Stingray Barb (slow),
Lamprey Mouth (kills heal), Hammerhead (5-pellet shotgun), Rear Fin (shoots backwards),
Abyssal Glow, Glowing Moss, Fire Urchin Spine (foes burst on death), Oyster Pearl, Sea Sponge,
Diver's Watch, Dolphin Fin, Brass Compass (landmarks on the map), Lantern Pearl (brighter glow).

**New actives (6).** Tidal Wave, Sea Dice, Kraken Call, Ink Cloud (unseen: foes lose you),
Whale Song (heal 35), Anchor Drop (digs a shaft straight down).

**New tags.** predator → Shark Mode, galleon → Pirate, coral → Coral Reef.

**Boss unlocks.** Ringmaster → Hammerhead · Jesters → Fire Urchin Spine · Mother Angler → Giant Squid Eye ·
Siphonophore → Kraken Call · Hollow Maw → Lamprey Mouth.


## 29. Realistic creatures; item effect captions

- **Creatures are drawn from real anatomy** (`src/render/mobs.ts`, helpers in `fauna.ts`): countershaded
  fish with rayed fins, scales, gill covers and lateral lines; jointed crabs with chelae; long-spined
  urchins; translucent moon jellies, sea nettles and salps; a glass shrimp with visible organs.
  No cartoon faces. Menace shows as darker, drained colors and amber-to-red irises.
- Renamed to match: Blubber Blob → **Spanish Dancer** (nudibranch), Splitter Slime → **Salp Chain**,
  Mimic Clam → **Giant Clam**.
- **Item captions list real effects.** Picking up an item shows its name, tagline, and one line per
  effect with numbers (e.g. "+0.8 damage", "×1.5 damage", "20% chance to freeze foes for 1.6s",
  "Recharges after 3 encounters"). The Sea-pedia and debug picker show the same text.

## 30. Ink dash

- **Shift** (touch: 💨): one hard mantle squeeze shoots Clementine ~3.4× swim speed along the
  held direction, or where her head points when hovering (upright = straight up).
- She coasts 0.24 s, untouchable for 0.32 s; cooldown 0.85 s. No hurt-blink during the dash.
- She leaves an ink cloud where she was: dense puffs that billow out, drift up and dissolve in
  2 s. Creatures inside are slowed.
- Animation: the mantle squeezes 1.5× harder than a swim stroke, the arms flare wide and then whip
  shut into a tight bundle; a water jet, wake bubbles and a soft violet flash mark the burst.

## 31. Three reefs per depth; dive to the Abyss in one run

- **Every depth (1–6) is three reefs** of the same biome, each its own generated level ending in a
  boss fight; HUD reads e.g. "KELP JUNGLE · 2/3", floor titles "DEPTH 2 · REEF 2 OF 3". The first
  two reefs are one macro column smaller; each reef is a little meaner (+0.04 menace).
  The Tank (7) stays a single level with The Hand.
- **Three bosses per biome**, in a seeded order so each reef has a different one. The Abyss always
  ends with the Hollow Maw (story boss). New bosses, all drawn from real animals:

| Depth | New boss | Behaviour |
|---|---|---|
| 1 | Old Gus the Grouper | Cruises; gulps (suction pull) then spits gravel; rams from phase 2 |
| 2 | Mama Otter | Floats on its back lobbing cracked urchins that burst into spines; dives at you |
| 3 | Captain Sawtooth (sawfish) | Telegraphed charges across the wreck; wall slams rain debris |
| 4 | Punchy the Mantis Shrimp | Hops; cavitation punches (white ring + bubble fan) |
| 5 | The Giant Squid | Tentacle lashes (circle, row, column), ink blackouts |
| 6 | The Frilled Shark | Eel-like body, S-curve approach, lunging bites, sheds teeth |
| 6 | The Sea Spider | Eight stilt legs stab where you are and where you're going |

- **Progression:** a run can go all the way to the Abyss (depth 6) — no more one-depth-per-run
  unlocks. The `dive2`–`dive6` achievements now mark clearing a biome's three reefs.
- **The Tank:** the first Hollow Maw kill shows a rusty grate that holds; the **second** (counted
  across runs, seeded dives excluded) breaks it: `tank` is unlocked, the current run's rift becomes the
  pipe (a short cutscene) and every later run can reach depth 7.
- Fix: a world only saves into its own floor's state (depth + reef), so a new reef never inherits the
  previous one's pickups or map.

