# Ink Deep — Theme Bible

The overall theme, with all the details: **the guideline for the game**. Every feature, screen, creature, sound and
line of text is checked against it, above all against the theme tests in §12. It consolidates every thematic
decision already made in DESIGN-TOPDOWN.md (authoritative for rules and numbers), DESIGN-3D.md and DESIGN-2D.md
(inherited) and DEPTH1-BESTIARY.md, and fills the gaps those documents leave open (palettes, typography, audio
identity, branding, tone-of-voice). Anything that is a new proposal rather than an existing rule is marked ▸.

## 1. The game in one breath

A top-down action roguelite in a darkening ocean. You are Clementine, a small bioluminescent jellyfish — the last of her bloom — diving after her family through a reef poisoned by the Leak. Every pearl you swallow becomes living light on your bell; every creature you defeat is freed, not killed; every dive ends at the Crack. At the bottom waits the truth: the reef was never being watched. It was being kept.
Genre anchors for anyone new to the project: the run structure and item density of The Binding of Isaac, the map-feeling and campfire-lighting philosophy of Below, the freedom and sadness of a Hollow Knight descent — rendered as one warm lantern drifting through seven deepening blues.

## 2. The theme, stated plainly

Ink Deep is a game about what we carry inside, told through light.
Three questions the game keeps asking, in ascending order:
- What does it mean to carry everything inside you? Clementine is a jellyfish: she cannot grab, hold, or anchor. Her items are not worn or wielded — they are absorbed, dissolved into light, worn as permanent patterns on her bell. The inventory is her body. Her family said a jellyfish’s bell is her heart wearing a lantern. The game makes that literal.
- What does warmth look like when everything warm is artificial? The corruption is not evil — it is domestic. The Leak is warm, too-bright tank-water bleeding down from a human aquarium. Corrupted creatures wear pet-shop colours. The deeper Clementine dives, the more the world is lit by things that were made, not grown — until the finale, where the only light left is a fluorescent LED strip and the horror is that it’s flat and kind and well-fed.
- What do you owe the things that hurt you? No creature in the game is evil. Every enemy is a neighbour rewritten from the outside. You never kill anything; you wash the tank out of it and watch its true colours come back. The only real adversary is The Hand — and even The Hand is just a child tapping on glass.
The title does triple duty: ink (the dash, the Leak, the darkening water), deep (the dive, the descent curve), and in deep (the idiom — too far down to turn back, which is the exact moment the Tank reveal lands).

## 3. The six pillars

Every decision — art, audio, system, sentence — must serve at least one of these. If it serves none, it doesn’t ship.

### Pillar 1 — Small warm light in overwhelming dark

The Below principle, adapted: our campfires are bioluminescent and the dark is the ocean itself. Clementine is always the warm light in the scene — in every depth, at every menace level, she never gets scary and never goes out. By Depth 5 she is the lighting design.

### Pillar 2 — Kept, not watched

The twist is not surveillance, it’s captivity. A jellyfish in a fish tank is not a prisoner who might escape — it is a decoration. Every foreshadowing beat (plastic plants that don’t sway, price tags, muffled glass thumps, the LED bleed) feeds this single realization. The Hand fight is staged as a pet reaching into the bowl, and its scale — “Clementine is a bug on a countertop” — is the theme made physical.

### Pillar 3 — The bell is the build sheet

Absorption is the signature system and a thematic one: pearls are swallowed, not equipped; a visible gulp of light; a permanent glowing motif on the bell. A player glancing at Clementine reads her build the way an Isaac player reads a sprite. Streamers get body-readable runs for free. Absorption is commitment — no un-absorbing, no dropping, ever. That refusal is the Isaac feeling.

### Pillar 4 — Freed, not killed

Combat is an act of restoration. Defeated creatures shudder, the tank colours wash out, they burst free in a poof of bright bubbles, the piece of the tank drops and sinks, and they swim on healed. The darker the depth, the more each small poof of true colour matters. This is the emotional engine of the whole game and it must never feel like a death animation.

