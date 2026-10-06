# Ink Deep — Game Design Document, 3D (v0.1, draft)

> A first-person roguelite shooter set in a stylized 3D reef. You are
> **Clementine**, the last octopus of a reef corrupted by the Leak (DESIGN-2D §1). You swim through procedural
> reefs, free corrupted sea life with your bubbles and dive after your family toward the bottom of the ocean.

This document covers **only what changes** from the 2D design
([`DESIGN-2D.md`](DESIGN-2D.md)). Every rule not mentioned here is inherited
unchanged: lore, core loop, items, synergies, transformations, seeds, Menace,
numeric health, pickups, economy, special rooms, meta progression, saves.

---

## 0. Decisions

| Topic | Decision |
|---|---|
| Platform | **Desktop** (Windows, Linux, macOS). Steam-ready exports. |
| Engine | **Godot 4** (Forward+ renderer), **C#** (.NET 8). |
| Camera | **First person**, toggle to **third person** (§3). |
| Movement | Free swimming, **level horizon** — yaw + pitch, no roll (§2). |
| Aiming | Mouse aim, **aim assist** with a strength option (§4.2). |
| Art | **Stylized**: toon-shaded, rim-lit, bold silhouettes, neon glow (§8). |
| World | **Open sea** basins with reef caves; vision limited by mist; circular **minimap** (§6, §9). |
| Items | **Pearls** found in shells, carried on the suckers of one tentacle (§6.6). |
| Input | Keyboard + mouse. Gamepad post-v1 (aim assist already supports it). |
| Determinism | Gameplay sim is engine-independent and seeded (§10). |

## 1. Scope of change

| Kept as-is | Redesigned |
|---|---|
| Items, modifiers, triggers, synergies, transformations | Camera, controls, aiming |
| Seeded RNG streams, Menace curve | Level generation (open sea, reef caves, SDF terrain) |
| HP, pickups, shops, Sea Snacks | Destructible reef (3D craters), item pearls and treasure chests |
| Bosses roster and story, The Tank | Enemy navigation and boss patterns |
| Achievements, unlocks, Sea-pedia, saves | Minimap, vision mist, lighting, water, all art |

## 2. Movement — level horizon

- **Mouse** turns (yaw) and looks up/down (pitch, clamped ±85°). **No roll** ever.
- **W/S** swim along the view direction (pitch included). **A/D** strafe on the horizontal plane.
- **Space** rises, **Ctrl** descends (world-vertical, independent of pitch).
- **Slow sink** (inherited): no input → drifts down at 0.15 m/s, eased in over 0.5 s; settles on the floor.
- **Jet start** (inherited §20): the first stroke from rest is a 0.38 s burst at ~1.8× speed. Sharp turns (>110°) trigger another.
- **Ink dash** (inherited §30): **Shift**, ~3.4× speed along the movement direction (or view direction if idle), 0.32 s invulnerable, 0.85 s cooldown, leaves a slowing ink cloud.
- Base swim speed **4.5 m/s**; item `speed` scales it. Inertia: 0.08 s to full speed, 0.12 s to stop; releasing the keys ends a jet burst at once. Responsive first.
- Collision: Clementine is a **sphere, r = 0.35 m**, slides along terrain.

## 3. Camera

### 3.1 First person (default)
- FOV **90°** (option 70–110). **No head bob**: the camera stays steady; strokes show only in the arms.
- **One tentacle in view — the throwing arm.** It reaches in from the bottom right in a relaxed curve, its tip loosely toward the crosshair, rolled so its suckers face into the screen. It floats: a slow drift, a tip that curls and uncurls, and water drag that makes it trail against her movement and turns. Like every one of her arms it has **8 pairs of suckers** in two staggered rows, each a pale raised ring around a rosy cup, shrinking toward the tip. Bubbles (§4.1) sit in the suckers, spread along the arm: you can see how many are left and the next one swelling back.
- **The skin**: soft and mostly matte, not glossy: a coral-red back dotted with dark chromatophores and pale flecks, a blotchy peach underside with creases and faint freckles, an opal sheen where the two meet, only a faint broken wet sheen, a paler translucent tip; it flushes dark red when she is hurt.
  - Idle: slow sway. Swimming: a ripple runs down the arm. Throw: the tip lunges toward the crosshair. Dash: the arm whips back.
  - **Items change the arm**: glow color, spines (Fire Urchin Spine), crusts (Barnacle Armor), etc.
