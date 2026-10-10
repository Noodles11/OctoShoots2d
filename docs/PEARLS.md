# Pearls

> Every pearl in the item catalogue (`data/items.json`): what it does, where it is found and what unlocks it. Built
> (✅) marks the pearls ported to the top-down game (`PlaneRun.PortedPearls`): they lie in treasure rooms, are sold in
> shops and fall from Queen Clam. The rest are designed and in the catalogue (their numbers load and are tested) but
> not yet in play. The effect lines are the game's own captions (`ItemCaption`). Generated from the catalogue; when
> the catalogue changes, this page should be regenerated.

| | Passive | Active | All |
|---|---|---|---|
| Pearls | 53 | 11 | 64 |
| Built | 22 | 2 | 24 |

- **Passive** pearls work on their own from the moment she absorbs one: her stats, her bubbles, or something that
  happens when she is hit or frees a creature. They stack.
- **Active** pearls are used with **F** and then recharge over time. She carries one at a time (a new one replaces
  the old); its charge is the rim of her bell, a light sweeping round it (DESIGN-TOPDOWN §2.5).
- **Quality** is ★ to ★★★★ (rarer, stronger). **Found in** lists the item pools that can offer it.

## Passive pearls

### Stats

| Built | Pearl | Tagline | Effect | Quality | Found in | Unlocked by |
|:---:|---|---|---|---|---|---|
| ✅ | **Coral Crown** | *A reef princess's lost tiara* | +20 max HP; +0.3 damage; Heals 20 HP | ★★ | treasure, boss |  |
|  | **Squid Ink Espresso** | *The Galleon's cook swore by it* | +0.3 speed; +0.4 fire rate | ★★ | treasure, shop |  |
|  | **Whale Lung** | *Deep breath!* | +1 range; ×1.3 shot size; No idle sinking | ★★ | treasure |  |
|  | **Pufferfish Pout** | *Angry and proud of it* | ×1.5 damage; −0.2 shot speed | ★★★ | treasure, boss |  |
| ✅ | **Lucky Sea Glass** | *Found only on moonlit tides* | +2 luck | ★ | treasure, shop | untouchable |
| ✅ | **Plankton Swarm** | *Tiny friends* | +0.5 fire rate; Fires 3 shots; ×0.6 shot size; ×0.5 shot damage | ★★ | treasure |  |
|  | **Barnacle Armor** | *Clingy but protective* | −0.1 speed; +30 foam HP | ★ | treasure, shop |  |

### Shot modifiers

| Built | Pearl | Tagline | Effect | Quality | Found in | Unlocked by |
|:---:|---|---|---|---|---|---|
|  | **Electric Eel Tail** | *Zzzap!* | Hits chain lightning to 2 foes (50% damage) | ★★★ | treasure |  |
|  | **Nautilus Spiral** | *Golden ratio, golden shots* | Shots spiral outward | ★★ | treasure |  |
| ✅ | **Mirror Scale** | *Shiny side out* | Shots bounce off rock 2× | ★★ | treasure |  |
| ✅ | **Anglerfish Lure** | *Follow the light* | Shots home onto foes | ★★★ | treasure |  |
| ✅ | **Swordfish Bill** | *En garde* | Shots pierce foes | ★★ | treasure |  |
|  | **Ghost Jelly** | *Walls are a suggestion* | Shots pass through rock | ★★ | treasure, secret |  |
| ✅ | **Mitosis** | *One becomes two* | Bubbles split in two when they pop on a foe or rock | ★★ | treasure | bubble_bath |
|  | **Frost Kelp** | *Grown in the cold seep* | 12% chance to freeze foes for 1.6s | ★★ | treasure |  |
|  | **Fire Coral** | *Don't touch* | 25% chance to burn foes for 3s | ★★ | treasure |  |
|  | **Sea Nettle Sting** | *It lingers* | 30% chance to poison foes for 4s | ★★ | treasure, curse |  |
| ✅ | **Boomerang Shrimp** | *Always comes back* | Shots return to you | ★★ | treasure |  |
| ✅ | **Pearl Diver** | *Patience makes pearls* | Hold to charge a pearl (up to ×3 damage) | ★★★ | treasure, boss |  |
|  | **Sunbeam** | *A shaft of the surface, bottled* | Hold to charge a laser beam | ★★★★ | treasure, boss, whale |  |
| ✅ | **Double Helix** | *Twice the twist* | Fires 2 shots; Shots twist in a double helix | ★★★ | treasure |  |
| ✅ | **Triple Tentacle** | *Why squirt once?* | Fires 3 shots; ×0.8 shot damage | ★★★ | treasure, boss |  |
| ✅ | **Starfish Arm** | *Grows back bigger* | Shots grow with distance | ★★ | treasure | big_bubble_energy |
| ✅ | **Ink Sac** | *Handle with care* | Shots explode | ★★★ | treasure, curse |  |
|  | **Siren Song** | *Come closer…* | 15% chance to charm foes for 3s | ★★ | treasure, siren |  |

