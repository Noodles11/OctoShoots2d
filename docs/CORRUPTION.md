# The corruption war

How Ink Deep's corruption, its enemies and the boss arena are built. The design brief is "Corruption & Combat:
Implementation Instruction". The simulation is in `src/Core/Plane/PlaneCorruption.cs` (the field, its tuning and
`PlaneOptions`) and `src/Core/Plane/PlaneBlight.cs` (Blightroots, murklings, the arena). The
presentation is in `src/Game/TopDown/BlightView.cs`, `CorruptionView.cs` (the field's
texture) and `CleanseMeter.cs`, and in the shaders
named below. The pufferling enemies are retracted (`PlaneOptions.Pufferlings = false`): none are placed, and ambush
clearings bud murklings instead. The healthy fish still swim the reef, and the murklings hunt them (see Lesser
murklings).

## The field

- A density grid of 0..1 for every half-metre cell (250 × 250), laid at level start from the map alone
  (`CorruptionField.ForMap`). **Half the open floor** starts corrupted (`Cover` 50% + 10% per Menace, at most 85%).
  Each cell's score is patchy noise lifted toward the level's deep end (its boss arena, else its exit;
  `Condense` 0.18). The top share is corrupted, thin (0.5) at its edges and thick (1) well inside. In practice about
  60% of the floor near the deep end is corrupt against about 12% at the far side.
- **Spreading:** Blotters smear corruption over clean floor (`CorruptionField.Spread`). Spread ink counts toward the
  level's total to cleanse, and Blightroots regrow it. It never reaches safe places (`Guard`).
- **Safe places** (the start, shops and clam rooms) are clean out to their radius + 3 m, feathered over 4 m.
  Blightroots never stand within 18 m of their edge.
- The **boss arena** is the condensation zone: density 1 everywhere inside.
- **Her light** cleanses a 2.4 m disc under her at 3.2 density/s. Lantern Pearl's glow widens it. The corruption
  never harms her.
- **A popping bubble** cleanses a burst of 1.4 m + 0.22 m per merged bubble (at most 4.2 m), scaled by the bubble's
  remaining light.
- **The ink is the matter the corrupted are made of.** Every creature born during the level (a budding murkling or
  prime, a Blotter's guard, a Blotter born from corruption) drinks a fixed amount of ink (`CorruptionField.Plan`,
  `Drink`) from anywhere on the level: the nearest corrupted cells first, then further and further out until its
  cost is paid, however far that is. So its draught is small on thick ink and reaches far where the ink is thin or
  gone. Only when the whole level holds less than its cost is it not born. The ground it drank is left
  dark and empty: the shards there stop and no gold shows, since she did not cleanse it (`CorruptionField.Drunk`). A
  living Blightroot regrows that ground like any other. The creature holds the ink (`CorruptionField.Held`) until she
  frees it; then it is cleansed (`Release`). Costs are in density × m² (1 is a square metre of the thickest ink):
  a murkling 3.6 (about a 1.2 m disc of thick ink), a prime 43.2 (twelve murklings), a Blotter born 21.6 (six
  murklings).
- **The look of a birth** (`BlightView.Gathers.cs`): the ink is sucked in as if by a vacuum. Dots of ink leave the
  very cells it drinks (as many from each as its share), are lifted off the floor, creep, then whoosh in, stretching
  along their flight and arcing over any rock in the way, into a blob of darkness swelling low over the floor as they
  arrive. Once all the ink is in, the blob rises to her plane as the creature. The dots are drawn after the water
  pass, like the ink, so they stay sharp. The creatures that wake with the level, ambush buds (the trap's
  own ink) and lesser murklings (turned fish) drink nothing.
- Totals are kept as cells change, so the level's cleanse share (`PlaneWorld.Cleansed`) costs nothing to read. It
  counts the ink on the ground and the ink the living creatures hold, so a birth never makes the level cleaner: only
  her light and freeing them do.
- **Tipping points:** at 50% cleansed murkling budding slows (×2 interval everywhere); at 75% regrowth stops.

## Blightroot (`Blightroot`)

An octopus the corruption has drawn up into a pole of ink (`BlightView.Octopus.cs`), draining its ink into the
ground round it, which is why that ground is the level's main source of ink. Freed, it becomes itself again and swims
away.

