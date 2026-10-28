# The adventure

How the adventure (branch `m3-adventure`) plays: the reef is being poisoned from **vents** in its walls; the poison
spreads as ink dots through the water and turns the reef's fish one by one into the creatures she fights; she wins a
level by **sealing the vents**, and the fish she frees are fish again (freed, not killed: THEME-BIBLE).

It is on with `PlaneOptions.Adventure` (the game's default). The simulation is in `src/Core/Plane/PlaneAdventure.cs`
(`AdventureTuning`, `InkDots`, `PlaneVent`, the fish gauges, the three murklings, her inner light) and
`src/Core/Gen/TopDown/AbyssReef.cs` (the abyss); the presentation in `src/Game/TopDown/AdventureView.cs` (dots, their
bracket marks, vents), `BlightView` (the murklings' bodies: `murkling.gdshaderinc` spikes and tadpole tail),
`ReefFishView` (anemone pillars) and `HudView` (the gonad gauge, the vent dots). Tests: `AdventureTests`.

All numbers are first guesses (`AdventureTuning`).

## 1. Corruption vents

- **What:** wounds in the reef walls, 3–5 a level, at least 30 m apart (closer only if the level has no room) and 24 m
  from her start, never at safe places or the way down. Each is a gash torn along a wall face at her plane (2.6 m
  long, pinched at its ends, its edges ragged, each its own), turned up out of the wall's slope so it shows from above,
  its torn rock lips standing out along both edges. A blister of thick ink goo plugs it, bulging out into the water
  and swelling more here than there (`vent_goo.gdshader`): an oily, iridescent film (violet, teal and magenta sliding
  over near-black, marbled like an oil slick, brightest seen edge-on), and through it the reef's inside deep down, a
  current running along the gash and pushing streaks and specks of corruption toward its middle. It breathes, ripples
  run along it, a few drips of goo run out of its lower edge down the wall, stretching and drawing back, and its
  current pushes dark motes of corruption out into the water (while she is within 30 m). A violet light throbs over it.
- **Open, it floods:** from the level's first second every vent wells out an ink dot (§2) every 1.6 s (faster with
  Menace: ÷ 1 + 0.5 × Menace) into the nearest empty cell before its mouth, so the stream pushes out into the water.
- **It has been pouring a while:** the deeper the level, the more ink it starts with. Each vent starts with a puddle of
  60 × Menace dots round its mouth (`PlacePuddles`: none at Depth 1, 12 at Depth 2, 36 at Depth 4, 60 at Depth 6, more
  on later loops), grown out of its mouth as the ink spreads — neighbour to neighbour through open water only, the
  nearest first, its edge ragged — so it lies joined to its vent and spreads from the start.
- **Sealing it:** her shots striking the wall within 1.6 m of its mouth count once each, as a seal wall's carving
  does; the hits must come one after another (1.4 s after the last, one drains away every 0.3 s). Each hit dents its
  goo where her light met it, ringing out over it, splashes goo off it (dark drops in its oily colours, flying out and
  sinking, glinting) and flings magic particles. Her hits show as thin veins of gold light creeping into the goo from
  its torn edges, further the more she has struck. Filled (8 hits, + 2 per Menace) the vent **seals**: with a shudder,
  a puff of magic particles flung out of its mouth and the last of its goo bursting off it, the goo shrinks back into
  the gash and the gash closes to a scar on a small rounded bump of rock, the glow going out; the stream stops. A
  sealed vent stays sealed; each is worth 25 XP.
- **Its ring of light:** sealing sends an unseen ring of her light out from its mouth, 6 m in 0.6 s, clearing the
  ink it crosses (each dot going up in magic particles).
- **Sealing stops the source, not the ink:** the dots it spread beyond its ring stay until cleared, but no longer
  spread (§2), unless they still link to another open vent.
- **The level's end:** the way down opens once every vent on the level is sealed ("The way down is open").

## 2. Ink dots

- **The look:** the corruption is not a stain on the ground but **round ink dots floating at her plane**, on an even
  grid 0.8 m apart, radius 0.135 m: matte ink brushed in dark bands with a dry-brushed violet rim, afloat — each bobs
  slowly up and down on its own beat (`ink_dot.gdshader`). A new dot swells in with a little overshoot.
- **Bracket marks:** while a patch is fed by an open vent (below), violet "(", ")", "[", "]" hop out through it from
  the vent's mouth to the rim, dot by dot (each hop a little arc onto the next dot one step further out), and pop at
  the rim — drawn as the magic particles are (fine-tip strokes), their dark counterpart. A patch cut off shows none.
- **Dividing:** a rim dot of a fed patch winds up before it divides — it swells, straining, over 0.7 s — then snaps
  back as the new dot slides out of it into the empty cell beside it.
- **Spreading:** only a patch joined to a working vent spreads: one whose dots link, neighbour to neighbour, to a dot
  within 2 m of an open vent's mouth. Every 10 s (÷ 1 + 0.3 × Menace) each whole dot of such a patch beside an empty
  cell divides into it, each dot on its own beat. A patch cut off from every open vent — its vent sealed, or her light
  cleared a way through it — stops growing and only waits to be cleared; so do trails and fallen throws. The ink only
  reaches a neighbour with open water between them: never through rock, never into safe places (the start, shops,
  treasure caves). At most 9,000 dots a level.
- **Trails:** every corrupted creature leaves a dot every 2.5 m it swims (into empty cells only); a spiked murkling's
  throw that falls leaves a dot where it lands.
- **Clearing:** her light: the light round her as she swims (worn down over a moment, a dot out of her light fills
  back), her shots — every dot along a shot's whole flight, and round where it pops — a Starburst's burst. Each dot
  cleared, or eaten by a fish, goes up in magic particles.
- **Eaten:** a healthy fish that touches a dot eats it; the ink goes into its gauge (§3). A
  freed fish eats dots harmlessly.

## 3. Fish that turn

- **Many fish:** every level has the Shallows' numbers of reef fish three times over — clownfish at their anemones,
  blue tangs, chromis in schools of 12–24 — and no corrupted creatures at its start.
- **Each fish's gauge** (0..1): it fills 0.12 a second with a dot within 1 m, 0.2 for each dot it eats, and drains
  0.05 a second over clean water. Full, the fish **turns** (the ink creeps over it) into the murkling of its kind
  (§4); a chromis turns with its whole school. A spiked murkling's throw that strikes a fish adds 0.6 to the gauges in
  its splash. A freed fish cannot turn again that level.
- **Pufferfish** (12–18 a level, in ones and twos) fill too: 0.06 a second with a dot within 1.4 m, 0.2 for each dot
  they eat, draining 0.03; full, a pufferfish is an Inkwarden (§4). Anemones are only the reef's: they never turn.

## 4. The murklings

All of them are fish underneath; freed, they are themselves again and swim off. Her light frees them as
it frees any corrupted creature: a corruption meter each, emptied by her shots, holes torn and healing.

| Mob | From | Core | Body | Behaviour |
|---|---|---|---|---|
| **Spiked murkling** | blue tang | blue | 0.38 m, short blowfish spikes all over | Keeps 6–9 m from her, circling, at 3.4 m/s; throws ink at her (the slingshot wind-up); never clings. Touching it hurts and knocks her off. |
| **Tadpole murkling** | clownfish | orange | 0.22 m, a short wiggling tadpole tail | Only chases her (within 16 m), at 1.1 times her cruise; clings on and drains her; never throws. |
| **Shoal murkling** | chromis | silver | 0.2 m, a whole school | Very fast (9 m/s). Within 14 m it rings her 1.8–2.6 m out, spread evenly all round in three layers, and the ring circles her (1.1 rad/s, each layer a little faster than the one inside it; each school its own way), never tied to her aim — she times her shots through the gaps, and a shot that meets one is spent on it. Harmless otherwise. |
| **Inkwarden** | pufferfish | yellow | an inflated ring of ink, eight spikes folded in toward its hole | Wards the corrupted creatures round it (8 m). Calm, it drifts slowly about its spot (within 5 m), turning lazily. Struck (by her light, her arm or a burst), it rolls its spikes out over its top (0.5 s), spins up (0.6 s) and fires them one by one, one every 0.15 s, each in a random direction (11 m/s, 6 damage, an ink spike: it falls as a throw does and her shots burst it); then it slows and grows them back inward (3 s), calm until struck again. Freed, it is a pufferfish, and swims off. |

Each depth's boss level holds its boss in its arena, Drippus Prime (docs/CORRUPTION.md); its brood are murklings like
any other. On a boss level the way down opens once every vent is sealed **and** the boss is freed, in either order.

Switched off in the adventure (their code kept, to bring back one by one): pufferlings, Blightroots, Blotters,
Lashjaws, Thumpsquirts, dens, curse dens, ambushes and murkling budding. Light springs
(docs/CORRUPTION.md: a god ray onto the seabed, a store of healing she may come back to, too bright for any murkling)
come only by her luck (docs/PEARLS.md, "Luck"); one keeps the seabed under it (§5).

## 5. The abyss reef

- **The seabed sinks with the menace:** the open floor between the walls drops away below her plane (`AbyssReef`),
  the deeper the greater the menace: 4 m down at Menace 0, sinking evenly to 26 m at Menace 1 and no further (about
  4.5 m deeper each depth). The walls carry on down, steepening, to the floor's depth 4 m out from the rock on the
  shallowest floor, 8 m on the deepest. Caves and tunnels (and their mouths) keep their floors; the way down keeps its
  own drop. The flora, kelp and buried coins of the dropped floor go with it.
- **Sand dunes** cover the dropped floor, taking over as the walls' feet give onto it: irregular ridges across a wind
  rolled per level, 7 m from crest to crest, each with a long gentle back and a short steep face (a quarter of it),
  up to 1.8 m from crest to trough on the shallowest floor (half that on the deepest). Their lines wander and their
  crests rise and die away patch by patch, and a finer set (a quarter as high, 3.5 m apart) runs across them at an
  angle. Seen clearly on the first depths; the deep water's blur and murk take them further down.
- **Reef shelves:** here and there a rounded ledge of rock juts from a wall just under her plane (1.2 m down, 3 m
  across, its rim falling away into the deep), at least 12 m apart, none within 20 m of her start. The anemones grow
  only on these.
- **Everything at her plane:** the dots, the fish and the murklings swim there.
- **Life in the deep** (`AbyssView`, cosmetic, seeded per level), seen only through the water's blur and murk:
  - **Kelp fields** rise from the deep floor (where it lies 19 m down or more, clear of the way down: only on the
    deepest depths): up to 14 a level, 15 m apart at least, 3.5–7 m across, their strands 11–17 m tall (tallest in the middle), their tips never
    higher than 8.5 m under her plane. Fronds up either side, swaying with a slow swell that rolls through each field.
  - **Big fish** cruise round her as dark shadows 12–17 m down, only where the floor lies deep enough to clear them:
    five at a time, long fish (5–8 m, the tail swinging)
    and rays (3.5–5 m long, their wings beating). Each comes in 18–30 m from her over the deep, crosses near her, turns
    away from rock ahead, and fades out after 20–40 s or once 40 m away; another comes in elsewhere.

## 6. Her oral arm, her inner light and the HUD

- **Her oral arm** stings: it trails her along the way she swam (her last 1.5 s of it, so it gathers in when she
  stops), 3.2 m long and 0.2 m longer for every skill point she spends (at most 6 m). A corrupted creature it brushes
  (a murkling, an Inkwarden) takes 30% of a shot's damage, once every 0.4 s.

- **Inner light** (her stamina, 0..1): a volley spends 0.07, a dash 0.3, a surge 0.45 a second. It refills 0.5 a
  second once 0.35 s pass without spending (about two seconds from empty). Emptied, she cannot shoot or dash until it
  is back to a quarter.
- **Her gauge** (top left): Clementine seen from above. Her bell, a disc of glass with her four gonads and her glowing
  rim (static); holding an active pearl, its colours and pattern fill the glass (no pearl is drawn), whitening for a
  beat when she uses it. Under it her four oral arms hang, one stat each, left to right by urgency, each as long as
  its stat is full, over a faint track of its whole length, with a small sign at its foot:
  1. **health** (warm white-gold; a heart) — a hit frays the lost piece off the arm, drifting away; low (under a
     quarter), it turns ragged and pulses rose;
  2. **inner light** (pale cyan; a spark) — dim and flickering while spent out;
  3. **the active pearl's charge** (the pearl's own colour; a pearl) — breathing when ready; without one, the arm is
     curled up short and dim;
  4. **experience** (amber; her level at its foot) — growing toward the next level, pulsing full with a point to
     spend.
  Beside the bell **her shells** (the shell and its count — shown only once she has collected at least one).
- **Maps:** the minimap and the whole level's map (Tab / Select) both hidden unless she has absorbed **Sea Chart Nautilus** ("Remembers every current"; shop and treasure).
  Never an experience ring or health bezel round it: a thin outline and the north mark only. The cleansing meter is
  gone.
  The whole level's map is cut out of its square along the level's outline — all its water and a few metres of rock
  round it, seen or not (the unseen under fog) — its edges fading softly into a dimming of the game, framed as large as
  it fits; no legend. Over it, the level's
  template name, its depth and level, and the run's clock.
- **Vent dots:** top centre, one per vent — dark with a violet glint while it pours, gold once sealed. The level's
  title and the banners sit under them.
