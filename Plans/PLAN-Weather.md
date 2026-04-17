# PLAN - Weather & Climate

**Status:** Design draft, 2026-04-17 (Tuvok)
**Owner:** Captain (LostBeard)
**Audience:** Gameplay designers, environmental systems team, audio team, rendering team

---

## Purpose

Weather in Lost Spawns is not cosmetic. Rain silences footsteps and hides the player from infected. Fog kills visibility for everyone, player and AI alike. Heat drives water-need. Cold drives fire-need. Storms break line of sight, amplify thunder, disrupt radio. Every weather state changes the peninsula's gameplay surface in measurable ways.

This plan defines the peninsula's weather systems, how they cycle, how they interact with other gameplay systems, and what the player experiences.

## The Pitch

It starts as a grey line on the horizon at noon. By 2 p.m. the clouds are dark enough that visibility drops to 200 meters. At 3 the wind picks up and the treetops roar. At 3:15 the rain starts, and the forest floor goes from crunching leaves to wet silence, and your scent doesn't carry, and you remember the deer you marked this morning. By 4 p.m. it's a downpour and the river you were going to cross is rising and the radio is all static. By dusk the infected in Mercy can't hear you, but the wolves can smell you just fine, and the fire you were going to make tonight will have to wait until you find shelter. The storm passes by 2 a.m., and the morning mist is so thick you can't see your own feet.

Weather is an opportunity, a threat, and a mood. It is rarely all three at once but it is always at least one.

---

## Design principles

### [COMMIT] Weather is simulated, not scripted

No hand-placed "thunderstorm at 3 p.m." events. The weather system runs on real climate simulation: atmospheric pressure, humidity, temperature, wind vector. Storms form, move, dissipate. A player can learn to read the sky.

**Why:** A simulated system lets players build intuition. A scripted system teaches them to wait for the next scheduled event. Only one of those feels like a real world.

### [COMMIT] Every weather state has gameplay consequences

Rain, fog, wind, snow, heat, cold, storms all have measurable effects on sound, vision, scent, movement speed, body temperature, fire-starting difficulty, radio reception, and vehicle handling. No cosmetic-only weather. If it changes the sky it changes the game.

### [COMMIT] Weather is readable by eye, not HUD

No temperature readout. No wind-speed icon. No "rain incoming" pop-up. Players learn to read clouds, smell the air (ambient audio cue), feel wind on their character (cloth simulation), see breath in cold (particle). The sky is the UI.

Cross-ref PLAN-UI-HUD.md diegetic-first principle.

### [COMMIT] Local variation, not global weather

Rain on the east coast does not mean rain on the west plateau. The peninsula is small enough that a player can walk from storm to sun in an hour, and that is a gameplay resource. A hunter rained out in the boreal forest might migrate two kilometers south and find clear skies.

### [LIKELY] Seasonal drift

The game's climate shifts over real weeks: a wet spring, a dry late summer, a stormy autumn, a cold winter (if snow biome ships). This gives the world a rhythm and prevents same-weather-forever sessions.

---

## Weather states

### [COMMIT] Clear

Default. No precipitation, variable cloud cover, variable temperature. Player/AI senses operate at baseline.

### [COMMIT] Overcast

Cloud cover 60-100%. No rain. Slight visibility reduction at distance. Colors desaturated. Common precursor to rain.

### [COMMIT] Light rain

Visibility reduced ~20%. Sound propagation: player footsteps attenuated ~40% (wet leaves muffle), but rain itself is ambient white noise that reduces infected detection radius ~30%. Scent tracking by wildlife impaired (wind-blown rain disperses scent). Fire-starting penalty without shelter. Comfort penalty - wet clothing accumulates water weight, slower to dry.

### [COMMIT] Heavy rain

Visibility reduced ~50%. All sound effects of light rain amplified. Puddles form on terrain. Rivers rise (cross-ref flooding). Lightning possible (visual only unless thunderstorm). Heavy movement in mud slower. Fire-starting nearly impossible outdoors. Radio reception degraded (cross-ref PLAN-Radio-Comms.md atmospheric interference).

### [COMMIT] Thunderstorm

Heavy rain + thunder + lightning. **Thunder is a weapon.** Each thunderclap masks player gunshots, footfalls, construction noise, and vehicle engines. Thunderstorms are the DayZ-style "cover your tracks under the storm" player narrative. Lightning can strike tall structures and metal objects (low chance, non-scripted). A player standing on a radio tower in a thunderstorm is asking for trouble.

### [COMMIT] Fog

