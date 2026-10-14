# The corruption war

How Ink Deep's corruption, its enemies and the boss arena are built. The design brief is "Corruption & Combat:
Implementation Instruction". The simulation is in `src/Core/Plane/PlaneCorruption.cs` (the field, its tuning and
`PlaneOptions`) and `src/Core/Plane/PlaneBlight.cs` (Blightroots, gloomvines, valves, murklings, the arena). The
presentation is in `src/Game/TopDown/CorruptionView.cs`, `BlightView.cs` and `CleanseMeter.cs`, and in the shaders
named below. The pufferling enemies are retracted (`PlaneOptions.Pufferlings = false`): none are placed, and ambush
clearings bud murklings instead. The healthy fish still swim the reef.

## The field

- A density grid of 0..1 for every half-metre cell (250 × 250), laid at level start from the map alone
  (`CorruptionField.ForMap`). **Half the open floor** starts corrupted (`Cover` 50% + 10% per Menace, at most 85%).
  Each cell's score is patchy noise lifted toward the level's deep end (its boss arena, else its exit;
  `Condense` 0.18). The top share is corrupted, thin (0.5) at its edges and thick (1) well inside. In practice about
  60% of the floor near the deep end is corrupt against about 12% at the far side.
- **Spreading:** Blotters smear corruption over clean floor (`CorruptionField.Spread`). Spread ink counts toward the
  level's total to cleanse, and Blightroots regrow it. It never reaches safe places (`Guard`).
- **Safe places** (the start, shops and treasure caves) are clean out to their radius + 3 m, feathered over 4 m.
  Blightroots never stand within 18 m of their edge.
- The **boss arena** is the condensation zone: density 1 everywhere inside.
- **Her light** cleanses a 2.4 m disc under her at 3.2 density/s. Lantern Pearl's glow widens it. The corruption
  never harms her.
- **A popping bubble** cleanses a burst of 1.4 m + 0.22 m per merged bubble (at most 4.2 m), scaled by the bubble's
  remaining light.
- Totals are kept as cells change, so the level's cleanse share (`PlaneWorld.Cleansed`) costs nothing to read.
- **Tipping points:** at 50% cleansed murkling budding slows (×2 interval everywhere); at 75% regrowth stops.

## Blightroot (`Blightroot`)

- Placed on plazas and canyon points, reachable from the start, 22 m apart, never in the arena or the shaft. There
  are 3 + 1.5 × Menace of them, at most 6.
- **Regrowth:** it restores its 9 m reach toward the starting density at 0.1/s, strongest at the stump.
- **Shot down:** its corruption meter (see "Corruption meters" below) is 125 × (1 + 0.2 × Menace), so it takes 13
  default shots on its 0.9 m sac in the Shallows, more deeper down. It re-corrupts quickly: 30/s (more with Menace) once 0.5 s pass without a hit. Emptied, it bursts. A
  wave of cleansing runs through its whole reach in 0.7 s and its vines dissolve. Cleansing the ground round it no
  longer harms it.
- **The pulse:** every 4 s the sac inflates for 0.8 s (the telegraph), then a ring of ink runs out at 10 m/s. The ring
  stains cleansed ground back to 0.35 and snuffs out each of her shots it crosses. Passing over her, it corrupts her
  for 6 + 2 × Menace (once per ring). Dashing through it takes nothing.

## Blotters (`Blotter`)

Giant ink slugs that spread the corruption (`PlaneBlotters.cs`).

- **Waking:** 2 + 1 per Menace (at most 6 alive) wake with the level. They start on thick ground (≥ 0.6), at least
  25 m from her start and 15 m apart, out of the arena and the shaft.
- **Born from corruption:** every 20 s there is a 5% chance (+3% per Menace) that one rises out of thick ground at
  least 18 m from her. It takes 1.5 s, with a dark ripple and a hint.
- **Creeping:** 1.2 m/s (+0.2 per Menace), with a surging slug gait. It turns slowly (1.6 rad/s) toward clean floor
  in sight 4–20 m off, the nearer the better, avoiding safe places and the arena. It re-picks a target on arrival,
  after 10 s, when stuck for 2.5 s, or once its target has been corrupted.
- **Spreading:** it smears ink over a 2.6 m disc beneath it, toward density 0.9.
- **Tough:** its corruption meter is 100 (+25% per Menace), about ten default shots in the Shallows. It regrows
  slowly (5/s) after 2 s without a hit. Each shot bursts on it. Emptied, it bursts into gold and cleanses a 5 m disc.