### Pillar 5 — The sadness curve is systemic

Tone is not written, it is driven. One global parameter — Menace (0.0 → 1.0) — ramps light, fog, saturation, aggression, telegraph time, corrupted-to- healthy ratios, empty dens, silence. Depth 1 is bright and playful with an ache underneath; Depth 6 is near-black, ruthless, and lonely. The player feels the descent before anyone explains it.

### Pillar 6 — Absurdity at the bottom

After six depths of creeping dread, the finale snaps into bright, flat, artificial comedy-horror. The Hand has a cartoon bandage, a smiley sticker, and chewed nails. Fish-food flakes heal you. The rug-pull only works because the game earned six floors of sincerity first. Never wink early.

## 4. Narrative theme & arc

### 4.1 Lore (fixed, inherited)

One day the Crack opened in the sea floor, and warm, too-bright water — the Leak — began seeping down into the reef. Whatever the Leak touches is rewritten: familiar neighbours turn pet-shop neon and lash out. One by one, every jellyfish of Clementine’s bloom was pulled down into the Crack in a single current. She follows, because she is the only one left who glows the family colour.
The truth (revealed at the end): high above the sea stands a human home aquarium, joined to the ocean by a pipe whose mouth lies at the bottom of the Crack. The tank broke once; some fish escaped back to the sea, already changed; its water bleeds down as the Leak. The pipe still draws creatures up. The jellyfish were taken that way.

### 4.2 Why the protagonist is a jellyfish (thematic load-bearing)

- She drifts. A jellyfish cannot grab, hold, or anchor — everything she carries, she carries inside. The absorbent gift makes that literal.
- She is the counter-adaptation. The Leak rewrites creatures from the outside; Clementine takes things in. The reef’s one perfect answer to the poison is a creature that metabolizes it into light.
- The Tank twist lands harder. “I was kept” is worse than “I was watched,” and a jellyfish is the single most decorative animal a human keeps. The finale’s horror is domesticity.
- She kept the name and changed species. Clementine is reclaimed from the octopus era — the tangerine warmth survives every redesign.

### 4.3 The descent as a three-act structure

| Act | Depths | Emotional job |
|---|---|---|
| I — The Bright Ache | 1 Shallows, 2 Kelp | Cute, readable, playful — with empty bloom-dens and the first wrong colours. Teach the player to love the reef so its loss matters. |
| II — The Wrongness Grows | 3 Galleon, 4 Carnival, 5 Trench | Light fails; corruption outnumbers health; foreshadowing turns physical (pipes, plastic, flakes, glass thumps). The player starts asking where the Leak comes from. |
| III — The Artificial | 6 Abyss, 7 Tank | Near-total dark, then the snap to fluorescent flatness. The reveal, The Hand, the rescue, the true ending. |

### 4.4 The foreshadowing ladder

Corrupted creatures carry the tank from Depth 1 (neon GloFish colours, gravel, price tags — see §6.4). From Depth 4 the evidence escalates beyond creatures: straight pipes in the far background, a plastic castle shard, a fish-food flake drifting down, muffled thump… thump… of glass-tapping in the Abyss. In the finale the direction of light itself inverts: the Tank’s LED bleeds down into the Abyss below — the first artificial light in the game that reaches downward.

### 4.5 Endings

- Per level, exactly two outcomes: descend through the rift, or perish. (The old Surface Bubble early-exit is cut — commitment again.)
- Progressive dives: each first boss kill deepens the Crack’s reach; a sealed rift wears a glowing “?” rune — a visible promise of what’s below — and entering it ends the run as a win.
- The pipe: the first Hollow Maw kill shows a rusty grate that holds; the second breaks it. Cutscene: roar of suction, everything streaming toward the grate, “SHLUUUURP!” — the one moment of true verticality in a plane-locked game.
- True ending: the final sting; “OW! It stings!”; the aquarium carried to the shore and tipped back into the sea; the bloom pours out; the pipe sealed; the Leak stops; true colour floods back. Credits over the Tide Pool, full of jellyfish again.

