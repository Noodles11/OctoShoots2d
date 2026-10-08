# Achievements that unlock pearls — proposal

> A proposal, not built yet. It plans pearl unlocks through achievements and the banner that announces them.
> Open questions are in §7. A playable motion prototype of the banner accompanies it.

## 1. The idea

A run starts with a **basic set** of pearls. Treasure rooms, shops and Queen Clam can offer any of them. Each
achievement unlocks **one specific pearl**, which joins the offers from then on. Unlocks are permanent: they are
saved in the profile across runs.

This uses machinery that already exists:
- `ItemDef.Unlock` in items.json names the achievement a pearl waits for.
- `ItemPools.IsUnlocked` checks it.
- `Profile.Achievements` and `Profile.Award(id, customSeed)` record it. Seeded runs never award anything (the Isaac
  rule, DESIGN-2D §13.1).

What is new:
- The plane game reading and writing the profile.
- The five achievements below.
- Their five pearls, made to work on the plane.
- The banner.

## 2. Pearls: the basic set and the unlocks

**Basic set (8).** These are available from the first run:
- Triple Tentacle, Hammerhead, Anglerfish Lure, Swordfish Bill, Mirror Scale, Boomerang Shrimp and Double Helix.
  These are the 7 already on the plane; Hammerhead loses its `ringmaster` lock.
- **Plankton Swarm** (new on the plane): a 3-bubble spray of small, weaker bubbles. It is needed for *Bubble Bath*:
  1 + 2 (Triple) + 4 (Hammerhead) + 2 (Plankton) + 1 (Helix) = **10 bubbles in one volley**.

**Unlocked by achievements (5).** Each one fits the feat that earns it:

| Achievement | Line | Unlocks | On the plane |
|---|---|---|---|
| **Big Bubble Energy** | candy | **Starfish Arm** — *Grows back bigger* | bubbles swell as they fly, up to 2× size at the end of their range |
| **Bubble Bath** | candy | **Mitosis** — *One becomes two* | a bubble that pops on a foe or rock splits into two half-damage bubbles at ±40° |
| **Shucked in Fifteen** | dark | **Giant Squid Eye** — *It sees your weak spot* | 10% of hits do triple damage (seeded) |
| **Untouchable** | dark | **Lucky Sea Glass** — *Found only on moonlit tides* | +2 luck: every foe drops 2 more shells |
| **Hermit Hoarder** | dark | **Pirate's Doubloon** — *Finders keepers* | +15 shells on pickup; shell caches hold 50% more |

*Untouchable → Lucky Sea Glass* is the mapping items.json and DESIGN-2D §732 already have. Mitosis
(`three_synergies`) and Giant Squid Eye (`mother_angler`) are remapped from achievements whose bosses and systems do
not exist on the plane yet.

## 3. The achievements

The checks are deterministic and run in Core (`PlaneAchievements`, fed by world events and room stats). Each is
awarded once per profile; a run on a custom seed never awards one.

| Id | Title | Earned when | Copy (shown on the banner) |
|---|---|---|---|
| `big_bubble_energy` | Big Bubble Energy | One hit from a full bubble (15 merged) defeats a foe that was at full health. | "Fifteen bubbles walked into a fish. Only the bubbles walked out." |
| `bubble_bath` | Bubble Bath | She throws 10 or more bubbles in a single volley. | "Ten at once. Clementine is no longer a jellyfish. She is a jacuzzi." |
| `shucked_in_fifteen` | Shucked in Fifteen | A boss is freed within 15 s of landing. | "She rehearsed a whole second phase. Nobody will ever see it." |
| `untouchable` | Untouchable | She goes through the rift's gateway without having taken damage in that room. | "Not a scratch. Somewhere in the dark, the reef is taking notes." |
| `hermit_hoarder` | Hermit Hoarder | She collects 100 shells in one room. | "More shells than a hermit crab could live in. The hermit crabs have noticed." |

