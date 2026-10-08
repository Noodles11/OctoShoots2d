# Title menu

> The title screen and its areas: new run, seeded run, statistics, the glossary (Sea-pedia) and save/load. The first
> pass is built (`src/Game/Title/`); §10 lists the decisions and §11 what this pass leaves for later.

## 1. What it is for

The game currently boots straight into a run. The title screen becomes the front door and the place between runs:
- start a run;
- see how you are doing;
- browse what you have found;
- keep your progress safe.

It follows the inherited meta rules:
- one profile;
- one suspended run;
- an export code for backups (DESIGN-2D §13);
- custom seeds never earn achievements (§13.1).

It is a menu scene, not the free-swim Tide Pool hub; the hub question stays open (DESIGN-TOPDOWN §12, item 4 in
§10 here).

## 2. Flow

```
 boot ──> Title ──┬── Continue ───────────────> splash ──> room N (where the save left off)
                  ├── New run ────────────────> splash ──> room 1 (random seed)
                  ├── Seeded run ─[seed box]──> splash ──> room 1 (that seed)
                  ├── Sea-pedia (glossary) ─── Pearls │ Creatures
                  ├── Statistics
                  ├── Save & load ──────────── continue slot · export · import · erase
                  ├── Settings
                  └── Quit
 in a run:  Esc ─> pause ─> "Save & quit to title"
 death:     splash ─> "New run" (as now) or "Back to title"
```

## 3. Look

### 3.1 The backdrop: a live sea, not a picture

The title renders the real game behind the menu.

**The scene**
- **The level.** A fixed, handsome seed (`TIDEPOOL`), shown from the game's own camera and slowly drifting along a
  canyon (about 0.6 m/s, turning gently).
- **Clementine** bobs idly in the right third of the screen. Her glow lights the water, and a bubble rises from her
  every few seconds.
- **The look:**
  - the depth focus, caustics and sunlight stay on;
  - the camera shake and the HUD are off;
  - the frame is dimmed by 15% under the menu column for legibility.
- **Trophies.** Once Queen Clam has been freed, she sits in the backdrop in her true colours, on a rock near
  Clementine. Each freed boss adds itself the same way, so the screen shows progress (DESIGN-2D §13.2).

**Mood**
- It is calm, bright and underwater: teal water, warm Clementine light, pearl-white cards.
- It uses the same bright language as the achievement banner, so the meta screens feel like one family.

### 3.2 Type and colour

**Fonts**
- **Display:** Baloo 2 ExtraBold, for the logo, screen titles and big numbers.
- **UI:** Nunito, for menu items, labels and body text.
- **Seeds and codes:** IBM Plex Mono.
- All three are open-licensed, so they can ship with the game as TTFs.

**Colours**

| Token | Use |
|---|---|
| Pearl `#fffaf4` | card faces |
| Coral `#ff7a6b` | rims, the focused item, primary buttons |
| Mint `#6fe3c1` | positive values, "new" chips, the seed box when valid |
| Butter `#ffd166` | records and highlights ("best") |
| Ink `#3b1f2b` | text on cards |
| Deep `#0e3237` | text on the water, panel shadows |

### 3.3 Layout (1600 × 900 reference; scales to the window, safe-area aware)

```
┌────────────────────────────────────────────────────────────────────────────────┐
│  INK DEEP                                                                      │
│  the absorbent jellyfish                                                       │
│                                                                                │
│  ▸ Continue          Depth 1 · Room 3 · KELP 7Q2Z · 12:41                     │
│    New run                                                ◯  Clementine      │
│    Seeded run   [ KELP 7Q2Z ] ⚄                              bobbing          │
│    Sea-pedia            12 / 63 pearls · 2 creatures                           │
│    Statistics                                                                  │
│    Save & load                                                                 │
│    Settings                                                                    │
│    Quit                                                      v0.3 · profile 1 │
└────────────────────────────────────────────────────────────────────────────────┘
```

