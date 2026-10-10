# Level flow: smaller levels, a hole in the floor, diving between them

> **Built.** DESIGN-TOPDOWN §4 and §8 now hold the rules (§4.6: the way down); this page keeps the reasoning. It
> reshapes how levels are sized, stocked, linked and left: each level is a smaller square, stocked from a per-level
> roll, and left through one hole in its floor. Clementine dives through the hole into the level below, whose start
> continues the rock around that hole. §10 lists the weak points; §11 the decisions taken on the open questions, and
> §12 the numbers the build had to change from this plan.

## 1. What changes, in one picture

```
 Depth 1 (Shallows)                                                          Depth 2 (Kelp)
 ┌─────────┐ hole ┌─────────┐ hole ┌─────────┐ hole ┌─────────┐ hole ┌─────────┐ Crack ┌─────────┐
 │ Level 1 │─────>│ Level 2 │─────>│ Level 3 │─────>│ Level 4 │─────>│ Level 5 │──────>│ Level 1 │ menace ↑
 └─────────┘      └─────────┘      └─────────┘      │ boss?   │      │ boss    │       └─────────┘
                                                    └────┬────┘      └─────────┘
                                                         └── if boss: Crack ──────────> Depth 2, Level 1
```

- **A depth is a short chain of levels.** The boss stands on level 4 (a chance) or level 5 (certain). Freeing it opens
  the Crack, and diving through the Crack starts the next depth at its level 1.
- **Every level has exactly one exit, a hole in the floor (the Leave POI).** On a boss level, that hole is the Crack in
  the arena.
- **Two levels live at once:** the current one and the one under its hole. After the dive, the level she left is
  dropped and the next one is made in the background.
- **The dive is a real descent.** Over the hole, Shift dives. The camera follows her down the shaft, the level above
  dissolves past the lens, and she arrives in a start area made of the same rock that ringed the hole.
- **No more trenches:** the hole is the only deep water on a level.
- **Menace rises only when a depth changes,** that is, when she leaves through a boss's Crack.
- **The cycle loops:** after the Tank's boss, the next dive starts Depth 1 again at a higher Menace (Q2).

## 2. Terms

| Term | Meaning |
|---|---|
| Depth | The biome tier (Shallows … Tank). It sets the look (ReefLook), the Menace and the bestiary. |
| Level | One generated square, numbered 1…5 within its depth. It replaces "Room" in the HUD, saves and stats. |
| Cycle | One pass through the seven depths. The loop counter raises Menace on later passes. |
| Hole | The opening in a level's floor. Below it lies the start of the next level. |
| Stamp | A small terrain patch: the rock around a hole, which the level below carries around its start. |
| ΔY | How far below the current level the next level's swim plane lies, in world space. |

## 3. Size

**The square shrinks from 150 m to 125 m a side** (about 30% less area). The 8 m rim stays. Distance rules scale by
×0.83 as a first pass; captures will tune them.

| Knob | Now | Proposed |
|---|---|---|
| Square | 150 m | **125 m** |
| Start → exit (geodesic, by swimming) | ≥ 130 m to the rift | **≥ 90 m** to the hole or the Crack (§12) |
| POI spacing | ≥ 25 m (caves ≥ 30 m) | ≥ 21 m (caves ≥ 25 m) |
| Early POI | 20–35 m from the start | 39–47 m (§12) |
| Boss arena | ~30 m disc | ~30 m disc (unchanged: the fight needs the room) |
| Trenches | 8–14 m wide, −4…−8 m | **none** |
| Rush time (one level) | 1–2 min | ~1–1.5 min |
| Full clear (one level) | ~10 min | ~6–7 min; a depth of 4–5 levels is ~30 min |

## 4. What each level holds

Each level is stocked from its own roll, at most **5 POIs**, not counting the start and the hole or Crack.

