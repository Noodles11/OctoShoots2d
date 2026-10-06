# Depth 1 — Sunlit Shallows: creatures and bosses

> The eight creatures are built (see §6); the three bosses are not yet built here; Queen Clam is built for the top-down game (DESIGN-TOPDOWN §12.2). Numbers are first guesses to be tuned in
> play: every creature's numbers are in `data/creatures.json` and in the F1 panel. Builds on
> [`DESIGN-3D.md`](DESIGN-3D.md) (fairness §7.2, open sea §6) and the 2D roster ([`DESIGN-2D.md`](DESIGN-2D.md) §12.1, §12.2, §31).

## 1. Principles

- **Depth 1 is cute and readable, with an ache underneath.** Round eyes, bright colours, slow, long telegraphs, small groups; most of the life around is healthy, but the octopus dens are empty. Menace is 0, so nothing is relentless yet (DESIGN-2D §1, §5.3).
- **Four movement classes** (DESIGN-3D §7.1), at least one creature each. Eight creatures in all, each with a different question for the player.
- **Every attack is telegraphed** (§7.2): ≥ 0.45 s, normally **0.8 s** at Depth 1, with a pose or glow cue, a positional sound and a screen-edge marker if it is out of view. At most 3 creatures attack from outside the view at once.
- **Fair damage.** Clementine has 100 HP. Numbers follow DESIGN-2D §23 for Depth 1.
- **Bubbles are slow and physical**, so every creature has a clear window where it is easy to hit: after an attack, mid-turn, or when it exposes itself.
- **Habitats, not rooms.** In an open sea, creatures live where their body suits: on the seabed, on reef walls, in holes, in open water, around anemones. They sleep in **dens** until Clementine is near (only creatures within ~80 m are simulated).

## 2. Noticing: creatures see farther than you do

The mist hides the sea past ~14–40 m (DESIGN-3D §9). **Creatures are not blinded by it.** Each type has a **notice range** that depends on its senses; the keenest can spot Clementine from inside the mist.

| Creature | Notice | How it notices | Gives up at |
|---|---|---|---|
| Barracuda | **30 m** | Sharp sight along open water (needs line of sight) | 90 m |
| Pufferling | **22.5 m** | Sight | 65 m |
| Crabby | **20 m** within 6 m of a surface, else 10 m | Feels vibrations through the seabed and reef | 55 m |
| Spanish Dancer | 15 m | Poor sight, but tastes the water (no line of sight needed) | 45 m |
| Jelly swarm | 10 m | Feels the water move | 30 m |
| Ninja | wakes at 11 m, attacks at 15 m once awake | Short on purpose: it is an ambush | goes back to sleep at 45 m |
| Sea Urchin | 8 m | Shadow and touch (needs line of sight) | 22 m |
| Moray | 5 m | Waits in its hole for something to pass | 14 m |

**What noticing looks like.** A creature that notices Clementine **startles for 0.6 s** (a jolt, a call you can hear in 3D), then acts. Creatures hunting you beyond the minimap range show as **arrows on its rim**, so a barracuda you cannot see is still announced.

**Sneaking** (to confirm): notice ranges are **halved while Clementine is barely moving** (no keys, just drifting) and **doubled for 2 s after she shoots, dashes or detonates a bomb**. An Ink Cloud hides her completely. Staying behind rock breaks line-of-sight senses.

**Leash.** A creature that loses her (beyond "gives up at", or hidden) swims back to its den and goes to sleep.

## 3. The creatures

HP assumes 3.5 damage per bubble. Damage is per hit; Clementine is then invulnerable for 1 s.

### Overview