- The **inventory tentacle** (item pearls, §6.6) is out of view; it rises along the left only while reviewing (Tab).
- Her glow is a **point light** at the camera — the main light in dark depths.
- Hurt: magenta screen-edge pulse, the arm jolts, short chromatic wobble.

### 3.2 Third person (toggle: **V**)
- Spring-arm camera 3.5 m behind and 0.6 m above, pulls in on terrain collision.
- Shows the full octopus body (mantle, eye, eight verlet arms, item cosmetics).
- Aiming always comes from the **screen-center reticle**; bubbles leave her throwing arm toward the reticle hit point.
- Gameplay is identical in both modes.

### 3.3 Always third person
Title screen, Tide Pool hub, cutscenes, boss intros, death cam.

## 4. Shooting

### 4.1 Bubbles
- Clementine **throws bubbles** from the tip of her throwing tentacle (no ink shots). **LMB** throws (hold for auto-throw at the fire-rate stat). Charge items (Pearl Diver, Sunbeam): hold LMB to charge.
- **Bubbles are ammunition**: each sucker of the throwing tentacle holds one. **8 bubbles** to start; every throw takes them from the tip end. **0.6 s** after the last throw they **regrow** from the base end, one every **0.35 s**. With none left, LMB only clicks.
- Item stats change this: **bubble capacity** (suckers, 2–12), **bubble regrowth** speed, **bubbles per throw** (thrown at once in a slight fan, each costing a bubble). Charged pearls and Sunbeam beams cost one bubble each.

| Item | Effect |
|---|---|
| Bubble Gland | +3 bubbles on the tentacle |
| Anemone Pump | ×1.6 bubble regrowth speed |
| Twin Siphon | +1 bubbles per throw, ×0.85 shot damage |

- Projectiles are **visible, slow, physical** — never hitscan. Keeps the Isaac feel. A bubble is clear with an iridescent rim and pops on whatever it hits (no stains).
- Stat mapping (1 tile in 2D = **1.5 m**):

| Stat | Base | 3D meaning |
|---|---|---|
| Damage | 3.5 | Unchanged |
| Fire rate | 2.7 /s | Unchanged |
| Shot speed | 1.0 | ×14 m/s |
| Range | 6.5 | ×1.5 m → 9.75 m, then the bubble pops |

- Shots inherit 30% of Clementine's velocity (inherited rule).
- Bubble hit radius **0.25 m** (generous on purpose).

### 4.2 Aim assist
Option: **Off / Low / Medium (default) / High**.

| Setting | Magnetism cone | Bullet bend | Sensitivity slow on target |
|---|---|---|---|
| Off | — | — | — |
| Low | 3° | 2°/s | 15% |
| Medium | 5° | 6°/s | 25% |
| High | 6.5° | 8°/s | 30% |

- **Magnetism**: on fire, the shot direction snaps toward the nearest visible enemy inside the cone.
- **Bullet bend**: projectiles curve slightly toward targets in the cone (cosmetic-safe, deterministic).
- **Slowdown**: mouse sensitivity drops while the reticle is over an enemy.
- Homing items stack on top of assist; they don't replace it.
- Seeded runs record the assist setting; it doesn't affect unlocks.

### 4.3 Shot modifiers in 3D