- **Its look** (all of it painted as the murklings are, its ink brushed in flowing strokes with ragged dry-brushed
  rims in its blue; no eyes):
  - A pole of ink (`ink_stalk.gdshader`) standing from a splay of rootlets on the sand up to its head, swaying, knotted
    here and there, its strokes running up it.
  - Its head, raised high on the pole (1.9 m over her plane: it stands tall, as kelp does): the target her light drains.
    Standing between her and the camera, its head and pole are drawn over her where they cover her, by kelp's rule
    (`over_her.gdshaderinc`, `murkling_over`, `ink_stalk_over`), and she wears her outline meanwhile. A liquid body (as a murkling's,
    `murkling.gdshader`): it breathes, swells on the pulse, sways on its pole, and shrinks as her light drains it. Her
    hits tear it as they tear a murkling: a deep dent that rings back, ragged holes (its inner blue light showing
    through) that close as its meter refills, chunks of ink ripped away. Ink runs down its pole into the sand.
  - It wears the Blightroot's blue under the ink, and its hit outline like every corrupted creature.
- **Freed** (smoothly, over 1.5 s, then away): its ink bursts off its head; the pole runs away from the top down into
  drips and the rootlets draw back into the sand; its head takes its own healthy coral, rounds out whole and draws out
  into a mantle while eight arms grow out under it, hanging, then curling. Then it swims away from her, rising, in
  strokes of its arms (each a contraction, arms closing back and the mantle squeezed, that drives it on, then a glide
  as they flare), leaning its mantle into its way, a trail of clean bubbles behind it. It fades out 4.6 s after it was
  freed.
- **Its guardians:** while it has been struck by her light within the last 3 s, its area's murklings wake to guard
  it. Its area buds every 5 s ÷ (1 + 1.5 × Menace) (5 s in the early depths, 2 s by the Tank), each bud 4–7 m round
  it (at least 5 m from her, drinking its ink as any bud does). Its cap rises by 1 + round(2 × Menace), and it buds
  even when its area has been cleansed out of budding. As they wake a dark blue ripple runs out over the floor and a
  low call sounds (`GuardiansWoke`).
- Placed on plazas and canyon points, reachable from the start, 22 m apart, never in the arena or the shaft. There
  are 3 + 1.5 × Menace of them, at most 6. A level's template (DESIGN-TOPDOWN §4.8) may cast them otherwise: so many,
  at its anchors (by a place it guards, in dead ends…), or none; and every corrupted kind below likewise.
- **Regrowth:** it restores its 9 m reach toward the starting density at 0.1/s, strongest at the octopus.
- **Shot down:** its corruption meter (see "Corruption meters" below) is 125 × (1 + 0.2 × Menace), so it takes 13
  default shots on its 0.9 m mantle in the Shallows, more deeper down. It re-corrupts quickly: 30/s (more with
  Menace) once 0.5 s pass without a hit. Emptied, it is freed (above; its ink pops, see evaporation below). A wave of
  cleansing runs through its whole reach in 0.7 s. Cleansing the ground round it no longer harms it.
- **A boxing pole** (`BlightView.Octopus`): its foot held fast in the sand, its pole leans on a spring and rings back
  upright. Her hits push it back, away from the shot, each as hard as it struck (a push of 1.4 + 6 × the hit's share
  of its meter); a run of them pushes it further, never past 0.45 rad.