| Creature | Class | HP | Damage | Role |
|---|---|---|---|---|
| **Clownfish ninja** (built) | Swimmer | 14 | 8 star | Ambusher hiding in a school |
| **Spanish Dancer** | Swimmer, slow | 28 | 10 | Tank; teaches dodging rings |
| **Pufferling** | Swimmer | 21 | 14 | Area denial; shoot it before it swells |
| **Barracuda** | Swimmer, fast | 18 | 16 | Charger; teaches the ink dash |
| **Jelly swarm** (moon jellies) | Swimmer, group | 4 each × 6–9 | 6 | Crowd; likes area damage |
| **Sea Urchin** | Clinger | 28 | 12 | Turret on any surface; teaches reading gaps |
| **Crabby** | Walker | 24 | 12 | Punishes hugging the seabed |
| **Moray** | Burrower | 21 | 18 | Guards caves; punishes careless passes |

### Spanish Dancer — slow swimmer
A red-orange sea slug with a white-edged ruffled mantle and two feathery gills, undulating through the water. Gentle, tanky, and hard to miss.
- **Behaviour:** drifts near the seabed and reef walls. On noticing, glides toward Clementine at 2 m/s.
- **Attack — Flare Burst:** within 5 m it flares its ruffles wide and glows (0.9 s, soft chime), then releases a **ring of 6 slow spores** (6 m/s, 10 damage) horizontally around itself. Gaps between spores are wide; leave the ring's plane (go up or down) or step through a gap.
- **Contact** also hurts (10). After the burst it spins for 1.2 s: free hits.
- **Why:** the first time the player meets a ring pattern, from a slow, forgiving enemy.

### Pufferling — swimmer
A round yellow puffer with big eyes and tiny fins, in groups of 1–3 near reef formations.
- **Behaviour:** bobs about; on noticing, chases at 3 m/s.
- **Attack — Swell:** within 4 m it **inflates** (0.8 s, squeaky sound) from 1× to 2.4× its size, spines out. Inflated, it keeps chasing at 4 m/s for 1.6 s (spines hurt on touch, 14), then **bursts into 12 spines** in all directions (8 m/s, 8 damage each).
- **Counterplay:** pop it while small; or keep distance so it swells and bursts at nothing; or dash through the burst. Shooting it while swollen makes it burst immediately.

### Barracuda — fast swimmer
A silver barracuda with a long jaw and a dark stripe. Patrols open water in the deeper half of the reef, alone or in pairs.
- **Behaviour:** on noticing (30 m, see §2) it **circles Clementine at 12–18 m** for 2–3 s, sizing her up.
- **Attack — Charge:** it points its nose at her, body quivering, fins flared, a rising hiss (**0.8 s**), then dashes **in a straight line at 16 m/s** (25 m long) for 16 damage. It overshoots, then takes ~1.5 s to turn around: a flank window. Pairs take turns.
- **Why:** the clearest test for the **ink dash**, whose 0.32 s of invulnerability is made to go through a charge.

### Jelly swarm — group swimmer
Six to nine translucent moon jellies, glowing faintly, pulsing as they rise and fall. They show up well in the mist.
- **Behaviour:** drift toward Clementine at 1.6 m/s with erratic bobbing. Every ~3 s each pulses (the bell visibly contracts, 0.5 s) and lunges 4 m.
- **Damage:** 6 on contact; they have no other attack.
- **Counterplay:** chain lightning, explosions and multi-bubble throws; or outrun them.
- **Drop:** a defeated jelly sometimes leaves a **glow jelly**, which recharges the active item.

### Sea Urchin — clinger
A black urchin with long spines tipped in violet. Sticks to the seabed, **reef walls, cave ceilings**: anywhere. Also grows as harmless-looking **beds** of 3–5 small urchins on the seabed that never shoot but hurt to touch (8).
- **Behaviour:** does not move. Notices Clementine in line of sight at 8 m.
- **Attack — Spine Bloom:** the spines stand and rattle (**0.9 s**), then **12 spines** fly out in all directions (7 m/s, range 14 m, 12 damage), spread over a sphere, leaving gaps of ~40°. The closer she is, the narrower the gaps, so staying in the gaps near the urchin is possible but tight. Every 3.5 s.
- **Why:** a turret you can read and plan around, on every surface.