## 5. Clementine — protagonist theme

### 5.1 Body language (fixed)

- Bell: translucent dome ~0.8 m across, slow breathing contract/relax cycle; eight oral arms plus marginal tentacles trailing beneath as verlet chains. Drawn floating 0.7 m above the swim plane.
- Pulse propulsion: every move is a contraction beat with recoil — the signature lurch-then-glide. Input overrides pulse timing within 0.1 s (responsive first). First stroke from rest: a 1.8× jet over 0.38 s; sharp turns (>110°) re-trigger it.
- Idle drift: no input → she slows, the bell relaxes, tentacles float up around her, and she bobs gently (~1 m) on the plane. The game should be beautiful to stop playing for a moment.
- Ink dash: yes, a jellyfish inks — the Leak changed her, and the Sea-pedia entry says so. 3.4× speed, 0.32 s untouchable, a slowing ink cloud where she was. The dash is the Leak’s gift turned against it: the only mechanic where Clementine uses the corruption’s own medium.
- No eyes on the UI — eyes on the body: rim organs pulse magenta when hurt, gold when the active item is ready, and a chromatic ripple runs base→rim when a pearl is consumed.

### 5.2 The bell as living UI (fixed)

Pearls are absorbed with a soft suction and a gulp of light — the one animation the whole item economy hangs on — and stabilize as permanent glowing motifs. Capacity: 16 motifs on the bell; pearls beyond apply their effect as faint interior glow only.
Motif × Colour grid (fixed):

| Family (effect class) | Motif | Example |
|---|---|---|
| Stat up | concentric ring | Coral Crown → one clean gold ring |
| Bubble modifier | radial ray from centre | Mitosis → forking twin ray |
| Defensive | scalloped edge band | Barnacle Armor → crusted rim band |
| Economy/luck | scattered dots | Lucky Sea Glass → drifting sparkles |
| Mobility | spiral | Drift Pearl → slow swirl |
| Status (burn/poison/freeze) | wavy band | Fire Coral → flickering wave band |
| Active item | central halo glyph | one slot only — dead centre |

Colour = pool of origin: treasure teal, boss gold, siren crimson, curse violet, secret white. 6 motifs × 5 pool colours gives every item a unique signature.
Synergies and transformations rewrite the pattern: named synergies add a glowing filament linking their two motifs; transformations re-theme the whole bell — Kraken Form turns motifs into dark veins, Neon Rave cycles every motif’s hue, Coral Reef makes them pulse like breathing coral, Shark Mode sharpens them, Pirate gilds the rings. (▸ The last two re-themes are proposed to complete the set of five.)

### 5.3 Personality without dialogue (▸)

Clementine never speaks. Her character is communicated entirely through motion and light:

- Curiosity: bell tilts toward unvisited places and glowing things.
- Fear/hurt: magenta rim flash, tentacles whip outward, chromatic wobble.
- Resolve: the gulp of light is always a beat slow — she considers each pearl for a heartbeat before it dissolves. Absorption should feel like swallowing something precious, not picking up loot.
- Grief: in empty bloom-dens, her idle bob slows and her glow dims by a fraction. Never announced; always felt.

## 6. Visual identity

### 6.1 Camera & composition (fixed)

