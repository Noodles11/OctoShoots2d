# Level templates: giving each level a shape and a cast

> **Built.** DESIGN-TOPDOWN §4.8 now holds the rules, and CORRUPTION.md's placement rules point to it; this page
> keeps the reasoning. Changed in the build: the cave has no "branches" knob (it grows branches until its water
> reaches its area), so a template shapes it through chamber size, tunnel width and length, and loops; and a
> depth's first level never holds a shop or a shell cache, whatever its template's chances.

## 1. The problem

Today every level is stocked by independent rolls (`LevelStocking`): a secret 40%, a curse den 30%, a shop 45%, and
so on. Every creature kind then places itself over the whole level by its own count and depth rules:
- Blightroots 3 + 1.5 × Menace.
- Blotters 2 + Menace.
- Thumpsquirts from Depth 2.
- Inkwardens beside Blightroots.
- Lashjaws from Depth 3.
- Murklings budding everywhere.

Nothing ties a level's places, layout and creatures together, so every level is a little of everything, scattered.
Levels feel alike and chaotic: no level has a character, and no place feels guarded on purpose.

## 2. The idea

Each level rolls one **template**: a named recipe that says what the level is about. A template:
- **requires** some places and allows others with a chance (forbidding the rest);
- **shapes** the cave (big chambers or long tunnels, few or many branches);
- **casts** the creatures: which kinds appear, how many, and **where** they stand (round a place, along the way, in
  the tunnels, at dead ends);
- adds **rules of its own** on top of the general ones (a guaranteed prime, more ink, faster guardians).

The general rules stay underneath and always win:
- The validator.
- Area and distance rules.
- The 5-place cap.
- Treasure rooms per depth.
- Depth gates for each creature kind.
- Menace scaling.

A template only narrows and arranges within them.

## 3. What a template holds

Templates are data (`data/level_templates.json`, loaded by Core like the pearls):

| Field | Meaning |
|---|---|
| `id`, `name` | Internal id and a storybook name ("The Hoard"), shown or not (§8 Q3). |
| `weight` | How likely it is among the eligible templates. |
| `depths` | Shown-depth range it may appear in (e.g. 1–6). |
| `slots` | Which levels of a depth: `first`, `middle`, `beforeBoss`, `boss`. |
| `places` | Per place kind: `required: n`, or `chance: p`, or `none`. Kinds not named keep the general roll. |
| `layout` | Cave knobs, as multipliers on CAVEGEN's: `chamberRadius`, `tunnelWidth`, `tunnelLength` (step), `branches`, `loops`. |
| `cast` | Per creature kind: `count` (a range, scaled by Menace), `anchor` (where, below), `none` to forbid. Kinds not named keep their general rule. |
| `rules` | Extra rules (below). |

**Anchors** (where a kind stands):
- `place:<kind>`: round that place's mouth, 4–10 m out.
- `path`: along the main chain from start to way down.
- `tunnels`: inside tunnels, not chambers.
- `chambers`: in open chambers.
- `deadEnds`: in branch chambers no place took.
- `walls`: on chamber walls (Lashjaws already do this).
- `spread`: anywhere, as today.

**Extra rules** (a small closed set the code knows):
- `prime: place:<kind>`: a murkling prime waits there from the start (not budded by chance).
- `guardians: place:<kind>`: the murklings of the nearest Blightroot bud round that place instead of round the root.
- `ink: +x`: the corruption's cover share, plus x (still capped at 85%).
- `budRate: ×x`, `budCap: +n`: murkling budding, faster or more.
- `springs: n`: light springs.
- `fish: ×x`: reef fish.
- `wardenFor: <kind>`: Inkwardens stand guard by that kind instead of by Blightroots.
- `quiet`: no Blotter birth all level.

## 4. How it is rolled

- Per level, from the run seed (the plan stream, so any level can still be generated on its own): pick among the
  templates eligible for its depth and slot, by weight.
