# Reef Fish: clownfish, blue tang and small schools

> **Built** (the decisions are in §8). The rules now live in DESIGN-TOPDOWN §12.2 and DEPTH1-BESTIARY §8.3; this
> page keeps the design and its reasoning. This page covers the background sea life of the reef. Clownfish live in
> anemones, blue tangs wander the reef, and small schools of fish sweep through the open water. They are healthy
> neighbours that mind their own business, like the healthy pufferlings. None of them is a target, none of them is a
> threat, and they change nothing in play. They are built to Clementine's standard, each with its own baked mesh,
> one shared shader and smooth animation. The numbers are first guesses for play.

## 1. Why they belong (Theme Bible)

- **Depth 1 is "healthy neighbours everywhere"** (Theme Bible §7). The game teaches you to love the reef here. Right
  now the only fish are the pufferlings, so the reef feels emptier than the bible asks for.
- **Silence is a resource** (§10). Later depths lose these fish one by one, so the reef growing quieter becomes
  something you can see (§5).
- **Names are species-true** (§11). The film's fish is a **Blue Tang** (*Paracanthurus hepatus*), and the game calls
  it that. No film names, looks or references: the tang is drawn from the real animal.
- **Readability** (§9). Healthy fish wear their natural colours, never tank colours. Anything in neon pet-shop
  colours stays a corrupted creature.

## 2. The three kinds

### 2.1 Clownfish (*Amphiprion ocellaris*), home-bound

- **Look:**
  - about 0.5 m long, a rounded body;
  - warm orange with three white bars, each edged in thin black;
  - fins rimmed in black;
  - a big dark eye with a catch-light.
- **Lives in an anemone:** a pair or a trio to each host. Each fish stays within about 1.5 m of its host, darts out
  in short bursts, hovers, and wriggles back to nestle among the tentacles.
- **Swimming:** a quick, waggling paddle. The pectoral fins do most of the work, and the tail beats only on darts.
- **The host anemone** (`ReefMeshes.Anemone`): on the seabed at the foot of the rock near the plazas, dens and
  channels, 12 m or more apart, its tentacles swaying in the water.

### 2.2 Blue Tang (*Paracanthurus hepatus*), wanderer

- **Look:**
  - about 0.7 m long, an oval body flattened sideways;
  - royal blue with the black "palette" mark sweeping from the eye to the tail;
  - a lemon-yellow tail and the yellow tips of its pectoral fins.
- **Lives:** alone or in pairs. It wanders the reef's edges and the channels, roaming farther than a pufferling: the
  next spot is 6–14 m away, within about 25 m of home.
- **Swimming:** it rows with its pectoral fins in an easy, gliding rhythm, and its tail beats only to turn or speed
  up. It pauses now and then to "graze" with its nose to the rock.

### 2.3 Small schools: Blue-Green Chromis (*Chromis viridis*)

- **Look:**
  - about 0.34 m each;
  - pale turquoise-green with a faint sheen;
  - a forked tail.
  - They suit the Sunlit Shallows palette.
- **Lives:** in few, big schools of 45–90 (big enough to see them split and join) over open sand and the reef's flats. Each school has a slowly drifting centre that
  moves along the main channels.
- **Swimming:** boids. Each fish keeps apart from its neighbours, lines up with them, and stays near the school. All
  of them turn together, which gives the school its glitter as the light catches their flanks.
- **A guide:** on a level with a secret room she has not found, a school she swims into holds still and gathers into
  an arrow pointing at the secret's way in (DESIGN-TOPDOWN §12.2).

## 3. How they behave

- **Mind their own business.** They wander, rest and graze. They are not drawn to Clementine and do not follow her.
- **Make room for her.** Within about 1.5 m they ease aside: a school parts around her and closes again behind
  her, a tang veers off, a clownfish ducks into its anemone. Her ink dash startles them farther: a quick dart, then
  they settle.
- **Never in the way.** Her bubbles pass through them, and they never block her, mobs, shots or pickups. They stay
  off the minimap. They never cross rock: they swim only where the level is open water, between the swim plane and
  the seabed below.
- **Depth.** They swim at slightly different heights in the water: clownfish low at their hosts, schools at middle
  height, tangs a little higher. That gives the top-down view some depth under the sunlight and caustics.