### More passives (DESIGN-2D §28)

| Built | Pearl | Tagline | Effect | Quality | Found in | Unlocked by |
|:---:|---|---|---|---|---|---|
| ✅ | **Moon Jelly Heart** | *Beats in slow pulses* | +20 max HP; Heals 20 HP | ★ | treasure, boss, whale |  |
| ✅ | **Shark Tooth** | *Still sharp* | +0.5 damage | ★★ | treasure, curse |  |
| ✅ | **Remora Sucker** | *Sticks to everything* | Pulls pickups toward you | ★ | shop, treasure |  |
|  | **Manta Shawl** | *Glide like a ray* | +0.2 speed; ×0.8 dash cooldown | ★★ | treasure, whale |  |
|  | **Cuttlebone** | *Light as foam, hard as shell* | +0.3 shot speed; +1 range | ★ | treasure, shop |  |
| ✅ | **Giant Squid Eye** | *It sees your weak spot* | 10% chance of a ×3 critical hit | ★★★ | treasure, boss | shucked_in_fifteen |
|  | **Coral Polyp** | *Slowly, the reef rebuilds* | +0.25 HP regeneration per second | ★★ | treasure, whale |  |
|  | **Brain Coral** | *Thinks two moves ahead* | +1 luck; ×1.35 active item recharge speed | ★★ | treasure, shop |  |
| ✅ | **Pirate's Doubloon** | *Finders keepers* | +15 sand dollars; Shell caches hold 50% more | ★ | treasure, goldenClam | hermit_hoarder |
|  | **Cannonball** | *Fire in the hold!* | +0.8 damage; −0.15 shot speed; ×1.4 shot size; ×2 knockback | ★★ | treasure, boss |  |
| ✅ | **Captain's Hook** | *Get over here* | ×2.5 knockback | ★ | treasure, shop |  |
|  | **Stingray Barb** | *Numbs on contact* | 30% chance to slow foes for 2s | ★ | treasure |  |
|  | **Lamprey Mouth** | *Every bite counts* | Each creature freed heals 3 HP | ★★★ | treasure, curse | hollow_maw |
| ✅ | **Hammerhead** | *Wide-angle view* | Fires a 5-pellet cone; ×0.6 shot damage | ★★★ | treasure, boss |  |
|  | **Rear Fin** | *Eyes in the back of your mantle* | Also fires backwards | ★ | treasure, shop |  |
|  | **Abyssal Glow** | *Light from below the light* | +0.4 damage; ×1.3 glow radius | ★★ | treasure, curse |  |
|  | **Glowing Moss** | *Soft and luminous* | ×1.5 glow radius; +0.1 HP regeneration per second | ★ | treasure, shop |  |
|  | **Fire Urchin Spine** | *Pass it on* | Freed creatures burst into 6 spines | ★★★ | treasure, curse | jesters |
|  | **Oyster Pearl** | *A little grit, a little shine* | +1 luck; +10 max HP; +5 sand dollars | ★ | treasure, goldenClam |  |
|  | **Sea Sponge** | *Soaks it up* | Each creature freed gives 3 foam HP | ★ | treasure, shop |  |
|  | **Diver's Watch** | *Time moves slower down here* | ×0.8 foe shot speed | ★★ | treasure, shop |  |
|  | **Dolphin Fin** | *Born to sprint* | +0.25 speed; ×0.85 dash cooldown | ★★ | treasure |  |
|  | **Brass Compass** | *Points to what matters* | Shows landmarks on the map | ★ | shop |  |
| ✅ | **Lantern Pearl** | *Carry the light down* | ×1.8 glow radius | ★ | treasure, boss |  |

### Bubbles on the throwing tentacle