- **Body:** six segments along its trail, tapering from 1.1 m (`Blotter.Segment`). Every segment takes hits and hurts
  on contact; the view draws the same segments.
- **Contact:** 8 corruption, with the usual hit grace, and a 7 m/s shove away.
- **Corruption bombs:** every 3.5 s it spits an ink ball up from its mouth. The ball arcs for 1.1 s and lands 3–7 m
  away at random. If her light struck it in the last 3 s, it lands where she stood when it was thrown, if she is
  within 13 m. A dark ring on the ground marks the landing spot and tightens as the ball falls. The splash spreads
  corruption over 2.2 m. If she is in it, she takes 10 corruption and is thrown back at 9 m/s. A dash slips it.
- **Guards:** every ~6 s (less with Menace) there is a 70% chance a murkling buds 2.5–4.5 m from it, up to 2 + Menace
  guards alive (`Murkling.Guarding`).
- **Looks** (`BlightView.Blotters.cs`): a chain of six glossy ink mounds from the floor up past her plane, following
  the head along its trail with a wave down its back. Two eye stalks are tipped with violet, and ink beads off it. On
  both maps it is a fat ink slug shape pointing the way it creeps. The corruption blades now stand on all open floor,
  so a Blotter's trail grows them.

## Buried caches (`BuriedCache`) and light orbs

With the corruption on there are no pots (`PlaneOptions.Vases` is off). Loot lies **buried under the ink** instead
(`PlaneCaches.cs`):

- **Placement:** 8–12 a level, under ink at least 0.55 thick, 9 m apart. They sit at least 14 m from her start and
  stay clear of places, the arena and the shaft.
- **Look:** a glossy bump in the ink (`BlightView.Caches.cs`). A faint warm glint shows through now and then. The bump
  sinks as the ink over it thins.
- **Uncovering:** once the ground over it is cleansed (her light, a pop), it breaks open in a puff of gold with a chime
  (and a hint the first time). Its loot floats up: a light orb (45%) or 2–4 shells.

**Light orbs** replace hearts everywhere: drops, the shop's top-up stand and caches. Each is a bubble with her light
inside, like her old bubble shots, with a soft halo and a warm light. It washes 12 corruption away. The sim still calls
it `PlaneHeart`. She takes one only when corrupted at all; otherwise it waits.

## Murklings (`Murkling`)

- **Budding:** each living Blightroot's area buds one from dense ground (≥ 0.6), at least 5 m from her, every
  15 s / (1 + 0.6 × Menace). The bud takes 0.8 s and is shown as a dark bead swelling out of the floor with a ripple.
- **Cap:** 2 + 1.5 × Menace per area. An area cleansed past 80% stops budding for good.
- **The arena** buds every 6 s, but only while its boss fights.
- **Movement:** 2.6 + 0.4 × Menace m/s (she cruises at 6), toward the brightest light within 14 m, her or one of her hanging bubbles (a merged one
  shines brighter than her). This makes bait and herding possible.
- **Attack:** a black orb thrown when she is within 9 m and in sight, for 7 damage. It flies at 10 + 1.5 × Menace m/s
  and is thrown every ~2.4 s / (1 + 0.25 × Menace).
- **Contact:** 6 damage, then it clings (at most 3 at once), draining 2.5 HP/s each and dimming her bell. Feeding shows
  as one corruption number a second, summed over it. A dash bursts her light: each clinging murkling is thrown off
  (9 m/s), losing one default shot's worth (10) of its meter, and is dazed for 0.7 s. Then it chases her again. One
  already worn down bursts. The dash shows as a small light burst, not an ink cloud.
- **Killed by light:** its corruption meter is 36 × (1 + 0.2 × Menace), so four default shots in a row burst it in
  the Shallows. It regrows at 14/s (more
  with Menace) after 0.8 s without a hit, and one strong (merged) shot empties it at once. Each shot bursts on it, and
  a piercing one flies on. Passing through a throw halves a shot's light. A murkling burst over cleansed ground drops
  its ember, which blooms a 2.6 m patch of colour.

## Corruption meters (`CorruptionMeter`)

Fighting the corrupted is a matter of thresholds and illuminance. Every murkling and Blightroot has a corruption
meter that starts full. Each shot of her light takes its damage (illuminance) off. After a pause without hits the
meter regrows, at a rate set by the creature's kind and the Menace. Emptied, the creature is cleansed: one strong
enough shot does it at once, or keep shooting until the meter runs out.

The meter is drawn as a ring round the creature (`corruption_ring.gdshader`), always on top. It fills clockwise from
the top with ink and a violet edge for what is left, and the chunk the last shots took off burns gold-white as it
drains away. It is shown only while partly emptied: it fades out when full again, and it is gone once emptied.

