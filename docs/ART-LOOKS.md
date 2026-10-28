# Art looks under study

> **A study, not a decision.** Ways to paint the reef that are more painterly and less colourful: a concept-art look,
> two-dimensional, its depth stated by layers and shading, with felt-tip touches like the minimap's. None of them is a
> filter laid over the finished picture. Each repaints the reef itself, in its own surface shading
> (`reef_art.gdshaderinc`, used by the ground and the free rock), and changes how the depth pass states distance.
> The game's own look keeps its painting; it takes only the stippling of looks 4 and 5 (the same dots, the same shade
> they state), each dot a deeper shade of the reef's own colour (45% of it, with a fifth of the ink mixed in, laid 70%
> over it) rather than ink, so the light falls on the dots like the rest.

Choose one with `--look=N` (the `reef_art` shader global): 0 is the game's own look.

| Look | The reef's surfaces | Light | Depth (`topdown_post.gdshader`) | Ink |
|---|---|---|---|---|
| **1 Gouache layers** | The palette held back (toward its own grey and the depth's water). The height cut into flat painted layers 1.8 m apart, their edges soft and ragged; higher layers a touch richer, deeper ones hazed and cooled. Broad, long brush strokes laid along the slopes instead of the fine photographic grain. The sand's ripples smoothed away. | Three soft-edged bands. The caustics become a few soft dapples. | The water under her plane is stated as flat layers (2.2 m steps), each hazier, barely blurred: no lens blur below her plane. | None. |
| **2 Felt tip and wash** | As 1, paler and greyer: a wash. | As 1. | As 1. | The minimap's hand: a bold wavering felt-tip line along every wall where it meets her plane, with a finer second pass beside it; faint, broken contour strokes up the walls every 1.5 m; bundles of short hatching strokes in the walls' shade near her plane, each bundle at its own angle. |
| **3 Flat concept** | The fewest, biggest shapes: rock and sand as flat tones with only a whisper of their own colour, layered 3.5 m apart, almost no texture. A pale rim catches the light along the walls' tops. | Two tones. No caustics. | As 1. | A single thin line along the walls at her plane. |
| **4 Flat and felt tip** | Drawn flat: rock and sand each one flat tone with a whisper of their own colour, no layers, no texture. | None: one flat light, no shading, no caustics. The stippling does all of it. | No blur below her plane at all, and the water a lighter veil (about half), so the stippling under it reads; the tall tops still blur. | Felt-tip stippling states the depth and the shade: small dots, at most one to each 0.13 m cell of a grid on the picture plane (so they keep one spacing on screen and never swim over the reef), each a little off its cell's middle and its own share of the tone: the darker the tone, the more of them are drawn and the fatter they are (never under a pixel across). Sparse just under her plane, crowding down to about 18 m; faces turned from the sun and the walls' cast shadows the same, the darker the shade. Sunlit tops stay clean. The walls are pocked like a sponge: holes and craters of two sizes (up to about 1 m and 0.45 m across their balls, about half the cells holding one), each a round shaft straight into the wall, its mouth where the wall cuts a ball hidden in the rock, with a fine lip on the side toward the sun (open on the lit side). Inside, what the eye sees down it from the camera's angle: half are shallow dishes (their side, then a pale bottom); a third deep pits (their side stippled darker the further down, going solid black where the eye loses it); one in six go right through the wall (their side, then the pale lit water beyond). |
| **5 Felt tip over the reef's colours** | The game's own: its textures and colours, untouched. | As 4: one flat light, no shading, no caustics. | The game's own: below her plane the reef blurs and sinks into the depth's water (so the stippling down there softens away with it). | As 4: the stippling and the sponge's holes. |

Creatures, kelp, Clementine and the HUD keep the game's own look in every study: only the reef and the water are
repainted so far.