| Modifier | 3D behavior |
|---|---|
| Spiral (Nautilus) | Helix around the flight axis |
| Wave (Double Helix) | Two shots in counter-phase helices |
| Bounce (Mirror Scale) | Reflects off the terrain SDF normal |
| Split (Mitosis) | Splits into 2 at ±25° in the plane facing the camera |
| Multishot / Triple | Horizontal fan; Hammerhead = cone |
| Boomerang | Returns to Clementine's current position |
| Orbit | Ring around Clementine in the horizontal plane |
| Laser (Sunbeam) | Beam from the siphon to the reticle point |
| Rear Fin | Fires opposite the view direction |
| Kraken Form (8-way) | 6 axis directions + 2 diagonals of the view |

Other modifiers (homing, pierce, spectral, freeze, burn, poison, charm, chain, explosive, grow) work unchanged.

### 4.4 Active items
- One active item at a time, used with **F**. It **recharges over time**: each item lists its own recharge time (for example Glow Burst 10 s, Bubble Shield 20 s, Conch Horn 30 s, Kraken Call 60 s); there is nothing to clear or kill. A new active item comes charged.
- The HUD shows a bar filling toward READY with the seconds left.
- **Active recharge speed** is a stat: Brain Coral gives ×1.35. A **glow jelly** pickup adds 30% of the recharge time.

## 5. Controls

| Action | Key |
|---|---|
| Look | Mouse |
| Swim / strafe | W A S D |
| Rise / descend | Space / Ctrl |
| Throw bubbles / charge | LMB |
| Ink dash | Shift |
| Active item | F |
| Ink Bomb | E |
| Consumable | Q |
| Camera toggle | V |
| Review pearls (hold) | Tab |
| Next pearl while reviewing | Mouse wheel |
| Pause | Esc |
| Restart (hold) | R |

All rebindable.

## 6. World — open sea reefs

### 6.1 Macro structure
- Each reef is a big **open-sea basin**: open water down to a rolling seabed, a sunlit surface overhead, ringed by cliffs that rise out of the water. Exploring is mostly swimming in the open; caves sit inside reef formations.
- Size: **224 × 224 m** at Depth 1 (reef 3), **+32 m per depth**; the first two reefs of a depth are 32 m smaller (inherited §31). The water column is 64 m.
- **Seabed**: a 1 m heightfield — shallow (~18 m above the bottom) at the start, deepening to ~10 m toward the boss reef, rolling dunes, a trench before the boss reef, cliffs around the rim.
- **Start** in a sunlit corner, ~10 m above the seabed. **Boss reef** in the opposite corner: a giant formation holding the spherical arena (r 17.5 m) with the Crack, reached by **one tunnel**. The dive goes from shallow to deep.
- **Reef formations**: blended rock ellipsoids rising from the seabed. Cave formations each hold one cave (treasure, shop, curse den, secret cave, 2–3 ordinary caves); 5 + depth plain formations, 3–5 rock arches and 6–10 pillars stand as landmarks in open water.
- **The ground** is shaped like a real Caribbean fore-reef: **spur-and-groove** ridges and sand channels running down the slope, low sand waves, scattered **boulders** (45 + 8 per depth) and knobbly **patch reefs** ("bommies", 10 + 2 per depth, 3–6 m tall), and formations weathered into limestone **ledges** and pockmarks.
- **Painted** by `reef_terrain.gdshader`: sand settles on low, flat ground near the seabed (rippled, with shell specks and paler and darker patches); everything else is old reef limestone, crusted pink with coralline algae, furred with green-brown turf on top in the shallows, dark and spotted with orange and purple encrusting sponges under overhangs. Sunlight **caustics** play over upward faces and fade with depth; deeper water turns everything darker and bluer.
- **Flora** (`FloraPlanner`, decoration only, seeded per reef, about 50 000 plants and animals) follows real reef zonation. The game's 60 m water column stands for about 27 m of real reef, and zonation uses that real depth:
  - **Turtle grass** (*Thalassia*) meadows on the sandy flats, with **Halimeda** algae, sea rods and small brain corals between them.
  - Sunlit rock in the shallows (top ~14 m): **elkhorn** and **staghorn** coral, **brain corals**, sea rods.
  - Down the slope: fewer corals, more **sea rods**, **tube sponges** (yellow, purple, lavender, orange) and **giant barrel sponges**; below that, mostly sponges.
  - Walls: **sea fans** (*Gorgonia*) turned broadside to the prevailing current, and tube sponges. Caves: only sponges.
  - Turtle grass, sea fans and sea rods sway in the surge. Each species is drawn as one MultiMesh per 32 m tile and fades out beyond the mist, so only the nearby reef costs anything.
