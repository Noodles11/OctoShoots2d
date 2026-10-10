# The corruption war

How Ink Deep's corruption, its enemies and the boss arena are built. The design brief is "Corruption & Combat:
Implementation Instruction". The simulation is in `src/Core/Plane/PlaneCorruption.cs` (the field, its tuning and
`PlaneOptions`) and `src/Core/Plane/PlaneBlight.cs` (Blightroots, gloomvines, valves, murklings, the arena). The
presentation is in `src/Game/TopDown/CorruptionView.cs`, `BlightView.cs` and `CleanseMeter.cs`, and in the shaders
named below. The pufferling enemies are retracted (`PlaneOptions.Pufferlings = false`): none are placed, and ambush
clearings bud murklings instead. The healthy fish still swim the reef.

## The field

- A density grid of 0..1 for every half-metre cell (250 × 250), laid at level start from the map alone
  (`CorruptionField.ForMap`). Open floor is corrupted in patches (0.45–1 from two octaves of noise).
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
- **Immune to shots:** a bubble reaching the 0.9 m sac is drunk. It is absorbed with no pop, no damage and no
  cleansing.
- **Starving:** when the mean density of its outer ring (55–100% of its reach) falls below 0.12, it bursts. A wave of
  cleansing runs through its whole reach in 0.7 s and its vines dissolve.
- **The pulse:** every 4 s the sac inflates for 0.8 s (the telegraph), then a ring of ink runs out at 10 m/s. The ring
  stains cleansed ground back to 0.35 and dims each of her bubbles it crosses to 70% (each ring once).

## Murklings (`Murkling`)

- **Budding:** each living Blightroot's area buds one from dense ground (≥ 0.6), at least 5 m from her, every
  15 s / (1 + 0.6 × Menace). The bud takes 0.8 s and is shown as a dark bead swelling out of the floor with a ripple.
- **Cap:** 2 + 1.5 × Menace per area. An area cleansed past 80% stops budding for good.
- **The arena** buds every 6 s, but only while its boss fights.
- **Movement:** they drift toward the brightest light within 14 m, her or one of her hanging bubbles (a merged one
  shines brighter than her). This makes bait and herding possible.
- **Attack:** a black orb thrown every ~2.4 s when she is within 9 m and in sight, for 7 damage.
- **Contact:** 6 damage, then it clings (at most 3 at once), draining 2.5 HP/s each and dimming her bell. A dash
  shakes them off into the ink cloud.
- **Killed by light:** one bubble bursts a murkling, and the bubble keeps flying at half its light (and damage).
  Passing through a throw also halves its light. A murkling burst over cleansed ground drops its ember, which blooms a
  2.6 m patch of colour.

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

## The arena: condensation

- **Crust:** the boss carries 5 crust layers, one stripped per 20% of the arena cleansed. Each layer absorbs 17% of a
  bubble's damage. Her ring and wall volleys grow thinner with each layer gone (60% of the full count with no crust).
  Her pearls' reach grows shorter (65% of full life with no crust). Her crust is drawn as glossy ink lumps that break
  off as layers fall.
- **Easing:** at the fight's start, her health and pattern density fall by up to 30% with the level cleansed (an
  explorer meets an easier boss).
- **The Crack** opens only when she is freed **and** the arena is ≥ 60% cleansed. The HUD tracks both.

## Presentation

- **One texture** (`CorruptionTexture`) holds the field: R is density, G is the gold of corruption dying. G is worked
  out from how fast each cell falls and fades over ~1.2 s. It uploads at most 30 times a second, and only while
  something changes. The level's floor, its blades and both maps read it.
- **Blades** (`corruption_blades.gdshader`): ~45k swaying ink blades per level in 64 chunked MultiMeshes (only those
  on screen are drawn). They are laid out off the main thread. A level below is laid out while she is still above it,
  so a dive costs nothing. Thick ground grows tall glossy blades with a violet rim and a slow pulse up their tips. They
  sway in the current and bend away from her light. As the ground is cleansed they shrivel, curl, ash, dissolve and
  burn gold at the tips.
- **The floor** (`reef_surface.gdshaderinc`): ink-black and glossy under the corruption, with faint violet veins. It
  drinks the caustics. Where it dies it burns gold, and its retreating edge keeps a warm fringe. The level below shows
  its own corruption through the shaft.
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