- Tilted top-down, 55–65°, perspective, narrow FOV (~35°) so arches and reef walls parallax softly. North is always up; the world and minimap agree.
- View on screen ~30×22 m: a corridor reads at a glance.
- Canopy fade: arch bodies, cave roofs and overhangs live on a canopy layer that blurs and dissolves while Clementine is underneath — the reef reads as layered and physical, not painted flat. Ridge walls between camera and player dissolve in a dithered cutout.
- Depth focus (the look, after Below): the swim plane is the focal plane. Within ~0.6 m below / 1.6 m above stays sharp; below blurs, fogs and darkens toward dim blue-green; above blurs and darkens into foreground silhouettes. Heavy oval vignette, cool grade on top. Clementine, the gateway, pearls, shells and ink are drawn after the pass — always sharp.
- Only two camera breaks, ever: the boss-awakening dip (2 s low swell) and the Tank transition (the Crack sucks her down a vertical shaft — the game’s single sanctioned moment of verticality).

### 6.2 Light — the core pillar (fixed)

Per-depth light budget drives palette, mood, and difficulty readability:

| Depth | Ambient | God rays | Player glow | Primary scenery light |
|---|---|---|---|---|
| 1 Shallows | High, sunlit turquoise | Strong, broad | Small (supplemental) | Sun + caustics |
| 2 Kelp | Dappled green | Broken by canopy | Small | Light shafts |
| 3 Galleon | Amber, dusty | Through wreck holes only | Medium | Lanternfish, loot glints |
| 4 Carnival | Neon but flickering | None — “stage lights” | Medium | Corrupted neon, bounce pads |
| 5 Trench | Near zero | None | Large — she is the lantern | Bioluminescent flora specks |
| 6 Abyss | Black | None | Large, bloom-heavy | Neon outlines + beneath-layer eyes |
| 7 Tank | Flat fluorescent LED | None — wrong | Irrelevant — everything is lit | Harsh, shadowless, artificial |

Rules: fog density, saturation falloff and glow reach all scale with Menace. Fog is also a fairness tool — creature shots glow in their own colour with short trails; telegraphs add rim-light flares. Readability never drowns in mood.

### 6.3 Palette (▸ concrete values; the depth list is fixed)

Master rule: warmth belongs to Clementine and to healthy life; artificiality is glossy, saturated-in-the-wrong-way, and slightly too bright. Reds are absorbed with depth (real water optics), so the deeper the game gets, the more her tangerine glow is the only warm hue on screen.

| Depth | Water | Seabed/rock | Accent | Notes |
|---|---|---|---|---|
| 1 Shallows | #3FBFBE → #7FDCD2 | sand #E3D2A0, pink coral #F089A8 | sun gold #FFD97A | Saturated, hopeful; caustics sharp |
| 2 Kelp | #2E7D5B → #8FBF6F | olive rock #5C6E46 | shaft yellow #E8D878 | Dappled; greens eat the reds |
| 3 Galleon | #4A5A66 | timber #7A5230, rust #8A4B2E | brass #D9A441, glint #F2C14E | Dusty amber; god rays through holes |
| 4 Carnival | #3A2A5E | coral #B44FD0 | hot pink #FF4FA3, neon cyan #2FF3E0, violet #9B5CFF | Neon that flickers; colour is off |
| 5 Trench | #0B1B33 | slate #1A2438 | biolume specks #7FE7FF, Clementine warm #FFC46B | Her glow is the palette |
| 6 Abyss | #05070F | black rock #0A0D16 | neon outline pink #FF3FA4, cyan #2FF3E0, eye-shine #FFD25E | Neon-on-black; eyes below |
| 7 Tank | LED white #F4F8FF | neon gravel #3A7BFF | plastic pink #FF6BCB, plastic green #7CFC4D, toy red #FF4D5E | Flat, shadowless, wrong |

Clementine, always: body tangerine #FF8C42, glow warm gold #FFC46B, hurt flash magenta #FF3D7F. She is the only hue that never changes with depth.
Landmark light language (fixed): treasure gold, shop green, curse red, secret violet — no beacon (found, not advertised), the Crack orange, start white, item cache teal (matching the treasure-pool family), ambushes coral #FF7F6B — warm but not gold, so they never read as treasure.