**The menu column** is at the left, 96 px from the edge.

**The logo**
- "INK DEEP" in Baloo 2 at 96 px, pearl-white, with a soft coral drop shadow.
- A small Clementine bell sits in the dot of the "i".
- Below it, "the absorbent jellyfish" in Nunito italic at 22 px.

**The items**
- Nunito ExtraBold, 30 px, pearl-white on the water, 18 px apart.
- Each item has a small grey **detail line** beside it, so the menu tells you something before you open anything:
  - **Continue:** where the save is.
  - **Sea-pedia:** how much you have found.
  - **Statistics:** your best room.
- **The focused item** turns coral and moves 12 px right. A coral pip appears in front of it: a tiny bubble that
  pulses.
- **Continue** appears only when there is a suspended run. When it is there, it is first and focused by default;
  otherwise **New run** is.

**Sub-screens** open as a large pearl card that rises from the bottom right:
- 1040 × 720 px, with an 18 px radius and a 3 px coral rim;
- the menu column stays visible but dims to 40%;
- the backdrop keeps moving behind it.

## 4. The screens

### 4.1 New run

- Starts at once with a new random seed: no confirmation.
- If a suspended run exists, the item asks first, in a small inline card: "Start a new run? Your saved run (Depth 1
  · Room 3) will be lost." It offers **Start new** and **Keep it**.

### 4.2 Seeded run

**The seed box**
- Choosing the item opens the box right under it, inline, without opening a card.
- It is a pill-shaped field in IBM Plex Mono at 28 px, showing two groups of four: `KELP 7Q2Z`.
- It is the game's own seed format: 8 characters from `ABCDEFGHJKLMNPQRSTUVWXYZ23456789`, with no I, O, 0 or 1.

**Typing**
- Typing is forgiving:
  - lower case is upper-cased;
  - the space between the groups is added for you;
  - I, O, 0 and 1 are not in the alphabet (they look alike), so typing one refuses it with a small shake of the box.
  - Pasting a whole code works, with or without the space.
- **Validity shows as you type:** a mint rim and a ✓ when the seed is complete; a coral underline under any character
  that is not in the alphabet.

**The box's controls and rules**
- A **dice button** (⚄) fills in a random seed; pressing it again rolls again.
- A note under the box: "Seeded runs don't earn achievements or unlock pearls."
- **Enter**, or the **Dive** button, starts the run; **Esc** closes the box.
- The last five seeds you played are listed under the box as chips, so you can replay one with a click.
- The box opens pre-filled with the last seed you played.

### 4.3 Statistics

A pearl card with a big-number header row, then grouped tables. All values come from the profile; seeded runs count
in a separate "seeded" column, so records stay honest.

**Header (four tiles, Baloo 2 numbers at 56 px)**

| Runs | Best reach | Foes defeated | Time in the sea |
|---|---|---|---|
| 37 | Depth 1 · Room 6 | 1,284 | 9 h 12 m |

**Groups (two columns of label/value rows, tabular numbers, a butter "best" badge on records)**

| Runs | | Combat | |
|---|---|---|---|
| Runs started | 37 | Bubbles thrown | 48,210 |
| Rooms cleared | 112 | Full bubbles made (15 merged) | 31 |
| Deaths | 36 | Biggest volley | 10 bubbles |
| Longest run | 41:07 | Damage dealt | 412,880 |
| Fastest room | 1:52 | Damage taken | 9,940 |
| Bosses freed | 18 | Fastest boss | 0:21 (Queen Clam) |

| Treasure | | Achievements | |
|---|---|---|---|
| Shells collected | 3,904 | Earned | 3 / 5 |
| Shells spent | 2,610 | Most shells in a room | 74 |
| Pearls absorbed | 166 | Untouchable rooms | 4 |
| Favourite pearl | Triple Tentacle (29 runs) | Seeded runs played | 6 |