- **Sea anemones** decorate the seabed: 55 + 8 per depth, rooted on the seabed (found by casting a ray onto the baked terrain, on gentle slopes with open water above), in eight colours, swaying in the current and glowing at the tips. 6 + depth of them (at most 12) are **nests** (§7.5): larger, violet-pink and a little brighter, at least 50 m from the start.
- Three reefs per depth, the Tank stays single (inherited §31). Everything is drawn from the run seed's `layout/depth/reef` stream; a layout that fails validation retries with the next seeded attempt.

### 6.2 Terrain
- Terrain is a **signed distance field** at 0.5 m resolution, clamped to ±4 m and stored in 32³ chunks; a chunk that is all open water or all deep rock keeps a single value, so a big basin costs memory only where there is a surface. Baked chunk by chunk on worker threads; each shape is evaluated only near its bounding box.
- Carving: caves = ellipsoids with a flat floor, tunnels = Catmull-Rom tube splines (r 1.8–2.1 m) from the cave out to a mouth above the seabed, plus overhangs, arches and pillars; rock surfaces get noise.
- Mesh: **surface nets** per 32³ chunk on worker threads (smooth faces for toon shading, seamless between chunks); triplanar stylized materials.
- Validation: a coarse flood fill (2 m lattice) from the start must reach every cave mouth; a local flood fill (0.5 m, clearance 0.45 m) from each mouth must reach its cave — and must **not** reach the secret cave; the boss arena must drop out of reach when its one tunnel is blocked; no sealed pocket may be reachable.
- Nothing swims above the sea surface; shots that break it dissolve.

### 6.3 Destructible reef
- Explosions subtract **spheres** from the SDF (ink bomb r = 2.5 m, explosive ink r = 0.6 m, charged pearl r = 0.8 m; beams burn r = 0.4 m every 0.1 s where they meet rock). Plain ink never digs.
- **Ink bombs** (E): dropped ahead, they sink in a slow arc, roll down slopes and blow after 1.5 s: 30 damage to creatures within 3 m, and 20 to Clementine if she is that close (inherited §23). Runs start with 3.
- Only affected chunks remesh. The outer 2 m shell (sides and bottom) is indestructible.
- Craters are stored per reef and saved with the run (inherited §22).
- **Sealed pockets** (4–6 per reef, more deeper): round hollows (r 1.6 m) under the open seabed behind 1.4 m of rock; one ink bomb on the seabed above opens them. The first holds a pearl shell (treasure pool), the rest a chest or a pile of coins.
- **Buried coins** (8–12 per reef): 2–3 coins, or a 5-coin, 0.4 m under the seabed; a crater on top digs them out.

### 6.4 Special caves
Treasure, shop, curse den, secret cave, Mermaid's Grotto — inherited, now as caves in reef formations. The **secret cave**'s tunnel is plugged by a 1.6 m slab of weak rock (one ink bomb opens it). Each cave has a **landmark light** inside and a beacon at its mouth so it reads from the open sea: treasure gold, shop green, curse red, secret violet (no beacon); the Crack glows orange.

### 6.5 Minimap
- A **circular top-down minimap** (top right) shows the area around Clementine (32 m, option), turning with her so forward is up.
- Nearby **creatures** are red dots (yellow while telegraphing; a clownfish ninja disguised in its school stays off the map until it attacks), faded with height difference and ticked when well above or below; **pearl shells** show in their pearl's colour, **closed chests** as brown squares, **pickups** as small gold dots, **cave mouths** as diamonds in their landmark colour.
- No full-level map: vision and the minimap are local on purpose (§9 mist).

