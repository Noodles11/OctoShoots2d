# Ink Deep

A top-down action roguelite in a darkening ocean. Design: [`docs/DESIGN-TOPDOWN.md`](docs/DESIGN-TOPDOWN.md)
(authoritative), on top of [`docs/DESIGN-3D.md`](docs/DESIGN-3D.md) and [`docs/DESIGN-2D.md`](docs/DESIGN-2D.md).

**Top-down, so far:** the level generator (DESIGN-TOPDOWN §4.1: ocean terrain → POIs fitted into it → path
network → carving → arches, caves and passes → decoration → spawn table, validated and retried per seed) producing
a `LevelMap`; the
plane-locked movement sim (`Core/Plane`); and the Godot presentation: heightfield chunks, the tilted camera,
a placeholder bell, canopy fade under arches and cave roofs, POIs in the landmark colours, the F3 debug map.
**WASD** swims (north is up), **Shift** dashes, **F3** shows the whole level, **R** regenerates from the seed
field (Enter applies a typed seed).

**Kept from the first-person game** (`--legacy-fp`): combat, items, creatures and the rest of the 3D build,
until each system is ported to the plane (DESIGN-TOPDOWN §11.1).

## Running

```bash
godot
```

From this folder, `godot.cmd` opens the editor with the real Godot .NET executable
(`godot --run` starts the game). `dotnet test` runs the Core tests without Godot.
Arguments after `--`: `--seed=KELP7Q2Z`, `--depth=`, `--reef=`, `--f3` (map open), `--at=arch|cave|rift`,
`--autopilot` (swims the shortest route to the rift), `--capture=dir --frames=a,b`, and `--legacy-fp`
for the first-person build.

## Layout

```
project.godot, OctoShoots.csproj   Godot 4.7 (.NET) project; compiles src/Game
data/items.json   60 items, 14 synergies, 5 transformations (validated on load)
src/Core/         pure C# simulation (no Godot types)
  Sim/              fixed 60 Hz World: player, creatures, weapon + shot modifiers, statuses, actives, nests
  Items/            item model, catalog, loadout (stat order, synergies, transformations), pools, captions
  Run/              seed codes and per-system RNG streams
  Saves/            versioned save format, migrations, export codes
  Gen/TopDown/       top-down level generator: LevelMap, terrain noise, the stages, validation, flood fill
  Plane/            plane-locked simulation (2D positions on a fixed swim band); movement so far
  Gen/              open-sea reef generator of the first-person game
  Loot/             pickups, shells, chests, drop tables
  Terrain/          voxel SDF (craters), grey-box cave, surface-nets meshing, reachability
src/Core.Tests/   xUnit tests for Core
src/Game/TopDown/ top-down entry scene: level view, camera rig, bell placeholder, F3 debug map
src/Game/         first-person Godot layer (legacy): scene, camera + viewmodel, terrain meshes, FX, HUD, debug panel
assets/shaders/   terrain, bubble, pearl, mist, sea surface and screen shaders
docs/             design documents, the Depth 1 creature/boss proposal and milestone checklists
```

## Requirements

- Godot 4.7.2 **.NET** edition
- .NET 8 SDK
