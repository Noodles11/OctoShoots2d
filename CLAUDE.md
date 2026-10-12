# OctoShoots / Ink Deep: working rules

Godot 4 .NET (C#). The deterministic sim lives in `src/Core` (tested with xUnit in `src/Core.Tests`), presentation in
`src/Game`, shaders in `assets/shaders`. Design docs are in `docs/` (THEME-BIBLE.md is the guideline; CORRUPTION.md
and LIGHT-BUBBLES.md describe the current corruption war and her light).

## Art direction (standing rules)

- **Creatures look real, not cartoonish.** No goofy eyes, faces, brows, crowns or other cartoon features on
  creatures or bosses. Model them on the real animal (a clam is a real clam: heavy ribbed valves that open and
  close; a slug is a slug). Their behaviour should read from their body: a shell opening, a body creeping, ink
  dripping.
- **Two forces, two looks.** Light is Clementine's: warm gold to white, bright and alive. Corruption is ink: black,
  glossy, light-drinking, and it drains colour out of the world (grey, ashen) rather than painting it. Anything that
  belongs to the corruption (its creatures' bodies, their projectiles, meters, stains) uses the ink look. Anything
  that cleanses uses light. Avoid other accent colours (no red/green UI for damage or healing).
- **Clementine stays the warm light in the scene.** Nothing else should out-glow her except a moment of cleansing
  (a burst, a freed creature).
- **Creatures do not "see" in a cartoon way.** Bosses and mobs act from what they are (a clam throws from its
  opening and does not turn to face her); avoid behaviour that implies a face tracking her.

## Workflow

- Run `dotnet build OctoShoots.csproj` and `dotnet test src/Core.Tests` before committing.
- Bump `src/Game/GameVersion.cs` with each user-visible change and keep the docs in `docs/` in step with the code.