- **No template twice in a row** within a depth, and each depth's levels draw from at least three different ones.
- The tutorial keeps its fixed levels. Boss levels roll from the `boss` slot only.
- **Conflicts with the general rules:**
  - Level 1 of a depth always holds a treasure room: a template that forbids one is not eligible there.
  - When a template requires more places than the cap allows, its optional ones go first.
  - A creature kind not yet unlocked at that depth is simply left out (and a template whose whole point needs one is
    not eligible).
- The plan records the template (`LevelPlan.Template`), so saves, the minimap legend and the debug panel can name it.

## 5. The templates

### 5.1 The Hoard
*A treasure room, a secret and a curse den, guarded by a murkling host led by a prime, with a few wardens.*

- **Places:** clam room required; secret required; curse den required; shop none; shell cache 30%.
- **Layout:** chambers ×1, branches ×1.2 (the rooms sit on branches off one shared hub chamber).
- **Cast:**
  - Blightroots 2, anchor `place:TreasureCave` and `place:Secret`.
  - Inkwardens 1–2, anchor `place:TreasureCave` (warding the guardians).
  - Blotters, Thumpsquirts and Lashjaws: none.
- **Rules:** `prime: place:TreasureCave`; `guardians: place:TreasureCave`; `budCap: +2`.
- **Slots:** first, middle. Depths 1–6.
- **Feel:** one fortified heart: the way to the pearl is a fight against a host, not a stroll.

### 5.2 The Long Haul
*A shop at the end of long, narrow tunnels with no open fields, patrolled by Thumpsquirts and Blotters.*

- **Places:** shop required; shell cache 40%; secret 20%; clam room none (unless level 1); curse den none.
- **Layout:** chamberRadius ×0.6, tunnelWidth ×0.8, tunnelLength ×1.4, branches ×0.6, loops ×0.
- **Cast:**
  - Thumpsquirts 2–3, anchor `tunnels`.
  - Blotters 2–3, anchor `path`.
  - Blightroots 1, anchor `deadEnds`.
  - Inkwardens and Lashjaws: none.