### Crabby — walker
A red-orange crab with one oversized claw.
- **Behaviour:** scuttles over the seabed and slopes (not walls) at 3.5 m/s. Notices Clementine through the seabed's vibrations when she is within 6 m of a surface (20 m), otherwise by sight (10 m).
- **Attack — Leap:** when she hovers within 6 m above it, it crouches (**0.7 s**), then leaps up to 7 m with its claw out (12 damage). It also snaps at anything within 1.5 m beside it. Airborne, it sinks back slowly: 2 s of free hits.
- **Counterplay:** keep above 8 m, or bait the leap and shoot it as it falls. It cannot reach open water.
- **Tech note:** walking is local steering along the terrain surface, not a full navmesh.

### Moray — burrower
A green-and-yellow moray in a dark hole in rock (about 1 m wide, framed by anemone-like growth, with a glint of eyes).
- **Behaviour:** sits in its hole. Notices Clementine within 5 m.
- **Attack — Lunge:** the head sways out 1 m, eyes glint, jaw gapes (**0.8 s**, hiss), then it **shoots out 6 m at 14 m/s** and bites (18). It stays exposed for 1 s after a miss (free hits), then retracts. Cooldown 3 s.
- **Placement:** guards cave mouths and the odd treasure. Holes are visible at range, so the danger is readable and avoidable by keeping 7 m clear.

### Clownfish ninja
As built (DESIGN-3D §7.5). Wakes at 11 m, attacks at 15 m once awake, 0.5 s wind-up because it is meant to feel like a surprise, and its star is slow enough to dodge.

### Left out of Depth 1
**Sand Flounder** (a second burrower, hides in sand) and **Splitter Slime / Salp Chain** can come at Depth 2. The **Lanternfish** moves to Depth 5, where the 2D roster puts it.

## 4. Where they live

Per reef (224 m, scaled by size); each group is a den:

| Creature | Dens | Where |
|---|---|---|
| Ninja nests | 7 | Seabed, ≥ 50 m from the start |
| Spanish Dancer | ~10 | Seabed and reef walls |
| Pufferling | ~8 groups of 1–3 | Near formations, mid-depth |
| Jelly swarm | ~5 | Open water, mid and upper |
| Sea Urchin | ~25 + 10 beds | Reef walls, cave interiors, seabed |
| Crabby | ~12 | Seabed |
| Moray | ~8 | Rock holes, cave mouths |
| Barracuda | 3 groups of 1–2 | **Deeper half only**, open water |

**Zones.** The shallow corner around the start has only jellies, urchins and dancers. The middle adds pufferlings, crabs and ninjas. The deep half adds barracuda and morays, and the boss reef is ringed by urchins and morays.

## 5. Depth 1 bosses

Each depth has three reefs, each ending in a boss; the three Depth 1 bosses are **Old Gus**, **Big Barnacle Bill** and **Queen Clam** (DESIGN-2D §12.2, §31). Depth 1 bosses are goofy.

**Shared rules**
- **Arena:** the 35 m sphere on the boss reef with the Crack in its floor, reached by one tunnel that is sealed by a strong inflowing current until the boss falls.
- **Intro:** a third-person cut (DESIGN-3D §3.3) as the boss rises out of the Crack or drops in; its colours are washed out ("drained") and return when it falls.
- **Phases:** two, at 50% HP, with a 1.5 s stagger between them. Depth 1 boss contact damage is 18 and boss shots 12.
- **On defeat:** the Crack opens, the reward pearl (boss pool) and a heart container float down.
- **Pattern language in 3D:** rings, spheres with a hole, sweeping planes: readable from inside the arena.

