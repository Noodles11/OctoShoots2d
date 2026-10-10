# Ink Deep

A top-down action roguelite in a darkening ocean.
- **Design:** [`docs/DESIGN-TOPDOWN.md`](docs/DESIGN-TOPDOWN.md) is authoritative. It builds on
  [`docs/DESIGN-3D.md`](docs/DESIGN-3D.md) and [`docs/DESIGN-2D.md`](docs/DESIGN-2D.md), whose rules it inherits.
- **Theme guideline:** [`docs/THEME-BIBLE.md`](docs/THEME-BIBLE.md). It covers the pillars, palettes, corruption
  and freeing, audio, tone of voice, and the theme tests every feature must pass.

**So far:**
- **Levels.** The level generator (DESIGN-TOPDOWN §4.1): a per-level plan (places, boss, Menace), POIs on a seabed,
  a canyon labyrinth, caves, arches, decoration and a spawn table, and one way down — a blue hole or the boss's
  Crack — ringed by rock shared with the level below (§4.6). Each level is validated and retried per seed, producing
  a `LevelMap`.
- **Simulation** (`Core/Plane`). The plane-locked sim: movement and dash, bubble shots that ease out, rest, merge
  and pop. It also has the Pufferling (corrupted ones to free, healthy ones about the reef) and ambushes, pearls,
  shells and the shop, the dive down the shaft, Menace,
  and Queen Clam, the Depth 1 boss.
- **The corruption war** ([`docs/CORRUPTION.md`](docs/CORRUPTION.md)): ink outgrowth over every level's floor that her
  light cleanses; Blightroot pylons that regrow it and starve when ringed by clean ground; murklings budding from it;
  gloomvines; valves of darkness; the boss arena as the condensation zone. The pufferling enemies are retracted for
  now (`PlaneOptions`).
- **Presentation.** The Godot layer: heightfield chunks, the tilted camera with depth focus, the level below drawn
  through the shaft and the dive into it, HUD and run clock, minimap with fog of war, pause menu, splashes, and the
  F3 debug map.

The game boots into the **title screen**: continue, a new run, a seeded run, the Sea-pedia, statistics, save & load and
settings. **WASD** swims (north is up), **Space** dashes, **Shift** over the shaft dives, the mouse (or arrow keys)
aims and fires, **Esc** pauses, **Tab**
shows the whole level, **F1** the debug menu (jump to any level, switch any pearl on or off; a debug run is not saved), **F3** the debug map, **R** regenerates from the seed field (Enter applies a typed seed).

**Controller** (rebindable with the keyboard and mouse in Settings → Controls, saved to `user://bindings.json`): left
stick swims, right stick aims, **RT** shoots, **LT** dashes, **X** dives, **Y** uses the active pearl, **View (Back)**
holds the level map, **Start** pauses (Start or **B** resumes). In menus the **D-pad** moves, **A** confirms and **B**
goes back. Typing a seed and the debug keys (F1, F3, R) are keyboard only.

## Running

```bash
godot
```

From this folder, `godot.cmd` opens the editor with the real Godot .NET executable
(`godot --run` starts the game). `dotnet test` runs the Core tests without Godot.
Arguments after `--`:
- Level and run setup: `--seed=KELP7Q2Z`, `--cycle=`, `--depth=`, `--level=`, `--pearls=a,b`, `--hp=`, `--boss-hp=`,
  `--calm` (no creatures).
- Where to start: `--at=start|arch|cave|exit|hole|shop|cache|treasure|ambush|mob|boss|vase`.
- Automation: `--autopilot` (swims the shortest route to the exit), `--dive` (dives whenever it can), `--fire`,
  `--use-active` (uses the active pearl whenever it is ready), `--paused`.
- Views and capture: `--map`, `--f3`, `--no-focus`, `--capture=dir --frames=a,b`.
- Debug views: `--dbg-noshadow`, `--dbg-nossao`, `--dbg-nodecor`, `--dbg-nocanopy`, `--dbg-normals`, `--dbg-nobanner`, `--dbg-zoom=metres` (camera distance), `--dbg-slowmo=0.1` (game time slowed, e.g. to watch bubbles pop), `--dbg-surge` (a current surge through her canyon, 1 s in), `--award=id` (shows an achievement's banner; awards nothing), `--reset-profile` (clears the saved profile's achievements and unlocks), `--dbg-follow=mob|fish` (the camera on the nearest pufferling)
  (no boss title card).
- Any of these flags skip the title and start a run that is neither recorded nor saved.
- The title's own review flags:
  - `--title-sample` uses an example profile that is never saved.
  - `--title-card=stats|pedia|creatures|save|settings|seed` opens that card.
  - `--title-launch=new|continue` starts a run after a moment.

## Layout

```
project.godot, OctoShoots.csproj   Godot 4.7 (.NET) project; compiles src/Game
data/items.json       items, synergies, transformations (validated on load)
data/creatures.json   the Depth 1 creatures' numbers (to be ported onto the plane)
src/Core/         pure C# (no Godot types)
  Plane/            the plane-locked simulation: movement, combat, economy, the boss, the run
  Gen/TopDown/      the level generator: LevelMap, terrain, the stages, validation, flood fill
  Gen/SdfMath.cs    distance-field primitives and blends used by the generator
  Items/            item model, catalog, loadout (stat order, synergies, transformations), pools, captions
  Creatures/        creature kinds and their catalog (creatures.json)
  Loot/             pickups, shells, chests, drop tables
  Run/              seed codes and per-system RNG streams
  Saves/            versioned save format, migrations, export codes
src/Core.Tests/   xUnit tests for Core
src/Game/Title/    title screen (the main scene): live-sea backdrop, menu, seed box, Sea-pedia, statistics, save & load, settings
src/Game/TopDown/ the game scene: level, combat and boss views, camera rig, HUD, minimap, menus, splashes
src/Game/Fx/      reusable art: flora, creature, clam and reef meshes, pearl materials, particles, sounds, sun light
src/Game/         controls (InputSetup), settings and save store, conversions
assets/shaders/   the reef's skin (reef_surface), each level's frame and the dive's portal (level_frame), the water pass (topdown_post), Clementine's bell and tentacles, bubble, pearl, boss, map and art shaders
docs/             design documents, the Depth 1 bestiary and milestone checklists
```

## Requirements

- Godot 4.7.2 **.NET** edition
- .NET 8 SDK