**At the bottom: How runs ended.** A horizontal bar per cause of death (by Ninja-dot shot, by Queen Clam's pearls, by
her snap, and so on), sorted by count. Each bar is coral with the count at its end.

### 4.4 Sea-pedia (the glossary)

A pearl card with two tabs, **Pearls** and **Creatures**. Each tab is a grid of entries on the left (about
60%) and a detail pane on the right (about 40%) for the selected entry. A filter row sits above the grid.

**Pearls tab**
- **Grid.** 72 px tiles: each is the pearl itself, rendered with its own shader and colours (`PearlMaterials`), turning
  slowly.
- **Each pearl is in one of four states:**

| State | Tile | Detail pane |
|---|---|---|
| **Absorbed** (taken at least once) | the pearl, in full colour | everything |
| **Seen** (offered in a room or shop, never taken) | the pearl, at 60% with a faint "seen" ring | name, tagline, effects; no stats yet |
| **Locked** (waits for an achievement) | a dark silhouette with a small lock | the achievement's name and how to earn it |
| **Unknown** | a dark silhouette with "?" | "Not found yet." |

- **Filters.** All · Absorbed · Locked, plus the pool (treasure, shop, boss).
- **Detail pane, top half:**
  - a 160 px pearl;
  - the name (Baloo 2, 32 px) and the tagline (italic);
  - the effect lines (the same `ItemCaption` text as the pause menu);
  - the pools it appears in.
- **Detail pane, bottom half: its record:**

| Absorbed | Runs it was in | Runs lost holding it | Rooms cleared with it |
|---|---|---|---|
| 29 | 24 | 21 | 71 |

**Creatures tab**
- **Grid.** One entry per creature and boss on the plane today: the placeholder shooting mob and **Queen Clam**. Each
  creature ported from the bestiary adds one.
- **Tiles** are drawn portraits; bosses get a gold frame. An unknown creature is a dark silhouette.
- **Each creature is in one of two states:**
  - **Known** after it has been met once (it noticed Clementine, or she hit it).
  - **Unknown** before that.