### Old Gus the Grouper — chaser (HP 340)
A huge, mossy, grumpy grouper, 9 m long, with a big lower lip, a barnacle on his forehead and one cloudy eye.
- **Phase 1 — Cruise and gulp.** Lazily circles the arena (3 m/s) until he stops and faces her: gills flare, the light dims and a low drone builds (**1.0 s**). He opens his mouth and **sucks** in a 20 m cone for 2.5 s, pulling Clementine, pickups and bombs toward him at up to 4 m/s. She can swim out sideways or dash across it; if she reaches his mouth: bite for 22. Then his cheeks puff (**0.7 s**) and he **spits a cone of 20 pebbles** (9 m/s, 12 each, 40° wide).
- **Weak spot:** the open mouth takes **×2 damage** while sucking. A **bomb dropped in the suction is swallowed**: 60 damage and he burps, stunned for 2 s.
- **Phase 2 — Hangry.** He backs to a wall, shudders (**1.0 s**), then **rams** at 15 m/s along a straight line to where she was, crashing into the wall: dazed for 2.5 s (gills glow, **×1.5 damage**). Gulps come faster, and the spit becomes two cones.
- **Goofy:** his stomach rumbles; he burps up an old boot and a tin can.

### Big Barnacle Bill — clinger with minions (HP 300)
A stout colony of barnacles shaped like a sailor, wearing a limpet cap, one huge googly eye and a pipe that puffs bubbles. He clings to the **dome of the arena**, crawling slowly.
- **Phase 1 — Larvae.** Spits **five barnacle larvae** (10 m/s, 8 damage) that stick to the walls and floor and grow in 6 s into **pods** (HP 8) that each fire one slow pellet every 4 s (up to 8 alive; pop them early). **Shell clap:** his plates open wide (**1.0 s**, creaking), then clap: a **horizontal shockwave ring** expands from him at one of two heights. Pass above or below it. After the clap his soft body is exposed for 1.5 s (**×2 damage**).
- **Phase 2 — Overboard.** He lets go and **drops to the floor** (shadow and vibration warning, 1.2 s), cracking a shock ring where he lands. On the floor he spins his shell edges: a **sweeping horizontal plane** rotating around him at a fixed height (gap above and below). He keeps spitting larvae but no longer clings, so he can be circled.
- **Goofy:** a muffled "Arrr!" every time he claps.

### Queen Clam — pattern boss (HP 280)
A huge giant clam, 8 m wide, sitting on the Crack, with a crown of pearls, a rippling blue-green mantle and a very grumpy face. She turns to face Clementine.
- **Vulnerability:** damaged only while **open**. Closed, bubbles pop on the shell; bombs still do 40.
- **Phase 1 — Pearl showers.** Opens (**0.9 s**: shell creak, her heart-pearl glows), then fires four volleys, one every 1.2 s, alternating:
  - **Pearl rings:** a tilted ring of 14 pearls (7 m/s, 12 damage) with a 3-pearl gap to fly through.
  - **Pearl spheres:** 20 pearls in a sphere with one big hole; aim for it.
  Then she shuts for 3 s. Every pearl **pops with one bubble**, so shooting a ring carves its own gap.
- **Phase 2 — Royal tantrum.** Adds a **rotating helix** (three arms of pearls), a slow **royal pearl** that homes on Clementine (pop it with two bubbles; hit: 14 and a 2 s slow), and a **snap** if Clementine is within 7 m (**0.7 s** warning, 18 damage).
- **Goofy:** a bubble raspberry; her pearl necklace jiggles.

### Why these three
| | Gus | Bill | Clam |
|---|---|---|---|
| Movement | Chases, charges | Crawls on the dome, then the floor | Stationary |
| Teaches | Dash, swimming sideways, bombs as weapons | Vertical dodging, killing adds | Reading rings and spheres, shooting bubbles |
| Weak spot | Open mouth, dazed gills | Soft body after the clap | Open shell |

## 6. How it is built