### 6.4 The corruption language (fixed from the bestiary proposal; adopt it)

Corruption is readable at 30 m through mist via three stacked tells:

1. Tank colours — the real GloFish/dyed-fish palette: electric green #7CFC4D, starfire red #FF4D5E, cosmic blue #4D6BFF, galactic purple #B04DFF, sunburst orange #FF9436, moonrise pink #FF6BCB — in patterns that don’t belong to the species (stripes on a spotted fish, spots on a striped one).
2. Plastic sheen + warm shimmer — a glossy, manufactured finish and a faint slow warm glow (the Leak) showing through fog, so a threat reads from afar.
3. A piece of the tank — one aquarium object per species: the ninja’s headband now flutters a price tag; the Spanish Dancer wears blue gravel along its back; the Pufferling has a fish-shop tag clipped to its tail; the Barracuda trails a plastic plant from its jaw; corrupted moon jellies cycle LED colours (healthy jellies are pale and steady); the Sea Urchin has a plastic castle shard in its spines; Crabby wears a bottle cap, decorator-crab style; the Moray’s hole is framed by plastic leaves.

Healthy twins wear natural colours, carry nothing, are never targets (bubbles pass through, aim assist ignores them, they stay off the minimap). Ratio healthy:corrupted ≈ 2:1 at Depth 1 → 1:1 at Depth 3 → 1:3 at Depth 6.

### 6.5 The freeing moment (adopt the proposal)

- Shudder (0.3 s) as tank colours wash out.
- Poof — a burst of bright bubbles with a soft popping chime; the piece of the tank drops and sinks (gravel scatters, the tag spins away, the bottle cap rolls).
- True colours flood back; loot drops.
- It carries on healthy — the fish swims off, jellies drift up, urchin spines lie down, the crab scuttles away, the moray slides home — slowly, and once it is out of the visible screen it is gone. Freed creatures never help Clementine and are never targets. Bosses free the same way at bigger scale, colours flooding back over the whole arena.
This replaces every death burst. It must read as relief, not victory.

### 6.6 Visual menace ramp (fixed)

Same archetype, re-parameterized per depth: round eyes → glowing slit pupils; smile → teeth (count scales); smooth silhouette → jagged; saturation down; darker body with bright rim light. A Depth-1 gumdrop is a Depth-6 toothy shadow. Behaviour ramps in lockstep (aggression 40→100%, speed ×1.0→1.5, telegraph 0.8→0.35 s and never lower).

### 6.7 The beneath-layer (fixed)

The floor is translucent water, not ground glass: ~10 m down, a blurred, slow-drifting shadow copy of the next depth — structures as silhouettes, next biome’s creatures as drifting shadows. Escalates with depth until, in Trench and Abyss, the beneath-layer becomes the scenery: giant slow shapes crossing underfoot, eyes opening and closing below. Gameplay tells glow through: sealed pockets shimmer, the Crack backlights both layers, and in the finale the Tank’s LED bleeds down — the inversion tells the story before the cutscene does. Shadow mobs are cosmetic only (determinism preserved).

### 6.8 Typography & logo (▸)

The comic onomatopoeia is retired; comic-cover boss title cards survive (“CLEMENTINE VS QUEEN CLAM”). The type system should be one family in two moods:

- Display / title cards / logo: a rounded, buoyant display face with hand-drawn warmth (e.g. Baloo 2 or Fredoka weight 700), letterspaced wide, glowing. Title cards punch in, stand, dissolve — never with onomatopoeia text.
- UI / captions / Sea-pedia: a clean humanist sans (e.g. Nunito or Inter), sentence case, generous line height. Captions are quiet; the light does the shouting.
- Monospace accents: seed codes (KELP7Q2Z), debug, and the run clock in a tabular monospace — the “instrument” voice.
- Logo direction: “INK DEEP” in the display face, the I replaced by Clementine’s glowing bell silhouette trailing two tentacles; a hairline crack of orange light running under the wordmark, widening downward. On dark, the wordmark is white; the bell is the only colour.