Visibility drops to 30-80 meters depending on density. Applies to player AND AI (infected, wildlife, cryptids all hunt by sound/scent in fog). Fog favors the sneaker and the trapper. Fog is terrifying in a Mothfolk-territory forest. Fog is why the Pale Woman cryptid (cross-ref PLAN-Lore-History.md) kills her victims in urban ruins at dawn.

### [LIKELY] Mist (morning)

Light fog, common at dawn especially near water. Visibility ~150m. Lifts within an hour of sunrise on clear days.

### [COMMIT] Wind (standalone state, layered over others)

Wind has **direction and speed**. Affects:

- **Scent propagation** - animals smell the player from upwind
- **Sound propagation** - gunshots carry farther downwind, are muffled upwind
- **Projectile flight** - arrows and long-range bullets drift with wind
- **Fire behavior** - fires spread in the wind direction, require shelter to start in high wind
- **Cloth simulation** - player coat, flags, laundry lines react to wind (visual indicator)
- **Vegetation** - grass and trees move in the wind direction (readable by player)

Wind is the single most impactful ambient parameter after rain.

### [COMMIT] Heat / hot day

Summer daytime high. Accelerates player thirst (cross-ref PLAN-Survival-Needs.md). Meat spoils faster. Distance heat-shimmer visual. Infected move slower but pursue longer. Water sources more important, rivers lower.

### [COMMIT] Cold / cold day