**Feasibility today.**
- **Big Bubble Energy:** reachable. A full bubble hits for 150 against a mob's 30 HP; the hard part is the setup.
- **Bubble Bath:** reachable with the four multishot pearls above.
- **Untouchable:** reachable.
- **Shucked in Fifteen:** needs about 100 damage per second while Queen Clam is open. A pearl-less Clementine does
  about 45, so it is a build-check achievement by design.
- **Hermit Hoarder:** **not reachable yet.** A fully cleared room yields about 50–60 shells. See §7.1.

## 4. The banner

### 4.1 Layout

The card is a ticket with a tear-off stub, at the bottom centre of the screen, clear of the HP bar, the shells and
the minimap. At 1600×900 it is about 760 × 170 px. Play does not pause behind it.

```
┌──────────────────────────────────────────┬┄┬──────────────────────────────┐
│ ACHIEVEMENT ◆                            │┆│  ╭──╮  NEW PEARL              │
│ Big Bubble Energy                        │┆│  │◉ │  Starfish Arm             │
│ "Fifteen bubbles walked into a fish.     │┆│  ╰──╯  Grows back bigger      │
│  Only the bubbles walked out."           │┆│        + bubbles swell in flight│
└──────────────────────────────────────────┴┄┴──────────────────────────────┘
  ▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔ (hold timer, drains)
```

- **The main card** carries the eyebrow, the title and the humorous line.
- **A perforated seam** joins it to the stub.
- **The stub** is the pearl's: a socket with the live 3D pearl (its own shader and colours), with the name, tagline
  and effect lines beside it.

### 4.2 Look

There is one bright style for every achievement; only the line on the card is candy or dark, depending on the
achievement.

- **The card** is pearl-white with a coral rim and a sprinkle of confetti dots.
- **The eyebrow** is coral, with a mint diamond.
- **The stub** holds the pearl in a white socket, with slowly turning pastel rays behind it, and a mint "NEW PEARL"
  chip.
- **The smoke** is bright and pastel: white, pink, mint and butter.
- **Sound (Sfx):** a pop on the way in, then a rising four-note chime; a soft whoosh and a small pop on the way out.

### 4.3 Timeline

| ms | Beat |
|---|---|
| 0–260 | A bubble rises from below to the card's centre (ease-out-back), wobbling. |
| 260 | **Pop.** A ring shockwave (400 ms) and 14 sparkles; the card scales 0.4 → 1.06 → 1 (spring, 420 ms). |
| 340–700 | The eyebrow slides in from the left, unskewing (−12° → 0). |
| 420–950 | The title drops in letter by letter, 22 ms apart (from −14 px and ±6°, overshooting). |
| 700–1000 | The line fades up 8 px. |
| 820–1250 | The stub flips in (90° → 0); the pearl drops into the socket with a squash. |
| 1000–1450 | The pearl's name, tagline and effect lines arrive 80 ms apart; a glint sweeps the pearl at 1200. |
| 1450–5200 | Hold. The timer line drains along the bottom; the pearl turns slowly. |
| 5200–5950 | **Smoke exit.** About 26 soft puffs bloom along the card's length, swell to 2.6× and drift up as they fade (each 640–840 ms, the middle first). Inside them the card fades out over 340 ms, growing 4% and rising 8 px. The pearl rises 200 px out of the smoke in a bubble and pops into sparkles. |

**Cost.**
- The smoke is cheap enough for mobile. Each puff is one pre-drawn soft sprite in a few tints, and the card itself
  only fades, scales and moves.
- No blur, noise or displacement filter runs on the card.
- In Godot, the puffs are a `CPUParticles2D` (or a few pooled `TextureRect`s) with a soft circle texture and a colour
  ramp, and the card is a Tween on `modulate` and `scale`.

**Rules.**
- Banners **queue**. The next starts 300 ms after the last one leaves; with three or more waiting, the hold
  shortens to 3 s.
- Banners run on game time: they freeze while paused and wait during the rift and death splashes.
- With reduced motion (a view option), the card cross-fades in and out with no travel, pops or particles.

### 4.4 Also

