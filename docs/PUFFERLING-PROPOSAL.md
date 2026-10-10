# Pufferling — the first proper fish of Depth 1

> **Built** (the decisions are in §7). The rules now live in DEPTH1-BESTIARY and DESIGN-TOPDOWN §12.2; this page keeps
> the design and its reasoning. The first real creature on the plane. It replaces the placeholder shooting dots (all of them
> become corrupted pufferling) and brings healthy pufferling to the reef. It is built to the same standard as Clementine:
> its own baked mesh, its own shader, smooth animation driven by the sim. The numbers are first guesses for play.
> §7 lists what needs a decision. Where it differs from the Pufferling in DEPTH1-BESTIARY, §6 says so; nothing
> changes silently. It keeps the bestiary's name, **Pufferling**, and replaces the bestiary's design.

## 1. The two pufferling

| | Healthy pufferling | Corrupted pufferling |
|---|---|---|
| Who | Reef life. Never a target: bubbles pass through it; it never shows on the minimap. | The Leak's version. A target; freed when its health runs out. |
| Colours | Natural: a warm sand-olive back with dark brown spots, a cream belly, faint gold stripes on the cheeks. | Tank colours: starfire red #FF4D5E with acid-lime spots #7CFC4D. Spots where a pufferling has none, too even (a pattern, not a fish). A glossy plastic sheen, and a slow warm shimmer (the Leak) that shows through the mist. |
| The tank piece | — | A small **pet-shop price tag** clipped to its tail fin (THEME-BIBLE §6.4), fluttering as it swims. |
| Eyes | Big, round, dark, a gold ring: curious. | The same round eyes (Depth 1 is Menace 0, so no slits yet), but with a heavy brow line, and the pupils track Clementine. |
| Behaviour | Swims about, unbothered by Clementine. | Swims the same way until she comes within reach; then faces her, blows up and fires needles (§3). |

## 2. The body (one mesh, one shader, both versions)

Built like Clementine (`BellView` and `clementine_bell.gdshader`): a procedural mesh baked once, and a shader that
does the drawing and the motion from a few values the view sets each frame.

- **Size.** About **1.1 m** nose to tail when calm (a little longer than Clementine's bell is wide, so it reads at
  play distance); blown up, a ball about **2.2 m** across.
- **Shape.** A blunt, rounded head with a small beak of a mouth (two pale "teeth" plates), a plump body tapering to a
  narrow tail stem. Inflation blends every body vertex from this shape toward a sphere; the fins, eyes and mouth stay
  where they belong on the surface, so it blows up as a ball, not as a balloon of the fish.
- **Eyes.** Two domed eyes high on the head, each its own little hemisphere: a gold ring, a dark pupil, a wet
  highlight. They bulge a little when it inflates.
- **Fins** (this is what makes it swim like a pufferling, not a torpedo):
  - **Pectorals:** two small fan fins just behind the eyes, fluttering fast (~6 beats a second): they do the
    hovering and the fine steering.
  - **Dorsal and anal fins:** set far back, top and bottom, rounded; they fan from side to side together, which is
    what pushes it forward.
  - **Tail fin:** a rounded fan on the tail stem, a slow side-to-side sweep, mostly a rudder. It bends into turns.
- **Spines.** About **60 short spines** over the body. Calm, they lie flat along the skin and read only as a faint
  speckle. Blown up, they stand straight out from the ball. When it fires, the spines on its skin shrink away (the
  needles have left), and grow back over the cooldown.
- **Skin shader.** Countershading (dark back, pale belly), the spot pattern, a fine scale texture, a soft rim light,
  and the sun's caustics on its back (`sunlight.gdshaderinc`, as the reef). It casts a real shadow on the seabed. The
  corrupted version swaps the palette, adds a plastic specular and the slow warm emission pulse. A hit flashes it.
- **Motion in the shader.** A gentle body wiggle in time with the dorsal and anal fins; the pectorals' flutter; the
  tail's sweep and its bend into turns; a slow bob up and down on the swim plane. All of it scales with its speed.

## 3. Behaviour

### 3.1 Swimming (both versions)

- **Wandering.** It picks a spot a few metres away in open water (no rock in between), swims there at an easy
  **1.5 m/s**, hangs there for 1–4 s (fins working, slowly turning, sometimes nibbling at the rock), then picks the
  next. It stays within about 12 m of its home spot.
- **Smooth turning.** It turns at up to **120°/s**, easing in and out of turns, and its body banks a little into
  them. It never snaps to a new heading.
- **Unbothered.** The healthy one ignores Clementine completely: it does not flee, and her bubbles pass through it.

### 3.2 Attacking (corrupted only)

1. **Notices her** within **10 m** with a clear line of sight. It stops wandering and **turns smoothly to face her**
   (about 0.3 s), then keeps facing her. It blows up when she is within its reach, **8 m**.
2. **Blows up (the telegraph, 0.8 s).** It swells quickly but visibly from 1× to 2× its size, its spines rising as
   it goes. It keeps turning to face her. At Depth 1 the telegraph is 0.8 s (never below 0.45 s; DEPTH1-BESTIARY §1).