## Gloomvines (`Gloomvine`)

- Two per Blightroot. They rest as 2.6 m curls of ink.
- When she lingers (under 1 m/s for 0.6 s) within reach, the tip creeps toward her at 0.8 m/s, up to 11.7 m long.
  This only punishes standing still.
- **Coil:** a tip that reaches her holds her for 1.2 s, draining 5 HP/s. A dash frees her. It then lets go and draws
  back 2 m.
- **Cutting:** a popping bubble cuts the vine where it meets it, and everything past the cut is gone. Vines regrow at
  0.4 m/s while the ground at their root is still corrupted.

## Valves (`Valve`)

- Placed 2.5 m out on the spur of each shell cache, secret and curse den. From level 2 onward there is also one at
  the narrowest neck of the main way.
- **Crossing:** she always can (light passes through dark). It costs up to ~11 HP at full integrity, scaled by
  integrity and distance swum inside. **Dashing through costs nothing.**
- **Cleansing its edges:** her light within 2.2 m of it wears it down in about 3 s, and each pop near it takes 0.06
  per bubble.
- **Cleared for good** once she has passed through, or once it is worn away. Each valve marks itself on both maps
  once seen.

## Queen Clam (the Depth 1 boss)

Redesigned to fit the game (art rules in `CLAUDE.md`): a real giant clam, no face.

- **Look** (`BossView`, `clam_shell.gdshader`): two heavy valves with radial ribs, growth lines and a scalloped lip.
  The corruption drains their colour to ash and settles ink into every groove. The mantle inside is dark. In her
  middle lies a **black pearl** (`corruption_orb`) with a cold glint that stirs while she is open.
- **She does not see Clementine.** She lies with her opening toward the screen (south, the camera's side, so the
  black pearl shows) and never turns. She opens and
  closes her lid and throws pearls from her opening: rings, walls thrown in a random spread around her facing, the
  helix, and in phase 2 the homing royal pearl. The snap still comes if Clementine is close while she is open.
- **Her pearls are corruption:** glossy ink orbs. Clementine's light passes through them and cannot clear them.
- **Weak spot:** only the black pearl (0.9 m, `CoreRadius`) can be hurt, and only while she is open (the light flies
  over the raised lid). Shut, or on the shell, shots break harmlessly.
- **Every boss's rule** (`CLAUDE.md`): a boss holds a corrupted pearl (`PlaneBoss.PearlAt`), the source of its
  malice and its only weak spot; defeated, the cleansed pearl is the reward, left exactly where it was attached.
- **Defeat:** the pearl bursts into bright light and stays white. The whole arena is wiped clean of corruption
  (`CorruptionField.Clear`), so the Crack opens as soon as the arena unseals. Her colour floods back. After a short
  shudder she leaps 6.5 m aside in one arc (0.7 s) and lies there wide open. Her cleansed pearl is the reward: it
  stays exactly where it sat in her (over the Crack's middle) as she leaps away from under it, ready to take.
- **The arena is hers alone during the fight:** sealing it sets every murkling (clinging ones too) and Blotter inside
  down just outside. Murklings never bud in it. Murklings, Blotters and vines cannot cross the sealed rim, murkling
  throws stop at it, and a Blotter's bomb bound inside lands just outside.

## The arena: condensation

- **Crust:** the boss carries 5 crust layers, one stripped per 20% of the arena cleansed. Each layer absorbs 17% of a
  bubble's damage. Her ring and wall volleys grow thinner with each layer gone (60% of the full count with no crust).
  Her pearls' reach grows shorter (65% of full life with no crust). Her crust is drawn as glossy ink lumps that break
  off as layers fall.
- **Easing:** at the fight's start, her health and pattern density fall by up to 30% with the level cleansed (an
  explorer meets an easier boss).
- **The Crack** opens only when she is freed **and** the arena is ≥ 60% cleansed. The HUD tracks both. Until then it
  lies sealed under sand, flush with the arena floor, with no glow. When it opens, the sand drains into it over 3 s
  (the middle sags first and the sand turns as it goes, grains pour off its rim, the ground rumbles), and then its
  orange glow, light and bubbles come up from below.

## Her corruption meter (replaces HP)

Clementine has no health bar. Everything that hurts her corrupts her instead, and her meter fills. When it is full the
corruption takes her and the run is lost. Inside, the sim still keeps `Hp`: corruption = 1 − Hp / MaxHp, and Hp 0
means a full meter. All damage, healing and items work unchanged.