### 6.6 Items and loot
- **Items are pearls.** Each item has a hand-picked **2–3 colour gradient** laid out in one of eight patterns (bands, swirl, spots, marble, rings, stripes, speckle, halo) and glows softly from within (`data/items.json`, `pearl`).
- **Pearl shells**: a pearl rests inside a **giant clam**, 1.7 m across, modelled on Tridacna: heavy ribs fanning from the hinge, fluted valves whose zigzag lips interlock when shut, mother-of-pearl inside and a glowing electric-blue, turquoise and violet mantle with bright spots along the lip. Shut, the pearl is hidden; the clam breathes, its lid lifting a hair now and then so the pearl's light seeps out of the seam, and a pool of light and a few sparkles give it away from afar. Open, the mantle breathes, the pearl rises out of its bed and a shaft of its light rises above the clam. Each clam opens toward the most open water around it. Shop clams are gilded. The clam **opens when Clementine comes within 4.5 m** (closes past 6.5 m), and the nearest open shell in front of her shows a **floating description**: name, tagline, one line per effect (2D §29) and, in the shop, the price. She takes the pearl by touching it. Shop pearls cost **15 sand dollars** (20 for quality 3–4).
- Where shells are: treasure, curse den and secret caves (one each, from their pools), the shop (three for sale), the first sealed pocket.
- **The inventory tentacle**: Clementine holds each pearl on a sucker of a second tentacle, out of view, in pickup order. It has **8 pairs of suckers**, one pearl per sucker from the base up; pearls beyond 16 are held but not drawn (for now). Hold **Tab** to review: the tentacle rises vertically along the left of the screen with the suckers turned toward the camera, each pearl labelled; the **mouse wheel** steps through them and the selected pearl's description shows beside it. She can't shoot while reviewing.
- **Treasure chests** (wooden, 6 + depth on the seabed, one per ordinary cave, some in sealed pockets) burst open when **hit by a shot or a blast**, spilling 2–5 sand dollars plus one or two of: heart, half heart, ink bomb, foam heart, glow jelly.
- **Loose pickups** (12–18 per reef) lie on the seabed, mostly sand dollars.
- **Creature drops**: a defeated creature may leave one pickup where it popped (35% sand dollar, then half heart, heart, ink bomb, foam heart, glow jelly); luck tips the odds, Pirate adds coins.
- Pickups sink and rest on the floor (foam floats up) and are collected on touch; hearts wait while HP is full, glow jellies while the active item is ready. Remora Sucker pulls them in from 6 m.
- Taken shells, opened chests, collected pickups and dug coins are saved with the run.

## 7. Enemies & bosses in 3D

### 7.1 Movement classes
| Class | 3D behavior |
|---|---|
| Swimmers | Free 3D flight on a voxel **flow field** toward Clementine |
| Walkers | Crawl on floors and slopes (surface navmesh) |
| Clingers | Stick to any surface — floor, wall or ceiling |
| Burrowers | Hide in sand or holes, burst out |

### 7.2 Fairness in first person
- Every attack has a **3D positional sound** telegraph.
- **Danger markers** at the screen edge for attacks from outside the view.
- Telegraph minimum is **0.45 s** (2D: 0.35 s).
- Max **3 enemies** attack from outside the view at once; others wait.
- Enemy projectiles glow in their own color and leave short trails.

### 7.3 Menace
All parameters inherited (§5.3 of the 2D doc). Visual menace: darker bodies, glowing slit eyes, sharper silhouettes, stronger rim light.