Autumn/winter day (or high-altitude mountain). Drops player body temperature. Requires insulated clothing or fire to maintain. Breath visible. Water freezes at coast/lakes (shallow only) in deep winter. Infected move slower, aggregate near heat sources (players' fires!). Metal weapons cold to the touch (flavor).

### [LIKELY] Snow

DEFERRED at biome level (cross-ref PLAN-World-Biomes-Regions.md - Snow biome is DEFER). Mountain peak and extreme winter can produce light snow statewide. If Snow biome ships, add:
- Tracks in snow persist until snowfall or thaw (hunter heaven, stealth hell)
- Snowfall reduces visibility similar to heavy rain
- Body temperature impact severe
- Vehicle traction reduced

### [COMMIT] Wet/dry ground states

Ground carries a "wetness" parameter that accumulates during rain and dries out over time (faster in heat, slower in shade). Wet ground:

- Holds tracks longer and deeper (cross-ref PLAN-Animal-Wildlife-Hunting-Fishing.md tracking)
- Silences footsteps
- Grows mushrooms and wild herbs faster (cross-ref PLAN-Crafting.md foraging)
- Creates mud traps in low terrain that slow vehicles

---

## Climate and seasons

### [COMMIT] In-game year length

One in-game year = roughly 2 real weeks. Each season ~3.5 real days. Day/night cycle at 1:48 real-to-game ratio means approximately 40-minute real day and 40-minute real night (cross-ref PLAN-Day-Night-Cycle.md for tuning).

### [COMMIT] Season effects

| Season | Dominant weather | Temperature | Wildlife | Hunting | Other |
|---|---|---|---|---|---|
| Spring | Wet, mild | Cool-mild | Migration return, spawning | Fish abundant, salmon run 1 | Storms frequent |
| Summer | Dry, hot | Hot | Mature populations | Deer active at dawn/dusk | Fire risk high, water critical |
| Autumn | Stormy, cooling | Cool | Rut (deer), pre-migration | Deer rut = aggressive bucks | Leaves change (visual), harvest season |
| Winter | Cold, clear or snowy | Cold-very cold | Hibernation, scarcity | Hard - but tracks visible in frost | Ice on lake edges, body-temp survival |

### [LIKELY] Server-level season offset

Server admins can set which season a new server starts in. Different regional servers might run offset seasons so a community can play "autumn in the east, spring in the west."

### [DEFER] Multi-year arcs

Long-term climate drift (a particularly wet year, a year of late frosts) DEFER for post-v1.0.

---

## Weather effects on other systems

### [COMMIT] Vision and visibility

| State | Max visibility | Notes |
|---|---|---|
| Clear day | 2+ km | Horizon distance terrain-limited |
| Overcast | 1.5 km | Slightly flat colors |
| Light rain | 800 m | Wet sheen on surfaces |
| Heavy rain | 300 m | Obscuring wall of precipitation |
| Thunderstorm | 200 m | Plus lightning flashes |
| Fog (light) | 150 m | Gradient into white/grey |
| Fog (heavy) | 30-50 m | Near-blindness beyond |
| Night clear | 100 m eye, further w/ moon | See PLAN-Vision.md night vision |
| Night storm | 20 m | Flashlights essential |

AI (infected, wildlife, cryptids) vision scales with the same values. A hunter CAN hide in fog from a wolf. A wolf CAN ambush a hunter in fog.

### [COMMIT] Sound propagation (critical interaction with infected)

Cross-ref PLAN-Infected-AI.md. Infected react to sound within a radius that varies by weather:

| Sound event | Clear | Light rain | Heavy rain | Thunder (active) | Heavy wind |
|---|---|---|---|---|---|
| Footstep (normal) | 8m | 4m | 2m | 0m | 3m |
| Footstep (sprint) | 25m | 15m | 8m | 3m | 10m |
| Pistol | 180m | 140m | 80m | 0m during thunder | 120m |
| Rifle | 400m | 320m | 200m | 0m during thunder | 280m |
| Shotgun | 250m | 200m | 120m | 0m during thunder | 180m |
| Bow release | 15m | 8m | 4m | 0m | 6m |
| Chopping wood | 60m | 40m | 20m | 0m during thunder | 40m |
| Vehicle engine | 200m | 160m | 100m | 0m during thunder | 140m |

Numbers approximate. Thunderclaps mask completely for their duration. A thunderstorm is the player's best stealth window.

### [COMMIT] Scent propagation (wildlife and cryptid tracking)

Cross-ref PLAN-Animal-Wildlife-Hunting-Fishing.md. Wildlife scent radius reduced ~50% in rain, ~30% in wind, near-eliminated in heavy rain. A player approaches a deer downwind on a rainy day and the deer does not know they're there until visible.

### [COMMIT] Fire-starting difficulty

| Condition | Fire difficulty |
|---|---|
| Clear, dry, no wind | Easy |
| Light wind | Slight delay |
| High wind | Requires shelter |
| Light rain | Requires shelter |
| Heavy rain | Requires shelter + dry tinder |
| Thunderstorm | Near-impossible outside |
| Cold | Slower to start, consumes more fuel |
| Very cold | Requires tinder + shelter |

Cross-ref PLAN-Base-Building.md for shelter and hearth designs.

### [COMMIT] Water sources

- Rainwater collectable (barrels, tarps) during rain
- Rivers rise during heavy rain, flood lowlands
- Ponds fill and drain with the season
- Coastal tide cycle independent of weather
- Summer drought can drop river levels, expose shortcut crossings

### [COMMIT] Radio atmospheric effects

Cross-ref PLAN-Radio-Comms.md. Thunderstorms disrupt radio (static, garbled transmissions). Solar activity (DEFER) could affect high-band comms. Calm high-pressure days provide best range (players learn to time long-distance broadcasts).

### [LIKELY] Vehicle handling

Cross-ref PLAN-Vehicles.md. Wet roads reduce traction, mud is worse, snow is worst (if snow ships). Flooded rivers impassable until waters recede.

### [COMMIT] Body temperature

Cross-ref PLAN-Survival-Needs.md. Rain wets player clothing (wet clothing drops insulation). Cold wet is hypothermia territory within 30 minutes without fire/shelter. Heat + heavy clothing = heat exhaustion. Clothing layers matter (cross-ref PLAN-Clothing-Storage.md).

### [LIKELY] Infected and cryptid behavior modifiers

- **Infected** move slower in cold, faster in heat, aggregate near heat sources in cold, disperse in heavy rain (water disrupts their sound-tracking)
- **Cryptids** mostly unaffected by weather - they're adapted to their biomes. The Pale Woman specifically hunts in fog. The Sodden is strongest in rain. The Caller's acoustic hunt is disrupted by loud rain.

---

## Visual and audio design

### [COMMIT] Volumetric clouds and sky rendering

Real sky. Dynamic cloud formation and movement. Sunset/sunrise light. Moon phase visible at night. Stars at night (cross-ref PLAN-Day-Night-Cycle.md). The sky is roughly 30% of a player's screen at any time; it needs to look good.

### [COMMIT] Rain rendering

- Streaking precipitation with velocity-aware motion
- Splash particles on hard surfaces
- Wet sheen (screen-space reflections) on cars, concrete, metal
- Puddles forming in low terrain
- Raindrops on visible weapon/camera (first-person detail)

### [COMMIT] Fog rendering

- Volumetric, not screen-space-post-effect
- Can be walked through (visibility decreases gradually)
- Interacts with light sources (lanterns cast cone through fog, flashlights dramatic)

### [COMMIT] Wind animation

- Vegetation responds to wind direction and speed (bending, rustling)
- Cloth items (clothing, flags, tents) react
- Particle systems (falling leaves, embers from fire) drift
- Water surfaces ripple

### [COMMIT] Audio

- Ambient weather audio (rain patter, thunder rumble, wind moan) mixed correctly with stereo/position
- Thunder spatialized (direction of strike audible, delay between flash and clap used for distance - real physics)
- Indoor attenuation (rain muffled indoors, louder near open doors/windows)
- Weather interacts with ambient biome audio (bird calls suppressed in rain, etc.)

### [LIKELY] Screen-surface effects

Rain on goggles/visor (if player wears one), condensation on breath-warmed windows, mud splash from vehicles. Optional, atmospheric.

### [REJECT] Weather HUD overlays

No "Raining" text. No temperature gauge. No forecast. The sky is the UI.

---

## Extreme weather events

### [LIKELY] Storms (named)

Named major storm events - seasonal, ~1 per in-game year - that sweep the peninsula with extreme effects:

- **Hurricane / tropical storm** (summer/autumn) - coastal flooding, sustained extreme wind, 24-72 real hours, ships are grounded, outdoor bases take damage
- **Ice storm** (winter) - ice coats surfaces, trees fall under weight, slip hazard, vehicles immobile, fires hard to start outdoors
- **Wildfire** (dry summer) - can start from lightning, spreads with wind direction, burns forest biome, rewrites terrain for weeks, infected flee ahead of it and create mass migration events
- **Dense fog bank** (any season, rare) - visibility 10m peninsula-wide for 6-12 hours, extremely dangerous but enables bold player movements
- **Heavy snowfall** (winter, if snow ships) - accumulates, buries tracks, creates ice caves on coast

Named events are the weather system's "boss moments" - players will remember "the week of the hurricane" as a server story.

### [LIKELY] Atmospheric anomalies (late-game lore)

Cross-ref PLAN-Lore-History.md. As Cascade 5 (if Captain chooses B) approaches in late game, weather becomes subtly wrong:
- Sky colors shift toward sickly tones
- Unnatural storms (no lightning, no thunder, just pressure)
- Rain containing faint trace elements (testing a collected sample reveals unknown chemistry)
- Aurora-like atmospheric lights at latitudes they shouldn't appear

These are late-game lore cues delivered through weather rather than dialog. Perfect diegetic storytelling.

### [REJECT] Weather "magic"

No mystical storms that "only appear when the hero approaches." No "clear skies on finale day." Weather is weather. If a plot beat needs specific weather, waiting is a valid quest (a quest giver says "storm passes in two days, meet me then"). Players should not feel dramaturgy puppeting the sky.

---

## Player interaction

### [COMMIT] Readability - teach the player the sky

Pre-Cascade almanacs, found in old farmhouses and meteorologist offices, teach players real cloud formations and what they predict. A player who reads the "Old Farmer's Almanac 2029" (a collectible holotape-equivalent) learns to identify:
- **Cirrus clouds** - fair weather approaching
- **Cumulus congestus** - possible afternoon thunderstorms
- **Mammatus** - severe storm already happening
- **Nimbostratus** - prolonged steady rain

These are real phenomena. Teaching players real meteorology via in-game items is the Fallout-holotape-philosophy applied to weather.

### [LIKELY] Weather intuition skill

Cross-ref PLAN-Player-Progression.md. A skill track for reading weather, unlocking:
- Basic: "storm coming" visual hint
- Mid: rough temperature sense in HUD
- Advanced: accurate rain timing (30-minute window) via sky reading
- Expert: long-range forecasting (6+ hours) via cloud and pressure cues

All tuned to NOT replace the sky-as-UI principle. These are interpretive aids, not HUD replacements.

### [COMMIT] Weather-based questing

Radio broadcasts (Radio Greta, cross-ref PLAN-Lore-History.md) reference current weather. Settlers NPC quests reference current weather ("we need to bring in the harvest before the storm"). Some quests trigger only during specific weather (the drowned body appears after heavy rain washes it downstream, the trail follows only visible in mud).

---

## Technical implementation notes

### [COMMIT] Weather simulation runs server-side

Server authoritative. All clients see consistent weather. No desync between players in the same region.

### [COMMIT] Regional grid

Peninsula divided into weather cells (~1 km on a side). Each cell has independent temperature, humidity, wind, precipitation. Cells interact at boundaries (storms advect, fronts form). Player-perceivable at cell boundaries as "it's raining here but clear over there."

### [LIKELY] Cloud advection

Clouds move at realistic speeds. A player who sees a storm front at 5 km can estimate time-to-arrival.

### [LIKELY] Precipitation accumulation tracking

Per-cell wetness parameter (0-1). Drives puddle rendering, fire-start penalty, track preservation.

### [UNDECIDED] Weather saved in world state

If a server restarts, does the weather state persist? Recommendation: yes, save last state on clean shutdown, resume. For ungraceful shutdowns, initialize from seasonal climate.

### [UNDECIDED] Client-side vs server-side rendering

Clouds and sky can be client-rendered from server-shared parameters. Rain particles and wetness are client-rendered from server wetness states. The server does NOT need to render - it just updates the state that clients draw. This keeps bandwidth low.

---

## Dependencies and cross-references

| Plan | How this plan relates |
|---|---|
| [PLAN-World-Biomes-Regions.md](PLAN-World-Biomes-Regions.md) | Biomes have climate baselines |
| [PLAN-Day-Night-Cycle.md](PLAN-Day-Night-Cycle.md) | Day/night interacts with weather (night storm, dawn fog) |
| [PLAN-Animal-Wildlife-Hunting-Fishing.md](PLAN-Animal-Wildlife-Hunting-Fishing.md) | Weather affects scent, tracks, wildlife behavior |
| [PLAN-Infected-AI.md](PLAN-Infected-AI.md) | Rain/thunder masks sound; infected thermal behavior |
| [PLAN-Survival-Needs.md](PLAN-Survival-Needs.md) | Body temperature, thirst acceleration in heat |
| [PLAN-Clothing-Storage.md](PLAN-Clothing-Storage.md) | Wet clothing, insulation layers |
| [PLAN-Combat.md](PLAN-Combat.md) | Wind affects projectile flight, visibility in fog |
| [PLAN-Radio-Comms.md](PLAN-Radio-Comms.md) | Atmospheric radio interference |
| [PLAN-Vehicles.md](PLAN-Vehicles.md) | Road conditions, mud traps, flood barriers |
| [PLAN-Base-Building.md](PLAN-Base-Building.md) | Shelter from weather, hearth positioning |
| [PLAN-Environment-Hazards.md](PLAN-Environment-Hazards.md) | Lightning strikes, wildfire spread |
| [PLAN-Audio-Design.md](PLAN-Audio-Design.md) | Weather audio layers |
| [PLAN-Vision.md](PLAN-Vision.md) | Fog and rain visibility, night vision interaction |
| [PLAN-Dynamic-World-Events.md](PLAN-Dynamic-World-Events.md) | Named storm events, wildfire events |
| [PLAN-Lore-History.md](PLAN-Lore-History.md) | Late-game atmospheric anomalies |
| [PLAN-Player-Progression.md](PLAN-Player-Progression.md) | Weather intuition skill |
| [PLAN-UI-HUD.md](PLAN-UI-HUD.md) | No weather HUD - sky-as-UI principle |

---

## Open Questions (Captain's call required)

1. **In-game year length** (proposal: ~2 real weeks)
2. **Snow biome - in or DEFER?** (current: DEFER; affects whether winter snowfall system ships)
3. **Named storm events count** (proposal: ~5 possible events, 1 random per in-game year)
4. **Wildfire as a persistent terrain-changing event** (proposal: yes, burns mark terrain for 2+ weeks; ambitious)
5. **Client-side rendering parameters set by server** (proposal: yes; defer heavy weather tech to client)
6. **Server-restart weather persistence** (proposal: yes for graceful, seasonal init for ungraceful)
7. **Atmospheric anomalies late-game** (proposal: yes, subtle; tied to Cascade 5 arc)
8. **Real-world cloud formation science in almanacs** (proposal: yes, teaches real weather literacy)
9. **Accessibility: visual-only weather cues for hearing-impaired players** (proposal: yes - every weather sound has a visual cue; cross-ref PLAN-Accessibility, which has not been written yet - Tuvok TODO)

---

## Style notes

- **Weather is a mood-setter.** The fog in the morning, the storm at dusk, the first snow of winter - these are emotional beats as much as gameplay beats.
- **The peninsula has a temperament.** Writers working on regional flavor should think about each biome's "weather personality" - the boreal forest is wet and often fogged, the plains are wind-swept and clear, the coast is storm-prone, Coalton Refinery is smoggy.
- **Silence is a weather state.** The calm after a storm, the heavy air before lightning strikes, the still air in fog. Absence of weather-sound is itself a signal.

---

_End of plan. Systems team: weather is an ambitious simulation system. Phase in. Start with state machine + effects on existing systems (sound, vision). Clouds, particles, and atmosphere rendering can follow._