- **Rules:** `budRate: ×0.6`.
- **Slots:** middle, beforeBoss. Depths 2–6 (Thumpsquirts' gate).
- **Feel:** a march: bumps and leaps in the dark tunnels, the slugs smearing the way ahead, the shop as the reward.

### 5.3 The Open Grotto
*Wide chambers where Lashjaws wait in the walls.*

- **Places:** shell cache 60%; secret 30%; the rest by the general roll.
- **Layout:** chamberRadius ×1.4, tunnelLength ×0.8, branches ×0.8, loops ×1.5.
- **Cast:**
  - Lashjaws 2–4, anchor `walls`.
  - Blightroots 2–3, anchor `chambers`.
  - Blotters 0–1.
  - Thumpsquirts and Inkwardens: none.
- **Slots:** first, middle. Depths 3–6 (Lashjaws' gate).
- **Feel:** room to dodge, but every wall may bite.

### 5.4 The Ink Fields
*A level drowning in ink: many Blightroots and Blotters spreading it; cleansing it is the whole job (and pays the most
experience).*

- **Places:** shell cache 50%; curse den 30%; shop 30%; secret 20%.
- **Layout:** chamberRadius ×1.2, branches ×1.
- **Cast:**
  - Blightroots 4–5, anchor `spread`.
  - Blotters 3–4, anchor `chambers`.
  - Inkwardens 0–1.
  - Thumpsquirts and Lashjaws: none.
- **Rules:** `ink: +0.15`; `springs: 2`.
- **Slots:** middle. Depths 1–6.
- **Feel:** a cleansing level: the ink map turns from violet to colour as she works through it.

### 5.5 The Warden Garden
*Inkwardens in clusters, each warding a Thumpsquirt or a Blightroot; break the wards to win the fights.*

- **Places:** secret 50%; shell cache 40%; the rest by the general roll.
- **Layout:** chambers ×1, branches ×1.
- **Cast:**
  - Inkwardens 3–4, anchor `chambers`.
  - Thumpsquirts 2, anchor `chambers`.
  - Blightroots 2, anchor `chambers`.
  - Blotters and Lashjaws: none.
- **Rules:** `wardenFor: Thumpsquirt` (each warden stands by a Thumpsquirt, else by a Blightroot).
- **Slots:** middle, beforeBoss. Depths 2–6.
- **Feel:** a puzzle of order: free the wardens first, or fight through their shield.

### 5.6 The Cache Run
*A shell cache that lures her in; Lashjaws on the tunnel walls on the way.*

- **Places:** shell cache required; secret 30%; shop none; curse den none.
- **Layout:** tunnelWidth ×0.9, branches ×0.8.
- **Cast:**
  - Lashjaws 1–2, anchor `walls`.
  - Blightroots 1–2, anchor `path`.
  - Thumpsquirts, Blotters and Inkwardens: none.
- **Slots:** middle. Depths 3–6.
- **Feel:** a gauntlet: tense, short.

### 5.7 The Quiet Reach
*A breather: a shop, a light spring and the reef's healthy fish; only a single Blightroot.*

- **Places:** shop required; clam room 30% (or required on level 1); shell cache 50%; curse den none.
- **Layout:** chambers ×1.1, loops ×1.
- **Cast:**
  - Blightroots 1, anchor `deadEnds`.
  - All other kinds: none.
- **Rules:** `quiet`; `springs: 1`; `fish: ×2`; `budRate: ×0.5`.
- **Slots:** beforeBoss (weighted up there), middle (rarely). Depths 1–6. At most once per depth.
- **Feel:** a breath before the boss: spend, heal, enjoy the reef she is saving.

### 5.8 The Approach (boss levels)
*Few creatures on the way, so the fight is the boss.*

- **Places:** shop 50%; shell cache 30%; the rest none.
- **Layout:** the arena as now.
- **Cast:**
  - Blightroots 1–2, anchor `path`.
  - Blotters 0–1.
  - Everything else: none.
- **Rules:** `quiet`.
- **Slots:** boss only.

### 5.9 Default
*The general rules as they are now, for variety and as the fallback when no template is eligible.*

- **Slots:** first, middle. Weight low.

## 6. What changes in the code

- **Core:**
  - `LevelTemplate` and its JSON catalogue.
  - `LevelPlan.Template`, rolled in `LevelPlan.For`, whose places now come from the template over the general roll.
- **Cave generator:** `CaveLayout` takes the layout multipliers (its step, radius, width, branch and loop ranges).
- **World:** `PlaneWorld`'s placements (`PlaceBlightroots`, `PlaceBlotters`, `PlaceThumpsquirts`, `PlaceInkwardens`,
  `PlaceLashjaws`, the springs, the reef fish) read their count and anchor from the template, falling back to their
  general rules. The extra rules are a handful of checks in budding and placement.
- **Tests:**
  - Every template generates on many seeds.
  - Its required places and casts appear, its forbidden ones don't.
  - Template rolls are deterministic and never repeat on consecutive levels.
- **Docs:** DESIGN-TOPDOWN §4.1 (stocking), §8 (progression) and CORRUPTION.md (placement) rewritten to the template
  rules.

## 7. Weak points

- **Fewer levels have everything:** a player may go a few levels without a curse den or a secret. Weights and the
  "three different templates per depth" rule keep variety; the general places' chances can stay as optional chances in
  most templates.
- **Anchors may fail in a given cave** (no dead end free, no tunnel long enough): the kind then falls back to `spread`,
  so a level never fails to generate over its cast.
- **Balance:** templates move difficulty around (The Hoard is harder, The Quiet Reach easier). Menace still scales
  counts and meters underneath.

## 8. Questions

1. Should the template's name be shown? For example, under the level title ("Depth 2 · Level 3 · The Hoard"), or
   only in the debug panel.
2. Data in JSON (easy to tune without code) or in C# (simpler, typed)? The proposal says JSON.
3. Any template above to drop or change, or others to add?
