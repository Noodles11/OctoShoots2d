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

- **What:** geyser-like openings in the reef walls, 3–5 a level, at least 30 m apart (closer only if the level has no
  room) and 24 m from her start, never at safe places or the way down. Each is a short cone of rock standing out of a
  wall face at her plane, its mouth toward open water, a dark throat glowing violet.
- **Open, it floods:** from the level's first second every vent wells out an ink dot (§2) every 1.6 s (faster with
  Menace: ÷ 1 + 0.5 × Menace) into the nearest empty cell before its mouth, so the stream pushes out into the water.
- **Sealing it:** her shots striking the wall within 1.6 m of its mouth count once each, as a seal wall's carving
  does; the hits must come one after another (1.4 s after the last, one drains away every 0.3 s). Filled (8 hits,
  + 2 per Menace) the vent **seals**: with a shudder and a puff of magic particles flung out of its mouth, its cone
  sinks back into the wall, the glow going out, until only a small rounded bump of rock is left; the stream stops. A
  gold ring round its mouth shows her hits. A sealed vent stays sealed; each is worth 25 XP.
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
- **Anemones** fill too (0.05 a second with a dot within 1.6 m, draining 0.03): full, the anemone is an Inkwarden;
  freed, it is an anemone again and cannot turn again that level.

## 4. The murklings

All of them are fish (or an anemone) underneath; freed, they are themselves again and swim off. Her light frees them as
it frees any corrupted creature: a corruption meter each, emptied by her shots, holes torn and healing.

| Mob | From | Core | Body | Behaviour |
|---|---|---|---|---|
| **Spiked murkling** | blue tang | blue | 0.38 m, short blowfish spikes all over | Keeps 6–9 m from her, circling, at 3.4 m/s; throws ink at her (the slingshot wind-up); never clings. Touching it hurts and knocks her off. |
| **Tadpole murkling** | clownfish | orange | 0.22 m, a short wiggling tadpole tail | Only chases her (within 16 m), at 1.1 times her cruise; clings on and drains her; never throws. |
| **Shoal murkling** | chromis | silver | 0.2 m, a whole school | Very fast (9 m/s). Within 14 m it rings her 1.8–2.6 m out, spread evenly all round in three layers, and the ring circles her (1.1 rad/s, each layer a little faster than the one inside it; each school its own way), never tied to her aim — she times her shots through the gaps, and a shot that meets one is spent on it. Harmless otherwise. |
| **Inkwarden** | sea anemone | — | the Inkwarden | Wards the corrupted creatures round it. |

Switched off in the adventure (their code kept, to bring back one by one): pufferlings, Blightroots, Blotters,
Lashjaws, Thumpsquirts, dens, curse dens, light springs, ambushes, murkling budding, Queen Clam and the boss arenas.

## 5. The abyss reef

- **No seabed:** the open floor between the walls drops away below her plane (`AbyssReef`): the walls carry on down,
  steepening, to 26 m deep 8 m out from the rock, and the water's deep murk takes it. Caves and tunnels (and their
  mouths) keep their floors; the way down keeps its own drop. The flora, kelp and buried coins of the dropped floor go
  with it.
- **Reef shelves:** here and there a rounded ledge of rock juts from a wall just under her plane (1.2 m down, 3 m
  across, its rim falling away into the deep), at least 12 m apart, none within 20 m of her start. The anemones grow
  only on these.
- **Everything at her plane:** the dots, the fish and the murklings swim there.

## 6. Her inner light and the HUD

- **Inner light** (her stamina, 0..1): a volley spends 0.07, a dash 0.3, a surge 0.45 a second. It refills 0.5 a
  second once 0.35 s pass without spending (about two seconds from empty). Emptied, she cannot shoot or dash until it
  is back to a quarter.
- **The gonad gauge** (top left): her emblem's four horseshoes, open toward the middle, each filling along its arc:
  health (top left; what she lost is ink, flashing violet on a hit), inner light (top right; dim and flickering while
  spent out), experience (bottom right; her level inside it; pulsing full with a point to spend), the active pearl's
  charge (bottom left, in the pearl's colour; breathing when ready). In its middle the **active pearl** itself, turning
  (an empty socket without one); under it **her shells**, the shell and its count, shown once she has had one —
  collected shells fly there.
- **Maps:** the minimap and the whole level's map (Tab / Select) both hidden unless she has absorbed **Sea Chart Nautilus** ("Remembers every current"; shop and treasure).
  Never an experience ring or health bezel round it: a thin outline and the north mark only. The cleansing meter is
  gone.
  The whole level's map is cut out of its square along the level's outline — all its water and a few metres of rock
  round it, seen or not (the unseen under fog) — its edges fading softly into a dimming of the game, framed as large as
  it fits; no legend. Over it, the level's
  template name, its depth and level, and the run's clock.
- **Vent dots:** top centre, one per vent — dark with a violet glint while it pours, gold once sealed. The level's
  title and the banners sit under them.