### 6.9 Post stack (fixed, restated)

Marine snow between camera and level · faint refraction wobble · vignette deepening with depth · chromatic aberration on hurt only · cool grade · depth-focus blur. God rays rake across the view, never into the lens. Caustics on upward faces, fading with depth.

## 7. The seven depths as themed acts

Each depth is three reefs of one biome (the Tank single), each reef a 150×150 m seeded square ringed by impassable reef wall, each a little meaner (+0.04 menace). HUD reads DEPTH x · REEF y/3.

### Depth 1 — Sunlit Shallows · The Bright Ache

Turquoise, sand, pink coral; strong broad god rays; caustics sharp. Cute and readable with an ache underneath: healthy neighbours everywhere, but the bloom-dens are empty. Mobs: clownfish ninja, Spanish Dancer, Pufferling, Barracuda, moon jelly swarms, Sea Urchin, Crabby, Moray. Bosses: Old Gus (grumpy, burps up a boot), Big Barnacle Bill (muffled “Arrr!”), Queen Clam (pearl crown, bubble raspberry). Goofy on purpose — the game teaches you to love the reef here.

### Depth 2 — Kelp Jungle · The Green Hush

Dappled greens, yellow shafts broken by canopy; tangling kelp slows. Fish are shyer; dens mix healthy and corrupted. Bosses: Kelpie the Tangler, Sir Urchin, Mama Otter (lobbing cracked urchins from her back).

### Depth 3 — Sunken Galleon · The Gilded Wreck

Amber dust, brass, loot glints; god rays only through wreck holes. Few fish, drifting debris; ambushes begin. Bosses: The Rusty Admiral (a crab in a cannon hat), Treasure Mimic, Captain Sawtooth. First depth where the silence between encounters is noticeable.

### Depth 4 — Coral Carnival · The Party That Isn’t

Hot pink, purple, neon — flickering, “stage lights” with no sun behind them. Corrupted neon is scenery light now; the colours are the Leak’s own. Fish hide. Grins have teeth. Bosses: Ringmaster Octo, The Jester Jellies (trio), Punchy the Mantis Shrimp. Foreshadowing turns physical: pipes in the far background, a plastic shard, the first muffled glass thump.

### Depth 5 — Twilight Trench · She Is the Lantern

Near-zero ambient; Clementine’s glow radius is the level design. Bioluminescent specks, no schools; stalkers strike from darkness. Positional audio becomes the off-screen warning system. Bosses: Mother Angler (dims the arena — light as a boss mechanic), The Siphonophore, The Giant Squid (ink blackouts).

### Depth 6 — The Abyss · Eyes Below

Black + neon outlines; heavy marine snow; only distant glowing eyes. The beneath-layer is the scenery; currents push along tunnels. Ruthless mobs, minimal telegraphs. Bosses: The Frilled Shark, The Sea Spider, and always last — The Hollow Maw, the tank’s oldest escapee, guarding the pipe grate.

### Depth 7 — The Tank · Kept

The look flips: flat fluorescent LED, neon-blue gravel, plastic plants that don’t sway (the uncanny tell against the living-water sim), a bubbling treasure-chest ornament, a plastic diver, a castle. Behind the glass: a giant blurry living room — lamp, TV glow, a cat’s eye passing by. The bloom floats here, listless, among other captured creatures. Mobs: Toy Diver, Snail (shell blocks shots). Boss: The Hand — three phases (Curious → Catch! → Frustrated): pokes, glass taps, fish-food flakes (they heal; it’s a trap and a kindness at once), the green net, gravel vacuum, pencil lines, sliding ice cubes, the castle tipping as a crushing rectangle. Everything the hand does is huge, slow, and telegraphed by shadow gliding across the gravel.

## 8. Audio identity