### 7.4 Bosses
- Arenas are **spherical caverns** (35 m across, §6.1) with the Crack, a glowing groove, across the floor.
- Bullet patterns become **rings, spheres and sweeping planes**, readable from inside.
- Examples:
  - **Old Gus**: suction pulls you toward his mouth; gravel spit in a cone.
  - **Giant Squid**: tentacles sweep through the arena as planes; ink blackouts cut the light.
  - **Hollow Maw**: drinks the light; arena darkens in phases.
  - **The Hand**: you look **up** through the water surface. A giant finger descends. Glass taps shake the camera. The net sweeps as a curved wall.

### 7.5 Clownfish ninjas and their anemones

The first real mob. A **clownfish ninja** looks like an ordinary clownfish except for a **dark headband with a red knot and two trailing ribbons**. It lives in an anemone nest, hides in a school of **3–5 normal clownfish** and ambushes with tiny starfish.

**Normal clownfish** are neutral: they never attack, **cannot be harmed** (bubbles and blasts pass straight through them) and wear no headband. They circle their anemone in a loose school, and scatter from Clementine (within 3 m) and from her bubbles.

**The ninja** (14 HP, hit radius 0.32 m):

| State | Behaviour |
|---|---|
| **Nested** | Asleep inside the anemone: invisible, can't be targeted. Wakes when Clementine comes within **11 m** (unless she is hidden by an Ink Cloud). |
| **Idle (disguised)** | Swims out and takes a place in the school, moving exactly like the normal fish. Stays off the minimap. Goes back to sleep if she gets farther than **45 m**. |
| **Telegraph** | When she is within **15 m**, in line of sight and its cooldown is ready: it stops and winds up for **0.5 s** (never below the 0.45 s fairness minimum, §7.2). The headband glows red and pulses, a starfish grows at its mouth, a rising tone plays from its position, and a danger marker shows at the screen edge if it is out of view. |
| **Throw** | Throws tiny starfish (**9 m/s**, radius 0.15 m, **8 damage**, range 20 m) aimed with half a lead so a dash or a sidestep dodges them: 1 at Depth 1, 2 at Depth 3, 3 at Depth 5 (a 12° fan). Stars stick in rock. |
| **Recover** | Darts back into the school for 0.7 s, then Idle. Next attack after **3–4.2 s** (the first after emerging: 1.5–2.5 s). |
| **Dead** | Drops loot like any creature (§6.6). The nest sends a new ninja after **60 s**, but only while Clementine is more than 45 m away. |

- Ninjas count toward the limit of 3 creatures attacking from outside the view (§7.2).
- The other Depth 1 creatures, how far each notices Clementine and the three Depth 1 bosses are in [DEPTH1-BESTIARY.md](DEPTH1-BESTIARY.md).
- They are hit by every shot modifier and status (freeze, burn, poison, charm, slow, stun) like other creatures.
- Telegraph, fire rate and damage are in the F1 panel (group "Ninja"); the grey-box cave has one nest ("F1 → Nearest nest" teleports to the closest).

### 7.6 Noticing

Creatures are not blinded by the vision mist (§9): each type has its own **notice range**, set by what it senses with, and some notice Clementine from beyond the mist wall. A creature that notices her startles for 0.6 s with a call and shows on the minimap rim if it is beyond the minimap range. Ranges per creature are in [`DEPTH1-BESTIARY.md`](DEPTH1-BESTIARY.md) §2 and live in `data/creatures.json`.

- **Senses.** Sight needs a clear line of sight; water senses (the Spanish Dancer, the jellies) do not; the crabby feels the seabed, so it notices from farther when she is within 6 m of a surface.
- **Sneaking.** Ranges halve while she is barely moving (under 0.6 m/s) and double for 2 s after she shoots, dashes or detonates a bomb. An Ink Cloud hides her completely.
- **Startle and leash.** Noticing starts a 0.6 s startle (a "!" over the creature, a call heard from its direction even beyond the mist), then it acts. Beyond its give-up range, or when she is hidden, it loses her and goes back to its den. Creatures hunting her beyond the minimap range show as arrows on its rim.
- **Paths.** Swimmers that have no line of sight follow a flow field around the reef (`FlowField`, a 2.5 m lattice flooded outward from her, rebuilt twice a second and after craters).
- **Dens.** Each den holds creatures asleep; those more than 80 m from her are not simulated. Dens are placed by the reef generator by zone (the shallow corner has only jellies, urchins and dancers) and are seeded like the rest of the layout. Slain den creatures stay dead until she dies; the ninja respawns as in §7.5.