| Built | Pearl | Tagline | Effect | Quality | Found in | Unlocked by |
|:---:|---|---|---|---|---|---|
|  | **Bubble Gland** | *Always one more in reserve* | +3 bubbles on the tentacle | ★★ | treasure, shop |  |
|  | **Anemone Pump** | *Squeeze, refill, repeat* | ×1.6 bubble regrowth speed | ★★ | treasure, shop |  |
|  | **Twin Siphon** | *Two at a time* | +1 bubbles per throw; ×0.85 shot damage | ★★★ | treasure, boss |  |
| ✅ | **Bubble Coral** | *Little ones grow up* | ×1.5 bubble hover time; Bubbles that touch merge into one, bigger and stronger | ★★ | treasure, shop |  |

## Active pearls (F, recharge over time)

| Built | Pearl | Tagline | Effect | Quality | Found in | Unlocked by |
|:---:|---|---|---|---|---|---|
|  | **Conch Horn** | *The sea answers* | Stuns and knocks back every foe within 8 m for 2 s; recharges in 30 s | ★★ | treasure, shop |  |
| ✅ | **Bubble Shield** | *Pop-proof, mostly* | An untouchable bubble round her for 3 s; recharges in 20 s | ★★ | treasure, shop |  |
|  | **Tidal Wave** | *Surf's up* | A wave that pushes and hurts everything ahead (10, 12 m); recharges in 40 s | ★★ | treasure |  |
|  | **Treasure Map** | *X marks a lot of spots* | Reveals the level, secrets included; recharges in 60 s | ★ | shop |  |
|  | **Mimic Clam** | *What's inside? Something else now* | Rerolls the pearls on offer in the room; recharges in 60 s | ★★★ | treasure, secret |  |
|  | **Glow Burst** | *Everybody shine* | Her bubbles ×2 for 5 s; a flash of light; recharges in 10 s | ★★ | treasure |  |
|  | **Sea Dice** | *Roll the tide* | Rerolls the pickups nearby; recharges in 30 s | ★ | shop |  |
|  | **Kraken Call** | *Something big owes you a favour* | A great tentacle strikes the foes about her (25); recharges in 60 s | ★★★ | treasure, boss | siphonophore |
|  | **Ink Cloud** | *Now you don't* | An ink cloud (3 m) hides her for 4 s: foes lose her; recharges in 20 s | ★★ | treasure, secret |  |
| ✅ | **Whale Song** | *A low hum that mends* | Heals 35 HP; recharges in 40 s | ★★ | whale, treasure |  |
|  | **Anchor Drop** | *Straight down* | Opens a way straight down to the next level; recharges in 30 s | ★ | shop, treasure |  |

## Synergies and transformations

Not pearls themselves, but what pearls do together (none are built yet).

| Synergy | Needs | What happens |
|---|---|---|
| **Pinball Storm** | Electric Eel Tail + Mirror Scale | Each wall bounce emits a lightning arc |
| **Lighthouse** | Sunbeam + Nautilus Spiral | The beam sweeps in a rotating spiral |
| **Cell Bloom** | Mitosis + Starfish Arm | Split shots also grow |
| **Guided Inkfish** | Ink Sac + Anglerfish Lure | Homing bombs; explosions leave slowing ink clouds |
| **Steam Vent** | Frost Kelp + Fire Coral | Frozen foes hit with fire explode into steam |
| **Tuna Rang** | Boomerang Shrimp + Swordfish Bill | Piercing boomerang hits twice and grows on the way back |
| **Pearl Necklace** | Pearl Diver + Mitosis | A full-charge pearl bursts into a ring of 8 pearls |
| **Prism Pearl** | Sunbeam + Pearl Diver | A full-charge pearl fires lasers in 4 directions when it pops |
| **Black Tide** | Tidal Wave + Ink Sac | The wave detonates ink on every foe it hits |
| **Big Mad Puff** | Pufferfish Pout + Starfish Arm | Shots burst into a spiky shotgun blast when they pop |
| **Will-o'-Wisp** | Ghost Jelly + Anglerfish Lure | Spectral wisps home twice as hard |
| **Toxic Bloom** | Sea Nettle Sting + Mitosis | Split shots carry a double dose of poison |
| **Feeding Frenzy** | Shark Tooth + Lamprey Mouth | Every creature freed sends you into a fast-firing frenzy |
| **Broadside** | Cannonball + Rear Fin | Shots out of the back explode |

| Transformation | Pearls with the tag | What happens |
|---|---|---|
| **Kraken Form** | tentacle | 8-way shooting, ink trail |
| **Neon Rave** | glow | Shots cycle colours, +damage |
| **Shark Mode** | predator | Speed and damage up, freeing heals |
| **Coral Reef** | coral | The reef mends you: slowly regain health |
| **Pirate** | galleon | +1 damage, +2 luck, foes drop more coins |
