# M0 — Feel prototype: go/no-go checklist

Step 1 of the delivery plan in [`DESIGN-3D.md`](DESIGN-3D.md) §12. The question M0 answers:
**does swimming and shooting in first person feel good enough to build the game on?**
If not, we fix the feel (or the camera decision) before any content work.

## What is in the build

- Grey-box cave (F1 → Level → Grey-box cave; the game now starts in a generated reef) from a voxel SDF: one ~24×16×24 m chamber with a flat floor, two pillars, an arch,
  an overhang, a tunnel to a side pocket and a chimney up to a small upper pocket.
  Triplanar 1 m / 4 m grid on the rock so speed and distance read clearly.
- Clementine, first person: swim, slow sink, jet start, sharp-turn jet, ink dash with ink cloud,
  inertia, sphere collision sliding along rock, warm glow light, one throwing tentacle.
- Bubbles thrown from the tentacle: visible, slow, physical, range-limited; 30 % velocity inheritance;
  8 on the suckers, regrowing after a short pause.
- Aim assist Off / Low / Medium / High: magnetism, bullet bend, sensitivity slowdown.
- Two creatures. The **clownfish ninja** (see DESIGN-3D §7.5) hides among neutral clownfish at anemone
  nests and throws tiny starfish after a 0.5 s wind-up. The **Lanternfish** (swimmer; moved to Depth 5, off by default): keeps ~7 m away, orbits, fires a slow glowing orb after a
  0.45 s telegraph (glow ramp + rising positional tone). At most 3 off-screen attackers at once;
  red danger markers at the screen edge for off-screen telegraphs and incoming orbs.
- 100 HP, hurt feedback (magenta edge pulse, chromatic wobble, arm jolts, shake), respawn on death.
- **F1 tuning panel**: every movement, dash, shooting, aim-assist, enemy and view number is live.

Not in M0: third person, items, destructible terrain, flow-field pathing, art pass, real audio.

## Running it

1. Open **Godot 4.7.2 (.NET)** from the Start menu (not the `godot` command on PATH — see below).
2. *Import* → pick `project.godot` in this folder → *Import & Edit*.
3. Press **F5** (the first run builds the C# projects).

Or from a terminal, with the real executable:

```bash
"<godot install dir>/Godot_v4.7.2-stable_mono_win64.exe" --path .
```

The `godot` command that WinGet puts on PATH is a link; the .NET build of Godot can't find its
`GodotSharp` folder through it and crashes with "Assemblies not found".

Tests (no Godot needed): `dotnet test`

## Controls

| Action | Key |
|---|---|
| Look | Mouse |
| Swim / strafe | W A S D |
| Rise / descend | Space / Ctrl |
| Shoot (hold for auto-fire) | LMB |
| Ink dash | Shift |
| Ink bomb | E |
| Review pearls (hold; wheel = next) | Tab |
| Use active item | F |
| Debug panel (items, seed, saves, tuning) | F1 |
| Pause | Esc |
| Restart | Hold R |

## Play session

Play at least **two 10-minute sessions** on different days, Medium aim assist, default tuning first.
Then use F1 to try the obvious knobs (SwimSpeed, AccelTime/StopTime, JetMultiplier, DashMultiplier,
aim-assist cones, mouse sensitivity, FOV) and press **Save** on anything you like better.

## Go criteria — all must pass

| # | Criterion | How to check |
|---|---|---|
| 1 | **Swimming is responsive, not floaty.** Starting, stopping and strafing around a pillar feel immediate. | Weave between the two pillars and through the arch at full speed. |
| 2 | **Level horizon holds.** Looking straight up/down and swimming never disorients; no roll. | Swim up the chimney into the upper pocket and back down. |
| 3 | **No motion sickness** after 10 minutes. | Note any discomfort and which options helped. |
| 4 | **Collision never snags or tunnels.** Sliding along walls, floor and ceiling is smooth; dashing into rock never passes through. | Dash into walls at angles; hug the overhang underside; sink onto the floor. |
| 5 | **Slow sink and jet start read clearly.** Idle → gentle drift down and soft landing; first stroke → visible burst. | Let go of the keys mid-chamber; then tap W. |
| 6 | **Dash reliably dodges a telegraphed orb** once you've learned the tone. | Wait for the rising tone, dash sideways: ≥ 8 of 10 dodges. |
| 7 | **Shots feel physical and fair.** Slow enough to see, fast enough to hit; range end is readable. | Hit a strafing lanternfish at 5–10 m. |
| 8 | **Medium aim assist helps but doesn't play for you.** Off feels hard but fair; High feels sticky but not robotic. | Compare Off / Medium / High for a few minutes each. |
| 9 | **Off-screen attacks are fair.** You always hear/see a warning before being hit from behind. | Set EnemyCount to 6; note any hit you didn't see coming. |
| 10 | **60 FPS** in normal play on your machine. | F1 readout. |
| 11 | **The clownfish ninja is fair and findable.** You can spot the headband among the school, the wind-up gives time to dodge, a dash or sidestep avoids the star, and normal clownfish never feel like targets. | F1 → Nearest nest; approach, spot it, dodge ten stars. |

## Outcome

- **Go** → step 2 (Core port: RNG, stats, items, modifiers, saves). Send the saved
  `m0_tuning.json` (`%APPDATA%\Godot\app_userdata\Clementine's Quest 3D\`) so the tuned numbers
  become the new defaults in the design doc.
- **No-go** → list the failing criteria; we iterate on feel (or revisit the camera decision)
  before moving on.