### 8.1 Philosophy (fixed + ▸)

- Synth-style SFX only (music post-v1). Sound is information first, atmosphere second.
- Pitch lowers with depth — the whole world’s voice deepens as she dives. Clementine’s own sounds never pitch down as far: her voice stays younger than the water.
- Positional audio is the darkness telegraph: in Trench and Abyss, sound is the off-screen warning system. Every telegraph has a 3D-positional cue and a screen-edge marker.
- Silence is a resource. The sadness curve is partly a foley curve: fewer healthy creatures, fewer calls, more empty water. Depth 6 should be the quietest hour in the game — so the Tank’s noise lands like a slap.

### 8.2 Per-depth sound beds (▸)

| Depth | Bed | Signature sounds |
|---|---|---|
| 1 | Bright water hiss, distant surf | Gull-faint surface wash, playful pops, chimey telegraphs |
| 2 | Filtered green hush, leaf-sway white noise | Kelp creaks, muffled darting |
| 3 | Wooden groans, chain ticks, drips | Cannonball thuds, coin brass |
| 4 | Detuned calliope fragments, failing neon buzz | Bounce-pad boing, carnival reverb too large for the space |
| 5 | Near-silence, low sub drone | Sonar-soft pulses, distant clicks, her own bell-pulse audible |
| 6 | Sub drone + slow pressure groans | Sparse clicks, the far muffled thump… thump… of glass |
| 7 | Filter-intake hum, LED mains buzz, muffled TV and living-room murmur through glass | Plastic taps, gravel skitter, slosh, and The Hand’s huge soft foomph |

### 8.3 Signature sounds (▸)

- Absorption (the gulp of light): a soft inward breath + rising warm chime resolving into a held tone — the game’s cash-register moment, tuned to feel sacred, not transactional.
- Freeing poof: a bright bubble-pop chord with a two-note uplift. It should be the happiest sound in the game; players will hear it thousands of times.
- The Crack opening: deep rock-split + an updraft rush that resolves into a low inviting hum, pitched like a doorway.
- Glass taps (Depth 4+): muffled, huge, irregular — never explained until the Tank.
- The Hand: no roar, no sting — domestic sounds at wrong scale. A finger entering water. A knuckle on glass. A child’s delighted gasp, muffled, enormous.

### 8.4 Music intent (post-v1; ▸ direction)

Sparse warm pads in the Shallows thinning to single-instrument drones by the Trench; diegetic-first mixing so silence stays available. The Tank gets the game’s only “music as object”: a tinny, looping speaker-muzak lullaby coming from somewhere above the waterline — cheerful, compressed, and unbearable.

## 9. UI/UX theme

Rule one: diegetic first. The HUD exists only where the body can’t carry the information.

- Ammo is diegetic: bubble orbs orbit beneath the bell — no ammo counter.
- Status is diegetic: rim organs pulse magenta (hurt) / gold (active ready); absorption is a chromatic ripple.
- HUD: health bar with drain trail (bottom left), run clock (top centre, stops while paused), active slot with recharge bar, shells counter that brightens and swells on pickup, absorbed pearls as dots above the HP bar, circular minimap top-right.
- Minimap: north-up, fog of war revealed in a 15 m radius that persists; unvisited places show “?” once approached, visited ones as dots in their landmark colour; shop $ and treasure trophy appear when fog lifts. Beneath-layer shadows never appear — the map shows your floor only.
- Tab map: the full chart, crater-accurate, no fog, with legend.
- Splashes: dark, quiet, typographic. Room-clear splash: time, foes freed, shells collected and spent, pearls found, places visited, damage taken, what she carries. Death splash: how far the run got, and the seed — because a seed is the run’s epitaph and its invitation to a friend.
- Captions (fixed voice): name, tagline, one line per effect with numbers — “+0.8 damage”, “20% chance to freeze foes for 1.6 s”. No comic onomatopoeia anywhere. Neon event language is light, not text.
- Accessibility is theme work, not a settings afterthought: shake toggle, reduced-flash, reduced ambient motion, colourblind-safe shot outlines, mist/FOV options. A game whose core pillar is light owes its players control over it.