- **Data.** `data/creatures.json` holds each creature's numbers (health, size, speed, senses, notice and give-up ranges, damage, attack range, telegraph, cooldown, recover, charge or lunge speed and length, shots); the F1 panel has a section per creature. A telegraph under 0.45 s is rejected when the file loads.
- **Sim.** `World.Creatures.cs` holds the shared brain (notice, startle, hunt, leash, sneaking) and one small state machine per creature: Idle → Alert (0.6 s) → Hunt → Telegraph → Attack → Recover. Pairs and packs take turns, and the limit of 3 attackers outside the view applies.
- **Pufferling.** Swells over the telegraph (1× to 2.4×), chases at 4 m/s for 1.6 s, then bursts into 12 spines; any hit while swollen bursts it at once.
- **Barracuda.** Circles at 12–20 m, then charges in a locked straight line (16 m/s, 25 m); the charge never steers.
- **Jelly swarm.** 6–9 per den; each pulses and lunges 4 m about every 3 s; defeated jellies sometimes drop a glow jelly.
- **Sea Urchin.** Beds are 3–5 small urchins (12 HP, 8 damage on touch, never shoot). The bloom is 12 spines on a cap around the surface normal.
- **Crabby.** Walks along the surface (local steering, gravity, slope limit), leaps up to 7 m, sinks slowly on the way down.
- **Moray.** The head moves on a line out of its hole (the body is a stretched tube); it lunges 6 m at 14 m/s and stays out for 1 s.
- **Views.** Procedural meshes (`CreatureMeshes`) and one shader (`creature.gdshader`, plus a translucent one for the jellies) driven from the sim state, so every wind-up is visible: ruffles flare, the puffer swells, the barracuda quivers, spines rattle, the crab crouches, the jaw gapes. Views exist only within about 110 m.
- **Debug.** F1 has a button per creature that swims to the next den of that kind; `--den=Pufferling[,index[,bed]]` does the same from the command line.

## 7. Decisions

