# Drippus Prime: the brood boss

> **Built** (the decisions are in §8). The rules now live in CORRUPTION.md ("Drippus Prime") and DESIGN-TOPDOWN
> §12.2; this page keeps the design and its reasoning. Drippus Prime is a murkling prime grown huge, a boss made of ink.
> It drops broods of murklings, and every birth costs it some of its own body, so the more it summons the smaller and
> weaker it gets. It throws patterns of ink drops, lobs bursts of bombs, and bounces her off when she comes too close.
>
> The numbers are first guesses for play. §7 lists where it bends the generator's rules and the Theme Bible's.

## 1. What it is

- **Who:** a murkling prime that never stopped drinking. It has gathered most of a level's ink into one body.
- **The fantasy:** a mother of murklings that spends herself. Every child she sends out is a piece of her. Left to
  brood, she fills the arena with murklings, but she wears herself thin doing it.
- **How the fight plays:** dodge its patterns and bombs, keep its brood in check, and stay out of bounce reach. Every
  brood makes it smaller and easier to free.

## 2. The body

The murklings' liquid body (`murkling.gdshader`, CORRUPTION.md "Murklings"), made huge.

- **Size:** 7 m across at full mass (a prime is 4.5 m, Queen Clam 5 m, her bell 0.84 m). It is solid to her at its
  skin where it meets her plane (95% of its radius).
- **Its surface:** a murkling's twelve spring points. It keeps everything a murkling's surface does: it rings when
  struck and sloshes when it stops.
- **Its look:** the murklings' painted ink with flowing bands, a ragged violet rim and one dab of sheen. Its ember is a
  deep magenta, like a prime's.
- **The drip:** drops well up all over it and lift off upward, so it carries its own weather (THEME-BIBLE §6.4, tell
  3). The drip is thickest where it is about to bud.
- **The pearl:** a corrupted pearl sits at its heart, the blackest point with a cold glint, deep in the ember. The
  pearl shows only through the holes her hits tear.

## 3. Mass: every birth costs it

- **Mass:** it starts with 30 murklings' worth of ink.
- **Cost of a birth:** each murkling it births takes away one murkling's worth of mass and **25 of its meter**, about
  2.5 default shots. The cost is the same whatever its meter is, so each brood is a clear step toward freeing it.
- **Size:** its area follows its mass, so its radius is √(mass ÷ 30) of full. After a birth it eases to the new size
  over 0.4 s with a slosh. It shrinks; it never pops a piece off.
- **Floor:** it stops birthing at 12 murklings' worth, which is 63% of its full radius, about 4.4 m across. That
  allows at most 18 births, worth 450 of its meter.
- **Speed:** smaller is quicker. Its pace rises from 1× at full to 1.5× at the floor, and its throws stay the same.
- **Hits don't shrink it:** her hits tear holes and rip chunks off, as on a murkling, but only births take mass.

## 4. The fight

It uses the same cycle machine as every boss (BOSS-GENERATOR §7): rest, telegraph, act, window.

### 4.1 Its attacks