## 10. Writing & tone of voice (▸ style guide)

- Item taglines: one breath, warm, a little salty, never jokey about the dark. “Found only on moonlit tides.” · “The Galleon’s cook swore by it.” · “Angry and proud of it.” · “Clingy but protective.”
- Sea-pedia voice: a field guide written by someone who loves these animals. Facts first, feeling in the last sentence. The ink-dash entry is the model: “Jellyfish do not ink. This one does. The Leak changed her — and she kept the change.”
- Boss title cards: CLEMENTINE VS QUEEN CLAM — punch in, stand 2.5 s, dissolve. Bosses are goofy early, menacing late, and their cards follow the same ramp: big and warm at Depth 1, thin and cold at Depth 6.
- Names: creatures are species-true (Spanish Dancer, Salp Chain, Giant Clam); bosses are storybook (Old Gus, Queen Clam, The Hollow Maw). The Hand is never named by the game — only by the child: “OW! It stings!”
- What the game never says: “kill”, “enemy”, “reward for violence”. Foes are freed; rooms are visited; the run ends when she perishes or descends.

## 11. Branding (▸)

- Title: Ink Deep — always two words, title case.
- Taglines (pick by context):
  - Primary: “Swallow the light. Dive for your family.”
  - Store short: “A roguelite about what you carry inside.”
  - Trailer card: “The reef wasn’t being watched. It was being kept.”
- Iconography (three marks, in priority order): ① the glowing bell with one pearl-motif ring; ② the Crack — a hairline of orange light in dark water; ③ the pearl, dissolving into light.
- Key art: top-down, near-black water; Clementine small and warm at centre, bell bright with motifs; beneath her, through the translucent floor, the faint neon grid of tank gravel — the entire game in one image, spoiler hidden in plain sight.
- Store capsule: the bell mark on the Depth-6 black; title in display face; no characters fighting, no enemies — the promise is descent, not combat.

## 12. Theme tests — run every new idea through these

- The Warmth Test: is Clementine still the warmest light in the scene?
- The Read-at-Range Test: does a corrupted creature read as corrupted at 30 m through mist — colour, sheen, and the piece of the tank?
- The Bell Test: does this item/system show on her body? If it can’t, why not?
- The Freeing Test: could this moment be mistaken for a kill? Fix it.
- The Sadness Test: does this feature respect the Menace ramp — quieter, darker, lonelier downward?
- The Sway Test: does everything in the ocean move like water, and everything in the Tank refuse to?
- The Two-Breaks Test: does this need a camera break? Then it’s one of the two, or it doesn’t get one.
- The No-Villain Test: does this imply any creature is evil? Only The Hand is an adversary, and even it is just curious, then frustrated.
- The Commitment Test: does this let the player take something back? Absorption is forever; descent is one-way; runs end in rifts or death.
- The Never-Wink Test: does this joke before the Tank? Cut it.

## 13. Theme decisions

- **Beneath-layer interaction:** none for now. The beneath-layer stays cosmetic; no boss reaches up through the
  floor.
- **Hub format:** unchanged for now. The title screen stays a menu over the live sea; the free-swim Tide Pool is
  not planned yet.
- **Freed creatures:** they swim away slowly and disappear once they are out of the visible screen. They never
  help Clementine.
- **Dropping pearls:** no. Absorption is commitment.
- **Landmark colours:** as the game has them: start white, item cache teal, ambushes coral (§6.3).
- **Healthy urchins:** prickly on touch, a small hazard. The reef is allowed to defend itself without being wrong.

*Ink Deep — the dark is the ocean, the light is you, and everything you swallow becomes part of the lantern.*