- **Pause menu → Achievements.** All five appear, earned or not. Earned ones show their pearl. Unearned ones show a
  dark pearl silhouette, the title and how to earn it.
- **First appearance in an offer.** Once an unlocked pearl turns up in a treasure room or shop, a small "NEW" chip
  floats above it.

## 5. How it would be built

**Core**
- `data/achievements.json` holds the id, title, line, tone, pearl and hint for each achievement. It is validated on
  load: every pearl exists, and no pearl is unlocked twice.
- An `AchievementCatalog` reads it.
- items.json sets `unlock` on the five pearls and removes Hammerhead's lock.
- `PlaneRun` takes the profile's unlocked set. The treasure, shop and boss offers draw from the ported pearls whose
  unlock is met.
- **`PlaneAchievements`** reads each step's events and the room's stats, and returns the ids newly earned. It
  needs a few more facts from the world:
  - Bubbles per volley (`Shot` event).
  - Each kill's bubble count and whether the foe was at full health (`MobDefeated`).
  - The boss's landing and freeing ticks.
- **The five pearls on the plane:**
  - grow (Starfish Arm), split (Mitosis) and crits (Giant Squid Eye), in `StepHerShot` and the hit code;
  - luck (Lucky Sea Glass) in `DropShells`;
  - the pickup bonus and richer caches (Pirate's Doubloon) in `PlaneEconomy`;
  - Plankton Swarm.
- Tests cover:
  - each condition;
  - seeded runs not awarding anything;
  - locked pearls never being offered;
  - each new pearl's behaviour.

**Game**
- `ProfileStore` loads and saves the profile at `user://profile.json`, through the existing `SaveStore`.
- **`AchievementBanner`** is a Control on its own CanvasLayer, above the HUD and below the pause menu. It is
  driven by one timeline of Tweens:
  - The pearl is a small SubViewport: a sphere with `PearlMaterials.Get(item)`, turning.
  - The smoke exit is a `CPUParticles2D` burst of soft pastel puffs (no shader filter on the card).
  - The title's per-letter drop-and-bounce is a custom `RichTextEffect`.
- Sfx gets the pop, chime, whoosh and the small exit pop.
- The pause menu gets an Achievements list.
- Debug flags:
  - `--award=id` shows a banner (and awards nothing in seeded runs).
  - `--reset-profile` clears the profile.

**Order of work:**
1. The profile, the data and the gating.
2. The detection and its tests.
3. The five pearls.
4. The banner.
5. The pause-menu list.

## 6. What changes in the spec

- DESIGN-TOPDOWN §7 / §8 gets an *Achievements* entry describing the rules above, and the banner under §9 UI.
- items.json changes its `unlock` fields.

## 7. Decisions so far

- **Hermit Hoarder:** keep 100 shells in one room; Lucky Sea Glass makes it reachable.
- **Shucked in Fifteen:** the clock starts when she lands. It needs a strong build, and that is intended.
- **Unlocks** apply from the moment they are earned: in the current run and all later ones.
- **Seeded runs** never earn achievements.
- **The banner** uses pop & smoke (§4).
- **Remaps:** Mitosis and Giant Squid Eye move from their old (3D-roster) achievements to Bubble Bath and
  Shucked in Fifteen.

## 8. The questions as asked

1. **Hermit Hoarder:** 100 shells in one room is out of reach today (about 50–60 at most). Options:
   - (a) keep 100 and let Lucky Sea Glass make it reachable, which makes it a chained goal (*Untouchable* first);
   - (b) lower it to 60;
   - (c) count it over a whole run.
   I recommend (a).
2. **Shucked in Fifteen:** does the clock start when she lands (proposed) or when the arena seals? Is it fine that it
   needs a strong build?
3. **Remaps:** is it OK to take Mitosis and Giant Squid Eye off their old (3D-roster) achievements?
4. **When unlocks apply:** from the moment they are earned, including later rooms of the same run (proposed), or
   from the next run?
5. **Seeded runs:** keep the rule that custom seeds earn nothing? It means verification runs with `--seed` never
   award anything.