- **Detail pane:**
  - **Header:** portrait, name, and a short, funny field-guide line (e.g. Queen Clam: "Sits on the rift. Hates
    visitors. Loves pearls, as long as they are hers.").
  - **Notes:** what it does, in plain words (shoots, how hard, what to watch for).
  - **The record:**

| Defeated | Defeated you | Encounters | Best time (bosses) |
|---|---|---|---|
| 1,240 | 31 | 1,402 | 0:21 |

**Counting on the record cards:**
- **Defeated** counts foes she beat (bosses: times freed).
- **Defeated you** counts runs that one ended (the killing blow).
- **Encounters** counts how many of them noticed her.

### 4.5 Save & load

The game saves on its own; this screen shows that and gives you the controls. It is a pearl card with three blocks.

**1. Your run**
- A slot card with:
  - the seed;
  - depth and room;
  - HP;
  - shells;
  - the pearls held (a row of small pearls);
  - the time played;
  - when it was saved ("today, 21:14").
- It offers **Continue** and **Abandon run**. Abandoning asks first, and counts the run as ended in the statistics.
- **The run is saved automatically:**
  - at the start of every room, once its splash is dismissed;
  - when you choose **Save & quit to title** from the pause menu.
- Continuing always restarts the room it was saved in, in the same state it was in when the room began (DESIGN-2D
  §13.1). The pause item says so: "Save & quit. You'll resume at the start of this room."

**2. Backup**
- **Copy save code.** This copies the whole save (profile and run) as one text code. It uses `SaveCodec`'s export
  format: gzip plus base64url, with a version prefix. A toast confirms "Save code copied".
- **Import save code.** This opens a field for pasting a code.
  - A good code shows a preview first: runs, pearls found and achievements. Then **Replace my save** confirms.
  - A bad code says why (not a save code, a newer version, damaged) and changes nothing.

**3. Profile**
- **Erase everything** clears the profile and the run. It is guarded: you type `ERASE`, then confirm.
- The save file's location is shown in small mono text, for people who back up files.
- If the save on disk was unreadable at boot, a coral notice explains that a fresh profile was started and that the
  old file was kept as `save.bad.json`. That fallback already exists in `SaveStore`.

### 4.6 Settings (small, needed by the rest)

- View options that exist today: the frame-rate cap, the sunlight strength and the camera shake.
- The planned **reduced motion** switch (the achievement banner uses it).
- Volume sliders, once there is audio.
- Key bindings (InputSetup) are read-only for now.

## 5. Motion

The title uses the **surface & dive** language of the banner prototype, so everything meta feels like one family.

**Boot**
- The backdrop fades up from black over 0.8 s.
- The logo **surfaces**: it rises 40 px through an invisible water line with a slight overshoot, a splash of droplets
  and a wet sheen sliding down it.
- The menu items then bob up one by one, 60 ms apart.

**Focus**
- The focused item glides 12 px right with a quick ease-out (120 ms).
- Its coral pip pops in with a tiny bubble-pop ring.
- Moving the focus leaves a short trail of 2–3 bubbles rising from the item it left.

**Opening a sub-screen**
- The card **surfaces** from the bottom right (rising and settling over 420 ms), and the menu dims.
- Closing makes it **dive** (a small hop, then down and out, with a stream of bubbles, 380 ms).
- Tabs switch with a horizontal slide of the grid (200 ms) and a cross-fade of the detail pane.

**Starting a run**
- Clementine jets downward out of frame, leaving a bubble trail.
- The screen tilts slightly as the camera "dives" and fades into the existing loading splash.
- The splash shows "Depth 1 · Room 1" and the seed.
- Total time to the splash: about 0.9 s.

**Pearl tiles in the Sea-pedia**
- Tiles turn slowly; on focus they grow 8% and catch a glint.
- A tile first unlocked since your last visit shows a mint **NEW** chip until you look at it.

**Cost and accessibility**
- Everything is transforms and opacity, plus small particles drawn as sprites; there are no full-screen filters, so
  it is cheap on weak machines.
- With **reduced motion**, surfacing, diving and particles become cross-fades.

## 6. Controls

- **Mouse:** hover focuses an item; a click activates it.
- **Keyboard:**
  - W/S or the arrow keys move the focus; Enter or Space activates; Esc goes back.
  - In grids, the arrow keys move between tiles and Tab switches tabs.
- **Gamepad** (later): the same model. A confirms, B goes back; the bumpers switch tabs.
- The seed box takes typing only while it is open; Esc closes it first, then the next Esc leaves it.

## 7. What needs recording (Core)

The profile already has achievements, seen items, creatures and bosses, and run stats. It needs more, recorded
deterministically from the sim's events by a new `ProfileRecorder` in Core, so it can be tested.

| New in the profile | Fed by |
|---|---|
| `Stats` totals: rooms cleared, bubbles thrown, full bubbles, biggest volley, damage dealt/taken, shells collected/spent, pearls absorbed, bosses freed, time played, seeded runs | world events and `PlaneStats` per room |
| Records: longest run, fastest room, fastest boss per boss, most shells in a room, untouchable rooms | room and boss timers |
| `Pearls[id]`: absorbed, runs in, runs lost holding it, rooms cleared with it | `PearlCollected`, run end, `GatewayEntered` |
| `SeenItems` grows when a pearl is offered (room or shop), not only when taken | room setup |
| `Creatures[kind]`: encounters, defeated, defeated you, best time (bosses) | `MobDefeated`, `BossFreed`, aggro onset, and the **killing blow** |
| `DeathsByCause` | the killing blow's source |
| `RecentSeeds` (last 5) | run start |

**The killing blow** needs one change in the sim: every hit on Clementine records its source (`mob`, `boss_pearl`,
`royal_pearl`, `boss_snap`, `boss_contact`). `PlayerDefeated` then carries it.

## 8. Saves

**The suspended run (`SaveFile.Run`)**
- It still has the 3D game's shape: position, yaw and pitch, craters, bombs.
- It becomes the plane run's state at the start of a room:
  - seed, custom-seed flag, depth and room;
  - pearls held, HP, shells;
  - time played;
  - the run's totals so far: foes, shells, rooms, damage.
- The save format moves to **version 2**. The migration from version 1 keeps the profile and drops an old 3D run,
  since none can exist on the plane.

**The export code**
- The export prefix changes from the 3D `CQ3D1:` to `INKD2:`, a new name for a new format. Old codes are refused
  with a clear message.

**Writing to disk**
- Writes are atomic: write `save.tmp`, then rename. A crash mid-write cannot corrupt the save.

## 9. How it would be built

**Scenes**
- **`TitleMain`** (new, `src/Game/Title/`) becomes the project's main scene. It owns the backdrop (a `LevelView` of
  the title seed, the `CameraRig` on a path, a `BellView`), the menu, and the sub-screen cards.