| Attack | The tell | What it does |
|---|---|---|
| **Brood** (signature) | 0.9 s: 2–4 bulges swell on its rim, the drip thick over them, its whole body drawing in. | Each bulge stretches on a neck and snaps off as a regular murkling, which swims out at once (the drop-parting look of its throws, writ large). Each birth costs mass (§3). It births 2, then 3, then 4 a brood by phase, never past the live brood cap: 3 + 2 × Menace. With the brood full, it picks another attack. |
| **Spray ring** | 0.7 s: it pulls in round and trembles. | A ring of 18 ink drops flung out all round, always with a gap at least 3 drops wide. |
| **Slingshot fan** | The murkling slingshot, writ large: it bends back into a C away from her, its aim following her, locked for the last 0.15 s. | A fan of 5 big drops at her (17.5 damage each, a prime's throw). |
| **Spiral** (from phase 2) | It starts to turn, its rim rippling. | 3 arms of drops spiral out as it spins for 2.5 s (the Helix). |
| **Bomb burst** | 0.8 s: its top swells, then it heaves. | It lobs 3, 5, then 7 ink bombs by phase (the bosses' arc bombs, `CorruptionBomb`). Each bomb's shadow marks where it will land, around her and ahead of where she is going, and it bursts in a ring of ink that stains the floor. |

All of it is ink her light cannot clear. Like a murkling's throw, a drop that flies its full range falls and stains
the floor where it lands.

### 4.2 The bounce: too close is a mistake

- **The tell:** when she comes within 1.2 m of its skin, the side facing her dimples inward for 0.3 s, like a drum
  skin drawing back. A dash gets her out in time.
- **The spring:** that side springs out and flings her away at 12 m/s, for 10 damage (with Menace, like its other
  damage). She tumbles out of control for 0.35 s.
- **Touching it** bounces her at once, with no tell.
- **Cooldown:** 1.5 s before it can bounce again.
- **How it looks:**
  - **Its skin:** it dents where she struck, springs out past round and rings, the liquid surface it already has.
  - **Her:** her bell squashes flat against it for a moment, then stretches along her flight and spins once as she
    tumbles away.
- **Feel:** a deep, rubbery "boing", a thump in the controller and a small shake.
- **It replaces the shared boss contact** (1.5× damage and a 9 m/s shove).

### 4.3 How her light frees it

- **Its whole body takes her light,** as a murkling's and a prime's do. Every shot counts against its meter.
- **Tears:** her hits tear holes in it as on a murkling. A shot that goes through an open tear and reaches the pearl
  counts double. The holes close over about 1.5 s.
- **No regrowth:** its meter never grows back. Only births take it down without her.
- **Meter:** 1100 at Depth 1, growing with Menace like every boss's (`BossRoster`). For comparison, Queen Clam has
  800, but her shell only takes light in her windows.

### 4.4 Phases

Phases count everything it has lost from its starting meter, her shots and its own births alike, so brooding pushes
it toward its end.

| Phase | When | What changes |
|---|---|---|
| 1 | At the start | Brood 2, spray ring, slingshot fan, bomb burst ×3. |
| 2 | 35% lost | Brood 3; the spiral joins; bomb burst ×5. |
| 3 | 70% lost | Brood 4; rests are shorter (×0.75); bomb burst ×7. |

Each phase change staggers it for 1.5 s, as other bosses do: it sags and sloshes.

### 4.5 Movement

- It drifts after her across the arena at 1.2 m/s at full size, faster as it shrinks (§3).
- It stops 4 m off its skin from her, keeps 3 m inside the rim, and never leaves the arena.
- While it births or throws it slows to a quarter of its pace, as a murkling does while winding up.

## 5. The arena

- **The arena is closed:** it bars the arena as Queen Clam does (`Blockade`), so its brood stays in the fight with
  her.
- **No arena budding:** the arena never buds; its brood are its only murklings.
- **Its entrance:** it rises from the ink instead of dropping like Queen Clam. After the cut-in, the arena floor's
  ink streams in from every side to one point, where it swells up as Drippus over 2.5 s. This is a prime's bud writ
  large. Where it drank, the floor is left empty and dark.
- **The intro cut-in** (DESIGN-TOPDOWN §6.4): it jiggles in like a water balloon. A drop plips off its bottom and
  wriggles into a tiny murkling, which it pats proudly. At Depth 1 it is round and gumdrop-soft; deeper, its rim is
  ragged and its ember burns colder.

## 6. Being freed

- **The burst:** it shudders for 0.3 s and its pearl bursts into white light, staying where it sat.
- **Its ink:** it pops in the biggest shower of ink in the game, magenta turning gold drop by drop. It does not
  break into murklings as a prime does. Any of its brood still alive pop with it, since they were its ink.
- **What swims out:** the school of reef fish it took into itself, about 20 chromis and tangs in their true colours.
  They scatter, then swim slowly off and are gone once off screen (THEME-BIBLE §6.5). This is its true form, the
  relief of the moment.
- **The arena:** colour floods back over the arena and its corruption is cleared, as with Queen Clam. The loot is
  Queen Clam's.

## 7. Where it bends the rules (to approve)

- **BOSS-GENERATOR §0 and §6:** "The pearl is its only weak spot." Here the whole body takes her light, as every
  murkling's does, and the pearl only counts double through tears. This is a creature of ink: her light tearing it
  is its whole language. Alternatively, it could keep the rule: shots break on it harmlessly except in a window
  (§8, question 2).
- **THEME-BIBLE §6.4:** "corruption shows as mutation of a creature." Drippus has no animal under it to mutate. It is
  the murklings' own kind, which the game already has. Its true form is the fish it swallowed (§6). That fits the
  murklings' purpose: their purpose is to corrupt the reef's fish.
- **THEME-BIBLE:** no depth's boss list names it. The Bible says those lists "set each depth's tone; they are not
  fixed rosters". The name is storybook, as the Bible's naming rule asks.

## 8. Decisions

1. **Where it appears:** every depth's boss is Drippus Prime, in the adventure too. Bringing it to the adventure
   brought its boss arenas back there: on a boss level the way down opens once every vent is sealed and the boss is
   freed. Queen Clam stays only as the generator's reference roll.
2. **What frees it:** its whole body takes her light, with double damage through tears (§4.3).
3. **Taking its brood back:** no. Births only ever weaken it.
4. **What swims out when it is freed:** the fish it swallowed (§6), 14 chromis and 6 blue tangs.

## 9. How it is built

- **Sim** (`src/Core`):
  - A new authored boss, built as Queen Clam is, by hand and not by the generator (`BossGenerator.DrippusPrime`):
    - `BossSpec`: a body plan `Ink`, a form `Murkling`, a concept `TheBrood`, the exposure `Whole`, and the attacks
      `Brood` and `Fan`.
    - `PlaneBoss`: its mass and size, its next attack, the bounce state and her light's tears.
  - In `PlaneDrippus.cs` (its numbers in `DrippusTuning`), hooked into `PlaneBoss.cs`:
    - The Brood act births through the murklings' own `Bud` (drinking nothing, out at once).
    - The bounce replaces contact for this boss; she tumbles (`PlaneBody.TumbleTimer`).
    - Its entrance drinks the arena's ink.
    - Freed, the fish it took swim out (`PlaneReefFish.Leaving`).
  - The roster picks it for every depth (§8, question 1).
  - Tests (`DrippusTests`, and the adventure's boss level in `AdventureTests`):
    - A birth costs mass and meter, and stops at the floor.
    - The brood cap holds.
    - The bounce has its tell, its damage and its cooldown.
    - Phases count births.
    - Freeing pops the brood.
- **View** (`src/Game`):
  - A boss rig (`DrippusRig`) on the murkling body and shader, with the bud bulges and necks, the tears and the
    pearl.
  - Her squash, stretch and spin when bounced.
  - The entrance stream.
  - Its cut-in art.
  - Sounds: a wet pop for each birth, the boing for the bounce.
- **Docs:** the rules are in CORRUPTION.md ("Drippus Prime"), DESIGN-TOPDOWN §6.4 and §12.2, BOSS-GENERATOR §1 and
  ADVENTURE.md.