- **Its hammer** (`PlaneBlight.StepHammer`, `BlightView.Octopus`): when she comes within 4.2 m it rears back away from
  her for 0.5 s, its pole drawing up to 1.3 times its height, its aim following her; then its head comes down at her
  in 0.14 s, ever faster as a hammer falls, its pole drawn out as far as it needs to reach. If she is still within
  4.6 m and in front of it as it lands, it lands on top of her (drawn over her, as above), corrupts her
  for 10 + 3 × Menace and knocks her away at 9 m/s; else it strikes the sand as far as it reaches her way. Where it
  lands: its liquid head flattens on the blow and rings back, 16–22 drops of it are torn off and flung out round it
  (slowed hard by the water, sinking a little), 3–5 chunks of it are ripped away, and the water is shoved out in two
  rings of refraction (the swells' distortion, `WaterSwells.AddRing`: a quick wide one and a closer one); a thud and
  a jolt of the screen. It rests on what it struck for 0.1 s, then rises back slowly through the water over 0.55 s
  and waits 1.2 s before it may swing again. A dash as it comes down takes nothing. It never swings while it gathers
  a pulse.
- **The pulse:** every 4 s it gathers for 0.8 s (the telegraph): its head swells, a swell of ink climbing its pole into
  it, and it stretches up a little; then it squashes all the way down to the sand in 0.07 s as a ring of ink runs out
  at 10 m/s, and springs back up. The ring
  stains cleansed ground back to 0.35 and snuffs out each of her shots it crosses. Passing over her, it corrupts her
  for 6 + 2 × Menace (once per ring). Dashing through it takes nothing.

## Blotters (`Blotter`)

Hermit crabs the corruption took over: a small crab under a huge shell of ink, spreading the corruption
(`PlaneBlotters.cs`).

- **Waking:** 2 + 1 per Menace (at most 6 alive) wake with the level. They start on thick ground (≥ 0.6), at least
  25 m from her start and 15 m apart, out of the arena and the shaft.
- **Born from corruption:** every 20 s there is a 5% chance (+3% per Menace) that one rises out of thick ground at
  least 18 m from her, while the level holds ink enough for it. It takes 1.5 s, with a dark ripple, and drinks six
  murklings' worth of ink (21.6) as it rises, sucked into it from the nearest ink. Freed, that ink is cleansed.
- **Creeping:** 1.2 m/s (+0.2 per Menace), surging and easing under its load. It turns slowly (1.6 rad/s) toward clean floor
  in sight 4–20 m off, the nearer the better, avoiding safe places and the arena. It re-picks a target on arrival,
  after 10 s, when stuck for 2.5 s, or once its target has been corrupted.
- **Spreading:** it smears ink over a 2.6 m disc beneath it, toward density 0.9.
- **Tough:** its corruption meter is 100 (+25% per Menace), about ten default shots in the Shallows. It regrows
  slowly (5/s) after 2 s without a hit. Each shot bursts on it. Emptied, its shell pops (see evaporation below) and it
  cleanses a 5 m disc.
- **Body:** one round shell, 1.9 m in radius (`BlotterTuning.ShellRadius`, `Blotter.Touches`), takes her hits and hurts
  on contact; the crab under it is too small to matter. Against rock its footprint is 1.4 m.
- **Contact:** 8 corruption, with the usual hit grace, and a 7 m/s shove away.
- **Corruption bombs:** every 3.5 s its shell pushes an ink ball out of its top. The ball arcs for 1.1 s and lands 3–7 m
  away at random. If her light struck it in the last 3 s, it lands where she stood when it was thrown, if she is
  within 13 m. A dark ring on the ground marks the landing spot and tightens as the ball falls. The splash spreads
  corruption over 2.2 m. If she is in it, she takes 10 corruption and is thrown back at 9 m/s. A dash slips it.
- **Guards:** every ~6 s (less with Menace) there is a 70% chance a murkling buds 2.5–4.5 m from it, up to 2 + Menace
  guards alive (`Murkling.Guarding`).
- **Looks** (`BlightView.Blotters.cs`):
  - **The shell:** a round bun of matte orange-rimmed ink like a bao (or a Bulbasaur's bulb), flat underneath, from
    just off the floor up past her plane. Its soft pleats swirl up into a pinched knot on top. It bobs and rocks on the
    crab's steps, and ink beads off it.
  - **The crab:** lobster red, far too small for its shell. It pokes out of the shell's front with amber-tipped eye
    stalks, one big claw and one small one, and four legs scuttling.
  - **Its bombs:** over the last 0.6 s before one, the shell squashes, swells and twists its knot. Then it springs up
    and pushes the ball out of the knot with a spurt of ink, while the crab keeps walking.
  - **Freed:** the shell pops. The bare crab, its soft pink tail showing, does a double take (a hop, its eyes shooting
    up, a look one way and the other). Then it scuttles off sideways, away from her and round rock, until it is off the
    screen, where it is gone.
  - **Shards:** the corruption shards hang over all open floor, so a Blotter's trail grows them.

## Thumpsquirts (`Thumpsquirt`, `PlaneThumpsquirts.cs`)

A corrupted sea squirt: an upright tube of matte ink (1.6 m tall, 0.84 m across) with two siphons glowing a dim
stolen ember. It stands on the floor where the floor comes near her plane and hangs just under it over deep water.

- **Where:** from Depth 2 (the Sunken Galleon) on: one a level there, 1–2 in Depth 3, 2 from Depth 4, never more than 3.
  Never within 20 m of her start, 18 m apart, never in the arena. It wakes when she is within 16 m and goes back to
  rest past 26 m.
- **Its cycle:** three leaps, a rest, a roll, a rest, and leaps again.
- **A leap:** it contracts into a squat drum, trembling, for 0.6 s (the telegraph), then locks onto where she is (at
  most 12 m off, onto open floor) and springs up stretched thin, flying 0.9 s in a high arc; a dark ring tightens on the
  ground where it will land. In the air it cannot be harmed: her light passes it by. It lands squashed and rebounds
  (0.35 s).
  - **Landing on her:** 14 and a hard shove (12 m/s).
  - **Every landing:** a ring of ink runs out to 4 m at 12 m/s: passing over her it corrupts her for 6 + 2 × Menace
    and shoves her off (8 m/s), a dash slipping through it; it stains cleansed ground it crosses back to 0.35, as a
    Blightroot's pulse does.
  - **Landing on uncorrupted ground** stains 2 m of it toward 0.7.
- **The roll:** it tips onto its rim (0.4 s) and runs on it like a coin after her for 4 s at 4.5 + 0.5 × Menace m/s
  (she cruises at 6), turning at most 2.2 rad/s, so she can sidestep it; it inks a 1 m trail behind it. Rolling into
  her: 10 and a shove. Then it rights itself (0.4 s).
- **The rest:** 2.5 s upright, heaving: the best moment to strike it.
- **Freeing it:** its corruption meter is 90 × (1 + 0.2 × Menace) (about 9 default shots), regrowing 20/s after 1 s
  without a hit; anything but the leap can be struck (her shots, pops, surges, bursts; homing bubbles seek it). Freed,
  its ink pops off it (see evaporation below), cleansing 4 m; it leaves 2–3 shells, and a pale, translucent sea squirt is left settled on the
  floor where it was.

## Inkwardens (`Inkwarden`, `PlaneInkwardens.cs`)

A corrupted sea anemone that shelters its kin as an anemone shelters clownfish: a squat column of matte ink crowned
with ten slowly writhing tentacles tipped violet, a faint ring of ink on the floor round it marking how far it guards.

- **Where:** from Depth 2 (the Sunken Galleon) one sits beside each Blightroot (3–5 m from it). From Depth 3 on, 1 +
  Menace more roam the level (far from her start, 12 m apart, never in the arena), at most 6 in all. A roamer drifts
  at 0.6 m/s toward where most of its kin gather within 25 m (looked for every 2 s) and settles beside them.
- **The ward:** every corrupted creature within 8 m of an Inkwarden — murklings (lesser ones and primes too), Blotters,
  Blightroots, Thumpsquirts — takes no harm from anything: her shots, pops, bursts, the Firefly Squid's surge, a dash's
  burst (a clinging murkling is still thrown off). Each hit turned aside shows an ink tether from every warden guarding
  the creature, wavering out of its crown, held while the hits come and fading 0.6 s after; the creature shimmers
  violet, the hit counts 0 and a dull knock sounds. Inkwardens never guard each other and bosses are never guarded, so
  the wardens are always the way in. The ground round them can still be cleansed.
- **Passive:** it never attacks and does not hurt to touch.
- **Freeing it:** its corruption meter is 60 × (1 + 0.2 × Menace) (about 6 default shots), regrowing 10/s after 1 s
  without a hit. Freed, its tethers snap and its ink pops off it (see evaporation below); it cleanses 3 m and leaves
  1–2 shells, and a healthy
  pale-pink anemone is left on the floor.

## Lashjaws (`Lashjaw`, `PlaneLashjaws.cs`)

A corrupted moray lodged in a hole in the reef wall, its tongue a lash of ink: a matte ink head with one ember eye
poking out of the rock at her height; it never moves.

- **Where:** from Depth 3 (the Coral Carnival) on: 1–2 a level there, 2–3 deeper, at most 3. On a true wall (rock
  rising over her plane behind it) facing the camera or sideways (never a north face the camera cannot see past), never
  under an arch or overhang, with open water before it; 25 m from her start, 20 m apart, 10 m clear of places, never by
  the arena.
- **What it watches:** a 140° fan before it, 10 m deep, wherever it has line of sight.
- **The lash:** its jaws gape and its eye flares for 0.6 s (the telegraph), then the tongue flies straight at where she
  (or a fish) was, at 25 m/s as far as 10 m: it can be dodged, and a dash slips it. A miss draws back in 0.5 s, and it
  waits 2 s before the next.
- **Caught:** it reels her toward its jaws at 3.5 m/s. Swimming against the pull slows it (by 0.45 per m/s of her pace
  against it, never below 1 m/s) and a dash against it holds it a moment, but nothing she does breaks the hold; she
  swims freely across it and may shoot. Only one Lashjaw holds her at a time.
- **The bite:** reeled all the way in (within a metre of its jaws), she is bitten for 25 and thrown clear; it chews for
  4 s and cannot lash.
- **Breaking free:** only her light on its head counts (the tongue cannot be harmed). Its meter is 50 × (1 + 0.2 ×
  Menace) (about 5 default shots), regrowing 15/s after 1.5 s without a hit. Emptied the first time, it lets go of her
  (or its fish) and is stunned for 3 s (jaw slack, eye dim), its meter full again; emptied the second time, it is
  freed. Until then it hunts on. An Inkwarden can ward it like any kin.
- **Fish:** with her out of its fan (or held by another), it lashes the nearest reef fish in it, reels it in and
  swallows it, chewing 4 s: her chance to strike.
- **Freed:** it pops (see evaporation below), cleansing 3 m, and leaves 3–4 shells; every fish it swallowed swims back out alive, and the
  healthy moray draws back into its hole.

## Dens (`Den`, `PlaneDens.cs`)

Every nave (a chamber off the main route) that holds no place holds a den: a knot of the corruption to clear
(DESIGN-TOPDOWN §4.9 has the rules). Its ground lies thick with ink. A murkling or more waits in it (1 plus 1 per
Menace, drifting back into the nave while she is away), and most dens have a heart: a Blightroot (its guardians bud
round it as ever), a Thumpsquirt, or an Inkwarden warding the murklings (free the warden first). When every creature
of it is freed and no murkling is left in the nave, the battle is over: the whole nave is cleansed at once, and it pays
out shells (4 plus 2 per Menace) and, 40% of the time, a light orb (`DenCleared`; gold light over the nave, a bell).

## Light orbs

With the corruption on there are no pots (`PlaneOptions.Vases` is off), and nothing lies buried under the ink: loot
comes from what she frees, the places and the shop.

**Light orbs** replace hearts everywhere: drops and the shop's top-up stand. Each is a bubble with her light
inside, like her old bubble shots, with a soft halo and a warm light. It washes 12 corruption away. The sim still calls
it `PlaneHeart`. She takes one only when corrupted at all; otherwise it waits.

## Murklings (`Murkling`)

- **Its body is liquid** (`BlightView.Murklings`, `murkling.gdshader`; lesser ones and primes alike): a drop of ink adrift
  like mercury in no gravity. Twelve points round it each stand out of round or in on a spring back to round (its
  surface tension), lightly damped so it rings, pulled toward their neighbours (a dent spreads and smooths), its
  volume kept (what one takes in, the rest push out). The ink lags it: speeding up, its trailing side bulges and its
  front flattens; stopping, it sloshes on; fast, it draws out along its way; still, the water stirs it gently. It is
  painted as her light is: its ink brushed in flowing bands, a ragged dry-brushed violet stroke for its rim, a torn
  silhouette, one soft dab of sheen.
- **Her hits tear it** (`TearMurkling`): the struck side dents deep and rebounds past round; a ragged hole opens there,
  wider for a stronger hit (about 0.25 rad for a plain shot, up to about 0.7 for one that takes all of it; at most
  six, the nearest widened past that), its lip glowing with the ember, its dark inside showing through with the ember
  deep in it; and chunks of its ink are ripped away (1 + 5 × the hit's share of its meter): wobbling blobs flung off
  the struck side that drag to a stop, sink, shed drops and break into a few. **The holes close as its corruption grows
  back:** they hold while its meter waits (its delay), shrink as it refills, and are gone when it is full.
- **Budding:** each living Blightroot's area buds one from dense ground (≥ 0.6), at least 5 m from her, every
  15 s / (1 + 0.6 × Menace), while the level holds ink enough for it. The bud takes 0.8 s. **It is made of ink:**
  over the bud it drinks 3.6 (about a 1.2 m disc of thick ink, the nearest first and further out where it is thin),
  leaving that floor dark and empty, and holds it until freed (see the field above). The ink is sucked into it and it
  rises as the murkling (see the look of a birth above). Guard buds drink the same way. Ambush buds gather a 1.2 m
  disc of dots but drink nothing: they are the trap's own ink.
- **Cap:** 2 + 1.5 × Menace per area. An area cleansed past 80% stops budding for good.
- **The arena** buds every 6 s, but only while its boss fights.
- **Movement:** 2.6 + 0.4 × Menace m/s (she cruises at 6), toward the brightest light within 14 m, her or one of her hanging bubbles (a merged one
  shines brighter than her). This makes bait and herding possible.
- **Its purpose is to corrupt every creature:** while she is more than 14 m off, it hunts the reef's fish instead. It
  goes after the nearest healthy fish within 14 m that it can see (looking again twice a second), keeps about 6 m from
  it and throws at it at its usual rate, led a little to where the fish is swimming. A clownfish tucked into its
  anemone is safe. Once she is within 14 m it turns to her as above.
- **Attack:** a black orb, a piece of itself, thrown when she is within 9 m and in sight, for 7 damage (its radius
  0.1 m). **The telegraph is a slingshot** (0.55 s ÷ (1 + 0.3 × Menace)): it bends back away from its aim into a
  C, its middle hauled back and its tips left reaching toward her, trembling and shivering with the effort, its
  liquid body sloshing back away from its aim with the strain, its aim following her as it draws; it crawls at a quarter of its pace meanwhile. For the last 0.12 s its aim is locked and it
  snaps forward through the opposite arc, the piece swelling out of its front on a narrowing neck. The drop parts
  from it like a drop leaving a surface (its back drawn into a neck that thins and snaps, then a wobble into its
  flying drop, which keeps a slow wobble as it flies) and the murkling, its front flung out at the snap, recoils and
  rings back to round. It flies at 10 + 1.5 × Menace m/s
  and is thrown every ~2.4 s / (1 + 0.25 × Menace). A throw that flies 9 m without hitting anything falls: it sinks
  to the sand over 0.45 s (harmless now, under her plane) and lands with a small splat, staining 0.9 m of the floor
  toward 0.7 density (never in safe places). Struck in the air by her light (a shot or Firefly Squid's surge), it
  bursts like a water balloon: a quick spray of ink droplets and a puff of violet mist, staining nothing and
  cleansing nothing.
- **Contact:** 6 damage, then it clings (at most 3 at once), draining 2.5 HP/s each and dimming her bell. Feeding shows
  as one corruption number a second, summed over it. A dash bursts her light: each clinging murkling is thrown off
  (9 m/s), losing one default shot's worth (10) of its meter, and is dazed for 0.7 s. Then it chases her again. One
  already worn down bursts. The dash shows as a small light burst, not an ink cloud.
- **Killed by light:** its corruption meter is 36 × (1 + 0.2 × Menace), so four default shots in a row burst it in
  the Shallows. It regrows at 14/s (more
  with Menace) after 0.8 s without a hit, and one strong (merged) shot empties it at once. Each shot bursts on it, and
  a piercing one flies on. Passing through a throw halves a shot's light. A murkling burst over cleansed ground drops
  its ember, which blooms a 2.6 m patch of colour.

## Murkling primes (`Murkling.Prime`)

- **Rare:** an area's bud is a prime with a chance of 6% + 4% × Menace, where it has room, she is over 10 m off and
  the level still holds a prime's ink (else it buds a murkling). At most one is alive per area and two bud a level;
  ambushes and guards never bud one.
- **Its bud** takes 2 s and drinks twelve murklings' worth of ink (43.2), about a 3.7 m disc of thick ink and further
  out where it is thin: a great stream of ink sucked in from all round into it.
- **Size:** five times a murkling's: a 2.25 m radius (4.5 m across, against her 0.84 m) to see and to hit; it moves as
  a 1.4 m body, so it squeezes through the passes.
- **Every stat scaled:** its corruption meter is ten times a murkling's (360 × (1 + 0.2 × Menace)); its sense and
  throw range 1.5 times (21 m and 13.5 m); its throws 2.5 times as big and as hard (17.5 damage, radius 0.25 m,
  splashing 2 m on fish, staining 2.25 m where they fall), after a 1 s slingshot wind-up (shorter with Menace). It
  moves at 0.7 times a murkling's pace. It hunts fish as a murkling does.
- **It rams instead of clinging:** touching her deals 15 and shoves her off at 9 m/s.
- **Freed:** it pops in a great shower of its ink, glowing magenta, felt (a deep chime, a shake); its ember blooms a
  6 m patch of colour
  wherever it bursts, and it always leaves 4–6 shells, and a light orb
  30% of the time. It breaks apart as it bursts: five murklings are flung out of it, already out of the ground, and
  fight on. They are made of its ink, a murkling's worth each; the rest of its ink is cleansed.

## Lesser murklings (`PlaneMurkFish.cs`)

The reef's fish (DESIGN-TOPDOWN, Reef fish) are what the murklings corrupt.

- **Struck:** fish never dodge a throw. One that meets a fish bursts there (the airborne splatter) and rolls for every
  healthy fish within 0.8 m of it: in a school that is about 4–6 fish, a lone tang or clownfish only itself. Each turns
  with a chance of 35% + 60% × Menace, at most 90% (35% in Depths 0 and 1, 47% in Depth 2, 83% in Depth 5). One that
  does not turn bolts: a chromis darts off, a tang veers away, a clownfish tucks in. One murkling turns a school of 60
  in about a minute and a half, several faster.
- **Fear:** fish veer off from any murkling (lesser ones too) within 4 m, as they make room for her; clownfish tuck
  into their anemone while one is within 3 m of it.
- **Turning:** over 0.6 s the fish falters and ink creeps over it from the tail as it shrinks; then it is a lesser
  murkling, with a puff of violet mist.
- **The lesser murkling:** a small matte ink bead in the murklings' look, carrying a dim ember in its fish's old colour
  (teal for a chromis, orange for a clownfish, blue for a tang), so it shows what it was. By the fish it was:

  | Fish | Radius | Corruption meter | Drain while clinging |
  |---|---|---|---|
  | Chromis | 0.16 m | 8 | 0.8 HP/s |
  | Clownfish | 0.24 m | 14 | 1.2 HP/s |
  | Blue tang | 0.32 m | 20 | 1.6 HP/s |

  Its meter grows with Menace and regrows as a murkling's does. It never throws. It chases her once she is within
  14 m, at 1.25 × a murkling's speed (about 3.3 m/s, against her 6). Touching her is no hit: it clings and drains, at
  most 8 lesser ones at once (besides the murklings' 3), each dimming her bell a little. Her light, pops, the Firefly
  Squid's surge and a dash's burst work on it as on a murkling, so one default shot or a dash frees a chromis.
- **Swarms:** chromis turned from one school stay one swarm. They flock round their shared middle, about 0.45 m apart,
  and the swarm chases her once she is within 14 m of its middle; otherwise it hangs together where it is. With every
  place on her taken, the rest of the swarm mills round her about 1.4 m out, waiting for one. What is left of the
  healthy school swims on, fleeing them.
- **Freed:** emptied, its ink cracks off in flakes with a little burst of its own colour and a chime, and it is its fish
  again, where it was: a chromis rejoins its school, a clownfish its anemone, a tang its home ground. A freed fish
  cannot be turned again that level. Freed fish drop nothing.

## Corruption meters (`CorruptionMeter`)

Fighting the corrupted is a matter of thresholds and illuminance. Every murkling and Blightroot has a corruption
meter that starts full. Each shot of her light takes its damage (illuminance) off. After a pause without hits the
meter regrows, at a rate set by the creature's kind and the Menace. Emptied, the creature is cleansed: one strong
enough shot does it at once, or keep shooting until the meter runs out.

No meter is drawn. Instead **her light's outline** (the warm-white line round the silhouette that each hit
flashes) **stays** round the creature for as long as its corruption is not full (`corruption_orb`'s `worn`,
`BlightView.SyncOutlines`): thin after the first hit, thicker the less corruption it has left, up to about a fifth of
its radius near empty, and a little brighter. Each hit flashes it brighter for 0.2 s. Regrowing to full, it is gone;
emptied, the creature bursts. A freed octopus swims off without it.

**Knockback** (`PlaneKnock.cs`): each shot of her light shoves the creature it strikes a little along the shot, the
smaller the further: 3 m/s for a 0.5 m body, scaled by 0.5 m over its radius (0.25–1.4 times that), easing off
quickly (about 0.3 m for a murkling, a few centimetres for a Blotter), quick hits adding up to at most 1.5 shoves'
worth, and times her knockback stat. Rock stops it dead. A clinging, budding or leaping creature is not moved, nor
the rooted ones (Blightroots, Lashjaws).

## Queen Clam (the Depth 1 boss)

Redesigned to fit the game (art rules in `CLAUDE.md`): a real giant clam, no face.

- **Look** (`BossView`, `clam_shell.gdshader`): two heavy valves with radial ribs, growth lines and a scalloped lip.
  The corruption drains their colour to ash and settles ink into every groove. The mantle inside is dark. In her
  middle lies a **black pearl** (`corruption_orb`) with a cold glint that stirs while she is open.
- **She does not see Clementine.** She lies with her opening toward the screen (south, the camera's side, so the
  black pearl shows) and never turns. She opens and
  closes her lid and throws pearls from her opening: rings, walls thrown in a random spread around her facing, the
  helix, and in phase 2 the homing royal pearl. The snap still comes if Clementine is close while she is open.
- **Her pearls are corruption:** matte ink orbs. Clementine's light passes through them and cannot clear them.
- **Weak spot:** only the black pearl (0.9 m, `CoreRadius`) can be hurt, and only while she is open (the light flies
  over the raised lid). Shut, or on the shell, shots break harmlessly.
- **Every boss's rule** (`CLAUDE.md`): a boss holds a corrupted pearl (`PlaneBoss.PearlAt`), the source of its
  malice and its only weak spot; defeated, the cleansed pearl is the reward, left exactly where it was attached.
- **Defeat:** the pearl bursts into bright light and stays white. The whole arena is wiped clean of corruption
  (`CorruptionField.Clear`), so the Crack opens as soon as the arena unseals. Her colour floods back. After a short
  shudder she leaps 6.5 m aside in one arc (0.7 s) and lies there wide open. Her cleansed pearl is the reward: it
  stays exactly where it sat in her (over the Crack's middle) as she leaps away from under it, ready to take.
- **The arena is hers alone during the fight:** sealing it sets every murkling (clinging ones too) and Blotter inside
  down just outside. Murklings never bud in it. Murklings and Blotters cannot cross the sealed rim, murkling
  throws stop at it, and a Blotter's bomb bound inside lands just outside.

## The arena: condensation

- **Crust:** the boss carries 5 crust layers, one stripped per 20% of the arena cleansed. Each layer absorbs 17% of a
  bubble's damage. Her ring and wall volleys grow thinner with each layer gone (60% of the full count with no crust).
  Her pearls' reach grows shorter (65% of full life with no crust). Her crust is drawn as matte ink lumps that break
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
- **It heals once a level.** The first visit on which it washes her uses it (a visit while she is already clean does
  not); when she leaves the water it is spent (`SpringSpent`): it heals no more, and it fades away over 2.5 s and is
  gone, from the maps too. So she must plan when to use it: going in early wastes most of it.
- Entering it while corrupted raises `SpringEntered`: a chime. Reaching clean raises
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
| A creature freed (`InkVapour.Pop`) | Never a flash of light. Every corrupted creature freed (a murkling or prime, a Blightroot's mantle, a Blotter's shell, a Thumpsquirt, an Inkwarden, a Lashjaw) pops like a water balloon full of ink. Its skin snaps and the ink it was made of showers out all round: droplets flung out fast, slowing in the water, each glowing in the creature's own colour as it thins (see the kinds' colours under Presentation). As the spray slows, the ink evaporates off it in the floor's dark curls, thinning to violet, with their gold glints. The shower scales with the body: about 40 droplets per metre of radius, 14 at least. Whatever ground it cleanses evaporates as the floor does. |

## Presentation

- **One texture** (`CorruptionTexture`) holds the field: R is density, G is the gold of corruption dying. G is worked
  out from how fast each cell falls and fades over ~1.2 s. It uploads at most 30 times a second, and only while
  something changes. The level's floor and its shards read it.
- **Ink drip** (`corruption_shards.gdshader`, `CorruptionView`): the corruption's only mark on the floor, as if from
  another dimension: ink dripping upward out of the ground, as fine as plankton (drops 4–8 cm across, about 70%
  opaque). From its own spot on corrupted ground (no current moves the spot) each drop wells up out of the floor and
  swells, lifts drawing a neck of ink up behind it that thins, snaps free with a small wobble, then rises buoyantly
  0.6–1.5 m (slow off the mark, quicker, easing) with a small sway and fades near the top; then another wells up, on its
  own loop (3.5–7.5 s, its own phase, so it never pulses together). One to five drops per half-metre cell, more where the
  corruption is thicker. They take no light, shading or fog from the scene and are drawn after the water pass, so
  nothing tints or blurs them; their roundness is their own (a fixed soft key, a cold violet rim, a glint). Each is seen
  out of phase, a fixed chromatic aberration: its red printed a fifth of its radius to the left and its cyan as far to
  the right, so every drop wears a red fringe on its left and a cyan one on its right. Rock in front
  still hides them. None within 2.5 m of a dive's gateway (the shaft). Drawn as camera-facing quads shaped in the shader,
  in 64 chunked MultiMeshes (only those on screen are drawn), laid out off the main thread (a level below while she is
  still above it). Where the ground is cleansed a spot's drops stop coming, and the last of them light up gold as they
  go: only the drops glow, never the ground. A level's drift fades in over 1.2 s when it is shown, and fades out with
  the rest of the level's life as she dives.
- **The floor** (`reef_surface.gdshaderinc`): the ground keeps its own colour under the drops; nothing covers it and it
  never glows. The level below shows its own corruption through the shaft.
- **Corrupted bodies** (`corruption_orb.gdshader`: throws, the crust, the other kinds; murklings and the Blightroot are
  `murkling.gdshader`, painted, the same matte ink) are matte, rubbery ink,
  tacky to look at: rough, with a weak broad sheen and a finely dimpled skin, never polished; a soft rim. And
  **each kind has its own base colour** (its skin under the ink and its glowing rim), shared by its projectiles and its
  rings: murklings stay ink-black with a violet rim, each with its core's colour (a murkling's amber, a prime's
  magenta, a lesser one's its fish's); Blightroots blue (its pole and head under the ink, its rings too); Blotters orange (their
  bombs and landing marks too); Thumpsquirts acid green (their landing rings and marks too); Inkwardens violet (their
  tethers too); Lashjaws crimson (their tongues too).
- **Their light** (`BlightView.Lights.cs`): each casts a little light in its core's or base colour (murklings and
  their throws their core's), so the creatures show in the dark depths: a pool of 24 small shadowless lights goes to
  the ones nearest her, stronger the darker the depth (× Lantern^0.8, §5.1 of DESIGN-TOPDOWN), always well under her
  own lantern.
- Other shaders:
  - `ink_ring.gdshader`: pulses, buds, bursts.
- **HUD** (`CleanseMeter`): the level cleansed, with the 50% and 75% tipping points marked. On boss levels it also
  shows the arena, with the Crack's 60% mark and the crust as pips.
- **Maps:** they show neither the corruption nor any corrupted creature; only the light springs, of its works.

## Not yet

- The arena's crust lumps have no sounds of their own; the war's sounds reuse the existing set.
- Murklings stay out of the Sea-pedia, which lists pearls and creatures from `SeaPediaCard.Creatures` only.