## 8. Art direction — stylized

- **Sunlight under water** (`SunLight`, `sunlight.gdshaderinc`): one sun direction (steep, a little slanted) drives everything, so a shaft of light in the water always ends in the bright spot it makes below.
  - **God rays**: the full-screen water pass marches each view ray through the water (14 steps over 48 m, jittered every frame) and gathers light where it crosses a sunbeam. The beams come in every width (broad soft shafts, brighter ones inside them, a few thin needles), drift and breathe with the swell, fade with depth and into the mist, and glow brightest looking toward the sun.
  - **Caustics and light spots**: rippling caustic nets and soft dappled patches play over every surface turned to the sun: rock, sand, flora and creatures. They are sharp near the surface and softer and fainter deeper down.
  - **Shade**: a sun shadow map (for every square metre of the surface, the height where its sunbeam first meets rock, rebuilt in the background after craters) keeps shafts and caustics out of the shade of overhangs, arches and caves. The sun's directional light also casts real soft shadows. The reef's outer rim casts none, since it is the edge of the world, not scenery.
  - **The water's colour** depends on where you look: bright turquoise toward the surface, a glow toward the sun, deep blue below; the deeper Clementine swims, the darker it gets.
  - **Marine snow** is faint in the shade and glitters as it drifts through a beam.
  - Option (F1, group Light): sunlight strength (0 turns shafts and caustics off).
- **Keeping the GPU cool**: the frame rate is capped at 60 (option View → MaxFps, 0 = uncapped) and drops to 15 while the window is in the background; vsync caps it further on slower screens. Measured on an RTX 3090 at a busy spot: about 30 W at the cap against about 160 W uncapped. Other savings: FXAA instead of MSAA, sun shadows in two cascades out to 45 m, flora and creatures lit by sunlight per vertex, cheaper rock noise, flora fading out by 45–60 m (inside the mist).


- **Toon shading**: 3-band diffuse ramp, strong rim light, soft fog.
- **Outlines**: thin dark post-process edges on gameplay objects only (echo of the comic look). Ambient stays soft.
- **Readable silhouettes**: enemies have simple, bold shapes and saturated accents.
- **Palette per depth** (inherited §5): bright turquoise shallows → neon-on-black Abyss → plastic LED Tank.
- **Neon glow**: bloom on shots, hits, pickups, synergies (inherited §11.3 event language).
- **Clementine** stays the warm orange light in every scene. Never scary.

### 8.1 Asset strategy
| Asset | Approach |
|---|---|
| Terrain | Procedural (SDF + triplanar) |
| Coral, kelp, grass, anemones | Procedural meshes (L-systems, verlet chains), instanced |
| Fish, eels, salps, jellies | Procedural lofted / tube meshes + shader animation |
| Clementine | Hand-modeled mantle + procedural verlet arm meshes |
| Crabs, shrimp, squid, bosses | Hand-modeled, rigged, stylized low-poly |
| The Tank props | Hand-modeled, deliberately plastic-looking |

## 9. Water, light & atmosphere

