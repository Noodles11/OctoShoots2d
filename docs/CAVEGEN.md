# Cave generator (m2 prototype)

A new level generator in the manner of Watabou's Cave Generator, written from scratch for Ink Deep (Watabou's code is
not published, so nothing is ported). It lives as a standalone prototype in `tools/cavegen/` and is not wired into
the game yet. The page `tools/cavegen/index.html` (built by `tools/cavegen/build.py` from `page.html` and
`cavegen.js`) rolls levels and draws them as the minimap and Tab map will.

## Rules (replacing the old size and shape rules)

- **Area:** reachable water covers **6,000 m², ±15%** (5,100 to 6,900 m²). Measured on the old generator: 5,350 to
  7,200 m², about 6,100 on average. The 140 m square is only a container, with the 8 m impassable rim.
- **Start and way down:** the start is a chamber on one side. The way down is the chamber at the end of the main
  chain, at least **90 m** along the water and **78 m** in a straight line. Boss levels end in a round 15 m arena with
  the Crack (an ellipse, 7–9 by 2.5–3.5 m); other levels end in a 6.5 m blue hole.
- **Places** (carried over from `LevelValidator`): exactly one place 39–47 m from the start along the water; every
  other place farther than that, at least 40 m from the way down and 21 m from the next place. Rooms (shop,
  treasure, secret, curse den) prefer dead-end chambers. At most 5 places (`LevelStocking` caps).
- **Dropped:** the shared entry and exit stamps between neighbouring levels, the 3–5 main routes, the plaza counts and
  the 40% / 45% wall and water ratios.

## Algorithm (`cavegen.js`)

1. **Main chain:** a start chamber 26 m in from a random side; chambers every 16–22 m wander toward a goal near the
   far side (each step blends a random turn of up to ±0.65 rad with the way to the goal); the exit chamber sits at
   the goal. Main tunnels are 6–9 m wide.
2. **Branches:** side chambers (5–9 m) off any chamber but the exit, 14–21 m out, kept clear of the others; a quarter
   also loop back to a near chamber. Branches are added by an area estimate, then measured, until the water reaches
   6,000 m² without passing 6,900.
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

- Rock is paper; water is the depth's shallow water colour (`ReefLook.WaterShallow`); the shaft is its deep water,
  darkening to murk, with a dashed rim.
- The wall line is fineliner: marching squares on the softened water mask, smoothed twice (Chaikin), drawn in ink with
  its weight wavering along it, plus a finer second pass a hair off.
- Hatching: bundles of 2–4 short parallel strokes at random angles in the rock along the walls, densest at the wall and
  thinning out over 6.5 m.
- Place emblems are the game's (`TopoMap.DrawEmblem`), unchanged.

## Bringing it into the game

1. Port `cavegen.js` to `src/Core/Gen/Caves/CaveGenerator.cs`, filling a `LevelMap` (chambers to `Plazas`, tunnels to
   `Corridors`, places to `Pois`, the shaft to `Shaft`), with `LevelPlan` deciding the places.
2. Replace the size rules in `LevelValidator` with the area rule; keep reachability, spacing and the shaft rules.
3. Keep the 3D terrain pass (wall noise, slopes, cave roofs) on the new outline; arches and passes become optional.
4. `TopoMap` / `topo_map.gdshader`: the depth water fill, the fineliner outline and the hatching, as on the page.
5. Rewrite the generator and validator tests.