3. **Fires 8 needles.** Evenly spaced around it, every 45°, with the whole ring turned by a **random angle** each
   time, so she cannot learn where the gaps will be. The spines vanish from its skin as they fly. It stays round.
   - **Needles** are big hot-pink spikes (about 1 m long, lime at the point, glowing), easy to see and to dodge.
     They fly at **20 m/s** (a little faster than her bubbles' 17 m/s throw) for **14 m**, and hurt **8**.
   - **A needle pops a bubble** it touches, and flies on.
   - Rock stops a needle.
4. **Stays round for 0.5 s**: the moment it is easiest to hit.
5. **Cooldown, 3 s.** It backs away about **3 m** from her (slowly, still facing her), shrinks back to its calm
   size over about 1.2 s, and its spines grow back. This is her window to close in and attack.
6. If she is still within reach after the cooldown, it starts again from 2. If she has left (beyond **16 m**, or out
   of sight), it goes back to wandering.

- **It drifts closer.** Off cooldown, with her in sight but beyond its reach (8 m), it drifts toward her at
  **1.2 m/s**, still facing her, until she is within reach. It never charges: slow enough that she can always back
  away, quick enough that hiding at the edge of its reach does not work for long.
- **Its spines hurt on touch** while it is blown up: **4** (half a needle), so she cannot just hug it.
- **Shooting it** at any point hurts it. It has **60 HP**, so six of her plain bubbles free it (twice the dots
  it replaces).

### 3.3 Only in the swim plane

The needles fly only in the swim plane: the 8 that matter, and nothing more (no decorative needles up, down or
diagonally — they cluttered the view and made the real ones hard to read).

### 3.4 Being freed

When its health runs out (THEME-BIBLE §6.5, the freeing moment):
1. It stops and shudders for 0.3 s as the tank colours wash out of it.
2. A poof of bright bubbles; the price tag comes off and sinks, spinning.
3. Its natural colours come back, it deflates, and it drops its shells (as now).
4. It swims away slowly as a healthy pufferling, and once it is off the screen it is gone. It never helps her.

## 4. Where they live

- **Corrupted pufferling** take every spot the shooting dots have now: the level's spawn table first, then spots in
  the open water she can reach, up to the level's count (16 at Depth 1, more with Menace), 10 m apart and 30 m clear
  of the start. Ambushes spring pufferling too (§7, Q5).
- **Healthy pufferling:** **8–12 per level**, in ones, twos and threes, near reef walls and in the plazas, away from
  the start's clearing.

## 5. How it would be built

- **Core** (`PlaneCombat.cs` and a new `PlaneFish.cs`):
  - A creature kind on the plane mob (`Pufferling`, corrupted or healthy).
  - The wandering brain: home spot, targets in open water, smooth turning.
  - The attack state machine: notice → face → inflate → fire → hold → cooldown.
  - Needle shots that pop her bubbles.
  - Healthy fish (never targets) and the freed ones swimming off.
  - All deterministic, seeded per level, and tested.
- **Game:**
  - `PufferlingView` (mesh baking, per-fish animation state, the price tag, the freeing).
  - `pufferling.gdshader`, and needle visuals.
  - `CombatView` draws pufferling instead of dots.
- **Docs:** the pufferling goes into DEPTH1-BESTIARY in place of the Pufferling (Q1), and DESIGN-TOPDOWN §12.2's
  placeholder-combat notes change to match.

## 6. Differences from the Pufferling in DEPTH1-BESTIARY

| Pufferling (bestiary) | Pufferling (this proposal) |
|---|---|
| Chases her at 3 m/s, then 4 m/s swollen | Holds its ground; turns to face her; backs off after firing |
| Swells to 2.4×, chases 1.6 s, then **bursts into 12 spines** | Swells to 2×, fires **8 needles** with a random turn, stays round, then deflates |
| A hit while swollen bursts it at once | A hit only hurts it |
| 21 HP, 8 per spine, 8 m/s spines | 60 HP, 8 per needle, 20 m/s needles that pop bubbles |
| Yellow, in groups of 1–3 | Natural colours (healthy) or starfire red with lime spots (corrupted) |

## 7. Decisions

| # | Question | Decision |
|---|---|---|
| Q1 | Replace the Pufferling in the Depth 1 bestiary with this behaviour? | Yes, keeping the name **Pufferling** |
| Q2 | Hold its ground, or drift closer when she is in sight but out of reach? | **Drift closer** (1.2 m/s, §3.2) |
| Q3 | Does a needle fly on after popping a bubble? | Yes |
| Q4 | Do its spines hurt on touch while blown up? | Yes, **4** (half a needle) |
| Q5 | Ambushes | 3–4 pufferlings, their first bursts staggered |
| Q6 | How many healthy pufferlings? | 8–12 a level for now |
| Q7 | Build the freeing now? | Yes |
| Q8 | Numbers | As in §3, tuned in play |