- **Vision mist**: past ~14 m the view blurs and fades into the water colour, gone by ~40 m (options). A soft fog of war: the open sea is found by swimming, not seen from afar.
- **Sea surface** overhead: bright, rippling sunlight seen from below, fading into the mist.
- **God rays + volumetric fog** (Godot built-in), density rises with Menace.
- **Caustics**: projected light texture on terrain, fades with depth.
- **Bubbles and creature shots are point lights** (pooled, max 32 active; rest fake it with emissive only).
- **Twilight Trench**: ambient near zero, only glows light the world.
- **Particles** (GPU): marine snow, bubbles rising and pooling under overhangs, sand puffs, ink clouds.
- **Water field**: low-res 3D velocity grid (24³) following the camera; impulses from strokes, shots, explosions. Drives particles, kelp and fish. **Cosmetic only.**
- **Fish schools**: 3D boids, flee Clementine and explosions.
- **Scorch marks** where creature shots hit rock (bubbles leave none): decals, newest 80 kept.
- Post: bloom, color grade per depth, vignette, light grain. Options for each.

## 10. Technical architecture

| Concern | Choice |
|---|---|
| Engine | Godot 4.x, Forward+ |
| Language | C# (.NET 8) |
| Sim | **`Core` library**: pure C#, no Godot types. Fixed 60 Hz tick. Seeded RNG streams. |
| Gameplay collision | Custom sphere-vs-SDF and sphere-vs-sphere in `Core` (deterministic) |
| Cosmetic physics | Godot/Jolt for debris; verlet arms, kelp, boids in `Game` |
| Terrain | Sparse chunked SDF (uniform chunks stored as one value), surface nets on worker threads |
| Navigation | Voxel flow fields (swimmers), surface navmesh (walkers) |
| Data | Items, enemies, bosses, pools as **JSON** — ported from the 2D game |
| Audio | Synth-style SFX + 3D positional audio (Godot AudioStreamPlayer3D) |
| Saves | JSON in user dir, versioned, export/import codes (inherited) |
| Tests | xUnit on `Core`: RNG, generation determinism, modifiers, saves |
| CI | GitHub Actions: tests + headless export for 3 desktop platforms |

```
project.godot
src/
  Core/          sim: rng, stats, items, modifiers, entities, combat, gen graph, saves
  Core.Tests/    xUnit
  Game/          Godot layer
    Player/      controller, cameras, viewmodel, aim assist
    Terrain/     sdf, chunks, meshing, craters
    Enemies/     visuals, animation, nav bridges
    Fx/          bubbles, ink clouds, glow, particles, water field, boids, plants
    UI/          hud, minimap, menus, sea-pedia
    Scenes/      title, hub, run, pause, gameover
data/            items.json, enemies.json, bosses.json, pools.json
assets/          models, shaders, materials, audio
```

Performance target: **60 FPS at 1080p** on a mid-range GPU (GTX 1660 / RX 5600 class) with 200 projectiles, 40 enemies, full particles. Quality presets Low/Medium/High/Auto (inherited).

## 11. Options & comfort

- FOV slider, camera shake off, motion blur off (default), mist distance, minimap range.
- Aim assist level, mouse sensitivity, invert Y.
- Reduced flash, reduced ambient motion, colorblind-safe shot outlines (inherited).
- Center dot always on (reduces motion sickness).

## 12. Delivery plan

1. **Feel prototype** — grey-box cave, swim, sink, jet, dash, shoot, one enemy, aim assist. **Go/no-go gate.**
2. **Core port** — RNG, stats, items, modifiers, saves in `Core` with tests.
3. **3D reefs** — open-sea basins, reef caves, SDF carving, meshing, validation, craters; pearl shells, inventory tentacle, chests, pickups, drops; minimap, vision mist.
4. **Enemies** — four movement classes, flow fields, telegraphs, danger markers.
5. **Depth 1 vertical slice** — 3 reefs, 3 bosses, ~15 items, pickups, shop, third-person toggle.
6. **Look** — toon shading, outlines, fog, god rays, caustics, particles, boids, plants.
7. **Meta** — hub, unlocks, Sea-pedia, saves.
8. **Depths 2–6**, then **The Tank** and The Hand.
9. **Polish** — comfort options, presets, balance, desktop exports.

## 13. Open questions

- Gamepad support timing (post-v1 proposed).
- Hub: free-swim 3D Tide Pool or a menu scene?
- Steam release vs. itch.io first?