- **`TopDownMain`** is opened with a launch request: a small static `RunLaunch` holding the seed, the custom flag,
  and continue or new. It gets two exits back to the title:
  - **Save & quit** in the pause menu;
  - **Back to title** on the death splash.
- Its debug flags (`--seed`, `--at`, …) keep working: they **skip the title** and start a run directly, so
  verification captures are unchanged.

**UI pieces**
- **`TitleMenu`:** the column, focus and the detail lines.
- **`SeedBox`:** parsing via `SeedCode.TryParse`, the per-character check, the dice and the recent chips.
- **`StatsCard`.**
- **`SeaPedia`:**
  - the tabs, the grid and the detail pane;
  - the pearl tiles share one SubViewport atlas with `PearlMaterials`;
  - the creature portraits are SubViewport renders of the creature meshes, made once.
- **`SaveCard`.**
- **`Toast`.**
- **A shared `SurfaceDive` motion helper:** Tween presets plus the water-line clip and the particles, reused by the
  achievement banner.

**Core**
- `ProfileRecorder` (§7).
- The new `SuspendedRun` and the version 2 migration.
- `SeedCode` input helpers (partial validation, formatting as you type).

**Tests**
- the recorder's counts from scripted event streams;
- save round-trips and the version 1 → 2 migration;
- export and import, including bad codes;
- seed input (paste, spacing, invalid characters);
- Continue restoring the room's starting state.

**Order of work:**
1. Saves version 2 and the recorder.
2. The title scene with New run, Seeded run, Continue and Save & quit.
3. Statistics.
4. Sea-pedia.
5. Save & load (export/import/erase).
6. The motion pass.
7. Settings.

## 10. Decisions

1. **"Failed" in the glossary.** For creatures it means *Defeated you* (the runs one ended). For pearls it means
   *Runs lost holding it*.
2. **One profile and one saved run**, plus the backup code.
3. **Seeded runs count** in the statistics, and are also counted on their own.
4. **A menu over the live sea.** The free-swim Tide Pool hub is for later.
5. **Continue resumes at the start of the room.**

## 11. Not in this first pass

- **Fonts.** Baloo 2, Nunito and IBM Plex Mono are not bundled yet. The screen asks the system for them, then falls
  back to Arial Rounded / Segoe UI / Consolas.
- **Trophies.** Freed bosses do not yet appear in the backdrop.
- **Small motion.** The bubble trail when the focus moves, the logo's splash, and Clementine's jet before a run are
  not built. The menu blooms in (fade and grow) rather than rising, because the rows sit in a layout container.
- **NEW chips** on pearls unlocked since your last visit wait for the achievements.
