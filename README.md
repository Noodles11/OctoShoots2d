# Ink Deep

A top-down action roguelite in a darkening ocean.
- **Design:** [`docs/DESIGN-TOPDOWN.md`](docs/DESIGN-TOPDOWN.md) is authoritative. It builds on
  [`docs/DESIGN-3D.md`](docs/DESIGN-3D.md) and [`docs/DESIGN-2D.md`](docs/DESIGN-2D.md), whose rules it inherits.
- **Theme guideline:** [`docs/THEME-BIBLE.md`](docs/THEME-BIBLE.md). It covers the pillars, palettes, corruption
  and freeing, audio, tone of voice, and the theme tests every feature must pass.

**So far:**
- **Levels.** The level generator (DESIGN-TOPDOWN §4.1): POIs on a seabed, a canyon labyrinth, trenches, caves,
  arches, decoration and a spawn table. Each level is validated and retried per seed, producing a `LevelMap`.
- **Simulation** (`Core/Plane`). The plane-locked sim: movement and dash, bubble shots that ease out, rest, merge
  and pop. It also has placeholder mobs and ambushes, pearls, shells and the shop, rooms joined through the rift,
  and Queen Clam, the Depth 1 boss.
- **Presentation.** The Godot layer: heightfield chunks, the tilted camera with depth focus, HUD and run clock,
  minimap with fog of war, pause menu, splashes, and the F3 debug map.

The game boots into the **title screen**: continue, a new run, a seeded run, the Sea-pedia, statistics, save & load and
settings. **WASD** swims (north is up), **Space** dashes, the mouse (or arrow keys) aims and fires, **Esc** pauses, **Tab**
shows the whole level, **F3** the debug map, **R** regenerates from the seed field (Enter applies a typed seed).

## Running

```bash
godot
```

From this folder, `godot.cmd` opens the editor with the real Godot .NET executable
(`godot --run` starts the game). `dotnet test` runs the Core tests without Godot.
Arguments after `--`:
- Level and run setup: `--seed=KELP7Q2Z`, `--depth=`, `--room=`, `--pearls=a,b`, `--hp=`, `--boss-hp=`.
- Where to start: `--at=start|arch|cave|rift|gate|shop|cache|treasure|ambush|mob|boss`.
- Automation: `--autopilot` (swims the shortest route to the rift), `--fire`, `--paused`.
- Views and capture: `--map`, `--f3`, `--no-focus`, `--capture=dir --frames=a,b`.
- Debug views: `--dbg-noshadow`, `--dbg-nossao`, `--dbg-nodecor`, `--dbg-nocanopy`, `--dbg-normals`, `--dbg-nobanner`, `--dbg-zoom=metres` (camera distance)
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
assets/shaders/   the reef's skin (reef_surface), the water pass (topdown_post) and the beneath-layer, Clementine's bell and tentacles, bubble, pearl, boss, map and art shaders
docs/             design documents, the Depth 1 bestiary and milestone checklists
```

## Requirements

- Godot 4.7.2 **.NET** edition
- .NET 8 SDK
