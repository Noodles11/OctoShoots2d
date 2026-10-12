# Godot Adaptive Music — Setup Guide

A 3-channel adaptive soundtrack system:

| Channel | Bus | Role | Driven by |
|---|---|---|---|
| Theme | `MTheme` | Main ambient theme, always playing | — |
| Corruption (dark) | `MDark` | Bass-heavy variant of the theme | corruption < 0 |
| Corruption (eerie) | `MEerie` | Treble-harsh variant of the theme | corruption > 0 |
| Combat | `MCombat` | Danger layer for encounters | `enter_combat()` |

All four buses are created automatically under a `Music` bus at runtime —
no manual bus layout needed.

## 1. Generate the stems (ACE-Step 1.5)

Use ACE-Step 1.5 (MIT license, safe for commercial games). The trick is to
generate **one** theme and derive everything else from it so all stems share
tempo, key and structure:

1. **Theme** — text-to-music. Be explicit about tempo and key, e.g.:
   > *"Ambient fantasy exploration theme, 90 BPM, D minor, warm pads, soft
   > strings, gentle melody, melancholic, seamless loop, instrumental"*
2. **Dark variant** — use ACE-Step's *cover/repaint* mode on the theme with a
   > *"same melody and tempo, darker, deep bass drones, distorted low
   > strings, oppressive, detuned"*
3. **Eerie variant** — same source, different direction:
   > *"same melody and tempo, high piercing strings, glass harmonics, shrill,
   > unsettling, thin texture"*
4. **Combat layer** — generate standalone (Stable Audio Open also works well
   here), matching BPM:
   > *"90 BPM tense percussion loop, taiko drums, urgent, dark, instrumental,
   > seamless loop"*

If a model ignores your BPM, detect the real BPM (e.g. in Audacity or any
BPM finder) and set the `bpm` export in Godot to match.

## 2. Prepare the audio files

- Trim all stems to the **same length** (or exact multiples) in Audacity.
- Export as OGG Vorbis (or WAV).
- In Godot's import dock, **enable Loop** for every stem — this is what
  keeps the layers in sync over hours of play.

## 3. Wire it up in Godot

In this project the manager is a C# port of `music_manager.gd`: `src/Game/Audio/MusicManager.cs`, registered as the
`MusicManager` autoload in `project.godot`, so the music carries on from the title screen into the game. It needs no
inspector setup: it loads the stems from this folder by name, as `.ogg`, `.mp3` or `.wav`, and makes them loop:

| File | Channel |
|---|---|
| `theme.*` | Theme |
| `corruption_dark.*` | Corruption (dark) |
| `corruption_eerie.*` | Corruption (eerie) |
| `combat.*` | Combat |

A missing stem stays silent, so the theme alone already plays. Set `Bpm` (88 by default), `DuckDb`, `MusicDb` (the
music bus, -6 dB) and the rest as exports on the node if needed. It logs what it found at startup ("Music: theme …").
`music_manager.gd` stays here as the reference version.

## 4. Drive it from gameplay

In this project the game drives it already: every frame of play, corruption is set from how much of the level is
still corrupted (`-(1 - world.Cleansed)`, the dark flavour), and the combat layer follows `world.InBattle` (a creature
that has noticed her is close, or the boss arena is sealed). The title screen sets it back to the clean theme. From
C#: `MusicManager.Instance?.SetCorruption(value)`, `EnterCombat()`, `ExitCombat()`. The GDScript API is the same:

```gdscript
# Corruption: -1 = fully bass/dark, +1 = fully treble/eerie, 0 = clean.
# Smoothed automatically — safe to call every frame.
MusicManager.set_corruption(level.corruption_ratio)        # 0..1 eerie
MusicManager.set_corruption(-level.corruption_ratio)       # 0..1 dark

# Encounters
MusicManager.enter_combat()   # fades in on the next beat, ducks other channels
MusicManager.exit_combat()    # fades out, restores the mix
```

## Tuning notes

- `corruption_smoothing` (default 1.5 s) controls how fast the blend chases
  your value — raise it for slow-burn corruption, lower it for jump scares.
- `duck_db` (default -6 dB) is how much combat pushes the other channels
  down. -3 dB is subtle, -9 dB is aggressive.
- If combat entry feels late with quantization on, either disable
  `combat_quantize_to_beat` or use a combat stem whose first beat is a
  pickup/hit that tolerates landing mid-bar.
- Want corruption to *modulate* combat instead of stacking? Route a second
  darker combat stem through `MDark`-style blending — the pattern in
  `_apply_corruption_blend()` generalizes.