- **Surges.** A current surge carries them like everything else not attached (§4.7). The schools hold their shape
  while drifting.

## 4. How they look on screen (Clementine's standard)

- **Mesh:** each kind has its own baked body (a CreatureMeshes-style builder):
  - a proper side profile and cross-section;
  - separate fins with translucency at the edges;
  - an eye with a pupil and a catch-light.
- **Shader:** each kind has its own shader (or one fish shader with per-kind uniforms):
  - pattern bands painted procedurally (bars, the tang's palette mark), so they stay sharp up close;
  - soft subsurface warmth;
  - a rim of Clementine's glow when they are near her;
  - the reef's caustics playing over their backs.
- **Animation**, driven by the vertex shader:
  - a travelling body wave and tail beat, with a stroke that quickens with speed;
  - flickering pectoral fins and a bank into turns;
  - a slow bob while they rest.
- **Cost:**
  - Each school, each anemone's clownfish and the tangs together are one MultiMesh, with the swim wave in the shader
    and the stroke's phase per fish. Each group's bounds follow its fish, so groups off the screen are not drawn.
  - The small chromis body has fewer rings and segments than the clownfish and tang bodies.
  - Budget: under 0.3 ms GPU, with the earlier GPU-fan fix in mind.

## 5. Where they live (by depth)

| Depth | Clownfish | Blue Tang | Schools |
|---|---|---|---|
| 1 Sunlit Shallows | 4–6 anemones × 2–3 | 4–7 (in ones and pairs) | 1–2 × 45–90 |
| 2 Kelp | 2–3 anemones; quicker to hide | 2–4 | 1 × 30–60 |
| 3 Galleon | 0–1 anemone | 1–2 | 0–1 × 30–45 |
| 4 Neon | none (the bible says "fish hide") | none | none |
| 5 Abyss | none ("no schools") | none | none |
| 6 The Tank | a few listless captives (a later design) | — | — |

- None of them is placed within 20 m of the level's start, so the opening stays calm.
- Placement is seeded from the level, so the same level always has the same fish.

## 6. How it is built

- **In the deterministic sim** (`PlaneReefFish.cs`), like the pufferlings, so they are ready to be corrupted later.
  - The world places them from the map (open water, corridors, plazas, spawns) on their own RNG stream from the
    level's seed, and steps them every tick: the clownfish and tangs swim as the pufferlings do, and the chromis are
    boids round their school's centre.
  - The world also holds the anemones (their positions and sizes), so the clownfish's homes are part of the level.
  - They raise no events and touch nothing else in play.
- **On screen:**
  - `ReefFishMeshes.cs` builds the three bodies;
  - `reef_fish.gdshader` paints and swims them;
  - `ReefFishView.cs` poses them from the sim (height in the water, bank, bob, a tang's grazing dip) and draws the
    anemones with `anemone.gdshader`.
- **Debug:**
  - `--at=fish`, `--at=tang` and `--at=school` start beside an anemone, a tang or a school;
  - `--dbg-follow=clownfish|bluetang|chromis` keeps the camera on the nearest fish of that kind;
  - `--dbg-nofish` hides them all, for comparing GPU time.

## 7. What it does not do (yet)

- No corrupted versions, no Sea-pedia entries, and no sounds beyond the existing ambience.
- The clownfish ninja stays a separate mob. Its "neutral clownfish" schoolmates will reuse this clownfish body when
  the ninja returns (§8, Q6).

## 8. Decisions

| # | Question | Decision |
|---|---|---|
| Q1 | Presentation only, or in the sim? | **In the sim**, like the pufferlings |
| Q2 | Make room for Clementine, or ignore her? | **Make room** (schools part, tangs veer off, clownfish tuck in; her dash from 4 m) |
| Q3 | Which fish for the schools? | **Blue-green chromis** |
| Q4 | Place host anemones for the clownfish? | Yes |
| Q5 | Thin out by depth, or the same everywhere? | **Thin out by depth** (§5) |
| Q6 | Should the clownfish ninja's neutral schoolmates later reuse this clownfish? | Yes, when the ninja returns |
| Q7 | Sizes | Clownfish 0.5 m, tang 0.7 m, chromis 0.34 m: stylised, read at play distance beside the pufferlings |
