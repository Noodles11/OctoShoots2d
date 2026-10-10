# Light Bubbles

Bubbles stay bubbles: same flight, physics and merge math. But each one carries light, a small warm lantern
inside a water membrane, exhaled from her bell. This is a presentation-only change. There are **no changes to
`PlaneWorld`**: flight, hover, merging, items, damage and hit radii are untouched.

## Fiction (Sea-pedia)

> *Her bloom's light was too much for one bell to hold. What she cannot carry, she exhales — bubbles of light, each a
> little of everyone she lost, sent into the dark to do one bright thing before it goes out.*

The light is what washes corruption off. A bubble's glow passes over a creature; when its HP runs out, its own light
reignites, kindled by hers. Corrupted creatures flinch from light.

## What is built

| Part | Where | What |
|---|---|---|
| Lantern core | `plane_bubble.gdshader` (`lantern`, `warmth`, `base_light`) | A hot gold centre (#FFC46B, the gonads' gold) and a soft warm fill through the film; the iridescent rim stays. Capped at `CORE_MAX` 1.12, below her bell's peak. |
| Brightness = damage | `CombatView.CoreLight` / `Warmth` | 0.32 for one bubble, ×1.35 per bubble merged in, capped at 1. Warmth goes from gold to white-gold over 1→15 bubbles. A full bubble's rainbow sheen becomes a faint corona around a small star. |
| Glow in the water | `bubble_halo.gdshader`, one pooled quad per shot | An additive billboard of a fixed 0.8 m radius. Its strength grows with merging, never its size: 0.16 → 0.4 (≤ 40 % of her halo). |
| Light on the world | `topdown_post.gdshader` (`bubble_lights[16]`, `bubble_count`) | Each bubble adds a 1.5 m halo on the swim layer at ≤ 0.35 of her halo strength. The 16 brightest within 25 m are sent; the rest glow on their own only. The halo uses her depth scaling (`0.6 + 0.8 × menace`), so it is subtle in the Shallows and strong in the deep. |
| Pop | `CombatView.Pop` | The lantern flares at ×1.5 for 150 ms and dies; droplets break off warm. It leaves a 1.5 m bloom of light that fades over 1 s (the same light array). |
| Her bell spends light | `BellView.Spend`, `clementine_bell.gdshader` `breath` | Each volley spends 0.07 per bubble of a light reserve, which rekindles at 0.45/s. The bell's interior glow (and her light) eases between 0.85 (spent) and 1.0. She is never dark. |
| Ammo orb | `BellView` charge orb | There are no ammo orbs on the plane yet. The orb that does exist, Pearl Diver's charge, is now the same lantern as her bubbles; a charged throw flies as a bright lantern bubble. |
| Merge feedback | `CombatView.Merged`, `Sfx` `merge` | A 200 ms light pulse at the merge point and a glassy chime, a semitone higher for every bubble the merged one holds (at most one chime per 60 ms). Bubbles hanging at the end of their range breathe slowly like embers. |
| Kill escalation | `CombatView.Kill`, `CameraRig.EdgeFlash`, `Sfx` `bell_ring` | The kill bloom's strength, life and reach (≤ 3 m) grow with the killing bubble's size. A kill by a bubble of 6 or more lifts the screen's edges toward white for 60 ms and rings a bell. The edge flash is skipped with reduced motion. |
| Light returning home | `PufferlingView` freed poof | The freed fish's burst is little lanterns. For 0.5 s they drift toward Clementine (re-aimed as she moves), then rise away. |

## Guardrails (all enforced in code)

- A bubble's halo is ≤ 40 % of hers. Ground light radius is 1.5 m, and kill blooms are at most 3 m: brightness grows,
  radius does not.
- The kill flash lasts 60 ms, reaches only the screen's edges, and is off with reduced motion.
- Bubble cores are capped below her bell's peak, so she stays the warmest light.
- Bubble light scales with depth darkness like hers.

## Not built (phase 2)

- The corruption tell (§7): corrupted creatures darkening away from nearby bubble light, and healthy ones warming in
  it.