| Part | Where | What |
|---|---|---|
| Meter | `MinimapView` frame | Replaces the red bezel round the minimap. Dark ink fills it clockwise from 12 o'clock over a pale gold track. A violet sheen sits on the ink's lip and pulses above 75 %. An ink bead with a glint marks the tip. |
| Numbers | `DamageNumbers` | No signs, two colours only: light and corruption. A hit on her shows its amount in dark violet ink with a pale violet edge. Her hits on anything and her cleansing or healing show in bright gold light. |
| Words | pause menu, save card, resume stats, death splash | "X% corrupted". The death splash reads "The corruption took Clementine". |

## Light springs (`LightSpring`)

A pool of clear, luminous water where her corruption washes away. Each level has a 50 % chance of one
(`SpringTuning.Chance`). The level seed places it on open floor in a plaza or corridor, at least 22 m from the start,
14 m from the exit, and clear of the shaft and other POIs (`LightSprings.Of`). Its 3 m radius is a safe place: the
field never seeds there.

- While she is inside, her meter drains by 30 % of the whole each second until it is empty (`StepSprings`).
- Entering it while corrupted raises `SpringEntered`: a chime, plus a hint the first time. Reaching clean raises
  `SpringCleansed`: a bell and "Wholly cleansed".
- Visuals (`SpringView`):
  - `light_spring.gdshader`: drifting caustics, a bright rim, and under the water her emblem, the four gold gonad
    horseshoes in a ring round a gold heart. The emblem turns slowly and wavers with the ripples.
  - `light_column.gdshader`: a soft column of light rising to her plane.
  - Rising aqua and gold sparkles, and an aqua OmniLight.
  - While she bathes, everything brightens and warms, the emblem turns faster, and the sparkles stream.
- On both maps, once seen: an aqua dot with a gold heart.

## Evaporation: corruption clearing

Cleared corruption never just vanishes. It evaporates.

| Where | What |
|---|---|
| The floor (`InkVapour.FromField`) | Each texture upload lists the cells whose density fell (`CorruptionTexture.Drops`). Ink lifts off them as dark curls that rise and swell, then thin to violet and fade. Gold glints rise with them. The chance of a curl grows with how much a cell lost. At most 48 curls a frame. |
| Her (`InkVapour.FromHer`) | When her meter drains (a light spring or any healing), the same vapour steams off her body. It is thicker the faster she clears. |
| Her meter (`MinimapView` frame) | Wisps of ink steam off the arc's receding tip. They drift outward and up, curling, and turn violet, then gold, as they fade. |

## Presentation

- **One texture** (`CorruptionTexture`) holds the field: R is density, G is the gold of corruption dying. G is worked
  out from how fast each cell falls and fades over ~1.2 s. It uploads at most 30 times a second, and only while
  something changes. The level's floor, its blades and both maps read it.
- **Blades** (`corruption_blades.gdshader`): ~45k swaying drained blades per level in 64 chunked MultiMeshes (only those
  on screen are drawn). They are laid out off the main thread. A level below is laid out while she is still above it,
  so a dive costs nothing. They are the corruption's only mark on the floor: about 0.4–1.3 m tall, pale, waxy and limp,
  drooping over, with a faint violet sheen. They sway in the current and bend away from her light. As the ground is cleansed the colour
  floods back: they stand up and turn green while they shrivel and dissolve, burning gold at the tips.
- **The floor** (`reef_surface.gdshaderinc`): the ground keeps its own colour under the blades; nothing covers it.
  Where the corruption dies the floor burns gold a moment. The level below shows its own corruption through the shaft.
- **Corrupted bodies** (`corruption_orb.gdshader`: murklings, sacs, stumps, throws, the crust) share one look:
  ink-black, glossy, a cold violet rim, and murklings carry a dim stolen ember.
- Other shaders:
  - `ink_ring.gdshader`: pulses, buds, bursts.
  - `valve_ink.gdshader`: rolling ink smoke whose cracks burn gold as it wears.
  - `gloomvine.gdshader`: glossy tubes with a violet pulse, dissolving in gold.
- **HUD** (`CleanseMeter`): the level cleansed, with the 50% and 75% tipping points marked. On boss levels it also
  shows the arena, with the Crack's 60% mark and the crust as pips.
- **Maps:** open water reads ink-violet where corrupted, so the map regains its colour as she plays. Blightroots and
  seen valves are marked.

## Not yet

- Gloomvines and the arena's crust lumps have no sounds of their own; the war's sounds reuse the existing set.
- Murklings stay out of the Sea-pedia, which lists pearls and creatures from `SeaPediaCard.Creatures` only.