| POI | Level 1 | Level 2+ | Notes |
|---|---|---|---|
| Treasure room | 1, plus a 2nd at **20%** | **95%** for one; a 2nd at **3%** | Holds one pearl she does not have yet. |
| Secret room | 0–1 (**40%**) | 0–1 (**40%**) | 8–12 shells, trapped. |
| Curse den | 0–1 (**30%**) | 0–1 (**30%**) | |
| Shop | never | **45%** | 2 pearls at 15 shells each, plus a health top-up. |
| Shell cache (today's item cache) | see Q1 | see Q1 | 6–9 shells, guarded. |
| Ambush | 0–2 | 0–2 | Rolled last; dropped first when over the cap. |
| Boss arena | — | level 4: **50%**; level 5: **100%** if 4 had none | Counts toward the 5. Its Crack is the exit. |

- **Over the cap,** POIs are dropped in this order until 5 remain: ambushes, then the curse den, then the secret, then
  the 2nd treasure. The boss, the shop and the first treasure are never dropped.
- **The roll happens before generation and is part of the level's identity** (`LevelPlan`, §6.1).
- **All the percentages are data in one table** (`LevelStocking`), not code. Q3 explains how to settle them.

## 5. The hole, the shaft and the coupled start

### 5.1 Cross-section

```
  level N  ─ swim plane (0) ────────┐      ┌──────────────   rock around the hole: walls of level N
           ░░░░░ floor (−1) ░░░░░░░ │ hole │ ░░░░░░░░░░░░░
                                    │shaft │                 the shaft: level N's walls carried straight down
                                    │      │
  level N+1 ─ swim plane (−8) ──────┘      └──────────────   the same rock again, as level N+1's start area
           ░░░░░ floor (−9) ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░    (a solid floor: never a second hole under this one)
```

- **The hole is an open clearing on level N** (radius ~7 m, swimmable at the swim plane). It has no floor of its own.
  Looking down it, you see level N+1 through plain water. Nothing marks what lies below.
- **The stamp:** the terrain within ~22 m of the hole is copied into level N+1 around its start.
  - The copy keeps the same height relative to its own swim plane, so in world space it sits exactly ΔY lower.
  - The mountain to the left and the hill below on level N are the same mountain and hill around her when she arrives.
    The shaft walls join the two copies, so the rock reads as one cliff going down.
- **Map coordinates need not match.** Level N+1 is placed under level N by a translation T (the hole on N minus the
  start on N+1), plus ΔY. Only the stamp has to line up.
- **"Level N+1 has a bottom":** with one hole per level, its hole is always ≥ 90 m from its start. So it is never in
  view down the shaft, and two levels can never be crossed at once.

### 5.2 ΔY = 8 m, with the fog left as it is

The water pass fogs whatever lies below the swim level, more the deeper it is. With the fog unchanged, here is how
much of the next level's floor shows down the shaft:

| ΔY | Fog over the lower floor, Shallows | Fog over the lower floor, Abyss | Read |
|---|---|---|---|
| 4 m | ~0.40 | ~0.59 | Clear, but it reads as a dip, not a drop |
| **8 m** | **~0.48** | **~0.67** | Blurred shapes you can still read; feels like a real descent |
| 12 m | ~0.56 | ~0.73 | Mostly murk; the stamp's rock hard to make out |

**8 m:** the deepest it can go while the rock below still reads through unchanged fog, and comfortably over the
brief's minimum of 3. The water pass also blurs it, which suits "seen through water".

### 5.3 Generating the coupling: the stamp first, from its own seed

1. **A stamp is generated alone, from its own seed:** a 44×44 m patch with the hole's clearing in the middle and 2–3
   openings where canyons leave it. It is cheap and deterministic.
2. **Level N places its child's stamp at its hole** as hard terrain. It cuts the hole and joins the openings to its
   canyon network.
3. **Level N+1 places the same stamp at its start** and builds outward from its openings. The start no longer has to
   sit on the border zone.

Each level depends only on seeds (the run seed, its depth and level, its own and its child's stamp). So Continue
regenerates only the saved level and the one below it, and the two can be made in parallel.

### 5.4 The boss level's exit: the Crack

The arena keeps its round, level floor. The Crack across its middle becomes the shaft: a fissure ~14–18 m long that
opens all the way down once the boss is freed. Its stamp is the arena's rim, so the next depth's level 1 starts
inside the same ring of rock.

## 6. How it would be built

### 6.1 Core: level identity and stocking
- `LevelPlan`: cycle, depth, level, has-boss, the POI roll, and the stamp seeds (its start stamp and its hole stamp).
  - It is built from the run seed alone; the `RunStreams` key becomes `topdown/{cycle}/{depth}/{level}/…`.
- `LevelStocking`: the §4 table as data, with tests (caps, drop order, the boss on 4 or 5).
- `PoiKind`: `Rift` becomes `Crack` and a new `Hole` kind is added. The item cache follows Q1.

### 6.2 Core: generator
- `LevelMap.Size` becomes 125, with every distance constant rescaled (§3); the tests follow.
- **Trenches are removed:** `CutTrenches`, the `Trench` type, trench decor, and the "trench route" note.
- **Stage 0 (new):** generate the start stamp and the hole stamp. Pin both as fixed heights before the POIs; the
  canyon network must connect to each stamp's openings.
- **Stage 1:**
  - The start sits at its stamp, anywhere inside the square.
  - On a non-boss level, the hole goes ≥ 90 m from the start by swimming. On a boss level, the Crack goes in the arena.
  - Optional POIs come from the plan's roll, under the cap.
- **Stage 3:** the hole's clearing is marked "shaft": no floor is drawn there, and the stamp's walls carry down.
- **Validator, new rules:**
  - The exit can be reached from the start, and lies ≥ 90 m away.
  - There is exactly one exit, inside the arena on a boss level.
  - Both stamps are matched within 0.1 m.
  - No deep water exists outside the shaft.
- **Watch:** two pinned stamps per level are hard constraints, so attempts will fail more often. Log the failure
  reasons and tune the stamp size and openings until fewer than 1 attempt in 3 fails.

### 6.3 Core: the dive in play
- A new input, **Dive (Shift; a controller button too)**.
- `PlaneWorld` raises `DiveReady` while she floats within the hole's radius and `DiveStarted` on the press. The sim
  stops stepping that level.
- **No diving in an active battle.** The dive is refused while any of these holds:
  - a creature that has noticed her is within ~12 m;
  - an ambush is sprung;
  - the arena is sealed.

  The prompt then reads "Not while fighting". The Crack works only after the boss is freed.
- **Menace:**
  - The sim reads `Menace` from the depth and the cycle; today it only drives the look.
  - It is applied where DESIGN-TOPDOWN §6.2 says: aggression, speed, telegraphs and budget.
  - It steps up only on a Crack dive.

### 6.4 Game: the level stack
- `LevelStack` holds **current** (map, world, full view) and **below** (its map plus views).
- **On arrival in a level:**
  1. Start a background task for the map below (pure Core, off the main thread, as the splash does today).
  2. When the map is ready, build its shaft view: terrain, rock and flora within ~22 m of its start, placed at T and
     −8 m. Build it in time slices so it never hitches.
  3. When she comes within ~30 m of the hole, start building the full view below, also time-sliced.
- **Unload:** after a dive, free everything of the level above: meshes, multimeshes, the `PlaneWorld`, materials, and
  any of its tasks still running.
- **Fallback:** if the level below is not ready when she presses Shift, the dive's opening beat (§6.6) holds a moment
  longer rather than stopping on a splash.

### 6.5 Game: seeing the level below
- The view below draws at its real place (T, −8 m). Its materials **clip to the shaft**: they discard anything outside
  the hole's footprint, extruded down.
  - Without the clip, the lower level's walls (up to +14 m, so about +6 m in world space) would poke through the
    current level's floor.
- Level N's terrain mesh leaves out the hole's floor and draws the shaft walls (its own rock extruded down by 8 m).
- The fog and blur stay exactly as they are (§5.2).
- The minimap and topographic map mark the hole the way they mark the rift today (the exit has to be findable). On a
  boss level, the Crack's icon takes that place.

### 6.6 Game: the dive (about 1.6 s, input locked)

| Time | Clementine | Camera and world |
|---|---|---|
| 0.00–0.25 | She gathers: the bell flares wide and the tentacles lift. | A slight push in. The "Dive" prompt fades. |
| 0.25–0.55 | A hard contraction. The bell flips apex-down and the tentacles stream up behind her. | The camera starts down with her. Level N's floor and walls rise past the lens and dissolve (the canopy dither, applied to the whole level). The focus plane slides from 0 to −8 m. |
| 0.55–1.20 | She falls down the shaft in two or three pulses, glow trailing. | Down the shaft: the walls are level N's rock carried down. Marine snow streaks upward. On a Crack dive, the look blends to the next depth's ReefLook. |
| 1.20–1.60 | She rights herself, flares to brake and settles on the new swim plane. | The camera eases to its usual height over level N+1. Level N is gone. |
| 1.60 | — | **Rebase**, all in one frame: the new level moves to the origin and the camera with it, so nothing on screen jumps. Input returns. |

- **Reduced motion:** a 0.6 s cross-dissolve along the same path, with no flip and no streaks.
- **Audio** (when sound exists): an inhale before the contraction, a muffled whoosh, and the room tone pitched down a
  step.

### 6.7 Progression, saves and UI
- HUD: `Depth x · Level y`, with the cycle shown from the second pass on. The between-room splash and its "Press Enter"
  give way to the dive. The splash stays for death.
- **Autosave on arrival** at each level. `SuspendedRun` stores `Cycle`, `Depth` and `Level`. Continue regenerates only
  that level and the one below (§5.3).
- Stats: "Rooms cleared" becomes "Levels cleared"; "Depths reached" and "Cycles" are new.
- The title backdrop simply uses the new size.

### 6.8 Order of work
1. `LevelPlan` and `LevelStocking`, with tests (Core only, nothing visible).
2. Size 125 and the rescaled rules; remove the trenches. Fix the generator and validator tests.
3. Stamps: the stamp generator, pinning at the start and the hole, the new validator rules, and a debug image of a
   hole beside the next level's start.
4. The hole in play: the Dive input, events, battle gating, and the Crack as the boss level's exit. As a stop-gap, a
   plain fade.
5. The level stack: background generation, the shaft view, the shaft clip, unloading.
6. The dive animation and rebase.
7. Menace in the sim, depth progression and the loop, saves, HUD, stats.
8. Docs: rewrite DESIGN-TOPDOWN §4, §8 and §12 in place (§9).

## 7. Performance budget (to check, not yet measured)

| Item | Estimate | Risk |
|---|---|---|
| Generating one level | ~0.65–0.75 s at 150 m today, ~0.5 s at 125 m; stamps add retries | Low: in the background |
| Two levels in memory | One full view, plus the shaft view below (a full view only near the hole) | Low to medium on mobile: measure |
| Drawing down the shaft | Shaft geometry only (clipped), no creatures below | Low |
| Building the full view below | Time-sliced over a few seconds as she approaches | Medium: `LevelView` needs slicing |

## 8. Theme check (THEME-BIBLE §12)

- **Warmth:** she stays the warm light. The shaft is dimmer than she is.
- **Captivity foreshadowing:** fits. A hole in the Tank's floor could show the flat, perfect gravel.
- **Freed, not killed:** unchanged. The boss is freed, the Crack opens, she descends.

## 9. Design rules this would change (none changed yet)

| DESIGN-TOPDOWN | Now | Proposed |
|---|---|---|
| §4.0 | A static 150×150 m footprint | 125×125 m |
| §4.0 / §4.4 | Two outcomes: descend through the rift (after the boss) or perish | Dive through the level's one hole, or through the Crack on a boss level; or perish |
| §4.1 Stage 1 | Start on the sunlit border; shop always; 1–2 secrets; 1 treasure; 2–4 ambushes; a guaranteed item cache | Start anywhere at its stamp; per-level roll (§4); ≤ 5 POIs |
| §4.1 | Rift ≥ 130 m geodesic | Exit ≥ 90 m |
| §4.1, §4.5 | Trenches 8–14 m wide, −4…−8 m | No trenches |
| §4.4 / §8 | Entering over a light-shaft; sucked down the Crack, then a comic-panel tunnel | A seamless dive down a shaft, every level |
| §8 | 7 depths × three reefs each; the run ends at The Hand | 7 depths × 4–5 levels, the boss on 4 or 5; the depths loop with Menace rising (Q2) |
| §6.4 | A boss on every level (the current build) | A boss only on level 4 (chance) or level 5 |

## 10. Weak points

1. **The lower level pokes through the floor** unless it is clipped (§6.5). This is the one hard technical point; the
   clip and the shaft walls must land together.
2. **Two pinned stamps per level** constrain the generator at both ends. Expect more retries, and a corridor network
   that sometimes bends hard to reach the hole's stamp.
3. **A dim view down.** With the fog as it is, the lower level shows as blurred shapes about half fogged (two-thirds
   in the dark depths). It reads as depth, but the "same rock" continuity relies on the shaft walls more than on the
   view.
4. **Rushing.** With one ungated exit ≥ 90 m away, a level can still be crossed in about a minute. That is
   roguelite-normal (the treasure is the reason to stay), but a rusher clears a depth in ~5–7 min.
5. **Loot and shells.** Fewer POIs per level but more levels per depth. Shells especially: they come from the cache,
   secrets and mob drops, while the shop now appears only 45% of the time. Q1 and Q3.
6. **Menace has no sim effect yet.** It drives only the look. "Menace rises after each boss" needs the §6.2 ramp built
   in Core.
7. **Only Depth 1 has creatures.** After the first boss, Depth 2 has the Kelp look but the Depth 1 bestiary at a higher
   Menace, until the new bestiaries are designed (design first, per our rule).
8. **Shift was the old dash key.** Muscle memory may cause accidental dives. The prompt and the battle gate help; a
   0.25 s hold is the fallback if it happens in testing.
9. **What the end of the loop means.** The design has a true ending at The Hand. "It will loop" needs a decision on
   whether the Hand fight still ends a cycle, or the loop replaces it (Q2).

## 11. Decisions on the open questions

### Q1. The item cache: keep, fold in, or drop?

What it is in the build today: an open clearing 40–60% of the way from the start to the rift, off the main canyons. It
holds **6–9 shells** (not a pearl, whatever the design doc's wording) and is guarded by 3–4 swimmers plus 1–2 crabs or
urchins. It is a level's one guaranteed fight-for-loot spot and its steadiest shell income.

Shells per level today vs. under the new roll:

| Source | Today | New roll, level 2+ (expected) |
|---|---|---|
| Item cache | 6–9 (always) | depends on the option below |
| Secret rooms | 8–12 each × 1–2 → ~15 | 8–12 × 40% → ~4 |
| Mob drops | 1–2 per creature freed | the same per creature (fewer creatures on a smaller map) |
| Shop to spend them in | always (2 pearls at 15) | 45% |

| Option | What happens | For | Against |
|---|---|---|---|
| **A. Keep it as a sixth fixed place** outside the cap | Every level has one guarded shell cache on the way | Steady shells; a fight that rewards leaving the path | Effectively 6 places; less room for the roll |
| **B. Make it a POI in the roll** (e.g. 60%) | Some levels have it, some don't | Fits the cap; more variety | Shells get swingier when the shop is rarer too |
| **C. Drop it** | Shells come from secrets and mobs only | Simplest; fewer places on a smaller map | ~10–20 fewer shells per level, while a pearl costs 15 |

**Decided: B at 60%, kept out of level 1** (where the treasure room already rewards exploring). Shell income
stays close to today's per level, on top of the drops. Level 1 gets an extra treasure chance instead.

### Q2. What does "it will loop" mean at the end?

- **(a) Endless:** after the Tank's boss, Depth 1 again at the next cycle's Menace; The Hand stays for later.
- **(b) The Hand ends a cycle:** win, then optionally loop on.

**Decided: (a), endless for now**, the HUD showing the cycle; revisit when The Hand is built.

### Q3. How to settle the percentages

Every number in §4 sits in the `LevelStocking` table. For each candidate table, a Core test-tool rolls 10,000 depths
and prints:
- pearls per depth;
- shells earned vs. shells a shop could take;
- how often a depth has no shop;
- how often a level hits the cap.

Proposed targets to tune toward:
- **4–6 pearls per depth** from treasure, about one more from shops;
- **at least one shop in 4 of every 5 depths;**
- **the cap hit on fewer than a quarter of levels.**

Start from §4's numbers and adjust from the printout, then from play.

## 12. What the build changed from this plan

A 125 m square holding two pinned stamps left the generator too little room for some of the planned numbers. Each
change below is in DESIGN-TOPDOWN as the rule:

| Rule | Planned | Built | Why |
|---|---|---|---|
| Start → exit by swimming | ≥ 108 m | **≥ 90 m** | Both stamp centres must sit 24 m in from the edge; at most ~109 m straight-line is left, so 108 m by swimming only fit corner to corner. |
| The early place | 17–29 m from the start | **39–47 m**; no place nearer than 39 m | A place's keep-out must not reach the route stretch pinned down a stamp's canyon (20 m), or the route is pushed into the stamp's rock. |
| Places from the exit | ≥ 33 m | **≥ 40 m** | The same reason, at the hole's stamp. |
| Spur length | ≤ 20 m | **≤ 24 m** | Routes give places a wider berth on the smaller square. |
| Reachable water | ≥ 50% | **≥ 45%** | The stamps' rock is fixed and never sinks into shoals. |
| Arches, passes | 1–5 arches, ≥ 1 pass | **0–5 arches, 0–3 passes** | Fewer flanks and saddles fit outside the stamps. |
| Side canyons, plazas | ≥ 4, 2–4 | **≥ 2, 1–4** | A smaller labyrinth. |
| Routes | 3–5 | **3–4** | |
| Treasure rooms | multi-chamber caves | a single pocket where the ridge behind is too thin | It was the commonest reason a level failed. |

A level now generates in about 6 attempts on average (0.3–3 s). The level below is made on one thread while she
plays, so the frame rate holds.