1. **Roster:** the eight above (Sand Flounder and the Splitter Slime or Salp Chain wait for Depth 2).
2. **Noticing:** ranges as in §2 with the sneaking rule (halved while drifting, doubled for 2 s after shooting, dashing or bombing); all notice ranges are half the first proposal; the barracuda lives only in the deeper half.
3. **Startle cue:** a 0.6 s jolt, a "!" over the creature, a call heard from its direction and a rim arrow on the minimap.
4. **Boss order:** seeded per reef, so the three reefs of a depth meet Old Gus, Big Barnacle Bill and Queen Clam in an order set by the run seed.
5. **Boss gimmicks:** as in §5 (bomb into Gus's mouth, popping Queen Clam's pearls with bubbles, killing Bill's pods).
6. **Tone:** goofy bosses, as in §5.

## 8. Corruption and freeing (proposal)

> **Proposal for review.** Nothing in this section is built yet.

Every hostile creature is **corrupted** by the Leak (DESIGN-2D §1). The same species also lives on the reef
**healthy**: natural colours, peaceful, harmless and not a target, like the neutral clownfish around a ninja.
A corrupted creature is told apart by two things: **tank colours** and **a piece of the tank** stuck to it.
Defeating it **frees** it instead of killing it.

### 8.1 How corruption looks

- **Tank colours.** Corrupted creatures wear the colours of a pet-shop aquarium: the fluorescent shades of
  real dyed and gene-edited aquarium fish (GloFish: electric green, starfire red, cosmic blue, galactic
  purple, sunburst orange, moonrise pink), in patterns that don't belong to their species: stripes on a
  spotted fish, spots on a striped one. The colours have a glossy, plastic sheen and a faint, slow
  **shimmer of warm light** (the Leak) that shows through the mist, so a threat reads from afar.
- **A piece of the tank.** Each species carries one aquarium object, as the ninja wears its headband:
  something a player learns to spot at a glance.
- **Healthy creatures** look exactly as now (natural colours) and carry nothing.

| Creature | Corrupted colours | Piece of the tank (the tell) | Healthy twin behaviour |
|---|---|---|---|
| Clownfish ninja | Stripes in electric green and purple (instead of orange and white) | The ninja headband (as now), now with a **price tag** fluttering from it | Neutral clownfish in the school (as now) |
| Spanish Dancer | Hot pink with an electric-blue ruffle | Bright **blue aquarium gravel** stuck along its back | Drifts over the seabed; flares its ruffles when Clementine is near, harmless |
| Pufferling | Starfire red with lime spots | A small **plastic fish-shop tag** clipped to its tail fin | Small groups bobbing near rock; puffs up a little when startled, harmless |
| Barracuda | Cosmic-blue sheen with a neon stripe | A strand of **plastic aquarium plant** trailing from its jaw | Hangs in open water, turns to watch her, never charges |
| Moon jelly swarm | Bells cycling slowly through aquarium-LED colours | The **colour cycling** itself (healthy jellies are pale and steady) | Drifts, pulses, harmless |
| Sea Urchin | Spines glossy violet with acid-green tips | A **plastic castle shard** wedged among the spines | Same urchins in natural black; still prick on touch, never shoot |
| Crabby | Electric blue shell (like a rare blue lobster) | A **bottle cap** worn on its back (decorator-crab style) | Scuttles about, waves its claw, never leaps |
| Moray | Neon yellow-green with magenta mottling | Its hole is framed by **plastic plant leaves** instead of growth | Peeks out of its hole and watches, never lunges |

### 8.2 Freeing

When a corrupted creature's health runs out:

1. It stops and shudders (0.3 s) as the tank colours wash out of it.
2. A **"poof"**: a burst of bright bubbles around it with a soft popping chime. The piece of the tank drops
   and sinks (the gravel scatters, the tag spins away, the bottle cap rolls).
3. Its **true colours** come back, and it drops its **loot** (as now).
4. It carries on **healthy**: the fish swims off, the jellies drift up, the urchin's spines lie down, the
   crab scuttles away, the moray slides back into its hole. It is now part of the reef's healthy life: not
   a target, harmless, and it stays healthy for the rest of the run.

Freed creatures replace today's death burst, sound and despawn. Bosses free the same way, at a bigger scale
(the boss's colour floods back, §5).

### 8.3 Healthy life on the reef

- **Who's around.** Besides the freed ones, every reef has healthy creatures of each species from the
  start. At Depth 1 about **two healthy for each corrupted** one; dens mix both (a pufferling group of
  three might be one corrupted and two healthy). The share of corrupted creatures rises with depth, so the
  reef feels sadder the deeper she goes (DESIGN-2D §5.3): roughly 2 : 1 at Depth 1, 1 : 1 at Depth 3,
  1 : 3 at Depth 6.
- **Not targets.** Bubbles pass through healthy creatures, aim assist ignores them, they never show on the
  minimap and they never attack. Like the neutral clownfish, they keep clear of Clementine's bubbles.
- **Same models, new look.** Every creature keeps its current mesh and behaviour; corruption is a second
  colour scheme, the tank piece and the shimmer, all switched in the creature shader.
- **Cost.** Healthy creatures run a cheap idle-and-flee behaviour and only within ~60 m; beyond that they
  are not simulated (as sleeping dens are now).

### 8.4 Open questions

1. **Ratios:** about 2 healthy to 1 corrupted at Depth 1, falling to 1 : 3 at Depth 6: right feel?
2. **Healthy urchins:** keep them prickly on touch (realistic, a small hazard) or fully harmless?
3. **Freed creatures:** should any of them ever help Clementine (for example a freed barracuda chasing her
   attackers for a few seconds), or stay purely scenery?
4. **The tank pieces:** happy with the list above (gravel, price tag, plastic plant, castle shard, bottle cap,
   plastic leaves, LED colour cycling)?
