# Cave generator (m2)

A new level generator in the manner of Watabou's Cave Generator, written from scratch for Ink Deep (Watabou's code is
not published, so nothing is ported). It lives as a standalone prototype in `tools/cavegen/` and is not wired into
the game yet. The page `tools/cavegen/index.html` (built by `tools/cavegen/build.py` from `page.html` and
`cavegen.js`) rolls levels and draws them as the minimap and Tab map will.

## Rules (replacing the old size and shape rules)

- **Area:** reachable water covers **4,000 m², ±15%** (3,400 to 4,600 m²), about 4,150 on average. The 140 m square is only a container, with the 8 m impassable rim.
- **Start and way down:** the start is a chamber on one side. The way down is the chamber at the end of the main
  chain, at least **55 m** along the water (shortcuts taken; the route itself runs ~70–80 m) and **56 m** in a
  straight line (both chambers 38 m in from their sides of the square). Boss levels end in a round 15 m arena with
  the Crack (an ellipse, 7–9 by 2.5–3.5 m); other levels end in a blue hole: the exit chamber sloping down from its walls to its middle, where she dives.
- **Places** (`CaveLayout.FromStart` and the rest, which `LevelValidator` checks): exactly one place 30–38 m from the
  start; every other place at least 30 m from the start and the way down and 17 m from the next place (rooms 20 m).
  Open places take a side chamber first, so the naves hold them (DESIGN-TOPDOWN §4.9). Rooms (shop,
  treasure, secret, curse den) prefer dead-end chambers. At most 5 places (`LevelStocking` caps).
- **Dropped:** the shared entry and exit stamps between neighbouring levels, the 3–5 main routes, the plaza counts and
  the 40% / 45% wall and water ratios.

## Algorithm (`cavegen.js`)

1. **Main chain (the route the level is built on):** a start chamber 38 m in from a random side; chambers every
   16–22 m zig-zag toward a goal near the far side (each step bends 0.55–0.9 rad off the way to the goal, the other
   way from the last four times in five); the exit chamber sits at the goal. Main tunnels are 6–9 m wide. The
   special rooms follow (14–21 m off a chamber). The level's **shortcuts** are bored once the rock stands: tunnels
   through the rock, roofed by it (TopDownGenerator's passes, DESIGN-TOPDOWN §4.1 Stage 4), the way down kept at
   least 55 m along the water.
2. **Branches, close to the route:** side chambers (5–9 m), 11–16 m out, their middles within 24 m of the route, two
   in three off a route chamber and the rest off one of those (at most two deep), kept clear of the others; a
   quarter also loop back to a near chamber, the route's first. Branches are added by an area estimate, then measured, until the water reaches
   4,000 m² without passing 4,600.
3. **Shape:** every chamber is a blob whose radius wavers by four low harmonics; every tunnel is a curved tube
   (quadratic Bézier) whose width breathes along it. Their signed distance field, roughened by two octaves of
   value noise (1.6 m and 0.6 m), gives the craggy outline; a 1.6 m core keeps each tunnel swimmable. Two
   majority-vote passes smooth the grid, and only water joined to the start is kept.
4. **Checks:** 8-neighbour Dijkstra from the start gives the path to the way down and the place distances. A level
   that misses a rule tries again (up to 60 attempts); in practice almost all pass on the first.
5. **Heights:** the 1 m grid the game uses: water at −1 (±0.2), rock rising 1.5 m per metre from the wall to a peak
   of 8–14 m, and the shaft cut down to −9.

**Output** mirrors `LevelMap`: `Size`, `Samples`, `Heights`, `Start`, `Exit`, `Pois`, `Plazas` (chambers),
`Corridors` (tunnels: Main, Side, Pass), `Shaft`, `HasBoss`. **Randomness** is the same xoshiro256** through
SplitMix64 as `OctoShoots.Core.Rng` (with `Stream(name)`), and the noise is integer-hash value noise, so the
algorithm ports line for line to C#.

On 60 test seeds (every fifth a boss level): all pass, 5,170–6,244 m² (about 5,850 on average), 90–110 m from start to
way down, every planned place placed, about 45 ms a level in Node.

## The map look

- Rock is the depth's reef rock in a stone tone (`TopoMap.RockTone`: `ReefLook.Rock` greyed toward stone and a shade
  darker), never paper white; water is the depth's shallow water colour (`ReefLook.WaterShallow`); the shaft is its deep water,
  darkening to murk, with a dashed rim.
- The wall line is fineliner: marching squares on the softened water mask, smoothed twice (Chaikin), drawn in ink with
  its weight wavering along it, plus a finer second pass a hair off.
- Hatching, as on a hand-inked cave map: a dense band hugging the wall on the rock side (about 17 px deep on the
  minimap, 11 px on the full chart, its outer edge ragged), laid along the outline as bundles of 3–5 parallel strokes
  at random angles, 7–12 px long and about 2.7 px apart. It is laid out in screen pixels, like a pen's fixed nib, so it
  keeps its texture at any zoom. A soft grey shadow sits just outside the wall line, cast down and to the right, and
  small inked pebbles are scattered along the walls on both sides.
- Place emblems are the game's (`TopoMap.DrawEmblem`), unchanged.

## In the game

- `src/Core/Gen/TopDown/CaveLayout.cs` is the C# port. `TopDownGenerator.LayCave` hands it the plan's places and records
  the cave: chambers as `Plazas`, tunnels as `Corridors` (Main, Side, and each special room's Spur), places as `Pois`.
  `RaiseCaveWalls` raises the rock from the water mask; the caves, passes, arches, slopes, shaft, decoration, loot and
  spawns are the old stages, unchanged.
- Special rooms (shop, secret, clam room, curse den) are laid right after the main chain, before the side branches:
  dead-end chambers left as rock with rock kept round them, their tunnel ending at the room's mouth; the old cave
  carving hollows them out behind it.
- `LevelMap.Size` is 140. `LevelValidator` checks the area rule (`WaterArea`, 3,400–4,600 m²) instead of the old
  corridor, plaza, side-canyon, open-share and stamp rules; reachability, spacing, the early place, the 55 m way down,
  the shaft, peaks, caves and loot rules stay.
- The minimap and the Tab map (`TopoMap`, `topo_map.gdshader`, `edges_only`) draw the look above in the shader: the
  heights texture carries the height and the distances into rock and into water, for the hatching band (Voronoi
  bundles of strokes at their own angles, laid in screen pixels) and the walls' shadow.
- In 200 test levels every one generates, in 1.6 attempts on average (about 0.1 s a level).
